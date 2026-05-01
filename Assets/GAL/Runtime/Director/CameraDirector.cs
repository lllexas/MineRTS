using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace GAL
{
    /// <summary>
    /// 相机导演 - 2D游戏镜头效果执行器
    ///
    /// 使用方式：
    /// 1. 将 CameraDirector 挂载到 Main Camera 上
    /// 2. 配置默认的 CameraEffectLibrarySO
    /// 3. 通过 CameraDirector.Instance.PlayEffect(key) 播放预定义效果
    /// 4. 或者直接传 CameraEffectConfig 执行自定义效果
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraDirector : MonoBehaviour
    {
        public static CameraDirector Instance { get; private set; }

        [Header("配置")]
        [Tooltip("默认镜头效果库")]
        public CameraEffectLibrarySO DefaultLibrary;

        [Tooltip("闪白/闪黑用的UI遮罩（可选）")]
        public CanvasGroup FlashOverlay;

        [Header("默认参数")]
        [Tooltip("默认震动基准距离（像素）")]
        public float DefaultShakeStrength = 50f;

        [Tooltip("默认缩放基准（正交大小）")]
        public float DefaultOrthoSize = 5f;

        // 运行时状态
        private Camera _camera;
        private Transform _cameraTransform;
        private Vector3 _originalPosition;
        private float _originalOrthoSize;
        private Tweener _currentTween;
        private readonly List<Tweener> _activeTweens = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _camera = GetComponent<Camera>();
            _cameraTransform = transform;
            _originalOrthoSize = _camera.orthographicSize;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            KillAllEffects();
        }

        // ==================== 公开API ====================

        /// <summary>
        /// 播放预定义效果（从 DefaultLibrary 查找）
        /// </summary>
        public void PlayEffect(string effectKey)
        {
            PlayEffect(effectKey, null);
        }

        public void PlayEffect(string effectKey, System.Action onComplete)
        {
            if (DefaultLibrary == null)
            {
                Debug.LogWarning("[CameraDirector] DefaultLibrary 未设置");
                onComplete?.Invoke();
                return;
            }

            if (DefaultLibrary.TryGetEffect(effectKey, out var config))
            {
                PlayEffect(config, onComplete);
            }
            else
            {
                Debug.LogWarning($"[CameraDirector] 找不到效果: {effectKey}");
                onComplete?.Invoke();
            }
        }

        public void PlayEffect(string effectKey, float? durationOverride, float? intensityOverride, Action onComplete = null)
        {
            if (DefaultLibrary == null)
            {
                Debug.LogWarning("[CameraDirector] DefaultLibrary 未设置");
                onComplete?.Invoke();
                return;
            }

            if (string.IsNullOrWhiteSpace(effectKey))
            {
                Debug.LogWarning("[CameraDirector] EffectKey 为空");
                onComplete?.Invoke();
                return;
            }

            if (!DefaultLibrary.TryGetEffect(effectKey, out var config))
            {
                Debug.LogWarning($"[CameraDirector] 找不到效果: {effectKey}");
                onComplete?.Invoke();
                return;
            }

            if (durationOverride.HasValue)
                config.Duration = durationOverride.Value;

            if (intensityOverride.HasValue)
                config.Intensity = intensityOverride.Value;

            PlayEffect(config, onComplete);
        }

        /// <summary>
        /// 播放指定配置的效果
        /// </summary>
        public void PlayEffect(CameraEffectConfig config)
        {
            PlayEffect(config, null);
        }

        public void PlayEffect(CameraEffectConfig config, System.Action onComplete)
        {
            switch (config.EffectType)
            {
                case CameraEffectType.ShakeHorizontal:
                    Shake(config, Vector3.right, onComplete);
                    break;
                case CameraEffectType.ShakeVertical:
                    Shake(config, Vector3.up, onComplete);
                    break;
                case CameraEffectType.ShakeRandom:
                    Shake(config, Vector3.one, onComplete);
                    break;
                case CameraEffectType.ZoomIn:
                case CameraEffectType.ZoomOut:
                    Zoom(config, onComplete);
                    break;
                case CameraEffectType.MoveTo:
                    Move(config, onComplete);
                    break;
                case CameraEffectType.FlashWhite:
                    Flash(Color.white, config.Duration, onComplete);
                    break;
                case CameraEffectType.FlashBlack:
                    Flash(Color.black, config.Duration, onComplete);
                    break;
                default:
                    onComplete?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// 立即停止所有效果并复位
        /// </summary>
        public void StopAll(bool resetToDefault = true)
        {
            KillAllEffects();

            if (resetToDefault)
            {
                _cameraTransform.localPosition = _originalPosition;
                _camera.orthographicSize = _originalOrthoSize;

                if (FlashOverlay != null)
                {
                    FlashOverlay.alpha = 0;
                    FlashOverlay.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 保存当前状态为原始状态（用于复位）
        /// </summary>
        public void SaveState()
        {
            _originalPosition = _cameraTransform.localPosition;
            _originalOrthoSize = _camera.orthographicSize;
        }

        // ==================== 效果实现 ====================

        private void Shake(CameraEffectConfig config, Vector3 direction, System.Action onComplete)
        {
            _originalPosition = _cameraTransform.localPosition;

            // 计算震动强度（像素转世界单位，近似）
            float strength = config.Intensity * DefaultShakeStrength * 0.01f;

            // 如果是随机震动，用 Vector3.one，否则限制方向
            Vector3 shakeStrength = direction == Vector3.one
                ? new Vector3(strength, strength, 0)
                : direction * strength;

            var tween = _cameraTransform.DOShakePosition(
                    config.Duration,
                    shakeStrength,
                    config.Vibrato,
                    config.Randomness,
                    false,
                    true
                )
                .SetEase(config.EaseType)
                .OnComplete(() =>
                {
                    _cameraTransform.localPosition = _originalPosition;
                    onComplete?.Invoke();
                });

            _activeTweens.Add(tween);
        }

        private void Zoom(CameraEffectConfig config, System.Action onComplete)
        {
            float targetSize = DefaultOrthoSize / config.TargetZoom; // Zoom值越大，正交大小越小

            var tween = DOTween.To(
                    () => _camera.orthographicSize,
                    size => _camera.orthographicSize = size,
                    targetSize,
                    config.Duration
                )
                .SetEase(config.EaseType)
                .OnComplete(() => onComplete?.Invoke());

            _activeTweens.Add(tween);
        }

        private void Move(CameraEffectConfig config, System.Action onComplete)
        {
            Vector3 targetPos = _cameraTransform.localPosition + config.TargetOffset;

            var tween = _cameraTransform.DOLocalMove(targetPos, config.Duration)
                .SetEase(config.EaseType)
                .OnComplete(() => onComplete?.Invoke());

            _activeTweens.Add(tween);
        }

        private void Flash(Color color, float duration, System.Action onComplete)
        {
            if (FlashOverlay == null)
            {
                Debug.LogWarning("[CameraDirector] FlashOverlay 未设置，无法播放闪白/闪黑效果");
                onComplete?.Invoke();
                return;
            }

            FlashOverlay.gameObject.SetActive(true);

            // 设置颜色
            var image = FlashOverlay.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.color = color;

            // 闪入->闪出
            FlashOverlay.alpha = 0;

            var tween = FlashOverlay.DOFade(1f, duration * 0.3f)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    FlashOverlay.DOFade(0f, duration * 0.7f)
                        .SetEase(Ease.Linear)
                        .OnComplete(() =>
                        {
                            FlashOverlay.gameObject.SetActive(false);
                            onComplete?.Invoke();
                        });
                });

            _activeTweens.Add(tween);
        }

        private void KillAllEffects()
        {
            foreach (var tween in _activeTweens)
            {
                if (tween != null && tween.IsActive())
                    tween.Kill();
            }
            _activeTweens.Clear();
        }
    }
}
