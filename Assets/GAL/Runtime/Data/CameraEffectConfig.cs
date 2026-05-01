using System;
using DG.Tweening;
using UnityEngine;

namespace GAL
{
    /// <summary>
    /// 镜头效果类型
    /// </summary>
    public enum CameraEffectType
    {
        /// <summary>水平震动</summary>
        ShakeHorizontal,
        /// <summary>垂直震动</summary>
        ShakeVertical,
        /// <summary>随机方向震动</summary>
        ShakeRandom,
        /// <summary>缩放拉近</summary>
        ZoomIn,
        /// <summary>缩放拉远</summary>
        ZoomOut,
        /// <summary>移动到目标位置</summary>
        MoveTo,
        /// <summary>闪白</summary>
        FlashWhite,
        /// <summary>闪黑</summary>
        FlashBlack,
    }

    /// <summary>
    /// 镜头效果配置
    /// </summary>
    [Serializable]
    public struct CameraEffectConfig
    {
        [Tooltip("效果类型")]
        public CameraEffectType EffectType;

        [Tooltip("持续时间（秒）")]
        public float Duration;

        [Tooltip("强度/幅度")]
        public float Intensity;

        [Tooltip("缓动类型")]
        public Ease EaseType;

        [Tooltip("震动频率（仅震动类有效）")]
        public int Vibrato;

        [Tooltip("随机性（0-1，仅震动类有效）")]
        public float Randomness;

        [Tooltip("目标位置（仅MoveTo有效，相对于当前位置的偏移）")]
        public Vector3 TargetOffset;

        [Tooltip("目标缩放（仅Zoom类有效）")]
        public float TargetZoom;

        /// <summary>
        /// 获取默认配置
        /// </summary>
        public static CameraEffectConfig GetDefault(CameraEffectType type)
        {
            return type switch
            {
                CameraEffectType.ShakeHorizontal => new CameraEffectConfig
                {
                    EffectType = type,
                    Duration = 0.3f,
                    Intensity = 0.5f,
                    EaseType = Ease.OutQuad,
                    Vibrato = 10,
                    Randomness = 0.5f
                },
                CameraEffectType.ShakeVertical => new CameraEffectConfig
                {
                    EffectType = type,
                    Duration = 0.3f,
                    Intensity = 0.5f,
                    EaseType = Ease.OutQuad,
                    Vibrato = 10,
                    Randomness = 0.5f
                },
                CameraEffectType.ShakeRandom => new CameraEffectConfig
                {
                    EffectType = type,
                    Duration = 0.5f,
                    Intensity = 0.3f,
                    EaseType = Ease.OutQuad,
                    Vibrato = 15,
                    Randomness = 0.8f
                },
                CameraEffectType.ZoomIn => new CameraEffectConfig
                {
                    EffectType = type,
                    Duration = 0.5f,
                    Intensity = 1f,
                    EaseType = Ease.InOutQuad,
                    TargetZoom = 1.2f
                },
                CameraEffectType.ZoomOut => new CameraEffectConfig
                {
                    EffectType = type,
                    Duration = 0.5f,
                    Intensity = 1f,
                    EaseType = Ease.InOutQuad,
                    TargetZoom = 0.8f
                },
                CameraEffectType.MoveTo => new CameraEffectConfig
                {
                    EffectType = type,
                    Duration = 0.5f,
                    Intensity = 1f,
                    EaseType = Ease.InOutQuad,
                    TargetOffset = Vector3.zero
                },
                CameraEffectType.FlashWhite or CameraEffectType.FlashBlack => new CameraEffectConfig
                {
                    EffectType = type,
                    Duration = 0.2f,
                    Intensity = 1f,
                    EaseType = Ease.Linear
                },
                _ => new CameraEffectConfig { EffectType = type, Duration = 0.5f }
            };
        }
    }
}

