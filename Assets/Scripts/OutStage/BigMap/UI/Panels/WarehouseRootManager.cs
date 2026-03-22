using System.Collections;
using UnityEngine;

namespace MineRTS.BigMap.UI.Panels
{
    public class WarehouseRootManager : TUIManager
    {
        [Header("Display Settings")]
        [SerializeField] private float blockDelay = 0.04f;
        [SerializeField] private Color titleColor = new Color(1f, 0.84f, 0.55f);
        [SerializeField] private Color borderColor = new Color(0.72f, 0.48f, 0.22f);
        [SerializeField] private Color accentColor = new Color(1f, 0.95f, 0.72f);

        private Coroutine _renderCoroutine;
        private string _lastChangeSummary = string.Empty;

        private static readonly string[] _artLarge = new[]
        {
            "██╗    ██╗ █████╗ ██████╗ ███████╗",
            "██║    ██║██╔══██╗██╔══██╗██╔════╝",
            "██║ █╗ ██║███████║██████╔╝█████╗  ",
            "██║███╗██║██╔══██║██╔══██╗██╔══╝  ",
            "╚███╔███╔╝██║  ██║██║  ██║███████╗",
            " ╚══╝╚══╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚══════╝",
            "",
            "██╗  ██╗ ██████╗ ██╗   ██╗███████╗███████╗",
            "██║  ██║██╔═══██╗██║   ██║██╔════╝██╔════╝",
            "███████║██║   ██║██║   ██║███████╗█████╗  ",
            "██╔══██║██║   ██║██║   ██║╚════██║██╔══╝  ",
            "██║  ██║╚██████╔╝╚██████╔╝███████║███████╗",
            "╚═╝  ╚═╝ ╚═════╝  ╚═════╝ ╚══════╝╚══════╝",
        };

        private static readonly string[] _artMedium = new[]
        {
            "╦ ╦┌─┐┬─┐┌─┐┬ ┬┌─┐┬ ┬┌─┐",
            "║║║├─┤├┬┘├┤ ├─┤│ ││ │└─┐",
            "╚╩╝┴ ┴┴└─└─┘┴ ┴└─┘└─┘└─┘",
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

        [Subscribe("Warehouse.Changed")]
        private void OnWarehouseChanged(object data)
        {
            if (data is PlayerWarehouseManager.WarehouseChangedPayload payload)
            {
                string reason = string.IsNullOrWhiteSpace(payload.Reason) ? "updated" : payload.Reason;
                _lastChangeSummary = $"warehouse {reason} | {payload.Changes.Count} change(s)";
            }
            else
            {
                _lastChangeSummary = "warehouse updated";
            }

            Render();
        }

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

            string packID = PlayerWarehouseManager.Instance != null
                ? PlayerWarehouseManager.Instance.CurrentWarehousePackID
                : PlayerWarehouseManager.DefaultWarehousePackID;

            var art = SelectTemplate(width, ConsoleHeight);
            var artStyle = ArtStyle;
            var accentStyle = AccentStyle;
            var border = BorderStyle.Classic;

            SendLine(TUITool.GenerateTopBorder("◉ Warehouse", width, artStyle, border));

            if (art != null)
            {
                yield return new WaitForSeconds(blockDelay);
                foreach (string line in art)
                {
                    SendLine(TUITool.FormatBoxLine(line, width, artStyle, border));
                    yield return new WaitForSeconds(blockDelay);
                }
            }

            yield return new WaitForSeconds(blockDelay);
            SendLine(TUITool.GenerateDivider(width, accentStyle, $"pack | {packID}", border));

            if (!string.IsNullOrWhiteSpace(_lastChangeSummary))
            {
                yield return new WaitForSeconds(blockDelay);
                SendLine(TUITool.FormatBoxLine(_lastChangeSummary, width, accentStyle, border));
            }

            yield return new WaitForSeconds(blockDelay);
            SendLine(TUITool.GenerateBottomBorder(width, artStyle, border));

            _renderCoroutine = null;
        }

        private static string[] SelectTemplate(int totalWidth, int visibleRows)
        {
            int contentWidth = totalWidth - 4;

            if (contentWidth >= 46 && visibleRows >= 15)
            {
                return _artLarge;
            }

            if (contentWidth >= 28 && visibleRows >= 5)
            {
                return _artMedium;
            }

            return null;
        }

        private void SendLine(string richText)
        {
            PostSystem.Instance.Send("WarehouseRoot.Output",
                new DeveloperConsole.ConsoleOutputEvent { message = richText, color = Color.white });
        }
    }
}
