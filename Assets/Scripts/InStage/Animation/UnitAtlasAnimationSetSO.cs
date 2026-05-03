using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitAtlasAnimationSetSO", menuName = "MineRTS/Animation/Unit Atlas Animation Set")]
public class UnitAtlasAnimationSetSO : ScriptableObject
{
    public const int BasePixelsPerCell = 64;

    [Header("Identity")]
    public string UnitTypeId;

    [Header("Atlas")]
    public Texture2D AtlasTexture;
    public UnitAnimationFrameTier FrameTier = UnitAnimationFrameTier.Small64;
    [Min(1)] public int AtlasColumns = 1;
    [Min(1)] public int AtlasRows = 1;

    [Header("Layout")]
    public Vector2 PivotNormalized = new Vector2(0.5f, 0f);
    public bool AllowFlipX = true;

    [Header("Clips")]
    public List<UnitAtlasClipDef> Clips = new List<UnitAtlasClipDef>();

    public int FrameSizePixels => (int)FrameTier;
    public int TotalFrameCapacity => AtlasColumns * AtlasRows;

    public bool TryGetClip(UnitAnimationStateId state, out UnitAtlasClipDef clip)
    {
        for (int i = 0; i < Clips.Count; i++)
        {
            if (Clips[i].State == state)
            {
                clip = Clips[i];
                return true;
            }
        }

        clip = default;
        return false;
    }

    public Rect GetFrameUvRect(AtlasFrameCoord frameCoord)
    {
        int safeColumns = Mathf.Max(1, AtlasColumns);
        int safeRows = Mathf.Max(1, AtlasRows);
        int col = Mathf.Clamp(frameCoord.Col, 0, safeColumns - 1);
        int row = Mathf.Clamp(frameCoord.Row, 0, safeRows - 1);

        float frameWidth = 1f / safeColumns;
        float frameHeight = 1f / safeRows;

        float xMin = col * frameWidth;
        float yMin = 1f - ((row + 1) * frameHeight);
        return new Rect(xMin, yMin, frameWidth, frameHeight);
    }

    private void OnValidate()
    {
        if (Clips == null)
        {
            Clips = new List<UnitAtlasClipDef>();
            return;
        }

        for (int i = 0; i < Clips.Count; i++)
        {
            UnitAtlasClipDef clip = Clips[i];
            clip.TicksPerFrame = Mathf.Max(1, clip.TicksPerFrame);
            if (clip.Frames == null)
            {
                clip.Frames = System.Array.Empty<AtlasFrameCoord>();
            }

            for (int frameIndex = 0; frameIndex < clip.Frames.Length; frameIndex++)
            {
                AtlasFrameCoord frame = clip.Frames[frameIndex];
                frame.Row = Mathf.Clamp(frame.Row, 0, Mathf.Max(0, AtlasRows - 1));
                frame.Col = Mathf.Clamp(frame.Col, 0, Mathf.Max(0, AtlasColumns - 1));
                clip.Frames[frameIndex] = frame;
            }

            Clips[i] = clip;
        }
    }
}
