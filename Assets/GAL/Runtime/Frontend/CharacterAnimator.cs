using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using SpaceTUI;
using NekoGraph;

namespace GAL
{
    /// <summary>
    /// 立绘动画器 - 通过 UIID 响应事件
    /// </summary>
    public class CharacterAnimator : SpaceUIAnimator
    {
        [SerializeField] private Image characterImage;
        [SerializeField] private Vector3 offscreenLeft = new Vector3(-1200, 0, 0);
        [SerializeField] private Vector3 offscreenRight = new Vector3(1200, 0, 0);
        
        [Header("槽位")]
        [SerializeField] private int slotIndex;  // 0-4
        
        private Vector3 _homePosition;
        private CharacterRenderProfileSO _pendingProfile;
        private string _pendingEmotionKey;
        private bool _fromLeft = true;
        private RectTransform _imageRect;
        private Vector2 _defaultAnchoredPosition;
        private Vector3 _defaultImageScale;
        private Vector2 _defaultPivot;
        private bool _defaultPreserveAspect;
        
        public string CurrentCharacterID { get; private set; }
        public bool IsOccupied => !string.IsNullOrEmpty(CurrentCharacterID);
        
        // 直接根据 slotIndex 生成 UIID（修复序列化冲突）
        protected override string UIID => $"CharSlot{slotIndex}";
        
        protected override void Awake()
        {
            base.Awake();
            _homePosition = transform.localPosition;
            _imageRect = characterImage != null ? characterImage.rectTransform : null;
            if (_imageRect != null)
            {
                _defaultAnchoredPosition = _imageRect.anchoredPosition;
                _defaultImageScale = _imageRect.localScale;
                _defaultPivot = _imageRect.pivot;
            }
            _defaultPreserveAspect = characterImage != null && characterImage.preserveAspect;

        }
        
        void Start()
        {
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            PostSystem.Instance.On("期望显示角色", OnCharacterShow);
            PostSystem.Instance.On("期望隐藏角色", OnCharacterHide);
            PostSystem.Instance.On("期望高亮角色", OnHighlightSpeaker);
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
            // 安全注销：场景卸载时 Instance 可能已为 null
            PostSystem.Instance?.Off("期望显示角色", OnCharacterShow);
            PostSystem.Instance?.Off("期望隐藏角色", OnCharacterHide);
            PostSystem.Instance?.Off("期望高亮角色", OnHighlightSpeaker);
        }
        
        /// <summary>
        /// 处理来自 DialogPlayer 的角色显示请求
        /// </summary>
        void OnCharacterShow(object data)
        {
            Debug.Log("现在期望显示角色");
            if (data is RoutedRequest<CharacterShowData> req && req.uiid == UIID)
            {
                PrepareShow(req.data.id, req.data.profile, req.data.emotionKey, req.data.fromLeft);
                OnShowPanel(null);
                req.onComplete?.Invoke(); // 通知 DialogPlayer 完成
            }
        }
        
        void OnHighlightSpeaker(object data)
        {
            if (data is SpeakerData speakerData)
            {
                bool isSpeaking = speakerData.slotIndex.HasValue && speakerData.slotIndex.Value == slotIndex;
                Highlight(isSpeaking);
            }
        }

        void OnCharacterHide(object data)
        {
            if (data is RoutedRequest<CharacterHideData> req && req.uiid == UIID)
            {
                HideCharacter(req.onComplete);
            }
        }
        
        public void PrepareShow(string characterID, CharacterRenderProfileSO profile, string emotionKey, bool fromLeft)
        {
            CurrentCharacterID = characterID;
            _pendingProfile = profile;
            _pendingEmotionKey = emotionKey;
            _fromLeft = fromLeft;
        }
        
        void OnShowPanel(object data)
        {
            ApplyAvatarPreset();
            
            ResetScale();
            StopBreathing();
            
            transform.localPosition = _fromLeft ? offscreenLeft : offscreenRight;
            Show();
            
            transform.DOLocalMove(_homePosition, _fadeDuration)
                .SetEase(Ease.OutBack, 0.5f);
            PlayScaleAnimation();
        }
        
        void OnHidePanel(object data)
        {
            HideCharacter(null);
        }

        void HideCharacter(System.Action onComplete)
        {
            if (!IsOccupied)
            {
                Hide();
                onComplete?.Invoke();
                return;
            }

            var targetPos = offscreenRight;
            
            transform.DOLocalMove(targetPos, _fadeDuration)
                .SetEase(Ease.InBack, 0.5f)
                .OnComplete(() => {
                    CurrentCharacterID = null;
                    _pendingProfile = null;
                    _pendingEmotionKey = null;
                    Hide();
                    onComplete?.Invoke();
                });
        }
        
        public void Highlight(bool isSpeaking)
        {
            if (characterImage == null) return;
            
            if (isSpeaking)
            {
                characterImage.DOFade(1f, 0.2f);
                transform.DOLocalMove(_homePosition, 0.3f);
            }
            else
            {
                characterImage.DOFade(0.6f, 0.2f);
                transform.DOLocalMove(_homePosition + Vector3.back * 50, 0.3f);
            }
        }
        
        public void ChangeExpression(Sprite newSprite)
        {
            if (characterImage == null) return;
            
            characterImage.DOFade(0f, 0.08f)
                .OnComplete(() => {
                    characterImage.sprite = newSprite;
                    characterImage.DOFade(1f, 0.08f);
                });
        }

        void ApplyAvatarPreset()
        {
            if (characterImage == null)
                return;

            ResetImageLayoutToDefault();

            if (_pendingProfile == null)
            {
                Debug.LogWarning("[CharacterAnimator] 角色 Profile 为空");
                return;
            }

            if (!_pendingProfile.TryGetAvatarPreset(_pendingEmotionKey, out CharacterAvatarPreset preset) || preset?.Sprite == null)
            {
                Debug.LogWarning($"[CharacterAnimator] 角色 '{_pendingProfile.name}' 未找到可用立绘：Emotion='{_pendingEmotionKey}'");
                return;
            }

            characterImage.sprite = preset.Sprite;
            characterImage.preserveAspect = preset.PreserveAspect;

            if (_imageRect == null)
                return;

            _imageRect.anchoredPosition = _defaultAnchoredPosition + preset.AnchoredOffset;
            _imageRect.localScale = _defaultImageScale * Mathf.Max(0.01f, preset.Scale);
            _imageRect.pivot = preset.OverridePivot ? preset.Pivot01 : _defaultPivot;
        }

        void ResetImageLayoutToDefault()
        {
            characterImage.preserveAspect = _defaultPreserveAspect;
            if (_imageRect == null)
                return;

            _imageRect.anchoredPosition = _defaultAnchoredPosition;
            _imageRect.localScale = _defaultImageScale;
            _imageRect.pivot = _defaultPivot;
        }
        
        protected override void CloseAction() { }
    }
}
