using System;
using System.Collections.Generic;
using System.Linq;
using NekoGraph;
using CatStrategies;
using UnityEngine;
using static CommandRegistry;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// DeveloperConsole - 开发者控制台逻辑层
/// ═══════════════════════════════════════════════════════════════
///
/// 设计理念：
/// 1. UI 与逻辑分离 - 不持有任何 UI 引用
/// 2. 事件驱动输出 - 通过 PostSystem 发送输出事件
/// 3. 命令注册管理 - 统一从 CommandRegistry 自动注册
/// 4. 支持管道和分号 - 命令组合功能
///
/// 继承关系：
///   DeveloperConsole : SingletonMono<DeveloperConsole>
///   ↑
///   └─ SocialCLI : DeveloperConsole (社交终端特化)
///
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class DeveloperConsole : MonoBehaviour
{
    // =========================================================
    //  输出事件数据结构
    // =========================================================
    public class ConsoleOutputEvent
    {
        public string message;
        public Color color;
    }

    // =========================================================
    //  命令注册表
    // =========================================================
    private Dictionary<string, System.Action<string[]>> _commands;

    public bool EnableUnityLogging = false;

    // =========================================================
    //  VFS 文件系统支持
    // =========================================================

    /// <summary>
    /// 当前控制台使用的 VFS Pack ID（动态检索，类比 CMD 的当前盘符）喵~
    /// 优先返回 GetPreferredPackID()，不存在则回退到 GraphAnalyser 中第一个可用实例。
    /// </summary>
    public string CurrentVFSPackID
    {
        get
        {
            var analyser = GraphAnalyser.Instance;
            if (analyser == null) return null;

            // 尝试首选盘喵~
            string preferred = GetPreferredPackID();
            if (!string.IsNullOrEmpty(preferred) && analyser.GetInstance(preferred) != null)
                return preferred;

            // 回退：第一个挂载的盘（类似 Unix 的 /）喵~
            var ids = analyser.GetAllInstanceIds();
            return (ids != null && ids.Count > 0) ? ids[0] : null;
        }
    }

    /// <summary>
    /// 此控制台首选的 VFS 包 ID（盘符）喵~
    /// 基类返回 null（无首选，直接用第一个可用盘）；子类 override 声明偏好。
    /// </summary>
    protected virtual string GetPreferredPackID() => null;

    /// <summary>当前 VFS 路径喵~</summary>
    protected string _currentPath = "/";
    public string CurrentPath => _currentPath;

    /// <summary>设置当前路径喵~</summary>
    public bool SetCurrentPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Log("路径不能为空", Color.red);
            return false;
        }

        if (string.IsNullOrEmpty(CurrentVFSPackID))
        {
            Log("未挂载文件系统喵！", Color.yellow);
            return false;
        }

        var analyser = GraphAnalyser.Instance;
        if (analyser == null)
        {
            Log("GraphAnalyser 实例不存在", Color.red);
            return false;
        }

        if (!analyser.PathExists(CurrentVFSPackID, path))
        {
            Log($"路径不存在：{path}", Color.red);
            return false;
        }

        var node = analyser.GetNode(CurrentVFSPackID, path);
        if (node is VFSNodeData vfs && !vfs.IsDirectory)
        {
            Log($"不是目录：{path}", Color.red);
            return false;
        }

        _currentPath = path;
        Log($"路径已切换到：{_currentPath}", Color.green);
        return true;
    }

    // =========================================================
    //  VFS 就绪信号处理
    // =========================================================

    [Subscribe("VFS.IO_Ready")]
    private void OnVFSSystemReady(object data)
    {
        // PersistentVFSManager 已在发送此信号前完成所有 EnsureVFS 喵~
        // Console 只需重置当前路径
        if (CurrentVFSPackID != null)
            _currentPath = "/";
    }

    // =========================================================
    //  策略系统（可接管控制台输入 + TUI 交互）
    // =========================================================
    private ICatStrategy _activeStrategy;

    /// <summary>是否有活跃的交互策略喵~</summary>
    public bool HasActiveStrategy => _activeStrategy != null;

    /// <summary>终端列宽（由面板层在 Start 时注入）喵~</summary>
    public int ConsoleWidth { get; set; } = 52;


