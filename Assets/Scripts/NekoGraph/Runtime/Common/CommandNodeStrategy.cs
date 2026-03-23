using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// 命令节点策略喵~
// =========================================================

/// <summary>
/// CommandNode 策略 - 命令执行节点喵~
/// 负责执行各种游戏命令（如召唤单位、发放奖励、修改资源等）
///
/// 【重构后】直接使用 CommandRegistry.Execute() 统一入口喵~
/// </summary>
public class CommandNodeStrategy : NodeStrategy
{
    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not CommandNodeData commandNode) return;

        if (runner.EnableDebugLog)
        {
            Debug.Log($"[CommandNode] 执行命令：{commandNode.Command.CommandName}");
        }

        // 执行命令
        ExecuteCommand(commandNode, context, pack);

        // 向输出节点传播信号
        PropagateSignal(commandNode, context, pack);
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        // 命令节点通常不直接响应外部事件
    }

    /// <summary>
    /// 执行命令喵~
    /// </summary>
    private void ExecuteCommand(CommandNodeData node, SignalContext context, BasePackData pack)
    {
        var command = node.Command;

        if (string.IsNullOrEmpty(command.CommandName))
        {
            Debug.LogWarning("[CommandNode] 命令名为空，跳过执行喵~");
            return;
        }

        // 同步参数
        command.SyncParameters();

        // 构建命令参数
        var args = BuildCommandArgs(command, context);

        // 【管道重构后】直接调用 CommandRegistry 统一入口喵~
        try
        {
            // 从 GraphRunner 获取当前执行主体的权限喵~
            int subjectLevel = GraphRunner.Instance?.GetSubjectLevel() ?? PackAccessSubjects.Player;
            var output = CommandRegistry.Execute(command.CommandName, subjectLevel, args, context.Args, null);

            // 将命令输出的 Payload 传递给下游节点
            if (output.Payload != null)
            {
                context.Args = output.Payload;
            }

            // 如果有日志消息，打印出来
            if (!string.IsNullOrEmpty(output.Message))
            {
                if (output.Result == CommandRegistry.CommandResult.Failed)
                    Debug.LogError($"[CommandNode] {output.Message}");
                else if (GraphRunner.Instance.EnableDebugLog)
                    Debug.Log($"[CommandNode] {output.Message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CommandNode] 命令执行失败：{command.CommandName}, 错误：{e.Message}");
        }
    }

    /// <summary>
    /// 构建命令参数喵~
    /// </summary>
    private string[] BuildCommandArgs(CommandData command, SignalContext context)
    {
        var argsList = new List<string>();

        // 添加预定义的参数
        if (!string.IsNullOrEmpty(command.Parameter))
        {
            argsList.Add(command.Parameter);
        }

        // 从信号上下文中提取额外参数
        if (context.Args != null)
        {
            if (context.Args is string strData)
            {
                argsList.Add(strData);
            }
            else if (context.Args is MissionArgs args)
            {
                if (!string.IsNullOrEmpty(args.StringKey))
                    argsList.Add(args.StringKey);
                if (args.Amount > 0)
                    argsList.Add(args.Amount.ToString());
            }
        }

        return argsList.ToArray();
    }

    /// <summary>
    /// 传播信号到输出节点喵~
    /// </summary>
    private void PropagateSignal(CommandNodeData node, SignalContext context, BasePackData pack)
    {
        EnqueueSignals(pack, node.OutputConnections, context);
        EnqueueSignals(pack, node.OutputNodeIDs, context);
    }
}
