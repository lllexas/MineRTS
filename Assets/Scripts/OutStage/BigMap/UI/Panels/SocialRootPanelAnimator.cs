using UnityEngine;
using UnityEngine.EventSystems;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// 社交根面板动画器
    ///
    /// <para>【UI ID】SocialRootPanel - 用于事件匹配</para>
    /// <para>【职责】社交系统根面板（好友、聊天、公会等）</para>
    /// </summary>
    public class SocialRootPanelAnimator : SpaceUIAnimator
    {
        private void Awake()
        {
            base.Awake();
            _uiID = "SocialRootPanel";
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
