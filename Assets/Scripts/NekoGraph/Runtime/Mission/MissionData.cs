using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

#if UNITY_EDITOR
using System.Reflection;
#endif

// =========================================================
// Mission 系统数据类型定义
// =========================================================
//
// 【重构后架构说明】
//
// 任务逻辑由流程图驱动，不再硬编码在 MissionNode 中喵~
//
// 流程图示例：
// ┌───────────┐    ┌────────────────┐    ┌───────────┐    ┌───────────┐
// │ Mission_A │───▶│ TriggerNode    │───▶│ Command   │───▶│ Mission_S │
// │  激活任务  │    │ (监听事件/条件) │    │ (执行动作) │    │  完成任务  │
// └───────────┘    └────────────────┘    └───────────┘    └───────────┘
//
// 职责划分：
// - MissionNode_A/S/F/R: 状态管理（激活/完成/失败/重置）
// - TriggerNode: 条件监听（监听事件、检查条件、触发信号）
// - CommandNode: 执行动作（发放奖励、修改资源、标记完成）
//
// =========================================================

/// <summary>
/// 任务优先级喵~
/// </summary>
[Serializable]
public enum MissionPriority
{
    Main,       // 主线：必须完成才能推进剧情或通关
    Side,       // 支线：可选，提供额外资源
    Tutorial    // 引导：教学性质
}

/// <summary>
/// 任务目标类型喵~
///
/// 【已废弃】
/// 旧的 Goal 驱动架构已废弃，现在使用流程图驱动架构。
/// 任务目标由 TriggerNode + CommandNode 组合实现。
///
/// 保留此枚举仅用于兼容旧数据，新代码请勿使用喵~
/// </summary>
[Serializable]
[Obsolete("已废弃：请使用流程图驱动架构，任务目标由 TriggerNode + CommandNode 实现喵~")]
public enum GoalType
{
    BuildEntity,
    KillEntity,
    SellResource,
    /// <summary>
    /// 未实现
    /// </summary>
    ReachPosition,
    SurviveTime,
    EarnMoney
}

/// <summary>
/// 动作类型喵~
///
/// 【已废弃】
/// 旧的 ActionType 已废弃，现在使用 CommandNode 执行动作。
/// 请在 CommandRegistry 中定义具体的命令喵~
/// </summary>
[Serializable]
[Obsolete("已废弃：请使用 CommandNode 执行动作，命令在 CommandRegistry 中定义喵~")]
public enum ActionType { SpawnUnits, SetAITarget, ShowDialogue, ToggleGlobalPower }


// =========================================================
// 【已废弃】旧架构数据类型
// =========================================================
// 以下数据类型已废弃，保留仅用于兼容旧数据
// 新代码请使用流程图驱动架构喵~

/// <summary>
/// 任务目标数据喵~
/// 【已废弃】旧 Goal 驱动架构的产物
/// </summary>
[Serializable]
[Obsolete("已废弃：请使用流程图驱动架构，任务目标由 TriggerNode + CommandNode 实现喵~")]
public class MissionGoal
{
    public GoalType Type;
    public string TargetKey;
    public long RequiredAmount;
    public long CurrentAmount;
    public bool IsReached => CurrentAmount >= RequiredAmount;
}

// =========================================================
// Mission 节点数据类型（仍在使用）
// =========================================================

/// <summary>
/// 任务数据 - 继承 BaseNodeData 喵~
/// 任务发起点（激活点）
///
/// 职责：
/// - 收到信号 → 激活任务 (IsActive = true)
/// - 向后续节点传播信号（通常是 TriggerNode）
///
/// 注意：
/// - 不再包含 Goals 列表！任务目标由流程图定义
/// - 不再监听事件！事件监听由 TriggerNode 负责
/// </summary>
[Serializable]
public class MissionNode_A_Data : BaseNodeData
{
    [Header("基本信息")]
    public string MissionID;
    public string Title;
    public string Description;
    public MissionPriority Priority; // 区分主支线

    [Header("状态")]
    public bool IsActive;            // 是否已激活（用于连环任务中的开启控制）
    public bool IsCompleted;
    public bool IsFailed;

