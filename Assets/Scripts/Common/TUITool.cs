using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// TUITool - TUI 工具类（布局 + 格式化）
/// ═══════════════════════════════════════════════════════════════
/// 像 CSS 布局一样处理 TUI 组件，返回 RichText 字符串喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public static class TUITool
{
    // ─────────────────────────────────────────────────────────────
    //  Box Drawing 字符定义
    // ─────────────────────────────────────────────────────────────
    
    private static readonly char BOX_TOP_LEFT = '┌';
    private static readonly char BOX_TOP_RIGHT = '┐';
    private static readonly char BOX_BOTTOM_LEFT = '└';
    private static readonly char BOX_BOTTOM_RIGHT = '┘';
    private static readonly char BOX_HORIZONTAL = '─';
    private static readonly char BOX_VERTICAL = '│';
    private static readonly char BOX_T_LEFT = '├';
    private static readonly char BOX_T_RIGHT = '┤';
    
    // ─────────────────────────────────────────────────────────────
    //  基础：视觉宽度计算
    // ─────────────────────────────────────────────────────────────
    
    /// <summary>
    /// 获取单个字符的视觉宽度
    /// <para>ASCII = 1, CJK/BoxDrawing = 2</para>
    /// </summary>
    public static int GetCharVisualWidth(char c)
    {
        return IsWideChar(c) ? 2 : 1;
    }

    /// <summary>
    /// 计算字符串的视觉宽度
    /// <para>ASCII = 1, CJK/BoxDrawing = 2</para>
    /// </summary>
    public static int GetVisualWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        int width = 0;
        bool inTag = false;

        foreach (char c in text)
        {
            // 跳过 RichText 标签
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (inTag) continue;

            width += GetCharVisualWidth(c);
        }

        return width;
    }

    /// <summary>
    /// 判断字符是否为双宽字符（CJK / BoxDrawing / 全角等）
    /// </summary>
    public static bool IsWideChar(char c)
    {
        return (c >= 0x1100 && c <= 0x115F)  // Hangul Jamo
            || (c >= 0x2500 && c <= 0x25FF)  // Box Drawing + Geometric Shapes（制表符 + 几何图形）
            || (c >= 0x2E80 && c <= 0x303F)  // CJK 部首 / 符号
            || (c >= 0x3040 && c <= 0x33FF)  // 日文假名 / CJK 扩展
            || (c >= 0x3400 && c <= 0x4DBF)  // CJK Extension A
            || (c >= 0x4E00 && c <= 0x9FFF)  // CJK 统一汉字
            || (c >= 0xAC00 && c <= 0xD7AF)  // 韩文音节
            || (c >= 0xF900 && c <= 0xFAFF)  // CJK 兼容
            || (c >= 0xFE10 && c <= 0xFE6F)  // 竖排 / 小写形式
            || (c >= 0xFF00 && c <= 0xFF60)  // 全角 ASCII
            || (c >= 0xFFE0 && c <= 0xFFE6); // 全角符号
    }

    /// <summary>
    /// 计算填充字符的数量（用于边框等）
    /// <para>例如：需要填充视觉宽度 10，字符是'─'(宽 2)，返回 5</para>
    /// </summary>
    public static int CalcFillCharCount(char fillChar, int targetVisualWidth)
    {
        int charWidth = GetCharVisualWidth(fillChar);
        return Mathf.Max(0, targetVisualWidth / charWidth);
    }
    
    /// <summary>
    /// 将艺术字中的连续空格扩展为双倍长度（适配 2 宽制表符字体）
    /// </summary>
    public static string ExpandArtSpaces(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(text, " +", m => new string(' ', m.Length * 2));
    }
    
    // ─────────────────────────────────────────────────────────────
    //  布局计算
    // ─────────────────────────────────────────────────────────────
    
    /// <summary>
    /// 计算内容区域宽度
    /// <para>contentWidth = totalWidth - 2*bleedX - 2*paddingX - 2(边框)</para>
    /// </summary>
    public static int CalcContentWidth(int totalWidth, TSSStyle style)
    {
        return totalWidth - 2 * style.bleedX - 2 * style.paddingX - 2; // 2 = 左右边框各 1 列
    }
    
    /// <summary>
    /// 计算居中对齐时的左侧填充空格数
    /// </summary>
    public static int CalcCenterPadding(string text, int contentWidth)
    {
        int visLen = GetVisualWidth(text);
        int pad = Mathf.Max(0, contentWidth - visLen);
        return pad / 2;
    }
    
    /// <summary>
    /// 计算右对齐时的左侧填充空格数
    /// </summary>
    public static int CalcRightPadding(string text, int contentWidth)
    {
        int visLen = GetVisualWidth(text);
        return Mathf.Max(0, contentWidth - visLen);
    }
    
    // ─────────────────────────────────────────────────────────────
    //  单行格式化
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 格式化一行内容为带边框的 RichText 行
    /// <para>格式：│[padding][content][padding]│</para>
    /// </summary>
    public static string FormatBoxLine(string content, int totalWidth, TSSStyle style, BorderStyle? border = null)
    {
        var b = border ?? BorderStyle.Classic;
        string borderHex = ColorUtility.ToHtmlStringRGB(style.borderColor);
        string contentHex = ColorUtility.ToHtmlStringRGB(style.contentColor);

        // 处理艺术字空格扩展
        if (style.expandArtSpaces && !string.IsNullOrEmpty(content))
        {
            content = ExpandArtSpaces(content);
        }

        // 使用 BorderStyle 生成内容行（边框色与内容色分离）
        return b.GenerateContentLine(content, totalWidth, style.paddingX, style.alignment, contentHex, borderHex);
    }
    
    /// <summary>
    /// 生成顶栏：┌─[title]───────────┐（传统 ASCII 框风格）
    /// <para>公式：totalWidth = topLeft + h×n1 + titleLeft + title + titleRight + h×n2 + topRight</para>
    /// </summary>
    public static string GenerateTopBorder(string title, int totalWidth, TSSStyle style, BorderStyle? border = null)
    {
        var b = border ?? BorderStyle.Classic;
        return b.GenerateTop(title, totalWidth);
    }

    /// <summary>
    /// 生成底栏：└───────────────────┘（传统 ASCII 框风格）
    /// <para>公式：totalWidth = bottomLeft + h×n + bottomRight</para>
    /// </summary>
    public static string GenerateBottomBorder(int totalWidth, TSSStyle style, BorderStyle? border = null)
    {
        var b = border ?? BorderStyle.Classic;
        return b.GenerateBottom(totalWidth);
    }
    
    /// <summary>
    /// 生成空行（纯边框 + 空格填充）
    /// </summary>
    public static string GenerateEmptyLine(int totalWidth, TSSStyle style, BorderStyle? border = null)
    {
        return FormatBoxLine("", totalWidth, style, border);
    }
    
    // ─────────────────────────────────────────────────────────────
    //  组件生成（返回多行 RichText 数组）
    // ─────────────────────────────────────────────────────────────
    
    /// <summary>
    /// 生成一个完整的文本框组件
    /// <para>结构：[bleedY 空行] + 顶栏 + [paddingY 空行] + 内容行 + [paddingY 空行] + 底栏 + [bleedY 空行]</para>
    /// </summary>
    public static string[] GenerateTextBox(string[] contentLines, int totalWidth, TSSStyle style, BorderStyle? border = null)
    {
        var result = new List<string>();

        // 上方出血空行
        for (int i = 0; i < style.bleedY; i++)
        {
            result.Add(new string(' ', totalWidth));
        }

        // 顶栏
        result.Add(GenerateTopBorder("", totalWidth, style, border));

        // 上方页边距空行
        for (int i = 0; i < style.paddingY; i++)
        {
            result.Add(GenerateEmptyLine(totalWidth, style, border));
        }

        // 内容行
        if (contentLines != null)
        {
            foreach (var line in contentLines)
            {
                result.Add(FormatBoxLine(line, totalWidth, style, border));
            }
        }

        // 下方页边距空行
        for (int i = 0; i < style.paddingY; i++)
        {
            result.Add(GenerateEmptyLine(totalWidth, style, border));
        }

        // 底栏
        result.Add(GenerateBottomBorder(totalWidth, style, border));

        // 下方出血空行
        for (int i = 0; i < style.bleedY; i++)
        {
            result.Add(new string(' ', totalWidth));
        }

        return result.ToArray();
    }

    /// <summary>
    /// 生成带标题的文本框组件
    /// </summary>
    public static string[] GenerateTextBoxWithTitle(string[] contentLines, string title, int totalWidth, TSSStyle style, BorderStyle? border = null)
    {
        var result = new List<string>();

        // 上方出血空行
        for (int i = 0; i < style.bleedY; i++)
        {
            result.Add(new string(' ', totalWidth));
        }

        // 顶栏（带标题）
        result.Add(GenerateTopBorder(title, totalWidth, style, border));

        // 上方页边距空行
        for (int i = 0; i < style.paddingY; i++)
        {
            result.Add(GenerateEmptyLine(totalWidth, style, border));
        }

        // 内容行
        if (contentLines != null)
        {
            foreach (var line in contentLines)
            {
                result.Add(FormatBoxLine(line, totalWidth, style, border));
            }
        }

        // 下方页边距空行
        for (int i = 0; i < style.paddingY; i++)
        {
            result.Add(GenerateEmptyLine(totalWidth, style, border));
        }

        // 底栏
        result.Add(GenerateBottomBorder(totalWidth, style, border));

        // 下方出血空行
        for (int i = 0; i < style.bleedY; i++)
        {
            result.Add(new string(' ', totalWidth));
        }

        return result.ToArray();
    }
    
    /// <summary>
    /// 生成通知分隔线：├······················┤
    /// </summary>
    public static string GenerateDivider(int totalWidth, TSSStyle style, string text = null, BorderStyle? border = null)
    {
        var b = border ?? BorderStyle.Classic;
        return b.GenerateDivider(totalWidth, text);
    }
}
