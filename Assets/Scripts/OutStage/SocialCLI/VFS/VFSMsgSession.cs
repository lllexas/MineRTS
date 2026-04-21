using System;
using System.Collections.Generic;
using System.Text;
using NekoGraph;
using SpaceTUI;
using UnityEngine;

/// <summary>
/// 基于 TUISelectSlot 的 .msg console session。
/// session 负责生命周期和后端放行；
/// 具体的选择输入与重渲染节奏复用 TUISelectSlot。
/// </summary>
public sealed class VFSMsgSession : TUISelectSlot
{
    private const int BoxBleedX = 4;
    private const int BoxBleedY = 1;

    private readonly VFSMsgQueryPayload _payload;

    public VFSMsgSession(VFSMsgQueryPayload payload)
        : base(BuildConfig(payload))
    {
        _payload = payload;
        Refresh();
    }

    public override string SessionId => _payload?.Message?.MessageTag ?? "vfs.msg";
    public override string SessionName => "VFS Message";

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

    public override bool HandleConfirm()
    {
        if (IsResolved)
            return HandleCancel();

        if (!HasItems)
            return HandleCancel();

        bool resumed = ResumeSelectedChoice();
        if (!resumed)
            return false;

        HandleCancel();
        return true;
    }

    public override bool HandleCancel()
    {
        Console?.EndSession(this);
        return true;
    }

    protected override List<string> BuildLines()
    {
        var lines = new List<string>();
        var style = Config.viewStyle;
        var message = _payload?.Message;
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[vfs_msg_session] build-lines msg={0} resolved={1} selectedIndex={2} itemCount={3}",
            message != null ? "ok" : "null",
            IsResolved,
            _payload?.ReplicaMeta?.SelectedChoiceIndex ?? -1,
            ItemCount);
        if (message == null)
            return lines;

        if (!string.IsNullOrWhiteSpace(Config.title))
            lines.Add(RenderStyledLine(Config.title, style.titleStyle, true));

        AddBlankLines(lines, style.topSpacing);

        lines.AddRange(BuildMessageBoxLines(message));

        if (HasItems)
        {
            lines.Add(string.Empty);

            for (int i = 0; i < ItemCount; i++)
            {
                var item = Config.items[i];
                bool selected = i == SelectedIndex;
                lines.Add(BuildItemLine(item, i, selected));
            }
        }

        AddBlankLines(lines, style.bottomSpacing);

        if (!string.IsNullOrWhiteSpace(Config.helpText))
            lines.Add(RenderStyledLine(Config.helpText, style.helpStyle, false));

