using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using SpaceTUI;

namespace GAL
{
    public enum TransitionType
    {
        FadeToBlack,
        FadeFromBlack,
        FlashWhite
    }
    
    public class TransitionData
    {
        public TransitionType type;
        public float? duration;
    }
    
    /// <summary>
    /// 转场管理器 - 独立响应事件
    /// </summary>
    public class TransitionManager : SpaceUIAnimator
    {
        [SerializeField] private Image blackScreen;
        [SerializeField] private Image whiteScreen;
        [SerializeField] private float defaultDuration = 0.5f;
        
        protected override string UIID => "TransitionManager";
        
        private Tween _currentTween;
        
        void Start()
        {
            期望显示面板 += OnTransition;
            
            if (blackScreen != null) blackScreen.color = new Color(0, 0, 0, 0);
            if (whiteScreen != null) whiteScreen.color = new Color(1, 1, 1, 0);
            
            Hide();
        }
        
        void OnTransition(object data)
        {
            if (data is TransitionData transData)
            {
                switch (transData.type)
                {
                    case TransitionType.FadeToBlack:
                        FadeToBlack(transData.duration);
                        break;
                    case TransitionType.FadeFromBlack:
                        FadeFromBlack(transData.duration);
                        break;
                    case TransitionType.FlashWhite:
                        FlashWhite(transData.duration);
                        break;
                }
            }
        }
        
        void FadeToBlack(float? duration)
        {
            KillCurrent();
            Show();
            
            float dur = duration ?? defaultDuration;
            _currentTween = blackScreen?.DOFade(1f, dur).SetEase(Ease.InOutQuad);
        }
        
        void FadeFromBlack(float? duration)
        {
            KillCurrent();
            Show();
            
            float dur = duration ?? defaultDuration;
            _currentTween = blackScreen?.DOFade(0f, dur)
                .SetEase(Ease.InOutQuad)
                .OnComplete(Hide);
        }
        
        void FlashWhite(float? duration)
        {
            KillCurrent();
            Show();
            
            float dur = duration ?? 0.3f;
            if (whiteScreen != null) whiteScreen.color = new Color(1, 1, 1, 0);
            
            Sequence seq = DOTween.Sequence();
            seq.Append(whiteScreen?.DOFade(1f, dur * 0.2f).SetEase(Ease.OutQuad));
            seq.Append(whiteScreen?.DOFade(0f, dur * 0.8f).SetEase(Ease.InQuad));
            seq.OnComplete(Hide);
            
            _currentTween = seq;
        }
        
        void KillCurrent()
        {
            _currentTween?.Kill();
            _currentTween = null;
        }
        
        protected override void CloseAction() { }
    }
}
