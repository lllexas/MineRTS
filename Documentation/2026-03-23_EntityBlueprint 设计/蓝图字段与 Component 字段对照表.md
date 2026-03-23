# 蓝图字段与 Component 字段对照表

**日期：** 2026-03-23  
**目的：** 明确 `EntityBlueprint`（静态配置）与 ECS Component（运行时状态）之间的字段映射关系

---

## 核心设计原则

| 概念 | EntityBlueprint | ECS Component |
|------|-----------------|---------------|
| **性质** | 静态配置（配方） | 运行时状态（实例） |
| **生命周期** | 永久存储，不变化 | 随实体创建/销毁 |
| **数据特点** | 只读，共享 | 可变，独立 |
| **类比** | 类（Class） | 对象（Object） |

---

## CoreComponent 对照表

| CoreComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|-------------------|-------------|------|-----------|
| `bool Active` | ❌ 否 | 运行时状态，实体激活标志 | - |
| `EntityHandle SelfHandle` | ❌ 否 | 运行时分配的句柄 | - |
| `int Team` | ⚠️ 部分 | 运行时设置，但蓝图可定义默认阵营 | `DefaultTeam` |
| `int Type` | ✅ 是 | 单位类型位掩码 | `UnitType` |
| `SerializableVector2 Position` | ❌ 否 | 运行时位置 | - |
| `SerializableVector2Int Rotation` | ❌ 否 | 运行时旋转 | - |
| `SerializableVector2Int LogicSize` | ✅ 是 | 逻辑尺寸（1x1, 3x3 等） | `LogicSize` |
| `SerializableVector2 VisualScale` | ✅ 是 | 视觉缩放比例 | `VisualScale` |
| `string BlueprintName` | ❌ 否 | 运行时反向引用（指向蓝图 ID） | - |
| `int CreationIndex` | ❌ 否 | 运行时创建序号 | - |

---

## MoveComponent 对照表

| MoveComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|-------------------|-------------|------|-----------|
| `LogicalPosition` | ❌ 否 | 运行时位置 | - |
| `PreviousLogicalPosition` | ❌ 否 | 运行时位置 | - |
| `TargetGridPosition` | ❌ 否 | 运行时目标 | - |
| `MoveIntervalTicks` | ✅ 是 | 移动间隔（静态配置） | `MoveInterval` |
| `MoveTimerTicks` | ❌ 否 | 运行时计时器 | - |
| `StuckTimerTicks` | ❌ 否 | 运行时阻塞计时 | - |
| `LastVisualPosition` | ❌ 否 | 运行时视觉插值 | - |
| `IsFlyer` | ✅ 是 | 是否飞行单位 | `IsFlyer` |
| `Waypoints` | ❌ 否 | 运行时路径 | - |
| `WaypointIndex` | ❌ 否 | 运行时路径索引 | - |
| `NextStepTile` | ❌ 否 | 运行时战术目标 | - |
| `HasNextStep` | ❌ 否 | 运行时状态 | - |
| `IsBlocked` | ❌ 否 | 运行时阻塞状态 | - |
| `IsPathPending` | ❌ 否 | 运行时寻路状态 | - |
| `IsPathStale` | ❌ 否 | 运行时路径过期状态 | - |

---

## AttackComponent 对照表

| AttackComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|---------------------|-------------|------|-----------|
| `TargetEntityId` | ❌ 否 | 运行时锁定目标 | - |
| `AttackRange` | ✅ 是 | 攻击射程 | `AttackRange` |
| `AttackDamage` | ✅ 是 | 攻击力 | `AttackDamage` |
| `AttackCooldownTicks` | ✅ 是 | 攻击冷却 | `AttackCooldown` |
| `LastAttackTick` | ❌ 否 | 运行时上次攻击时间 | - |
| `WindUpTimer` | ❌ 否 | 运行时前摇计时 | - |
| `ProjectileSpriteId` | ✅ 是 | 子弹贴图 ID | `ProjectileSpriteId` |
| `ProjectileSpeed` | ✅ 是 | 子弹速度 | `ProjectileSpeed` |

---

## HealthComponent 对照表

| HealthComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|---------------------|-------------|------|-----------|
| `Health` | ❌ 否 | 运行时当前血量 | - |
| `MaxHealth` | ✅ 是 | 最大血量 | `MaxHealth` |
| `IsAlive` | ❌ 否 | 运行时存活状态 | - |
| `ExplodeOnDeath` | ✅ 是 | 死亡是否殉爆 | `ExplodeOnDeath` |
| `LastAttackerFaction` | ❌ 否 | 运行时最后攻击者 | - |

