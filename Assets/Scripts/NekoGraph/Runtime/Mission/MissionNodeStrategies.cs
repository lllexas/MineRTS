using UnityEngine;
using System.Linq;

// =========================================================
// 任务节点策略 - MissionNode_A/S/F/R 处理器喵~
// =========================================================
//
// 【重构后】架构说明：
//
// 本文件只负责管理任务节点的状态，不处理任务逻辑！
// 任务逻辑由流程图中的 TriggerNode 和 CommandNode 处理。
//
// 流程图示例：
// ┌───────────┐    ┌────────────────┐    ┌───────────┐    ┌───────────┐
// │ Mission_A │───▶│ Trigger(建筑)  │───▶│ Command   │───▶│ Mission_S │
// │  激活任务  │    │ 监听"建筑完成"  │    │ 检查数量≥3 │    │  完成任务  │
// └───────────┘    └────────────────┘    └───────────┘    └───────────┘
//
// 职责划分：
// - MissionNode_A/S/F/R: 状态管理（激活/完成/失败/重置）
// - TriggerNode: 条件监听（监听事件、检查条件）
// - CommandNode: 执行动作（发放奖励、修改资源）
//
// =========================================================

/// <summary>
/// MissionNode_A 策略 - 任务发起点喵~
///
/// 职责：
/// 1. 收到信号 → 激活任务 (IsActive = true)
/// 2. 向后续节点传播信号（通常是 TriggerNode）
///
/// 注意：
/// - 不再监听事件！事件监听由 TriggerNode 负责
/// - 不再检查 Goals！进度由流程图中的 Trigger + Command 处理
/// </summary>
public class MissionNodeAStrategy : INodeStrategy
{
    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is not MissionNode_A_Data missionNode) return;

        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[MissionNode A] 任务激活：{missionNode.Title} (ID: {missionNode.MissionID})");
        }

        // 激活任务节点
        missionNode.IsActive = true;
        missionNode.IsCompleted = false;
        missionNode.IsFailed = false;

        // 发送 UI 刷新信号
        SendUIRefreshSignal();

        // 向输出节点传播信号（触发后续的 TriggerNode）
        PropagateSignal(missionNode, context, instance);
    }

    public void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance)
    {
        // 任务节点通常不直接响应外部事件
        // 事件监听由 TriggerNode 负责
    }

    /// <summary>
    /// 传播信号到输出节点喵~
    /// </summary>
    private void PropagateSignal(MissionNode_A_Data node, SignalContext context, RuntimeGraphInstance instance)
    {
        // 通过连接传播信号
        foreach (var conn in node.OutputConnections)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = conn.TargetNodeID;
            instance.InjectSignal(newSignal);
        }

        // 兼容旧版 OutPutNodeIDs 字段
        foreach (var nextId in node.OutPutNodeIDs)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = nextId;
            instance.InjectSignal(newSignal);
        }
    }

    /// <summary>
    /// 发送 UI 刷新信号喵~
    /// </summary>
    private void SendUIRefreshSignal()
    {
        PostSystem.Instance.Send("UI_MISSION_REFRESH", null);
    }
}

/// <summary>
/// MissionNode_S 策略 - 任务成功点喵~
///
/// 职责：
/// 1. 收到信号 → 标记任务完成 (IsCompleted = true)
/// 2. 发送 UI 完成信号
/// 3. 向后续节点传播信号（可能是奖励 CommandNode）
///
/// 注意：
/// - 不再查找 MissionNode_A！完成状态由流程图的信号传播决定
/// - 不直接发放奖励！奖励由后续的 CommandNode 处理
/// </summary>
public class MissionNodeSStrategy : INodeStrategy
{
    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is not MissionNode_S_Data missionNode) return;

        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[MissionNode S] 成功节点触发：{missionNode.NodeID} (MissionID: {missionNode.MissionID})");
        }

        // 找到对应的 MissionNode_A 并标记为完成
        var missionA = instance.GetNodesOfType<MissionNode_A_Data>()
            .FirstOrDefault(m => m.MissionID == missionNode.MissionID);

        if (missionA != null)
        {
            missionA.IsCompleted = true;
            missionA.IsActive = false;

            if (GraphRunner.Instance.EnableDebugLog)
            {
                Debug.Log($"[MissionNode S] 任务完成：{missionA.Title}");
            }

            // 发送 UI 完成信号
            SendMissionCompleteSignal(missionA);
        }

        // 向输出节点传播信号（可能是奖励 CommandNode）
        PropagateSignal(missionNode, context, instance);
    }

    public void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance)
    {
        // 任务节点通常不直接响应外部事件
        // 事件监听由 TriggerNode 负责
    }

    /// <summary>
    /// 传播信号到输出节点喵~
    /// </summary>
    private void PropagateSignal(MissionNode_S_Data node, SignalContext context, RuntimeGraphInstance instance)
    {
        // 通过连接传播信号
        foreach (var conn in node.OutputConnections)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = conn.TargetNodeID;
            instance.InjectSignal(newSignal);
        }

        // 兼容旧版 OutPutNodeIDs 字段
        foreach (var nextId in node.OutPutNodeIDs)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = nextId;
            instance.InjectSignal(newSignal);
        }
    }

    /// <summary>
    /// 发送任务完成信号喵~
    /// </summary>
    private void SendMissionCompleteSignal(MissionNode_A_Data mission)
    {
        PostSystem.Instance.Send("UI_MISSION_COMPLETE", mission);
    }
}

