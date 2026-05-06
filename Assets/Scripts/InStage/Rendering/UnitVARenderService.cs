using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Per-instance draw request for a single VA unit.
/// </summary>
public struct UnitVADrawRequest
{
    public UnitVASO VASO;
    public Matrix4x4 Matrix;
    public int GlobalFrameIndexA;
    public int GlobalFrameIndexB;
    public float BlendWeight;
}

/// <summary>
/// Renders vertex-animated units via Graphics.RenderMeshInstanced.
///
/// Batches by UnitVASO (same Mesh + BaseTexture + ComputeBuffer → same draw call).
/// Each instance receives a _VA_FrameOffset float property that the shader uses
/// to index into the StructuredBuffer&lt;float2&gt; _VAPositions.
///
/// Singleton pattern: UnitVARenderService.Shared (pure C#, not MonoBehaviour).
/// Follows the same Clear → Enqueue → Flush rhythm as UnitAtlasBillboardRenderService.
/// </summary>
public sealed class UnitVARenderService
{
    public static UnitVARenderService Shared { get; } = new UnitVARenderService();

    private const int MaxInstancesPerDraw = 1023;
    private const int UnitRenderQueue = 3010;
    private const int UnitRendererPriority = 30;

    private readonly Dictionary<UnitVASO, BatchData> _batches = new Dictionary<UnitVASO, BatchData>();
    private readonly Dictionary<UnitVASO, Material> _materialCache = new Dictionary<UnitVASO, Material>();
    private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

    private Shader _shader;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Clear all batch lists for a new frame.
    /// </summary>
    public void Clear()
    {
        foreach (BatchData batch in _batches.Values)
        {
            batch.Matrices.Clear();
            batch.FrameOffsetsA.Clear();
            batch.FrameOffsetsB.Clear();
            batch.BlendWeights.Clear();
        }
    }

    /// <summary>
    /// Enqueue a VA unit for rendering this frame.
    /// </summary>
    public void Enqueue(UnitVADrawRequest request)
    {
        if (request.VASO == null)
        {
            return;
        }

        if (!_batches.TryGetValue(request.VASO, out BatchData batch))
        {
            batch = new BatchData();
            _batches.Add(request.VASO, batch);
        }

        batch.Matrices.Add(request.Matrix);
        batch.FrameOffsetsA.Add(request.GlobalFrameIndexA);
        batch.FrameOffsetsB.Add(request.GlobalFrameIndexB);
        batch.BlendWeights.Add(request.BlendWeight);
    }

    /// <summary>
    /// Submit all batched draw calls.
    /// </summary>
    public void Flush()
    {
        Shader shader = GetShader();
        if (shader == null)
        {
            return;
        }

        foreach ((UnitVASO vaso, BatchData batch) in _batches)
        {
            if (vaso == null || batch.Matrices.Count == 0)
            {
                continue;
            }

            if (vaso.Mesh == null || vaso.BaseTexture == null)
            {
                continue;
            }

            if (!UnitVABufferManager.Instance.TryGetBufferData(vaso, out UnitVABufferManager.VABufferData bufferData))
            {
                continue;
            }

            if (bufferData.PositionBuffer == null)
            {
                continue;
            }

            Material material = GetOrCreateMaterial(vaso, shader, bufferData);
            if (material == null)
            {
                continue;
            }

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = _propertyBlock,
                rendererPriority = UnitRendererPriority
            };

            for (int start = 0; start < batch.Matrices.Count; start += MaxInstancesPerDraw)
            {
                int count = Mathf.Min(MaxInstancesPerDraw, batch.Matrices.Count - start);
                List<Matrix4x4> matricesSlice = batch.Matrices.GetRange(start, count);
                List<int> frameOffsetsASlice = batch.FrameOffsetsA.GetRange(start, count);
                List<int> frameOffsetsBSlice = batch.FrameOffsetsB.GetRange(start, count);
                List<float> blendWeightsSlice = batch.BlendWeights.GetRange(start, count);

                _propertyBlock.Clear();

                float[] offsetsAFloat = new float[count];
                float[] offsetsBFloat = new float[count];
                for (int i = 0; i < count; i++)
                {
                    offsetsAFloat[i] = frameOffsetsASlice[i];
                    offsetsBFloat[i] = frameOffsetsBSlice[i];
                }

                _propertyBlock.SetFloatArray("_VA_FrameOffset", offsetsAFloat);
                _propertyBlock.SetFloatArray("_VA_FrameOffset2", offsetsBFloat);
                _propertyBlock.SetFloatArray("_VA_BlendWeight", blendWeightsSlice.ToArray());

                Graphics.RenderMeshInstanced(renderParams, vaso.Mesh, 0, matricesSlice);
            }
        }
    }

    /// <summary>
    /// Drop all cached materials. Called on stage exit so stale ComputeBuffer
    /// references are not held.
    /// </summary>
    public void ClearCache()
    {
        _materialCache.Clear();
        _shader = null;
    }

    // -----------------------------------------------------------------------
    // Internal
    // -----------------------------------------------------------------------

    private Shader GetShader()
    {
        if (_shader == null)
        {
            _shader = Shader.Find("Custom/UnitVAShader");
        }

        return _shader;
    }

    private Material GetOrCreateMaterial(UnitVASO vaso, Shader shader, UnitVABufferManager.VABufferData bufferData)
    {
        if (_materialCache.TryGetValue(vaso, out Material material) && material != null)
        {
            return material;
        }

        material = new Material(shader);
        material.enableInstancing = true;
        material.renderQueue = UnitRenderQueue;
        material.SetTexture("_MainTex", vaso.BaseTexture);
        material.SetBuffer("_VAPositions", bufferData.PositionBuffer);
        material.SetInt("_VAVertexCount", bufferData.VertexCount);
        material.SetFloat("_VA_DisplayScale", vaso.DisplayScale);

        _materialCache[vaso] = material;
        return material;
    }

    // -----------------------------------------------------------------------
    // Internal types
    // -----------------------------------------------------------------------

    private sealed class BatchData
    {
        public List<Matrix4x4> Matrices { get; } = new List<Matrix4x4>(1024);
        public List<int> FrameOffsetsA { get; } = new List<int>(1024);
        public List<int> FrameOffsetsB { get; } = new List<int>(1024);
        public List<float> BlendWeights { get; } = new List<float>(1024);
    }
}
