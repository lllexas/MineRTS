using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeveloperConsole : SingletonMono<DeveloperConsole>
{
    [Header("UI Components")]
    [SerializeField] private GameObject consoleWindow;
    [SerializeField] private TMP_InputField inputField; // <--- 记得在 Inspector 里把 Line Type 改为 Multi Line Newline 喵！
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Button closeButton;

    private Dictionary<string, System.Action<string[]>> _commands;
    private StringBuilder _logBuilder = new StringBuilder();
    private bool _needScrollToBottom = false;
    private const int MAX_LOG_LINES = 100;

    // --- 公共接口 (API) ---

    public void AddCommand(string key, System.Action<string[]> action)
    {
        key = key.ToLower();
        if (_commands.ContainsKey(key))
        {
            Log($"Command '{key}' is already registered!", Color.yellow);
            return;
        }
        _commands.Add(key, action);
    }

    public void Log(string message, Color color)
    {
        string colorHex = ColorUtility.ToHtmlStringRGB(color);
        _logBuilder.AppendLine($"<color=#{colorHex}>{message}</color>");

        // 限制日志行数，防止内存无限增长
        TrimLogLines();

        logText.text = _logBuilder.ToString();

        // 标记需要滚动到底部，将在Update中延迟执行
        _needScrollToBottom = true;
    }

    private void TrimLogLines()
    {
        // 简单实现：计算换行符数量来估计行数
        int lineCount = 0;
        for (int i = 0; i < _logBuilder.Length; i++)
        {
            if (_logBuilder[i] == '\n') lineCount++;
        }

        // 如果超过最大行数，移除最旧的行
        if (lineCount > MAX_LOG_LINES)
        {
            // 找到第(行数 - MAX_LOG_LINES)个换行符的位置
            int linesToRemove = lineCount - MAX_LOG_LINES;
            int charIndex = 0;
            int foundNewlines = 0;

            for (charIndex = 0; charIndex < _logBuilder.Length; charIndex++)
            {
                if (_logBuilder[charIndex] == '\n')
                {
                    foundNewlines++;
                    if (foundNewlines == linesToRemove)
                    {
                        // 保留这个换行符之后的文本
                        _logBuilder.Remove(0, charIndex + 1);
                        break;
                    }
                }
            }
        }
    }

    public IEnumerable<string> GetCommandKeys() => _commands.Keys;


    // --- 内部实现 ---

    protected override void Awake()
    {
        base.Awake();
        _commands = new Dictionary<string, System.Action<string[]>>();
        RegisterCommands();
        consoleWindow.SetActive(false);
        closeButton.onClick.AddListener(ToggleConsole);

        // 也可以在这里强制设置，防止主人忘记在 Inspector 里改喵
        inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
    }

    private void OnEnable()
    {
        // 注意：多行模式下 onSubmit 行为会改变，我们主要靠键盘监听提交
        inputField.onSubmit.AddListener(ProcessCommand);
        Application.logMessageReceived += HandleUnityLog;
    }

    private void OnDisable()
    {
        inputField.onSubmit.RemoveListener(ProcessCommand);
        Application.logMessageReceived -= HandleUnityLog;
    }

    private void Update()
    {
        // 1. 波浪号开关
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            ToggleConsole();
        }

        // 2. 核心逻辑重写喵：
        if (consoleWindow.activeSelf && inputField.isFocused)
        {
            // 如果按下的是回车 (包括小键盘的回车)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // 情况 A: 按住了 Ctrl + Enter -> 插入换行符
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    int cursorPosition = inputField.caretPosition;
                    inputField.text = inputField.text.Insert(cursorPosition, "\n");
                    inputField.caretPosition = cursorPosition + 1; // 光标后移一位
                }
                // 情况 B: 只按了 Enter -> 立即执行指令喵！
                else
                {
                    ProcessCommand(inputField.text);

                    // 【关键】由于是多行模式，回车默认会加一个换行，
                    // 我们要在下一帧或者清空时确保它不会污染下次输入
                    inputField.text = "";
                }
            }
        }

        // 3. 延迟滚动到底部（避免在Log回调中强制刷新UI）
        if (_needScrollToBottom && scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
            _needScrollToBottom = false;
        }
    }

    private void ToggleConsole()
    {
        bool isActive = !consoleWindow.activeSelf;

        if (!isActive)
        {
            // 关闭控制台时清空所有日志记录
            _logBuilder.Clear();
            if (logText != null)
                logText.text = "";
        }

        consoleWindow.SetActive(isActive);
        if (isActive)
        {
            inputField.ActivateInputField();
        }
    }

    private void HandleUnityLog(string logString, string stackTrace, LogType type)
    {
        // 控制台关闭时不记录日志，避免TMP动态字体生成冲突
        if (!consoleWindow.activeSelf)
            return;

        var color = type switch
        {
            LogType.Error or LogType.Exception => Color.red,
            LogType.Warning => Color.yellow,
            _ => Color.white,
        };
        Log(logString, color);
    }

    public void ProcessCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        // 【核心修改】支持分号、换行符作为指令分隔符
        string[] commandQueue = input.Split(new[] { ';', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var commandLine in commandQueue)
        {
            string trimmedLine = commandLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

            Log($"> {trimmedLine}", Color.cyan);

            string[] parts = trimmedLine.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            string commandKey = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            if (_commands.TryGetValue(commandKey, out var commandAction))
            {
                try
                {
                    commandAction.Invoke(args);
                }
                catch (System.Exception e)
                {
                    Log($"Command '{commandKey}' failed: {e.Message}", Color.red);
                    Debug.LogException(e); // 同时发往 Unity Console 方便排查
                }
            }
            else
            {
                Log($"Unknown command: '{commandKey}'", Color.red);
            }
        }

        // 执行完清空输入框
        inputField.text = "";
        inputField.ActivateInputField();
    }

    private void RegisterCommands()
    {
        // 【重构后】所有命令统一从 CommandRegistry 自动注册喵~
        CommandRegistry.RegisterAll(this);
    }
}