using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatStrategies;
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

    private bool _defaultCommandBooted;

    protected override string GetPreferredPackID()
    {
        return string.IsNullOrWhiteSpace(preferredVFSPackID) ? null : preferredVFSPackID;
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

    private void HandleOpenLabCommand(string[] args)
    {
        SetActiveStrategy(new LabTerminalStrategy(this, windowPanelID, windowTitle), CurrentPath);
    }

    private sealed class LabTerminalStrategy : CatStrategyBase
    {
        private readonly LabCLI _console;
        private readonly string _windowPanelID;
        private readonly string _windowTitle;
        private LabSelectionSlot _slot;

        public LabTerminalStrategy(LabCLI console, string windowPanelID, string windowTitle)
        {
            _console = console;
            _windowPanelID = windowPanelID;
            _windowTitle = windowTitle;
        }

        public override void Execute(string vfsPath, string graphPath = null)
        {
            _console.ClearConsole();

            var items = BuildItems();
            var config = TUISelectionConfig.Default;
            config.console = _console;
            config.title = "LAB TERMINAL";
            config.helpText = "Up/Down to navigate, Enter to pin, Esc to close the window panel.";
            config.items = items;
            config.viewStyle = BuildViewStyle();
            config.interaction = BuildInteractionConfig();

            _slot = new LabSelectionSlot(config);
            _console.MountInputHandler(_slot);
            _console.ScrollConsoleToTop();

            if (items.Count > 0)
            {
                PublishWindow(items[0]);
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
                _console.UnmountInputHandler(_slot);
                _slot = null;
            }

            PostSystem.Instance.Send("期望隐藏面板", _windowPanelID);
        }

        public override void OnArrowKey(bool isUp)
        {
            _slot?.HandleNavigation(isUp ? ConsoleNavKey.Up : ConsoleNavKey.Down);
        }

        public override void OnConfirm()
        {
            _slot?.HandleConfirm();
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
                    subtitle = "Session summary and current terminal state.",
                    payload = "overview"
                },
                new TUISelectionItem
                {
                    key = 2,
                    indexText = "2",
                    label = "Workspace",
                    subtitle = "Current drive and working path snapshot.",
                    payload = "workspace"
                },
                new TUISelectionItem
                {
                    key = 3,
                    indexText = "3",
                    label = "Drives",
                    subtitle = "Mounted packs exposed as terminal drives.",
                    payload = "drives"
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
            interaction.onConfirmSelection = (_, item) => PublishWindow(item);
            interaction.onCancel = () => PostSystem.Instance.Send("期望隐藏面板", _windowPanelID);
            return interaction;
        }

        private void PublishWindow(TUISelectionItem item)
        {
            var payload = item.payload as string ?? string.Empty;
            var lines = payload switch
            {
                "workspace" => BuildWorkspaceLines(),
                "drives" => BuildDriveLines(),
                _ => BuildOverviewLines()
            };

            PostSystem.Instance.Send("LabWindow.Refresh", new LabGUI.DisplayData
            {
                Title = $"{_windowTitle} / {item.label}",
                Lines = lines,
                Footer = item.subtitle
            });
            PostSystem.Instance.Send("期望显示面板", _windowPanelID);
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
