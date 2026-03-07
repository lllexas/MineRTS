using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 世界空间 Canvas 控制器
    /// 职责：控制 Canvas 的整体动画效果（淡入淡出、缩放等）
    /// 不是单例，可以挂载到多个 Canvas 上
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class WorldSpaceCanvasController : MonoBehaviour
    {
        [Header("动画配置")]
        [Tooltip("淡入/淡出动画时长（秒）")]
        [SerializeField] private float _fadeDuration = 0.3f;

        [Tooltip("缩放动画时长（秒）")]
        [SerializeField] private float _scaleDuration = 0.2f;

        [Tooltip("目标缩放值")]
        [SerializeField] private Vector3 _targetScale = Vector3.one;

        [Tooltip("浮现动画位移量（Y 轴向上）")]
        [SerializeField] private float _slideInOffset = 30f;

        [Header("缓动曲线")]
        [Tooltip("淡入缓动")]
        [SerializeField] private Ease _fadeInEase = Ease.OutCubic;

        [Tooltip("淡出缓动")]
        [SerializeField] private Ease _fadeOutEase = Ease.InCubic;

        [Header("组件引用")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _canvasGroup;

        // 缓存的初始缩放
        private Vector3 _initialScale;
        private Vector3 _slideInStartPosition;

        // DOTween 缓存
        private Tween _fadeTween;
        private Tween _slideTween;
        private Tween _scaleTween;

        /// <summary>
        /// Canvas 是否可见
        /// </summary>
        public bool IsVisible { get; private set; } = true;

        private void Awake()
        {
            // 自动获取组件
            if (_canvas == null)
                _canvas = GetComponent<Canvas>();

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            // 设置初始状态
            _initialScale = transform.localScale;
            _targetScale = _initialScale;

            // 确保 Canvas 设置正确
            _canvas.worldCamera = Camera.main;
            _canvas.sortingOrder = 10; // UI 层在底图之上

            Debug.Log($"<color=cyan>[WorldSpaceCanvasController]</color> 初始化完成：{gameObject.name}");
        }

        private void OnDestroy()
        {
            // 清理 DOTween
            _fadeTween?.Kill();
            _slideTween?.Kill();
            _scaleTween?.Kill();
        }

        /// <summary>
        /// 淡入 Canvas（透明度渐变 + 从下往上滑动）
        /// </summary>
        public void FadeIn()
        {
            // 停止之前的动画
            _fadeTween?.Kill();
            _slideTween?.Kill();

            gameObject.SetActive(true);

            // 设置初始状态：透明 + 偏移位置
            _canvasGroup.alpha = 0f;
            _slideInStartPosition = transform.localPosition;
            transform.localPosition = _slideInStartPosition - Vector3.up * _slideInOffset;

            // 淡入动画
            _fadeTween = _canvasGroup
                .DOFade(1f, _fadeDuration)
                .SetEase(_fadeInEase);

            // 滑动动画
            _slideTween = transform
                .DOLocalMove(_slideInStartPosition, _fadeDuration)
                .SetEase(_fadeInEase);

            IsVisible = true;

            Debug.Log("<color=cyan>[WorldSpaceCanvasController]</color> 淡入动画开始");
        }

        /// <summary>
        /// 淡出 Canvas（透明度渐变 + 向上滑动消失）
        /// </summary>
        public void FadeOut()
        {
            // 停止之前的动画
            _fadeTween?.Kill();
            _slideTween?.Kill();

            // 淡出动画
            _fadeTween = _canvasGroup
                .DOFade(0f, _fadeDuration)
                .SetEase(_fadeOutEase)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                });

            // 滑动动画（向上消失）
            _slideTween = transform
                .DOLocalMove(_slideInStartPosition - Vector3.up * _slideInOffset, _fadeDuration)
                .SetEase(_fadeOutEase);

            IsVisible = false;

            Debug.Log("<color=cyan>[WorldSpaceCanvasController]</color> 淡出动画开始");
        }

        /// <summary>
        /// 立即显示 Canvas（无动画）
        /// </summary>
        public void Show()
        {
            _fadeTween?.Kill();
            _slideTween?.Kill();

            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            transform.localPosition = _slideInStartPosition;
            IsVisible = true;
        }

        /// <summary>
        /// 立即隐藏 Canvas（无动画）
        /// </summary>
        public void Hide()
        {
            _fadeTween?.Kill();
            _slideTween?.Kill();

            gameObject.SetActive(false);
            _canvasGroup.alpha = 0f;
            IsVisible = false;
        }

        /// <summary>
        /// 播放缩放动画
        /// </summary>
        public void PlayScaleAnimation()
        {
            _scaleTween?.Kill();

            _scaleTween = transform
                .DOScale(_targetScale, _scaleDuration)
                .SetEase(Ease.OutBack);
        }

        /// <summary>
        /// 重置缩放
        /// </summary>
        public void ResetScale()
        {
            _scaleTween?.Kill();
            transform.localScale = _initialScale;
        }

        /// <summary>
        /// 设置目标缩放值
        /// </summary>
        public void SetTargetScale(Vector3 scale)
        {
            _targetScale = scale;
        }
    }
}
