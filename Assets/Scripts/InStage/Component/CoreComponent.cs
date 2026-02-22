using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public struct CoreComponent
{
    public bool Active;
    public EntityHandle SelfHandle;
    public int Team; // 是否属于玩家阵营，或者敌对阵营（可以用不同的数字表示不同的敌对阵营）
    public int Type; // 位掩码
    public Vector2 Position;
    public Vector2Int Rotation;
    // 逻辑尺寸：例如 1x1, 2x2, 3x3。用于 GridSystem 的占据计算和视觉中心偏移计算
    public Vector2Int LogicSize;

    // 视觉缩放：大部分建筑设为 (1,1) 即可，资源实体可以设为 (0.4, 0.4)
    public Vector2 VisualScale;
    // 蓝图名称，用于反查端口定义等静态数据
    public string BlueprintName; 
    // 建造序号 (或叫创建索引)
    public int CreationIndex;
}
public static class UnitType
{
    public const int None = 0;
    public const int Hero = 1 << 0; // 1: 英雄 (拥有碾压权限)
    public const int Minion = 1 << 1; // 2: 小兵
    public const int Building = 1 << 2; // 4: 建筑
    public const int ResourceItem = 1 << 3; // <--- 新增：掉落在地上的矿石或物资包
    public const int Projectile = 1 << 4; // <--- 【新增】实体子弹
    public const int Flyer = 1 << 5;      // <--- 【新增】飞行单位标记(辅助位)
    // ... 以后可以加 Flying, Boss 等
}

public struct MoveComponent
{
    // --- 逻辑数据 ---
    public Vector2Int LogicalPosition;    // 当前（或即将到达的）格子
    public Vector2Int PreviousLogicalPosition; // 正在离开的格子
    public Vector2Int TargetGridPosition; // 最终想要去的格子

    // --- 节奏控制 (Tick化) ---
    public int MoveIntervalTicks; // 移动一格所需的总Tick数 (例如：0.5s = 5 Ticks)
    public int MoveTimerTicks;    // 剩余Tick倒计时
    public int StuckTimerTicks; // 🔥 新增：用于记录连续被堵了多少个 Tick

    // --- 【新增：旧代码兼容访问器】 ---
    // 允许旧代码继续使用 .MoveInterval, .Timer, .BlockWaitTimer (float)

    public float MoveInterval => MoveIntervalTicks * TimeTicker.SecondsPerTick;

    public float Timer
    {
        get
        {
            float t = MoveTimerTicks * TimeTicker.SecondsPerTick - TimeTicker.SubTickOffset;
            return Mathf.Max(0, t);
        }
        set => MoveTimerTicks = TimeTicker.ToTicks(value);
    }

    public float BlockWaitTimer
    {   
        get
        {
            float t = BlockWaitTimerTicks * TimeTicker.SecondsPerTick - TimeTicker.SubTickOffset;
            return Mathf.Max(0, t);
        }
        set => BlockWaitTimerTicks = TimeTicker.ToTicks(value);
    }
    // ----------------------------------

    // --- 视觉插值 ---
    public Vector2 LastVisualPosition;

    public bool IsFlyer;

    //------
    // 【修改】核心路径数据
    // 旧的 List<Vector2Int> CurrentPath 被废弃。
    // 我们不再存储一整条确定的格子路径，而是存储“战略骨架”。

    // 1. 战略层 (Strategy)：由 PathfindingSystem 生成
    // 存储的是带有 RangeMin/RangeMax 的门户序列
    public List<PathfindingSystem.Waypoint> Waypoints;
    public int WaypointIndex; // 当前目标是 Waypoints[WaypointIndex]
    public Vector2Int CurrentReservedTile; // 用于 Gizmos 绘制预定路径

    // 2. 战术层 (Tactics)：由 ArbitrationSystem (仲裁系统) 生成
    // 仲裁系统每一帧(或每一步)会根据 Waypoints 和当前拥堵情况，计算出“当下这一步”具体踩哪
    public Vector2Int NextStepTile;
    public bool HasNextStep; // true 表示仲裁系统已经下达了下一步指令，MoveSystem 可以执行了

    // 3. 避让与状态 (Avoidance)
    // 用于实现类似星际的“撞墙-等待-侧滑”逻辑
    public bool IsBlocked;       // 当前是否因为拥堵而无法移动
    public int BlockWaitTimerTicks; // 阻塞等待Tick数
    //-----

    public bool IsPathPending; // 是否正在等待寻路结果
    public bool IsPathStale;   // 路径是否过期

