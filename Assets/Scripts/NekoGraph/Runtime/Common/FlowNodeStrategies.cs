using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// 基础流程节点策略 - Root/Spine/Leaf 节点处理器喵~
// =========================================================

/// <summary>
/// Root 节点策略 - 流程的起始锚点喵~
/// </summary>
public class RootNodeStrategy : NodeStrategy
{
    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not RootNodeData rootNode) return;

        if (runner.EnableDebugLog)
        {
            Debug.Log($"[RootNode] 流程启动：{rootNode.NodeID}");
        }

        // 向所有输出节点传播信号
        EnqueueSignals(pack, rootNode.OutputConnections, context);
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        // Root 节点不响应外部事件
    }
}

/// <summary>
/// Spine 节点策略 - 流程的逻辑骨架/无线输电继电器喵~
/// </summary>
public class SpineNodeStrategy : NodeStrategy, IBlockingNodeStrategy
{
    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not SpineNodeData spineNode) return;

        if (runner.EnableDebugLog)
        {
            Debug.Log($"[SpineNode] 信号中继：{spineNode.NodeID} (ProcessID: {spineNode.ProcessID})");
        }

        // 1. 激活关联的 Leaf A 节点
        ActivateLeafNodes(spineNode, context, pack);

        // 2. 向下一个 Spine 节点传播信号
        PropagateToNextSpine(spineNode, context, pack);
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not SpineNodeData spineNode) return;

        // Spine 节点可以通过 Leaf B 节点的回调来响应事件
        if (runner.EnableDebugLog)
        {
            Debug.Log($"[SpineNode] 收到事件：{eventName} -> {spineNode.NodeID}");
        }
    }

    private void ActivateLeafNodes(SpineNodeData node, SignalContext context, BasePackData pack)
    {
        // 查找所有与当前 Spine 节点共享 ProcessID 的 Leaf A 节点
        foreach (var leafKvp in pack.Nodes)
        {
            if (leafKvp.Value is LeafNode_A_Data leaf && leaf.ProcessID == node.ProcessID)
            {
                if (GraphRunner.Instance.EnableDebugLog)
                {
                    Debug.Log($"[SpineNode] 激活 Leaf A: {leaf.NodeID} (ProcessID: {leaf.ProcessID})");
                }

                // 向 Leaf A 节点发送信号
                EnqueueSignal(pack, leaf.NodeID, context);
            }
        }
    }

    private void PropagateToNextSpine(SpineNodeData node, SignalContext context, BasePackData pack)
    {
        // 通过 OutputConnections 传播
        EnqueueSignals(pack, node.OutputConnections, context);

        // 兼容旧版 NextSpineNodeIDs 字段
        EnqueueSignals(pack, node.NextSpineNodeIDs, context);
    }

    // =========================================================
    // IBlockingNodeStrategy 实现 - 阻隔状态捕获与恢复喵~
    // =========================================================

    /// <summary>
    /// 捕获 Spine 节点的阻隔状态喵~
    /// </summary>
    public object CaptureBlockingState(BaseNodeData node)
    {
        if (node is not SpineNodeData spineNode) return null;

        // Spine 的阻隔状态：等待哪些 Leaf 完成
        // 通过查找共享 ProcessID 的 Leaf A 节点来确定
        return new SpineBlockingState
        {
            ProcessID = spineNode.ProcessID
        };
    }

    /// <summary>
    /// 恢复 Spine 节点的阻隔状态喵~
    /// </summary>
    public void RestoreBlockingState(BaseNodeData node, object state)
    {
        if (node is not SpineNodeData spineNode) return;
        if (state is not SpineBlockingState spineState) return;

        // Spine 节点读档后不需要特殊恢复，ProcessID 是持久化在节点数据中的
        // 这里保留方法用于未来扩展
    }

    /// <summary>
    /// Spine 阻隔状态数据结构喵~
    /// </summary>
    [Serializable]
    private class SpineBlockingState
    {
        public string ProcessID;
    }
}

/// <summary>
/// 销毁节点策略 - 销毁 Pack 实例并重置状态喵~
/// </summary>
public class DestroyNodeStrategy : NodeStrategy
{
    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (runner.EnableDebugLog)
        {
            Debug.Log($"[DestroyNode] 销毁 Pack 实例：{pack.PackID} (InstanceID: {packInstanceID})");
        }

        // 销毁当前 Pack 实例，强制重置状态喵~
        runner.UnloadPack(packInstanceID);
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID) { }
}

/// <summary>
/// Leaf A 节点策略 - 处理具体的执行演出喵~
/// </summary>
public class LeafNodeAStrategy : NodeStrategy
{
    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not LeafNode_A_Data leafNode) return;

        if (runner.EnableDebugLog)
        {
            Debug.Log($"[LeafNode A] 执行演出：{leafNode.NodeID} (ProcessID: {leafNode.ProcessID})");
        }

        // 向输出节点传播信号（通常是执行具体动作）
        EnqueueSignals(pack, leafNode.OutputConnections, context);

        // 同时通知对应的 Leaf B 节点（通过 pack.Nodes 查找）
        NotifyLeafB(leafNode, context, pack);
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        // Leaf A 节点通常不直接响应外部事件
    }

    private void NotifyLeafB(LeafNode_A_Data node, SignalContext context, BasePackData pack)
    {
        // 查找对应的 Leaf B 节点（共享 ProcessID）
        foreach (var leafBKvp in pack.Nodes)
        {
            if (leafBKvp.Value is LeafNode_B_Data leafB && leafB.ProcessID == node.ProcessID)
            {
                if (GraphRunner.Instance.EnableDebugLog)
                {
                    Debug.Log($"[LeafNode A] 通知 Leaf B: {leafB.NodeID}");
                }

                EnqueueSignal(pack, leafB.NodeID, context);
            }
        }
    }
}

/// <summary>
/// Leaf B 节点策略 - 处理执行完毕的回调喵~
/// </summary>
public class LeafNodeBStrategy : NodeStrategy
{
    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not LeafNode_B_Data leafNode) return;

        if (runner.EnableDebugLog)
        {
            Debug.Log($"[LeafNode B] 执行回调：{leafNode.NodeID} (ProcessID: {leafNode.ProcessID})");
        }

        // 向输出节点传播信号（通常是完成回调）
        EnqueueSignals(pack, leafNode.OutputConnections, context);
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        // Leaf B 节点通常不直接响应外部事件
    }
}
