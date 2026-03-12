using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// 触发器节点策略 - 响应式电子伏特协议喵~
// 接线员模式：Strategy 仅负责在"进电"时将"后续传导逻辑"注入 Data 的回调中
// 数据自治：TriggerData 自行管理 PostSystem 监听生命周期
// =========================================================

/// <summary>
/// TriggerNode 策略 - 极简响应式模式喵~
///
/// 工作原理：
/// 1. 信号进入 → trigger.Trigger.Register() 挂载监听
/// 2. 事件匹配 → TriggerData 内部回调 → 累积进度 → 无脑转发到 ProgressOutputs
/// 3. 达到目标 → 从主输出端口发射信号 → 自动 Unregister()
/// 4. 图卸载 → 遍历节点调用 data.Trigger.Unregister() 清理
///
/// 单例模式：通过 Instance 静态属性访问唯一实例喵~
/// </summary>
public class TriggerNodeStrategy : INodeStrategy
{
    /// <summary>
    /// 单例实例喵~
    /// </summary>
    public static TriggerNodeStrategy Instance { get; private set; }

    /// <summary>
    /// 静态构造函数 - 注册单例喵~
    /// </summary>
    static TriggerNodeStrategy()
    {
        Instance = new TriggerNodeStrategy();
    }

    /// <summary>
    /// 私有构造函数防止外部实例化喵~
    /// </summary>
    private TriggerNodeStrategy()
    {
    }

    /// <summary>
    /// 追踪本策略实例管理的所有 TriggerData（用于图卸载时清理）喵~
    /// </summary>
    private HashSet<TriggerData> _managedTriggers = new HashSet<TriggerData>();

    /// <summary>
    /// 图实例到 TriggerData 集合的映射（用于按实例清理）喵~
    /// </summary>
    private Dictionary<string, HashSet<TriggerData>> _instanceToTriggers = new Dictionary<string, HashSet<TriggerData>>();

    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is not TriggerNodeData triggerNode) return;

        var trigger = triggerNode.Trigger;

        // 如果已经触发过，不再响应
        if (trigger.HasTriggered)
        {
            return;
        }

        // 如果已经注册过，跳过（防止重复注册）
        if (trigger.IsRegistered)
        {
            Debug.LogWarning($"[TriggerNode] 节点 {triggerNode.NodeID} 的触发器已经注册，跳过喵~");
            return;
        }

        if (GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[TriggerNode] 信号进入：{trigger.EventName} (NodeID: {triggerNode.NodeID})");
        }

        // 注册监听，注入传导逻辑喵~
        RegisterTrigger(triggerNode, context, instance);
    }

    public void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance)
    {
        // 响应式模式下，OnEvent 不再被调用
        // 事件由 PostSystem 直接回调处理
    }

    /// <summary>
    /// 注册触发器 - "接线员"模式喵~
    /// 将传导逻辑注入 TriggerData 的回调中
    /// </summary>
    private void RegisterTrigger(TriggerNodeData node, SignalContext context, RuntimeGraphInstance instance)
    {
        var trigger = node.Trigger;

        // 重置累积进度
        node.CurrentAmount = 0;

        // 注册监听，注入回调逻辑
        trigger.Register(payload =>
        {
            if (GraphRunner.Instance.EnableDebugLog)
            {
                Debug.Log($"[TriggerNode] 事件匹配：{trigger.EventName} (NodeID: {node.NodeID})");
            }

            // 从事件数据中提取数量（用于累积进度）
            double amount = ExtractAmountFromPayload(payload);
            node.CurrentAmount += amount;

            if (GraphRunner.Instance.EnableDebugLog)
            {
                Debug.Log($"[TriggerNode] 进度更新：{node.CurrentAmount}/{node.RequiredAmount}");
            }

            // ★ 无脑转发信号到"进度输出"端口（周期分流器）★
            // 把 payload 放入当前 Signal 的 Args，然后复制并传播
            context.Args = payload;
            if (node.ProgressOutputs != null && node.ProgressOutputs.Count > 0)
            {
                foreach (var targetId in node.ProgressOutputs)
                {
                    var progressSignal = context.Clone();
                    progressSignal.CurrentNodeId = targetId;
                    instance.InjectSignal(progressSignal);
                }
            }

            // 检查是否达到目标
            if (node.CurrentAmount >= node.RequiredAmount)
            {
                // 从主输出端口发射信号
                PropagateSignal(node, context, instance);
            }
        });

        // 追踪管理 - 本地集合
        _managedTriggers.Add(trigger);

        // 追踪管理 - 实例映射
        if (!_instanceToTriggers.TryGetValue(instance.InstanceID, out var triggerSet))
        {
            triggerSet = new HashSet<TriggerData>();
            _instanceToTriggers[instance.InstanceID] = triggerSet;
        }
        triggerSet.Add(trigger);
    }

    /// <summary>
    /// 从事件数据中提取数量喵~
    /// </summary>
    private double ExtractAmountFromPayload(object payload)
    {
        if (payload == null) return 1;

        // 尝试从 MissionArgs 中提取
        if (payload is MissionArgs args)
        {
            return args.Amount;
        }

        // 尝试从数值类型中提取
        if (payload is long lAmount) return lAmount;
        if (payload is int iAmount) return iAmount;
        if (payload is double dAmount) return dAmount;
        if (payload is float fAmount) return fAmount;

        // 默认返回 1
        return 1;
    }

    /// <summary>
    /// 传播信号到输出节点喵~
    /// </summary>
    private void PropagateSignal(TriggerNodeData node, SignalContext context, RuntimeGraphInstance instance)
    {
        // 使用 OutputConnections 传播信号
        foreach (var conn in node.OutputConnections)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = conn.TargetNodeID;
            instance.InjectSignal(newSignal);
        }

        // 兼容旧版 OutputNodeIDs
        foreach (var nextId in node.OutputNodeIDs)
        {
            var newSignal = context.Clone();
            newSignal.CurrentNodeId = nextId;
            instance.InjectSignal(newSignal);
        }
    }

    /// <summary>
    /// 图卸载时强制注销所有监听 - 清理喵~
    /// </summary>
    public void ForceDeactivate(string instanceID)
    {
        if (_instanceToTriggers.TryGetValue(instanceID, out var triggerSet))
        {
            foreach (var trigger in triggerSet)
            {
                trigger.Unregister();
            }
            _instanceToTriggers.Remove(instanceID);

            if (GraphRunner.Instance.EnableDebugLog)
            {
                Debug.Log($"[TriggerNode] 图卸载清理完成 (Instance: {instanceID})");
            }
        }
    }

    /// <summary>
    /// 获取当前管理的触发器数量喵~
    /// </summary>
    public int GetActiveListenerCount()
    {
        return _managedTriggers.Count;
    }

    /// <summary>
    /// 获取调试信息喵~
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"[TriggerNodeStrategy] 管理的触发器：{_managedTriggers.Count}");
        info.AppendLine($"[TriggerNodeStrategy] 图实例数：{_instanceToTriggers.Count}");
        foreach (var kvp in _instanceToTriggers)
        {
            info.AppendLine($"  - 实例 {kvp.Key}: {kvp.Value.Count} 个触发器");
        }
        return info.ToString();
    }
}
