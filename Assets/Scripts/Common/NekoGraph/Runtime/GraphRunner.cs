using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 图运行器 - 管理所有运行时图实例的中央调度器喵~
/// 这是唯一的单例，负责驱动信号流动和事件广播
/// </summary>
public class GraphRunner : SingletonMono<GraphRunner>
{
    /// <summary>
    /// 所有活跃的图实例字典：InstanceID -> RuntimeGraphInstance
    /// </summary>
    private Dictionary<string, RuntimeGraphInstance> _instances;

    /// <summary>
    /// 节点策略缓存（用于快速查找）喵~
    /// </summary>
    private Dictionary<BaseNodeData, INodeStrategy> _strategyCache;

    /// <summary>
    /// 最大信号传播深度（防止无限循环）喵~
    /// </summary>
    public int MaxSignalDepth = 100;

    /// <summary>
    /// 是否启用调试日志喵~
    /// </summary>
    public bool EnableDebugLog = false;

    protected override void Awake()
    {
        base.Awake();
        _instances = new Dictionary<string, RuntimeGraphInstance>();
        _strategyCache = new Dictionary<BaseNodeData, INodeStrategy>();
    }

    private void Start()
    {
        // 注册到 PostSystem 接收全局事件
        PostSystem.Instance.Register(this);
    }

    private void Update()
    {
        // 驱动所有实例中的信号步进
        TickAllInstances();
    }

    private void OnDestroy()
    {
        _instances.Clear();
        _strategyCache.Clear();
    }

    // =========================================================
    // 核心 API - 图实例管理
    // =========================================================

    /// <summary>
    /// 注册一个新的图实例（加载电路板）喵~
    /// </summary>
    public void RegisterInstance(RuntimeGraphInstance instance)
    {
        if (instance == null)
        {
            Debug.LogError("[GraphRunner] 尝试注册空的图实例喵~");
            return;
        }

        if (_instances.ContainsKey(instance.InstanceID))
        {
            Debug.LogWarning($"[GraphRunner] 图实例 {instance.InstanceID} 已存在，覆盖注册喵~");
            UnregisterInstance(instance.InstanceID);
        }

        _instances[instance.InstanceID] = instance;
        instance.IsRunning = true;

        if (EnableDebugLog)
        {
            Debug.Log($"[GraphRunner] 图实例已注册：{instance.InstanceID}");
        }
    }

    /// <summary>
    /// 注销一个图实例（卸载电路板）喵~
    /// </summary>
    public void UnregisterInstance(string instanceID)
    {
        if (_instances.TryGetValue(instanceID, out var instance))
        {
            instance.IsRunning = false;
            instance.ClearSignals();
            _instances.Remove(instanceID);

            // 清理策略缓存
            var toRemove = _strategyCache.Keys.Where(k => instance.NodeMap.ContainsValue(k)).ToList();
            foreach (var key in toRemove)
            {
                _strategyCache.Remove(key);
            }

            // 清理该图实例的所有活跃监听器（TriggerNode 的响应式监听）
            CleanupInstanceListeners(instanceID);

            if (EnableDebugLog)
            {
                Debug.Log($"[GraphRunner] 图实例已注销：{instanceID}");
            }
        }
    }

    /// <summary>
    /// 获取指定 ID 的图实例喵~
    /// </summary>
    public RuntimeGraphInstance GetInstance(string instanceID)
    {
        _instances.TryGetValue(instanceID, out var instance);
        return instance;
    }

    /// <summary>
    /// 获取所有活跃的图实例喵~
    /// </summary>
    public IEnumerable<RuntimeGraphInstance> GetAllInstances()
    {
        return _instances.Values;
    }

    /// <summary>
    /// 清空所有图实例喵~
    /// </summary>
    public void ClearAllInstances()
    {
        var ids = _instances.Keys.ToList();
        foreach (var id in ids)
        {
            UnregisterInstance(id);
        }
    }

    // =========================================================
    // 核心 API - 信号驱动
    // =========================================================

    /// <summary>
    /// 向指定图实例注入信号喵~
    /// </summary>
    public void InjectSignal(string instanceID, SignalContext signal)
    {
        if (_instances.TryGetValue(instanceID, out var instance))
        {
            instance.InjectSignal(signal);
        }
    }

    /// <summary>
    /// 向所有图实例广播信号喵~
    /// </summary>
    public void BroadcastSignal(SignalContext signal)
    {
        foreach (var instance in _instances.Values)
        {
            instance.InjectSignal(signal.Clone());
        }
    }