---

## SpawnComponent 对照表

| SpawnComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|---------------------|-------------|------|-----------|
| `SpawnBlueprint` | ✅ 是 | 生产什么单位（蓝图名） | `SpawnBlueprint` |
| `SpawnInterval` | ✅ 是 | 生产间隔 | `SpawnInterval` |
| `Timer` | ❌ 否 | 运行时计时器 | - |
| `MaxMinions` | ✅ 是 | 最大子单位数量 | `MaxMinions` |
| `CurrentMinions` | ❌ 否 | 运行时当前数量 | - |

---

## DrawComponent 对照表

| DrawComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|-------------------|-------------|------|-----------|
| `Matrix` | ❌ 否 | 运行时变换矩阵 | - |
| `SpriteId` | ✅ 是 | 贴图 ID | `SpriteId` |
| `TeamColor` | ❌ 否 | 运行时阵营颜色 | - |
| `AnimationFrame` | ❌ 否 | 运行时动画帧 | - |
| `IsSelected` | ❌ 否 | 运行时选中状态 | - |

---

## AIComponent 对照表

| AIComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|-----------------|-------------|------|-----------|
| `CurrentCommand` | ❌ 否 | 运行时当前指令 | - |
| `CurrentState` | ❌ 否 | 运行时当前状态 | - |
| `TargetEntity` | ❌ 否 | 运行时目标 | - |
| `CommandPos` | ❌ 否 | 运行时目标坐标 | - |
| `ScanTimer` | ❌ 否 | 运行时扫描计时 | - |
| `ScanRange` | ✅ 是 | 索敌半径 | `ScanRange` |

---

## WorkComponent 对照表

| WorkComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|-------------------|-------------|------|-----------|
| `WorkType` | ✅ 是 | 工作类型 | `WorkType` |
| `Progress` | ❌ 否 | 运行时进度 | - |
| `WorkSpeed` | ✅ 是 | 工作速度 | `WorkSpeed` |
| `DrillRange` | ✅ 是 | 矿机探测范围 | `DrillRange` |
| `RequiresPower` | ✅ 是 | 是否需要电力 | `RequiresPower` |
| `IsPowered` | ❌ 否 | 运行时通电状态 | - |
| `EnergyBuffer` | ❌ 否 | 运行时能量缓存 | - |
| `Task0-7` | ❌ 否 | 运行时任务 | - |

---

## InventoryComponent 对照表

| InventoryComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|------------------------|-------------|------|-----------|
| `InputSlotCount` | ✅ 是 | 输入槽数量 | `InputSlotCount` |
| `OutputSlotCount` | ✅ 是 | 输出槽数量 | `OutputSlotCount` |
| `Input0-3` | ⚠️ 部分 | 槽位结构，蓝图定义初始配置 | `InputSlots` |
| `Output0-3` | ⚠️ 部分 | 槽位结构，蓝图定义初始配置 | `OutputSlots` |

---

## PowerComponent 对照表

| PowerComponent 字段 | 是否在蓝图中 | 说明 | 蓝图字段名 |
|---------------------|-------------|------|-----------|
| `NetID` | ❌ 否 | 运行时电网 ID | - |
| `IsNode` | ✅ 是 | 是否为电网节点 | `IsPowerNode` |
| `SupplyRange` | ✅ 是 | 供电半径 | `SupplyRange` |
| `ConnRange` | ✅ 是 | 连接半径 | `ConnectionRange` |
| `Production` | ✅ 是 | 能量产出 | `EnergyGeneration` |
| `Demand` | ❌ 否 | 运行时需求 | - |
| `Capacity` | ✅ 是 | 蓄电池容量 | `EnergyCapacity` |
| `StoredEnergy` | ❌ 否 | 运行时存储能量 | - |
| `CurrentSatisfaction` | ❌ 否 | 运行时满足率 | - |

---

## 字段分类总结

### ✅ 蓝图字段（静态配置）

这些字段在蓝图中定义，运行时复制到 Component：

