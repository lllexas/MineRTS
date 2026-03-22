using System.Collections;
using UnityEngine;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// LabRootManager - 科技树根面板管理器
    /// </summary>
    public class LabRootManager : TUIManager
    {
        [Header("Display Settings")]
        [SerializeField] private float blockDelay = 0.04f;

        [SerializeField] private Color titleColor = new Color(0.45f, 1f, 0.85f);
        [SerializeField] private Color borderColor = new Color(0.3f, 0.75f, 0.6f);
        [SerializeField] private Color accentColor = new Color(0.9f, 1f, 0.55f);

        private Coroutine _renderCoroutine;

        // Large: LAB 艺术字 6 行，行宽约 24 字符 (每行从左到右 = L + A + B)
        // 触发：contentW（= ConsoleWidth - 4）≥ 28
        private static readonly string[] _artLarge = new[]
        {
            "█╗     ██╗  ███╗ ",
            "█║    █╔═█╗ █╔═█╗",
            "█║    ████║ ███╔╝",
            "█║ ╔╗ █╔═█║ █╔═█║",
            "████╣ █║ █║ ███╔╝",
            "╚═══╝ ╚╝ ╚╝ ╚══╝ ",
        };

        // Medium: LAB 3 行紧凑版，行宽 12 字符
        // 触发：contentW ≥ 16
        private static readonly string[] _artMedium = new[]
        {
            "╦  ╔═╗╦═╗",
            "║  ╠═╣╠═╣",
            "╩═╝╩ ╩╩═╝",
        };

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
            expandArtSpaces = true
        };

        private TSSStyle AccentStyle => new TSSStyle
        {
            bleedX = 0,
            bleedY = 0,
            paddingX = 1,
            paddingY = 0,
            borderColor = borderColor,
            contentColor = accentColor,
            backgroundColor = null,
            alignment = TextAlignment.Center,
            expandArtSpaces = false
        };

        protected override void OnConsoleWidthChanged(int newWidth) => Render();

        protected override void OnConsoleHeightChanged(int newHeight) => Render();

        public void Render()
        {
            if (_renderCoroutine != null)
            {
                StopCoroutine(_renderCoroutine);
            }

            _renderCoroutine = StartCoroutine(RenderCoroutine());
        }

        private IEnumerator RenderCoroutine()
        {
            InvokeClearRequested();

            int width = ConsoleWidth;
            if (width % 2 != 0)
            {
                width--;
            }

            var artStyle = ArtStyle;
            var accentStyle = AccentStyle;
            var border = BorderStyle.Classic;
            string[] art = SelectTemplate(width, ConsoleHeight);

            SendLine(TUITool.GenerateTopBorder("◉ Lab", width, artStyle, border));

            if (art != null)
            {
                yield return new WaitForSeconds(blockDelay);
                foreach (string artLine in art)
                {
                    SendLine(TUITool.FormatBoxLine(artLine, width, artStyle, border));
                    yield return new WaitForSeconds(blockDelay);
                }
            }

            SendLine(TUITool.GenerateDivider(width, accentStyle, "research | formulas | blueprints", border));

            yield return new WaitForSeconds(blockDelay);
            SendLine(TUITool.GenerateBottomBorder(width, artStyle, border));

            _renderCoroutine = null;
        }

        private static string[] SelectTemplate(int totalWidth, int visibleRows)
        {
            if (visibleRows >= 8)
                return _artLarge;

            if (visibleRows >= 5)
                return _artMedium;

            return null;
        }

        private void SendLine(string richText)
        {
            PostSystem.Instance.Send("LabRoot.Output",
                new DeveloperConsole.ConsoleOutputEvent { message = richText, color = Color.white });
        }
    }
}
