using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// TSS (Terminal Style Sheet) - 终端样式表
/// ═══════════════════════════════════════════════════════════════
/// 像 CSS 一样声明 TUI 组件的样式喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public struct TSSStyle
{
    // ── 盒模型（所有单位均为"视觉列数"）──────────────────────────────
    /// <summary>左右出血边（TUI 框距离终端边缘的留白）</summary>
    public int bleedX;
    
    /// <summary>上下出血边（TUI 框上下方的空行数）</summary>
    public int bleedY;
    
    /// <summary>左右页边距（边框到内容的间距）</summary>
    public int paddingX;

    /// <summary>上下页边距（边框到内容的空行数）</summary>
    public int paddingY;

    /// <summary>列间距（表格列与列之间的空格数）</summary>
    public int spacingX;
    
    // ── 边框样式 ─────────────────────────────────────────────────
    /// <summary>边框颜色</summary>
    public Color borderColor;

    /// <summary>内容颜色</summary>
    public Color contentColor;

    /// <summary>标题颜色（用于顶栏标题、表头等）</summary>
    public Color titleColor;

    /// <summary>背景颜色（可选，用于填充）</summary>
    public Color? backgroundColor;
    
    // ── 文本对齐 ─────────────────────────────────────────────────
    /// <summary>水平对齐方式</summary>
    public TextAlignment alignment;
    
    /// <summary>是否将艺术字中的单空格扩展为双空格（适配 2 宽制表符字体）</summary>
    public bool expandArtSpaces;
    
    // ── 默认值 ───────────────────────────────────────────────────
    public static TSSStyle Default => new TSSStyle
    {
        bleedX = 0,
        bleedY = 0,
        paddingX = 1,
        paddingY = 0,
        spacingX = 1,
        borderColor = Color.gray,
        contentColor = Color.white,
        titleColor = Color.cyan,
        backgroundColor = null,
        alignment = TextAlignment.Center,
        expandArtSpaces = false
    };
}

/// <summary>
/// 水平对齐方式
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right
}
