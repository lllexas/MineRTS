using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GPU buffer manager for UnitVASO vertex animation data.
///
/// Lifecycle:
///   Lazy created by SingletonMono on first access.
///   RegisterVASO on first encounter in DrawSystem (flatten + upload ComputeBuffer).
///   ReleaseAll on stage exit (ClearWorld).
///
/// Buffer layout (per UnitVASO):
///   Flat Vector2[]: all frames concatenated in clip order.
///   [clip0_frame0_vert0, clip0_frame0_vert1, ..., clip0_frame1_vert0, ...]
///   ComputeBuffer stride = 8 (sizeof float2).
///   Shader reads via StructuredBuffer&lt;float2&gt; _VAPositions.
///
/// Clip offset table maps UnitAnimationStateId → global frame index range.
/// </summary>
public sealed class UnitVABufferManager : SingletonMono<UnitVABufferManager>
{
    // -----------------------------------------------------------------------
    // Inspector: interceptor chain priority (read-only, synced from VAInterceptorChain)
    // -----------------------------------------------------------------------

    [Header("Animation State Arbitration")]
    [Tooltip("Interceptor chain in priority order. First match wins.")]
    [SerializeField] private List<InterceptorEntry> _interceptorChain = new List<InterceptorEntry>();

    [Serializable]
    private struct InterceptorEntry
    {
        [HideInInspector] public int Priority;
        public string StateName;
        public string Condition;
    }

    /// <summary>
    /// Per-clip metadata stored on GPU side of the manager.
    /// GlobalFrameStart is the first frame index of this clip inside the flat buffer.
    /// </summary>
    [Serializable]
    public struct VAClipGPUInfo
    {
        public UnitAnimationStateId State;
        public int GlobalFrameStart;
        public int FrameCount;
        public int TicksPerFrame;
        public bool Loop;
    }

    /// <summary>
    /// All GPU resources and metadata for a single registered UnitVASO.
    /// </summary>
    public sealed class VABufferData
    {
        public ComputeBuffer PositionBuffer;
        public int VertexCount;
        public int TotalFrames;
        public UnitVASO SourceVASO;
        public Dictionary<UnitAnimationStateId, VAClipGPUInfo> ClipMap;

        public VABufferData()
        {
            ClipMap = new Dictionary<UnitAnimationStateId, VAClipGPUInfo>();
        }
    }

    private readonly Dictionary<UnitVASO, VABufferData> _bufferData = new Dictionary<UnitVASO, VABufferData>();

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Register a UnitVASO: flatten all frames into a flat Vector2 array,
    /// upload to a ComputeBuffer, and build the clip offset table.
    /// Idempotent – returns false if already registered.
    /// </summary>
    public bool RegisterVASO(UnitVASO vaso)
    {
        if (vaso == null)
        {
            Debug.LogWarning("[UnitVABufferManager] RegisterVASO: vaso is null.");
            return false;
        }

        if (_bufferData.ContainsKey(vaso))
        {
            return false;
        }

        if (!vaso.HasExpectedFrameVertexCounts())
        {
            Debug.LogError($"[UnitVABufferManager] VASO '{vaso.name}' failed frame vertex count check. Skip registration.");
            return false;
        }

        int vertexCount = vaso.VertexCount;
        int totalFrames = vaso.TotalFrameCount;
        if (vertexCount <= 0 || totalFrames <= 0)
        {
            Debug.LogWarning($"[UnitVABufferManager] VASO '{vaso.name}' has VertexCount={vertexCount}, TotalFrames={totalFrames}. Skip registration.");
            return false;
        }

        // Flatten all clips → frames → vertex positions into one contiguous array.
        Vector2[] flatPositions = new Vector2[totalFrames * vertexCount];
        VABufferData data = new VABufferData();
        int nextGlobalFrame = 0;
        int writeIndex = 0;

        for (int clipIndex = 0; clipIndex < vaso.Clips.Count; clipIndex++)
        {
            UnitVAClip clip = vaso.Clips[clipIndex];
            if (clip == null || clip.FrameCount <= 0)
            {
                continue;
            }

            VAClipGPUInfo info = new VAClipGPUInfo
            {
                State = clip.State,
                GlobalFrameStart = nextGlobalFrame,
                FrameCount = clip.FrameCount,
                TicksPerFrame = clip.TicksPerFrame,
                Loop = clip.Loop
            };

            for (int frameIndex = 0; frameIndex < clip.FrameCount; frameIndex++)
            {
                UnitVAFrame frame = clip.Frames[frameIndex];
                if (frame?.Positions == null)
                {
                    // Fill with zeros for missing frames (shouldn't happen after validation).
                    for (int v = 0; v < vertexCount; v++)
                    {
                        flatPositions[writeIndex++] = Vector2.zero;
                    }
                }
                else
                {
                    for (int v = 0; v < vertexCount; v++)
                    {
                        flatPositions[writeIndex++] = v < frame.Positions.Length
                            ? frame.Positions[v]
                            : Vector2.zero;
                    }
                }

                nextGlobalFrame++;
            }

            // Key by state. If multiple clips share the same state, last one wins.
            data.ClipMap[info.State] = info;
        }

        // Create GPU buffer.
        int bufferCount = totalFrames * vertexCount;
        ComputeBuffer buffer = new ComputeBuffer(bufferCount, sizeof(float) * 2, ComputeBufferType.Structured);
        buffer.SetData(flatPositions);

        data.PositionBuffer = buffer;
        data.VertexCount = vertexCount;
        data.TotalFrames = totalFrames;
        data.SourceVASO = vaso;

        _bufferData[vaso] = data;

        float bufferSizeKB = (bufferCount * 8L) / 1024f;
        Debug.Log($"[UnitVABufferManager] Registered '{vaso.name}': {vertexCount} verts, {totalFrames} total frames, " +
                  $"{vaso.Clips.Count} clips, {bufferSizeKB:F1} KB buffer.");

        return true;
    }

