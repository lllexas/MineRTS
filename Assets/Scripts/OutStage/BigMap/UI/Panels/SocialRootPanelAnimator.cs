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
        /// <summary>
        /// UI ID - 由代码硬编码，Inspector 显示但不可改
        /// </summary>
        protected override string UIID => "SocialRootPanel";

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
            进入根界面 += OnEnterRoot;
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标滑入 += OnMouseEnterHandler;
            鼠标滑出 += OnMouseExitHandler;
            鼠标点击 += OnMouseClickHandler;
        }

        /// <summary>
        /// 进入根界面时显示面板
        /// </summary>
        private void OnEnterRoot(object data)
        {
            FadeIn();
        }

        private void OnShowPanel(object data)
        {
            // TODO: 显示面板逻辑
        }

        private void OnHidePanel(object data)
        {
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
            // 点击社交根面板，打开社交主面板
            PostSystem.Instance.Send("期望显示面板", "SocialPanel");
            Debug.Log("<color=cyan>[SocialRootPanel]</color> 点击打开 SocialPanel");
        }
    }
}