| 组件 | 蓝图字段数量 | 字段列表 |
|------|------------|---------|
| **Core** | 3 | `UnitType`, `LogicSize`, `VisualScale` |
| **Move** | 2 | `MoveInterval`, `IsFlyer` |
| **Attack** | 5 | `AttackRange`, `AttackDamage`, `AttackCooldown`, `ProjectileSpriteId`, `ProjectileSpeed` |
| **Health** | 2 | `MaxHealth`, `ExplodeOnDeath` |
| **Spawn** | 3 | `SpawnBlueprint`, `SpawnInterval`, `MaxMinions` |
| **Draw** | 1 | `SpriteId` |
| **AI** | 1 | `ScanRange` |
| **Work** | 4 | `WorkType`, `WorkSpeed`, `DrillRange`, `RequiresPower` |
| **Inventory** | 2 | `InputSlotCount`, `OutputSlotCount` |
| **Power** | 5 | `IsPowerNode`, `SupplyRange`, `ConnectionRange`, `EnergyGeneration`, `EnergyCapacity` |

**总计：约 28 个静态配置字段**

---

### ❌ 运行时字段（不在蓝图中）

这些字段由运行时系统管理，不在蓝图中定义：

| 类型 | 字段示例 |
|------|---------|
| **状态标志** | `Active`, `IsAlive`, `IsPowered`, `IsBlocked` |
| **运行时句柄** | `SelfHandle`, `TargetEntityId` |
| **位置/旋转** | `Position`, `LogicalPosition`, `Rotation` |
| **计时器** | `MoveTimerTicks`, `AttackCooldownTicks`, `ScanTimer` |
| **进度/缓存** | `Progress`, `EnergyBuffer`, `StoredEnergy` |
| **路径数据** | `Waypoints`, `NextStepTile`, `CurrentReservedPortal` |
| **任务队列** | `Task0-7`, `ActiveReservations` |

---

### ⚠️ 特殊情况

| 字段 | 说明 |
|------|------|
| `Team` | 蓝图可定义默认阵营，但运行时由生成方设置 |
| `InventorySlot` | 蓝图定义槽位结构和初始配置，运行时动态变化 |

---

## EntityBlueprint 结构建议

```csharp
/// <summary>
/// 实体蓝图 - 静态配置数据（配方）
/// </summary>
public class EntityBlueprint
{
    // === Core ===
    public int UnitType;
    public Vector2Int LogicSize;
    public Vector2 VisualScale;
    public int DefaultTeam;  // 可选，默认阵营

    // === Move ===
    public float MoveInterval;
    public bool IsFlyer;

    // === Attack ===
    public float AttackRange;
    public float AttackDamage;
    public float AttackCooldown;
    public int ProjectileSpriteId;
    public float ProjectileSpeed;

    // === Health ===
    public float MaxHealth;
    public bool ExplodeOnDeath;

    // === Spawn ===
    public string SpawnBlueprint;
    public float SpawnInterval;
    public int MaxMinions;

    // === Draw ===
    public int SpriteId;

    // === AI ===
    public float ScanRange;

    // === Work ===
    public WorkType WorkType;
    public float WorkSpeed;
    public int DrillRange;
    public bool RequiresPower;

    // === Inventory ===
    public int InputSlotCount;
    public int OutputSlotCount;

    // === Power ===
    public bool IsPowerNode;
    public float SupplyRange;
    public float ConnectionRange;
    public float EnergyGeneration;
    public float EnergyCapacity;
}
```

---

## 运行时实体创建流程

```
1. 读取 EntityBlueprint
   ↓
2. 创建 CoreComponent
   - Core.UnitType = blueprint.UnitType
   - Core.LogicSize = blueprint.LogicSize
   - Core.BlueprintName = blueprint.Id  // 反向引用
   - Core.Active = true  // 运行时默认值
   - Core.SelfHandle = EntitySystem.AllocateHandle()  // 运行时分配
   ↓
3. 创建 MoveComponent
   - Move.MoveIntervalTicks = ToTicks(blueprint.MoveInterval)
   - Move.IsFlyer = blueprint.IsFlyer
   ↓
4. 创建其他 Component...
   ↓
5. 实体注册到 ECS 世界
```

---

## 总结

| 对比项 | EntityBlueprint | ECS Component |
|--------|-----------------|---------------|
| **字段数量** | ~28 个静态字段 | ~100+ 个运行时字段 |
| **数据性质** | 只读配置 | 可变状态 |
| **存储方式** | VFS JSON 文件 | 内存 Struct 数组 |
| **生命周期** | 永久 | 随实体创建/销毁 |
| **共享性** | 多个实体共享同一蓝图 | 每个实体独立 Component |

**核心洞察：** 蓝图是"类定义"，Component 是"对象实例"。蓝图字段是 Component 字段的**静态子集**。