    [InPort(0, "信号经入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs;

    [OutPort(0, "信号经出", NekoPortCapacity.Multi)]
    public List<string> OutPutNodeIDs;
}

/// <summary>
/// 任务成功点喵~
///
/// 职责：
/// - 收到信号 → 标记任务完成 (IsCompleted = true)
/// - 发送 UI 完成信号
/// - 向后续节点传播信号（可能是奖励 CommandNode）
/// </summary>
[Serializable]
public class MissionNode_S_Data : BaseNodeData
{
    public string MissionID;

    [InPort(0, "信号经入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs;

    [OutPort(0, "信号经出", NekoPortCapacity.Multi)]
    public List<string> OutPutNodeIDs;
}

/// <summary>
/// 任务失败点喵~
///
/// 职责：
/// - 收到信号 → 标记任务失败 (IsFailed = true)
/// - 发送 UI 失败信号
/// - 向后续节点传播信号（可能是惩罚 CommandNode 或剧情分支）
/// </summary>
[Serializable]
public class MissionNode_F_Data : BaseNodeData
{
    public string MissionID;

    [InPort(0, "信号经入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs;

    [OutPort(0, "信号经出", NekoPortCapacity.Multi)]
    public List<string> OutPutNodeIDs;
}

/// <summary>
/// 任务刷新点喵~
///
/// 职责：
/// - 收到信号 → 重置任务状态
/// - 可选：重新激活任务
/// - 发送 UI 刷新信号
///
/// 注意：
/// - ResetProgress 已废弃，进度重置应由 CommandNode 处理
/// </summary>
[Serializable]
public class MissionNode_R_Data : BaseNodeData
{
    public string MissionID;

    [Header("重置选项")]
    [Obsolete("已废弃：请使用 CommandNode 重置进度喵~")]
    public bool ResetProgress;      // 重置进度（已废弃）
    public bool Reactivate;         // 重新激活

    [InPort(0, "信号经入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs;

    [OutPort(0, "信号经出", NekoPortCapacity.Multi)]
    public List<string> OutPutNodeIDs;
}


// =========================================================
// 地图节点数据类型
// =========================================================

/// <summary>
/// 地图节点数据 - Mission 系统专用喵~
/// 用于表示大地图上的一个关卡节点
/// </summary>
[Serializable]
public class MapNodeData : BaseNodeData
{
    public string MapID;                     // 地图 ID（关卡 ID）
    public Vector2Int SelectedPosition;      // 选中的地图坐标
    public string PositionName;              // 位置别名（可选）

    [OutPort(0, "坐标输出", NekoPortCapacity.Multi)]
    public List<string> ConnectedSpawnIds = new List<string>();
}

/// <summary>
/// 绑定地图节点数据 - Mission 系统专用喵~
/// 用于指定任务包绑定的地图，没有输入输出端口喵~
/// </summary>
[Serializable]
public class BoundMapNodeData : BaseNodeData
{
    public string MapID;                     // 地图 ID（关卡 ID）
    public Vector2Int SelectedPosition;      // 选中的地图坐标
    public string PositionName;              // 位置别名（可选）
}

/// <summary>
/// 任务数据包 - 继承 BasePackData 喵~
///
/// 包含一个完整任务包的所有节点数据：
/// - MissionNodes: 任务节点（A/S/F/R）
/// - Triggers: 触发器节点（监听事件、检查条件）
/// - Commands: 命令节点（执行动作、发放奖励）
/// - MapNodes: 地图节点（大地图关卡）
/// - BoundMap: 绑定的地图信息
///
/// 【注】旧有的专用字段（如 MissionNodes、Triggers 等）保留用于向后兼容，
/// 新代码请直接使用基类的 Nodes 字段喵~
/// </summary>
[System.Serializable]
public class MissionPackData : BasePackData
{
    public BoundMapNodeData BoundMap; // 绑定地图节点（由基类反射自动填充喵~）

    // 任务节点列表
    public List<MissionNode_A_Data> MissionNodes_A = new List<MissionNode_A_Data>();

    // 专用节点列表
    public List<TriggerNodeData> Triggers = new List<TriggerNodeData>();
    public List<MapNodeData> MapNodes = new List<MapNodeData>();
    public List<CommandNodeData> Commands = new List<CommandNodeData>();
}
