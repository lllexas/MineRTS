using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 命令执行器 - 运行时执行 CommandNode 中定义的命令喵~
///
/// 负责在剧情/任务流程中执行一系列预定义的命令。
/// 命令数据来自 CommandNodeData，执行逻辑来自 CommandRegistry。
/// </summary>
public class CommandExecutor : SingletonMono<CommandExecutor>
{
    // 命令队列喵~
    private Queue<CommandData> _commandQueue = new Queue<CommandData>();

    // 是否正在执行命令喵~
    private bool _isExecuting = false;

    // =========================================================
    // 核心 API
    // =========================================================

    /// <summary>
    /// 执行单个命令喵~
    /// </summary>
    public CommandRegistry.CommandResult ExecuteCommand(CommandData command)
    {
        if (string.IsNullOrEmpty(command.CommandName))
        {
            Debug.LogWarning("[CommandExecutor] 命令名为空，跳过执行喵~");
            return CommandRegistry.CommandResult.Skipped;
        }

        // 委托给 CommandRegistry 执行喵~
        var output = CommandRegistry.Execute(command.CommandName, command.Parameters?.ToArray() ?? new string[0], null, null);
        return output.Result;
    }

    /// <summary>
    /// 将命令加入队列喵~
    /// </summary>
    public void QueueCommand(CommandData command)
    {
        _commandQueue.Enqueue(command);

        if (!_isExecuting)
        {
            ProcessQueue();
        }
    }

    /// <summary>
    /// 批量添加命令到队列喵~
    /// </summary>
    public void QueueCommands(IEnumerable<CommandData> commands)
    {
        foreach (var command in commands)
        {
            _commandQueue.Enqueue(command);
        }

        if (!_isExecuting)
        {
            ProcessQueue();
        }
    }

    /// <summary>
    /// 清空命令队列喵~
    /// </summary>
    public void ClearQueue()
    {
        _commandQueue.Clear();
        _isExecuting = false;
    }

    // =========================================================
    // 辅助方法喵~
    // =========================================================

    /// <summary>
    /// 解析网格坐标喵~
    /// </summary>
    private Vector2Int ParseGridPos(string text)
    {
        string[] parts = text.Split(',');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0], out int x) &&
            int.TryParse(parts[1], out int y))
        {
            return new Vector2Int(x, y);
        }
        return Vector2Int.zero;
    }

    /// <summary>
    /// 处理命令队列喵~
    /// </summary>
    private async void ProcessQueue()
    {
        if (_isExecuting || _commandQueue.Count == 0) return;

        _isExecuting = true;

        while (_commandQueue.Count > 0)
        {
            var command = _commandQueue.Dequeue();
            ExecuteCommand(command);

            // 等待一帧，避免卡顿
            await System.Threading.Tasks.Task.Yield();
        }

        _isExecuting = false;
    }
}