    /// <summary>
    /// 驱动所有实例的信号步进喵~
    /// </summary>
    private void TickAllInstances()
    {
        foreach (var instance in _instances.Values)
        {
            if (!instance.IsRunning) continue;

            TickInstance(instance);
        }
    }

    /// <summary>
    /// 驱动单个实例的信号步进喵~
    /// </summary>
    private void TickInstance(RuntimeGraphInstance instance)
    {
        // 限制每帧处理的信号数量，防止卡顿
        int signalsToProcess = Math.Min(instance.ActiveSignals.Count, 50);

        for (int i = 0; i < signalsToProcess; i++)
        {
            if (instance.ActiveSignals.Count == 0) break;

            var signal = instance.ActiveSignals.Dequeue();
            ProcessSignal(signal, instance);
        }
    }

    /// <summary>
    /// 处理单个信号的传播喵~
    /// </summary>
    private void ProcessSignal(SignalContext signal, RuntimeGraphInstance instance)
    {
        if (signal.Depth > MaxSignalDepth)
        {
            Debug.LogWarning($"[GraphRunner] 信号传播深度超过限制 ({MaxSignalDepth})，终止传播喵~");
            return;
        }

        // 找到信号来源节点
        if (!string.IsNullOrEmpty(signal.SourceNodeId) &&
            instance.NodeMap.TryGetValue(signal.SourceNodeId, out var sourceNode))
        {
            var strategy = GetStrategy(sourceNode);
            if (strategy != null)
            {
                strategy.OnSignalEnter(sourceNode, signal, instance);
            }
        }
        else
        {
            // 没有来源节点，可能是初始信号，需要找到入口节点（如 Root 节点）
            var rootNodes = instance.GetNodesOfType<RootNodeData>();
            foreach (var rootNode in rootNodes)
            {
                var strategy = GetStrategy(rootNode);
                if (strategy != null)
                {
                    strategy.OnSignalEnter(rootNode, signal, instance);
                }
            }
        }
    }

    // =========================================================
    // 核心 API - 事件处理
    // =========================================================

    /// <summary>
    /// 全局事件桥接器 - 接收 PostSystem 的事件并广播给所有实例喵~
    /// </summary>
    [Subscribe("EVT_GLOBAL")]
    private void OnGlobalEvent(object data)
    {
        // 解析事件数据
        if (data is GlobalEventData globalEvent)
        {
            BroadcastEvent(globalEvent.EventName, globalEvent.EventData);
        }
    }

    /// <summary>
    /// 广播事件到所有图实例中正在通电的 Trigger 节点喵~
    /// </summary>
    public void BroadcastEvent(string eventName, object eventData)
    {
        foreach (var instance in _instances.Values)
        {
            if (!instance.IsRunning) continue;

            // 找到所有正在通电的 Trigger 节点
            foreach (var triggerId in instance.PoweredTriggerIds)
            {
                if (instance.NodeMap.TryGetValue(triggerId, out var node) && node is TriggerNodeData triggerData)
                {
                    var strategy = GetStrategy(triggerData);
                    if (strategy != null)
                    {
                        strategy.OnEvent(triggerData, eventName, eventData, instance);
                    }
                }
            }
        }
    }

    // =========================================================
    // 辅助方法
    // =========================================================

    /// <summary>
    /// 获取节点的策略处理器喵~
    /// </summary>
    private INodeStrategy GetStrategy(BaseNodeData data)
    {
        if (data == null) return null;

        if (!_strategyCache.TryGetValue(data, out var strategy))
        {
            strategy = NodeStrategyFactory.GetStrategy(data);
            if (strategy != null)
            {
                _strategyCache[data] = strategy;
            }
        }

        return strategy;
    }

    /// <summary>
    /// 清理图实例的所有活跃监听器（TriggerNode 的响应式监听）喵~
    /// </summary>
    private void CleanupInstanceListeners(string instanceID)
    {
        // 通过 TriggerNodeStrategy 单例调用清理方法
        TriggerNodeStrategy.Instance?.ForceDeactivate(instanceID);
    }

    /// <summary>
    /// 获取调试信息喵~
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"[GraphRunner] 活跃图实例：{_instances.Count}");
        foreach (var instance in _instances.Values)
        {
            info.AppendLine($"  - {instance.GetDebugInfo()}");
        }
        return info.ToString();
    }

    // =========================================================
    // 全局事件数据结构
    // =========================================================

    /// <summary>
    /// 全局事件数据 - 用于在 PostSystem 和 GraphRunner 之间传递事件喵~
    /// </summary>
    public class GlobalEventData
    {
        public string EventName;
        public object EventData;

        public GlobalEventData(string eventName, object eventData)
        {
            EventName = eventName;
            EventData = eventData;
        }
    }
}
