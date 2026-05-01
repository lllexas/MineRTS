using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using SpaceTUI;
using NekoGraph;

namespace GAL
{
    /// <summary>
    /// 背景动画器 - 处理背景图片的切换与淡入效果
    /// </summary>
    public class BackgroundAnimator : SpaceUIAnimator
    {
        [Header("背景组件")]
        [SerializeField] private Image backgroundImage;
        
        [Header("过渡配置")]
        [SerializeField] private float defaultDuration = 0.5f;
        
        protected override string UIID => "BackgroundAnimator";
        
        private Sprite _pendingSprite;
        private FadeType _pendingFadeType;
        private Tween _currentTween;
        
        void Start()
        {
            期望显示面板 += OnShowBackground;
            期望隐藏面板 += OnHideBackground;
            PostSystem.Instance.On("期望切换背景", OnBackgroundChange);
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
            PostSystem.Instance?.Off("期望切换背景", OnBackgroundChange);
            _currentTween?.Kill();
        }
        
        /// <summary>
        /// 处理背景切换请求
        /// </summary>
        void OnBackgroundChange(object data)
        {
            if (data is RoutedRequest<BackgroundChangeData> req && req.uiid == UIID)
            {
                Show();
                PrepareChange(req.data.sprite, req.data.fadeType);
                PlayTransition(req.data.fadeType, req.onComplete);
            }
        }
        
        void OnShowBackground(object data)
        {
            Show();
        }
        
        void OnHideBackground(object data)
        {
            Hide();
        }
        
        public void PrepareChange(Sprite sprite, FadeType fadeType)
        {
            _pendingSprite = sprite;
            _pendingFadeType = fadeType;
        }
        
        /// <summary>
        /// 执行背景过渡动画
        /// </summary>
        void PlayTransition(FadeType fadeType, System.Action onComplete)
        {
            _currentTween?.Kill();
            
            if (backgroundImage == null || _pendingSprite == null)
            {
                onComplete?.Invoke();
                return;
            }
            
            switch (fadeType)
            {
                case FadeType.Instant:
                    InstantSwitch(onComplete);
                    break;
                case FadeType.FadeIn:
                    FadeSwitch(onComplete);
                    break;
                case FadeType.FadeInWithScale:
                    FadeWithScaleSwitch(onComplete);
                    break;
            }
        }
        
        void InstantSwitch(System.Action onComplete)
        {
            backgroundImage.sprite = _pendingSprite;
            backgroundImage.color = Color.white;
            onComplete?.Invoke();
        }
        
        void FadeSwitch(System.Action onComplete)
        {
            var seq = DOTween.Sequence();
            seq.Append(backgroundImage.DOFade(0f, defaultDuration * 0.5f));
            seq.AppendCallback(() => backgroundImage.sprite = _pendingSprite);
            seq.Append(backgroundImage.DOFade(1f, defaultDuration * 0.5f));
            seq.OnComplete(() => onComplete?.Invoke());
            _currentTween = seq;
        }
        
        void FadeWithScaleSwitch(System.Action onComplete)
        {
            var seq = DOTween.Sequence();
            seq.Append(backgroundImage.DOFade(0f, defaultDuration * 0.3f));
            seq.Join(transform.DOScale(1.1f, defaultDuration * 0.3f));
            seq.AppendCallback(() => 
            {
                backgroundImage.sprite = _pendingSprite;
                transform.localScale = Vector3.one * 0.9f;
            });
            seq.Append(backgroundImage.DOFade(1f, defaultDuration * 0.5f));
            seq.Join(transform.DOScale(1f, defaultDuration * 0.5f).SetEase(Ease.OutBack));
            seq.OnComplete(() => onComplete?.Invoke());
            _currentTween = seq;
        }
        
        protected override void CloseAction() { }
    }
    
    /// <summary>
    /// 背景切换数据
    /// </summary>
    public class BackgroundChangeData
    {
        public Sprite sprite;
        public FadeType fadeType;
    }
}
