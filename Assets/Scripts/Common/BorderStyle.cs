using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// BorderStyle - 边框样式定义
/// ═══════════════════════════════════════════════════════════════
/// 描述一个完整边框的各个组成部分，所有字符可自定义喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public struct BorderStyle
{
    // ── 四个角 ─────────────────────────────────────────────────
    /// <summary>左上角字符</summary>
    public char topLeft;
    
    /// <summary>右上角字符</summary>
    public char topRight;
    
    /// <summary>左下角字符</summary>
    public char bottomLeft;
    
    /// <summary>右下角字符</summary>
    public char bottomRight;
    
    // ── 边框线 ─────────────────────────────────────────────────
    /// <summary>横线字符</summary>
    public char horizontal;
    
    /// <summary>竖线字符</summary>
    public char vertical;
    
    // ── 标题包裹符 ─────────────────────────────────────────────
    /// <summary>标题左括号</summary>
    public char titleLeft;
    
    /// <summary>标题右括号</summary>
    public char titleRight;
    
    // ── 分隔符（用于通知栏等） ─────────────────────────────────
    /// <summary>左分隔符</summary>
    public char dividerLeft;
    
    /// <summary>右分隔符</summary>
    public char dividerRight;
    
    // ── 填充字符 ───────────────────────────────────────────────
    /// <summary>分隔线填充字符（如·）</summary>
    public char dividerFill;
    
    // ─────────────────────────────────────────────────────────────
    //  预设样式
    // ─────────────────────────────────────────────────────────────
    
    /// <summary>经典单线边框样式</summary>
    public static BorderStyle Classic => new BorderStyle
    {
        topLeft = '┌', topRight = '┐',
        bottomLeft = '└', bottomRight = '┘',
        horizontal = '─', vertical = '│',
        titleLeft = '[', titleRight = ']',
        dividerLeft = '├', dividerRight = '┤',
        dividerFill = '·'
    };
    
    /// <summary>双线边框样式</summary>
    public static BorderStyle Double => new BorderStyle
    {
        topLeft = '╔', topRight = '╗',
        bottomLeft = '╚', bottomRight = '╝',
        horizontal = '═', vertical = '║',
        titleLeft = '[', titleRight = ']',
        dividerLeft = '╠', dividerRight = '╣',
        dividerFill = '·'
    };
    
    /// <summary>简洁样式（无标题括号）</summary>
    public static BorderStyle Simple => new BorderStyle
    {
        topLeft = '┌', topRight = '┐',
        bottomLeft = '└', bottomRight = '┘',
        horizontal = '─', vertical = '│',
        titleLeft = ' ', titleRight = ' ',
        dividerLeft = '├', dividerRight = '┤',
        dividerFill = '─'
    };
    
    // ─────────────────────────────────────────────────────────────
    //  生成方法
    // ─────────────────────────────────────────────────────────────
    
    /// <summary>
    /// 生成顶栏：左上角 + 横线 + 标题左 + title + 标题右 + 横线 + 右上角
    /// <para>公式：totalWidth = topLeft + h×n1 + titleLeft + title + titleRight + h×n2 + topRight</para>
    /// </summary>
    public string GenerateTop(string title, int totalWidth)
    {
        int w_corner = TUITool.GetCharVisualWidth(topLeft);
        int w_bracket = TUITool.GetCharVisualWidth(titleLeft);
        int w_fill = TUITool.GetCharVisualWidth(horizontal);
        int titleVisWidth = TUITool.GetVisualWidth(title);
        
        // 固定部分：左上 + 右上 + 标题左 + 标题右 + title
        int fixedWidth = 2 * w_corner + 2 * w_bracket + titleVisWidth;
        
        // fill 总视觉宽度
        int totalFillVisualWidth = Mathf.Max(0, totalWidth - fixedWidth);
        
        // fill 字符总数
        int totalFillCharCount = TUITool.CalcFillCharCount(horizontal, totalFillVisualWidth);

        // 平分到 title 两侧
        int n1 = totalFillCharCount / 2;
        int n2 = totalFillCharCount - n1;

        return $"{topLeft}{new string(horizontal, n1)}{titleLeft}{title}{titleRight}{new string(horizontal, n2)}{topRight}";
    }
    
    /// <summary>
    /// 生成底栏：左下角 + 横线×n + 右下角
    /// <para>公式：totalWidth = bottomLeft + h×n + bottomRight</para>
    /// </summary>
    public string GenerateBottom(int totalWidth)
    {
        int w_corner = TUITool.GetCharVisualWidth(bottomLeft);
        int w_fill = TUITool.GetCharVisualWidth(horizontal);
        
        // 固定部分：左下 + 右下
        int fixedWidth = 2 * w_corner;
        
        // fill 视觉宽度
        int fillVisualWidth = Mathf.Max(0, totalWidth - fixedWidth);
        
        // fill 字符数
        int fillCharCount = TUITool.CalcFillCharCount(horizontal, fillVisualWidth);

        return $"{bottomLeft}{new string(horizontal, fillCharCount)}{bottomRight}";
    }
    
    /// <summary>
    /// 生成内容行：竖线 + padding + content + padding + 竖线
    /// <para>borderColorHex 单独控制竖线颜色，contentColorHex 控制内容颜色，均可为 null（不着色）</para>
    /// </summary>
    public string GenerateContentLine(string content, int totalWidth, int paddingX, TextAlignment alignment = TextAlignment.Center, string contentColorHex = null, string borderColorHex = null)
    {
        // 竖线宽度
        int w_vertical = TUITool.GetCharVisualWidth(vertical);

        // 内容区宽度 = totalWidth - 2*竖线 - 2*paddingX
        int contentWidth = totalWidth - 2 * w_vertical - 2 * paddingX;

        // 计算内容视觉宽度
        int visLen = TUITool.GetVisualWidth(content);

        // 根据对齐方式计算 padding
        int padLen = alignment switch
        {
            TextAlignment.Left => 0,
            TextAlignment.Center => Mathf.Max(0, (contentWidth - visLen) / 2),
            TextAlignment.Right => Mathf.Max(0, contentWidth - visLen),
            _ => 0
        };

        string leftPad  = new string(' ', paddingX + padLen);
        string rightPad = new string(' ', Mathf.Max(0, contentWidth - visLen - padLen + paddingX));

        // 竖线：独立着色
        string vert = borderColorHex != null
            ? $"<color=#{borderColorHex}>{vertical}</color>"
            : vertical.ToString();

        // 内容：独立着色
        string inner = contentColorHex != null
            ? $"<color=#{contentColorHex}>{leftPad}{content}{rightPad}</color>"
            : $"{leftPad}{content}{rightPad}";

        return $"{vert}{inner}{vert}";
    }
    
    /// <summary>
    /// 生成空行：竖线 + 空格×n + 竖线
    /// </summary>
    public string GenerateEmptyLine(int totalWidth, int paddingX)
    {
        int w_vertical = TUITool.GetCharVisualWidth(vertical);
        
        // 内容区宽度 = totalWidth - 2*竖线
        int contentWidth = totalWidth - 2 * w_vertical;
        
        // 生成带 padding 的空格
        string spaces = new string(' ', contentWidth);
        
        return $"{vertical}{spaces}{vertical}";
    }
    
    /// <summary>
    /// 生成通知分隔线：左分隔 + 填充×n + [text] + 填充×n + 右分隔
    /// </summary>
    public string GenerateDivider(int totalWidth, string text = null, char? fillChar = null)
    {
        char fill = fillChar ?? dividerFill;
        int w_divider = TUITool.GetCharVisualWidth(dividerLeft);
        int w_fill = TUITool.GetCharVisualWidth(fill);

        // 固定部分：左分隔 + 右分隔
        int fixedWidth = 2 * w_divider;

        if (string.IsNullOrEmpty(text))
        {
            // 纯分隔线
            int fillVisualWidth = Mathf.Max(0, totalWidth - fixedWidth);
            int fillCharCount = TUITool.CalcFillCharCount(fill, fillVisualWidth);
            string fillStr = new string(fill, fillCharCount);

            return $"{dividerLeft}{fillStr}{dividerRight}";
        }
        else
        {
            // 带文本的分隔线
            int textVisWidth = TUITool.GetVisualWidth(text);
            int fillVisualWidth = Mathf.Max(0, totalWidth - fixedWidth - textVisWidth);
            int fillCharCountEachSide = TUITool.CalcFillCharCount(fill, fillVisualWidth / 2);
            string fillStr = new string(fill, fillCharCountEachSide);

            return $"{dividerLeft}{fillStr}{text}{fillStr}{dividerRight}";
        }
    }
}

