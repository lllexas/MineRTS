using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatStrategies;
using NekoGraph;
using UnityEngine;
using SpaceTUI;

public class LabCLI : DeveloperConsole
{
    [Header("Lab CLI")]
    [SerializeField] private bool autoBootDefaultCommand = true;
    [SerializeField] private string defaultCommandName = "lab";
    [SerializeField] private string preferredVFSPackID = string.Empty;
    [SerializeField] private string windowPanelID = "LabWindowPanel";
    [SerializeField] private string windowTitle = "Lab";
    [SerializeField] private string labEntriesRootPath = "/";

    private bool _defaultCommandBooted;
    private LabTerminalStrategy _activeLabStrategy;

    protected override string GetPreferredPackID()
    {
        if (!string.IsNullOrWhiteSpace(preferredVFSPackID))
            return preferredVFSPackID;

        return GraphHub.Instance?.GetFacade<LabFacade>()?.ResolvedPackID;
    }

    protected override void Awake()
    {
        base.Awake();
        AddCommand(defaultCommandName, HandleOpenLabCommand);
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

    public void CloseLabSession()
    {
        CloseActiveStrategy();
    }

    public override void Log(string message, Color color)
    {
        PostSystem.Instance.Send("LabCLI.Output", new DeveloperConsole.ConsoleOutputEvent
        {
            message = message,
            color = color
        });
    }

    public override void ScrollConsoleToTop()
    {
        PostSystem.Instance.Send("LabCLI.ScrollToTop", null);
    }

    [Subscribe(LabFacade.LabChangedEvent)]
    private void OnLabChanged(object data)
    {
        _activeLabStrategy?.Refresh();
    }

    private void HandleOpenLabCommand(string[] args)
    {
        var strategy = new LabTerminalStrategy(this, windowPanelID, windowTitle, labEntriesRootPath);
        _activeLabStrategy = strategy;
        SetActiveStrategy(strategy, CurrentPath);
    }

    private void OnLabStrategyClosed(LabTerminalStrategy strategy)
    {
        if (ReferenceEquals(_activeLabStrategy, strategy))
        {
            _activeLabStrategy = null;
        }
    }

    private sealed class LabTerminalStrategy : CatStrategyBase
    {
        private readonly LabCLI _console;
        private readonly string _windowPanelID;
        private readonly string _windowTitle;
        private readonly string _labEntriesRootPath;
        private LabSelectionSlot _slot;

        public LabTerminalStrategy(LabCLI console, string windowPanelID, string windowTitle, string labEntriesRootPath)
        {
            _console = console;
            _windowPanelID = windowPanelID;
            _windowTitle = windowTitle;
            _labEntriesRootPath = labEntriesRootPath;
        }

        public override void Execute(string vfsPath, string graphPath = null)
        {
            _console.ClearConsole();

            var config = BuildConfig();

            _slot = new LabSelectionSlot(config);
            _console.BeginSession(_slot);
            _console.ScrollConsoleToTop();

            if (config.items.Count > 0)
            {
                PublishWindow(config.items[0]);
            }
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

            PostSystem.Instance.Send("期望隐藏面板", _windowPanelID);
            _console.OnLabStrategyClosed(this);
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

            var config = BuildConfig();
            _slot.UpdateConfig(config, resetSelection: false);

            if (config.items.Count > 0)
            {
                int selectedIndex = Mathf.Clamp(_slot.SelectedIndex, 0, config.items.Count - 1);
                PublishWindow(config.items[selectedIndex]);
            }
        }

        private TUISelectionConfig BuildConfig()
        {
            var config = TUISelectionConfig.Default;
            config.console = _console;
            config.title = "LAB TERMINAL";
            config.helpText = "Up/Down to navigate, Enter to pin, Esc to close the window panel.";
            config.items = BuildItems();
            config.viewStyle = BuildViewStyle();
            config.interaction = BuildInteractionConfig();
            return config;
        }

        private IReadOnlyList<TUISelectionItem> BuildItems()
        {
            var facade = GraphHub.Instance?.GetFacade<LabFacade>();
            var analyser = GraphHub.Instance?.DefaultAnalyser;
            var nodes = facade != null ? facade.ListEntryNodes(analyser, PackAccessSubjects.Player) : null;

            var items = new List<TUISelectionItem>();
            if (nodes == null || nodes.Count == 0)
            {
                items.Add(new TUISelectionItem
                {
                    key = 1,
                    indexText = "1",
                    label = "(No lab entries)",
                    subtitle = "Lab is empty. Add .labentry files to the lab pack.",
                    payload = null
                });
                return items;
            }

            int index = 1;
            foreach (var node in nodes)
            {
                var entry = facade?.GetLabEntry(node);
                string label = entry?.EntryId ?? node.Name;
                string subtitle = string.IsNullOrWhiteSpace(entry?.Description)
                    ? BuildDefaultSubtitle(entry?.EntityBlueprint)
                    : entry.Description;

                if (facade?.IsUnlocked(node) ?? false)
                {
                    label = $"[已解锁] {label}";
                }

                items.Add(new TUISelectionItem
                {
                    key = index,
                    indexText = index.ToString(),
                    label = label,
                    subtitle = subtitle,
                    payload = node
                });
                index++;
            }

            return items;
        }

        private static string BuildDefaultSubtitle(EntityBlueprintSO blueprint)
        {
            if (blueprint == null)
                return "No entity blueprint.";
            return $"Unlocks: {blueprint.DisplayName ?? blueprint.BlueprintId}";
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
                borderColor = new Color(0.35f, 0.8f, 0.75f),
                contentColor = new Color(0.8f, 1f, 0.95f),
                titleColor = new Color(0.8f, 1f, 0.95f),
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
                contentColor = new Color(0.75f, 0.9f, 1f),
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
                contentColor = new Color(0.65f, 0.75f, 0.8f),
                titleColor = Color.white,
                backgroundColor = null,
                alignment = SpaceTUI.TextAlignment.Left,
                expandArtSpaces = false
            };
            viewStyle.emptyStyle = viewStyle.helpStyle;
            viewStyle.normalState = new TUISelectionStateStyle
            {
                prefixText = "  ",
                contentColor = new Color(0.75f, 0.9f, 1f),
                indexColor = new Color(0.4f, 0.8f, 1f),
                prefixColor = null
            };
            viewStyle.selectedState = new TUISelectionStateStyle
            {
                prefixText = "> ",
                contentColor = new Color(0.95f, 1f, 0.65f),
                indexColor = Color.white,
                prefixColor = new Color(1f, 0.9f, 0.35f)
            };
            return viewStyle;
        }

        private TUISelectionInteractionConfig BuildInteractionConfig()
        {
            var interaction = TUISelectionInteractionConfig.Default;
            interaction.wrapNavigation = true;
            interaction.enableDigitSelect = true;
            interaction.allowConfirmOnEmptySubmit = true;
            interaction.onSelectionChanged = (_, item) => PublishWindow(item);
            interaction.onConfirmSelection = OnLabEntrySelected;
            interaction.onCancel = () => PostSystem.Instance.Send("期望隐藏面板", _windowPanelID);
            return interaction;
        }

        private void OnLabEntrySelected(int index, TUISelectionItem item)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                "[lab_cli] entry-selected index={0} label={1}", index, item.label);

