using System;
using UnityEngine;

namespace GAL
{
/// <summary>
/// 演出效果条目基类
/// </summary>
[Serializable]
public abstract class EffectEntry : ISequenceEntry { }

/// <summary>
/// 头像切换效果
/// </summary>
[Serializable]
public class AvatarEffectEntry : EffectEntry
{
    [Range(0, 4)]
    public int SlotIndex;
    public AvatarAction Action = AvatarAction.Show;
    public CharacterRenderProfileSO Profile;
    public string EmotionKey;
    public bool FromLeft = true;
}

public enum AvatarAction
{
    Show,
    Hide
}

/// <summary>
/// 背景切换效果
/// </summary>
[Serializable]
public class BackgroundEffectEntry : EffectEntry
{
    public BackgroundPresetSO Preset;
}

/// <summary>
/// 语音播放效果
/// </summary>
[Serializable]
public class VoiceEffectEntry : EffectEntry
{
    /// <summary>关联的 DialogEntry.Id，空表示紧跟前一条对话</summary>
    public string TargetDialogId;
    public AudioClip VoiceClip;
}

/// <summary>
/// 镜头效果
/// </summary>
[Serializable]
public class CameraEffectEntry : EffectEntry
{
    public string EffectKey;
    public bool UseDurationOverride;
    public float DurationOverride = 0.5f;
    public bool UseIntensityOverride;
    public float IntensityOverride = 1f;
}

/// <summary>
/// 屏幕闪白/闪黑效果
/// </summary>
[Serializable]
public class ScreenFlashEntry : EffectEntry
{
    public ScreenFlashType FlashType;
    public float Duration = 0.2f;
}

public enum FadeType { Instant, FadeIn, FadeInWithScale }
public enum ScreenFlashType { White, Black, Red }
}
