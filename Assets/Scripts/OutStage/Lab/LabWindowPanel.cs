using MineRTS.BigMap.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MineRTS.BigMap.UI.Panels
{
    public class LabWindowPanel : SpaceUIAnimator
    {
        [Header("Lab GUI")]
        [SerializeField] private LabGUI labGUI;

        protected override string UIID => "LabWindowPanel";

        protected override void Awake()
        {
            base.Awake();
            if (labGUI == null)
            {
                labGUI = GetComponent<LabGUI>();
            }
        }

        private void Start()
        {
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

        [Subscribe("LabWindow.Refresh")]
        private void HandleRefresh(object data)
        {
            if (labGUI == null)
            {
                return;
            }

            if (data is LabGUI.DisplayData displayData)
            {
                labGUI.Render(displayData);
            }
        }

        private void OnShowPanel(object data)
        {
            FadeIn();
            StartBreathing();
        }

        private void OnHidePanel(object data)
        {
            StopBreathing();
            FadeOut();
        }

        private void OnMouseEnterHandler(PointerEventData eventData)
        {
            SetTargetScale(new Vector3(1.01f, 1.01f, 1.01f));
            PlayScaleAnimation();
        }

        private void OnMouseExitHandler(PointerEventData eventData)
        {
            ResetScale();
            ResetRotation();
        }

        private void OnMouseClickHandler(PointerEventData eventData)
        {
        }
    }
}
