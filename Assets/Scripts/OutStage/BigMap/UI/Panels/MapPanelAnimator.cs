using UnityEngine;
using UnityEngine.EventSystems;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// 地图根面板动画器
    ///
    /// <para>【UI ID】MapPanel - 用于事件匹配</para>
    /// <para>【职责】地图系统根面板（世界地图、区域选择等）</para>
    /// </summary>
    public class MapPanelAnimator : SpaceUIAnimator
    {
        /// <summary>
        /// UI ID - 由代码硬编码，Inspector 显示但不可改
        /// </summary>
        protected override string UIID => "MapPanel";

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void CloseAction()
        {
            FadeOut();
        }

        private void Start()
        {
            // 追加行为到委托链（子类决定行为内容）
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标滑入 += OnMouseEnterHandler;
            鼠标滑出 += OnMouseExitHandler;
            鼠标点击 += OnMouseClickHandler;
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
            // TODO: 鼠标滑入逻辑
        }

        private void OnMouseExitHandler(PointerEventData eventData)
        {
            // TODO: 鼠标滑出逻辑
        }

        private void OnMouseClickHandler(PointerEventData eventData)
        {
            // TODO: 鼠标点击逻辑
        }
    }
}
