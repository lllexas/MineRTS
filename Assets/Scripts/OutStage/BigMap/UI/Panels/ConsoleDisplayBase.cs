using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// ConsoleDisplayBase - 控制台显示基类（纯显示，无输入）
    /// ═══════════════════════════════════════════════════════════════
    ///
    /// 设计理念：
    /// 1. 纯显示层 - 只负责文本渲染，不处理输入
    /// 2. TerminalBuffer 集成 - 自动换行、视口裁切
    /// 3. 滚动控制 - 支持鼠标滚轮和滚动条
    /// 4. 可作为父类被 ConsolePanelBase 继承
    ///
    /// 继承关系：
    ///   SpaceUIAnimator
    ///   ↑
    ///   ConsoleDisplayBase（本类）
    ///   ↑
    ///   ConsolePanelBase（添加输入功能）
    ///
    /// ═══════════════════════════════════════════════════════════════
    /// </summary>
    public abstract class ConsoleDisplayBase<T> : SpaceUIAnimator where T : TUIManager
    {
        // =========================================================
        //  TUI 管理层（由子类实现）
        // =========================================================

        /// <summary>TUI 管理层实例（由子类实现）</summary>
        protected abstract T ConsoleLogic { get; }


        // =========================================================
        //  UI 组件引用（显示层）
        // =========================================================
        [Header("Console Display Components")]
        [Tooltip("历史文本（TextMeshProUGUI，唯一历史输出口）")]
        [SerializeField] protected TextMeshProUGUI historyText;

        [Tooltip("滚动条（Scrollbar，用于可视化和拖拽滚动）")]
        [SerializeField] protected Scrollbar scrollbar;

        [Header("Display Settings")]
        [Tooltip("最大日志行数")]
        [SerializeField] protected int maxLogLines = 500;

        [Tooltip("是否自动滚动到底部")]
        [SerializeField] protected bool autoScrollToBottom = true;

        [Tooltip("滚轮滚动速度")]
        [SerializeField] protected float scrollSpeed = 3f;

        [Tooltip("每行最大列数（视觉宽度单位，0=自动计算）")]
        [SerializeField] protected int maxColumns = 0;

        [Tooltip("行高（像素，用于计算）")]
        [SerializeField] protected float lineHeight = 20f;

        // =========================================================
        //  核心组件（逻辑层）
        // =========================================================
        protected TerminalBuffer _buffer;

        // =========================================================
        //  滚动状态
        // =========================================================
        protected int _scrollLineIndex = 0; // 当前滚动行索引
        protected int _visibleRows = 25; // 可见行数
        protected bool _isDirty = false; // 是否需要刷新显示

        // =========================================================
        //  尺寸变化防抖
        // =========================================================
        private Coroutine _dimensionsDebounce;

        // =========================================================
        //  公开接口
        // =========================================================
        public int LineCount => _buffer?.LineCount ?? 0;
        public int ScrollLineIndex => _scrollLineIndex;
        public bool IsDirty => _isDirty;

        protected virtual int GetReservedBottomRows()
        {
            return 0;
        }

        protected virtual int GetHistoryViewportRows()
        {
            int visibleRows = Mathf.Max(1, _visibleRows);
            return Mathf.Max(0, visibleRows - Mathf.Max(0, GetReservedBottomRows()));
        }

        protected virtual int GetScrollableHistoryRows()
        {
            return Mathf.Max(1, GetHistoryViewportRows());
        }

        // =========================================================
        //  Unity 生命周期
        // =========================================================

        protected override void Awake()
        {
            base.Awake();
            InitializeDisplay();
        }

        protected virtual void OnDisable()
        {
            // 注销滚动条事件
            if (scrollbar != null)
            {
                scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
            }
            // 反订阅清屏事件
            if (ConsoleLogic != null)
                ConsoleLogic.OnClearRequested -= ClearLog;
        }

        /// <summary>
        /// historyText 的 RectTransform 随面板缩放而变化时触发（Layout 重排、窗口调整等）。
        /// 加防抖避免 Layout 抖动帧中重复计算。
        /// </summary>
        protected virtual void OnRectTransformDimensionsChange()
        {
            // _buffer 为 null 说明 Awake 还没走完，忽略
            if (_buffer == null) return;

            // 立即清空旧内容——buffer 为空时 historyText.text 自动变成空字符串，
            // 无需手动禁用/启用 historyText，也不会留下错误宽度的残影。
            ClearLog();

            if (_dimensionsDebounce != null) StopCoroutine(_dimensionsDebounce);
            _dimensionsDebounce = StartCoroutine(DelayedDimensionsUpdate());
        }

        private IEnumerator DelayedDimensionsUpdate()
        {
            yield return new WaitForSeconds(0.1f);
            _dimensionsDebounce = null;

            int newColumns = CalculateMaxColumns();
            RecalculateVisibleRows();
            _buffer.MaxColumns = newColumns;

            if (ConsoleLogic == null) yield break;

            // 先注入高度，再注入宽度。
            // 两个 setter 内部都有"值未变则不触发"的守卫，不会重复 Render。
            // 若两者都变，宽度注入会打断高度注入启动的 Coroutine，净效果仍是一次 Render。
            ConsoleLogic.ConsoleHeight = _visibleRows;
            ConsoleLogic.ConsoleWidth  = newColumns;
        }

        protected virtual void Start()
        {
            // Layout 已完成，重新计算行数和列数
            RecalculateVisibleRows();
            if (_buffer != null)
            {
                _buffer.MaxColumns = CalculateMaxColumns();
            }

            // 统一在 ConsoleDisplayBase 层注入 Clear/Width 钩子
            // 注意：必须先订阅 OnClearRequested，再设置 ConsoleWidth。
            // 因为设置宽度可能立即触发 OnConsoleWidthChanged → Render() → InvokeClearRequested()，
            // 若此时 ClearLog 还未订阅，清屏无效，内容会叠加。
            if (ConsoleLogic != null)
            {
                ConsoleLogic.OnClearRequested += ClearLog;           // ← 先订阅
                ConsoleLogic.ConsoleHeight = _visibleRows;           // ← 注入行高
                if (_buffer != null && _buffer.MaxColumns > 0)
                    ConsoleLogic.ConsoleWidth = _buffer.MaxColumns;  // ← 后注入（可能立即触发 Render）
            }
        }

        protected new virtual void Update()
        {
            base.Update();

            // 鼠标滚轮监听（当面板显示且鼠标在面板上时）
            if (_canvasGroup != null && _canvasGroup.alpha > 0.5f)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (scroll != 0)
                {
                    ScrollByDelta(-Mathf.RoundToInt(scroll * scrollSpeed));
                }
            }

            // 历史文本更新（仅当需要时）
            if (_isDirty)
            {
                RefreshHistoryDisplay();
                _isDirty = false;
            }
        }

        // =========================================================
        //  初始化
        // =========================================================

        /// <summary>
        /// 初始化显示层
        /// </summary>
        protected virtual void InitializeDisplay()
        {
            // 创建缓冲区
            _buffer = new TerminalBuffer();
            _buffer.MaxLines = maxLogLines;

            // 计算 MaxColumns
            if (maxColumns <= 0)
            {
                maxColumns = CalculateMaxColumns();
            }
            _buffer.MaxColumns = maxColumns;

            // 计算可见行数（延迟到 Start 中重新计算，确保 Layout 完成）
            _visibleRows = 25; // 默认值

            // 监听滚动条
            if (scrollbar != null)
            {
                scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
                scrollbar.direction = Scrollbar.Direction.TopToBottom;
            }

            // 初始化历史文本
            if (historyText != null)
            {
                historyText.richText = true;
                historyText.enableWordWrapping = false; // TUI 行宽由 TerminalBuffer 自行控制，TMP 不应折行
                historyText.alignment = TextAlignmentOptions.TopLeft;
                historyText.overflowMode = TextOverflowModes.Overflow; // 超宽由父级 Mask 裁切，不截断后续行
            }
        }

        // =========================================================
        //  输出协议（核心）
        // =========================================================

        /// <summary>
        /// 输出一行文本（带颜色）
        /// </summary>
        public virtual void OutputLine(string message, Color color)
        {
            if (_buffer == null) return;

            string colorHex = ColorUtility.ToHtmlStringRGB(color);
            string formatted = $"<color=#{colorHex}>{message}</color>";
            AppendLine(formatted);
        }

        /// <summary>
        /// 输出原始文本（带颜色）
        /// </summary>
        public virtual void Output(string message, Color color)
        {
            OutputLine(message, color);
        }

        /// <summary>
        /// 输出纯文本（默认白色）
        /// </summary>
        public virtual void OutputLine(string message)
        {
            OutputLine(message, Color.white);
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        public virtual void ClearLog()
        {
            _buffer?.Clear();
            _scrollLineIndex = 0;
            InvalidateDisplay();
        }

        /// <summary>
        /// 获取指定行内容，越界返回空字符串
        /// </summary>
        public virtual string GetLine(int index)
        {
            return _buffer?.GetLine(index) ?? string.Empty;
        }

        /// <summary>
        /// 追加一行原始文本
        /// </summary>
        public virtual void AppendLine(string message)
        {
            if (_buffer == null) return;

            _buffer.AddLine(message);
            InvalidateDisplay(preserveScrollWhenNotPinned: true);
        }

        /// <summary>
        /// 追加多行原始文本
        /// </summary>
        public virtual void AppendLines(IEnumerable<string> lines)
        {
            if (_buffer == null || lines == null) return;

            _buffer.AppendLines(lines);
            InvalidateDisplay(preserveScrollWhenNotPinned: true);
        }

        /// <summary>
        /// 追加多行内容（AppendLines 的语义别名）
        /// </summary>
        public virtual void AppendRange(IEnumerable<string> lines)
        {
            AppendLines(lines);
        }

        /// <summary>
        /// 覆盖指定行内容
        /// </summary>
        public virtual void SetLine(int index, string message)
        {
            if (_buffer == null) return;

            _buffer.SetLine(index, message);
            InvalidateDisplay();
        }

        /// <summary>
        /// 插入一行内容
        /// </summary>
        public virtual void InsertLine(int index, string message)
        {
            if (_buffer == null) return;

            _buffer.InsertLine(index, message);
            InvalidateDisplay();
        }

        /// <summary>
        /// 在指定位置插入多行内容
        /// </summary>
        public virtual void InsertRange(int index, IEnumerable<string> lines)
        {
            if (_buffer == null || lines == null) return;

            _buffer.InsertRange(index, lines);
            InvalidateDisplay();
        }

        /// <summary>
        /// 删除指定行
        /// </summary>
        public virtual void RemoveLine(int index)
        {
            if (_buffer == null) return;

            _buffer.RemoveLine(index);
            ClampScrollAndInvalidate();
        }

        /// <summary>
        /// 删除连续行
        /// </summary>
        public virtual void RemoveRange(int index, int count)
        {
            if (_buffer == null) return;

            _buffer.RemoveRange(index, count);
            ClampScrollAndInvalidate();
        }

        /// <summary>
        /// 清空连续行内容但保留行结构
        /// </summary>
        public virtual void ClearRange(int index, int count)
        {
            if (_buffer == null) return;

            _buffer.ClearRange(index, count);
            InvalidateDisplay();
        }

        /// <summary>
        /// 用新的内容替换一段连续行
        /// </summary>
        public virtual void ReplaceRange(int index, int count, IEnumerable<string> lines)
        {
            if (_buffer == null) return;

            _buffer.ReplaceRange(index, count, lines);
            ClampScrollAndInvalidate();
        }

        // =========================================================
        //  滚动控制（公开接口）
        // =========================================================

        /// <summary>
        /// 滚动到指定行
        /// </summary>
        public virtual void ScrollToLine(int lineIndex)
        {
            if (_buffer == null) return;

            int maxScrollIndex = Mathf.Max(0, _buffer.LineCount - GetScrollableHistoryRows());
            _scrollLineIndex = Mathf.Clamp(lineIndex, 0, maxScrollIndex);
            _isDirty = true;
            UpdateScrollbar();
        }

        /// <summary>
        /// 相对滚动（正数向下，负数向上）
        /// </summary>
        public virtual void ScrollByDelta(int delta)
        {
            ScrollToLine(_scrollLineIndex + delta);
        }

        /// <summary>
        /// 滚动到底部
        /// </summary>
        public virtual void ScrollToBottom()
        {
            if (_buffer == null) return;

            int visibleRows = GetScrollableHistoryRows();
            _scrollLineIndex = Mathf.Max(0, _buffer.LineCount - visibleRows);
            UpdateScrollbar();
        }

        /// <summary>
        /// 滚动到顶部
        /// </summary>
        public virtual void ScrollToTop()
        {
            _scrollLineIndex = 0;
            _isDirty = true;
            UpdateScrollbar();
        }

        // =========================================================
        //  渲染刷新（核心）
        // =========================================================

        /// <summary>
        /// 刷新历史显示（视口裁切）
        /// </summary>
        protected virtual void RefreshHistoryDisplay()
        {
            if (historyText == null || _buffer == null) return;

            // 确保 _visibleRows 有效
            if (_visibleRows <= 0)
            {
                _visibleRows = RecalculateVisibleRows();
            }

            int visibleRows = GetHistoryViewportRows();
            int maxScrollIndex = Mathf.Max(0, _buffer.LineCount - GetScrollableHistoryRows());
            _scrollLineIndex = Mathf.Clamp(_scrollLineIndex, 0, maxScrollIndex);

            // 裁切可见行
            var visibleLines = visibleRows > 0
                ? _buffer.GetVisibleLines(_scrollLineIndex, visibleRows)
                : new List<string>();

            // 处理颜色标签闭合
            var closedLines = new List<string>();
            foreach (var line in visibleLines)
            {
                closedLines.Add(CloseColorTags(line));
            }

            historyText.text = string.Join("\n", closedLines);
        }

        /// <summary>
        /// 确保颜色标签正确闭合
        /// </summary>
        protected virtual string CloseColorTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            int openTags = CountSubstring(text, "<color=");
            int closeTags = CountSubstring(text, "</color>");

            if (openTags > closeTags)
            {
                for (int i = 0; i < openTags - closeTags; i++)
                {
                    text += "</color>";
                }
            }

            return text;
        }

        /// <summary>
        /// 统计子字符串出现次数
        /// </summary>
        protected static int CountSubstring(string text, string substring)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(substring)) return 0;
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(substring, index, System.StringComparison.Ordinal)) != -1)
            {
                count++;
                index += substring.Length;
            }
            return count;
        }

        // =========================================================
        //  滚动条控制
        // =========================================================

        /// <summary>
        /// 更新滚动条位置
        /// </summary>
        protected virtual void UpdateScrollbar()
        {
            if (scrollbar == null || _buffer == null) return;

            int totalLines = _buffer.LineCount;
            int visibleRows = GetScrollableHistoryRows();

            // 内容还没窗口多，不需要滚动条
            if (totalLines <= visibleRows)
            {
                scrollbar.size = 1f;
                scrollbar.value = 0;
                return;
            }

            int maxScrollIndex = totalLines - visibleRows;
            scrollbar.size = Mathf.Clamp01((float)visibleRows / totalLines);
            scrollbar.value = Mathf.Clamp01((float)_scrollLineIndex / maxScrollIndex);
        }

        /// <summary>
        /// 滚动条事件处理
        /// </summary>
        protected virtual void OnScrollbarChanged(float value)
        {
            if (_buffer == null) return;

            int maxScrollIndex = Mathf.Max(0, _buffer.LineCount - GetScrollableHistoryRows());
            int newScrollIndex = Mathf.RoundToInt(value * maxScrollIndex);
            newScrollIndex = Mathf.Clamp(newScrollIndex, 0, maxScrollIndex);

            if (newScrollIndex != _scrollLineIndex)
            {
                _scrollLineIndex = newScrollIndex;
                _isDirty = true;
            }
        }

        // =========================================================
        //  工具方法
        // =========================================================

        /// <summary>
        /// 计算每行最大列数（以 ASCII 半宽字符为单位）
        /// </summary>
        protected virtual int CalculateMaxColumns()
        {
            if (historyText == null || historyText.font == null) return 80;

            float viewportWidth = historyText.rectTransform.rect.width;
            float charWidth = GetCharacterWidth('M', historyText);

            if (charWidth <= 0) return 80;

            return Mathf.FloorToInt(viewportWidth / charWidth);
        }

        /// <summary>
        /// 重新计算可见行数
        /// </summary>
        protected virtual int RecalculateVisibleRows()
        {
            if (historyText == null) return 25;

            float viewportHeight = historyText.rectTransform.rect.height;
            float fontSize = historyText.fontSize;

            if (fontSize <= 0) return 25;

            _visibleRows = Mathf.FloorToInt(viewportHeight / (fontSize * 1.2f));
            return Mathf.Max(1, _visibleRows);
        }

        /// <summary>
        /// 从 TMP 字体表取字符的实际推进宽度
        /// </summary>
        protected virtual float GetCharacterWidth(char c, TMP_Text tmpText)
        {
            if (tmpText?.font?.characterLookupTable == null) return 10f;

            uint charCode = c;
            if (!tmpText.font.characterLookupTable.TryGetValue(charCode, out TMP_Character character))
            {
                // 尝试用 'M' 作为回退
                if (!tmpText.font.characterLookupTable.TryGetValue((uint)'M', out character))
                {
                    return 10f;
                }
            }

            float pointSize = tmpText.fontSize;
            float fontScale = pointSize / tmpText.font.faceInfo.pointSize;
            return character.glyph.metrics.horizontalAdvance * fontScale;
        }

        /// <summary>
        /// 标记需要刷新显示
        /// </summary>
        public virtual void MarkDirty()
        {
            _isDirty = true;
        }

        protected virtual void InvalidateDisplay(bool preserveScrollWhenNotPinned = false)
        {
            _isDirty = true;

            if (autoScrollToBottom)
            {
                ScrollToBottom();
                return;
            }

            if (!preserveScrollWhenNotPinned)
            {
                ClampScrollAndInvalidate();
                return;
            }

            UpdateScrollbar();
        }

        protected virtual void ClampScrollAndInvalidate()
        {
            if (_buffer == null)
            {
                _isDirty = true;
                return;
            }

            int visibleRows = GetScrollableHistoryRows();
            int maxScrollIndex = Mathf.Max(0, _buffer.LineCount - visibleRows);
            _scrollLineIndex = Mathf.Clamp(_scrollLineIndex, 0, maxScrollIndex);
            _isDirty = true;
            UpdateScrollbar();
        }
    }
}