/// <summary>
/// MissionNode_F 策略 - 任务失败点喵~
///
/// 职责：
/// 1. 收到信号 → 标记任务失败 (IsFailed = true)
/// 2. 发送 UI 失败信号
/// 3. 向后续节点传播信号（可能是惩罚 CommandNode 或剧情分支）
/// </summary>
public class MissionNodeFStrategy : INodeStrategy
{
    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is not MissionNode_F_Data missionNode) return;

        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[MissionNode F] 失败节点触发：{missionNode.NodeID} (MissionID: {missionNode.MissionID})");
        }

        // 找到对应的 MissionNode_A 并标记为失败
        var missionA = instance.GetNodesOfType<MissionNode_A_Data>()
            .FirstOrDefault(m => m.MissionID == missionNode.MissionID);

        if (missionA != null)
        {
            missionA.IsFailed = true;
            missionA.IsActive = false;

            if (GraphRunner.Instance.EnableDebugLog)
            {
                Debug.Log($"[MissionNode F] 任务失败：{missionA.Title}");
            }

            // 发送 UI 失败信号
            SendMissionFailedSignal(missionA);
        }

        // 向输出节点传播信号
        PropagateSignal(missionNode, context, instance);
    }

    public void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance)
    {
        // 任务节点通常不直接响应外部事件
        // 事件监听由 TriggerNode 负责
    }

    /// <summary>
    /// 传播信号到输出节点喵~
    /// </summary>
    private void PropagateSignal(MissionNode_F_Data node, SignalContext context, RuntimeGraphInstance instance)
    {
        // 通过连接传播信号
        foreach (var conn in node.OutputConnections)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = conn.TargetNodeID;
            instance.InjectSignal(newSignal);
        }

        // 兼容旧版 OutPutNodeIDs 字段
        foreach (var nextId in node.OutPutNodeIDs)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = nextId;
            instance.InjectSignal(newSignal);
        }
    }

    /// <summary>
    /// 发送任务失败信号喵~
    /// </summary>
    private void SendMissionFailedSignal(MissionNode_A_Data mission)
    {
        PostSystem.Instance.Send("UI_MISSION_FAILED", mission);
    }
}

/// <summary>
/// MissionNode_R 策略 - 任务刷新点喵~
///
/// 职责：
/// 1. 收到信号 → 重置任务状态
/// 2. 可选：重置进度（如果需要）
/// 3. 发送 UI 刷新信号
/// 4. 向后续节点传播信号
/// </summary>
public class MissionNodeRStrategy : INodeStrategy
{
    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is not MissionNode_R_Data missionNode) return;

        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[MissionNode R] 刷新节点触发：{missionNode.NodeID} (MissionID: {missionNode.MissionID})");
        }

        // 找到对应的 MissionNode_A 并重置状态
        var missionA = instance.GetNodesOfType<MissionNode_A_Data>()
            .FirstOrDefault(m => m.MissionID == missionNode.MissionID);

        if (missionA != null)
        {
            // 重置进度（如果配置了）
            if (missionNode.ResetProgress)
            {
                // 注意：这里不再重置 Goals，因为进度由流程图管理
                // 如果需要重置，应该通过 CommandNode 来执行
                Debug.LogWarning("[MissionNode R] ResetProgress 已废弃，请使用 CommandNode 重置进度喵~");
            }

            // 重新激活任务
            if (missionNode.Reactivate)
            {
                missionA.IsActive = true;
                missionA.IsCompleted = false;
                missionA.IsFailed = false;

                if (GraphRunner.Instance.EnableDebugLog)
                {
                    Debug.Log($"[MissionNode R] 任务已重新激活：{missionA.Title}");
                }
            }

            // 发送 UI 刷新信号
            SendUIRefreshSignal();
        }

        // 向输出节点传播信号
        PropagateSignal(missionNode, context, instance);
    }

    public void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance)
    {
        // 任务节点通常不直接响应外部事件
        // 事件监听由 TriggerNode 负责
    }

    /// <summary>
    /// 传播信号到输出节点喵~
    /// </summary>
    private void PropagateSignal(MissionNode_R_Data node, SignalContext context, RuntimeGraphInstance instance)
    {
        // 通过连接传播信号
        foreach (var conn in node.OutputConnections)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = conn.TargetNodeID;
            instance.InjectSignal(newSignal);
        }

        // 兼容旧版 OutPutNodeIDs 字段
        foreach (var nextId in node.OutPutNodeIDs)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = nextId;
            instance.InjectSignal(newSignal);
        }
    }

    /// <summary>
    /// 发送 UI 刷新信号喵~
    /// </summary>
    private void SendUIRefreshSignal()
    {
        PostSystem.Instance.Send("UI_MISSION_REFRESH", null);
    }
}
