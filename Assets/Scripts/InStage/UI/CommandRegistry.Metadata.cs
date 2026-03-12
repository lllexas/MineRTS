using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// CommandRegistry.Metadata - 命令注册和执行的元数据逻辑喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 使用反射自动扫描所有带 [CommandInfo] 特性的方法，
/// 并提供统一的注册和执行入口喵~
///
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public static partial class CommandRegistry
{
    // 命令执行委托喵~
    public delegate CommandResult CommandHandler(DeveloperConsole console, string[] args);

    // 命令处理器缓存喵~
    private static Dictionary<string, CommandHandler> _commandHandlers;
    private static Dictionary<string, CommandInfoAttribute> _commandMetadatas;

    // 是否已初始化喵~
    private static bool _isInitialized = false;

    /// <summary>
    /// 命令执行结果喵~
    /// </summary>
    public enum CommandResult
    {
        Success,
        Failed,
        Skipped,
        Pending
    }

    /// <summary>
    /// 初始化命令注册表（反射扫描）喵~
    /// </summary>
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        if (_isInitialized) return;

        _commandHandlers = new Dictionary<string, CommandHandler>();
        _commandMetadatas = new Dictionary<string, CommandInfoAttribute>();

        // 反射扫描所有带 [CommandInfo] 的静态方法
        var type = typeof(CommandRegistry);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<CommandInfoAttribute>();
            if (attr != null)
            {
                // 检查方法签名是否正确：(DeveloperConsole, string[])
                var parameters = method.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(DeveloperConsole) &&
                    parameters[1].ParameterType == typeof(string[]))
                {
                    // 创建委托
                    var handler = (CommandHandler)Delegate.CreateDelegate(typeof(CommandHandler), method);
                    _commandHandlers[attr.Name.ToLower()] = handler;
                    _commandMetadatas[attr.Name.ToLower()] = attr;

                    Debug.Log($"[CommandRegistry] 注册命令：{attr.Name} ({attr.DisplayName}) 喵~");
                }
                else
                {
                    Debug.LogWarning($"[CommandRegistry] 命令方法 {method.Name} 签名不正确，需要 (DeveloperConsole, string[]) 喵~");
                }
            }
        }

        _isInitialized = true;
        Debug.Log($"[CommandRegistry] 初始化完成，共注册 {_commandHandlers.Count} 个命令喵~");
    }

    /// <summary>
    /// 注册所有命令到 DeveloperConsole 喵~
    /// </summary>
    public static void RegisterAll(DeveloperConsole console)
    {
        Initialize();

        foreach (var kvp in _commandHandlers)
        {
            string commandName = kvp.Key;
            CommandHandler handler = kvp.Value;

            console.AddCommand(commandName, (args) =>
            {
                handler.Invoke(console, args);
            });
        }
    }

    /// <summary>
    /// 统一的命令执行入口喵~
    /// </summary>
    public static CommandResult Execute(string commandName, string[] args, DeveloperConsole console = null)
    {
        Initialize();

        if (string.IsNullOrEmpty(commandName))
        {
            return CommandResult.Skipped;
        }

        string key = commandName.ToLower();
        if (_commandHandlers.TryGetValue(key, out var handler))
        {
            try
            {
                return handler.Invoke(console, args);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CommandRegistry] 执行命令 {commandName} 失败：{e} 喵~");
                return CommandResult.Failed;
            }
        }

        Debug.LogWarning($"[CommandRegistry] 未知命令：{commandName} 喵~");
        return CommandResult.Failed;
    }

    /// <summary>
    /// 获取所有命令的元数据喵~
    /// </summary>
    public static Dictionary<string, CommandInfoAttribute> GetAllMetadatas()
    {
        Initialize();
        return new Dictionary<string, CommandInfoAttribute>(_commandMetadatas);
    }

    /// <summary>
    /// 获取单个命令的元数据喵~
    /// </summary>
    public static bool TryGetMetadata(string commandName, out CommandInfoAttribute metadata)
    {
        Initialize();
        return _commandMetadatas.TryGetValue(commandName.ToLower(), out metadata);
    }

    /// <summary>
    /// 获取所有命令名列表喵~
    /// </summary>
    public static List<string> GetAllCommandNames()
    {
        Initialize();
        return new List<string>(_commandMetadatas.Keys);
    }

    // =========================================================
    // 兼容旧版的注册方法（委托给 RegisterAll）喵~
    // =========================================================

    public static void RegisterEntityCommands(DeveloperConsole console) => RegisterAll(console);
    public static void RegisterSystemCommands(DeveloperConsole console) => RegisterAll(console);
    public static void RegisterTimeCommands(DeveloperConsole console) => RegisterAll(console);
    public static void RegisterCameraCommands(DeveloperConsole console) => RegisterAll(console);
}
