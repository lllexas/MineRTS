using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using SpaceTUI;

namespace MineRTS.BigMap.UI.Panels
{
    public class WarehousePanelAnimator : ConsolePanelBase<WarehouseCLI>
    {
        [Header("Warehouse CLI")]
        [SerializeField] private WarehouseCLI warehouseCLI;

        private Coroutine _inputActivationCoroutine;

        protected override WarehouseCLI ConsoleLogic => warehouseCLI;

        protected override string UIID => "WarehousePanel";

        protected override string GetPrompt()
        {
            char? drive = warehouseCLI != null ? warehouseCLI.CurrentDriveLetter : null;
            string path = warehouseCLI != null ? warehouseCLI.CurrentPath.TrimEnd('/') : string.Empty;
            string displayPath = drive.HasValue
                ? $"{drive}:{(string.IsNullOrEmpty(path) ? "/" : path)}"
                : (string.IsNullOrEmpty(path) ? "~" : "~" + path);
            return $"<color=#F2B86C>WH</color> <color=#FFF2C4>{displayPath}</color><color=#FFFFFF>></color> ";
        }

        public override void OnSubmitCommand(string input)
        {
            if (warehouseCLI == null)
            {
                Debug.LogError("[WarehousePanelAnimator] WarehouseCLI reference is missing.");
                return;
            }

            warehouseCLI.ProcessCommand(input);
        }

        protected override void Awake()
        {
            base.Awake();
            if (warehouseCLI == null)
            {
                warehouseCLI = GetComponent<WarehouseCLI>();
            }
        }

        protected override void Start()
        {
            base.Start();

            if (warehouseCLI == null)
            {
                warehouseCLI = GetComponent<WarehouseCLI>();
                if (warehouseCLI == null)
                {
                    Debug.LogError("[WarehousePanelAnimator] WarehouseCLI reference is missing and not found on the same GameObject.");
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

        [Subscribe("WarehouseCLI.Output")]
        private void HandleOutput(object data)
        {
            if (data is DeveloperConsole.ConsoleOutputEvent evt)
            {
                Output(evt.message, evt.color);
            }
        }

        [Subscribe("WarehouseCLI.ScrollToTop")]
        private void HandleScrollToTop(object data)
        {
            _scrollLineIndex = 0;
            _isDirty = true;
        }

        private void OnShowPanel(object data)
        {
            FadeIn();
            StartBreathing();
            warehouseCLI?.EnsureBooted();
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
