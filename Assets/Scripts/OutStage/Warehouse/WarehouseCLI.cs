using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatStrategies;
using NekoGraph;
using UnityEngine;
using SpaceTUI;

public class WarehouseCLI : DeveloperConsole
{
    [Header("Warehouse CLI")]
    [SerializeField] private bool autoBootDefaultCommand = true;
    [SerializeField] private string defaultCommandName = "warehouse";
    [SerializeField] private string preferredWarehousePackID = string.Empty;
    [SerializeField] private string itemRootPath = PlayerWarehouseManager.DefaultItemsRootPath;
    [SerializeField] private int[] sampleItemTypes = { 1001, 1002, 1003, 2001, 3001 };

    private bool _defaultCommandBooted;
    private WarehouseTerminalStrategy _activeWarehouseStrategy;

    protected override string GetPreferredPackID()
    {
        if (!string.IsNullOrWhiteSpace(preferredWarehousePackID))
        {
            return preferredWarehousePackID;
        }

        return PlayerWarehouseManager.Instance?.CurrentWarehousePackID;
    }

    protected override void Awake()
    {
        base.Awake();
        AddCommand(defaultCommandName, HandleOpenWarehouseCommand);
    }

    private IEnumerator Start()
    {
        if (!autoBootDefaultCommand)
        {
            yield break;
        }

        yield return null;
        EnsureBooted();
    }

    public void EnsureBooted()
    {
        if (_defaultCommandBooted)
        {
            return;
        }

        _defaultCommandBooted = true;
        ProcessCommand(defaultCommandName);
    }

    public override void Log(string message, Color color)
    {
        PostSystem.Instance.Send("WarehouseCLI.Output", new DeveloperConsole.ConsoleOutputEvent
        {
            message = message,
            color = color
        });
    }

    public override void ScrollConsoleToTop()
    {
        PostSystem.Instance.Send("WarehouseCLI.ScrollToTop", null);
    }

    [Subscribe("Warehouse.Changed")]
    private void OnWarehouseChanged(object data)
    {
        _activeWarehouseStrategy?.Refresh();
    }

    private void HandleOpenWarehouseCommand(string[] args)
    {
        string resolvedItemRootPath = NormalizeItemRootPath(itemRootPath);
        var strategy = new WarehouseTerminalStrategy(this, resolvedItemRootPath, sampleItemTypes);
        _activeWarehouseStrategy = strategy;
        SetActiveStrategy(strategy, CurrentPath);
    }

    private void OnWarehouseStrategyClosed(WarehouseTerminalStrategy strategy)
    {
        if (ReferenceEquals(_activeWarehouseStrategy, strategy))
        {
            _activeWarehouseStrategy = null;
        }
    }

    private static string NormalizeItemRootPath(string path)
    {
        string normalized = string.IsNullOrWhiteSpace(path)
            ? PlayerWarehouseManager.DefaultItemsRootPath
            : VFSPathResolver.Normalize(path);
        return normalized.EndsWith("/") ? normalized : normalized + "/";
    }

    private sealed class WarehouseTerminalStrategy : CatStrategyBase
    {
        private readonly WarehouseCLI _console;
        private readonly string _itemRootPath;
        private readonly int[] _sampleItemTypes;
        private WarehouseTerminalSlot _slot;

        public WarehouseTerminalStrategy(WarehouseCLI console, string itemRootPath, int[] sampleItemTypes)
        {
            _console = console;
            _itemRootPath = itemRootPath;
            _sampleItemTypes = sampleItemTypes ?? Array.Empty<int>();
        }

        public override void Execute(string vfsPath, string graphPath = null)
        {
            _console.ClearConsole();

            var config = BuildConfig();
            _slot = new WarehouseTerminalSlot(config, BuildDetailLines);
            _console.BeginSession(_slot);
            _console.ScrollConsoleToTop();
        }

        public override void OnInput(string input)
        {
            _slot?.HandleSubmit(input);
        }

        public override void Close()
        {
            if (_slot != null)
            {
                _console.EndSession(_slot);
                _slot = null;
            }

            _console.OnWarehouseStrategyClosed(this);
        }

        public override void OnArrowKey(bool isUp)
        {
            _slot?.HandleNavigation(isUp ? ConsoleNavKey.Up : ConsoleNavKey.Down);
        }

        public override void OnConfirm()
        {
            _slot?.HandleConfirm();
        }

        public void Refresh()
        {
            if (_slot == null)
            {
                return;
            }

            _slot.UpdateConfig(BuildConfig(), resetSelection: false);
        }

        private TUISelectionConfig BuildConfig()
        {
            var config = TUISelectionConfig.Default;
            config.console = _console;
            config.title = "WAREHOUSE TERMINAL";
            config.helpText = "Up/Down to navigate, Enter to refresh, Esc to close the session.";
            config.items = BuildItems();
            config.viewStyle = BuildViewStyle();

            var interaction = TUISelectionInteractionConfig.Default;
            interaction.wrapNavigation = true;
            interaction.enableDigitSelect = true;
            interaction.allowConfirmOnEmptySubmit = true;
            interaction.onConfirmSelection = (_, __) => Refresh();
            interaction.onCancel = () => _console.CloseActiveStrategy();
            config.interaction = interaction;

            return config;
        }

