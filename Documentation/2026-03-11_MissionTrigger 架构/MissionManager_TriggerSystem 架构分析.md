# MissionManager & TriggerSystem 架构分析

**文档日期**: 2026 年 3 月 11 日  
**分析版本**: 电子伏特协议重构版（进行中）  
**状态**: ⚠️ 待重构 - 硬骨头部分

---

## 📋 概述

本文档详细分析了任务系统（MissionManager）和触发器系统（TriggerSystem）的架构设计、方法功能和数据流转，为后续电子伏特协议重构提供参考。

---

# Part 1: MissionManager 任务管理系统喵~

## 🎯 核心职责

`MissionManager` 是任务系统的运行时管理器，负责：
1. 加载和管理任务包（MissionPack）
2. 追踪任务进度
3. 判定任务完成/失败
4. 发放奖励和激活连环任务
5. 判定关卡胜利条件

---

## 📊 数据结构

### 成员变量

| 变量名 | 类型 | 说明 |
|--------|------|------|
| `ActiveMissions` | `List<MissionData>` | 当前内存中所有活跃的任务（⚠️ 待改为 `MissionNode_A_Data`） |
| `LoadedPackNames` | `HashSet<string>` | 记录当前加载的所有任务包路径（支持多包并存） |

---

## 🔧 方法详解

### 1. Unity 生命周期

#### `Start()`
```csharp
private void Start()
{
    PostSystem.Instance.Register(this);
}
```
**功能**: 注册到全局事件系统（PostSystem），使 MissionManager 可以接收事件喵~

---

### 2. 任务包加载

#### `LoadMissionPack(string path, bool append = false)`
**功能**: 【通用加载入口】从 Resources 加载任意路径的任务剧本

| 参数 | 类型 | 说明 |
|------|------|------|
| `path` | `string` | Resources 下的路径，如 `"Missions/Tutorial"` |
| `append` | `bool` | `true`=保留现有任务；`false`=清空后加载 |

**执行流程**:
```
1. 如果 append=false → 调用 ClearCurrentMissions() 清空现有任务
2. 从 Resources 加载 JSON 资源
3. 反序列化为 MissionPackData
4. 调用 InitializeMissionPack() 缝合奖励数据
5. 将任务注入 ActiveMissions 列表
6. 发送 UI_MISSION_REFRESH 事件刷新 UI
7. 调用 TriggerSystem.LoadTrigger() 加载触发器
```

**关键代码**:
```csharp
// 1. 反序列化
MissionPackData pack = JsonUtility.FromJson<MissionPackData>(jsonAsset.text);

// 2. 核心：缝合奖励数据 (Stitching)
InitializeMissionPack(pack);

// 3. 注入活跃列表
ActiveMissions.AddRange(pack.Missions);

// 4. 加载触发器
TriggerSystem.Instance.LoadTrigger(pack);
```

---

#### `LoadMissionPackForStage(string stageID)`
**功能**: 根据关卡 ID 自动加载绑定的任务包

| 参数 | 类型 | 说明 |
|------|------|------|
| `stageID` | `string` | 关卡 ID（地图 ID） |

**执行流程**:
```
1. 检查 stageID 是否为空
2. 遍历 Resources/Missions 下所有任务包
3. 查找 BoundMap.MapID == stageID 的任务包
4. 调用 LoadMissionPack() 加载
```

---

#### `GetResourcePath(TextAsset asset)` (私有)
**功能**: 获取 TextAsset 在 Resources 下的相对路径

**返回值**: `"Missions/" + asset.name`

---

#### `InitializeMissionPack(MissionPackData pack)` (私有)
**功能**: 【内部核心】缝合逻辑 - 将独立存放的 Rewards 映射回 Missions

**执行流程**:
```
1. 检查 pack.Rewards 是否为空
2. 构建 ID 查找字典：rewardLookup = { NodeID → MissionReward }
3. 遍历所有 Mission
4. 如果 mission.RewardID 存在且在字典中找到 → 赋值 mission.Reward
```

