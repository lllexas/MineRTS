using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// 基础流程节点策略 - Root/Spine/Leaf 节点处理器喵~
// =========================================================

/// <summary>
/// Root 节点策略 - 流程的起始锚点喵~
/// </summary>
public class RootNodeStrategy : INodeStrategy
{
    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is not RootNodeData rootNode) return;

        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[RootNode] 流程启动：{rootNode.NodeID}");
        }

        // 向所有输出节点传播信号
        PropagateSignal(rootNode, context, instance);
    }

    public void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance)
    {
        // Root 节点不响应外部事件
    }

    private void PropagateSignal(RootNodeData node, SignalContext context, RuntimeGraphInstance instance)
    {
        foreach (var conn in node.OutputConnections)
        {
            var newSignal = context.Clone();
            newSignal.SourceNodeId = conn.TargetNodeID;
            instance.InjectSignal(newSignal);
        }
    }
}

/// <summary>
/// Spine 节点策略 - 流程的逻辑骨架/无线输电继电器喵~
/// </summary>
public class SpineNodeStrategy : INodeStrategy
{
    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is not SpineNodeData spineNode) return;

        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[SpineNode] 信号中继：{spineNode.NodeID} (ProcessID: {spineNode.ProcessID})");
        }

        // 1. 激活关联的 Leaf A 节点
        ActivateLeafNodes(spineNode, context, instance);

        // 2. 向下一个 Spine 节点传播信号
        PropagateToNextSpine(spineNode, context, instance);
    }

    public void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance)
    {
        if (data is not SpineNodeData spineNode) return;

        // Spine 节点可以通过 Leaf B 节点的回调来响应事件
        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[SpineNode] 收到事件：{eventName} -> {spineNode.NodeID}");
        }
    }

    private void ActivateLeafNodes(SpineNodeData node, SignalContext context, RuntimeGraphInstance instance)
    {
        // 查找所有与当前 Spine 节点共享 ProcessID 的 Leaf A 节点
        var leafNodes = instance.GetNodesOfType<LeafNode_A_Data>();
        foreach (var leaf in leafNodes)
        {
            if (leaf.ProcessID == node.ProcessID)
            {
                if (GraphRunner.Instance.EnableDebugLog)
                {
                    Debug.Log($"[SpineNode] 激活 Leaf A: {leaf.NodeID} (ProcessID: {leaf.ProcessID})");
                }

                // 向 Leaf A 节点发送信号
                var newSignal = context.Clone();
                newSignal.SourceNodeId = leaf.NodeID;
                instance.InjectSignal(newSignal);
            }
        }
    }

    private void PropagateToNextSpine(SpineNodeData node, SignalContext context, RuntimeGraphInstance instance)
    {
        // 通过 OutputConnections 或 NextSpineNodeIDs 传播
        foreach (var conn in node.OutputConnections)
        {
            var newSignal = context.Clone();
            newSignal.SourceNodeId = conn.TargetNodeID;
            instance.InjectSignal(newSignal);
        }

        // 兼容旧版 NextSpineNodeIDs 字段
        foreach (var nextId in node.NextSpineNodeIDs)
        {
            var newSignal = context.Clone();
            newSignal.SourceNodeId = nextId;
            instance.InjectSignal(newSignal);
        }
    }
}

/// <summary>
/// Leaf A 节点策略 - 处理具体的执行演出喵~
/// </summary>
public class LeafNodeAStrategy : INodeStrategy
{
    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is not LeafNode_A_Data leafNode) return;

        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[LeafNode A] 执行演出：{leafNode.NodeID} (ProcessID: {leafNode.ProcessID})");
        }

        // 向输出节点传播信号（通常是执行具体动作）
        foreach (var conn in leafNode.OutputConnections)
        {
            var newSignal = context.Clone();
            newSignal.SourceNodeId = conn.TargetNodeID;
            instance.InjectSignal(newSignal);
        }

        // 同时通知对应的 Leaf B 节点（通过 Spine 节点中转）
        NotifyLeafB(leafNode, context, instance);
    }

    public void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance)
    {
        // Leaf A 节点通常不直接响应外部事件
    }

    private void NotifyLeafB(LeafNode_A_Data node, SignalContext context, RuntimeGraphInstance instance)
    {
        // 查找对应的 Leaf B 节点（共享 ProcessID）
        var leafBNodes = instance.GetNodesOfType<LeafNode_B_Data>();
        foreach (var leafB in leafBNodes)
        {
            if (leafB.ProcessID == node.ProcessID)
            {
                if (GraphRunner.Instance.EnableDebugLog)
                {
                    Debug.Log($"[LeafNode A] 通知 Leaf B: {leafB.NodeID}");
                }

                var newSignal = context.Clone();
                newSignal.SourceNodeId = leafB.NodeID;
                instance.InjectSignal(newSignal);
            }
        }
    }
}

/// <summary>
/// Leaf B 节点策略 - 处理执行完毕的回调喵~
/// </summary>
public class LeafNodeBStrategy : INodeStrategy
{
    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is not LeafNode_B_Data leafNode) return;

        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[LeafNode B] 执行回调：{leafNode.NodeID} (ProcessID: {leafNode.ProcessID})");
        }

        // 向输出节点传播信号（通常是完成回调）
        foreach (var conn in leafNode.OutputConnections)
        {
            var newSignal = context.Clone();
            newSignal.SourceNodeId = conn.TargetNodeID;
            instance.InjectSignal(newSignal);
        }
    }

    public void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance)
    {
        // Leaf B 节点通常不直接响应外部事件
    }
}
