using UnityEngine;
using UnityEngine.EventSystems;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// 新闻根面板动画器
    ///
    /// <para>【UI ID】NewsRootPanel - 用于事件匹配</para>
    /// <para>【职责】新闻系统根面板（公告、活动、更新日志等）</para>
    /// </summary>
    public class NewsRootPanelAnimator : SpaceUIAnimator
    {
        private void Awake()
        {
            base.Awake();
            _uiID = "NewsRootPanel";
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
            // TODO: 显示面板逻辑
        }

        private void OnHidePanel(object data)
        {
            // TODO: 隐藏面板逻辑
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