    /// <summary>
    /// Convert two local frames for the same state to global buffer offsets.
    /// Used by UnitVAInterpolator for sub-tick interpolation between frames.
    /// </summary>
    public bool TryGetGlobalFrameIndices(
        UnitVASO vaso, UnitAnimationStateId state,
        int localFrameA, int localFrameB,
        out int globalA, out int globalB)
    {
        globalA = 0;
        globalB = 0;
        if (vaso == null || !_bufferData.TryGetValue(vaso, out VABufferData data))
        {
            return false;
        }

        if (!data.ClipMap.TryGetValue(state, out VAClipGPUInfo info))
        {
            return false;
        }

        int clampedA = Mathf.Clamp(localFrameA, 0, Mathf.Max(0, info.FrameCount - 1));
        int clampedB = Mathf.Clamp(localFrameB, 0, Mathf.Max(0, info.FrameCount - 1));
        globalA = info.GlobalFrameStart + clampedA;
        globalB = info.GlobalFrameStart + clampedB;
        return true;
    }

    /// <summary>
    /// Convert a resolved state + local frame to the global (flat) frame index
    /// used by the StructuredBuffer.
    /// </summary>
    public bool TryGetGlobalFrameIndex(UnitVASO vaso, UnitAnimationStateId state, int localFrame, out int globalFrameIndex)
    {
        globalFrameIndex = 0;
        if (vaso == null || !_bufferData.TryGetValue(vaso, out VABufferData data))
        {
            return false;
        }

        if (!data.ClipMap.TryGetValue(state, out VAClipGPUInfo info))
        {
            return false;
        }

        int clampedLocalFrame = Mathf.Clamp(localFrame, 0, Mathf.Max(0, info.FrameCount - 1));
        globalFrameIndex = info.GlobalFrameStart + clampedLocalFrame;
        return true;
    }

    /// <summary>
    /// Expose buffer data for the render service to bind to materials.
    /// </summary>
    public bool TryGetBufferData(UnitVASO vaso, out VABufferData data)
    {
        return _bufferData.TryGetValue(vaso, out data);
    }

    /// <summary>
    /// Quick check whether a VASO has been registered.
    /// </summary>
    public bool IsRegistered(UnitVASO vaso)
    {
        return vaso != null && _bufferData.ContainsKey(vaso);
    }

    /// <summary>
    /// Release all ComputeBuffers and clear the registry.
    /// Also notifies UnitVARenderService to drop cached materials
    /// (those materials hold references to the released buffers).
    /// </summary>
    public void ReleaseAll()
    {
        foreach (VABufferData data in _bufferData.Values)
        {
            data.PositionBuffer?.Release();
            data.PositionBuffer = null;
        }

        _bufferData.Clear();
        UnitVARenderService.Shared.ClearCache();

        Debug.Log("[UnitVABufferManager] All VA buffers released.");
    }

    // -----------------------------------------------------------------------
    // Singleton lifecycle
    // -----------------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();
        SyncInterceptorChainView();
    }

    private void SyncInterceptorChainView()
    {
        VAInterceptorChain.EnsureInitialized();
        IReadOnlyList<VAInterceptorInfo> interceptors = VAInterceptorChain.Interceptors;

        _interceptorChain.Clear();
        for (int i = 0; i < interceptors.Count; i++)
        {
            _interceptorChain.Add(new InterceptorEntry
            {
                Priority = interceptors[i].Priority,
                StateName = interceptors[i].Name,
                Condition = interceptors[i].Description
            });
        }
    }

    private void OnDestroy()
    {
        ReleaseAll();
    }
}