        private IReadOnlyList<TUISelectionItem> BuildItems()
        {
            return new List<TUISelectionItem>
            {
                new TUISelectionItem
                {
                    key = 1,
                    indexText = "1",
                    label = "Overview",
                    subtitle = "Warehouse Pack status, mount state, and root path.",
                    payload = "overview"
                },
                new TUISelectionItem
                {
                    key = 2,
                    indexText = "2",
                    label = "Sample Items",
                    subtitle = "Configured item counts fetched through PlayerWarehouseManager.",
                    payload = "items"
                },
                new TUISelectionItem
                {
                    key = 3,
                    indexText = "3",
                    label = "Batch Guide",
                    subtitle = "How this terminal should talk to the warehouse batch processor.",
                    payload = "batch"
                }
            };
        }

        private TUISelectionViewStyle BuildViewStyle()
        {
            var viewStyle = TUISelectionViewStyle.Default;
            viewStyle.topSpacing = 1;
            viewStyle.bottomSpacing = 1;
            viewStyle.titleStyle = new TSSStyle
            {
                bleedX = 0,
                bleedY = 0,
                paddingX = 0,
                paddingY = 0,
                spacingX = 1,
                borderColor = new Color(0.8f, 0.55f, 0.25f),
                contentColor = new Color(1f, 0.92f, 0.68f),
                titleColor = new Color(1f, 0.92f, 0.68f),
                backgroundColor = null,
                alignment = SpaceTUI.TextAlignment.Left,
                expandArtSpaces = false
            };
            viewStyle.itemStyle = new TSSStyle
            {
                bleedX = 0,
                bleedY = 0,
                paddingX = 0,
                paddingY = 0,
                spacingX = 1,
                borderColor = Color.gray,
                contentColor = new Color(0.95f, 0.86f, 0.72f),
                titleColor = Color.white,
                backgroundColor = null,
                alignment = SpaceTUI.TextAlignment.Left,
                expandArtSpaces = false
            };
            viewStyle.helpStyle = new TSSStyle
            {
                bleedX = 0,
                bleedY = 0,
                paddingX = 0,
                paddingY = 0,
                spacingX = 1,
                borderColor = Color.gray,
                contentColor = new Color(0.72f, 0.72f, 0.72f),
                titleColor = Color.white,
                backgroundColor = null,
                alignment = SpaceTUI.TextAlignment.Left,
                expandArtSpaces = false
            };
            viewStyle.emptyStyle = viewStyle.helpStyle;
            viewStyle.normalState = new TUISelectionStateStyle
            {
                prefixText = "  ",
                contentColor = new Color(0.95f, 0.86f, 0.72f),
                indexColor = new Color(1f, 0.72f, 0.4f),
                prefixColor = null
            };
            viewStyle.selectedState = new TUISelectionStateStyle
            {
                prefixText = "> ",
                contentColor = new Color(1f, 0.98f, 0.82f),
                indexColor = Color.white,
                prefixColor = new Color(1f, 0.72f, 0.4f)
            };
            return viewStyle;
        }

        private string[] BuildDetailLines(TUISelectionItem item)
        {
            string mode = item.payload as string ?? string.Empty;
            return mode switch
            {
                "items" => BuildSampleItemLines(),
                "batch" => BuildBatchGuideLines(),
                _ => BuildOverviewLines()
            };
        }

        private string[] BuildOverviewLines()
        {
            string packID = ResolveWarehousePackID();
            var analyser = GraphAnalyser.Instance;
            var pack = !string.IsNullOrWhiteSpace(packID) ? analyser?.GetPack(packID, PackAccessSubjects.Player) : null;
            string access = pack != null
                ? (GraphHub.Instance?.GetPackAccessLevel(GraphInstanceSlot.Player, pack)
                    ?? analyser.GetPackAccessLevel(pack, PackAccessSubjects.Player)).ToString()
                : "(unmounted)";
            string system = pack != null ? pack.System.ToString() : "(unknown)";

            return new[]
            {
                $"Warehouse pack : {SafeValue(packID)}",
                $"Mounted        : {(pack != null ? "yes" : "no")}",
                $"Pack system    : {system}",
                $"Access level   : {access}",
                $"Pack root      : {_itemRootPath}",
                $"Current path   : {_console.CurrentPath}",
                "Warehouse body is a Pack. VFS is its use.",
                "This terminal is only an access surface and batch entry point."
            };
        }

        private string[] BuildSampleItemLines()
        {
            if (_sampleItemTypes.Length == 0)
            {
                return new[]
                {
                    "No sample item types are configured on WarehouseCLI.",
                    "Set sampleItemTypes in the inspector if you want live counters here."
                };
            }

            string packID = ResolveWarehousePackID();
            var lines = new List<string>
            {
                $"Pack      : {SafeValue(packID)}",
                $"Pack root  : {_itemRootPath}",
                string.Empty
            };

            foreach (int itemType in _sampleItemTypes.Where(id => id > 0).Distinct())
            {
                long count = PlayerWarehouseManager.Instance != null
                    ? PlayerWarehouseManager.Instance.GetCount(itemType, packID, _itemRootPath)
                    : 0;
                lines.Add($"Item {itemType,-6} -> {count}");
            }

            return lines.ToArray();
        }