**⚠️ 注意**: 此方法依赖已删除的 `MissionReward` 类型，重构后需要改用 Command 系统喵~

---

#### `ClearCurrentMissions()`
**功能**: 清空当前所有任务

```csharp
public void ClearCurrentMissions()
{
    ActiveMissions.Clear();
    LoadedPackNames.Clear();
}
```

---

### 3. 任务进度追踪

#### `UpdateGoalProgress(GoalType type, string key, long addAmount)` (私有)
**功能**: 更新所有活跃任务的指定目标进度

| 参数 | 类型 | 说明 |
|------|------|------|
| `type` | `GoalType` | 目标类型（建造/击杀/出售等） |
| `key` | `string` | 目标 Key（如 BlueprintID） |
| `addAmount` | `long` | 增加的数量 |

**执行流程**:
```
1. 遍历所有 ActiveMissions
2. 跳过未激活/已完成/已失败的任务
3. 查找匹配的 Goal（Type 和 TargetKey 都匹配）
4. 增加 CurrentAmount
5. 检查所有 Goal 是否都已完成
6. 如果都完成 → 调用 CompleteMission()
7. 发送 UI_MISSION_REFRESH 事件
```

**关键逻辑**:
```csharp
bool missionFinished = true;
foreach (var goal in mission.Goals)
{
    if (!goal.IsReached) missionFinished = false;
}
if (missionFinished) CompleteMission(mission);
```

---

### 4. 任务完成逻辑

#### `ForceCompleteActiveMissions()`
**功能**: 【调试专用】强行完成当前所有已激活但未完成的任务

**用途**: 调试时跳过任务喵~

**执行流程**:
```
1. 查找所有 IsActive=true 且 IsCompleted=false 的任务
2. 强行填满所有目标的 CurrentAmount = RequiredAmount
3. 调用 CompleteMission()
```

---

#### `CompleteMission(MissionData mission)` (私有)
**功能**: 【核心】完成任务并处理后续逻辑

**执行流程**:
```
1. 设置 mission.IsCompleted = true
2. 【发放奖励】发送 EVT_MISSION_REWARD 事件（包含 Reward 数据）
3. 【连环任务】查找并激活 NextMissionID 对应的下一个任务
4. 发送 UI_MISSION_COMPLETE 事件
5. 调用 CheckLevelVictory() 检查胜利条件
```

**⚠️ 注意**: 此方法依赖 `mission.Reward` 和 `mission.NextMissionID`，重构后需要改为从节点端口获取喵~

---

#### `CheckLevelVictory()` (私有)
**功能**: 【胜利判定】检查所有主线任务是否完成

**执行流程**:
```
1. 查找所有 Priority == Main 的任务
2. 检查是否全部 IsCompleted = true
3. 如果全部完成 → 发送 UI_LEVEL_VICTORY 事件
```

**关键代码**:
```csharp
bool allMainDone = ActiveMissions
    .Where(m => m.Priority == MissionPriority.Main)
    .All(m => m.IsCompleted);

if (allMainDone && ActiveMissions.Any(m => m.Priority == MissionPriority.Main))
{
    PostSystem.Instance.Send("UI_LEVEL_VICTORY");
}
```

---

### 5. 事件监听器

MissionManager 监听以下全局事件喵~：

| 事件名 | 方法 | 参数类型 | 功能 |
|--------|------|---------|------|
| `建筑完成` | `OnEntityBuilt()` | `string` (unitKey) | 更新建造目标进度 |
| `击败目标` | `OnEntityKilled()` | `string` (unitKey) | 更新击杀目标进度 |
| `出售资源` | `OnResourceSold()` | `MissionArgs` | 更新出售目标进度 |
| `金币更变` | `OnGoldGained()` | `long/int` | 更新赚钱目标进度 |
| `生存时间增加` | `OnSurviveTimeTick()` | `MissionArgs` | 更新生存时间进度 |

