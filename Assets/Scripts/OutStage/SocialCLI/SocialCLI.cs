using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using CatStrategies;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// SocialCLI - 社交命令行安检员喵~ (GraphVSF 集成版)
/// ═══════════════════════════════════════════════════════════════
///
/// 继承自 DeveloperConsole，是"开发终端"在社交领域的特化形态喵！
///
/// 职责：
/// 1. 维护社交命令白名单（通过反射扫描 [SocialCommand] 标记）
/// 2. 安检：验证命令是否在白名单内
/// 3. 转发：通过安检的命令丢给基类 DeveloperConsole 处理
/// 4. 输出：重写 Log() 方法，输出到社交面板而不是主控制台
/// 5. 路径管理：使用 GraphAnalyser 管理文件树路径（CurrentPath）
///
/// 安全隔离：
/// - 只允许执行带 [SocialCommand] 标记的命令
/// - 无法通过社交 CLI 操作游戏进程
///
/// GraphVSF 集成：
/// - 使用 GraphAnalyser 单例管理 VFS 实例
/// - CurrentPath 从 GraphAnalyser 查询实时路径
/// - cd 命令通过 GraphAnalyser.GetNode() 验证路径
///
/// 初始化章法：
/// - [RuntimeInitializeOnLoadMethod] 作为静态入口，确保内存中第一时间建立白名单
/// - 严禁在 ExecuteCommand 里调用 Initialize()（初始化是系统级行为，执行是业务级行为）
/// - 内部拦截：if (_isInitialized) return; 确保只跑一次
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class SocialCLI : DeveloperConsole
{
    // =========================================================
    //  社交命令白名单
    // =========================================================
    private static HashSet<string> _socialCommands = new HashSet<string>();
    private static bool _isInitialized = false;

    // =========================================================
    //  隔离模式开关喵~
    // =========================================================
    /// <summary>
    /// 是否启用命令隔离（白名单检查）喵~
    /// true = 只允许执行白名单内的命令（默认安全模式）
    /// false = 解除隔离，可以执行任意命令（调试模式）
    /// </summary>
    public bool EnableCommandIsolation = true;

    // 策略系统方法已上移至 DeveloperConsole 基类喵~

    // =========================================================
    //  VFS 实例配置
    // =========================================================

    // CurrentPath / SetCurrentPath 已上移至基类喵~

    /// <summary>社交终端首选的 VFS 盘符喵~ 默认 social_tree_default，Inspector 可覆盖</summary>
    [Tooltip("首选 VFS 包 ID（盘符），不存在时自动回退到第一个可用盘")]
    [SerializeField] private string _preferredVFSPackID = "social_tree_default";

    protected override string GetPreferredPackID() => _preferredVFSPackID;

    // =========================================================
    //  初始化（反射扫描）
    // =========================================================
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        if (_isInitialized) return;

        _socialCommands.Clear();

        // 扫描 CommandRegistry 中所有带 [SocialCommand] 标记的静态方法
        var type = typeof(CommandRegistry);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        foreach (var method in methods)
        {
            // 检查是否有 [SocialCommand] 标记
            if (method.GetCustomAttribute<SocialCommandAttribute>() != null)
            {
                // 获取 [CommandInfo] 获取命令名
                var cmdAttr = method.GetCustomAttribute<CommandInfoAttribute>();
                if (cmdAttr != null)
                {
                    _socialCommands.Add(cmdAttr.Name.ToLower());
                    Debug.Log($"[SocialCLI] 注册社交命令：{cmdAttr.Name}");
                }
            }
        }

        _isInitialized = true;
        Debug.Log($"[SocialCLI] 初始化完成，共 {_socialCommands.Count} 个社交命令");
    }

    // =========================================================
    //  命令执行入口（安检员模式 + 重定向增强）
    // =========================================================

    /// <summary>
    /// 执行命令字符串（安检 → 重定向识别 → 转发）
    /// </summary>
    public override void ProcessCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        // 策略拦截已在基类 DeveloperConsole.ProcessCommand 处理，
        // 但 SocialCLI 完全重写了 ProcessCommand，需在此处调用基类处理喵~
        if (HasActiveStrategy)
        {
            base.ProcessCommand(input);
            return;
        }

        // 0.5 隔离模式检查喵~（如果关闭隔离，则跳过白名单检查）
        if (!EnableCommandIsolation)
        {
            // 解除隔离模式：直接转发给基类，不检查白名单
            base.ProcessCommand(input);
            return;
        }

        // 1. Tokenize & Validate：先做安检喵~
        var commandTokens = ExtractCommandTokens(input);
        foreach (var token in commandTokens)
        {
            if (!_socialCommands.Contains(token.ToLower()))
            {
                Log($"命令 '{token}' 不允许在社交终端执行喵~", Color.red);
                return;
            }
        }

        // 2. 安检通过，转发给基类（基类负责重定向解析和执行）喵~
        base.ProcessCommand(input);
    }

    /// <summary>
    /// 从输入字符串中提取所有命令名 Token
    /// </summary>
    private static List<string> ExtractCommandTokens(string input)
    {
        var tokens = new List<string>();

        // 支持分号、换行符作为指令分隔符
        string[] commandQueue = input.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var commandLine in commandQueue)
        {
            string trimmedLine = commandLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

            // 检查是否有管道符 |
            if (trimmedLine.Contains('|'))
            {
                // 管道模式：分割每个管道段
                string[] parts = trimmedLine.Split('|');
                foreach (var part in parts)
                {
                    string trimmed = part.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    string[] tokensInPart = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokensInPart.Length > 0)
                    {
                        tokens.Add(tokensInPart[0]);
                    }
                }
            }
            else
            {
                // 普通模式：直接取第一个词
                string[] parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    tokens.Add(parts[0]);
                }
            }
        }

        return tokens;
    }

    // =========================================================
    //  重写 Log 方法（输出到社交面板）喵~
    // =========================================================

    /// <summary>
    /// 重写基类的 Log，输出到社交面板而不是主控制台喵~
    /// 注意：不调用 base.Log()，直接发送 SocialCLI 专属事件
    /// </summary>
    public override void Log(string message, Color color)
    {
        Debug.Log(message);
        // 发送 SocialCLI 专属事件，与 DeveloperConsole 隔离（只发送一个事件，避免重复）
        PostSystem.Instance.Send("SocialCLI.Output", new DeveloperConsole.ConsoleOutputEvent { message = message, color = color });
    }

    /// <summary>请求面板将视口滚动到顶部喵~</summary>
    public override void ScrollConsoleToTop()
    {
        PostSystem.Instance.Send("SocialCLI.ScrollToTop", null);
    }

    // ==================== Unity 生命周期 ====================

    protected override void Awake()
    {
        base.Awake(); // 基类负责 PostSystem.Register 和 VFS 兜底初始化喵~
        Debug.Log("[SocialCLI] 终端就绪，正在等待 VFS 系统供电信号喵~");
    }

    // OnDestroy 已上移至基类（负责 PostSystem.Unregister）喵~
    // OnVFSSystemReady 已上移至基类（Subscribe VFS.IO_Ready）喵~
    // VFS 初始化由 SaveManager 在挂盘时统一完成喵~

    // =========================================================
    //  调试方法喵~
    // =========================================================

    /// <summary>
    /// 获取 VFS 调试信息喵~
    /// </summary>
    public string GetVFSDebugInfo()
    {
        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return "GraphAnalyser 实例不存在";

        if (analyser.GetPack(CurrentVFSPackID, PackAccessSubjects.Player) == null) return $"VFS Pack 不存在：{CurrentVFSPackID}";

        return analyser.GetDebugInfo();
    }
}
