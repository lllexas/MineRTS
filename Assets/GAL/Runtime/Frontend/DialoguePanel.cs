using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;
using System;
using SpaceTUI;

namespace GAL
{
    // 播放行事件数据
    public class PlayLineEventData
    {
        public DialogEntry Entry;
        public System.Action OnComplete;
    }
    /// <summary>
    /// 对话条目状态
    /// </summary>
    public enum DialogEntryState
    {
        /// <summary>播放中</summary>
        Playing,
        /// <summary>播放结束</summary>
        Completed
    }
    
    /// <summary>
    /// 对话数据 - Model 层
    /// </summary>
    public class DialogueData
    {
        public string characterName;
        public string text;
        /// <summary>打字速度（全角字符/秒），默认 15</summary>
        public float typingCharsPerSecond = 15f;
        public string characterID;
        public int? slotIndex;
    }
    
    /// <summary>
    /// 说话者高亮数据 - 纯 MVVM 模式
    /// </summary>
    public class SpeakerData
    {
        public int? slotIndex;  // null = 无人说话
    }
    
    /// <summary>
    /// 对话数据包 - 包含回调（后端推送用）
    /// </summary>
    public class DialoguePackage
    {
        public DialogueData data;
        public System.Action onComplete;
    }
    
    /// <summary>
    /// 对话面板 - 独立响应事件
    /// </summary>
    public class DialoguePanel : SpaceUIAnimator
    {
        [Header("显示组件")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        
        [Header("设置")]
        [SerializeField] private float defaultCharsPerSecond = 15f;
        
        [Header("自动播放")]
        [SerializeField] private bool auto = false;
        [SerializeField] private float autoPauseTime = 1.5f;
        
        protected override string UIID => "DialoguePanel";
        
        private Tween _typewriterTween;
        private Tween _autoAdvanceTween;
        private bool _isTyping = false;
        private bool _appliedAutoState = false;
        // private Action _onAdvance;  // 暂时禁用鼠标点击，此字段暂不需要
        
        // RoutedRequest 中的 onComplete，点击后调用
        // private System.Action _currentOnComplete;  // 暂时禁用，此字段暂不需要
        
        // 播放行事件中的 OnComplete 回调
        private System.Action _playLineOnComplete;
        
        /// <summary>当前对话条目</summary>
        public DialogEntry CurrentDialogEntry { get; private set; }
        
        /// <summary>当前对话条目状态</summary>
        public DialogEntryState CurrentState { get; private set; } = DialogEntryState.Completed;
        
        /// <summary>是否自动播放</summary>
        public bool Auto 
        { 
            get { return auto; }
            set { SetAutoState(value); }
        }
        
        /// <summary>自动播放时每段话播放完成后的停留时间（秒）</summary>
        public float AutoPauseTime 
        { 
            get { return autoPauseTime; } 
            set { autoPauseTime = value; } 
        }
        
        void Start()
        {
            _appliedAutoState = auto;
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标点击 += OnClickAdvance;
            Hide();
            // 注意：PostSystem.Register 由基类 SpaceUIAnimator.Awake() 处理，此处无需重复注册
        }

        protected override void Update()
        {
            base.Update();
            // Inspector/动画/序列化路径可能绕过属性 setter，运行时主动同步一次。
            if (auto != _appliedAutoState)
            {
                ApplyAutoStateChange();
            }
        }
        
        /// <summary>
        /// 监听【播放行】事件 - 赋值 Entry、重置状态、开始打字机
        /// </summary>
        [Subscribe("播放行")]
        public void OnPlayLine(object data)
        {
            if (data is not PlayLineEventData eventData || eventData.Entry == null)
            {
                Debug.LogWarning("[DialoguePanel] 播放行事件数据无效");
                return;
            }

            CancelAutoAdvance();
            
            // 1. 保存 Entry
            CurrentDialogEntry = eventData.Entry;
            // 2. 重置状态
            CurrentState = DialogEntryState.Playing;
            // 3. 保存回调
            _playLineOnComplete = eventData.OnComplete;
            
            Debug.Log($"[DialoguePanel] 开始播放 - Entry: {eventData.Entry.Id}, Speaker: {eventData.Entry.Speaker}");
            
            // 4. 开始打字机！
            var dialogueData = new DialogueData
            {
                characterName = eventData.Entry.Speaker,
                text = eventData.Entry.Content,
                typingCharsPerSecond = defaultCharsPerSecond,
                characterID = null  // 可以从 Entry 扩展
            };
            ShowDialogueInternal(dialogueData);
        }
        
        void OnShowPanel(object data)
        {
            Debug.Log("[DialogPanel]正在打开台词面板");
            Show();
        }
        
        void OnHidePanel(object data)
        {
            CancelAutoAdvance();
            Debug.Log("[DialogPanel]正在关闭台词面板");
            Hide();
        }
        
        void OnClickAdvance(PointerEventData eventData)
        {
            switch (CurrentState)
            {
                case DialogEntryState.Playing:
                    // 播放中：只快进当前打字，不打断 auto 模式
                    _typewriterTween?.Complete();
                    Debug.Log(auto
                        ? "[DialoguePanel] Auto 模式下点击快进当前句"
                        : "[DialoguePanel] 点击快进");
                    break;
                    
                case DialogEntryState.Completed:
                    // 已完成：点击视为手动接管，关闭 auto 后继续下一句
                    if (auto)
                    {
                        Auto = false;
                        Debug.Log("[DialoguePanel] 点击继续时关闭自动播放");
                    }

                    InvokeLineComplete();
                    Debug.Log("[DialoguePanel] 点击继续下一句");
                    break;
            }
        }
        
        void ShowDialogueInternal(DialogueData data)
        {
            
            // 角色名
            if (nameText != null)
            {
                if (!string.IsNullOrEmpty(data.characterName))
                {
                    nameText.text = data.characterName;
                    nameText.gameObject.SetActive(true);
                }
                else
                {
                    nameText.gameObject.SetActive(false);
                }
            }
            
            // 打字机（计算总时长 = 字符数 / 每秒字符数）
            float charsPerSecond = data.typingCharsPerSecond > 0 ? data.typingCharsPerSecond : defaultCharsPerSecond;
            float duration = data.text.Length / Mathf.Max(1f, charsPerSecond);
            StartTypewriter(data.text, duration);
        }
        
        /// <summary>
        /// 打字机完成回调 - 通知打字完成，由外部决定如何继续
        /// </summary>
        void OnTypewriterComplete()
        {
            _isTyping = false;
            if (dialogueText != null) dialogueText.text = CurrentDialogEntry?.Content;
            CurrentState = DialogEntryState.Completed;
            
            // 尝试自动推进（如果 auto 为 true）
            TryAutoAdvance();
        }
        
        /// <summary>
        /// 尝试自动推进 - 检查 auto 状态并执行
        /// </summary>
        void TryAutoAdvance()
        {
            CancelAutoAdvance();

            if (auto && CurrentState == DialogEntryState.Completed && _playLineOnComplete != null)
            {
                _autoAdvanceTween = DOVirtual.DelayedCall(autoPauseTime, InvokeLineComplete)
                    .SetUpdate(true);
            }
        }
        
        /// <summary>
        /// 触发行完成回调
        /// </summary>
        void InvokeLineComplete()
        {
            CancelAutoAdvance();
            _playLineOnComplete?.Invoke();
            _playLineOnComplete = null;
        }

        void CancelAutoAdvance()
        {
            _autoAdvanceTween?.Kill();
            _autoAdvanceTween = null;
        }

        void SetAutoState(bool value)
        {
            if (auto == value && _appliedAutoState == value)
            {
                return;
            }

            auto = value;
            ApplyAutoStateChange();
        }

        void ApplyAutoStateChange()
        {
            _appliedAutoState = auto;

            if (!auto)
            {
                CancelAutoAdvance();
                return;
            }

            TryAutoAdvance();
        }
        
        void StartTypewriter(string text, float duration)
        {
            _typewriterTween?.Kill();
            _isTyping = true;
            if (dialogueText != null) dialogueText.text = "";
            
            int currentIndex = 0;
            _typewriterTween = DOTween.To(
                () => currentIndex,
                x => {
                    currentIndex = x;
                    if (dialogueText != null) dialogueText.text = text.Substring(0, x);
                },
                text.Length,
                Mathf.Max(0.01f, duration)
            )
            .SetEase(Ease.Linear)
            .OnComplete(() => OnTypewriterComplete());
        }
        
        
        protected override void CloseAction()
        {
            CancelAutoAdvance();
            FadeOut();
        }
    }
}
