using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

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
        // 找到信号当前所在的节点
        if (!string.IsNullOrEmpty(signal.CurrentNodeId) &&
            instance.NodeMap.TryGetValue(signal.CurrentNodeId, out var currentNode))
        {
            var strategy = GetStrategy(currentNode);
            if (strategy != null)
            {
                strategy.OnSignalEnter(currentNode, signal, instance);
            }
        }
        else
        {
            // 没有当前节点，可能是初始信号，需要找到入口节点（如 Root 节点）
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
    // 存档系统 - 捕获运行中的图实例状态喵~
    // =========================================================

    /// <summary>
    /// 捕获所有正在运行的图实例状态喵~
    /// 只保存 IsRunning = true 的实例
    /// </summary>
    public List<GraphInstanceSnapshot> CaptureAllRunningGraphs()
    {
        var snapshots = new List<GraphInstanceSnapshot>();

        foreach (var instance in _instances.Values)
        {
            if (instance.IsRunning)
            {
                snapshots.Add(CaptureInstanceSnapshot(instance));
            }
        }

        if (EnableDebugLog)
        {
            Debug.Log($"[GraphRunner] 已捕获 {snapshots.Count} 个运行中的图实例快照喵~");
        }

        return snapshots;
    }

    /// <summary>
    /// 捕获单个图实例的快照喵~
    /// </summary>
    private GraphInstanceSnapshot CaptureInstanceSnapshot(RuntimeGraphInstance instance)
    {
        var snapshot = new GraphInstanceSnapshot
        {
            InstanceID = instance.InstanceID,
            PackID = instance.PackID,
            GraphType = instance.GraphType,
            SourceJsonFileName = instance.SourceJsonFileName,
            ActiveSignals = new List<SignalContextSnapshot>(),
            BlockingNodes = new List<NodeBlockingSnapshot>()
        };

        // 1. 捕获活跃信号队列喵~
        foreach (var signal in instance.ActiveSignals)
        {
            snapshot.ActiveSignals.Add(CaptureSignalSnapshot(signal));
        }

        // 2. 捕获阻隔节点状态喵~
        // 遍历所有节点，检查策略是否实现 IBlockingNodeStrategy
        foreach (var node in instance.NodeMap.Values)
        {
            var strategy = GetStrategy(node);

            // 问策略：你是不是阻隔节点策略？
            if (strategy is IBlockingNodeStrategy blockingStrategy)
            {
                // 是阻隔节点，调用策略的捕获方法
                var blockingState = blockingStrategy.CaptureBlockingState(node);

                if (blockingState != null)
                {
                    snapshot.BlockingNodes.Add(new NodeBlockingSnapshot
                    {
                        NodeID = node.NodeID,
                        BlockingStateJson = JsonConvert.SerializeObject(blockingState, GraphRunner.JsonSettings)
                    });
                }
            }
        }

        if (EnableDebugLog)
        {
            Debug.Log($"[GraphRunner] 图实例 {instance.InstanceID} 快照：{snapshot.ActiveSignals.Count} 个信号，{snapshot.BlockingNodes.Count} 个阻隔节点喵~");
        }

        return snapshot;
    }

    /// <summary>
    /// 捕获信号上下文快照喵~
    /// </summary>
    private SignalContextSnapshot CaptureSignalSnapshot(SignalContext signal)
    {
        var snapshot = new SignalContextSnapshot
        {
            CurrentNodeId = signal.CurrentNodeId,
            ArgsJson = signal.Args != null ? JsonConvert.SerializeObject(signal.Args, JsonSettings) : null,
            TraveledPath = new List<ConnectionDataSnapshot>()
        };

        // 记录信号走过的路径喵~
        if (signal.TraveledPath != null)
        {
            foreach (var conn in signal.TraveledPath)
            {
                snapshot.TraveledPath.Add(new ConnectionDataSnapshot(conn));
            }
        }

        return snapshot;
    }

    /// <summary>
    /// JSON 序列化设置喵~
    /// </summary>
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None
    };

    // =========================================================
    // 读档系统 - 恢复图实例状态喵~
    // =========================================================

    /// <summary>
    /// 恢复所有图实例快照喵~
    /// </summary>
    public void RestoreAllFromSnapshots(List<GraphInstanceSnapshot> snapshots)
    {
        if (snapshots == null || snapshots.Count == 0)
        {
            Debug.Log("[GraphRunner] 没有需要恢复的图实例快照喵~");
            return;
        }

        foreach (var snapshot in snapshots)
        {
            RestoreFromSnapshot(snapshot);
        }

        Debug.Log($"[GraphRunner] 已恢复 {snapshots.Count} 个图实例喵~");
    }

    /// <summary>
    /// 从快照恢复单个图实例喵~
    /// </summary>
    public void RestoreFromSnapshot(GraphInstanceSnapshot snapshot)
    {
        // 优先使用 PackID，兼容旧的 SourceJsonFileName
        if (string.IsNullOrEmpty(snapshot.PackID) && string.IsNullOrEmpty(snapshot.SourceJsonFileName))
        {
            Debug.LogError($"[GraphRunner] 快照缺少 PackID 和 SourceJsonFileName，无法恢复：{snapshot.InstanceID}");
            return;
        }

        // 1. 从 MetaLib 智能仓库获取 PackData
        BasePackData pack = null;
        if (!string.IsNullOrEmpty(snapshot.PackID))
        {
            pack = MetaLib.GetPack<BasePackData>(snapshot.PackID);
        }
        else // 兼容旧存档
        {
            Debug.LogWarning($"[GraphRunner] 快照缺少 PackID，尝试从 SourceJsonFileName 回退加载：{snapshot.SourceJsonFileName}");
            var meta = MetaLib.GetMetaByPath(snapshot.SourceJsonFileName);
            if (meta != null)
            {
                 pack = MetaLib.GetPack<BasePackData>(meta.PackID);
            }
        }

        if (pack == null)
        {
            Debug.LogError($"[GraphRunner] Pack 加载失败 (PackID: {snapshot.PackID})");
            return;
        }
        
        // 2. 重新加载图实例（使用快照中的 InstanceID）
        var instance = GraphLoader.LoadFromPackGeneric(
            pack,
            snapshot.InstanceID,
            snapshot.GraphType,
            snapshot.PackID
        );

        if (instance == null)
        {
            Debug.LogError($"[GraphRunner] 图实例加载失败：{snapshot.InstanceID}");
            return;
        }

        // 4. 恢复阻隔节点状态
        int restoredNodes = 0;
        foreach (var blockingNodeSnapshot in snapshot.BlockingNodes)
        {
            if (instance.NodeMap.TryGetValue(blockingNodeSnapshot.NodeID, out var node))
            {
                var strategy = GetStrategy(node);
                if (strategy is IBlockingNodeStrategy blockingStrategy)
                {
                    var state = JsonConvert.DeserializeObject(blockingNodeSnapshot.BlockingStateJson, JsonSettings);
                    blockingStrategy.RestoreBlockingState(node, state);
                    restoredNodes++;
                }
            }
        }

        // 5. 恢复信号队列（唤醒流程）
        int restoredSignals = 0;
        foreach (var signalSnapshot in snapshot.ActiveSignals)
        {
            // 重建 SignalContext
            var signal = new SignalContext
            {
                CurrentNodeId = signalSnapshot.CurrentNodeId,
                Args = signalSnapshot.ArgsJson != null ? JsonConvert.DeserializeObject(signalSnapshot.ArgsJson, JsonSettings) : null
            };

            // 遍历途径点，触发策略的唤醒方法
            // 注意：不包含当前节点，只处理途径点
            foreach (var connSnapshot in signalSnapshot.TraveledPath)
            {
                // 找到途径点对应的源节点
                if (instance.NodeMap.TryGetValue(connSnapshot.SourceNodeID, out var passedNode))
                {
                    var strategy = GetStrategy(passedNode);
                    // 这里不需要特殊处理，因为阻隔状态已经恢复
                    // 信号注入后会自动按照策略逻辑流动
                }
            }

            // 注入信号到实例
            instance.InjectSignal(signal);
            restoredSignals++;
        }

        // 6. 注册到 GraphRunner 并标记为运行中
        instance.IsRunning = true;
        RegisterInstance(instance);

        if (EnableDebugLog)
        {
            Debug.Log($"[GraphRunner] 图实例 {snapshot.InstanceID} 恢复完成：{restoredNodes} 个阻隔节点，{restoredSignals} 个信号喵~");
        }
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
