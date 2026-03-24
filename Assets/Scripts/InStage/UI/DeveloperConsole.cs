using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
public class DeveloperConsole : TUIManager, IConsoleController
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
    //  权限支持喵~
    // =========================================================

    /// <summary>
    /// 当前控制台的主体等级（默认 SystemMin，开发者工具）喵~
    /// </summary>
    private int _subjectLevel = PackAccessSubjects.SystemMin;

    /// <summary>
    /// 获取当前控制台的主体等级喵~
    /// </summary>
    public int GetSubjectLevel() => _subjectLevel;

    /// <summary>
    /// 设置当前控制台的主体等级喵~
    /// </summary>
    public void SetSubjectLevel(int level) => _subjectLevel = level;

    // =========================================================
    //  VFS 文件系统支持
    // =========================================================

    /// <summary>
    /// 运行时显式切换的盘符（null = 自动选择）喵~
    /// 优先级：_currentVFSPackID > GetPreferredPackID() > 第一个可用盘
    /// </summary>
    protected string _currentVFSPackID = null;

    /// <summary>
    /// 当前控制台使用的 VFS Pack ID（类比 CMD 的当前盘符）喵~
    /// </summary>
    public string CurrentVFSPackID
    {
        get
        {
            var analyser = GraphAnalyser.Instance;
            if (analyser == null) return null;

            // 1. 运行时显式切换的盘符优先喵~
            if (!string.IsNullOrEmpty(_currentVFSPackID) && analyser.GetPack(_currentVFSPackID, _subjectLevel) != null)
                return _currentVFSPackID;

            // 2. 子类首选盘喵~
            string preferred = GetPreferredPackID();
            if (!string.IsNullOrEmpty(preferred) && analyser.GetPack(preferred, _subjectLevel) != null)
                return preferred;

            // 3. 兜底：第一个挂载的盘喵~
            var ids = analyser.GetAllPackIds(_subjectLevel);
            return (ids != null && ids.Count > 0) ? ids[0] : null;
        }
    }

    /// <summary>
    /// 此控制台首选的 VFS 包 ID（盘符）喵~
    /// 基类返回 null（无首选，直接用第一个可用盘）；子类 override 声明偏好。
    /// </summary>
    protected virtual string GetPreferredPackID() => null;

    // =========================================================
    //  盘符映射 API 喵~
    // =========================================================

    /// <summary>
    /// 盘符映射：从 GraphAnalyser 中筛选非 Hidden 的 Pack，按插入顺序分配 A、B、C...
    /// </summary>
    public List<(char letter, string packID)> GetDriveMap()
    {
        var result = new List<(char, string)>();
        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return result;
        char letter = 'A';
        foreach (var id in analyser.GetAllPackIds(_subjectLevel))
        {
            var pack = analyser.GetPack(id, _subjectLevel);
            var accessLevel = GraphHub.Instance?.GetPackAccessLevel(GraphInstanceSlot.Player, pack)
                ?? analyser.GetPackAccessLevel(pack, _subjectLevel);
            if (pack != null && accessLevel != PackAccessLevel.Hidden)
            {
                result.Add((letter, id));
                letter++;
            }
        }
        return result;
    }

    // 缓存当前盘符字母，避免每帧重建 GetDriveMap 列表喵~
    private char? _cachedDriveLetter = null;

    /// <summary>当前盘符字母，null 表示无法映射（盘未挂载）喵~</summary>
    public char? CurrentDriveLetter => _cachedDriveLetter;

    /// <summary>重新计算并缓存当前盘符字母喵~（在包切换/VFS就绪时调用）</summary>
    private void RefreshDriveLetter()
    {
        var map = GetDriveMap();
        string cur = CurrentVFSPackID;
        for (int i = 0; i < map.Count; i++)
        {
            if (map[i].packID == cur) { _cachedDriveLetter = map[i].letter; return; }
        }
        _cachedDriveLetter = null;
    }

    /// <summary>
    /// 解析含盘符的路径 → (packID, 绝对vfsPath) 喵~
    /// "C:/messages/" → (C盘的packID, "/messages/")
    /// "messages/"    → (当前盘, 当前路径+messages/)
    /// </summary>
    public (string packID, string vfsPath) ResolveDrivePath(string input)
    {
        if (!string.IsNullOrEmpty(input) && input.Length >= 2
            && char.IsLetter(input[0]) && input[1] == ':')
        {
            int idx = char.ToUpper(input[0]) - 'A';
            var map = GetDriveMap();
            string vfsPath = input.Length > 2 ? input.Substring(2) : "/";
            if (!vfsPath.StartsWith("/")) vfsPath = "/" + vfsPath;
            if (idx >= 0 && idx < map.Count)
                return (map[idx].packID, VFSPathResolver.Normalize(vfsPath));
            return (null, vfsPath); // 未知盘符
        }
        string resolved = input != null && input.StartsWith("/")
            ? VFSPathResolver.Normalize(input)
            : VFSPathResolver.Combine(_currentPath, input ?? "");
        return (CurrentVFSPackID, resolved);
    }

    /// <summary>
    /// 切换当前盘符喵~ 传 null/空 则重置为自动选择。
    /// 切换成功后路径自动重置到 /。
    /// </summary>
    public bool SetCurrentPackID(string packID)
    {
        if (string.IsNullOrEmpty(packID))
        {
            _currentVFSPackID = null;
            _currentPath = "/";
            return true;
        }

        var analyser = GraphAnalyser.Instance;
        if (analyser == null)
        {
            Log("GraphAnalyser 实例不存在", Color.red);
            return false;
        }

        var pack = analyser.GetPack(packID, _subjectLevel);
        if (pack == null)
        {
            Log($"盘符不存在：{packID}", Color.red);
            return false;
        }
        PackAccessLevel accessLevel = GraphHub.Instance?.GetPackAccessLevel(GraphInstanceSlot.Player, pack)
            ?? analyser.GetPackAccessLevel(pack, _subjectLevel);
        if (accessLevel == PackAccessLevel.Hidden)
        {
            Log($"盘符不可访问：{packID}", Color.red);
            return false;
        }

        _currentVFSPackID = packID;
        _currentPath = "/";
        RefreshDriveLetter();
        return true;
    }

    /// <summary>当前 VFS 路径喵~</summary>
    protected string _currentPath = "/";
    public string CurrentPath => _currentPath;

    /// <summary>含盘符的完整路径，如 "A:/messages"；无盘符时降级为 CurrentPath 喵~</summary>
    public string FullCurrentPath
    {
        get
        {
            var letter = CurrentDriveLetter;
            if (!letter.HasValue) return CurrentPath;
            string p = CurrentPath.TrimEnd('/');
            return $"{letter}:{(string.IsNullOrEmpty(p) ? "/" : p)}";
        }
    }

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

        if (!analyser.PathExists(CurrentVFSPackID, path, _subjectLevel))
        {
            Log($"路径不存在：{path}", Color.red);
            return false;
        }

        var node = analyser.GetNode(CurrentVFSPackID, path, _subjectLevel);
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
        // SaveManager 已在发送此信号前完成所有挂盘喵~
        // 固定首选盘符（如果有）；否则清空让属性自动回退
        string preferred = GetPreferredPackID();
        _currentVFSPackID = (!string.IsNullOrEmpty(preferred) &&
                             GraphAnalyser.Instance?.GetPack(preferred, _subjectLevel) != null)
            ? preferred
            : null;

        if (CurrentVFSPackID != null)
            _currentPath = "/";

        RefreshDriveLetter();
    }

    // =========================================================
    //  策略系统（可接管控制台输入 + TUI 交互）
    // =========================================================
    private IConsoleInputHandler _currentInputHandler;
    private ICatStrategy _activeStrategy;
    private Func<int> _inputHandleLineProvider;
    private Action<int, int, IEnumerable<string>> _inputHandleRangeWriter;

    /// <summary>是否有活跃的交互策略喵~</summary>
    public bool HasActiveStrategy => _activeStrategy != null;
    public bool HasInputHandler => _currentInputHandler != null;
    public IConsoleInputHandler CurrentInputHandler => _currentInputHandler;
    public bool HasInputHandleHost => _inputHandleLineProvider != null && _inputHandleRangeWriter != null;
    public int InputHandleStartLine => _inputHandleLineProvider?.Invoke() ?? 0;

    /// <summary>
    /// 挂载输入处理器。挂载后，常规控制台输入将优先转发给它。
    /// </summary>
    public void MountInputHandler(IConsoleInputHandler handler)
    {
        if (handler == null) return;
        if (ReferenceEquals(_currentInputHandler, handler)) return;

        _currentInputHandler = handler;
    }

    /// <summary>
    /// 卸载输入处理器。若传入 null，则无条件卸载当前处理器。
    /// </summary>
    public void UnmountInputHandler(IConsoleInputHandler handler = null)
    {
        if (_currentInputHandler == null) return;
        if (handler != null && !ReferenceEquals(_currentInputHandler, handler)) return;

        // 先解除绑定，再清空处理器（状态机闭环）喵~
        UnbindInputHandleHost();
        
        _currentInputHandler = null;
    }

    public void BindInputHandleHost(Func<int> lineProvider, Action<int, int, IEnumerable<string>> rangeWriter)
    {
        _inputHandleLineProvider = lineProvider;
        _inputHandleRangeWriter = rangeWriter;
    }

    public void UnbindInputHandleHost(Func<int> lineProvider = null, Action<int, int, IEnumerable<string>> rangeWriter = null)
    {
        if (lineProvider != null && !ReferenceEquals(_inputHandleLineProvider, lineProvider)) return;
        if (rangeWriter != null && !ReferenceEquals(_inputHandleRangeWriter, rangeWriter)) return;

        _inputHandleLineProvider = null;
        _inputHandleRangeWriter = null;
    }

    public void WriteInputHandleRange(int index, int count, IEnumerable<string> lines)
    {
        _inputHandleRangeWriter?.Invoke(index, count, lines);
    }


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
            ICatStrategy strategy = _activeStrategy;
            _activeStrategy = null;
            strategy.Close();
            UnmountInputHandler();
        }
    }

    /// <summary>将上下箭头方向键转发给当前活跃策略喵~</summary>
    public void SendArrowKeyToStrategy(bool isUp) => _activeStrategy?.OnArrowKey(isUp);

    /// <summary>将回车确认转发给当前活跃策略喵~</summary>
    public void ConfirmStrategySelection() => _activeStrategy?.OnConfirm();

    /// <summary>触发清屏请求喵~</summary>
    public virtual void ClearConsole() => InvokeClearRequested();

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
        if (HasInputHandler && _currentInputHandler.HandleSubmit(input))
        {
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

        // 执行命令获取 CommandOutput（管道中的参数也要替换索引）
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
            // 【索引替换】执行命令前替换参数中的索引喵~
            args = ResolveIndexArguments(args);
            output = CommandRegistry.Execute(cmdName, _subjectLevel, args, null, this);
        }

        if (output == null || output.Result == CommandResult.Failed)
        {
            Log($"命令执行失败，重定向已终止喵：{output?.Message}", Color.red);
            return;
        }

        string content = output.Payload?.ToString() ?? output.Message ?? "";

        // 【索引替换】重定向路径也要替换索引喵~
        string resolvedRedirectPath = redirectPath;
        if (Regex.IsMatch(redirectPath, @"^\.\d+$"))
        {
            var analyserRedirect = GraphAnalyser.Instance;
            if (analyserRedirect != null)
            {
                var children = analyserRedirect.GetChildren(CurrentVFSPackID, CurrentPath, _subjectLevel);
                var validChildren = children.Where(c => c is VFSNodeData vfs && vfs.IsEnabled)
                                            .Cast<VFSNodeData>()
                                            .ToList();

                int index = int.Parse(redirectPath.Substring(1));
                if (index >= 0 && index < validChildren.Count)
                {
                    // 使用 Name + Extension 作为路径喵~（目录的 Extension 为空，不影响）
                    var node = validChildren[index];
                    resolvedRedirectPath = node.Name + node.Extension;
                }
                else
                {
                    Log($"重定向索引 {index} 超出范围 (0-{validChildren.Count - 1}) 喵~", Color.red);
                    return;
                }
            }
        }

        var (redirectPackID, fullPath) = ResolveDrivePath(resolvedRedirectPath);
        if (string.IsNullOrEmpty(redirectPackID))
        {
            Log($"重定向目标盘符不存在喵：{resolvedRedirectPath}", Color.red);
            return;
        }

        var analyser = GraphAnalyser.Instance;
        if (analyser == null) { Log("GraphAnalyser 未初始化喵！", Color.red); return; }

        if (isAppend)
        {
            var existing = analyser.GetNode(redirectPackID, fullPath, _subjectLevel);
            if (existing is VFSNodeData existingVfs)
                content = existingVfs.DataJson + "\n" + content;
        }

        if (analyser.WriteFile(redirectPackID, fullPath, content, _subjectLevel))
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

            // 【索引替换】管道中也要替换索引喵~
            args = ResolveIndexArguments(args);

            lastOutput = CommandRegistry.Execute(commandName, _subjectLevel, args, payload, this);
            payload = lastOutput.Payload;

            if (lastOutput.Result == CommandResult.Failed)
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

    // =========================================================
    //  索引解析支持喵~
    // =========================================================

    /// <summary>
    /// 替换参数中的索引为实际路径喵~
    /// 只有独立参数且完全匹配 ^\.\d+$ 才替换（如 .0, .1）
    /// 小数 (1.5)、后缀 (file.1)、路径 (/path/.1) 不会被替换喵~
    /// </summary>
    private string[] ResolveIndexArguments(string[] args)
    {
        if (string.IsNullOrEmpty(CurrentVFSPackID)) return args;

        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return args;

        // 获取当前目录的子节点列表
        var children = analyser.GetChildren(CurrentVFSPackID, CurrentPath, _subjectLevel);
        var validChildren = children.Where(c => c is VFSNodeData vfs && vfs.IsEnabled)
                                    .Cast<VFSNodeData>()
                                    .ToList();

        for (int i = 0; i < args.Length; i++)
        {
            // 只有独立参数且完全匹配 ^\.\d+$ 才替换
            if (Regex.IsMatch(args[i], @"^\.\d+$"))
            {
                int index = int.Parse(args[i].Substring(1));

                if (index >= 0 && index < validChildren.Count)
                {
                    // 使用 Name + Extension 作为路径喵~（目录的 Extension 为空，不影响）
                    var node = validChildren[index];
                    args[i] = node.Name + node.Extension;
                }
                else
                {
                    Log($"索引 {index} 超出范围 (0-{validChildren.Count - 1}) 喵~", Color.red);
                }
            }
            // 不匹配的保持原样：1.5, file.txt, /path/.1 等
        }

        return args;
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

        // 【索引替换】在执行前替换所有参数中的索引喵~
        args = ResolveIndexArguments(args);

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

            // 【索引替换】在管道中也要替换索引喵~
            args = ResolveIndexArguments(args);

            // 执行命令，传入上游的 payload
            var output = CommandRegistry.Execute(commandName, _subjectLevel, args, payload, this);

            // 将输出 Payload 传递给下游
            payload = output.Payload;

            // 如果失败，停止管道
            if (output.Result == CommandResult.Failed)
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

        // 兜底：若 VFS 系统已就绪（GraphAnalyser 已有挂盘），直接重置路径喵~
        if (GraphAnalyser.Instance != null && CurrentVFSPackID != null)
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
        // 从 NekoGraph CommandRegistry 全域扫描结果中注册所有命令到控制台喵~
        foreach (var name in CommandRegistry.GetAllCommandNames())
        {
            string captured = name;
            AddCommand(captured, (args) =>
            {
                var output = CommandRegistry.Execute(captured, GetSubjectLevel(), args, null, this);
                if (!string.IsNullOrEmpty(output.Message))
                    Log(output.Message, output.Result == CommandResult.Success ? UnityEngine.Color.green : UnityEngine.Color.red);
            });
        }
    }
}






