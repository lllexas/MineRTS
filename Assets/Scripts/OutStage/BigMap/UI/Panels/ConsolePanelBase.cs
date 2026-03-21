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

        [Tooltip("输入文本（TextMeshProUGUI，当前输入行）")]
        [SerializeField] protected TextMeshProUGUI inputText;

        [Header("Cursor Settings")]
        [Tooltip("光标根节点（RectTransform，作为 inputText 的子对象，用于定位）")]
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
        /// 关闭动作（简单 FadeOut）
        /// </summary>
        protected override void CloseAction() => FadeOut();

        // =========================================================
        //  Unity 生命周期
        // =========================================================

        protected override void Awake()
        {
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
            // 显示时自动激活输入
            if (inputField != null)
            {
                inputField.enabled = true;
                inputField.ActivateInputField();
                inputField.Select();
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
                bgRect.pivot           = new Vector2(0f, 0f);
                bgRect.anchorMin       = new Vector2(0f, 0f);
                bgRect.anchorMax       = new Vector2(0f, 0f);
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
        //  Update 轮询（输入层）
        // =========================================================

        protected new virtual void Update()
        {
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

            // 输入处理器激活时：数字键（1-9）直接走提交通道
            if (ConsoleLogic != null && ConsoleLogic.HasInputHandler)
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ConsoleLogic != null && ConsoleLogic.TryHandleCancel())
                    return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (ConsoleLogic != null && ConsoleLogic.TryHandleNavigation(ConsoleNavKey.Up))
                    return;

                HandleVerticalNavigation(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (ConsoleLogic != null && ConsoleLogic.TryHandleNavigation(ConsoleNavKey.Down))
                    return;

                HandleVerticalNavigation(false);
                return;
            }

            // Home 键：移到行首
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (ConsoleLogic != null && ConsoleLogic.TryHandleNavigation(ConsoleNavKey.Home))
                    return;

                MoveToLineStart();
                return;
            }

            // End 键：移到行尾
            if (Input.GetKeyDown(KeyCode.End))
            {
                if (ConsoleLogic != null && ConsoleLogic.TryHandleNavigation(ConsoleNavKey.End))
                    return;

                MoveToLineEnd();
                return;
            }

            // 左右移动时重置目标列
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (ConsoleLogic != null && ConsoleLogic.TryHandleNavigation(ConsoleNavKey.Left))
                    return;

                _targetColumn = -1;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (ConsoleLogic != null && ConsoleLogic.TryHandleNavigation(ConsoleNavKey.Right))
                    return;

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

                if (string.IsNullOrWhiteSpace(inputField.text))
                {
                    if (ConsoleLogic != null && ConsoleLogic.TryHandleConfirm())
                    {
                        inputField.text = "";
                        _lastInputText = "";
                        UpdateInputLine("", 0);
                        return;
                    }
                }

                // 提交时移除所有换行符（防止自动换行或手动换行截断指令）
                string input = System.Text.RegularExpressions.Regex.Replace(inputField.text, @"\s*\n\s*", " ");

                if (ConsoleLogic != null && ConsoleLogic.TryHandleSubmit(input))
                {
                    inputField.text = "";
                    _lastInputText = "";
                    UpdateInputLine("", 0);
                    return;
                }

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
