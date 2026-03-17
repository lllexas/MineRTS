using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// TerminalBuffer - 终端缓冲区（逻辑层）
    /// ═══════════════════════════════════════════════════════════════
    ///
    /// 职责：
    /// 1. 管理所有历史行数据（List<string> _history）
    /// 2. 根据 MaxColumns 强制物理换行（CJK 1:2 网格对齐）
    /// 3. 提供视口裁切接口（GetVisibleLines）
    ///
    /// ═══════════════════════════════════════════════════════════════
    /// </summary>
    public class TerminalBuffer
    {
        // =========================================================
        //  数据
        // =========================================================
        private List<string> _history = new List<string>();
        private int _maxColumns = 80;
        private int _maxLines = 500;

        // =========================================================
        //  配置
        // =========================================================
        public int MaxColumns
        {
            get => _maxColumns;
            set
            {
                if (_maxColumns != value)
                {
                    _maxColumns = value;
                    RebuildAllLines();
                }
            }
        }

        public int MaxLines
        {
            get => _maxLines;
            set => _maxLines = value;
        }

        public int LineCount => _history.Count;

        // =========================================================
        //  视口裁切（核心）
        // =========================================================

        /// <summary>
        /// 获取可见行范围（用于渲染）
        /// </summary>
        public List<string> GetVisibleLines(int scrollOffset, int visibleRows)
        {
            if (_history.Count == 0) return new List<string>();

            int startLine = Mathf.Max(0, scrollOffset);
            int actualRows = Mathf.Min(visibleRows, _history.Count - startLine);

            if (actualRows <= 0) return new List<string>();

            return _history.GetRange(startLine, actualRows);
        }

        /// <summary>
        /// 获取最后一行（用于输入行上方显示）
        /// </summary>
        public string GetLastLine()
        {
            if (_history.Count == 0) return string.Empty;
            return _history[_history.Count - 1];
        }

        // =========================================================
        //  添加行
        // =========================================================

        /// <summary>
        /// 添加一行文本（自动根据 MaxColumns 换行）
        /// </summary>
        public void AddLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _history.Add(string.Empty);
                TrimLines();
                return;
            }

            // 先按换行符分割（支持 \r\n, \r, \n）
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // 对每一行分别进行自动换行处理
            foreach (var line in lines)
            {
                var wrappedLines = WrapLine(line, _maxColumns);
                _history.AddRange(wrappedLines);
            }
            
            TrimLines();
        }

        /// <summary>
        /// 清空所有行
        /// </summary>
        public void Clear()
        {
            _history.Clear();
        }

        // =========================================================
        //  内部方法
        // =========================================================

        private void TrimLines()
        {
            while (_history.Count > _maxLines)
            {
                _history.RemoveAt(0);
            }
        }

        private List<string> WrapLine(string text, int maxColumns)
        {
            var result = new List<string>();

            if (string.IsNullOrEmpty(text))
            {
                result.Add(string.Empty);
                return result;
            }

            // 移除富文本标签计算纯文本宽度
            string plainText = StripRichTextTags(text);

            int currentCol = 0;
            int lineStart = 0;
            int originalIndex = 0; // 原始文本（含标签）的索引

            for (int i = 0; i < plainText.Length; i++)
            {
                char c = plainText[i];
                int charWidth = (c <= 127) ? 1 : 2;

                if (currentCol + charWidth > maxColumns)
                {
                    // 找到原始文本中对应的位置
                    int originalEnd = FindOriginalIndex(text, originalIndex, i - lineStart);
                    string lineText = text.Substring(lineStart > 0 ? FindOriginalIndex(text, 0, lineStart) : 0, originalEnd - (lineStart > 0 ? FindOriginalIndex(text, 0, lineStart) : 0));
                    result.Add(lineText);
                    lineStart = i;
                    originalIndex = originalEnd;
                    currentCol = charWidth;
                }
                else
                {
                    currentCol += charWidth;
                }
            }

            if (lineStart < plainText.Length)
            {
                result.Add(text.Substring(lineStart > 0 ? FindOriginalIndex(text, 0, lineStart) : 0));
            }

            return result;
        }

        /// <summary>
        /// 移除富文本标签
        /// </summary>
        private static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var result = new System.Text.StringBuilder();
            bool inTag = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>' && inTag)
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// 找到纯文本索引对应的原始文本索引（跳过富文本标签）
        /// </summary>
        private static int FindOriginalIndex(string text, int startIndex, int plainTextLength)
        {
            int plainIndex = 0;
            bool inTag = false;

            for (int i = startIndex; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>' && inTag)
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    plainIndex++;
                    if (plainIndex >= plainTextLength)
                    {
                        return i + 1;
                    }
                }
            }

            return text.Length;
        }

        private void RebuildAllLines()
        {
            var allText = new List<string>(_history);
            _history.Clear();

            foreach (var text in allText)
            {
                var wrappedLines = WrapLine(text, _maxColumns);
                _history.AddRange(wrappedLines);
            }
        }
    }
}
