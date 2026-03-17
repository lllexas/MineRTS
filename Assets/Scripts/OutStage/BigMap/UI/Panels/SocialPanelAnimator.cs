using System;
using UnityEngine;
using UnityEngine.EventSystems;
using MineRTS.BigMap.UI.Panels;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// SocialPanelAnimator - 社交终端面板（CLI 风格）
    /// ═══════════════════════════════════════════════════════════════
    ///
    /// <para>【UI ID】SocialPanel - 用于事件匹配</para>
    /// <para>【职责】社交系统命令行终端 UI</para>
    /// <para>【虚拟终端渲染器】ScreenBuffer + LineRendererPool</para>
    ///
    /// 继承关系：
    ///   SpaceUIAnimator
    ///   ↑
    ///   ConsolePanelBase<SocialCLI>
    ///   ↑
    ///   SocialPanelAnimator (社交终端特化)
    ///
    /// ═══════════════════════════════════════════════════════════════
    /// </summary>
    public class SocialPanelAnimator : ConsolePanelBase<SocialCLI>
    {
        [Header("Social CLI")]
        [SerializeField] private SocialCLI _socialCLI; // 直接在 Inspector 中拖拽引用

        // =========================================================
        //  抽象接口实现
        // =========================================================

        protected override string GetPrompt()
        {
            return $"<color=#00FF00>{_socialCLI.CurrentPath}</color> ";
        }

        public override void OnSubmitCommand(string input)
        {
            var logic = _socialCLI;
            if (logic != null)
            {
                logic.ProcessCommand(input);
            }
            else
            {
                Debug.LogError("[SocialPanelAnimator] SocialCLI reference is missing!");
            }
        }

        // =========================================================
        //  重写基类方法
        // =========================================================

        protected override void Awake()
        {
            _uiID = "SocialPanel";
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Debug.Log($"[SocialPanelAnimator] OnEnable called，注册 PostSystem 标签订阅");
            PostSystem.Instance.Register(this);
        }

        protected override void OnDisable()
        {
            Debug.Log($"[SocialPanelAnimator] OnDisable called，注销 PostSystem 标签订阅");
            if (PostSystem.Instance != null)
                PostSystem.Instance.Unregister(this);
        }

        protected override void Start()
        {
            base.Start();

            if (_socialCLI == null)
            {
                _socialCLI = GetComponent<SocialCLI>();
                if (_socialCLI == null)
                {
                    Debug.LogError("[SocialPanelAnimator] Start: SocialCLI reference is missing and not found on the same GameObject!");
                }
            }

            // 追加 SpaceUIAnimator 的事件订阅
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标滑入 += OnMouseEnterHandler;
            鼠标滑出 += OnMouseExitHandler;
            鼠标点击 += OnMouseClickHandler;

            // 输出欢迎信息
            OutputLine("=== 社交终端 v1.0 ===", Color.cyan);
            OutputLine("输入 'help' 查看帮助，输入 'ls' 浏览目录", Color.gray);
            OutputLine(string.Empty, Color.black);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // 取消 SpaceUIAnimator 的事件订阅
            期望显示面板 -= OnShowPanel;
            期望隐藏面板 -= OnHidePanel;
            鼠标滑入 -= OnMouseEnterHandler;
            鼠标滑出 -= OnMouseExitHandler;
            鼠标点击 -= OnMouseClickHandler;
        }

        // =========================================================
        //  PostSystem 标签订阅方法
        // =========================================================

        [Subscribe("SocialCLI.Output")]
        private void HandleOutput(object data)
        {
            if (data is DeveloperConsole.ConsoleOutputEvent evt)
            {
                Output(evt.message, evt.color);
            }
        }

        // =========================================================
        //  SpaceUIAnimator 事件回调
        // =========================================================

        private void OnShowPanel(object data)
        {
            Debug.Log($"<color=cyan>[SocialPanel]</color> 显示面板：{data}");
            FadeIn();
            StartBreathing();
        }

        private void OnHidePanel(object data)
        {
            Debug.Log($"<color=cyan>[SocialPanel]</color> 隐藏面板：{data}");
            StopBreathing();
            FadeOut();
        }

        private void OnMouseEnterHandler(PointerEventData eventData)
        {
            SetTargetScale(new Vector3(1.02f, 1.02f, 1.02f));
            PlayScaleAnimation();
        }

        private void OnMouseExitHandler(PointerEventData eventData)
        {
            ResetScale();
            ResetRotation();
        }

        private void OnMouseClickHandler(PointerEventData eventData)
        {
            if (inputField != null)
            {
                inputField.ActivateInputField();
            }
        }
    }
}
