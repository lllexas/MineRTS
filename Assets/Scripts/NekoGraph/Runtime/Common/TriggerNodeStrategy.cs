using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TriggerNode 策略 - 响应式监听与信号传导喵！
/// 职责：管理基于 PostSystem 的订阅生命周期，并将 Payload 注入 SignalContext 喵~
/// </summary>
public class TriggerNodeStrategy : NodeStrategy
{
    public static TriggerNodeStrategy Instance { get; private set; }

    static TriggerNodeStrategy()
    {
        Instance = new TriggerNodeStrategy();
    }

    private TriggerNodeStrategy() { }

    /// <summary>
    /// 图实例到 TriggerNodeData 集合的映射（用于按实例清理）喵~
    /// </summary>
    private Dictionary<string, HashSet<TriggerNodeData>> _instanceToTriggers = new Dictionary<string, HashSet<TriggerNodeData>>();

    private HashSet<string> _registeredNodes = new HashSet<string>();

    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not TriggerNodeData triggerNode) return;

        // 如果已经注册过，跳过（防止重复注册）喵~
        if (_registeredNodes.Contains(triggerNode.NodeID)) return;

        if (runner.EnableDebugLog)
        {
            Debug.Log($"[TriggerNode] 信号进入，开始监听事件：{triggerNode.Event} (NodeID: {triggerNode.NodeID}) 喵~");
        }

        // 注册监听，注入简单的传导逻辑喵~
        RegisterTrigger(triggerNode, context, pack);
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID) { }

    private void RegisterTrigger(TriggerNodeData node, SignalContext context, BasePackData pack)
    {
        Action<object> callback = null;
        callback = payload =>
        {
            if (GraphRunner.Instance.EnableDebugLog)
            {
                Debug.Log($"[TriggerNode] 事件响起：{node.Event}，信号放行喵！");
            }

            // 触发后自动注销监听喵~
            node.Unregister(callback);
            _registeredNodes.Remove(node.NodeID);

            // ★ 核心任务：把 Payload 塞进去，然后直接传导喵！ ★
            var nextSignal = context.Clone();
            nextSignal.Args = payload;

            PropagateSignal(node, nextSignal, pack);
        };

        node.Register(callback);
        _registeredNodes.Add(node.NodeID);

        // 追踪管理，方便强制清理喵~
        if (!_instanceToTriggers.TryGetValue(pack.PackID, out var nodeSet))
        {
            nodeSet = new HashSet<TriggerNodeData>();
            _instanceToTriggers[pack.PackID] = nodeSet;
        }
        nodeSet.Add(node);
    }

    private void PropagateSignal(TriggerNodeData node, SignalContext signal, BasePackData pack)
    {
        // 我们只关注 SignalOutputs (端口 0) 喵~
        if (node.SignalOutputs == null) return;

        EnqueueSignals(pack, node.SignalOutputs, signal);
    }

    public void ForceDeactivate(string packID)
    {
        // 这里需要更精细的注销逻辑，目前先简单清理喵~
        if (_instanceToTriggers.TryGetValue(packID, out var nodeSet))
        {
            _instanceToTriggers.Remove(packID);
        }
    }
}
