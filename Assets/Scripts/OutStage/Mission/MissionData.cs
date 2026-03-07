using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum MissionPriority
{
    Main,       // 主线：必须完成才能推进剧情或通关
    Side,       // 支线：可选，提供额外资源
    Tutorial    // 引导：教学性质
}

[Serializable]
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

// TriggerType 已移动到 Common/Trigger/TriggerType.cs 中统一喵~

[Serializable]
public enum ActionType { SpawnUnits, SetAITarget, ShowDialogue, ToggleGlobalPower }

// TriggerEventData 已删除，统一使用 TriggerNodeData（定义在 StoryData.cs 中）
// TriggerNodeData 包含以下字段：
// - NodeID (节点唯一 ID)
// - EditorPosition (编辑器位置)
// - Trigger (TriggerData 触发器数据)
// - NextNodeIDs (下一个节点 ID 列表，用于一对多连线)
// - NextSpawnID (连向的召唤节点 ID，Mission 系统专用)

// --- [新增] 召唤动作数据 ---
[Serializable]
public class SpawnUnitEntry { public string BlueprintId; public int Count; }

[Serializable]
public class SpawnActionData
{
    public string SpawnID;
    [HideInInspector] public Vector2 EditorPosition;
    public int Team;
    public Vector2Int SpawnPos;
    public List<SpawnUnitEntry> Units = new List<SpawnUnitEntry>();

    // 连向的 AI 节点 ID
    public string AttachAIBrainID;
}

// --- [新增] AI 节点数据 ---
[Serializable]
public class AIBrainActionData
{
    public string BrainNodeID;
    [HideInInspector] public Vector2 EditorPosition;
    public string BrainIdentifier; // 如 "Red_Dot_Wave"
    public Vector2Int TargetPos;
}

// --- 任务奖励：用于挂钩 GlobalProgression ---
[Serializable]
public class MissionReward
{
    public long Money;              // 给钱
    public int TechPoints;           // 给科技点
    public List<string> Blueprints;  // 解锁图纸 Key
}

[Serializable]
public class MissionGoal
{
    public GoalType Type;
    public string TargetKey;
    public long RequiredAmount;
    public long CurrentAmount;
    public bool IsReached => CurrentAmount >= RequiredAmount;
}

[Serializable]
public class MissionData
{
    [Header("基本信息")]
    public string MissionID;
    public string Title;
    public string Description;
    public MissionPriority Priority; // 区分主支线
    [HideInInspector] public Vector2 EditorPosition; // 仅供编辑器记录位置

    [Header("状态")]
    public bool IsActive;            // 是否已激活（用于连环任务中的开启控制）
    public bool IsCompleted;
    public bool IsFailed;

    [Header("任务目标")]
    public List<MissionGoal> Goals = new List<MissionGoal>();

    [Header("奖励")]
    public MissionReward Reward;     // 任务完成时的奖励内容
    // 🔥 [NEW] 这个用于编辑器记录它连向了哪个 RewardNode
    public string RewardID;

    [Header("连环逻辑")]
    public string NextMissionID;     // 完成后自动激活的下一个任务 ID
}

[System.Serializable]
public class MissionPackData
{
    public string PackID;
    [Tooltip("绑定的关卡ID，如果为空则表示通用任务包")]
    public string BoundStageID;
    public List<MissionData> Missions = new List<MissionData>();
    // --- [NEW] 专门存奖励节点及其位置 ---
    public List<RewardSaveData> Rewards = new List<RewardSaveData>();
    // [触发器节点列表] - 统一使用 TriggerNodeData
    public List<TriggerNodeData> Triggers = new List<TriggerNodeData>();
    public List<SpawnActionData> SpawnActions = new List<SpawnActionData>();
    public List<AIBrainActionData> AIBrainActions = new List<AIBrainActionData>();
    public List<MapNodeData> MapNodes = new List<MapNodeData>();
}

[Serializable]
public class MapNodeData
{
    public string NodeID;                    // 节点唯一ID
    [HideInInspector] public Vector2 EditorPosition; // 编辑器位置
    public string MapID;                     // 地图ID（关卡ID）
    public Vector2Int SelectedPosition;      // 选中的地图坐标
    public string PositionName;              // 位置别名（可选）
    public List<string> ConnectedSpawnIds = new List<string>();   // 连接的召唤节点ID
    public List<string> ConnectedBrainIds = new List<string>();   // 连接的AI节点ID
    public bool IsBoundNode;                 // 是否为绑定地图节点（没有端口，只能选择任务包绑定地图）
}

[Serializable]
public class RewardSaveData
{
    public string RewardID;
    public Vector2 Position;   // 存位置喵
    public MissionReward Data; // 存具体的奖励内容
}