---

#### 事件处理器通用模式:
```csharp
[Subscribe("事件名")]
private void OnEvent(object data)
{
    if (data is 期望类型)
    {
        UpdateGoalProgress(GoalType.XXX, key, amount);
    }
    else
    {
        LogTypeError("事件名", "期望类型", data);
    }
}
```

---

#### `LogTypeError(string eventName, string expectedType, object receivedData)` (私有)
**功能**: 【错误日志】记录事件参数类型不匹配的错误

**输出示例**:
```
[Mission Error] 事件「建筑完成」参数异常！
期望类型：string, 实际收到：null。请检查发送源喵！
```

---

## 🔄 数据流转图

```
[JSON 任务包] 
    ↓ (LoadMissionPack)
[MissionPackData]
    ↓ (InitializeMissionPack - 缝合奖励)
[MissionData] × N
    ↓ (注入 ActiveMissions)
[运行时任务列表]
    ↓ (事件触发)
[UpdateGoalProgress]
    ↓ (进度检查)
[CompleteMission]
    ├→ 发放奖励 (EVT_MISSION_REWARD)
    ├→ 激活连环任务
    └→ 检查胜利 (CheckLevelVictory → UI_LEVEL_VICTORY)
```

---

# Part 2: TriggerSystem 触发器系统喵~

## 🎯 核心职责

`TriggerSystem` 是事件触发系统，负责：
1. 监听游戏事件（基于事件名）
2. 管理触发器索引（按事件名/来源 ID/时间）
3. 触发时执行动作（⚠️ 待重构为使用 Command 系统）

---

## 📊 数据结构

### 成员变量

| 变量名 | 类型 | 说明 |
|--------|------|------|
| `_listeningTriggers` | `HashSet<TriggerData>` | 所有正在监听的触发器集合 |
| `_byEventName` | `Dictionary<string, List<TriggerData>>` | 按事件名索引：EventName → List<TriggerData> |
| `_bySourceId` | `Dictionary<string, TriggerData>` | 按来源 ID 索引：SourceID → TriggerData |
| `_timeTriggers` | `SortedDictionary<float, List<TriggerData>>` | 时间触发器：时间戳 → List<TriggerData> |

**⚠️ 已删除**:
- `_spawnDict` - 召唤数据字典（已重构为使用 CommandNodeData）
- `_aiDict` - AI 数据字典（已重构为使用 CommandNodeData）
- `_nodeDataMap` - NodeID 映射（不再需要）

---

## 🔧 方法详解

### 1. Unity 生命周期

#### `Start()`
```csharp
private void Start()
{
    PostSystem.Instance.Register(this);
}
```
**功能**: 注册到全局事件系统喵~

---

#### `Update()`
```csharp
private void Update()
{
    if (TimeSystem.Instance == null || TimeSystem.Instance.IsPaused) return;
    ProcessTimeTriggers();
}
```
**功能**: 每帧处理时间触发器（检查是否到达触发时间）

---

### 2. 触发器加载与注册

#### `LoadTrigger(MissionPackData pack)`
**功能**: 加载任务包中的触发器数据

**执行流程**:
```
1. 清空旧数据 (ClearAllTriggers)
2. 遍历 pack.Triggers
3. 调用 RegisterTrigger() 注册每个触发器
```

**⚠️ 已重构**:
```csharp
// ❌ 旧代码（已删除）
_spawnDict = pack.SpawnActions?.ToDictionary(...);
_aiDict = pack.AIBrainActions?.ToDictionary(...);

// ✅ 新代码（待实现）
// 使用统一的 CommandNodeData 处理召唤和 AI 逻辑
```

---

#### `ClearAllTriggers()` (私有)
**功能**: 清空所有触发器索引

```csharp
private void ClearAllTriggers()
{
    _listeningTriggers.Clear();
    _byEventName.Clear();
    _bySourceId.Clear();
    _timeTriggers.Clear();
    // _nodeDataMap.Clear(); // 已重构删除
}
```