    /// <summary>
    /// 当前正在前往的门后格子（如果非空，表示单位已预约并正在前往此出口）
    /// 到达后应推进 WaypointIndex
    /// </summary>
    public Vector2Int? TargetPortalExit;
    public NavPortal CurrentReservedPortal;  // 当前成功预约的门户
    public int CurrentReservedLane;          // 预约的车道索引
    public ulong CurrentReservedMask;        // 预约的掩码（用于释放）
    public List<ReservationRecord> ActiveReservations;
}

public struct AttackComponent
{
    public int TargetEntityId;      // 当前锁定的目标 ID

    // --- 攻击属性 (从蓝图复制，可能被Buff修改) ---
    public float AttackRange;
    public float AttackDamage;

    // --- Tick化攻击属性 ---
    public int AttackCooldownTicks;  // 攻击冷却tick数
    public long LastAttackTick;      // 上次攻击的tick

    // --- 兼容性属性 (秒为单位) ---
    public float AttackCooldown
    {
        get => AttackCooldownTicks * TimeTicker.SecondsPerTick;
        set => AttackCooldownTicks = TimeTicker.ToTicks(value);
    }

    public float LastAttackTime
    {
        get => LastAttackTick * TimeTicker.SecondsPerTick;
        set => LastAttackTick = TimeTicker.ToTicks(value); // ToTicks返回int，隐式转换为long
    }

    // --- 状态控制 ---
    public float WindUpTimer;       // 前摇计时 (可选，用于更细腻的手感)

    // --- 子弹模式 ---
    public int ProjectileSpriteId;  // -1: 近战直接结算; >=0: 发射子弹实体
    public float ProjectileSpeed;   // 子弹飞行速度
}

public struct HealthComponent
{
    public float Health;
    public float MaxHealth;
    public bool IsAlive;

    // --- 死亡行为 (从蓝图复制) ---
    public bool ExplodeOnDeath;     // 是否殉爆 (如: 自爆虫, 净化者)
    // 殉爆伤害通常直接取 AttackDamage，范围取 AttackRange
}
public struct ProjectileComponent
{
    public int SourceEntityId;      // 谁射的？(防止打到自己人，或者计算击杀统计)
    public int TargetEntityId;      // 锁定目标 (如果是追踪弹)
    public Vector2 TargetPosition;  // 目标地点 (如果是非追踪弹)

    public float Speed;
    public float Damage;
    public float HitRadius;         // 判定半径 (通常 0.5f)

    public bool IsHoming;           // 是否追踪
}
public struct SpawnComponent
{
    // 生产队列逻辑
    public string SpawnBlueprint; // <--- 修改：存蓝图名，而不是 Type
    public float SpawnInterval;
    public float Timer;

    // 限制最大子单位数量 (比如虫母最多带 5 个小虫)
    public int MaxMinions;
    public int CurrentMinions;
}

public struct DrawComponent
{
    public Matrix4x4 Matrix;
    public int SpriteId;
    public Color TeamColor;
    public float AnimationFrame;
    public bool IsSelected; // <--- 新增：是否被选中
}

public struct EntityHandle : IEquatable<EntityHandle>
{
    public int Id;      // 唯一的身份证号（对应查询表的下标）
    public int Version; // 版本号（防止复用ID时，旧的Handle指向了新的实体）

    public static EntityHandle None => new EntityHandle { Id = -1, Version = 0 };

    public bool Equals(EntityHandle other) => Id == other.Id && Version == other.Version;
    public override bool Equals(object obj) => obj is EntityHandle other && Equals(other);
    public override int GetHashCode() => Id * 397 ^ Version;
    public static bool operator ==(EntityHandle left, EntityHandle right) => left.Equals(right);
    public static bool operator !=(EntityHandle left, EntityHandle right) => !left.Equals(right);
}

public enum AIState
{
    Idle,      // 待机：啥也不干，或者寻找目标
    Moving,    // 移动：正朝着某个目的地走（可能是路点，也可能是追击目标）
    Attacking, // 攻击：目标在射程内，开始疯狂输出
    Fleeing,   // <--- 【新增】溃逃：血量低时反向逃跑
    Dead       // 死亡：等待回收
}
public enum UnitCommand
{
    None,       // 啥也不干
    Stop,       // 强行停止所有动作，原地待命
    Hold,       // 坚守阵地：原地不动，但攻击射程内的人
    Move,       // 纯移动：死也要走到终点，路边有人打我不还手
    AttackMove, // 进攻移动：往那走，但路上碰见人就停下来打，打完继续走
    AttackTarget // 锁定攻击：死磕这个目标，直到它死或者我死
}

