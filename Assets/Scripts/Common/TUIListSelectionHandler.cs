using System.Collections.Generic;
using UnityEngine;

public sealed class TUIListSelectionHandler : TUISelectSlot
{
    public TUIListSelectionHandler(TUISelectionConfig config)
        : base(config)
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
        var config = Config;

        AddBlankLines(lines, config.viewStyle.topSpacing);

        if (!string.IsNullOrEmpty(config.title))
        {
            lines.Add(RenderStyledLine(config.title, config.viewStyle.titleStyle, useTitleColor: true));
        }

        if (!HasItems)
        {
            lines.Add(RenderStyledLine(config.emptyText, config.viewStyle.emptyStyle, useTitleColor: false));
        }
        else
        {
            for (int i = 0; i < ItemCount; i++)
            {
                lines.Add(RenderItemLine(config.items[i], i == SelectedIndex));
            }
        }

        if (!string.IsNullOrEmpty(config.helpText))
        {
            lines.Add(RenderStyledLine(config.helpText, config.viewStyle.helpStyle, useTitleColor: false));
        }

        AddBlankLines(lines, config.viewStyle.bottomSpacing);
        return lines;
    }

    private string RenderItemLine(TUISelectionItem item, bool selected)
    {
        var config = Config;
        var stateStyle = selected ? config.viewStyle.selectedState : config.viewStyle.normalState;
        var itemStyle = config.viewStyle.itemStyle;

        string indexText = string.IsNullOrEmpty(item.indexText) ? item.key.ToString() : item.indexText;
        string plainText = $"{stateStyle.prefixText}[ {indexText} ] {item.label}";

        if (!string.IsNullOrEmpty(item.subtitle))
        {
            plainText += $" {item.subtitle}";
        }

        string leftPadding = BuildLeftPadding(plainText, itemStyle.alignment);
        string prefixColorHex = ColorUtility.ToHtmlStringRGB(stateStyle.prefixColor ?? stateStyle.contentColor);
        string indexColorHex = ColorUtility.ToHtmlStringRGB(stateStyle.indexColor);
        string labelColorHex = ColorUtility.ToHtmlStringRGB(stateStyle.contentColor);
        string subtitleColorHex = ColorUtility.ToHtmlStringRGB(itemStyle.titleColor);

        string richText =
            $"{leftPadding}" +
            $"<color=#{prefixColorHex}>{stateStyle.prefixText}</color>" +
            $"<color=#{indexColorHex}>[ {indexText} ]</color>" +
            $" <color=#{labelColorHex}>{item.label}</color>";

        if (!string.IsNullOrEmpty(item.subtitle))
        {
            richText += $" <color=#{subtitleColorHex}>{item.subtitle}</color>";
        }

        return richText;
    }
}
