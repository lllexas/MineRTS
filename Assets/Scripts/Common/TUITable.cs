using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// TUITable - TUI 表格系统
/// ═══════════════════════════════════════════════════════════════
/// 像 HTML 表格一样声明式定义，支持边框、对齐、自动列宽喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>

/// <summary>
/// 表格列定义
/// </summary>
public class TUITableColumn
{
    /// <summary>列标题</summary>
    public string header;
    
    /// <summary>列宽度（0 = 自动填充剩余空间）</summary>
    public int width;
    
    /// <summary>对齐方式</summary>
    public TextAlignment alignment;
    
    /// <summary>是否与下一列用虚线连接</summary>
    public bool connectToNext;
    
    /// <summary>连接字符（如 ─ 或 ·）</summary>
    public char connectorChar = '─';
    
    public TUITableColumn(string header, int width = 0, TextAlignment alignment = TextAlignment.Left)
    {
        this.header = header;
        this.width = width;
        this.alignment = alignment;
    }
}

/// <summary>
/// 表格数据行
/// </summary>
public class TUITableRow
{
    /// <summary>单元格数据</summary>
    public List<string> cells = new List<string>();
    
    public TUITableRow(params string[] cells)
    {
        this.cells.AddRange(cells);
    }
}

/// <summary>
/// 表格构建器（流式 API）
/// </summary>
public class TUITableBuilder
{
    private List<TUITableColumn> _columns = new List<TUITableColumn>();
    private List<TUITableRow> _rows = new List<TUITableRow>();
    private string _title = "";
    private string _footer = "";
    private TSSStyle _style = TSSStyle.Default;
    private BorderStyle _border = BorderStyle.Classic;
    private bool _useBorder = true;
    private int _connectorColumnIndex = -1; // 哪一列后面加连接器
    
    /// <summary>
    /// 设置表格标题
    /// </summary>
    public TUITableBuilder SetTitle(string title)
    {
        _title = title;
        return this;
    }
    
    /// <summary>
    /// 设置表格页脚
    /// </summary>
    public TUITableBuilder SetFooter(string footer)
    {
        _footer = footer;
        return this;
    }
    
    /// <summary>
    /// 设置样式
    /// </summary>
    public TUITableBuilder SetStyle(TSSStyle style)
    {
        _style = style;
        return this;
    }
    
    /// <summary>
    /// 设置边框样式
    /// </summary>
    public TUITableBuilder SetBorderStyle(BorderStyle border)
    {
        _border = border;
        return this;
    }
    
    /// <summary>
    /// 是否使用边框
    /// </summary>
    public TUITableBuilder UseBorder(bool use)
    {
        _useBorder = use;
        return this;
    }
    
    /// <summary>
    /// 添加列
    /// </summary>
    public TUITableBuilder AddColumn(string header, int width = 0, TextAlignment alignment = TextAlignment.Left)
    {
        _columns.Add(new TUITableColumn(header, width, alignment));
        return this;
    }
    
    /// <summary>
    /// 添加列（带连接器）
    /// </summary>
    public TUITableBuilder AddColumn(string header, int width = 0, TextAlignment alignment = TextAlignment.Left, bool connectToNext = false, char connectorChar = '─')
    {
        var col = new TUITableColumn(header, width, alignment);
        col.connectToNext = connectToNext;
        col.connectorChar = connectorChar;
        _columns.Add(col);
        return this;
    }
    
    /// <summary>
    /// 设置哪一列后面加连接器（用于名称和序号之间的虚线）
    /// </summary>
    public TUITableBuilder SetConnectorColumn(int columnIndex, char connectorChar = '─')
    {
        _connectorColumnIndex = columnIndex;
        if (_connectorColumnIndex >= 0 && _connectorColumnIndex < _columns.Count)
        {
            _columns[_connectorColumnIndex].connectToNext = true;
            _columns[_connectorColumnIndex].connectorChar = connectorChar;
        }
        return this;
    }
    
    /// <summary>
    /// 添加数据行
    /// </summary>
    public TUITableBuilder AddRow(params string[] cells)
    {
        _rows.Add(new TUITableRow(cells));
        return this;
    }
    