        private string[] BuildBatchGuideLines()
        {
            return new[]
            {
                "Entry point:",
                "PlayerWarehouseManager.Instance.CreateBatch(reason, packID, itemRootPath)",
                string.Empty,
                "Typical flow:",
                "1. CreateBatch(...)",
                "2. Add / Consume / Set / Delete",
                "3. Preview(batch) or CanApply(batch)",
                "4. Apply(batch)",
                string.Empty,
                "This panel should not own warehouse truth.",
                "It should only inspect and submit batches to the warehouse Pack."
            };
        }

        private string ResolveWarehousePackID()
        {
            if (PlayerWarehouseManager.Instance != null &&
                !string.IsNullOrWhiteSpace(PlayerWarehouseManager.Instance.CurrentWarehousePackID))
            {
                return PlayerWarehouseManager.Instance.CurrentWarehousePackID;
            }

            return _console.GetPreferredPackID();
        }

        private static string SafeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }

    private sealed class WarehouseTerminalSlot : TUISelectSlot
    {
        private readonly Func<TUISelectionItem, string[]> _detailLinesProvider;

        public WarehouseTerminalSlot(TUISelectionConfig config, Func<TUISelectionItem, string[]> detailLinesProvider)
            : base(config)
        {
            _detailLinesProvider = detailLinesProvider;
            Refresh();
        }

        protected override bool TryNavigate(ConsoleNavKey key)
        {
            switch (key)
            {
                case ConsoleNavKey.Up:
                case ConsoleNavKey.Left:
                    MoveSelection(-1);
                    return true;

                case ConsoleNavKey.Down:
                case ConsoleNavKey.Right:
                    MoveSelection(1);
                    return true;

                default:
                    return false;
            }
        }

        protected override List<string> BuildLines()
        {
            var lines = new List<string>();
            var style = Config.viewStyle;

            if (!string.IsNullOrWhiteSpace(Config.title))
            {
                lines.Add(RenderStyledLine(Config.title, style.titleStyle, true));
            }

            AddBlankLines(lines, style.topSpacing);

            if (TryGetSelectedItem(out var selectedItem))
            {
                foreach (string line in _detailLinesProvider?.Invoke(selectedItem) ?? Array.Empty<string>())
                {
                    lines.Add(string.IsNullOrEmpty(line)
                        ? string.Empty
                        : RenderStyledLine(line, style.itemStyle, false));
                }

                lines.Add(string.Empty);
                lines.Add(RenderStyledLine("Sections", style.helpStyle, false));
            }

            if (!HasItems)
            {
                lines.Add(RenderStyledLine(Config.emptyText, style.emptyStyle, false));
            }
            else
            {
                for (int i = 0; i < ItemCount; i++)
                {
                    var item = Config.items[i];
                    bool selected = i == SelectedIndex;
                    lines.Add(BuildItemLine(item, selected));

                    if (!string.IsNullOrWhiteSpace(item.subtitle))
                    {
                        lines.Add(BuildSubtitleLine(item.subtitle, selected));
                    }
                }
            }

            AddBlankLines(lines, style.bottomSpacing);

            if (!string.IsNullOrWhiteSpace(Config.helpText))
            {
                lines.Add(RenderStyledLine(Config.helpText, style.helpStyle, false));
            }

            return lines;
        }

        private string BuildItemLine(TUISelectionItem item, bool selected)
        {
            var state = selected ? Config.viewStyle.selectedState : Config.viewStyle.normalState;
            string prefix = state.prefixText ?? string.Empty;
            string indexText = string.IsNullOrWhiteSpace(item.indexText) ? item.key.ToString() : item.indexText;
            string plain = $"{prefix}[{indexText}] {item.label}";
            string padding = BuildLeftPadding(plain, Config.viewStyle.itemStyle.alignment);

            string prefixHex = ColorUtility.ToHtmlStringRGB(state.prefixColor ?? state.contentColor);
            string indexHex = ColorUtility.ToHtmlStringRGB(state.indexColor);
            string contentHex = ColorUtility.ToHtmlStringRGB(state.contentColor);

            return
                $"{padding}<color=#{prefixHex}>{prefix}</color>" +
                $"<color=#{indexHex}>[{indexText}]</color>" +
                $"<color=#{contentHex}> {item.label}</color>";
        }

        private string BuildSubtitleLine(string subtitle, bool selected)
        {
            Color color = selected ? new Color(0.92f, 0.82f, 0.64f) : new Color(0.7f, 0.66f, 0.6f);
            string colorHex = ColorUtility.ToHtmlStringRGB(color);
            return $"    <color=#{colorHex}>{subtitle}</color>";
        }
    }
}
