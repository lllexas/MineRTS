using UnityEngine;
using UnityEngine.EventSystems;
using SpaceTUI;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// 社交根面板动画器
    ///
    /// <para>【UI ID】SocialRootPanel - 用于事件匹配</para>
    /// <para>【职责】社交系统根面板显示层（Tab 标签 + 新消息通知）</para>
    /// <para>【继承】ConsoleDisplayBase&lt;SocialRootManager&gt;（复用流式渲染）</para>
    /// </summary>
    public class SocialRootPanelAnimator : ConsoleDisplayBase<SocialRootManager>
    {
        // =========================================================
        //  UI ID
        // =========================================================
        protected override string UIID => "SocialRootPanel";

        // =========================================================
        //  TUI 管理层
        // =========================================================
        [Header("TUI 管理层")]
        [SerializeField] private SocialRootManager _manager;

        protected override SocialRootManager ConsoleLogic => _manager;

        // =========================================================
        //  新消息通知状态
        // =========================================================
        private int _unreadCount = 0;
        private bool _socialPanelVisible = false;

        // =========================================================
        //  Unity 生命周期
        // =========================================================

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void CloseAction()
        {
            FadeOut();
        }

        protected override void Start()
        {
            base.Start(); // ConsoleDisplayBase.Start：注入 Clear/Width 钩子

            // 追加行为到委托链
            进入根界面 += OnEnterRoot;
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标滑入 += OnMouseEnterHandler;
            鼠标滑出 += OnMouseExitHandler;
            鼠标点击 += OnMouseClickHandler;

            // PostSystem 手动订阅（面板显示/隐藏通知）
            PostSystem.Instance.On("期望显示面板", OnAnyPanelShow);
            PostSystem.Instance.On("期望隐藏面板", OnAnyPanelHide);
        }

        protected override void OnDisable()
        {
            base.OnDisable(); // ConsoleDisplayBase.OnDisable：反订阅 Clear / 滚动条
        }

        protected virtual void OnDestroy()
        {
            // 取消委托订阅
            进入根界面 -= OnEnterRoot;
            期望显示面板 -= OnShowPanel;
            期望隐藏面板 -= OnHidePanel;
            鼠标滑入 -= OnMouseEnterHandler;
            鼠标滑出 -= OnMouseExitHandler;
            鼠标点击 -= OnMouseClickHandler;

            // 反注册 PostSystem 手动订阅
            if (PostSystem.Instance != null)
            {
                PostSystem.Instance.Off("期望显示面板", OnAnyPanelShow);
                PostSystem.Instance.Off("期望隐藏面板", OnAnyPanelHide);
            }
        }

        // =========================================================
        //  PostSystem 标签订阅方法
        // =========================================================

        /// <summary>接收 SocialRootManager 推送的渲染行</summary>
        [Subscribe("SocialRoot.Output")]
        private void HandleRenderLine(object data)
        {
            if (data is DeveloperConsole.ConsoleOutputEvent evt)
                OutputLine(evt.message, evt.color);
        }

        /// <summary>监听 SocialCLI 输出，若 SocialPanel 不可见则累计未读数</summary>
        [Subscribe("SocialCLI.Output")]
        private void OnSocialCLIOutput(object data)
        {
            if (!_socialPanelVisible)
                _manager?.UpdateNotification(++_unreadCount);
        }

        // =========================================================
        //  面板显示状态追踪（手动订阅，data 为面板 ID 字符串）
        // =========================================================

        private void OnAnyPanelShow(object data)
        {
            if (data?.ToString() != "SocialPanel") return;
            _socialPanelVisible = true;
            _unreadCount = 0;
            _manager?.UpdateNotification(0);
        }

        private void OnAnyPanelHide(object data)
        {
            if (data?.ToString() == "SocialPanel")
                _socialPanelVisible = false;
        }

        // =========================================================
        //  SpaceUIAnimator 事件回调
        // =========================================================

        private void OnEnterRoot(object data)
        {
            FadeIn();
        }

        private void OnShowPanel(object data)
        {
            // SocialRootPanel 本身不直接响应面板切换
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
