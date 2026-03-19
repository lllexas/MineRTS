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
            
            width += IsWideChar(c) ? 2 : 1;
        }
        
        return width;
    }
    
    /// <summary>
    /// 判断字符是否为双宽字符（CJK / BoxDrawing / 全角等）
    /// </summary>
    public static bool IsWideChar(char c)
    {
        return (c >= 0x1100 && c <= 0x115F)  // Hangul Jamo
            || (c >= 0x2500 && c <= 0x257F)  // Box Drawing（制表符）
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
    public static string FormatBoxLine(string content, int totalWidth, TSSStyle style)
    {
        string borderHex = ColorUtility.ToHtmlStringRGB(style.borderColor);
        string contentHex = ColorUtility.ToHtmlStringRGB(style.contentColor);
        
        // 处理艺术字空格扩展
        if (style.expandArtSpaces && !string.IsNullOrEmpty(content))
        {
            content = ExpandArtSpaces(content);
        }
        
        // 计算内容区宽度
        int contentWidth = CalcContentWidth(totalWidth, style);
        
        // 根据对齐方式计算 padding
        string leftPad = "";
        string rightPad = "";
        
        if (!string.IsNullOrEmpty(content))
        {
            int padLen = style.alignment switch
            {
                TextAlignment.Left => 0,
                TextAlignment.Center => CalcCenterPadding(content, contentWidth),
                TextAlignment.Right => CalcRightPadding(content, contentWidth),
                _ => 0
            };
            leftPad = new string(' ', padLen);
            rightPad = new string(' ', Mathf.Max(0, contentWidth - GetVisualWidth(content) - padLen));
        }
        else
        {
            // 空行：全填充空格
            leftPad = new string(' ', contentWidth);
        }
        
        // 构建 RichText
        return $"<color=#{borderHex}>│</color> <color=#{contentHex}>{leftPad}{content}{rightPad}</color> <color=#{borderHex}>│</color>";
    }
    
    /// <summary>
    /// 生成顶栏：┌─[title]───────────┐
    /// </summary>
    public static string GenerateTopBorder(string title, int totalWidth, TSSStyle style)
    {
        string borderHex = ColorUtility.ToHtmlStringRGB(style.borderColor);
        string titleHex = ColorUtility.ToHtmlStringRGB(style.contentColor);
        
        // 计算 title 占据的视觉宽度
        int titleVisWidth = GetVisualWidth(title);
        
        // ┌─[title] 的视觉宽度 = 2(┌─) + titleVisWidth + 2([ 和 ])
        int fixedPartWidth = 4 + titleVisWidth + 2;
        int fillLen = Mathf.Max(0, totalWidth - fixedPartWidth - 1); // 1 = ┐
        
        string fill = new string(BOX_HORIZONTAL, fillLen);
        
        return $"<color=#{borderHex}>┌─[</color><color=#{titleHex}>{title}</color><color=#{borderHex}>]{fill}┐</color>";
    }
    
    /// <summary>
    /// 生成底栏：└───────────────────┘
    /// </summary>
    public static string GenerateBottomBorder(int totalWidth, TSSStyle style)
    {
        string borderHex = ColorUtility.ToHtmlStringRGB(style.borderColor);
        int fillLen = Mathf.Max(0, totalWidth - 2); // 2 = └┘
        string fill = new string(BOX_HORIZONTAL, fillLen);
        return $"<color=#{borderHex}>└{fill}┘</color>";
    }
    
    /// <summary>
    /// 生成空行（纯边框 + 空格填充）
    /// </summary>
    public static string GenerateEmptyLine(int totalWidth, TSSStyle style)
    {
        return FormatBoxLine("", totalWidth, style);
    }
    
    // ─────────────────────────────────────────────────────────────
    //  组件生成（返回多行 RichText 数组）
    // ─────────────────────────────────────────────────────────────
    
    /// <summary>
    /// 生成一个完整的文本框组件
    /// <para>结构：[bleedY 空行] + 顶栏 + [paddingY 空行] + 内容行 + [paddingY 空行] + 底栏 + [bleedY 空行]</para>
    /// </summary>
    public static string[] GenerateTextBox(string[] contentLines, int totalWidth, TSSStyle style)
    {
        var result = new List<string>();
        
        // 上方出血空行
        for (int i = 0; i < style.bleedY; i++)
        {
            result.Add(new string(' ', totalWidth));
        }
        
        // 顶栏
        result.Add(GenerateTopBorder("", totalWidth, style));
        
        // 上方页边距空行
        for (int i = 0; i < style.paddingY; i++)
        {
            result.Add(GenerateEmptyLine(totalWidth, style));
        }
        
        // 内容行
        if (contentLines != null)
        {
            foreach (var line in contentLines)
            {
                result.Add(FormatBoxLine(line, totalWidth, style));
            }
        }
        
        // 下方页边距空行
        for (int i = 0; i < style.paddingY; i++)
        {
            result.Add(GenerateEmptyLine(totalWidth, style));
        }
        
        // 底栏
        result.Add(GenerateBottomBorder(totalWidth, style));
        
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
    public static string[] GenerateTextBoxWithTitle(string[] contentLines, string title, int totalWidth, TSSStyle style)
    {
        var result = new List<string>();
        
        // 上方出血空行
        for (int i = 0; i < style.bleedY; i++)
        {
            result.Add(new string(' ', totalWidth));
        }
        
        // 顶栏（带标题）
        result.Add(GenerateTopBorder(title, totalWidth, style));
        
        // 上方页边距空行
        for (int i = 0; i < style.paddingY; i++)
        {
            result.Add(GenerateEmptyLine(totalWidth, style));
        }
        
        // 内容行
        if (contentLines != null)
        {
            foreach (var line in contentLines)
            {
                result.Add(FormatBoxLine(line, totalWidth, style));
            }
        }
        
        // 下方页边距空行
        for (int i = 0; i < style.paddingY; i++)
        {
            result.Add(GenerateEmptyLine(totalWidth, style));
        }
        
        // 底栏
        result.Add(GenerateBottomBorder(totalWidth, style));
        
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
    public static string GenerateDivider(int totalWidth, TSSStyle style, char dividerChar = '·', string text = null)
    {
        string borderHex = ColorUtility.ToHtmlStringRGB(style.borderColor);
        string textHex = text != null ? ColorUtility.ToHtmlStringRGB(style.contentColor) : borderHex;
        
        if (string.IsNullOrEmpty(text))
        {
            int fillLen = Mathf.Max(0, totalWidth - 2); // 2 = ├┤
            string fill = new string(dividerChar, fillLen);
            return $"<color=#{borderHex}>├{fill}┤</color>";
        }
        else
        {
            // 带文本的通知：├···【3 条新消息】···┤
            int contentWidth = CalcContentWidth(totalWidth, style);
            int textVisWidth = GetVisualWidth(text);
            int fillEachSide = Mathf.Max(0, (contentWidth - textVisWidth) / 2);
            
            string fill = new string(dividerChar, fillEachSide);
            return $"<color=#{borderHex}>├</color><color=#{borderHex}>{fill}</color><color=#{textHex}>{text}</color><color=#{borderHex}>{fill}</color><color=#{borderHex}>┤</color>";
        }
    }
}
