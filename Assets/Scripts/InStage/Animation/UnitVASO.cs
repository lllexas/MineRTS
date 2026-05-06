using System;
using System.Collections.Generic;
using UnityEngine;

public static class UnitVASettings
{
    /// <summary>
    /// Project-level default for Spine-to-VA baking and clip preview playback.
    /// A preview speed of 1 means one baked frame is advanced at this FPS.
    /// Individual UnitVASO assets may override BakeSampleFps; this value is only
    /// the default used when a new or invalid asset value needs initialization.
    /// </summary>
    public const int DefaultBakeSampleFps = 60;

    public const int MinBakeSampleFps = 1;

    public static int NormalizeBakeSampleFps(int sampleFps)
    {
        return sampleFps < MinBakeSampleFps ? DefaultBakeSampleFps : sampleFps;
    }
}

[Serializable]
public sealed class UnitVAFrame
{
    /// <summary>
    /// All vertex positions for this animation frame, in mesh vertex order.
    /// Layout rule for this authoring asset:
    /// clip -> frame -> vertex -> xy.
    /// Positions[vertexIndex] is the local-space xy of that mesh vertex at this frame.
    /// This array is intentionally not a serialized GPU buffer. Runtime upload code may
    /// flatten and quantize all frames later, but this asset stores the baked VA structure
    /// in the same hierarchy that the Spine baker produces.
    /// </summary>
    public Vector2[] Positions = Array.Empty<Vector2>();
}

[Serializable]
public sealed class UnitVAClip
{
    public string SourceAnimationName;
    public UnitAnimationStateId State;

    [Min(1)] public int TicksPerFrame = 1;
    public bool Loop = true;
    public bool LockUntilComplete;

    /// <summary>
    /// Frames belonging to this clip. Frames are stored contiguously inside the clip, and
    /// every frame must contain exactly UnitVASO.VertexCount xy entries in mesh vertex order.
    /// Runtime buffer layout, if needed, should be derived from clip order then frame order.
    /// </summary>
    public List<UnitVAFrame> Frames = new List<UnitVAFrame>();

    public int FrameCount => Frames?.Count ?? 0;
}

[CreateAssetMenu(fileName = "UnitVASO", menuName = "MineRTS/Animation/Unit VASO")]
public sealed class UnitVASO : ScriptableObject
{
    [Header("Identity")]
    public string UnitTypeId;

    [Header("Source Trace")]
    public TextAsset SourceJson;
    public UnityEngine.Object SourceSkeletonDataAsset;
    public string SourceAssetGuid;
    public string SourceAssetPath;
    public string SourceSpineVersion;
    /// <summary>
    /// When true, all baked vertex positions are mirrored on X and triangle winding
    /// is reversed so the unit faces right (the project convention) regardless of the
    /// source Spine asset's original facing direction.
    /// </summary>
    public bool FlipHorizontal;
    /// <summary>
    /// Sampling FPS used by the Spine baker and by the editor clip preview.
    /// The default is UnitVASettings.DefaultBakeSampleFps, currently 60, matching
    /// the expected Spine authoring cadence. Values below 1 are repaired to the
    /// project default; valid per-asset overrides are otherwise preserved.
    /// </summary>
    [Min(1)] public int BakeSampleFps = UnitVASettings.DefaultBakeSampleFps;

    [Header("Static Runtime Assets")]
    public Mesh Mesh;
    public Texture2D BaseTexture;
    /// <summary>
    /// Per-asset display scale applied in the vertex shader (not CPU-side).
    /// Multiplied directly onto VA positions before world transform.
    /// Tune this so the unit height reads roughly 1.2 world units.
    /// </summary>
    [Min(0.01f)] public float DisplayScale = 1f;

    [Header("VA Layout")]
    [Min(0)] public int VertexCount;

    [Header("Clips")]
    /// <summary>
    /// Baked vertex animation data grouped by clip, then by frame.
    /// This SO is the conversion result of Spine organization -> VA organization.
    /// It should usually be generated next to the source Spine json/atlas/texture files
    /// so the derived asset is easy to inspect and regenerate.
    /// </summary>
    public List<UnitVAClip> Clips = new List<UnitVAClip>();

    public int TotalFrameCount
    {
        get
        {
            int total = 0;
            if (Clips == null)
            {
                return total;
            }

            for (int i = 0; i < Clips.Count; i++)
            {
                total += Clips[i]?.FrameCount ?? 0;
            }

            return total;
        }
    }

    public int ExpectedPositionCount => Mathf.Max(0, VertexCount) * TotalFrameCount;

    public bool TryGetClip(UnitAnimationStateId state, out UnitVAClip clip)
    {
        if (Clips != null)
        {
            for (int i = 0; i < Clips.Count; i++)
            {
                UnitVAClip candidate = Clips[i];
                if (candidate != null && candidate.State == state)
                {
                    clip = candidate;
                    return true;
                }
            }
        }

        clip = null;
        return false;
    }

    public bool TryGetPosition(UnitVAClip clip, int frameIndex, int vertexIndex, out Vector2 position)
    {
        if (clip?.Frames == null ||
            frameIndex < 0 ||
            frameIndex >= clip.Frames.Count ||
            vertexIndex < 0 ||
            vertexIndex >= VertexCount)
        {
            position = default;
            return false;
        }

        UnitVAFrame frame = clip.Frames[frameIndex];
        if (frame?.Positions == null || vertexIndex >= frame.Positions.Length)
        {
            position = default;
            return false;
        }

        position = frame.Positions[vertexIndex];
        return true;
    }

    public bool HasExpectedFrameVertexCounts()
    {
        if (Clips == null)
        {
            return true;
        }

        for (int clipIndex = 0; clipIndex < Clips.Count; clipIndex++)
        {
            UnitVAClip clip = Clips[clipIndex];
            if (clip?.Frames == null)
            {
                return false;
            }

            for (int frameIndex = 0; frameIndex < clip.Frames.Count; frameIndex++)
            {
                UnitVAFrame frame = clip.Frames[frameIndex];
                if (frame?.Positions == null || frame.Positions.Length != VertexCount)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void OnValidate()
    {
        if (Mesh != null)
        {
            VertexCount = Mesh.vertexCount;
        }
        else
        {
            VertexCount = Mathf.Max(0, VertexCount);
        }

        BakeSampleFps = UnitVASettings.NormalizeBakeSampleFps(BakeSampleFps);

        if (Clips == null)
        {
            Clips = new List<UnitVAClip>();
            return;
        }

        for (int clipIndex = 0; clipIndex < Clips.Count; clipIndex++)
        {
            UnitVAClip clip = Clips[clipIndex];
            if (clip == null)
            {
                clip = new UnitVAClip();
                Clips[clipIndex] = clip;
            }

            clip.TicksPerFrame = Mathf.Max(1, clip.TicksPerFrame);
            clip.Frames ??= new List<UnitVAFrame>();

            for (int frameIndex = 0; frameIndex < clip.Frames.Count; frameIndex++)
            {
                UnitVAFrame frame = clip.Frames[frameIndex];
                if (frame == null)
                {
                    frame = new UnitVAFrame();
                    clip.Frames[frameIndex] = frame;
                }

                frame.Positions ??= Array.Empty<Vector2>();
            }
        }
    }
}
