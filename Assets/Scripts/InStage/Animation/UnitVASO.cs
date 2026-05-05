using System;
using System.Collections.Generic;
using UnityEngine;

public enum UnitVAPositionFormat
{
    RG16 = 0,
    RG16F = 1
}

[Serializable]
public struct UnitVAClipDef
{
    public UnitAnimationStateId State;
    /// <summary>
    /// Start frame in the flattened position stream. Frames in the same clip must be contiguous.
    /// </summary>
    [Min(0)] public int FrameStart;
    [Min(0)] public int FrameCount;
    [Min(1)] public int TicksPerFrame;
    public bool Loop;
    public bool LockUntilComplete;
}

[CreateAssetMenu(fileName = "UnitVASO", menuName = "MineRTS/Animation/Unit VASO")]
public sealed class UnitVASO : ScriptableObject
{
    [Header("Identity")]
    public string UnitTypeId;

    [Header("Static Runtime Assets")]
    public Mesh Mesh;
    public Texture2D BaseTexture;

    [Header("VA Layout")]
    [Min(0)] public int VertexCount;
    public UnitVAPositionFormat PositionFormat = UnitVAPositionFormat.RG16F;

    [Header("Clips")]
    public List<UnitVAClipDef> Clips = new List<UnitVAClipDef>();

    [Header("Animation Data")]
    /// <summary>
    /// Flattened vertex animation stream.
    /// Layout:
    /// clip block -> frame block -> vertex block -> xy.
    /// Clips reserve contiguous frame ranges through <see cref="UnitVAClipDef.FrameStart"/> and
    /// <see cref="UnitVAClipDef.FrameCount"/>. Each frame stores all vertices in mesh vertex order.
    /// Index rule:
    /// bufferIndex = ((clip.FrameStart + localFrame) * VertexCount) + vertexIndex.
    /// Byte offset:
    /// byteOffset = bufferIndex * BytesPerVertex.
    /// Per-vertex element order is always x then y.
    /// For RG16: byte 0..1 = x as UInt16, byte 2..3 = y as UInt16.
    /// For RG16F: byte 0..1 = x as Half, byte 2..3 = y as Half.
    /// </summary>
    public byte[] PositionData = Array.Empty<byte>();

    public int BytesPerVertex => PositionFormat switch
    {
        UnitVAPositionFormat.RG16 => 4,
        UnitVAPositionFormat.RG16F => 4,
        _ => 4
    };

    public int TotalFrameCount
    {
        get
        {
            int maxFrame = 0;
            if (Clips == null)
            {
                return maxFrame;
            }

            for (int i = 0; i < Clips.Count; i++)
            {
                UnitVAClipDef clip = Clips[i];
                maxFrame = Mathf.Max(maxFrame, clip.FrameStart + clip.FrameCount);
            }

            return maxFrame;
        }
    }

    public int ExpectedPositionDataBytes => Mathf.Max(0, VertexCount) * TotalFrameCount * BytesPerVertex;

    public bool TryGetClip(UnitAnimationStateId state, out UnitVAClipDef clip)
    {
        if (Clips != null)
        {
            for (int i = 0; i < Clips.Count; i++)
            {
                if (Clips[i].State == state)
                {
                    clip = Clips[i];
                    return true;
                }
            }
        }

        clip = default;
        return false;
    }

    public int GetPositionByteOffset(int frameIndex, int vertexIndex)
    {
        int safeVertexCount = Mathf.Max(1, VertexCount);
        return ((frameIndex * safeVertexCount) + vertexIndex) * BytesPerVertex;
    }

    public int GetPositionByteOffset(UnitVAClipDef clip, int localFrame, int vertexIndex)
    {
        int clampedFrame = Mathf.Clamp(localFrame, 0, Mathf.Max(0, clip.FrameCount - 1));
        return GetPositionByteOffset(clip.FrameStart + clampedFrame, vertexIndex);
    }

    public bool HasExpectedPositionDataLength()
    {
        return PositionData != null && PositionData.Length == ExpectedPositionDataBytes;
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

        if (Clips == null)
        {
            Clips = new List<UnitVAClipDef>();
            return;
        }

        for (int i = 0; i < Clips.Count; i++)
        {
            UnitVAClipDef clip = Clips[i];
            clip.FrameStart = Mathf.Max(0, clip.FrameStart);
            clip.FrameCount = Mathf.Max(0, clip.FrameCount);
            clip.TicksPerFrame = Mathf.Max(1, clip.TicksPerFrame);
            Clips[i] = clip;
        }

        PositionData ??= Array.Empty<byte>();
    }
}