public struct AIComponent
{
    public UnitCommand CurrentCommand; // 当前脊髓指令
    public AIState CurrentState;       // 脊髓当前的执行状态 (Idle, Moving, Attacking)

    public EntityHandle TargetEntity;  // 锁定的目标
    public Vector2Int CommandPos;      // 目标坐标 (Move 或 AttackMove 的终点)

    public float ScanTimer;            // 扫描计时器 (脊髓不需要每帧扫描，0.2s 一次足矣)
    public float ScanRange;            // 索敌半径
}
public struct UserControlComponent
{
    // 0: 无, 1-4: 对应快捷键槽位
    public int HeroSlot;
}

public struct ResourceComponent
{
    public int ResourceType; // 0: 无, 1: 铁矿, 2: 铜矿, 3: 加工后的补给物资
    public float Amount;     // 物资含量 (比如一坨矿石代表 5 个单位)
}

// 单个槽位的定义
[Serializable]
public struct InventorySlot
{
    public int ItemType;    // 0: 空
    public int Count;       // 当前数量
    public int MaxCapacity; // 堆叠上限
    public int Filter;      // 0: 无过滤（只要是空的就能进），>0: 只允许该 ID 物品
                            // --- 严谨的物流统计 ---
    public int EjectReserved; // 正在飞出去，但还没下账的数量
    public int GrabIncoming;  // 正在从外面飞进来，还没入账的数量

    // 真正能向外派发的物品数
    public int AvailableToEject => Count - EjectReserved;
    // 真正还能容纳的剩余空间（考虑了已经在路上的矿）
    public int AvailableSpace => MaxCapacity - (Count + GrabIncoming); public bool IsEmpty => Count <= 0;
    public bool IsFull => Count >= MaxCapacity;

    // 尝试添加物品
    // 返回值: 实际添加了多少个
    public int TryAdd(int itemType, int amount)
    {
        // 1. 如果槽位有东西，且类型不匹配 -> 滚蛋
        if (Count > 0 && ItemType != itemType) return 0;

        // 2. 如果槽位有过滤器，且类型不匹配 -> 滚蛋
        if (Filter > 0 && Filter != itemType) return 0;

        // 3. 计算能放多少
        int space = MaxCapacity - Count;
        if (space <= 0) return 0;

        int toAdd = Math.Min(space, amount);

        // 4. 执行添加
        if (Count == 0) ItemType = itemType; // 如果原来是空的，初始化类型
        Count += toAdd;

        return toAdd;
    }

    // 尝试移除物品
    public int TryRemove(int amount)
    {
        if (Count <= 0) return 0;
        int toRemove = Math.Min(Count, amount);
        Count -= toRemove;
        if (Count == 0) ItemType = 0; // 拿光了重置类型
        return toRemove;
    }
}

public struct InventoryComponent
{
    // 输入槽
    public int InputSlotCount;
    public InventorySlot Input0;
    public InventorySlot Input1;
    public InventorySlot Input2;
    public InventorySlot Input3;

    // 输出槽
    public int OutputSlotCount;
    public InventorySlot Output0;
    public InventorySlot Output1;
    public InventorySlot Output2;
    public InventorySlot Output3;
}

public enum HandoverMode { None, Eject, Grab }
public enum HandoverStatus { Flying, Returning }

public struct HandoverTask
{
    public bool Active;
    public HandoverMode Mode;   // 吐还是吸
    public HandoverStatus Status;
    public int ItemType;

    // 逻辑索引
    public int InvSlotIndex; // 对应 InputSlot 或 OutputSlot 的序号
    public int PortIndex;    // 对应 Blueprint.Ports[i] 的序号

    // 物理轨迹
    public Vector2 StartPos;
    public Vector2 EndPos;
    public float Progress;   // 0-1
    public float Speed;      // 1/Time

    // 目标传送带信息 (仅吐出时校验用)
    public int TargetLineID;
    public float TargetDistance;
}

// 【新增】ECS 风格的访问助手
public static class InventoryAccess
{
    // 必须传入 ref InventoryComponent，否则修改的是副本！
    public static ref InventorySlot GetInput(ref this InventoryComponent inv, int index)
    {
        switch (index)
        {
            case 0: return ref inv.Input0;
            case 1: return ref inv.Input1;
            case 2: return ref inv.Input2;
            case 3: return ref inv.Input3;
            default: throw new IndexOutOfRangeException();
        }
    }

    public static ref InventorySlot GetOutput(ref this InventoryComponent inv, int index)
    {
        switch (index)
        {
            case 0: return ref inv.Output0;
            case 1: return ref inv.Output1;
            case 2: return ref inv.Output2;
            case 3: return ref inv.Output3;
            default: throw new IndexOutOfRangeException();
        }
    }
}