            if (item.payload is not VFSNodeData node)
            {
                Debug.LogWarning("[lab_cli] selected item has no VFSNodeData payload");
                return;
            }

            var content = VFSContentResolver.Resolve(node);
            if (content == null)
            {
                Debug.LogWarning("[lab_cli] VFSContentResolver.Resolve returned null");
                return;
            }

            var labFacade = GraphHub.Instance?.GetFacade<LabFacade>();
            var queryContext = new VFSQueryContext
            {
                PackID = labFacade?.ResolvedPackID,
                VfsPath = node.Name + node.Extension,
                RequestName = LabClientViewKeys.Inspect,
                Node = node,
                SubjectLevel = PackAccessSubjects.Player,
                FrontendContext = _console
            };

            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                "[lab_cli] triggering query pack={0} path={1}", queryContext.PackID, queryContext.VfsPath);

            var result = VFSLabEntryResource.Query(content, queryContext);
            if (result == null)
            {
                Debug.LogWarning("[lab_cli] VFSLabEntryResource.Query returned null");
                return;
            }

            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                "[lab_cli] query result presentationType={0} title={1}", result.PresentationType, result.Title);

            bool presented = _console.ClientRuntime?.TryPresent(result) ?? false;
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                "[lab_cli] TryPresent returned {0}", presented);
        }

        private void PublishWindow(TUISelectionItem item)
        {
            string[] lines;
            string footer = item.subtitle;

            if (item.payload is VFSNodeData node)
            {
                lines = BuildEntryLines(node, out footer);
            }
            else
            {
                var payload = item.payload as string ?? string.Empty;
                lines = payload switch
                {
                    "workspace" => BuildWorkspaceLines(),
                    "drives" => BuildDriveLines(),
                    _ => BuildOverviewLines()
                };
            }

            PostSystem.Instance.Send("LabWindow.Refresh", new LabGUI.DisplayData
            {
                Title = $"{_windowTitle} / {item.label}",
                Lines = lines,
                Footer = footer
            });
            PostSystem.Instance.Send("期望显示面板", _windowPanelID);
        }

        private string[] BuildEntryLines(VFSNodeData node, out string footer)
        {
            footer = "无法读取条目";

            var facade = GraphHub.Instance?.GetFacade<LabFacade>();
            if (facade == null || node == null)
                return new[] { "LabFacade 不可用，无法读取条目详情。" };

            var entry = facade.GetLabEntry(node);
            if (entry == null)
                return new[] { "该 .labentry 节点无法解析为 LabEntrySO。" };

            var blueprint = entry.EntityBlueprint;
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                lines.Add(entry.Description);
                lines.Add(string.Empty);
            }

            lines.Add($"Entry ID : {entry.EntryId}");
            lines.Add($"Node     : {node.NodeID}");
            lines.Add(string.Empty);

            if (blueprint != null)
            {
                lines.Add($"Entity   : {blueprint.DisplayName ?? blueprint.BlueprintId}");
                lines.Add($"Faction  : {FormatFaction(blueprint.Faction)}");
                lines.Add($"Type     : {BuildUnitTypeLine(blueprint.UnitType)}");
                lines.Add($"HP       : {blueprint.MaxHealth:0}");
                lines.Add($"Size     : {blueprint.LogicSize.x}x{blueprint.LogicSize.y}");
                lines.Add(string.Empty);
            }
            else
            {
                lines.Add("Entity   : (none)");
                lines.Add(string.Empty);
            }

            if (entry.UnlockCosts != null && entry.UnlockCosts.Length > 0)
            {
                lines.Add("Unlock Costs:");
                foreach (var cost in entry.UnlockCosts)
                    lines.Add($"  - Resource {cost.ResourceType}: {cost.Amount}");
            }
            else
            {
                lines.Add("Unlock Costs: Free");
            }

            footer = facade.IsUnlocked(node)
                ? "[已解锁] 该实体已加入仓库"
                : "Enter 查看详情，输入 unlock 解锁该条目";

            return lines.ToArray();
        }

        private string[] BuildOverviewLines()
        {
            return new[]
            {
                $"Current drive : {FormatDrive()}",
                $"Current path  : {_console.CurrentPath}",
                $"Console size  : {_console.ConsoleWidth} x {_console.ConsoleHeight}",
                $"Mounted packs : {_console.GetDriveMap().Count}",
                "Terminal mode is the primary access path.",
                "Window mode is summoned on demand for supplemental display."
            };
        }

        private string[] BuildWorkspaceLines()
        {
            return new[]
            {
                $"Preferred pack : {SafeValue(_console.GetPreferredPackID())}",
                $"Resolved pack  : {SafeValue(_console.CurrentVFSPackID)}",
                $"Working path   : {_console.CurrentPath}",
                $"Prompt drive   : {FormatDrive()}",
                "Use this view to keep side information visible while the TUI stays in control."
            };
        }

        private string[] BuildDriveLines()
        {
            var drives = _console.GetDriveMap();
            if (drives.Count == 0)
            {
                return new[] { "No mounted packs are currently exposed as drives." };
            }

            return drives
                .Select(entry => $"{entry.letter}:  {entry.packID}")
                .ToArray();
        }

        private string FormatDrive()
        {
            return _console.CurrentDriveLetter.HasValue
                ? _console.CurrentDriveLetter.Value.ToString()
                : "N/A";
        }

        private static string SafeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }

        private static string FormatFaction(int faction)
        {
            return faction switch
            {
                0 => "Protocol",
                1 => "SunCity",
                2 => "Gaia",
                _ => $"F:{faction}"
            };
        }

        private static string BuildUnitTypeLine(int unitTypeMask)
        {
            if (unitTypeMask == UnitType.None)
                return "None";

            var parts = new List<string>();
            if ((unitTypeMask & UnitType.Hero) != 0) parts.Add("Hero");
            if ((unitTypeMask & UnitType.Minion) != 0) parts.Add("Minion");
            if ((unitTypeMask & UnitType.Building) != 0) parts.Add("Building");
            if ((unitTypeMask & UnitType.ResourceItem) != 0) parts.Add("Resource");
            if ((unitTypeMask & UnitType.Projectile) != 0) parts.Add("Projectile");
            if ((unitTypeMask & UnitType.Flyer) != 0) parts.Add("Flyer");
            return string.Join("|", parts);
        }
    }

    private sealed class LabSelectionSlot : TUISelectSlot
    {
        public LabSelectionSlot(TUISelectionConfig config) : base(config)
        {
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
            Color color = selected ? new Color(0.78f, 0.84f, 0.65f) : new Color(0.55f, 0.65f, 0.72f);
            string colorHex = ColorUtility.ToHtmlStringRGB(color);
            return $"    <color=#{colorHex}>{subtitle}</color>";
        }
    }
}
