using UnityEditor;
using UnityEngine;

public static class UnitVAClipPreviewSession
{
    public static UnitVASO ActiveAsset { get; private set; }
    public static int ActiveClipIndex { get; private set; } = -1;
    public static int CurrentFrame { get; set; }
    public static bool IsPlaying { get; set; }
    public static float PlaybackSpeed { get; set; } = 1f;

    public static bool IsActive => ActiveAsset != null && ActiveClipIndex >= 0;

    public static event System.Action Changed;

    public static bool IsPreviewing(UnitVASO asset, int clipIndex)
    {
        return ActiveAsset == asset && ActiveClipIndex == clipIndex;
    }

    public static UnitVAClip GetActiveClip()
    {
        if (!IsActive || ActiveAsset.Clips == null || ActiveClipIndex >= ActiveAsset.Clips.Count)
        {
            return null;
        }

        return ActiveAsset.Clips[ActiveClipIndex];
    }

    public static void Toggle(UnitVASO asset, int clipIndex)
    {
        if (IsPreviewing(asset, clipIndex))
        {
            Close();
            return;
        }

        Open(asset, clipIndex);
    }

    public static void Open(UnitVASO asset, int clipIndex)
    {
        ActiveAsset = asset;
        ActiveClipIndex = clipIndex;
        CurrentFrame = 0;
        IsPlaying = false;
        PlaybackSpeed = Mathf.Max(0.01f, PlaybackSpeed);

        UnitVAClipPreviewWindow.ShowWindow();
        NotifyChanged();
    }

    public static void Close()
    {
        Clear();
        UnitVAClipPreviewWindow.CloseIfOpen();
        NotifyChanged();
    }

    public static void CloseFromWindow()
    {
        Clear();
        NotifyChanged();
    }

    private static void Clear()
    {
        ActiveAsset = null;
        ActiveClipIndex = -1;
        CurrentFrame = 0;
        IsPlaying = false;
    }

    public static void NotifyChanged()
    {
        Changed?.Invoke();
        SceneView.RepaintAll();
    }
}