public enum WorkType
{
    None,
    Drill,      // 矿机
    Generator,  // 发电机
    Conveyor,   // 传送带
    Factory,    // 加工厂/发电机
    Buffer,     // 缓存箱
    Battery,    // 电池
    Seller,     // 售卖箱
    PowerPole   // 电线杆/电网桩
}

public struct WorkComponent
{
    public WorkType WorkType;

    // --- 生产相关 ---
    public float Progress;      // 当前进度 (0-1)
    public float WorkSpeed;     // 生产速度

    // --- 矿机专属参数 ---
    public int DrillRange;      // 矿机探测线的长度

    public bool RequiresPower; // 是否需要电力才能工作
    public bool IsPowered;     // 当前是否通电 (由 PowerSystem 刷新)
    public float EnergyBuffer; // 蓄电池或机器内部存储的能量

    public HandoverTask Task0;
    public HandoverTask Task1;
    public HandoverTask Task2;
    public HandoverTask Task3;
    public HandoverTask Task4;
    public HandoverTask Task5;
    public HandoverTask Task6;
    public HandoverTask Task7;
}

public static class WorkComponentExtensions
{
    public static ref HandoverTask GetTask(ref this WorkComponent work, int index)
    {
        switch (index)
        {
            case 0: return ref work.Task0;
            case 1: return ref work.Task1;
            case 2: return ref work.Task2;
            case 3: return ref work.Task3;
            case 4: return ref work.Task4;
            case 5: return ref work.Task5;
            case 6: return ref work.Task6;
            case 7: return ref work.Task7;
            default: throw new IndexOutOfRangeException();
        }
    }
}

public enum PortType
{
    DirectIn,   // 直连吃：传送带的终点（被动接收）
    DirectOut,  // 直连吐：传送带的起点（主动喷出）
    SideIn,     // 侧接吃：主动检测邻居并抓取
    SideOut     // 侧接吐：主动检测邻居并塞入
}

public struct BuildingPort
{
    public Vector2Int Offset;    // 相对于建筑逻辑中心 (move.LogicalPosition) 的偏移
    public Vector2Int Direction; // 接口的朝向 (Vector2Int.up, down, left, right)
    public PortType Type;
    public int MapToSlotIndex;   // <--- 【关键新增】该端口关联的槽位序号
}

public struct ConveyorComponent
{
    public int LineID;        // 我属于哪条传输线？ (-1 表示不属于任何线)
    public int SegmentIndex;  // 我是这条线的第几个格子？

    // (未来可以加更多状态，比如 IsJammed, PowerStatus 等)
}

public struct PowerComponent
{
    // --- 状态 ---
    public int NetID;           // 所属电网ID，-1表示未联网

    // --- 属性 (来自蓝图) ---
    public bool IsNode;         // 是否为电网节点（供电桩/发电机/蓄电池）
    public float SupplyRange;   // 供电半径：覆盖消费建筑
    public float ConnRange;     // 连接半径：与其他节点连线并网

    public float Production;    // 能量产出 (J/s)
    public float Demand;        // 能量需求 (J/s)
    public float Capacity;      // 蓄电池容量 (J)
    public float StoredEnergy;  // 当前存储能量 (J)
    public float CurrentSatisfaction;
}
public class PowerNet
{
    public int NetID;
    public List<EntityHandle> Nodes = new List<EntityHandle>(); // 供电建筑（桩、发电机）
    public List<EntityHandle> Consumers = new List<EntityHandle>(); // 被覆盖的消费建筑

    public float TotalProduction;  // 总发电量/秒
    public float TotalDemand;      // 总需求量/秒
    public float TotalStorage;     // 总蓄电量上限
    public float CurrentStorage;   // 当前总蓄电量

    public float Satisfaction;     // 供电满足率 (0.0 - 1.0)

    public void ResetStats()
    {
        TotalProduction = 0;
        TotalDemand = 0;
        TotalStorage = 0;
        // CurrentStorage 保持持续
    }
}

/// <summary>
/// 围棋规则组件
/// 标记单位是否参与围棋规则，并记录当前气数
/// </summary>
public struct GoComponent
{
    /// <summary>
    /// 是否参与围棋规则
    /// 地面单位和建筑为true，飞行单位、子弹、掉落物等为false
    /// </summary>
    public bool IsGoPiece;

    /// <summary>
    /// 当前这块棋的气数（空相邻格子数量）
    /// 供UI显示或AI逃跑参考
    /// </summary>
    public int CurrentLiberties;
}
