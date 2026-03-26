using System.Collections;
using UnityEngine;
using SpaceTUI;

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
        [Tooltip("块状拆入动画的块间延迟（秒）")]
        [SerializeField] private float blockDelay = 0.04f;

        [Tooltip("标题颜色")]
        [SerializeField] private Color titleColor = new Color(0.2f, 0.8f, 1f);

        [Tooltip("边框颜色")]
        [SerializeField] private Color borderColor = new Color(0.4f, 0.4f, 0.4f);

        [Tooltip("背景填充颜色")]
        [SerializeField] private Color fillColor = new Color(0.1f, 0.1f, 0.15f);

        [Tooltip("通知计数颜色")]
        [SerializeField] private Color notificationColor = new Color(1f, 0.6f, 0.2f);

        private int _unreadCount = 0;
        private Coroutine _renderCoroutine;

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
            "╚═╝└─┘└─┘┴┴ ┴┴─┘╚═╝╩═╝╩",
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
            alignment = SpaceTUI.TextAlignment.Center,
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
            alignment = SpaceTUI.TextAlignment.Center,
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

        /// <summary>ConsoleHeight 被注入新值时自动重新渲染</summary>
        protected override void OnConsoleHeightChanged(int newHeight) => Render();

        // ─────────────────────────────────────────────────────────────
        //  渲染（使用 TUITool）
        // ─────────────────────────────────────────────────────────────

        /// <summary>向面板层推送完整渲染内容（块状拆入动画）</summary>
        public void Render()
        {
            if (_renderCoroutine != null) StopCoroutine(_renderCoroutine);
            _renderCoroutine = StartCoroutine(RenderCoroutine());
        }

        private IEnumerator RenderCoroutine()
        {
            InvokeClearRequested();

            int w = ConsoleWidth;
            // 强制偶数宽（与 TUITable.Render 保持一致）
            if (w % 2 != 0) w--;

            var artStyle   = ArtStyle;
            var notifStyle = NotificationStyle;
            var border     = BorderStyle.Classic;
            string[] art   = SelectTemplate(w, ConsoleHeight);

            // Block 1：顶栏（即刻）
            SendLine(TUITool.GenerateTopBorder("◉ SocialCLI", w, artStyle, border));

            // Block 2：艺术字逐行拆入
            if (art != null)
            {
                yield return new WaitForSeconds(blockDelay);
                foreach (var artLine in art)
                {
                    SendLine(TUITool.FormatBoxLine(artLine, w, artStyle, border));
                    yield return new WaitForSeconds(blockDelay);
                }
            }

            // Block 3：通知区（可选）
            if (_unreadCount > 0)
            {
                string notifText = $"✉ 【{_unreadCount}条新消息】";
                yield return new WaitForSeconds(blockDelay);
                SendLine(TUITool.GenerateDivider(w, notifStyle, notifText, border));
            }

            // Block 4：底栏
            yield return new WaitForSeconds(blockDelay);
            SendLine(TUITool.GenerateBottomBorder(w, artStyle, border));

            _renderCoroutine = null;
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

        /// <summary>根据内容宽度和可见行高选择艺术字模板；返回 null 表示 Small 模式（不显示艺术字）</summary>
        private static string[] SelectTemplate(int totalWidth, int visibleRows)
        {
            // 估算内容宽度：totalWidth - 2*paddingX - 2(边框)
            int contentW = totalWidth - 4;

            // Large 模板 13 行内容 + top + bottom = 15 行总计
            if (contentW >= 44 && visibleRows >= 15) return _artLarge;

            // Medium 模板 3 行内容 + top + bottom = 5 行总计
            if (contentW >= 22 && visibleRows >= 5) return _artMedium;

            return null; // Small：不显示艺术字
        }

        private void SendLine(string richText)
        {
            PostSystem.Instance.Send("SocialRoot.Output",
                new DeveloperConsole.ConsoleOutputEvent { message = richText, color = Color.white });
        }
    }
}
