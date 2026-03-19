using UnityEngine;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// SocialRootManager - 社交根面板管理器
    ///
    /// <para>【职责】管理 SocialRootPanel 的显示内容，通过事件推送渲染内容给 SocialRootPanelAnimator</para>
    /// <para>【TUI 风格】使用制表符和 ASCII 艺术字显示；艺术字随面板宽度自适应三级模板</para>
    /// <para>【继承】TUIManager（提供 ConsoleWidth / OnClearRequested 接口）</para>
    /// </summary>
    public class SocialRootManager : TUIManager
    {
        [Header("显示设置")]
        [Tooltip("标题颜色")]
        [SerializeField] private Color titleColor = new Color(0.2f, 0.8f, 1f);

        [Tooltip("边框颜色")]
        [SerializeField] private Color borderColor = new Color(0.4f, 0.4f, 0.4f);

        [Tooltip("背景填充颜色")]
        [SerializeField] private Color fillColor = new Color(0.1f, 0.1f, 0.15f);

        [Tooltip("通知计数颜色")]
        [SerializeField] private Color notificationColor = new Color(1f, 0.6f, 0.2f);

        private int _unreadCount = 0;

        // ─────────────────────────────────────────────────────────────
        //  艺术字模板（每元素为一行纯内容，不含边框）
        // ─────────────────────────────────────────────────────────────

        // Large：Social 6 行 + 空行 + CLI 6 行，各行约 39 / 19 字符宽
        // 触发：contentW（= ConsoleWidth - 4）≥ 44
        private static readonly string[] _artLarge = new[]
        {
            "███████╗ ██████╗  ██████╗██╗ █████╗ ██╗",
            "██╔════╝██╔═══██╗██╔════╝██║██╔══██╗██║",
            "███████╗██║   ██║██║     ██║███████║██║",
            "╚════██║██║   ██║██║     ██║██╔══██║██║",
            "███████║╚██████╔╝╚██████╗██║██║  ██║██║",
            "╚══════╝ ╚═════╝  ╚═════╝╚═╝╚═╝  ╚═╝╚═╝",
            "",
            " ██████╗██╗     ██╗",
            "██╔════╝██║     ██║",
            "██║     ██║     ██║",
            "██║     ██║     ██║",
            "╚██████╗███████╗██║",
            " ╚═════╝╚══════╝╚═╝",
        };

        // Medium：SocialCLI 3 行紧凑框线风格，各行约 23 字符宽
        // 触发：contentW ≥ 22
        private static readonly string[] _artMedium = new[]
        {
            "╔═╗┌─┐┌─┐┬┌─┐┬  ╔═╗╦  ╦",
            "╚═╗│ ││  │├─┤│  ║  ║  ║",
            "╚═╝└─┘└─┘┴└─┘┴─┘╚═╝╩═╝╩",
        };

        // Small：contentW < 22，不显示艺术字，仅 Tab 头 + 底栏边框

        // ─────────────────────────────────────────────────────────────
        //  TSS 样式
        // ─────────────────────────────────────────────────────────────

        /// <summary>获取艺术字区域的 TSS 样式</summary>
        private TSSStyle ArtStyle => new TSSStyle
        {
            bleedX = 0,
            bleedY = 0,
            paddingX = 1,
            paddingY = 0,
            borderColor = borderColor,
            contentColor = titleColor,
            backgroundColor = null,
            alignment = TextAlignment.Center,
            expandArtSpaces = true // 自动将艺术字空格扩展为双倍
        };

        /// <summary>获取通知区域的 TSS 样式</summary>
        private TSSStyle NotificationStyle => new TSSStyle
        {
            bleedX = 0,
            bleedY = 0,
            paddingX = 1,
            paddingY = 0,
            borderColor = borderColor,
            contentColor = notificationColor,
            backgroundColor = null,
            alignment = TextAlignment.Center,
            expandArtSpaces = false
        };

        // ─────────────────────────────────────────────────────────────
        //  Unity 生命周期
        // ─────────────────────────────────────────────────────────────

        private void Start()
        {
            // 不在此处手动 Render()。
            // ConsoleDisplayBase.Start() 会注入 ConsoleWidth，
            // 触发 OnConsoleWidthChanged → Render()。
        }

        /// <summary>ConsoleWidth 被注入新值时自动重新渲染</summary>
        protected override void OnConsoleWidthChanged(int newWidth) => Render();

        // ─────────────────────────────────────────────────────────────
        //  渲染（使用 TUITool）
        // ─────────────────────────────────────────────────────────────

        /// <summary>向面板层推送完整渲染内容（先清屏，再逐行推送）</summary>
        public void Render()
        {
            InvokeClearRequested();

            int w = ConsoleWidth;
            var artStyle = ArtStyle;
            var notifStyle = NotificationStyle;

            // ── 顶栏 ──────────────────────────────────────────────────
            SendLine(TUITool.GenerateTopBorder("◉ SocialCLI", w, artStyle));

            // ── 艺术字内容区 ─────────────────────────────────────────
            string[] art = SelectTemplate(w);
            if (art != null)
            {
                string[] lines = TUITool.GenerateTextBox(art, w, artStyle);
                foreach (var line in lines)
                {
                    SendLine(line);
                }
            }

            // ── 通知区（条件显示）────────────────────────────────────
            if (_unreadCount > 0)
            {
                string notifText = $"✉ 【{_unreadCount}条新消息】";
                SendLine(TUITool.GenerateDivider(w, notifStyle, '·', notifText));
            }

            // ── 底栏 ──────────────────────────────────────────────────
            SendLine(TUITool.GenerateBottomBorder(w, artStyle));
        }

        // ─────────────────────────────────────────────────────────────
        //  公开操作
        // ─────────────────────────────────────────────────────────────

        /// <summary>更新未读计数并重新渲染</summary>
        public void UpdateNotification(int count)
        {
            _unreadCount = count;
            Render();
        }

        /// <summary>设置自定义颜色</summary>
        public void SetColors(Color title, Color border, Color fill)
        {
            titleColor = title;
            borderColor = border;
            fillColor = fill;
            Render();
        }

        // ─────────────────────────────────────────────────────────────
        //  私有辅助
        // ─────────────────────────────────────────────────────────────

        /// <summary>根据内容宽度选择艺术字模板；返回 null 表示 Small 模式（不显示艺术字）</summary>
        private static string[] SelectTemplate(int totalWidth)
        {
            // 估算内容宽度：totalWidth - 2*paddingX - 2(边框)
            int contentW = totalWidth - 4;
            
            if (contentW >= 44) return _artLarge;
            if (contentW >= 22) return _artMedium;
            return null;
        }

        private void SendLine(string richText)
        {
            PostSystem.Instance.Send("SocialRoot.Output",
                new DeveloperConsole.ConsoleOutputEvent { message = richText, color = Color.white });
        }
    }
}
