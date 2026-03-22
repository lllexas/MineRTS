using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 选择项在某个状态下的视觉样式。
/// </summary>
public struct TUISelectionStateStyle
{
    /// <summary>行首前缀文本，例如 "  " 或 "> "。</summary>
    public string prefixText;

    /// <summary>条目主体文本颜色。</summary>
    public Color contentColor;

    /// <summary>序号文本颜色。</summary>
    public Color indexColor;

    /// <summary>前缀文本颜色；为 null 时沿用 <see cref="contentColor"/>。</summary>
    public Color? prefixColor;

    public static TUISelectionStateStyle Default => new TUISelectionStateStyle
    {
        prefixText = "  ",
        contentColor = Color.white,
        indexColor = Color.cyan,
        prefixColor = null
    };
}

/// <summary>
/// 选择组件的视觉配置。
/// 只描述“如何显示”，不描述“如何处理输入”。
/// </summary>
public struct TUISelectionViewStyle
{
    /// <summary>普通状态样式。</summary>
    public TUISelectionStateStyle normalState;

    /// <summary>选中状态样式。</summary>
    public TUISelectionStateStyle selectedState;

    /// <summary>标题样式。</summary>
    public TSSStyle titleStyle;

    /// <summary>条目基础样式。</summary>
    public TSSStyle itemStyle;

    /// <summary>帮助提示样式。</summary>
    public TSSStyle helpStyle;

    /// <summary>空列表提示样式。</summary>
    public TSSStyle emptyStyle;

    /// <summary>列表前的空行数。</summary>
    public int topSpacing;

    /// <summary>列表后的空行数。</summary>
    public int bottomSpacing;

    public static TUISelectionViewStyle Default => new TUISelectionViewStyle
    {
        normalState = TUISelectionStateStyle.Default,
        selectedState = new TUISelectionStateStyle
        {
            prefixText = "> ",
            contentColor = new Color(0.4f, 1f, 0.4f),
            indexColor = Color.white,
            prefixColor = null
        },
        titleStyle = TSSStyle.Default,
        itemStyle = TSSStyle.Default,
        helpStyle = TSSStyle.Default,
        emptyStyle = TSSStyle.Default,
        topSpacing = 0,
        bottomSpacing = 0
    };
}

/// <summary>
/// 单个选择项的数据快照。
/// 用于把业务对象先映射成稳定的显示模型。
/// </summary>
public struct TUISelectionItem
{
    /// <summary>业务键，通常用于数字直选或确认回调。</summary>
    public int key;

    /// <summary>序号文本，例如 "1"、"A"。</summary>
    public string indexText;

    /// <summary>主标签文本。</summary>
    public string label;

    /// <summary>可选副标题或补充信息。</summary>
    public string subtitle;

    /// <summary>可选业务载荷，由应用层解释。</summary>
    public object payload;

    /// <summary>当前项被确认时执行的应用层动作。</summary>
    public Action onConfirm;
}

/// <summary>
/// 选择组件的交互配置。
/// 描述“如何进行选择”以及“选择后做什么”。
/// </summary>
public struct TUISelectionInteractionConfig
{
    /// <summary>是否循环导航。</summary>
    public bool wrapNavigation;

    /// <summary>是否允许数字直选。</summary>
    public bool enableDigitSelect;

    /// <summary>是否允许空输入时按回车直接确认当前项。</summary>
    public bool allowConfirmOnEmptySubmit;

    /// <summary>Esc 取消时的回调。</summary>
    public Action onCancel;

    /// <summary>选中项变化时的回调。</summary>
    public Action<int, TUISelectionItem> onSelectionChanged;

    /// <summary>确认当前项时的回调。</summary>
    public Action<int, TUISelectionItem> onConfirmSelection;

    public static TUISelectionInteractionConfig Default => new TUISelectionInteractionConfig
    {
        wrapNavigation = true,
        enableDigitSelect = true,
        allowConfirmOnEmptySubmit = true,
        onCancel = null,
        onSelectionChanged = null,
        onConfirmSelection = null
    };
}

/// <summary>
/// 选择组件的完整配置。
/// 将视觉规则、交互规则和数据源组合在一起。
/// </summary>
public struct TUISelectionConfig
{
    /// <summary>组件标题；为空则不显示。</summary>
    public string title;

    /// <summary>帮助文本；为空则不显示。</summary>
    public string helpText;

    /// <summary>空列表时显示的文本。</summary>
    public string emptyText;

    /// <summary>默认选中项的 key；-1 表示由组件自行决定。</summary>
    public int initialSelectedKey;

    /// <summary>所属控制台引用。</summary>
    public DeveloperConsole console;

    /// <summary>选择项数据源。</summary>
    public IReadOnlyList<TUISelectionItem> items;

    /// <summary>视觉配置。</summary>
    public TUISelectionViewStyle viewStyle;

    /// <summary>交互配置。</summary>
    public TUISelectionInteractionConfig interaction;

    public static TUISelectionConfig Default => new TUISelectionConfig
    {
        title = null,
        helpText = null,
        emptyText = "暂无可选项",
        initialSelectedKey = -1,
        console = null,
        items = Array.Empty<TUISelectionItem>(),
        viewStyle = TUISelectionViewStyle.Default,
        interaction = TUISelectionInteractionConfig.Default
    };
}