    /// <summary>
    /// 渲染表格，返回 RichText 字符串数组
    /// </summary>
    public string[] Render(int totalWidth)
    {
        var result = new List<string>();

        if (_columns.Count == 0) return result.ToArray();

        // 总宽强制偶数（制表符横线 ─ 是 2 宽，奇数总宽无法精确填充）
        if (totalWidth % 2 != 0) totalWidth--;

        // 1. 计算列宽
        int[] colWidths = CalculateColumnWidths(totalWidth);
        
        // 2. 生成内容
        string borderHex = ColorUtility.ToHtmlStringRGB(_style.borderColor);
        string contentHex = ColorUtility.ToHtmlStringRGB(_style.contentColor);
        string headerHex = ColorUtility.ToHtmlStringRGB(_style.titleColor);
        
        if (_useBorder)
        {
            // 顶栏（带标题）
            result.Add(_border.GenerateTop(_title, totalWidth));
            
            // 表头行
            result.Add(RenderHeaderRow(colWidths, totalWidth, _border, headerHex, borderHex));
            
            // 分隔线
            result.Add(RenderDividerLine(totalWidth, _border, borderHex));
        }
        
        // 数据行
        foreach (var row in _rows)
        {
            result.Add(RenderDataRow(row, colWidths, totalWidth, _border, contentHex, borderHex));
        }
        
        if (_useBorder)
        {
            // 页脚（如果有）
            if (!string.IsNullOrEmpty(_footer))
            {
                result.Add(_border.GenerateEmptyLine(totalWidth, _style.paddingX));
                result.Add(_border.GenerateContentLine(_footer, totalWidth, _style.paddingX, TextAlignment.Left, contentHex, borderHex));
            }

            // 底栏
            result.Add(_border.GenerateBottom(totalWidth));
        }
        else if (!string.IsNullOrEmpty(_footer))
        {
            result.Add(_footer);
        }
        
        return result.ToArray();
    }
    
    /// <summary>
    /// 计算列宽
    /// </summary>
    private int[] CalculateColumnWidths(int totalWidth)
    {
        int n = _columns.Count;
        int borderW = TUITool.GetCharVisualWidth(_border.vertical);
        // sum(colWidths) = totalWidth - 2×borderWidth - 2×paddingX - (n-1)×spacingX
        int availableForCols = totalWidth
            - 2 * borderW
            - 2 * _style.paddingX
            - (n - 1) * _style.spacingX;

        int[] widths = new int[n];

        // 1. 固定列取指定宽，自动列先取表头最小宽
        int[] autoIndices = new int[n];
        int autoCount = 0;
        int baseTotal = 0;

        for (int i = 0; i < n; i++)
        {
            int headerVis = TUITool.GetVisualWidth(_columns[i].header);
            if (_columns[i].width > 0)
                widths[i] = Mathf.Max(_columns[i].width, headerVis);
            else
            {
                widths[i] = headerVis;
                autoIndices[autoCount++] = i;
            }
            baseTotal += widths[i];
        }

        // 2. 剩余空间严格分配给自动列，余数逐一补到前几列
        int remaining = Mathf.Max(0, availableForCols - baseTotal);
        if (autoCount > 0)
        {
            int extra = remaining / autoCount;
            int rem   = remaining % autoCount;
            for (int k = 0; k < autoCount; k++)
                widths[autoIndices[k]] += extra + (k < rem ? 1 : 0);
        }

        return widths;
    }
    
    /// <summary>
    /// 渲染表头行
    /// </summary>
    private string RenderHeaderRow(int[] colWidths, int totalWidth, BorderStyle border, string colorHex, string borderHex)
    {
        var sb = new StringBuilder();
        sb.Append($"<color=#{borderHex}>{border.vertical}</color>");

        string leftPadding = new string(' ', _style.paddingX);
        string rightPadding = new string(' ', _style.paddingX);

        sb.Append(leftPadding);

        for (int i = 0; i < _columns.Count; i++)
        {
            string cell = _columns[i].header;
            int width = colWidths[i];

            string content = AlignText(cell, width, _columns[i].alignment);
            sb.Append(content);

            // 列间距
            if (i < _columns.Count - 1)
            {
                sb.Append(new string(' ', _style.spacingX));
            }
        }

        sb.Append(rightPadding);
        sb.Append($"<color=#{borderHex}>{border.vertical}</color>");

        return $"<color=#{colorHex}>{sb.ToString()}</color>";
    }
    
