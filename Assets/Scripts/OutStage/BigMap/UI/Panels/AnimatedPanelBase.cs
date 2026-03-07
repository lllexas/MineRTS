using UnityEngine;
using DG.Tweening;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 带动画的面板基类
    /// 提供统一的浮现/消失动画效果
    /// </summary>
    public abstract class AnimatedPanelBase : MonoBehaviour, IMenuPanel
    {
        [Header("动画配置")]
        [Tooltip("面板浮现动画位移量")]
        [SerializeField] protected float _slideInOffset = 20f;

        [Tooltip("动画时长")]
        [SerializeField] protected float _animationDuration = 0.3f;

        [Header("组件引用")]
        [SerializeField] protected CanvasGroup _canvasGroup;
        [SerializeField] protected GameObject _panelRoot;

        // 面板状态
        public bool IsOpen { get; protected set; } = false;
        public GameObject PanelRoot => _panelRoot != null ? _panelRoot : gameObject;

        // DOTween 缓存
        private Tween _openTween;
        private Tween _closeTween;

        protected Vector3 _initialPosition;

        protected virtual void Awake()
        {
            // 缓存初始位置
            _initialPosition = transform.localPosition;
        }

        protected virtual void OnDestroy()
        {
            // 清理 DOTween
            _openTween?.Kill();
            _closeTween?.Kill();
        }

        /// <summary>
        /// 显示面板（带 DOTween 动画）
        /// </summary>
        public virtual void Open()
        {
            // 停止之前的动画
            _openTween?.Kill();
            _closeTween?.Kill();

            gameObject.SetActive(true);

            // 设置初始状态：透明 + 偏移位置
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;

            transform.localPosition = _initialPosition - Vector3.up * _slideInOffset;

            // 打开动画：淡入 + 滑动
            _openTween = DOTween.Sequence()
                .Append(_canvasGroup != null
                    ? _canvasGroup.DOFade(1f, _animationDuration)
                    : null)
                .Join(transform.DOLocalMove(_initialPosition, _animationDuration)
                    .SetEase(Ease.OutCubic))
                .OnComplete(() =>
                {
                    IsOpen = true;
                });
        }

        /// <summary>
        /// 隐藏面板（带 DOTween 动画）
        /// </summary>
        public virtual void Close()
        {
            // 停止之前的动画
            _openTween?.Kill();
            _closeTween?.Kill();

            // 关闭动画：淡出 + 向上滑动
            _closeTween = DOTween.Sequence()
                .Append(_canvasGroup != null
                    ? _canvasGroup.DOFade(0f, _animationDuration)
                    : null)
                .Join(transform.DOLocalMove(_initialPosition - Vector3.up * _slideInOffset, _animationDuration)
                    .SetEase(Ease.InCubic))
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    IsOpen = false;
                });
        }

        /// <summary>
        /// 立即显示（无动画）
        /// </summary>
        public virtual void Show()
        {
            _openTween?.Kill();
            _closeTween?.Kill();

            gameObject.SetActive(true);
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
            transform.localPosition = _initialPosition;
            IsOpen = true;
        }

        /// <summary>
        /// 立即隐藏（无动画）
        /// </summary>
        public virtual void Hide()
        {
            _openTween?.Kill();
            _closeTween?.Kill();

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            transform.localPosition = _initialPosition;
            gameObject.SetActive(false);
            IsOpen = false;
        }
    }
}
