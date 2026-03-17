using System;
using System.Collections.Generic;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// GraphRunner 存档系统 - 图运行时状态快照喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 设计理念：
/// 1. 图的状态 = 信号队列 + 阻隔节点状态
/// 2. 只保存正在运行的图实例 (IsRunning = true)
/// 3. 阻隔状态由策略决定 (IBlockingNodeStrategy)
/// 4. 信号路径独立记录，分叉时新建路径列表
///
/// 存档内容：
/// - ActiveSignals: 当前等待处理的信号队列（带路径记录）
/// - BlockingNodes: 阻隔节点的当前状态（由策略捕获）
///
/// 不存档的内容：
/// - 运行时句柄（如 TriggerData 的监听委托）
/// - 读档后重新注册监听即可
/// ═══════════════════════════════════════════════════════════════
/// </summary>

/// <summary>
/// 图实例快照 - 单个图实例的完整状态喵~
/// </summary>
[Serializable]
public class GraphInstanceSnapshot
{
    /// <summary>
    /// 图实例唯一 ID
    /// </summary>
    public string InstanceID;

    /// <summary>
    /// Pack 唯一 ID（用于重载）
    /// </summary>
    public string PackID;

    /// <summary>
    /// 图类型（Mission/Story...）
    /// </summary>
    public string GraphType;


    /// <summary>
    /// 源 JSON 文件名（用于从 MetaLib 查找原始 Pack）
    /// 例如："NekoGraph/Packs/tech_pack_001"
    /// </summary>
    public string SourceJsonFileName;

    /// <summary>
    /// 当前活跃的信号队列快照
    /// </summary>
    public List<SignalContextSnapshot> ActiveSignals = new List<SignalContextSnapshot>();

    /// <summary>
    /// 阻隔节点状态快照（只有实现 IBlockingNodeStrategy 的节点才需要保存）
    /// </summary>
    public List<NodeBlockingSnapshot> BlockingNodes = new List<NodeBlockingSnapshot>();
}

/// <summary>
/// 信号上下文快照 - 可序列化的信号数据喵~
/// </summary>
[Serializable]
public class SignalContextSnapshot
{
    /// <summary>
    /// 当前节点 ID（信号正在处理的节点）
    /// </summary>
    public string CurrentNodeId;

    /// <summary>
    /// 信号携带的数据（JSON 序列化）
    /// </summary>
    public string ArgsJson;

    /// <summary>
    /// 信号已走过的路径（分叉时新建列表，独立记录）
    /// </summary>
    public List<ConnectionDataSnapshot> TraveledPath = new List<ConnectionDataSnapshot>();
}

/// <summary>
/// 阻隔节点状态快照 - 由策略捕获的节点状态喵~
/// </summary>
[Serializable]
public class NodeBlockingSnapshot
{
    /// <summary>
    /// 节点唯一 ID
    /// </summary>
    public string NodeID;

    /// <summary>
    /// 阻隔状态数据（JSON 序列化，具体结构由策略决定）
    /// </summary>
    public string BlockingStateJson;
}

/// <summary>
/// 连线数据快照 - 可序列化的连线数据喵~
/// </summary>
[Serializable]
public class ConnectionDataSnapshot
{
    /// <summary>
    /// 源节点 ID
    /// </summary>
    public string SourceNodeID;

    /// <summary>
    /// 目标节点 ID
    /// </summary>
    public string TargetNodeID;

    /// <summary>
    /// 源端口索引
    /// </summary>
    public int SourcePortIndex;

    /// <summary>
    /// 目标端口索引
    /// </summary>
    public int TargetPortIndex;

    public ConnectionDataSnapshot() { }

    /// <summary>
    /// 从 ConnectionData 创建快照喵~
    /// </summary>
    public ConnectionDataSnapshot(ConnectionData conn)
    {
        SourceNodeID = conn.SourceNodeID;
        TargetNodeID = conn.TargetNodeID;
        SourcePortIndex = conn.FromPortIndex;
        TargetPortIndex = conn.ToPortIndex;
    }

    /// <summary>
    /// 转换为 ConnectionData 喵~
    /// </summary>
    public ConnectionData ToConnectionData()
    {
        return new ConnectionData(SourceNodeID, SourcePortIndex, TargetNodeID, TargetPortIndex);
    }
}