---

#### `RegisterTrigger(TriggerNodeData nodeData)`
**功能**: 注册单个触发器到索引系统

**执行流程**:
```
1. 重置 trigger.HasTriggered = false
2. 添加到监听集合 _listeningTriggers
3. 按事件名索引 → _byEventName
4. 按来源 ID 索引 → _bySourceId
   - 如果是 Time 触发器 → 添加到 _timeTriggers
   - 否则 → 添加到 _bySourceId
```

**关键代码**:
```csharp
// 按事件名索引
_byEventName[eventName].Add(trigger);

// 按来源 ID 索引（来源是第一个参数）
string sourceId = trigger.GetParam(0, null);
if (eventName == "Time" && float.TryParse(sourceId, out float time))
{
    _timeTriggers[time].Add(trigger);
}
else
{
    _bySourceId[sourceId] = trigger;
}
```

---

#### `RemoveTrigger(TriggerData trigger)`
**功能**: 移除触发器（从所有索引中删除）

**执行流程**:
```
1. 从 _listeningTriggers 移除
2. 从 _byEventName 移除
3. 从 _bySourceId 移除
```

---

### 3. 事件处理器

#### `OnMissionCompleted(object data)`
**功能**: 监听任务完成事件，触发后续触发器

**触发条件**: `UI_MISSION_COMPLETE` 事件

**执行流程**:
```
1. 检查 data 是否为 MissionData
2. 从 _bySourceId 查找 mission.MissionID 对应的触发器
3. 如果触发器未触发过 → 调用 TriggerEvent()
```

---

#### `OnUnitKilled(object data)`
**功能**: 监听单位被击败事件

**触发条件**: `击败目标` 事件

**执行流程**:
```
1. 检查 data 是否为 string (blueprintId)
2. 从 _bySourceId 查找 blueprintId 对应的触发器
3. 如果未触发 → 调用 TriggerEvent()
```

---

#### `OnAreaReached(object data)`
**功能**: 监听到达区域事件

**触发条件**: `单位进入区域` 事件

**执行流程**:
```
1. 检查 data 是否为 string (areaId)
2. 从 _bySourceId 查找 areaId 对应的触发器
3. 如果未触发 → 调用 TriggerEvent()
```

---

#### `OnCustomEvent(object data)`
**功能**: 监听自定义事件

**触发条件**: `CUSTOM_TRIGGER_EVENT` 事件

**执行流程**:
```
1. 检查 data 是否为 CustomTriggerEvent
2. 从 _byEventName 查找 "Custom" 事件名下的所有触发器
3. 检查触发器的自定义事件名是否匹配
4. 如果匹配且未触发 → 调用 TriggerEvent()
```

---

### 4. 时间触发器处理

#### `ProcessTimeTriggers()` (私有)
**功能**: 每帧检查并触发已到达时间的触发器

**执行流程**:
```
1. 获取当前时间 TimeSystem.Instance.TotalElapsedSeconds
2. 查找所有 Key <= currentTime 的时间点
3. 触发每个时间点下的所有未触发触发器
4. 从 _timeTriggers 移除已处理的时间点
```

**关键代码**:
```csharp
var triggeredTimes = _timeTriggers
    .Where(kvp => kvp.Key <= currentTime)
    .ToList();

foreach (var kvp in triggeredTimes)
{
    foreach (var trigger in kvp.Value.Where(t => !t.HasTriggered))
    {
        TriggerEvent(trigger);
    }
    _timeTriggers.Remove(kvp.Key);
}
```

---

### 5. 核心执行逻辑

#### `TriggerEvent(TriggerData trigger)` (私有)
**功能**: 【核心】触发单个事件并执行动作

**当前状态**: ⚠️ **待重构**

**旧逻辑** (已删除):
```csharp
// ❌ 旧代码
// 1. 查找 spawnDict 中的召唤数据
// 2. 执行单位召唤
// 3. 绑定 AI 到召唤的单位
```

