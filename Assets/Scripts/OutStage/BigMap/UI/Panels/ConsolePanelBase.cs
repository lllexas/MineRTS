using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// ConsolePanelBase<T> - 控制台面板 UI 基类（视口切片渲染）
    /// ═══════════════════════════════════════════════════════════════
    ///
    /// 设计理念：
    /// 1. 双 Text 渲染 - HistoryText + InputText，GPU 只渲染 2 个网格
    /// 2. 视口裁切 - 根据滚动索引裁切可见行，一次性填入 HistoryText
    /// 3. CJK 网格对齐 - ASCII=1, 中文=2，强制物理换行
    /// 4. 段落感滚动 - 滚动时一行一行"跳"上去
    ///
    /// 继承关系：
    ///   SpaceUIAnimator
    ///   ↑
    ///   ConsolePanelBase<T>
    ///   ├─ DeveloperConsolePanel (开发终端面板)
    ///   └─ SocialPanelAnimator (社交终端面板)
    ///
    /// ═══════════════════════════════════════════════════════════════
    /// </summary>
    public abstract class ConsolePanelBase<T> : SpaceUIAnimator, IPointerClickHandler where T : DeveloperConsole
    {
        // =========================================================
        //  UI 组件引用（双 Text 架构）
        // =========================================================
        [Header("Console UI Components")]
        [Tooltip("输入框（TMP_InputField，透明覆盖，只捕获输入）")]
        [SerializeField] protected TMP_InputField inputField;

        [Tooltip("历史文本（TextMeshProUGUI，唯一历史输出口）")]
        [SerializeField] protected TextMeshProUGUI historyText;

        [Tooltip("输入文本（TextMeshProUGUI，当前输入行）")]
        [SerializeField] protected TextMeshProUGUI inputText;

        [Tooltip("滚动条（Scrollbar，用于可视化和拖拽滚动）")]
        [SerializeField] protected Scrollbar scrollbar;

        [Header("Console Settings")]
        [Tooltip("最大日志行数")]
        [SerializeField] protected int maxLogLines = 500;

        [Tooltip("是否自动滚动到底部")]
        [SerializeField] protected bool autoScrollToBottom = true;

        [Tooltip("滚轮滚动速度")]
        [SerializeField] protected float scrollSpeed = 3f;

        [Tooltip("光标闪烁速度（Hz）")]
        [SerializeField] protected float cursorBlinkSpeed = 4f;

        [Tooltip("光标字符")]
        [SerializeField] protected string cursorChar = "_";

        [Header("Cursor Settings")]
        [Tooltip("光标根节点（RectTransform，作为 inputText 的子对象，用于定位）")]
        [SerializeField] protected RectTransform cursorRoot;

        [Tooltip("光标背景（Image，白色块）")]
        [SerializeField] protected Image cursorBackground;

        [Tooltip("光标文字（TextMeshProUGUI，黑色文字）")]
        [SerializeField] protected TextMeshProUGUI cursorCharText;

        [Tooltip("IME 预览文本（TextMeshProUGUI，独立显示在左下角）")]
        [SerializeField] protected TextMeshProUGUI imePreviewText;

        [Tooltip("是否启用光标闪烁")]
        [SerializeField] protected bool enableCursorBlink = false;

        [Tooltip("光标闪烁速度（Hz）")]
        [SerializeField] protected float cursorBlockBlinkSpeed = 4f;

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
        //  输入状态
        // =========================================================
        protected int _lastCaretPosition = 0;
        protected string _lastInputText = "";
        
        // 上下导航保持性目标列
        protected int _targetColumn = -1;

        // 光标状态
        protected RectTransform _cursorRootRect;
        protected float _cursorBlinkTimer = 0f;
        protected bool _cursorVisible = true;

        // =========================================================
        //  抽象接口（由子类实现）
        // =========================================================

        /// <summary>
        /// 获取当前的 Prompt 字符串
        /// </summary>
        protected abstract string GetPrompt();

        /// <summary>
        /// 处理输入提交
        /// </summary>
        public abstract void OnSubmitCommand(string input);

        /// <summary>
        /// 获取逻辑层实例（由子类提供 T 引用）
        /// </summary>
        protected abstract T ConsoleLogic { get; }

        /// <summary>
        /// 关闭动作（简单 FadeOut）
        /// </summary>
        protected override void CloseAction() => FadeOut();

        // =========================================================
        //  Unity 生命周期
        // =========================================================

        protected override void Awake()
        {
            base.Awake();
            InitializeTerminal();
        }

        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
            // 注销滚动条事件
            if (scrollbar != null)
                scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);

            // 反订阅清屏事件
            if (ConsoleLogic != null)
                ConsoleLogic.OnClearRequested -= ClearLog;
        }

        /// <summary>
        /// 显示面板（重写以联动输入状态）
        /// </summary>
        public override void Show()
        {
            base.Show();
            // 显示时自动激活输入
            if (inputField != null)
            {
                inputField.enabled = true;
                inputField.ActivateInputField();
                inputField.Select();
            }
            
            // 显示光标（cursorBackground 在 Update 中按需启用）
        }

        /// <summary>
        /// 隐藏面板（重写以联动输入状态）
        /// </summary>
        public override void Hide()
        {
            // 隐藏时取消输入
            if (inputField != null)
            {
                inputField.DeactivateInputField();
                inputField.enabled = false;

                // 清空输入状态
                _lastInputText = "";
                _lastCaretPosition = 0;
            }

            // 隐藏光标
            if (cursorBackground != null) cursorBackground.enabled = false;
            
            // 隐藏 IME 预览
            if (imePreviewText != null)
            {
                imePreviewText.enabled = false;
            }

            // 重置光标状态
            _cursorBlinkTimer = 0f;
            _cursorVisible = true;

            base.Hide();
        }

        protected virtual void Start()
        {
            // 重新计算可见行数（确保 Layout 已完成）
            RecalculateVisibleRows();
            _buffer.MaxColumns = CalculateMaxColumns();
            if (ConsoleLogic != null && _buffer.MaxColumns > 0)
                ConsoleLogic.ConsoleWidth = _buffer.MaxColumns;

            // 订阅清屏事件
            if (ConsoleLogic != null)
                ConsoleLogic.OnClearRequested += ClearLog;
        }

        /// <summary>
        /// 初始化终端
        /// </summary>
        protected virtual void InitializeTerminal()
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
            _visibleRows = 25; // 默认值，防止 Awake 时 Layout 未布局完成

            // 监听滚动条
            if (scrollbar != null)
            {
                scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
                // 设置滚动条方向（从上到下）
                scrollbar.direction = Scrollbar.Direction.TopToBottom;
            }

            // 初始化输入框
            if (inputField != null)
            {
                inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
                inputField.contentType = TMP_InputField.ContentType.Standard;
                // 输入框颜色和射线穿透由 Unity Inspector 控制
            }

            // 初始化历史文本
            if (historyText != null)
            {
                historyText.richText = true;
                historyText.enableWordWrapping = true;
                historyText.alignment = TextAlignmentOptions.TopLeft;
            }

            // 初始化输入文本
            if (inputText != null)
            {
                inputText.richText = true;
                inputText.enableWordWrapping = true;
            }

            // 初始化光标背景（层级由 Inspector 排好，不在代码中移动）
            if (cursorBackground != null)
            {
                var bgRect = cursorBackground.rectTransform;
                bgRect.pivot        = new Vector2(0f, 0f);
                bgRect.anchorMin    = new Vector2(0f, 0f);
                bgRect.anchorMax    = new Vector2(0f, 0f);
                bgRect.anchoredPosition = Vector2.zero;
                cursorBackground.color   = Color.white;
                cursorBackground.enabled = false;
            }

            // 初始化 IME 预览文本
            if (imePreviewText != null && inputText != null)
            {
                imePreviewText.richText = true;
                imePreviewText.enableWordWrapping = false;
                imePreviewText.alignment = TextAlignmentOptions.TopLeft;
                imePreviewText.fontSize = inputText.fontSize;
                imePreviewText.font = inputText.font;
                imePreviewText.enabled = false;
            }
        }

        // =========================================================
        //  Update 轮询（脏标记优化）
        // =========================================================

        protected new virtual void Update()
        {
            base.Update();

            // 键盘输入监听
            HandleKeyboardInput();

            // 鼠标滚轮监听（当面板显示且鼠标在面板上时）
            if (_canvasGroup != null && _canvasGroup.alpha > 0.5f)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (scroll != 0)
                {
                    int maxScrollIndex = Mathf.Max(0, _buffer.LineCount - _visibleRows);
                    _scrollLineIndex -= Mathf.RoundToInt(scroll * scrollSpeed);
                    _scrollLineIndex = Mathf.Clamp(_scrollLineIndex, 0, maxScrollIndex);
                    _isDirty = true;
                }
            }

            // 输入行更新（同步处理）
            if (inputField != null && inputField.isFocused)
            {
                string currentText = inputField.text;
                int currentCaret = inputField.caretPosition;

                // 直接同步更新
                UpdateInputLine(currentText, currentCaret);
                
                _lastInputText = currentText;
                _lastCaretPosition = currentCaret;
            }

            // 历史文本更新（仅当需要时）
            if (_isDirty)
            {
                RefreshHistoryDisplay();
                _isDirty = false;
                UpdateScrollbar();
            }
        }

        // =========================================================
        //  输入事件处理（IPointerClickHandler）
        // =========================================================

        /// <summary>
        /// 点击窗口主体时激活输入框
        /// </summary>
        public override void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            base.OnPointerClick(eventData);
            // 点击时激活输入框（仅在面板显示时）
            if (inputField != null && _canvasGroup.blocksRaycasts)
            {
                inputField.enabled = true;
                inputField.ActivateInputField();
                inputField.Select();
                
                // 强制立即更新光标位置
                UpdateInputLine(inputField.text, inputField.caretPosition);
            }
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

            // 确保 _visibleRows 有效（兜底逻辑）
            if (_visibleRows <= 0)
            {
                _visibleRows = RecalculateVisibleRows();
            }

            // 确保 visibleRows 至少为 1
            int visibleRows = Mathf.Max(1, _visibleRows);
            
            // 限制 scrollIndex 在有效范围内
            int maxScrollIndex = Mathf.Max(0, _buffer.LineCount - visibleRows);
            _scrollLineIndex = Mathf.Clamp(_scrollLineIndex, 0, maxScrollIndex);

            // 裁切可见行
            var visibleLines = _buffer.GetVisibleLines(_scrollLineIndex, visibleRows);

            // 一次性填入
            // 注意：需要确保每行的颜色标签正确闭合，否则颜色会泄漏到下一行喵~
            var closedLines = new List<string>();
            foreach (var line in visibleLines)
            {
                // 统计该行未闭合的颜色标签数量
                int openTags = CountSubstring(line, "<color=");
                int closeTags = CountSubstring(line, "</color>");
                
                if (openTags > closeTags)
                {
                    // 补充缺失的 </color> 标签
                    string closedLine = line;
                    for (int i = 0; i < openTags - closeTags; i++)
                    {
                        closedLine += "</color>";
                    }
                    closedLines.Add(closedLine);
                }
                else
                {
                    closedLines.Add(line);
                }
            }
            
            historyText.text = string.Join("\n", closedLines);
        }

        /// <summary>
        /// 统计子字符串出现次数
        /// </summary>
        private static int CountSubstring(string text, string substring)
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
        //  光标控制方法
        // =========================================================

        /// <summary>
        /// 处理光标闪烁（纯状态维护，由 UpdateInputLine 消费）
        /// </summary>
        private void HandleCursorBlink()
        {
            if (!enableCursorBlink) { _cursorVisible = true; return; }
            _cursorBlinkTimer += Time.unscaledDeltaTime * cursorBlockBlinkSpeed;
            _cursorVisible = Mathf.Sin(_cursorBlinkTimer * Mathf.PI) >= 0;
        }

        /// <summary>
        /// 更新输入行（同步处理，富文本反色光标方案）
        /// </summary>
        protected virtual void UpdateInputLine(string input, int caret)
        {
            if (inputText == null || inputField == null) return;
            string composition = Input.compositionString;
            caret = Mathf.Clamp(caret, 0, input.Length);
            if (input != _lastInputText) _targetColumn = -1;

            HandleCursorBlink();

            string beforeCaret = input.Substring(0, caret);
            string afterCaret  = caret < input.Length ? input.Substring(caret + 1) : "";
            char   rawChar     = caret < input.Length ? input[caret] : '\0';

            string cursorSegment;
            if (_cursorVisible && rawChar != '\0' && rawChar != '\n')
                cursorSegment = $"<color=#000000>{rawChar}</color>";
            else
                cursorSegment = rawChar == '\0' ? "" : rawChar.ToString();

            string inputWithCursor = beforeCaret + cursorSegment + afterCaret;
            string imePreview = string.IsNullOrEmpty(composition) ? "" : $"<u color=white>{composition}</u>";
            string formattedInput = inputWithCursor.Replace("\n", "\n" + GetPrompt());
            inputText.text = GetPrompt() + formattedInput + imePreview;
            inputText.ForceMeshUpdate();

            if (cursorBackground != null)
            {
                if (_cursorVisible) { PositionCursorBackground(caret, input); cursorBackground.enabled = true; }
                else cursorBackground.enabled = false;
            }

            if (imePreviewText != null)
            {
                if (!string.IsNullOrEmpty(composition)) { imePreviewText.text = $"<color=#FFFFFF>{composition}</color>"; imePreviewText.enabled = true; }
                else imePreviewText.enabled = false;
            }
        }

        /// <summary>
        /// 将 cursorBackground Image 精确定位到光标字符处
        /// </summary>
        private void PositionCursorBackground(int rawCaret, string rawInput)
        {
            TMP_TextInfo textInfo = inputText.textInfo;
            if (textInfo == null || textInfo.characterCount == 0) return;

            int promptLen = GetPromptVisibleLength();
            int newlines = 0;
            for (int i = 0; i < rawCaret && i < rawInput.Length; i++)
                if (rawInput[i] == '\n') newlines++;
            int charIndex = promptLen * (1 + newlines) + rawCaret;

            bool isAtEnd = charIndex >= textInfo.characterCount;
            TMP_CharacterInfo ci = isAtEnd
                ? textInfo.characterInfo[textInfo.characterCount - 1]
                : textInfo.characterInfo[charIndex];

            float x = isAtEnd ? ci.xAdvance : ci.origin;
            float y = ci.descender;
            float w = isAtEnd ? (inputText.fontSize * 0.5f) : (ci.xAdvance - ci.origin);
            float h = ci.ascender - ci.descender;

            Vector3 worldPos = inputText.transform.TransformPoint(x, y, 0f);
            RectTransform bgRect = cursorBackground.rectTransform;
            bgRect.localPosition = bgRect.parent.InverseTransformPoint(worldPos);
            bgRect.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// 计算 Prompt 的可见字符数（去除富文本标签）
        /// </summary>
        private int GetPromptVisibleLength()
        {
            string prompt = GetPrompt();
            if (string.IsNullOrEmpty(prompt)) return 0;
            int count = 0; bool inTag = false;
            foreach (char c in prompt)
            {
                if (c == '<') inTag = true;
                else if (c == '>' && inTag) inTag = false;
                else if (!inTag) count++;
            }
            return count;
        }

        // =========================================================
        //  输出协议
        // =========================================================

        /// <summary>
        /// 输出一行文本
        /// </summary>
        public virtual void OutputLine(string message, Color color)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(color);
            string formatted = $"<color=#{colorHex}>{message}</color>";
            _buffer.AddLine(formatted);
            _isDirty = true;

            if (autoScrollToBottom)
            {
                ScrollToBottom();
            }
        }

        /// <summary>
        /// 输出原始文本
        /// </summary>
        public virtual void Output(string message, Color color)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(color);
            string formatted = $"<color=#{colorHex}>{message}</color>";
            _buffer.AddLine(formatted);
            _isDirty = true;

            if (autoScrollToBottom)
            {
                ScrollToBottom();
            }
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        public virtual void ClearLog()
        {
            _buffer?.Clear();
            _scrollLineIndex = 0;
            _isDirty = true;
        }

        // =========================================================
        //  滚动控制
        // =========================================================

        /// <summary>
        /// 滚动到底部
        /// </summary>
        protected virtual void ScrollToBottom()
        {
            // 确保 _visibleRows 至少为 1
            int visibleRows = Mathf.Max(1, _visibleRows);
            _scrollLineIndex = Mathf.Max(0, _buffer.LineCount - visibleRows);
            UpdateScrollbar();
        }

        /// <summary>
        /// 更新滚动条位置
        /// </summary>
        protected virtual void UpdateScrollbar()
        {
            if (scrollbar == null) return;

            int totalLines = _buffer.LineCount;
            int visibleRows = Mathf.Max(1, _visibleRows);

            // 内容还没窗口多，不需要滚动条喵！
            if (totalLines <= visibleRows)
            {
                scrollbar.size = 1f;
                scrollbar.value = 0;
                // 如果主人想更专业点，可以这里把 scrollbar.gameObject.SetActive(false);
                return;
            }

            // 计算实际可滚动的区间大小
            int maxScrollIndex = totalLines - visibleRows;

            // 把手大小：窗口行数 / 总行数
            scrollbar.size = Mathf.Clamp01((float)visibleRows / totalLines);

            // 把手位置：当前索引 / 最大可滚动索引
            // 注意：因为主人设置了 BottomToTop，需要确认 0 是底部还是顶部喵
            scrollbar.value = Mathf.Clamp01((float)_scrollLineIndex / maxScrollIndex);
        }

        /// <summary>
        /// 滚动条事件处理
        /// </summary>
        protected virtual void OnScrollbarChanged(float value)
        {
            // value 0~1，映射到 0~maxScrollIndex
            int maxScrollIndex = Mathf.Max(0, _buffer.LineCount - _visibleRows);
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
        /// 计算每行最大列数（margin 感知 + 字体精确宽度）
        /// </summary>
        protected virtual int CalculateMaxColumns()
        {
            if (historyText == null || historyText.font == null) return 140;

            float viewportWidth = historyText.rectTransform.rect.width
                                  - historyText.margin.x - historyText.margin.z;
            if (viewportWidth <= 0) return 140;

            float charWidth = GetActualCharWidth();
            return Mathf.FloorToInt(viewportWidth / charWidth);
        }

        /// <summary>
        /// 从字体表取实际半宽字符推进宽度（ASCII → CJK 半宽 → 0.5em 兜底）
        /// </summary>
        private float GetActualCharWidth()
        {
            if (historyText == null || historyText.font == null)
                return historyText.fontSize * 0.5f;

            var font = historyText.font;
            float scale = historyText.fontSize / font.faceInfo.pointSize;

            // 优先用 'M'（ASCII 半宽参考字符）
            if (font.characterLookupTable != null && font.characterLookupTable.TryGetValue('M', out var chM))
                return chM.glyph.metrics.horizontalAdvance * scale;

            // 'M' 不在字体表（纯 CJK Mono 字体）→ 用全宽字符 advance 的一半
            if (font.characterLookupTable != null && font.characterLookupTable.TryGetValue('我', out var chWo))
                return (chWo.glyph.metrics.horizontalAdvance * scale) / 2f;

            // 终极兜底：等宽 CJK 字体半宽 = 0.5em
            return historyText.fontSize * 0.5f;
        }

        /// <summary>
        /// 重新计算可见行数（确保 Layout 完成后调用）
        /// </summary>
        protected virtual int RecalculateVisibleRows()
        {
            // 使用 historyText 的 RectTransform 来计算可见行数
            // 因为这才是实际用于显示文本的区域喵~
            if (historyText != null)
            {
                float textHeight = historyText.rectTransform.rect.height;

                // 使用 TextMeshPro 的实际行高（fontSize + lineSpacing）
                // TMP 的行高约等于 fontSize * 1.2（考虑行间距）
                float actualLineHeight = historyText.fontSize * 1.2f;

                _visibleRows = Mathf.FloorToInt(textHeight / actualLineHeight);
                if (_visibleRows <= 0)
                {
                    _visibleRows = 25; // 默认值，防止计算结果为 0
                }

                // 更新 lineHeight 以匹配实际行高，用于后续滚动计算
                lineHeight = actualLineHeight;
            }
            else
            {
                _visibleRows = 25; // 默认值
            }
            return _visibleRows;
        }

        // =========================================================
        //  多行输入导航辅助方法
        // =========================================================

        /// <summary>
        /// 计算当前行号和列号
        /// </summary>
        private void GetLineColumn(int caret, string text, out int line, out int column)
        {
            line = 0;
            column = 0;
            for (int i = 0; i < caret; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    column = 0;
                }
                else
                {
                    column++;
                }
            }
        }

        /// <summary>
        /// 计算某行的起始位置
        /// </summary>
        private int GetLineStart(int line, string text)
        {
            int currentLine = 0;
            int startPos = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    currentLine++;
                    if (currentLine == line)
                    {
                        return i + 1;
                    }
                }
            }
            return startPos;
        }

        /// <summary>
        /// 计算某行的结束位置
        /// </summary>
        private int GetLineEnd(int line, string text)
        {
            int currentLine = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    if (currentLine == line)
                    {
                        return i;
                    }
                    currentLine++;
                }
            }
            return text.Length;
        }

        /// <summary>
        /// 处理上下箭头导航（保持列位置）
        /// </summary>
        private void HandleVerticalNavigation(bool isUp)
        {
            if (inputField == null) return;

            string text = inputField.text;
            int currentCaret = inputField.caretPosition;

            GetLineColumn(currentCaret, text, out int currentLine, out int currentColumn);

            // 更新或设置目标列
            if (_targetColumn < 0)
            {
                _targetColumn = currentColumn;
            }

            int targetLine = isUp ? currentLine - 1 : currentLine + 1;
            int totalLines = text.Count(c => c == '\n') + 1;

            if (targetLine >= 0 && targetLine < totalLines)
            {
                int lineStart = GetLineStart(targetLine, text);
                int lineEnd = GetLineEnd(targetLine, text);
                int lineLength = lineEnd - lineStart;

                // 目标位置 = 行首 + min(目标列，行长度)
                int targetCaret = lineStart + Mathf.Min(_targetColumn, lineLength);
                inputField.caretPosition = targetCaret;
            }
        }

        /// <summary>
        /// 移到行首
        /// </summary>
        private void MoveToLineStart()
        {
            if (inputField == null) return;

            string text = inputField.text;
            int currentCaret = inputField.caretPosition;

            GetLineColumn(currentCaret, text, out int line, out int column);
            int lineStart = GetLineStart(line, text);

            inputField.caretPosition = lineStart;
            _targetColumn = 0;
        }

        /// <summary>
        /// 移到行尾
        /// </summary>
        private void MoveToLineEnd()
        {
            if (inputField == null) return;

            string text = inputField.text;
            int currentCaret = inputField.caretPosition;

            GetLineColumn(currentCaret, text, out int line, out int column);
            int lineEnd = GetLineEnd(line, text);

            inputField.caretPosition = lineEnd;
            _targetColumn = column; // 保持当前列
        }

        // =========================================================
        //  输入处理
        // =========================================================

        /// <summary>
        /// 处理键盘输入
        /// </summary>
        protected virtual void HandleKeyboardInput()
        {
            if (inputField == null || !inputField.isFocused) return;

            // 策略激活时：数字键（1-9）直接选择选项，不需要 Enter
            if (ConsoleLogic != null && ConsoleLogic.HasActiveStrategy)
            {
                for (int i = 1; i <= 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                    {
                        ConsoleLogic.ProcessCommand(i.ToString());
                        inputField.text = "";
                        _lastInputText = "";
                        return;
                    }
                }
            }

            // 上下箭头：有活跃策略时转发给策略，否则正常导航
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                bool isUp = Input.GetKeyDown(KeyCode.UpArrow);
                if (ConsoleLogic != null && ConsoleLogic.HasActiveStrategy)
                    ConsoleLogic.SendArrowKeyToStrategy(isUp);
                else
                    HandleVerticalNavigation(isUp);
                return;
            }

            // Home 键：移到行首
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveToLineStart();
                return;
            }

            // End 键：移到行尾
            if (Input.GetKeyDown(KeyCode.End))
            {
                MoveToLineEnd();
                return;
            }

            // 左右移动时重置目标列
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                _targetColumn = -1;
            }

            // 回车提交命令（Shift+Enter 换行）
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Shift+Enter 不提交，只换行（由 inputField 自动处理）
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    return;
                }

                // 有活跃策略且输入为空时，转发 Confirm（↑↓ 导航后 Enter 确认）
                if (ConsoleLogic != null && ConsoleLogic.HasActiveStrategy && string.IsNullOrWhiteSpace(inputField.text))
                {
                    ConsoleLogic.ConfirmStrategySelection();
                    inputField.text = "";
                    _lastInputText = "";
                    return;
                }

                // 提交时移除所有换行符（防止自动换行或手动换行截断指令）
                string input = System.Text.RegularExpressions.Regex.Replace(inputField.text, @"\s*\n\s*", " ");
                
                // 提交处理后的内容
                OnSubmitCommand(input);

                // 清空输入框
                inputField.text = "";
                _lastInputText = "";
                _isDirty = true;

                // 更新
                UpdateInputLine("", 0);
                ScrollToBottom();
            }
        }
    }
}
