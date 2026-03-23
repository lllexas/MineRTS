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
    /// ConsolePanelBase<T> - 控制台面板 UI 基类（输入层）
    /// ═══════════════════════════════════════════════════════════════
    ///
    /// 设计理念：
    /// 1. 双 Text 渲染 - HistoryText（基类）+ InputText，GPU 只渲染 2 个网格
    /// 2. 视口裁切 - 根据滚动索引裁切可见行，一次性填入 HistoryText（基类）
    /// 3. CJK 网格对齐 - ASCII=1, 中文=2，强制物理换行
    /// 4. 段落感滚动 - 滚动时一行一行"跳"上去
    ///
    /// 继承关系：
    ///   SpaceUIAnimator
    ///   ↑
    ///   ConsoleDisplayBase（纯显示层）
    ///   ↑
    ///   ConsolePanelBase<T>（添加输入层）
    ///   ├─ DeveloperConsolePanel (开发终端面板)
    ///   └─ SocialPanelAnimator (社交终端面板)
    ///
    /// ═══════════════════════════════════════════════════════════════
    /// </summary>
    public abstract class ConsolePanelBase<T> : ConsoleDisplayBase<T>, IPointerClickHandler where T : DeveloperConsole
    {
        // =========================================================
        //  UI 组件引用（输入层）
        // =========================================================
        [Header("Console Input Components")]
        [Tooltip("输入框（TMP_InputField，透明覆盖，只捕获输入）")]
        [SerializeField] protected TMP_InputField inputField;

        [Header("Cursor Settings")]
        [Tooltip("光标根节点（RectTransform，用于定位光标块）")]
        [SerializeField] protected RectTransform cursorRoot;

        [Tooltip("光标背景（Image，白色块）")]
        [SerializeField] protected Image cursorBackground;

        [Tooltip("光标文字（TextMeshProUGUI，黑色文字）")]
        [SerializeField] protected TextMeshProUGUI cursorCharText;

        [Tooltip("IME 预览文本（TextMeshProUGUI，独立显示在左下角）")]
        [SerializeField] protected TextMeshProUGUI imePreviewText;

        [Tooltip("光标字符")]
        [SerializeField] protected string cursorChar = "_";

        [Tooltip("光标闪烁速度（Hz）")]
        [SerializeField] protected float cursorBlinkSpeed = 4f;

        [Tooltip("是否启用光标闪烁")]
        [SerializeField] protected bool enableCursorBlink = false;

        [Tooltip("光标块闪烁速度（Hz）")]
        [SerializeField] protected float cursorBlockBlinkSpeed = 4f;

        // =========================================================
        //  输入状态
        // =========================================================
        protected int _lastCaretPosition = 0;
        protected string _lastInputText = "";

        // 上下导航保持性目标列
        protected int _targetColumn = -1;
        protected bool _isVerticalNavigationActive = false;

        // 光标状态
        protected RectTransform _cursorRootRect;
        protected float _cursorBlinkTimer = 0f;
        protected bool _cursorVisible = true;
        protected string _renderedInputText = "";
        protected int _renderedInputLineCount = 1;

        private int _inputStartSourceIndex = -1;
        private int _inputStartLineNumber = -1;
        private bool _inputHandleHostBound = false;
        private Func<int> _inputHandleLineProvider;
        private Action<int, int, IEnumerable<string>> _inputHandleRangeWriter;

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
        /// 关闭动作（简单 FadeOut）
        /// </summary>
        protected override void CloseAction() => FadeOut();

        // =========================================================
        //  Unity 生命周期
        // =========================================================

        protected override void Awake()
        {
            _inputHandleLineProvider = GetInputHandleStartLine;
            _inputHandleRangeWriter = ReplaceRange;
            base.Awake(); // ConsoleDisplayBase.Awake → InitializeDisplay
            InitializeTerminal(); // 只含输入层初始化
        }

        protected virtual void OnEnable()
        {
        }

        protected override void OnDisable()
        {
            base.OnDisable(); // ConsoleDisplayBase.OnDisable 已处理 Clear 反订阅和滚动条
        }

        /// <summary>
        /// 显示面板（重写以联动输入状态）
        /// </summary>
        public override void Show()
        {
            base.Show();
            TryBindInputHandleHost();
            // 显示时自动激活输入
            if (inputField != null)
            {
                inputField.enabled = true;
                inputField.ActivateInputField();
                inputField.Select();
                UpdateInputLine(inputField.text, inputField.caretPosition);
            }
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
            if (cursorCharText != null) cursorCharText.enabled = false;

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

        protected override void Start()
        {
            base.Start(); // ConsoleDisplayBase.Start 已处理 Clear/Width 注入
            TryBindInputHandleHost();
            if (inputField != null)
            {
                UpdateInputLine(inputField.text, inputField.caretPosition);
            }
        }

        protected override void OnDestroy()
        {
            TryUnbindInputHandleHost();
            base.OnDestroy();
        }

        /// <summary>
        /// 初始化终端输入层（不含显示层，由基类 InitializeDisplay 处理）
        /// </summary>
        protected virtual void InitializeTerminal()
        {
            // 初始化输入框
            if (inputField != null)
            {
                inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
                inputField.contentType = TMP_InputField.ContentType.Standard;
            }

            // 初始化光标背景（层级由 Inspector 排好，不在代码中移动）
            if (cursorBackground != null)
            {
                var bgRect = cursorBackground.rectTransform;
                bgRect.pivot           = new Vector2(0f, 0f);
                bgRect.anchorMin       = new Vector2(0f, 0f);
                bgRect.anchorMax       = new Vector2(0f, 0f);
                bgRect.anchoredPosition = Vector2.zero;
                cursorBackground.color   = Color.white;
                cursorBackground.enabled = false;
            }

            // 初始化 IME 预览文本
            if (imePreviewText != null && historyText != null)
            {
                imePreviewText.richText = true;
                imePreviewText.enableWordWrapping = false;
                imePreviewText.alignment = TextAlignmentOptions.TopLeft;
                imePreviewText.fontSize = historyText.fontSize;
                imePreviewText.font = historyText.font;
                imePreviewText.enabled = false;
            }
        }

        // =========================================================
        //  Update 轮询（输入层）
        // =========================================================

        protected new virtual void Update()
        {
            TryBindInputHandleHost();
            base.Update(); // 滚轮 + 脏刷新（ConsoleDisplayBase 处理）

            // 键盘输入监听
            HandleKeyboardInput();

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
            _targetColumn = -1;
            _isVerticalNavigationActive = false;
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

        private void ResetCursorBlink()
        {
            _cursorBlinkTimer = 0f;
            _cursorVisible = true;
        }

        /// <summary>
        /// 更新输入行（同步处理，富文本反色光标方案）
        /// </summary>
        protected virtual void UpdateInputLine(string input, int caret)
        {
            if (historyText == null || inputField == null) return;

            input ??= string.Empty;
            string composition = Input.compositionString;
            caret = Mathf.Clamp(caret, 0, input.Length);
            if (input != _lastInputText)
            {
                _targetColumn = -1;
                _isVerticalNavigationActive = false;
            }

            if (caret != _lastCaretPosition)
            {
                ResetCursorBlink();
            }
            else
            {
                HandleCursorBlink();
            }

            ResolveInputLineState(out bool shouldRenderInputLine, out string prompt);
            string renderedInput = shouldRenderInputLine
                ? BuildRenderedInputText(input, caret, composition, prompt)
                : string.Empty;
            int renderedLineCount = shouldRenderInputLine ? CountRenderedLines(renderedInput) : 0;
            bool layoutChanged = renderedLineCount != _renderedInputLineCount;
            bool contentChanged = !string.Equals(_renderedInputText, renderedInput, StringComparison.Ordinal);

            _renderedInputText = renderedInput;
            _renderedInputLineCount = renderedLineCount;

            if (layoutChanged)
            {
                if (autoScrollToBottom) ScrollToBottom();
                else ClampScrollAndInvalidate();
            }

            if (contentChanged || layoutChanged)
            {
                _isDirty = true;
                RefreshHistoryDisplay();
                _isDirty = false;
            }

            if (cursorBackground != null)
            {
                if (shouldRenderInputLine && _cursorVisible && inputField.isFocused)
                {
                    PositionCursorBackground(caret, input, prompt);
                    cursorBackground.enabled = true;
                }
                else cursorBackground.enabled = false;
            }

            if (cursorCharText != null) cursorCharText.enabled = false;

            if (imePreviewText != null)
            {
                imePreviewText.enabled = false;
            }

            _lastInputText = input;
            _lastCaretPosition = caret;
        }

        /// <summary>
        /// 将 cursorBackground Image 精确定位到光标字符处
        /// </summary>
        private void PositionCursorBackground(int rawCaret, string rawInput, string prompt)
        {
            TMP_TextInfo textInfo = historyText.textInfo;
            if (textInfo == null || textInfo.lineCount <= 0) return;

            int absoluteCaretSourceIndex = GetAbsoluteCaretSourceIndex(rawCaret, rawInput, prompt);
            int lineNumber = GetAbsoluteInputLineNumber(rawCaret, rawInput, textInfo.lineCount);
            bool isOnRenderableCharacter = rawCaret < rawInput.Length && rawInput[rawCaret] != '\n';

            float x;
            float y;
            float w;
            float h;

            if (isOnRenderableCharacter && TryFindCharacterInfo(textInfo, absoluteCaretSourceIndex, out TMP_CharacterInfo ci))
            {
                x = ci.bottomLeft.x;
                y = ci.descender;
                w = Mathf.Max(1f, ci.topRight.x - ci.bottomLeft.x);
                h = ci.ascender - ci.descender;
            }
            else
            {
                TMP_LineInfo lineInfo = textInfo.lineInfo[lineNumber];
                x = lineInfo.maxAdvance;

                // TMP 不将行尾空格计入 maxAdvance，需手动补偿喵~
                int trailingSpaces = 0;
                for (int i = rawCaret - 1; i >= 0 && rawInput[i] != '\n'; i--)
                {
                    if (rawInput[i] == ' ') trailingSpaces++;
                    else break;
                }
                if (trailingSpaces > 0)
                    x += trailingSpaces * GetCharacterWidth(' ', historyText);

                y = lineInfo.descender;
                w = Mathf.Max(1f, GetCursorAdvanceWidth());
                h = lineInfo.ascender - lineInfo.descender;
            }

            ApplyCursorVisual(x, y, w, h);
        }

        protected override int GetReservedBottomRows()
        {
            if (_canvasGroup == null || !_canvasGroup.blocksRaycasts)
            {
                return 0;
            }

            ResolveInputLineState(out bool shouldRenderInputLine, out _);
            if (!shouldRenderInputLine)
            {
                return 0;
            }

            return Mathf.Max(1, _renderedInputLineCount);
        }

        protected override void RefreshHistoryDisplay()
        {
            if (historyText == null || _buffer == null) return;

            if (_visibleRows <= 0)
            {
                _visibleRows = RecalculateVisibleRows();
            }

            int historyRows = GetHistoryViewportRows();
            int maxScrollIndex = Mathf.Max(0, _buffer.LineCount - GetScrollableHistoryRows());
            _scrollLineIndex = Mathf.Clamp(_scrollLineIndex, 0, maxScrollIndex);

            var historyLines = historyRows > 0
                ? _buffer.GetVisibleLines(_scrollLineIndex, historyRows)
                : new List<string>();

            var closedHistoryLines = new List<string>(historyLines.Count);
            foreach (var line in historyLines)
            {
                closedHistoryLines.Add(CloseColorTags(line));
            }

            var displayLines = new List<string>(closedHistoryLines);
            ResolveInputLineState(out bool shouldRenderInputLine, out _);
            var inputLines = shouldRenderInputLine
                ? SplitRenderedLines(_renderedInputText)
                : new List<string>();
            string historyBlock = string.Join("\n", closedHistoryLines);
            _inputStartSourceIndex = -1;
            _inputStartLineNumber = -1;

            if (inputLines.Count > 0)
            {
                _inputStartLineNumber = closedHistoryLines.Count > 0 ? closedHistoryLines.Count : 0;
                _inputStartSourceIndex = closedHistoryLines.Count > 0 ? historyBlock.Length + 1 : 0;
                displayLines.AddRange(inputLines);
            }

            historyText.text = string.Join("\n", displayLines);
            historyText.ForceMeshUpdate();
        }

        private void ResolveInputLineState(out bool shouldRenderInputLine, out string prompt)
        {
            shouldRenderInputLine = true;
            prompt = GetPrompt() ?? string.Empty;

            IConsoleInputLineState lineState = ConsoleLogic != null
                ? ConsoleLogic.CurrentInputHandler as IConsoleInputLineState
                : null;
            if (lineState == null)
            {
                return;
            }

            shouldRenderInputLine = lineState.ShouldRenderInputLine;
            string customPrompt = lineState.GetInputPrompt(prompt);
            if (!string.IsNullOrEmpty(customPrompt))
            {
                prompt = customPrompt;
            }
        }

        private void TryBindInputHandleHost()
        {
            if (_inputHandleHostBound || ConsoleLogic == null)
            {
                return;
            }

            ConsoleLogic.BindInputHandleHost(_inputHandleLineProvider, _inputHandleRangeWriter);
            _inputHandleHostBound = true;
        }

        private void TryUnbindInputHandleHost()
        {
            if (!_inputHandleHostBound || ConsoleLogic == null)
            {
                return;
            }

            ConsoleLogic.UnbindInputHandleHost(_inputHandleLineProvider, _inputHandleRangeWriter);
            _inputHandleHostBound = false;
        }

        private int GetInputHandleStartLine()
        {
            return LineCount;
        }

        private string BuildRenderedInputText(string input, int caret, string composition, string prompt)
        {
            string beforeCaret = input.Substring(0, caret);
            string afterCaret = caret < input.Length ? input.Substring(caret + 1) : "";
            char rawChar = caret < input.Length ? input[caret] : '\0';

            string cursorSegment;
            if (_cursorVisible && inputField != null && inputField.isFocused && rawChar != '\0' && rawChar != '\n')
                cursorSegment = $"<color=#000000>{rawChar}</color>";
            else
                cursorSegment = rawChar == '\0' ? "" : rawChar.ToString();

            string inputWithCursor = beforeCaret + cursorSegment + afterCaret;
            string imePreview = string.IsNullOrEmpty(composition) ? "" : $"<u color=white>{composition}</u>";
            string formattedInput = inputWithCursor.Replace("\n", "\n" + prompt);
            return prompt + formattedInput + imePreview;
        }

        private void ApplyCursorVisual(float x, float y, float width, float height)
        {
            Vector3 worldPos = historyText.transform.TransformPoint(x, y, 0f);

            if (cursorBackground != null)
            {
                RectTransform bgRect = cursorBackground.rectTransform;
                bgRect.localPosition = bgRect.parent.InverseTransformPoint(worldPos);
                bgRect.sizeDelta = new Vector2(width, height);
            }
        }

        private int GetAbsoluteCaretSourceIndex(int rawCaret, string rawInput, string prompt)
        {
            if (_inputStartSourceIndex < 0)
            {
                return -1;
            }

            int relativeSourceIndex = prompt.Length;
            for (int i = 0; i < rawCaret && i < rawInput.Length; i++)
            {
                relativeSourceIndex += rawInput[i] == '\n'
                    ? 1 + prompt.Length
                    : 1;
            }

            bool highlightsCurrentCharacter =
                _cursorVisible &&
                inputField != null &&
                inputField.isFocused &&
                rawCaret >= 0 &&
                rawCaret < rawInput.Length &&
                rawInput[rawCaret] != '\n';

            if (highlightsCurrentCharacter)
            {
                relativeSourceIndex += "<color=#000000>".Length;
            }

            return _inputStartSourceIndex + relativeSourceIndex;
        }

        private int GetAbsoluteInputLineNumber(int rawCaret, string rawInput, int totalLineCount)
        {
            int lineNumber = Mathf.Max(0, _inputStartLineNumber);
            for (int i = 0; i < rawCaret && i < rawInput.Length; i++)
            {
                if (rawInput[i] == '\n')
                {
                    lineNumber++;
                }
            }

            return Mathf.Clamp(lineNumber, 0, Mathf.Max(0, totalLineCount - 1));
        }

        private static bool TryFindCharacterInfo(TMP_TextInfo textInfo, int sourceIndex, out TMP_CharacterInfo characterInfo)
        {
            if (textInfo != null)
            {
                for (int i = 0; i < textInfo.characterCount; i++)
                {
                    TMP_CharacterInfo ci = textInfo.characterInfo[i];
                    if (ci.index == sourceIndex)
                    {
                        characterInfo = ci;
                        return true;
                    }
                }
            }

            characterInfo = default(TMP_CharacterInfo);
            return false;
        }

        private float GetCursorAdvanceWidth()
        {
            char glyph = !string.IsNullOrEmpty(cursorChar) ? cursorChar[0] : '_';
            return GetCharacterWidth(glyph, historyText);
        }

        private static int CountRenderedLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1;
            }

            int count = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }

        private static List<string> SplitRenderedLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            return text.Split(new[] { '\n' }, StringSplitOptions.None).ToList();
        }

        private static int CountVisibleCharacters(IReadOnlyList<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                count += CountVisibleCharacters(lines[i]);
                if (i < lines.Count - 1)
                {
                    count += 1;
                }
            }

            return count;
        }

        private static int CountVisibleCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int count = 0;
            bool inTag = false;
            foreach (char c in text)
            {
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (c == '>' && inTag)
                {
                    inTag = false;
                    continue;
                }

                if (!inTag)
                {
                    count++;
                }
            }

            return count;
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

            // 只有连续上下导航时才保持锚点列；其他来源的移动会重新取当前列
            if (!_isVerticalNavigationActive || _targetColumn < 0)
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
                _isVerticalNavigationActive = true;
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
            _isVerticalNavigationActive = true;
        }

        /// <summary>
        /// 移到行尾
        /// </summary>
        private void MoveToLineEnd()
        {
            if (inputField == null) return;

            string text = inputField.text;
            int currentCaret = inputField.caretPosition;

            GetLineColumn(currentCaret, text, out int line, out _);
            int lineStart = GetLineStart(line, text);
            int lineEnd = GetLineEnd(line, text);
            int lineLength = lineEnd - lineStart;

            inputField.caretPosition = lineEnd;
            _targetColumn = lineLength;
            _isVerticalNavigationActive = true;
        }

        private void InsertNewLineAtCaret()
        {
            if (inputField == null) return;

            string text = inputField.text ?? string.Empty;
            int anchor = Mathf.Clamp(inputField.selectionStringAnchorPosition, 0, text.Length);
            int focus = Mathf.Clamp(inputField.selectionStringFocusPosition, 0, text.Length);
            int start = Mathf.Min(anchor, focus);
            int end = Mathf.Max(anchor, focus);
            string updatedText = text.Substring(0, start) + "\n" + text.Substring(end);
            int newCaret = start + 1;

            inputField.text = updatedText;
            inputField.stringPosition = newCaret;
            inputField.selectionStringAnchorPosition = newCaret;
            inputField.selectionStringFocusPosition = newCaret;
            inputField.caretPosition = newCaret;
            inputField.selectionAnchorPosition = newCaret;
            inputField.selectionFocusPosition = newCaret;
            inputField.ForceLabelUpdate();
            inputField.ActivateInputField();
            inputField.Select();

            _targetColumn = -1;
            _isVerticalNavigationActive = false;
            ResetCursorBlink();
            UpdateInputLine(updatedText, newCaret);
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

            // 有 InputHandler 时，所有输入打包成 KeyInfo 直接交给它处理喵~
            if (ConsoleLogic != null && ConsoleLogic.HasInputHandler)
            {
                var handler = ConsoleLogic.CurrentInputHandler;

                // 检测按下的键
                KeyCode? pressedKey = null;

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    pressedKey = KeyCode.Return;
                else if (Input.GetKeyDown(KeyCode.Escape))
                    pressedKey = KeyCode.Escape;
                else if (Input.GetKeyDown(KeyCode.UpArrow))
                    pressedKey = KeyCode.UpArrow;
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                    pressedKey = KeyCode.DownArrow;
                else if (Input.GetKeyDown(KeyCode.Home))
                    pressedKey = KeyCode.Home;
                else if (Input.GetKeyDown(KeyCode.End))
                    pressedKey = KeyCode.End;
                else if (Input.GetKeyDown(KeyCode.LeftArrow))
                    pressedKey = KeyCode.LeftArrow;
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                    pressedKey = KeyCode.RightArrow;

                // 有键按下就打包丢给 Handler
                if (pressedKey.HasValue)
                {
                    var keyInfo = new KeyInfo
                    {
                        keyCode = pressedKey.Value,
                        isShiftDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                        isCtrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                        isAltDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
                    };

                    if (handler.HandleKey(keyInfo))
                        return; // Handler 处理了，返回
                }

                // 其他输入（文本输入）通过输入框内容提交
                return;
            }

            // 没有 InputHandler，走默认处理逻辑
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Shift+Enter 不提交，只换行（由 inputField 自动处理）
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    InsertNewLineAtCaret();
                    return;
                }

                if (string.IsNullOrWhiteSpace(inputField.text))
                {
                    _lastInputText = "";
                    UpdateInputLine("", 0);
                    return;
                }

                string input = System.Text.RegularExpressions.Regex.Replace(_lastInputText, @"\s*\n\s*", " ").Trim();

                string prompt = GetPrompt();
                string echoedInput = prompt + input;
                Output(echoedInput, Color.white);

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