        return lines;
    }

    protected override bool TryRenderSelectionChange(int previousIndex, int currentIndex)
    {
        if (Console == null || !HasItems)
            return false;

        var optionLines = BuildOptionSectionLines();
        int optionStart = StartLine + GetOptionSectionStartOffset();
        int previousHeight = GetOptionSectionRenderedHeight();
        Console.WriteInputHandleRange(optionStart, previousHeight, optionLines);
        return true;
    }

    private bool ResumeSelectedChoice()
    {
        if (!TryGetSelectedItem(out var item))
            return false;

        var replicaMeta = _payload?.ReplicaMeta;
        if (replicaMeta == null || replicaMeta.IsResolved)
            return false;

        if (item.payload is not string targetNodeId || string.IsNullOrWhiteSpace(targetNodeId))
            return false;

        var runner = GraphHub.Instance?.DefaultRunner;
        if (runner == null)
            return false;

        bool resumed = runner.ResumeSuspendedSignalToTarget(
            replicaMeta.BackendPackID,
            replicaMeta.SignalId,
            replicaMeta.BackendNodeID,
            targetNodeId);

        if (!resumed)
            return false;

        replicaMeta.IsResolved = true;
        replicaMeta.SelectedChoiceIndex = SelectedIndex;
        PersistReplicaMeta(replicaMeta);
        return true;
    }

    private void PersistReplicaMeta(VFSMsgReplicaMeta replicaMeta)
    {
        if (replicaMeta == null || string.IsNullOrWhiteSpace(_payload?.PackID) || string.IsNullOrWhiteSpace(_payload?.VfsPath))
            return;

        var analyser = GraphHub.Instance?.DefaultAnalyser;
        if (analyser?.GetNode(_payload.PackID, _payload.VfsPath, PackAccessSubjects.SystemMin) is VFSNodeData node)
            node.InlineText = VFSMsgReplicaMeta.Serialize(replicaMeta);
    }

    private static TUISelectionConfig BuildConfig(VFSMsgQueryPayload payload)
    {
        var config = TUISelectionConfig.Default;
        config.console = payload?.FrontendContext as ConsoleManager;
        config.title = BuildTitle(payload?.Message);
        config.helpText = BuildHelpText(payload);
        config.items = BuildItems(payload);
        config.viewStyle = BuildViewStyle();

        var interaction = TUISelectionInteractionConfig.Default;
        interaction.wrapNavigation = true;
        interaction.enableDigitSelect = true;
        interaction.allowConfirmOnEmptySubmit = true;
        interaction.onCancel = () => config.console?.EndSession();
        config.interaction = interaction;
        ApplyResolvedSelection(config, payload);
        return config;
    }

    private static IReadOnlyList<TUISelectionItem> BuildItems(VFSMsgQueryPayload payload)
    {
        var items = new List<TUISelectionItem>();
        var choices = payload?.Message?.Choices;
        var targets = payload?.ReplicaMeta?.ChoiceTargetNodeIDs;
        if (choices == null || targets == null)
            return items;

        if (choices.Count != targets.Count)
        {
            Debug.LogError(
                $"<color=red>[VFSMsgSession] 严重错误：VFSMsgSO 配置了 {choices.Count} 个选项，" +
                $"但 graph 节点只连接了 {targets.Count} 个子节点。" +
                $"选项与连线数量不匹配，已截断为 {Math.Min(choices.Count, targets.Count)} 个显示。" +
                $"请检查节点 {payload?.SourceNodeId ?? "(unknown)"} 的连线。</color>");
        }

        int count = Math.Min(choices.Count, targets.Count);
        for (int i = 0; i < count; i++)
        {
            var choice = choices[i];
            bool isResolvedChoice = payload?.ReplicaMeta?.IsResolved == true &&
                                    payload.ReplicaMeta.SelectedChoiceIndex == i;
            string label = string.IsNullOrWhiteSpace(choice?.Text) ? "(empty)" : choice.Text;
            if (isResolvedChoice)
                label = $"[已选择] {label}";

            items.Add(new TUISelectionItem
            {
                key = i + 1,
                indexText = (i + 1).ToString(),
                label = label,
                subtitle = string.IsNullOrWhiteSpace(choice?.ChoiceTag) ? null : choice.ChoiceTag,
                payload = targets[i]
            });
        }

        return items;
    }

    private IEnumerable<string> BuildMessageBoxLines(VFSMsgSO message)
    {
        foreach (string blank in BuildBlankLines(BoxBleedY))
            yield return blank;

        int boxWidth = GetBoxWidth();
        int contentWidth = Math.Max(1, TUITool.CalcContentWidth(boxWidth, MsgBoxStyle) - 2);
        string speaker = SafeValue(message?.Sender);

        var contentLines = new List<string>
        {
            $"Title  : {SafeValue(message?.Title)}",
            string.Empty
        };

        if (!string.IsNullOrWhiteSpace(message?.Body))
        {
            foreach (string wrapped in WrapText(message.Body, contentWidth))
                contentLines.Add("  " + wrapped);
        }
        else
        {
            contentLines.Add("  (empty)");
        }

        foreach (string line in TUITool.GenerateTextBoxWithTitle(contentLines.ToArray(), speaker, boxWidth, MsgBoxStyle))
            yield return BuildBoxLine(line);

        foreach (string blank in BuildBlankLines(BoxBleedY))
            yield return blank;
    }

    private static string BuildTitle(VFSMsgSO message)
    {
        if (message == null)
            return "MESSAGE";

        if (!string.IsNullOrWhiteSpace(message.Title))
            return $"MESSAGE / {message.Title}";

        if (!string.IsNullOrWhiteSpace(message.Sender))
            return $"MESSAGE / {message.Sender}";

        return "MESSAGE";
    }

    private static string BuildHelpText(VFSMsgQueryPayload payload)
    {
        int choiceCount = payload?.Message?.Choices?.Count ?? 0;
        if (payload?.ReplicaMeta?.IsResolved == true)
            return choiceCount > 0
                ? "This message has been resolved. Press Enter / Esc to close."
                : "Press Enter / Esc to close.";

        return choiceCount > 0
            ? "Up/Down or 1-9 to choose, Enter confirm, Esc close."
            : "Enter / Esc close.";
    }

    private static void ApplyResolvedSelection(TUISelectionConfig config, VFSMsgQueryPayload payload)
    {
        if (config.items == null || config.items.Count == 0)
            return;

        var meta = payload?.ReplicaMeta;
        if (meta == null || !meta.IsResolved)
            return;

        int selectedIndex = meta.SelectedChoiceIndex;
        if (selectedIndex < 0 || selectedIndex >= config.items.Count)
            selectedIndex = 0;

        config.initialSelectedKey = config.items[selectedIndex].key;
    }

    private List<string> BuildOptionSectionLines()
    {
        var lines = new List<string>();
        var style = Config.viewStyle;

        if (HasItems)
        {
            lines.Add(string.Empty);

            for (int i = 0; i < ItemCount; i++)
            {
                var item = Config.items[i];
                bool selected = i == SelectedIndex;
                lines.Add(BuildItemLine(item, i, selected));
            }
        }

        AddBlankLines(lines, style.bottomSpacing);

        if (!string.IsNullOrWhiteSpace(Config.helpText))
            lines.Add(RenderStyledLine(Config.helpText, style.helpStyle, false));

        return lines;
    }

    private int GetOptionSectionStartOffset()
    {
        int offset = 0;
        if (!string.IsNullOrWhiteSpace(Config.title))
            offset += 1;

        offset += Config.viewStyle.topSpacing;
        offset += CountMessageBoxLines(_payload?.Message);
        return offset;
    }

    private int GetOptionSectionRenderedHeight()
    {
        int height = 0;
        if (HasItems)
            height += 1 + ItemCount;

        height += Config.viewStyle.bottomSpacing;
        if (!string.IsNullOrWhiteSpace(Config.helpText))
            height += 1;

        return height;
    }

    private int CountMessageBoxLines(VFSMsgSO message)
    {
        int boxWidth = GetBoxWidth();
        int contentWidth = Math.Max(1, TUITool.CalcContentWidth(boxWidth, MsgBoxStyle) - 2);
        int bodyLineCount = string.IsNullOrWhiteSpace(message?.Body)
            ? 1
            : WrapText(message.Body, contentWidth).Count;

        int contentLineCount = 2 + bodyLineCount;
        int borderAndPaddingLines = 2 + (MsgBoxStyle.paddingY * 2);
        return BoxBleedY + borderAndPaddingLines + contentLineCount + BoxBleedY;
    }

    private static TSSStyle MsgBoxStyle => new TSSStyle
    {
        bleedX = 0,
        bleedY = 0,
        paddingX = 1,
        paddingY = 1,
        borderColor = new Color(0.5f, 0.5f, 0.5f),
        contentColor = Color.white,
        titleColor = new Color(0.82f, 0.93f, 1f),
        backgroundColor = null,
        alignment = SpaceTUI.TextAlignment.Left,
        expandArtSpaces = false
    };

    private static TUISelectionViewStyle BuildViewStyle()
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
            borderColor = new Color(0.35f, 0.7f, 0.9f),
            contentColor = new Color(0.82f, 0.93f, 1f),
            titleColor = new Color(0.82f, 0.93f, 1f),
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
            contentColor = new Color(0.92f, 0.92f, 0.92f),
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
            contentColor = new Color(0.6f, 0.6f, 0.6f),
            titleColor = Color.white,
            backgroundColor = null,
            alignment = SpaceTUI.TextAlignment.Left,
            expandArtSpaces = false
        };
        viewStyle.emptyStyle = viewStyle.helpStyle;
        viewStyle.normalState = new TUISelectionStateStyle
        {
            prefixText = "  ",
            contentColor = new Color(0.9f, 0.9f, 0.9f),
            indexColor = new Color(0.55f, 0.85f, 1f),
            prefixColor = null
        };
        viewStyle.selectedState = new TUISelectionStateStyle
        {
            prefixText = "> ",
            contentColor = new Color(1f, 0.96f, 0.72f),
            indexColor = Color.white,
            prefixColor = new Color(1f, 0.8f, 0.35f)
        };
        return viewStyle;
    }

    private string BuildItemLine(TUISelectionItem item, int itemIndex, bool selected)
    {
        if (IsResolved && TryGetResolvedChoiceIndex(out int resolvedIndex))
        {
            if (itemIndex == resolvedIndex)
                selected = true;
            else
                selected = false;
        }

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

    private bool IsResolved => _payload?.ReplicaMeta?.IsResolved == true;

    private bool TryGetResolvedChoiceIndex(out int resolvedIndex)
    {
        resolvedIndex = _payload?.ReplicaMeta?.SelectedChoiceIndex ?? -1;
        return resolvedIndex >= 0 && resolvedIndex < ItemCount;
    }

    private static string BuildSubtitleLine(string subtitle, bool selected)
    {
        Color color = selected ? new Color(0.9f, 0.82f, 0.64f) : new Color(0.65f, 0.65f, 0.65f);
        string colorHex = ColorUtility.ToHtmlStringRGB(color);
        return $"    <color=#{colorHex}>{subtitle}</color>";
    }

    private int GetBoxWidth()
    {
        int consoleWidth = Console?.ConsoleWidth ?? 64;
        int width = Mathf.Max(consoleWidth - BoxBleedX * 2, 42);
        if (width % 2 != 0)
            width--;

        return width;
    }

    private string BuildBoxLine(string line)
    {
        int consoleWidth = Console?.ConsoleWidth ?? 64;
        int leftPad = Math.Max(0, (consoleWidth - GetBoxWidth()) / 2);
        return leftPad > 0 ? new string(' ', leftPad) + line : line;
    }

    private static IEnumerable<string> BuildBlankLines(int count)
    {
        for (int i = 0; i < count; i++)
            yield return string.Empty;
    }

    private static List<string> WrapText(string text, int maxVisualWidth)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add(string.Empty);
            return result;
        }

        string[] paragraphs = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (string rawPara in paragraphs)
        {
            string para = rawPara.Replace("\t", "    ");
            if (string.IsNullOrEmpty(para))
            {
                result.Add(string.Empty);
                continue;
            }

            var sb = new StringBuilder();
            int width = 0;
            bool inTag = false;

            foreach (char c in para)
            {
                if (c == '<')
                {
                    inTag = true;
                    sb.Append(c);
                    continue;
                }

                if (c == '>')
                {
                    inTag = false;
                    sb.Append(c);
                    continue;
                }

                if (inTag)
                {
                    sb.Append(c);
                    continue;
                }

                int charWidth = TUITool.IsWideChar(c) ? 2 : 1;
                if (width + charWidth > maxVisualWidth)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                    width = 0;
                }

                sb.Append(c);
                width += charWidth;
            }

            if (sb.Length > 0)
                result.Add(sb.ToString());
        }

        return result;
    }

    private static string SafeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    }
}
