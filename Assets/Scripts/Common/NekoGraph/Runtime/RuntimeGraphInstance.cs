using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 运行时图实例 - 每个加载的 PackData 对应一个独立的"电路板"喵~
/// 支持多图并行运行，互不干扰
/// </summary>
public class RuntimeGraphInstance
{
    /// <summary>
    /// 图实例唯一 ID
    /// </summary>
    public string InstanceID;

    /// <summary>
    /// 图的类型（Story/Mission/Event 等）
    /// </summary>
    public string GraphType;

    /// <summary>
    /// 节点快照字典：NodeID -> BaseNodeData
    /// </summary>
    public Dictionary<string, BaseNodeData> NodeMap;

    /// <summary>
    /// 当前正在该图中流动的信号队列
    /// </summary>
    public Queue<SignalContext> ActiveSignals;

    /// <summary>
    /// 已激活的 Trigger 节点集合（用于事件监听）
    /// </summary>
    public HashSet<string> PoweredTriggerIds;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning;

    /// <summary>
    /// 加载时间戳
    /// </summary>
    public float LoadTime;

    public RuntimeGraphInstance(string instanceID, string graphType = "Generic")
    {
        InstanceID = instanceID;
        GraphType = graphType;
        NodeMap = new Dictionary<string, BaseNodeData>();
        ActiveSignals = new Queue<SignalContext>();
        PoweredTriggerIds = new HashSet<string>();
        IsRunning = false;
        LoadTime = Time.time;
    }

    /// <summary>
    /// 向图中注入一个信号喵~
    /// </summary>
    public void InjectSignal(SignalContext signal)
    {
        ActiveSignals.Enqueue(signal);
    }

    /// <summary>
    /// 获取指定类型的节点列表喵~
    /// </summary>
    public List<T> GetNodesOfType<T>() where T : BaseNodeData
    {
        return NodeMap.Values.OfType<T>().ToList();
    }

    /// <summary>
    /// 根据 ID 获取节点喵~
    /// </summary>
    public T GetNode<T>(string nodeID) where T : BaseNodeData
    {
        if (NodeMap.TryGetValue(nodeID, out var node) && node is T tNode)
        {
            return tNode;
        }
        return null;
    }

    /// <summary>
    /// 清空所有信号喵~
    /// </summary>
    public void ClearSignals()
    {
        ActiveSignals.Clear();
    }

    /// <summary>
    /// 获取调试信息喵~
    /// </summary>
    public string GetDebugInfo()
    {
        return $"[RuntimeGraph: {InstanceID}] Type={GraphType}, Nodes={NodeMap.Count}, Signals={ActiveSignals.Count}, PoweredTriggers={PoweredTriggerIds.Count}";
    }
}
