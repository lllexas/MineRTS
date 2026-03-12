using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 节点策略接口 - 定义节点处理器的协议喵~
/// 每个节点类型都有一个对应的 Strategy 实现
/// </summary>
public interface INodeStrategy
{
    /// <summary>
    /// 处理信号进入节点时的逻辑喵~
    /// </summary>
    /// <param name="data">节点数据</param>
    /// <param name="context">信号上下文</param>
    /// <param name="instance">所属的图实例</param>
    void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance);

    /// <summary>
    /// 处理外部事件（如：击败目标、时间流逝等）喵~
    /// </summary>
    /// <param name="data">节点数据</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="instance">所属的图实例</param>
    void OnEvent(BaseNodeData data, string eventName, object eventData, RuntimeGraphInstance instance);
}

/// <summary>
/// 节点策略工厂 - 根据节点类型创建对应的处理器喵~
/// </summary>
public static class NodeStrategyFactory
{
    private static Dictionary<Type, INodeStrategy> _strategyMap;

    static NodeStrategyFactory()
    {
        _strategyMap = new Dictionary<Type, INodeStrategy>();
        RegisterDefaultStrategies();
    }

    /// <summary>
    /// 注册默认的节点策略喵~
    /// </summary>
    private static void RegisterDefaultStrategies()
    {
        // Root 节点
        Register<RootNodeData>(new RootNodeStrategy());
        
        // Spine 节点
        Register<SpineNodeData>(new SpineNodeStrategy());
        
        // Leaf 节点
        Register<LeafNode_A_Data>(new LeafNodeAStrategy());
        Register<LeafNode_B_Data>(new LeafNodeBStrategy());
        
        // Mission 节点
        Register<MissionNode_A_Data>(new MissionNodeAStrategy());
        Register<MissionNode_S_Data>(new MissionNodeSStrategy());
        Register<MissionNode_F_Data>(new MissionNodeFStrategy());
        Register<MissionNode_R_Data>(new MissionNodeRStrategy());
        
        // Command 节点
        Register<CommandNodeData>(new CommandNodeStrategy());

        // Trigger 节点 - 使用单例实例
        Register<TriggerNodeData>(TriggerNodeStrategy.Instance);
    }

    /// <summary>
    /// 注册单个策略喵~
    /// </summary>
    public static void Register<T>(INodeStrategy strategy) where T : BaseNodeData
    {
        _strategyMap[typeof(T)] = strategy;
    }

    /// <summary>
    /// 获取节点对应的策略喵~
    /// </summary>
    public static INodeStrategy GetStrategy(BaseNodeData data)
    {
        if (data == null) return null;
        
        var dataType = data.GetType();
        if (_strategyMap.TryGetValue(dataType, out var strategy))
        {
            return strategy;
        }
        
        // 尝试查找基类策略
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

    /// <summary>
    /// 清除所有注册的策略（用于重新加载）喵~
    /// </summary>
    public static void Clear()
    {
        _strategyMap.Clear();
    }
}