    /// <summary>
    /// 渲染分隔线
    /// </summary>
    private string RenderDividerLine(int totalWidth, BorderStyle border, string colorHex)
    {
        // 使用横线字符作为分隔线填充
        return $"<color=#{colorHex}>{border.GenerateDivider(totalWidth, null, border.horizontal)}</color>";
    }
    
    /// <summary>
    /// 渲染数据行
    /// </summary>
    private string RenderDataRow(TUITableRow row, int[] colWidths, int totalWidth, BorderStyle border, string contentHex, string borderHex)
    {
        var sb = new StringBuilder();
        sb.Append($"<color=#{borderHex}>{border.vertical}</color>");
        Debug.Log($"totalWidth={totalWidth}, sum_cols={colWidths.Sum()}");
        string leftPadding = new string(' ', _style.paddingX);
        string rightPadding = new string(' ', _style.paddingX);

        sb.Append(leftPadding);

        for (int i = 0; i < _columns.Count; i++)
        {
            string cell = i < row.cells.Count ? row.cells[i] : "";
            int width = colWidths[i];

            // 检查是否是连接器列（名称列后面跟序号列）
            if (i == _connectorColumnIndex && i + 1 < _columns.Count)
            {
                // 名称列 + 序号列合并处理：内容 + 连接器 + 序号
                string nextCell = i + 1 < row.cells.Count ? row.cells[i + 1] : "";
                int nextWidth = colWidths[i + 1];

                // 连接器长度 = 名称列宽 + spacingX + 序号列宽 - 名称内容宽 - spacingX(名称后间距) - 序号内容宽
                int cellVisWidth = TUITool.GetVisualWidth(cell);
                int nextVisWidth  = TUITool.GetVisualWidth(nextCell);
                int connectorVisWidth = Mathf.Max(2, width + _style.spacingX + nextWidth - cellVisWidth - _style.spacingX - nextVisWidth);

                // 计算连接器字符数（考虑字符可能是 2 宽），余数用空格补齐保证视觉宽度精确
                int connectorCharWidth  = TUITool.GetCharVisualWidth(_columns[i].connectorChar);
                int connectorCharCount  = connectorVisWidth / connectorCharWidth;
                int connectorRemainder  = connectorVisWidth - connectorCharCount * connectorCharWidth;
                string connector = new string(_columns[i].connectorChar, connectorCharCount) + new string(' ', connectorRemainder);

                sb.Append(cell);
                sb.Append(new string(' ', _style.spacingX)); // 名称后间距
                sb.Append($"<color=#{borderHex}>{connector}</color>");
                sb.Append(nextCell); // 序号直接贴在连接器后面

                // 跳过下一列（已经处理了）
                i++;
                // 跳过列间空格（因为序号列已经处理完了）
                continue;
            }
            else
            {
                string content = AlignText(cell, width, _columns[i].alignment);
                sb.Append(content);
            }

            // 列间距
            if (i < _columns.Count - 1)
            {
                sb.Append(new string(' ', _style.spacingX));
            }
        }

        sb.Append(rightPadding);
        sb.Append($"<color=#{borderHex}>{border.vertical}</color>");

        return $"<color=#{contentHex}>{sb.ToString()}</color>";
    }
    
    /// <summary>
    /// 对齐文本
    /// </summary>
    private string AlignText(string text, int width, TextAlignment alignment)
    {
        int visLen = TUITool.GetVisualWidth(text);
        int pad = Mathf.Max(0, width - visLen);
        
        switch (alignment)
        {
            case TextAlignment.Left:
                return text + new string(' ', pad);
            case TextAlignment.Right:
                return new string(' ', pad) + text;
            case TextAlignment.Center:
            default:
                int left = pad / 2;
                int right = pad - left;
                return new string(' ', left) + text + new string(' ', right);
        }
    }
}

/// <summary>
/// TUITool 表格扩展方法
/// </summary>
public static class TUITableExtensions
{
    /// <summary>
    /// 快速创建表格
    /// </summary>
    public static TUITableBuilder CreateTable()
    {
        return new TUITableBuilder();
    }
}
