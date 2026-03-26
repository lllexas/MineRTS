using System.Collections;
using MineRTS.BigMap.UI.Panels;
using UnityEngine;
using UnityEngine.EventSystems;
using SpaceTUI;

namespace MineRTS.BigMap.UI.Panels
{
    public class LabTerminalPanel : ConsolePanelBase<LabCLI>
    {
        [Header("Lab CLI")]
        [SerializeField] private LabCLI labCLI;

        private Coroutine _inputActivationCoroutine;

        protected override LabCLI ConsoleLogic => labCLI;

        // Keep the existing root-panel event chain working without touching old code.
        protected override string UIID => "LabPanel";

        protected override string GetPrompt()
        {
            char? drive = labCLI != null ? labCLI.CurrentDriveLetter : null;
            string path = labCLI != null ? labCLI.CurrentPath.TrimEnd('/') : string.Empty;
            string displayPath = drive.HasValue
                ? $"{drive}:{(string.IsNullOrEmpty(path) ? "/" : path)}"
                : (string.IsNullOrEmpty(path) ? "~" : "~" + path);
            return $"<color=#7DF2D4>LAB</color> <color=#8CB8FF>{displayPath}</color><color=#FFFFFF>></color> ";
        }

        public override void OnSubmitCommand(string input)
        {
            if (labCLI == null)
            {
                Debug.LogError("[LabTerminalPanel] LabCLI reference is missing.");
                return;
            }

            labCLI.ProcessCommand(input);
        }

        protected override void Awake()
        {
            base.Awake();
            if (labCLI == null)
            {
                labCLI = GetComponent<LabCLI>();
            }
        }

        protected override void Start()
        {
            base.Start();

            if (labCLI == null)
            {
                labCLI = GetComponent<LabCLI>();
                if (labCLI == null)
                {
                    Debug.LogError("[LabTerminalPanel] LabCLI reference is missing and not found on the same GameObject.");
                    return;
                }
            }

            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标滑入 += OnMouseEnterHandler;
            鼠标滑出 += OnMouseExitHandler;
            鼠标点击 += OnMouseClickHandler;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            期望显示面板 -= OnShowPanel;
            期望隐藏面板 -= OnHidePanel;
            鼠标滑入 -= OnMouseEnterHandler;
            鼠标滑出 -= OnMouseExitHandler;
            鼠标点击 -= OnMouseClickHandler;
        }

        protected override void CloseAction()
        {
            FadeOut();
        }

        [Subscribe("LabCLI.Output")]
        private void HandleOutput(object data)
        {
            if (data is DeveloperConsole.ConsoleOutputEvent evt)
            {
                Output(evt.message, evt.color);
            }
        }

        [Subscribe("LabCLI.ScrollToTop")]
        private void HandleScrollToTop(object data)
        {
            _scrollLineIndex = 0;
            _isDirty = true;
        }

        private void OnShowPanel(object data)
        {
            FadeIn();
            StartBreathing();
            labCLI?.EnsureBooted();
            ActivateConsoleInput();
        }

        private void OnHidePanel(object data)
        {
            StopBreathing();
            DeactivateConsoleInput();
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
            ActivateConsoleInput();
        }

        private void ActivateConsoleInput()
        {
            if (inputField == null)
            {
                return;
            }

            FocusConsoleInputNow();

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (_inputActivationCoroutine != null)
            {
                StopCoroutine(_inputActivationCoroutine);
            }

            _inputActivationCoroutine = StartCoroutine(ActivateConsoleInputNextFrame());
        }

        private void DeactivateConsoleInput()
        {
            if (_inputActivationCoroutine != null)
            {
                StopCoroutine(_inputActivationCoroutine);
                _inputActivationCoroutine = null;
            }

            if (inputField != null)
            {
                inputField.DeactivateInputField();
            }
        }

        private IEnumerator ActivateConsoleInputNextFrame()
        {
            yield return null;
            _inputActivationCoroutine = null;
            FocusConsoleInputNow();
        }

        private void FocusConsoleInputNow()
        {
            if (inputField == null)
            {
                return;
            }

            inputField.enabled = true;
            inputField.ActivateInputField();
            inputField.Select();
            UpdateInputLine(inputField.text, inputField.caretPosition);
        }
    }
}
