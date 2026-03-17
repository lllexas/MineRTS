using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    /// 处理命令字符串（支持分号和管道）
    /// </summary>
    public virtual void ProcessCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        // 支持分号、换行符作为指令分隔符
        string[] commandQueue = input.Split(new[] { ';', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var commandLine in commandQueue)
        {
            string trimmedLine = commandLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

            // 检查是否有管道符
            if (trimmedLine.Contains('|'))
            {
                ExecutePipeline(trimmedLine);
            }
            else
            {
                ExecuteSingleCommand(trimmedLine);
            }
        }
    }

    // =========================================================
    //  命令执行内部方法
    // =========================================================

    /// <summary>
    /// 执行单个命令
    /// </summary>
    private void ExecuteSingleCommand(string input)
    {
        Log($"> {input}", Color.cyan);

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
        Log($"> {input}", Color.cyan);

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