/// <summary>设置并启动一个新的 cat 策略喵~</summary>
    public void SetActiveStrategy(ICatStrategy strategy, string vfsPath, string graphPath = null)
    {
        CloseActiveStrategy();
        _activeStrategy = strategy;
        _activeStrategy.Execute(vfsPath, graphPath);
    }

    /// <summary>关闭当前正在运行的策略喵~</summary>
    public void CloseActiveStrategy()
    {
        if (_activeStrategy != null)
        {
            _activeStrategy.Close();
            _activeStrategy = null;
        }
    }

    /// <summary>将上下箭头方向键转发给当前活跃策略喵~</summary>
    public void SendArrowKeyToStrategy(bool isUp) => _activeStrategy?.OnArrowKey(isUp);

    /// <summary>将回车确认转发给当前活跃策略喵~</summary>
    public void ConfirmStrategySelection() => _activeStrategy?.OnConfirm();

    /// <summary>
    /// 请求清空控制台输出喵~
    /// 面板层订阅此事件后调用 ClearLog()
    /// </summary>
    public event Action OnClearRequested;

    /// <summary>触发清屏请求喵~</summary>
    public virtual void ClearConsole() => OnClearRequested?.Invoke();

    /// <summary>请求将控制台视口滚动到顶部喵~（子类可重写）</summary>
    public virtual void ScrollConsoleToTop() { }  // 基类默认空实现

    // =========================================================
    //  公共接口 (API)
    // =========================================================

    /// <summary>
    /// 注册命令
    /// </summary>
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

    /// <summary>
    /// 获取所有命令键
    /// </summary>
    public IEnumerable<string> GetCommandKeys() => _commands.Keys;

    // =========================================================
    //  受保护的触发器（子类专用通道）
    // =========================================================

    /// <summary>
    /// 发射输出信号（受保护方法，仅供子类调用）喵~
    /// </summary>
    protected void FireOutputEvents(string message, Color color)
    {
        // 只发送一个事件，避免重复处理喵~
        PostSystem.Instance.Send("DeveloperConsole.Output", new ConsoleOutputEvent { message = message, color = color });
    }

    /// <summary>
    /// 输出日志（事件驱动，不直接操作 UI）
    /// </summary>
    public virtual void Log(string message, Color color)
    {
        FireOutputEvents(message, color);
    }

    // =========================================================
    //  命令执行入口
    // =========================================================

    /// <summary>
    /// 处理命令字符串（支持分号、管道和重定向）
    /// </summary>
    public virtual void ProcessCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        // 策略拦截：有活跃策略时直接转发，不走命令系统喵~
        if (_activeStrategy != null)
        {
            _activeStrategy.OnInput(input);
            return;
        }

        // 支持分号、换行符作为指令分隔符
        string[] commandQueue = input.Split(new[] { ';', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var commandLine in commandQueue)
        {
            string trimmedLine = commandLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

            // 解析重定向符号（>> 或 >）
            string execLine = ParseRedirection(trimmedLine, out string redirectPath, out bool isAppend);

            if (redirectPath != null)
            {
                ExecuteWithRedirection(execLine, redirectPath, isAppend);
            }
            else if (trimmedLine.Contains('|'))
            {
                ExecutePipeline(trimmedLine);
            }
            else
            {
                ExecuteSingleCommand(trimmedLine);
            }
        }
    }

    /// <summary>
    /// 从命令行中解析重定向部分（从右往左查找，避免参数中的 > 误判）喵~
    /// 返回去掉重定向部分的执行命令；redirectPath 为 null 表示无重定向
    /// </summary>
    private string ParseRedirection(string line, out string redirectPath, out bool isAppend)
    {
        redirectPath = null;
        isAppend = false;

        // 先找 >>（追加），避免与单 > 混淆
        int appendIdx = line.LastIndexOf(">>");
        if (appendIdx >= 0)
        {
            isAppend = true;
            redirectPath = line.Substring(appendIdx + 2).Trim();
            return line.Substring(0, appendIdx).Trim();
        }

        // 再找单独的 >（覆写）
        int writeIdx = line.LastIndexOf('>');
        if (writeIdx >= 0)
        {
            isAppend = false;
            redirectPath = line.Substring(writeIdx + 1).Trim();
            return line.Substring(0, writeIdx).Trim();
        }

        return line;
    }

    /// <summary>
    /// 执行命令并将输出重定向写入 VFS 文件喵~
    /// </summary>
    private void ExecuteWithRedirection(string execLine, string redirectPath, bool isAppend)
    {
        if (string.IsNullOrEmpty(CurrentVFSPackID))
        {
            Log("未挂载文件系统，无法使用重定向喵！", Color.yellow);
            return;
        }

        // 执行命令获取 CommandOutput
        CommandOutput output;
        if (execLine.Contains('|'))
        {
            output = ExecutePipelineGetOutput(execLine);
        }
        else
        {
            string[] parts = execLine.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            string cmdName = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();
            output = CommandRegistry.Execute(cmdName, args, null, this);
        }

        if (output == null || output.Result == CommandRegistry.CommandResult.Failed)
        {
            Log($"命令执行失败，重定向已终止喵：{output?.Message}", Color.red);
            return;
        }

        string content = output.Payload?.ToString() ?? output.Message ?? "";
        string fullPath = redirectPath.StartsWith("/") ? redirectPath : VFSPathResolver.Combine(_currentPath, redirectPath);

        var analyser = GraphAnalyser.Instance;
        if (analyser == null) { Log("GraphAnalyser 未初始化喵！", Color.red); return; }

        if (isAppend)
        {
            var existing = analyser.GetNode(CurrentVFSPackID, fullPath);
            if (existing is VFSNodeData existingVfs)
                content = existingVfs.DataJson + "\n" + content;
        }

        if (analyser.WriteFile(CurrentVFSPackID, fullPath, content))
            Log($"内容已{(isAppend ? "追加" : "写入")}到：{fullPath}", Color.green);
        else
            Log($"写入失败，请检查路径是否正确喵：{fullPath}", Color.red);
    }

    /// <summary>
    /// 执行管道命令并返回最终 CommandOutput（供重定向使用）喵~
    /// </summary>
    private CommandOutput ExecutePipelineGetOutput(string input)
    {
        string[] parts = input.Split('|');
        object payload = null;
        CommandOutput lastOutput = CommandOutput.Fail("空管道");

        foreach (var part in parts)
        {
            string trimmed = part.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            string[] tokens = trimmed.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;

            string commandName = tokens[0].ToLower();
            string[] args = tokens.Skip(1).ToArray();

            lastOutput = CommandRegistry.Execute(commandName, args, payload, this);
            payload = lastOutput.Payload;

            if (lastOutput.Result == CommandRegistry.CommandResult.Failed)
            {
                Log($"Pipeline failed at '{commandName}': {lastOutput.Message}", Color.red);
                break;
            }
        }

        return lastOutput;
    }

    // =========================================================
    //  命令执行内部方法
    // =========================================================

    /// <summary>
    /// 执行单个命令
    /// </summary>
    private void ExecuteSingleCommand(string input)
    {

        string[] parts = input.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

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
                Debug.LogException(e);
            }
        }
        else
        {
            Debug.Log($"Unknown command: '{commandKey}'");
            Log($"Unknown command: '{commandKey}'", Color.red);
        }
    }

    /// <summary>
    /// 执行管道命令
    /// </summary>
    private void ExecutePipeline(string input)
    {

        string[] parts = input.Split('|');
        object payload = null;

        foreach (var part in parts)
        {
            string trimmed = part.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            string[] tokens = trimmed.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;

            string commandName = tokens[0].ToLower();
            string[] args = tokens.Skip(1).ToArray();

            // 执行命令，传入上游的 payload
            var output = CommandRegistry.Execute(commandName, args, payload, this);

            // 将输出 Payload 传递给下游
            payload = output.Payload;

            // 如果失败，停止管道
            if (output.Result == CommandRegistry.CommandResult.Failed)
            {
                Log($"Pipeline failed at '{commandName}': {output.Message}", Color.red);
                break;
            }

            // 成功则继续
            if (GraphRunner.Instance != null && GraphRunner.Instance.EnableDebugLog)
            {
                Log($"Pipeline: {commandName} → Payload: {(payload != null ? payload.GetType().Name : "null")}", Color.gray);
            }
        }
    }

    // =========================================================
    //  Unity 生命周期
    // =========================================================

    protected virtual void Awake()
    {
        _commands = new Dictionary<string, System.Action<string[]>>();
        RegisterCommands();
        PostSystem.Instance.Register(this);

        // 兜底：若 VFS 系统已就绪（PersistentVFSManager 已挂盘），直接重置路径喵~
        if (PersistentVFSManager.Instance != null &&
            PersistentVFSManager.Instance.IsReady &&
            CurrentVFSPackID != null)
        {
            _currentPath = "/";
        }
    }

    protected virtual void OnDestroy()
    {
        if (PostSystem.Instance != null)
            PostSystem.Instance.Unregister(this);
    }

    private void OnEnable()
    {
        if (Application.isEditor && EnableUnityLogging)
        {
            // 编辑器模式下可以捕获 Unity 日志
            Application.logMessageReceived += HandleUnityLog;
        }
    }

    private void OnDisable()
    {
        if (Application.isEditor && EnableUnityLogging)
        {
            Application.logMessageReceived -= HandleUnityLog;
        }
    }

    /// <summary>
    /// 处理 Unity 日志（可选）
    /// </summary>
    private void HandleUnityLog(string logString, string stackTrace, LogType type)
    {
        var color = type switch
        {
            LogType.Error or LogType.Exception => Color.red,
            LogType.Warning => Color.yellow,
            _ => Color.white,
        };
        Log(logString, color);
    }

    /// <summary>
    /// 注册命令（从 CommandRegistry 自动注册）
    /// </summary>
    private void RegisterCommands()
    {
        CommandRegistry.RegisterAll(this);
    }
}
