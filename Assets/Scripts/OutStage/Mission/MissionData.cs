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

[Serializable]
public enum TriggerType { Time, MissionCompleted, AreaReached }

[Serializable]
public enum ActionType { SpawnUnits, SetAITarget, ShowDialogue, ToggleGlobalPower }

[Serializable]
public class ScenarioEventData
{
    public string EventID;
    [HideInInspector] public Vector2 EditorPosition;
    public TriggerType Trigger;
    public string TriggerParam;
    public bool HasTriggered;

    // [新增] 连向的行为节点 ID
    public string NextSpawnID;
}

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
    public List<MissionData> Missions = new List<MissionData>();
    // --- [NEW] 专门存奖励节点及其位置 ---
    public List<RewardSaveData> Rewards = new List<RewardSaveData>();
    public List<ScenarioEventData> ScenarioEvents = new List<ScenarioEventData>();
    public List<SpawnActionData> SpawnActions = new List<SpawnActionData>();
    public List<AIBrainActionData> AIBrainActions = new List<AIBrainActionData>();
}
[Serializable]
public class RewardSaveData
{
    public string RewardID;
    public Vector2 Position;   // 存位置喵
    public MissionReward Data; // 存具体的奖励内容
}