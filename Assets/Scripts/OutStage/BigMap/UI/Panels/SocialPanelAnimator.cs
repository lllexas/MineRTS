using System;
using UnityEngine;
using UnityEngine.EventSystems;
using MineRTS.BigMap.UI.Panels;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// SocialPanelAnimator - 社交终端面板（CLI 风格）
    /// </summary>
    public class SocialPanelAnimator : ConsolePanelBase<SocialCLI>
    {
        [Header("Social CLI")]
        [SerializeField] private SocialCLI _socialCLI;

        protected override SocialCLI ConsoleLogic => _socialCLI;

        protected override string GetPrompt()
        {
            string p = _socialCLI.CurrentPath.TrimEnd('/');
            string displayPath = string.IsNullOrEmpty(p) ? "~" : "~" + p;
            return $"<color=#00FF88>SC</color> <color=#88AAFF>{displayPath}</color><color=#FFFFFF>></color> ";
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

        protected override string UIID => "SocialPanel";

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
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
                    return;
                }
            }

            _socialCLI.BindInputHandleHost(() => LineCount, ReplaceRange);

            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标滑入 += OnMouseEnterHandler;
            鼠标滑出 += OnMouseExitHandler;
            鼠标点击 += OnMouseClickHandler;

            OutputLine("=== 社交终端 v1.0 ===", Color.cyan);
            OutputLine("输入 'help' 查看帮助，输入 'ls' 浏览目录", Color.gray);
            OutputLine(string.Empty, Color.black);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_socialCLI != null)
            {
                _socialCLI.UnbindInputHandleHost();
            }

            期望显示面板 -= OnShowPanel;
            期望隐藏面板 -= OnHidePanel;
            鼠标滑入 -= OnMouseEnterHandler;
            鼠标滑出 -= OnMouseExitHandler;
            鼠标点击 -= OnMouseClickHandler;
        }

        [Subscribe("SocialCLI.Output")]
        private void HandleOutput(object data)
        {
            if (data is DeveloperConsole.ConsoleOutputEvent evt)
            {
                Output(evt.message, evt.color);
            }
        }

        [Subscribe("SocialCLI.ScrollToTop")]
        private void HandleScrollToTop(object data)
        {
            _scrollLineIndex = 0;
            _isDirty = true;
        }

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



