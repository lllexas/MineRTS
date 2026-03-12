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
    // 命令执行委托喵~（新管道版本）
    public delegate CommandOutput CommandHandlerWithOutput(DeveloperConsole console, string[] args, object payload);

    // 命令执行委托喵~（旧版本，兼容用）
    public delegate CommandResult CommandHandler(DeveloperConsole console, string[] args);

    // 命令处理器缓存喵~
    private static Dictionary<string, CommandHandlerWithOutput> _commandHandlers;
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
    /// 命令输出 - 包含执行结果、日志消息和管道数据喵~
    /// </summary>
    public class CommandOutput
    {
        public CommandResult Result { get; set; }   // 执行结果
        public string Message { get; set; }         // 日志消息（给人看的）
        public object Payload { get; set; }         // 管道数据（给下游命令用的）

        public static CommandOutput Success(string message = null, object payload = null)
            => new CommandOutput { Result = CommandResult.Success, Message = message, Payload = payload };

        public static CommandOutput Fail(string error)
            => new CommandOutput { Result = CommandResult.Failed, Message = error, Payload = null };

        public static CommandOutput Skip()
            => new CommandOutput { Result = CommandResult.Skipped, Message = null, Payload = null };

        public static CommandOutput Pending()
            => new CommandOutput { Result = CommandResult.Pending, Message = null, Payload = null };
    }

    /// <summary>
    /// 初始化命令注册表（反射扫描）喵~
    /// </summary>
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        if (_isInitialized) return;

        _commandHandlers = new Dictionary<string, CommandHandlerWithOutput>();
        _commandMetadatas = new Dictionary<string, CommandInfoAttribute>();

        // 反射扫描所有带 [CommandInfo] 的静态方法
        var type = typeof(CommandRegistry);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<CommandInfoAttribute>();
            if (attr != null)
            {
                // 检查方法签名是否正确：(DeveloperConsole, string[], object) 返回 CommandOutput
                var parameters = method.GetParameters();
                if (parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(DeveloperConsole) &&
                    parameters[1].ParameterType == typeof(string[]) &&
                    parameters[2].ParameterType == typeof(object) &&
                    method.ReturnType == typeof(CommandOutput))
                {
                    // 创建委托
                    var handler = (CommandHandlerWithOutput)Delegate.CreateDelegate(typeof(CommandHandlerWithOutput), method);
                    _commandHandlers[attr.Name.ToLower()] = handler;

                    Debug.Log($"[CommandRegistry] 注册命令：{attr.Name} ({attr.DisplayName}) 喵~");
                }
                else
                {
                    Debug.LogWarning($"[CommandRegistry] 命令方法 {method.Name} 签名不正确，需要 (DeveloperConsole, string[], object) 返回 CommandOutput 喵~");
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
            CommandHandlerWithOutput handler = kvp.Value;

            console.AddCommand(commandName, (args) =>
            {
                var output = handler.Invoke(console, args, null);  // 控制台调用没有 payload
                // 如果有消息，打印到控制台
                if (!string.IsNullOrEmpty(output.Message))
                {
                    console?.Log(output.Message, output.Result == CommandResult.Success ? Color.green : Color.red);
                }
                // 不再返回 output.Result，因为 AddCommand 期望的是 void 返回
            });
        }
    }

    /// <summary>
    /// 统一的命令执行入口喵~（返回 CommandOutput）
    /// </summary>
    public static CommandOutput Execute(string commandName, string[] args, object payload = null, DeveloperConsole console = null)
    {
        Initialize();

        if (string.IsNullOrEmpty(commandName))
        {
            return CommandOutput.Skip();
        }

        string key = commandName.ToLower();
        if (_commandHandlers.TryGetValue(key, out var handler))
        {
            try
            {
                return handler.Invoke(console, args, payload);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CommandRegistry] 执行命令 {commandName} 失败：{e} 喵~");
                return CommandOutput.Fail($"执行命令 {commandName} 失败：{e.Message}");
            }
        }

        Debug.LogWarning($"[CommandRegistry] 未知命令：{commandName} 喵~");
        return CommandOutput.Fail($"未知命令：{commandName}");
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