**新逻辑** (待实现):
```csharp
// ✅ 待实现：使用统一的 CommandRegistry 执行命令
// TODO: 实现新的命令执行逻辑
```

**当前代码**:
```csharp
private void TriggerEvent(TriggerData trigger)
{
    trigger.HasTriggered = true;
    // TODO: 实现新的命令执行逻辑
    Debug.Log($"触发事件：{trigger.EventName}");
}
```

---

### 6. 辅助数据结构

#### `CustomTriggerEvent` (内部类)
```csharp
public class CustomTriggerEvent
{
    public string EventName;      // 自定义事件名
    public object[] Parameters;   // 参数数组
}
```
**功能**: 用于触发自定义事件的数据结构喵~

---

## 🔄 数据流转图

```
[MissionPackData.Triggers]
    ↓ (LoadTrigger)
[TriggerNodeData] × N
    ↓ (RegisterTrigger)
[索引系统]
├─ _byEventName: EventName → List<TriggerData>
├─ _bySourceId: SourceID → TriggerData
└─ _timeTriggers: 时间戳 → List<TriggerData>

[游戏事件] → [PostSystem]
    ↓
[事件处理器 OnEvent()]
    ↓ (从索引查找触发器)
[TriggerEvent()]
    ↓ (⚠️ 待实现)
[CommandRegistry 执行命令]
```

---

# Part 3: 重构待办事项喵~

## ⚠️ MissionManager 重构要点

| 项目 | 当前状态 | 重构目标 |
|------|---------|---------|
| `ActiveMissions` | `List<MissionData>` | 改为 `List<MissionNode_A_Data>` |
| `InitializeMissionPack` | 缝合 Reward 数据 | 删除（奖励改用 Command 系统） |
| `CompleteMission` | 发放 Reward + 激活连环任务 | 改为从 OutPutNodeIDs 查找后续节点 |
| `CheckLevelVictory` | 检查主线任务完成 | 保持不变，但数据源改为 MissionNode_A_Data |
| 事件监听器 | 更新 MissionData.Goals | 改为更新 MissionNode_A_Data.Goals |

---

## ⚠️ TriggerSystem 重构要点

| 项目 | 当前状态 | 重构目标 |
|------|---------|---------|
| `TriggerEvent` | 执行召唤+AI 绑定（已删除） | 使用 CommandRegistry 执行命令 |
| 事件处理器 | 查找 MissionData | 改为查找 MissionNode_A_Data |
| 索引系统 | 按 MissionID 索引 | 按 NodeID 索引 |

---

## 📝 电子伏特协议适配

### Mission 节点信号流转:
```
Trigger → MissionNode_A (信号经入)
                ↓
         [任务执行中...]
                ↓
         MissionNode_S (成功) → Command (奖励)
                ↓
         MissionNode_F (失败)
```

### 连环任务实现:
```csharp
// 从 MissionNode_A 的 OutPutNodeIDs 查找下一个任务
var nextMissionId = missionNode.OutPutNodeIDs[0];
var nextMission = ActiveMissionNodes.Find(m => m.NodeID == nextMissionId);
nextMission.IsActive = true;
```

---

## 🎯 总结

**MissionManager** 和 **TriggerSystem** 是任务系统的两大核心模块：

- **MissionManager**: 管理任务生命周期（加载→追踪→完成→奖励）
- **TriggerSystem**: 监听事件并触发执行（索引→查找→触发→执行）

**重构关键**:
1. 数据类型从 `MissionData` 改为 `MissionNode_A_Data`
2. 奖励系统从 `MissionReward` 改为 `CommandNodeData`
3. 连环任务从 `NextMissionID` 改为从 `OutPutNodeIDs` 获取
4. 触发执行从硬编码召唤改为 `CommandRegistry` 统一执行

---

*文档由 NekoGraph 架构分析工具生成喵~ (=^･ω･^=)*
