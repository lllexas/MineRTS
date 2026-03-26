using UnityEngine;
using UnityEngine.EventSystems;
using SpaceTUI;

namespace MineRTS.BigMap.UI.Panels
{
    public class WarehouseRootPanelAnimator : ConsoleDisplayBase<WarehouseRootManager>
    {
        [Header("Warehouse Root")]
        [SerializeField] private WarehouseRootManager manager;

        protected override WarehouseRootManager ConsoleLogic => manager;

        protected override string UIID => "WarehouseRootPanel";

        protected override void Awake()
        {
            if (manager == null)
            {
                manager = GetComponent<WarehouseRootManager>();
            }

            base.Awake();
        }

        protected override void CloseAction()
        {
            FadeOut();
        }

        protected override void Start()
        {
            base.Start();

            if (manager == null)
            {
                manager = GetComponent<WarehouseRootManager>();
                if (manager == null)
                {
                    Debug.LogError("[WarehouseRootPanelAnimator] WarehouseRootManager reference is missing and not found on the same GameObject.");
                    return;
                }
            }

            进入根界面 += OnEnterRoot;
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标滑入 += OnMouseEnterHandler;
            鼠标滑出 += OnMouseExitHandler;
            鼠标点击 += OnMouseClickHandler;
        }

        protected virtual void OnDestroy()
        {
            进入根界面 -= OnEnterRoot;
            期望显示面板 -= OnShowPanel;
            期望隐藏面板 -= OnHidePanel;
            鼠标滑入 -= OnMouseEnterHandler;
            鼠标滑出 -= OnMouseExitHandler;
            鼠标点击 -= OnMouseClickHandler;
        }

        [Subscribe("WarehouseRoot.Output")]
        private void HandleRenderLine(object data)
        {
            if (data is DeveloperConsole.ConsoleOutputEvent evt)
            {
                OutputLine(evt.message, evt.color);
            }
        }

        private void OnEnterRoot(object data)
        {
            FadeIn();
        }

        private void OnShowPanel(object data)
        {
        }

        private void OnHidePanel(object data)
        {
            FadeOut();
        }

        private void OnMouseEnterHandler(PointerEventData eventData)
        {
        }

        private void OnMouseExitHandler(PointerEventData eventData)
        {
        }

        private void OnMouseClickHandler(PointerEventData eventData)
        {
            PostSystem.Instance.Send("期望显示面板", "WarehousePanel");
        }
    }
}
