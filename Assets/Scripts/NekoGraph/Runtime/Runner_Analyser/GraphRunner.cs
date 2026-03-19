using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 图运行器 - 管理所有运行时图实例的中央调度器喵~
/// 直接操作 BasePackData，不再需要 RuntimeGraphInstance 中间层喵！
/// </summary>
public class GraphRunner : SingletonMono<GraphRunner>
{
    /// <summary>
    /// 持久化 GUID 到实例化 Pack 字典喵~
    /// Key: InstanceID (运行时生成的 GUID), Value: BasePackData 本体
    /// 通常指向 UserModel.PackDataDict（GraphRunner 只是引用，不拥有所有权）
    /// 生命周期跟随 UserModel，GraphRunner 只是个"打工人"喵~
    /// </summary>
    public Dictionary<string, BasePackData> PersistentGuidToInstancedPackDict;

    /// <summary>
    /// InstanceID 缓存列表 - 用于安全遍历，防止"回手掏"导致字典修改异常喵~
    /// </summary>
    [NonSerialized]
    private List<string> _instanceIdCache = new List<string>();

    /// <summary>
    /// 最大信号传播深度（防止死循环）喵~
    /// </summary>
    public int MaxSignalDepth = 100;

    /// <summary>
    /// 是否启用调试日志喵~
    /// </summary>
    public bool EnableDebugLog = false;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        // 注册到 PostSystem 接收全局事件
        PostSystem.Instance.Register(this);
    }

    private void Update()
    {
        // 驱动所有 Pack 中的信号步进喵~
        TickAllPacks();
    }

    private void OnDestroy()
    {
        PersistentGuidToInstancedPackDict?.Clear();
    }

    // =========================================================
    // 核心 API - Pack 数据管理
    // =========================================================

    /// <summary>
    /// 设置 Pack 数据字典引用喵~
    /// 通常指向 UserModel.PackDataDict
    /// </summary>
    public void SetPackDataDict(Dictionary<string, BasePackData> dict)
    {
        PersistentGuidToInstancedPackDict = dict;
    }

    /// <summary>
    /// 加载 Pack，返回 instanceID 作为句柄喵~
    /// </summary>
    /// <param name="pack">Pack 数据</param>
    /// <returns>InstanceID (GUID)，用作其他系统的句柄</returns>
    public string LoadPack(BasePackData pack)
    {
        if (pack == null)
        {
            Debug.LogError("[GraphRunner] 尝试加载空的 Pack 喵~");
            return null;
        }

        // 1. 动态生成 instanceID (GUID)
        string instanceID = Guid.NewGuid().ToString("N");

        // 2. 添加到主字典
        PersistentGuidToInstancedPackDict[instanceID] = pack;

        if (EnableDebugLog)
        {
            Debug.Log($"[GraphRunner] Pack 已加载：{pack.PackID} → InstanceID: {instanceID}");
        }

        return instanceID;
    }

    /// <summary>
    /// 卸载指定 instanceID 的 Pack 喵~
    /// </summary>
    public void UnloadPack(string instanceID)
    {
        if (string.IsNullOrEmpty(instanceID)) return;

        // 清理该实例的信号（如果有）
        if (PersistentGuidToInstancedPackDict.TryGetValue(instanceID, out var pack))
        {
            pack.ActiveSignals.Clear();
        }

        PersistentGuidToInstancedPackDict.Remove(instanceID);

        if (EnableDebugLog)
        {
            Debug.Log($"[GraphRunner] Pack 已卸载：InstanceID: {instanceID}");
        }
    }

    /// <summary>
    /// 卸载所有指定 PackID 的实例喵~
    /// </summary>
    public void UnloadPacks(string packID)
    {
        if (string.IsNullOrEmpty(packID)) return;

        var toRemove = PersistentGuidToInstancedPackDict
            .Where(kvp => kvp.Value.PackID == packID)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var instanceID in toRemove)
        {
            UnloadPack(instanceID);
        }

        if (EnableDebugLog)
        {
            Debug.Log($"[GraphRunner] 已卸载 {toRemove.Count} 个 PackID 为 {packID} 的实例喵~");
        }
    }

    // =========================================================
    // 核心 API - 信号驱动
    // =========================================================

    /// <summary>
    /// 向指定 instance 注入信号喵~
    /// </summary>
    public void InjectSignal(string instanceID, SignalContext signal)
    {
        if (PersistentGuidToInstancedPackDict == null) return;
        if (!PersistentGuidToInstancedPackDict.TryGetValue(instanceID, out var pack)) return;

        pack.ActiveSignals.Enqueue(signal);
    }

    /// <summary>
    /// 从 Root 节点注入信号喵~（便捷方法）
    /// </summary>
    public void InjectSignalFromRoot(string instanceID, object args = null)
    {
        if (PersistentGuidToInstancedPackDict == null) return;
        if (!PersistentGuidToInstancedPackDict.TryGetValue(instanceID, out var pack)) return;
        if (string.IsNullOrEmpty(pack.RootNodeId))
        {
            Debug.LogWarning($"[GraphRunner] Pack 没有 RootNodeId，无法注入信号喵~");
            return;
        }

        var signal = new SignalContext(pack.RootNodeId, args);
        pack.ActiveSignals.Enqueue(signal);
    }

    /// <summary>
    /// 向所有 Pack 广播信号喵~
    /// </summary>
    public void BroadcastSignal(SignalContext signal)
    {
        if (PersistentGuidToInstancedPackDict == null) return;

        foreach (var pack in PersistentGuidToInstancedPackDict.Values)
        {
            pack.ActiveSignals.Enqueue(signal.Clone());
        }
    }

    /// <summary>
    /// 驱动所有 Pack 的信号步进喵~
    /// 使用缓存列表防止"回手掏"导致字典修改异常喵~
    /// </summary>
    private void TickAllPacks()
    {
        if (PersistentGuidToInstancedPackDict == null) return;

        // 使用缓存列表安全遍历，防止信号处理过程中卸载 Pack 导致字典修改喵~
        _instanceIdCache.Clear();
        foreach (var key in PersistentGuidToInstancedPackDict.Keys)
        {
            _instanceIdCache.Add(key);
        }

        foreach (var instanceID in _instanceIdCache)
        {
            if (PersistentGuidToInstancedPackDict.TryGetValue(instanceID, out var pack))
            {
                if (pack.ActiveSignals.Count > 0)
                {
                    TickPack(pack);
                }
            }
        }
    }

    /// <summary>
    /// 驱动单个 Pack 的信号步进喵~
    /// 限制每帧处理的信号数量，防止卡顿
    /// </summary>
    private void TickPack(BasePackData pack)
    {
        int signalsToProcess = Math.Min(pack.ActiveSignals.Count, 50);

        for (int i = 0; i < signalsToProcess; i++)
        {
            if (pack.ActiveSignals.Count == 0) break;

            var signal = pack.ActiveSignals.Dequeue();

            ProcessSignal(signal, pack);
        }
    }

    /// <summary>
    /// 处理单个信号的传播喵~
    /// 包含深度检查，防止死循环喵~
    /// </summary>
    private void ProcessSignal(SignalContext signal, BasePackData pack)
    {
        // 深度检查：防止死循环喵~
        if (signal.Depth > MaxSignalDepth)
        {
            if (EnableDebugLog)
            {
                Debug.LogWarning($"[GraphRunner] 信号深度超过上限 ({MaxSignalDepth})，已强制丢弃喵~ Signal: {signal.CurrentNodeId}");
            }
            return;
        }

        // 如果 CurrentNodeId 为空，直接丢弃信号喵~
        if (string.IsNullOrEmpty(signal.CurrentNodeId))
        {
            if (EnableDebugLog)
            {
                Debug.LogWarning($"[GraphRunner] 信号没有 CurrentNodeId，已丢弃喵~");
            }
            return;
        }

        // 直接从 pack.Nodes 字典查找节点喵~
        if (pack.Nodes.TryGetValue(signal.CurrentNodeId, out var currentNode))
        {
            var strategy = GetStrategy(currentNode);
            if (strategy != null)
            {
                strategy.OnSignalEnter(currentNode, signal, pack);
            }
        }
        else
        {
            if (EnableDebugLog)
            {
                Debug.LogWarning($"[GraphRunner] 节点不存在：{signal.CurrentNodeId}");
            }
        }
    }

    // =========================================================
    // 辅助方法
    // =========================================================

    /// <summary>
    /// 获取节点的策略处理器喵~
    /// 直接从 NodeStrategyFactory 获取（工厂已按类型缓存）
    /// </summary>
    private NodeStrategy GetStrategy(BaseNodeData data)
    {
        if (data == null) return null;
        return NodeStrategyFactory.GetStrategy(data);
    }

    /// <summary>
    /// 清理指定 instance 的所有活跃监听器（TriggerNode 的响应式监听）喵~
    /// </summary>
    private void CleanupInstanceListeners(string instanceID)
    {
        // 通过 TriggerNodeStrategy 单例调用清理方法
        TriggerNodeStrategy.Instance?.ForceDeactivate(instanceID);
    }

    /// <summary>
    /// 获取调试信息喵~
    /// 包含信号积压警告喵~
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"[GraphRunner] Pack 数据：{PersistentGuidToInstancedPackDict?.Count ?? 0}");

        if (PersistentGuidToInstancedPackDict != null)
        {
            foreach (var kvp in PersistentGuidToInstancedPackDict)
            {
                var pack = kvp.Value;
                int signalCount = pack.ActiveSignals.Count;
                
                // 信号积压警告喵~
                if (signalCount > 20)
                {
                    info.AppendLine($"  ⚠️ [警告] 信号积压！InstanceID: {kvp.Key}, Count: {signalCount}");
                }
                else
                {
                    info.AppendLine($"  - {kvp.Key}: Nodes={pack.Nodes.Count}, Signals={signalCount}");
                }
            }
        }

        return info.ToString();
    }
}
