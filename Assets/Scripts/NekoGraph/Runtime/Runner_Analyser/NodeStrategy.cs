using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NekoGraph;

/// <summary>
/// 节点策略抽象基类 - 提取公共逻辑喵~
/// </summary>
public abstract class NodeStrategy
{
    /// <summary>
    /// 注入信号到指定节点喵~（O(1) 操作）
    /// 同时标记当前节点为已检查喵~
    /// </summary>
    protected void EnqueueSignal(BasePackData pack, string targetNodeId, SignalContext context)
    {
        // 标记当前节点已被信号检查喵~
        if (pack.Nodes.TryGetValue(context.CurrentNodeId, out var currentNode))
        {
            currentNode.IsChecked = true;
        }

        var newSignal = context.Clone();
        newSignal.CurrentNodeId = targetNodeId;
        pack.ActiveSignals.Enqueue(newSignal);
    }

    /// <summary>
    /// 注入信号到多个目标节点喵~
    /// </summary>
    protected void EnqueueSignals(BasePackData pack, IEnumerable<string> targetIds, SignalContext context)
    {
        foreach (var targetId in targetIds)
        {
            EnqueueSignal(pack, targetId, context);
        }
    }

    /// <summary>
    /// 注入信号到连接列表的目标节点喵~
    /// </summary>
    protected void EnqueueSignals(BasePackData pack, List<ConnectionData> connections, SignalContext context)
    {
        foreach (var conn in connections)
        {
            EnqueueSignal(pack, conn.TargetNodeID, context);
        }
    }

    /// <summary>
    /// 子类实现：处理信号进入节点喵~
    /// </summary>
    public abstract void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID);

    /// <summary>
    /// 子类实现：处理外部事件喵~
    /// </summary>
    public abstract void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID);
}

/// <summary>
/// 阻隔节点策略接口 - 标记需要存档的阻隔节点喵~
/// </summary>
public interface IBlockingNodeStrategy
{
    object CaptureBlockingState(BaseNodeData node);
    void RestoreBlockingState(BaseNodeData node, object state);
}

/// <summary>
/// 节点策略工厂 - 根据节点类型创建对应的处理器喵~
/// </summary>
public static class NodeStrategyFactory
{
    private static Dictionary<Type, NodeStrategy> _strategyMap;

    static NodeStrategyFactory()
    {
        _strategyMap = new Dictionary<Type, NodeStrategy>();
        RegisterDefaultStrategies();
    }

    private static void RegisterDefaultStrategies()
    {
        Register<RootNodeData>(new RootNodeStrategy());
        Register<SpineNodeData>(new SpineNodeStrategy());
        Register<LeafNode_A_Data>(new LeafNodeAStrategy());
        Register<LeafNode_B_Data>(new LeafNodeBStrategy());
        Register<MissionNode_A_Data>(new MissionNodeAStrategy());
        Register<MissionNode_S_Data>(new MissionNodeSStrategy());
        Register<MissionNode_F_Data>(new MissionNodeFStrategy());
        Register<MissionNode_R_Data>(new MissionNodeRStrategy());
        Register<CommandNodeData>(new CommandNodeStrategy());
        Register<TriggerNodeData>(TriggerNodeStrategy.Instance);
        Register<SocialMsgContentNodeData>(new SocialMsgContentNodeStrategy());
        Register<ChoiceTextNodeData>(new ChoiceTextNodeStrategy());
        Register<SocialMsgEndNodeData>(new SocialMsgEndNodeStrategy());
        Register<DestroyNodeData>(new DestroyNodeStrategy());
    }

    public static void Register<T>(NodeStrategy strategy) where T : BaseNodeData
    {
        _strategyMap[typeof(T)] = strategy;
    }

    public static NodeStrategy GetStrategy(BaseNodeData data)
    {
        if (data == null) return null;

        var dataType = data.GetType();
        if (_strategyMap.TryGetValue(dataType, out var strategy))
        {
            return strategy;
        }

        var baseType = dataType.BaseType;
        while (baseType != null && baseType != typeof(BaseNodeData))
        {
            if (_strategyMap.TryGetValue(baseType, out strategy))
            {
                return strategy;
            }
            baseType = baseType.BaseType;
        }

        Debug.LogWarning($"[NodeStrategyFactory] 未找到节点类型 {dataType.Name} 的策略处理器喵~");
        return null;
    }

    public static void Clear()
    {
        _strategyMap.Clear();
    }
}
