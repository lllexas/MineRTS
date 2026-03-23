using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ComparerNode 策略 - 逻辑判断与信号调度中心喵~
/// </summary>
public class ComparerNodeStrategy : NodeStrategy
{
    public static ComparerNodeStrategy Instance { get; private set; }

    static ComparerNodeStrategy()
    {
        Instance = new ComparerNodeStrategy();
    }

    private ComparerNodeStrategy() { }

    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not ComparerNodeData comparerNode) return;

        // 1. 执行比较逻辑喵~
        object payload = context.Args;
        var result = ComparerRegistry.Execute(comparerNode.ComparerName, payload, comparerNode.Parameters.ToArray());

        if (runner.EnableDebugLog)
        {
            Debug.Log($"[ComparerNode] 判定结果：{result} (NodeID: {comparerNode.NodeID}) 喵~");
        }

        // 2. 信号分流喵~
        if (result == ComparerResult.Pass)
        {
            // 通过：走绿灯喵！
            Propagate(comparerNode.PassOutputs, context, pack);
        }
        else
        {
            // 契约校验失败，红色警告！
            if (result == ComparerResult.TypeMismatch)
            {
                Debug.LogError($"<color=red>[ComparerNode] 类型失配！</color> 比较器 [{comparerNode.ComparerName}] 无法处理当前 Payload 类型 [{payload?.GetType().Name ?? "null"}] 喵！\n" +
                               $"请检查 Trigger 绑定的事件协议是否与 Comparer 匹配喵~");
            }

            // 不通过：走红灯喵！
            Propagate(comparerNode.FailOutputs, context, pack);
            BacktraceAndReactivateTrigger(context, pack, runner, packInstanceID);
        }
    }

    /// <summary>
    /// 信号传播喵~
    /// </summary>
    private void Propagate(List<string> targets, SignalContext context, BasePackData pack)
    {
        if (targets == null) return;
        EnqueueSignals(pack, targets, context);
    }

    /// <summary>
    /// 顺着信号路径往回找，重新激活上一个 Trigger 节点喵！
    /// </summary>
    private void BacktraceAndReactivateTrigger(SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        // 信号路径是从近到远排列的，我们倒着找喵~
        // 注意：TraveledPath 存储的是 ConnectionData 列表
        var path = context.TraveledPath;
        if (path == null || path.Count == 0) return;

        // 倒序遍历，找到最近的一个 Trigger 节点喵~
        for (int i = path.Count - 1; i >= 0; i--)
        {
            var conn = path[i];

            // 优先使用 SourceNodeID，如果没有则使用当前节点 ID 回溯
            string nodeId = !string.IsNullOrEmpty(conn.SourceNodeID) ?
                conn.SourceNodeID :
                FindSourceNodeId(conn, pack);

            if (!string.IsNullOrEmpty(nodeId) &&
                pack.Nodes.TryGetValue(nodeId, out var node) &&
                node is TriggerNodeData triggerNode)
            {
                if (GraphRunner.Instance.EnableDebugLog)
                {
                    Debug.Log($"[ComparerNode] 检测到失败，回溯并重连 Trigger: {triggerNode.Event} (NodeID: {nodeId}) 喵！");
                }

                // 重新通过 Strategy 激活该 Trigger 的监听喵~
                TriggerNodeStrategy.Instance.OnSignalEnter(triggerNode, context, pack, runner, packInstanceID);
                break; // 找到最近的一个就够了喵~
            }
        }
    }

    /// <summary>
    /// 从连接数据中查找源节点 ID 喵~
    /// </summary>
    private string FindSourceNodeId(ConnectionData conn, BasePackData pack)
    {
        // 遍历所有节点，找到包含该连接的节点
        foreach (var kvp in pack.Nodes)
        {
            if (kvp.Value is BaseNodeData node)
            {
                // 检查这个节点的 OutputConnections 是否包含这个连接
                if (node.OutputConnections != null)
                {
                    foreach (var outputConn in node.OutputConnections)
                    {
                        if (outputConn.TargetNodeID == conn.TargetNodeID &&
                            outputConn.FromPortIndex == conn.FromPortIndex)
                        {
                            return kvp.Key;
                        }
                    }
                }
            }
        }
        return null;
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID) { }
}
