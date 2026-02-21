using System.Collections.Generic;
using UnityEngine;

public struct EntityBlueprint
{
    // --- 基础信息 ---
    public string Name;
    public int SpriteId;
    public Vector2Int LogicSize;
    public Vector2 VisualScale;
    public int UnitType;
    public int Faction;            // <--- 【新增】阵营：0:协议军, 1:日之城, 2:盖亚黎明
    public float MaxHealth;

    // --- 战斗/移动核心参数 ---
    public float MoveInterval;     // 移速（秒/格）
    public float AttackRange;      // 射程（格，1.0~1.5算近战）
    public float AttackDamage;     // 伤害
    public float AttackCooldown;   // 攻击间隔（秒）

    // --- 子弹实体参数 ---
    public float ProjectileSpeed;  // 子弹飞多快
    public int ProjectileSpriteId; // 子弹长啥样 (-1 代表无子弹/近战)

    // --- 【新增】特殊战术标签 ---
    public bool IsFlyer;           // 是否飞行（无视地形）
    public bool ExplodeOnDeath;    // 死亡是否殉爆
    public float FleeHealthPercent;// 逃跑生命阈值 (0.0 - 1.0, 0表示死战不退)

    // --- 工业/生产参数 ---
    public WorkType WorkType;
    public float WorkSpeed;
    public int DrillRange;
    public bool RequiresPower;

    // --- 产兵参数 (虫母用) ---
    public string SpawnBlueprint;  // 生产什么单位 (填蓝图Key)
    public float SpawnInterval;    // 生产间隔

    // --- 电力系统参数 ---
    public bool IsPowerNode;
    public float SupplyRange;
    public float ConnectionRange;
    public float EnergyGeneration;
    public float IdleEnergy; // 【新增】待机/阻塞时的耗电 (比如 10W)
    public float WorkEnergy; // 【新增】全速工作时的耗电 (比如 100W)
    public float EnergyCapacity;

    // --- 库存配置 ---
    public int InputCount;
    public int OutputCount;
    public int DefaultCapacity;

    // 端口定义
    public BuildingPort[] Ports;
}

public static class BlueprintRegistry
{
    private static Dictionary<string, EntityBlueprint> _blueprints = new Dictionary<string, EntityBlueprint>();

    static BlueprintRegistry()
    {
        // =========================================================
        // 1. 矿机 (3x3) - 耗电大户
        // =========================================================
        _blueprints["miner"] = new EntityBlueprint
        {
            Name = "矿机",
            SpriteId = 0,
            LogicSize = new Vector2Int(3, 3),
            VisualScale = Vector2.one,
            UnitType = UnitType.Building,
            WorkType = WorkType.Drill,
            MaxHealth = 1000f,
            WorkSpeed = 1.0f,

            // --- 电力配置 ---
            RequiresPower = true,
            IsPowerNode = false,

            // 【数值设计】
            // 待机：10W (维持指示灯和控制电路)
            // 工作：120W (钻头全速旋转)
            IdleEnergy = 10.0f,
            WorkEnergy = 120.0f,

            InputCount = 0,
            OutputCount = 1,
            DefaultCapacity = 20,
            Ports = new BuildingPort[] {
                new BuildingPort { Offset = new Vector2Int(0, -1), Direction = new Vector2Int(0, -1), Type = PortType.DirectOut }
            }
        };

        // =========================================================
        // 2. 传送带 (1x1) - 暂时无需电力
        // =========================================================
        _blueprints["conveyor"] = new EntityBlueprint
        {
            Name = "传送带",
            SpriteId = 3,
            LogicSize = Vector2Int.one,
            VisualScale = Vector2.one,
            UnitType = UnitType.Building,
            WorkType = WorkType.Conveyor,
            MaxHealth = 100f,
            WorkSpeed = 2.0f,
            RequiresPower = false, // 机械传动，不需要电

            InputCount = 0,
            OutputCount = 0,
            DefaultCapacity = 0,
            Ports = new BuildingPort[] {
                new BuildingPort { Offset = Vector2Int.zero, Direction = new Vector2Int(0, -1), Type = PortType.DirectIn },
                new BuildingPort { Offset = Vector2Int.zero, Direction = new Vector2Int(0, 1), Type = PortType.DirectOut }
            }
        };

        // =========================================================
        // 3. 发电机 (3x3) - 核心修改
        // 特性：有范围、有连接、自带小电池、产电
        // =========================================================
        _blueprints["generator"] = new EntityBlueprint
        {
            Name = "发电机",
            SpriteId = 1,
            LogicSize = new Vector2Int(3, 3),
            VisualScale = Vector2.one,
            UnitType = UnitType.Building,
            WorkType = WorkType.Generator,
            MaxHealth = 1500f,

            // --- 电力配置 ---
            RequiresPower = false, // 自产自销
            IsPowerNode = true,

            SupplyRange = 6.0f,      // 范围适中，能覆盖紧贴着的两圈机器
            ConnectionRange = 12.0f, // 能连到稍微远一点的电线杆

            // 【数值设计】
            // 产出：500W (一台发电机 = 4 台满载矿机 + 20W余量)
            // 这样玩家配平的时候：4矿机+1发电机是经典组合，略有富余充电。
            EnergyGeneration = 500.0f,

            // 自身缓存：2000J (满载下能撑 4秒，主要是为了平滑燃烧判定)
            EnergyCapacity = 2000.0f,

            IdleEnergy = 0f,
            WorkEnergy = 0f,

            InputCount = 1,
            OutputCount = 0,
            DefaultCapacity = 50,
            Ports = new BuildingPort[] {
                new BuildingPort { Offset = new Vector2Int(0, 1), Direction = new Vector2Int(0, 1), Type = PortType.SideIn, MapToSlotIndex = 0 },
                new BuildingPort { Offset = new Vector2Int(0, -1), Direction = new Vector2Int(0, -1), Type = PortType.SideIn, MapToSlotIndex = 0 },
                new BuildingPort { Offset = new Vector2Int(-1, 0), Direction = new Vector2Int(-1, 0), Type = PortType.SideIn, MapToSlotIndex = 0 },
                new BuildingPort { Offset = new Vector2Int(1, 0), Direction = new Vector2Int(1, 0), Type = PortType.SideIn, MapToSlotIndex = 0 }
            }
        };

        // =========================================================
        // 4. 蓄电池 (2x2) - 纯电池+大节点
        // =========================================================
        _blueprints["battery"] = new EntityBlueprint
        {
            Name = "蓄电池",
            SpriteId = 5,
            LogicSize = new Vector2Int(2, 2),
            VisualScale = Vector2.one,
            UnitType = UnitType.Building,
            WorkType = WorkType.Battery,
            MaxHealth = 800f,

            IsPowerNode = true,
            RequiresPower = false,

            SupplyRange = 3.0f,      // 小范围供电
            ConnectionRange = 10.0f,

            // 【数值设计】
            // 容量：100,000 J (100kJ)
            // 计算：如果电网亏空 200W (比如发电机停了一半)，这块电池能撑 500秒。
            // 好像有点太强了？
            // 调整为：30,000 J (30kJ)
            // 计算：带 1 台矿机(120W) 能跑 250秒 (约4分钟)。
            // 带 10 台矿机(1200W) 能跑 25秒 (救急用)。
            EnergyCapacity = 30000.0f,

            InputCount = 0,
            OutputCount = 0,
            DefaultCapacity = 0,
            Ports = null
        };

        // =========================================================
        // 5. 电网桩 (1x1) - 纯连接
        // =========================================================
        _blueprints["power_pole"] = new EntityBlueprint
        {
            Name = "电网桩",
            SpriteId = 4,
            LogicSize = Vector2Int.one,
            VisualScale = Vector2.one,
            UnitType = UnitType.Building,
            WorkType = WorkType.PowerPole,
            MaxHealth = 50f,

            // --- 电力配置 ---
            IsPowerNode = true,
            RequiresPower = false, // 纯连接，不耗电

            // 【数值设计】
            // 覆盖半径 8格：这是一个非常舒服的距离，大概能覆盖 16x16 的区域
            // 连接距离 18格：允许玩家跨越小河或峡谷拉电线
            SupplyRange = 8.0f,
            ConnectionRange = 18.0f,

            EnergyGeneration = 0f,
            EnergyCapacity = 0f,
            IdleEnergy = 0f,
            WorkEnergy = 0f,

            InputCount = 0,
            OutputCount = 0,
            DefaultCapacity = 0,
            Ports = null
        };

        // =========================================================
        // 6. 缓存箱 (2x2) - 不耗电
        // =========================================================
        _blueprints["buffer"] = new EntityBlueprint
        {
            Name = "缓存箱",
            SpriteId = 6,
            LogicSize = new Vector2Int(2, 2),
            VisualScale = Vector2.one,
            UnitType = UnitType.Building,
            WorkType = WorkType.Buffer,
            MaxHealth = 2000f,
            RequiresPower = false, // 箱子不需要电

            InputCount = 3,
            OutputCount = 3,
            DefaultCapacity = 20,
            Ports = new BuildingPort[] {
                new BuildingPort { Offset = new Vector2Int(0, 0), Direction = new Vector2Int(0, -1), Type = PortType.SideIn },
                new BuildingPort { Offset = new Vector2Int(1, 0), Direction = new Vector2Int(0, -1), Type = PortType.SideOut }
            }
        };

        // =========================================================
        // 7. 售卖箱 (3x3) - 终极消费者
        // =========================================================
        _blueprints["seller"] = new EntityBlueprint
        {
            Name = "售卖站",
            SpriteId = 2,
            LogicSize = new Vector2Int(3, 3),
            VisualScale = Vector2.one,
            UnitType = UnitType.Building,
            WorkType = WorkType.Seller,
            MaxHealth = 5000f,

            RequiresPower = false,

            InputCount = 3,
            OutputCount = 0,
            DefaultCapacity = 100,
            Ports = new BuildingPort[] {
                new BuildingPort { Offset = new Vector2Int(0, -1), Direction = new Vector2Int(0, -1), Type = PortType.DirectIn }, // 下
                new BuildingPort { Offset = new Vector2Int(0, 1), Direction = new Vector2Int(0, 1), Type = PortType.DirectIn },   // 上
                new BuildingPort { Offset = new Vector2Int(-1, 0), Direction = new Vector2Int(-1, 0), Type = PortType.DirectIn }, // 左
                new BuildingPort { Offset = new Vector2Int(1, 0), Direction = new Vector2Int(1, 0), Type = PortType.DirectIn }    // 右
            }
        };

        // =========================================================
        // 资源实体
        // =========================================================
        _blueprints["ore_iron"] = new EntityBlueprint
        {
            Name = "黄矿石",
            SpriteId = 7,
            LogicSize = Vector2Int.one,
            VisualScale = new Vector2(1f, 1f),
            UnitType = UnitType.ResourceItem
        };

        _blueprints["ore_copper"] = new EntityBlueprint
        {
            Name = "蓝矿石",
            SpriteId = 8,
            LogicSize = Vector2Int.one,
            VisualScale = new Vector2(1f, 1f),
            UnitType = UnitType.ResourceItem
        };
        // =========================================================
        // PART B: 协议军 (Xie Yi Army) - Faction 0
        // =========================================================

        // 机械狗 (1本)
        _blueprints["x_dog"] = new EntityBlueprint
        {
            Name = "机械狗",
            SpriteId = 8,
            LogicSize = Vector2Int.one,
            VisualScale = Vector2.one,
            UnitType = UnitType.Minion,
            Faction = 0,
            MaxHealth = 60f,

            MoveInterval = 0.25f,       // 快速
            AttackRange = 0f,           // 无攻击
            AttackDamage = 0f,

        };

        // 无人机 (1本)
        _blueprints["x_drone"] = new EntityBlueprint
        {
            Name = "无人机",
            SpriteId = 9,
            LogicSize = Vector2Int.one,
            VisualScale = Vector2.one,
            UnitType = UnitType.Minion,
            Faction = 0,
            MaxHealth = 30f,

            MoveInterval = 0.2f,        // 极快
            AttackRange = 0f,           // 无攻击
            AttackDamage = 0f,
            IsFlyer = true,             // 【飞行】
        };

        // 战狼 (2本)
        _blueprints["x_warwolf"] = new EntityBlueprint
        {
            Name = "战狼",
            SpriteId = 22,
            LogicSize = Vector2Int.one,
            VisualScale = Vector2.one,
            UnitType = UnitType.Minion,
            Faction = 0,
            MaxHealth = 120f,

            MoveInterval = 0.4f,        // 中速
            AttackRange = 5.0f,         // 远程
            AttackDamage = 12.0f,
            AttackCooldown = 0.8f,
            ProjectileSpriteId = 100,   // 子弹ID
            ProjectileSpeed = 12.0f,

        };

        // =========================================================
        // PART C: 日之城 (City of Sun) - Faction 1
        // =========================================================

        // 信徒 (1本)
        _blueprints["c_believer"] = new EntityBlueprint
        {
            Name = "信徒",
            SpriteId = 10,
            LogicSize = Vector2Int.one,
            VisualScale = Vector2.one,
            UnitType = UnitType.Minion,
            Faction = 1,
            MaxHealth = 80f,

            MoveInterval = 0.5f,        // 中速
            AttackRange = 4.0f,         // 中程
            AttackDamage = 10.0f,
            AttackCooldown = 1.0f,
            ProjectileSpriteId = 101,
            ProjectileSpeed = 10.0f,

            FleeHealthPercent = 0.5f,   // 【逃跑】半血溃逃
        };

        // 神圣净化者 (2本)
        _blueprints["c_purifier"] = new EntityBlueprint
        {
            Name = "神圣净化者",
            SpriteId = 11,
            LogicSize = Vector2Int.one,
            VisualScale = Vector2.one,
            UnitType = UnitType.Minion,
            Faction = 1,
            MaxHealth = 200f,

            MoveInterval = 0.6f,
            AttackRange = 2.0f,         // 喷火器，射程短
            AttackDamage = 4.0f,        // 单次伤害低
            AttackCooldown = 0.1f,      // 但射速极快
            ProjectileSpriteId = 102,   // 火焰特效
            ProjectileSpeed = 8.0f,

            ExplodeOnDeath = true,      // 【殉爆】
        };

        // =========================================================
        // PART D: 盖亚黎明 (Gaia's Dawn) - Faction 2
        // =========================================================

        // 自爆机械蠕虫 (1本)
        _blueprints["g_maggot"] = new EntityBlueprint
        {
            Name = "自爆机械蠕虫",
            SpriteId = 12,
            LogicSize = Vector2Int.one,
            VisualScale = Vector2.one,
            UnitType = UnitType.Minion,
            Faction = 2,
            MaxHealth = 40f,

            MoveInterval = 0.8f,        // 慢
            AttackRange = 1.1f,         // 贴脸
            AttackDamage = 60.0f,       // 极高伤害
            AttackCooldown = 0.0f,      // 一次性
            ProjectileSpriteId = -1,

            ExplodeOnDeath = true,      // 自爆的核心逻辑
        };

        // 履带式虫母 (1本)
        _blueprints["g_broodmother"] = new EntityBlueprint
        {
            Name = "履带式虫母",
            SpriteId = 13,
            LogicSize = new Vector2Int(2, 2),
            VisualScale = 2 * Vector2.one,
            UnitType = UnitType.Minion,
            Faction = 2,
            MaxHealth = 400f,

            MoveInterval = 0.7f,        // 中慢速
            AttackRange = 6.0f,         // 远程
            AttackDamage = 5.0f,
            AttackCooldown = 1.5f,
            ProjectileSpriteId = 103,   // 毒液弹

            SpawnBlueprint = "g_maggot", // 【产卵】生产自爆虫
            SpawnInterval = 6.0f,

        };
        // =========================================================
        // PART E: 英雄单位 (Heroes) - 工程师双形态
        // =========================================================

        // 形态一：晋-协议 (Jin - Protocol)
        _blueprints["hero_jin_proto"] = new EntityBlueprint
        {
            Name = "晋-协议",
            SpriteId = 14,          // 主人指定的 Sprite 14
            LogicSize = 2 * Vector2Int.one,
            VisualScale = 2 * Vector2.one,
            UnitType = UnitType.Hero, // 标记为英雄
            Faction = 0,            // 玩家阵营 (协议军)

            MaxHealth = 600f,       // 英雄血量比小兵厚实得多

            // --- 战斗参数 ---
            MoveInterval = 0.25f,   // 移动速度较快 (0.25s/格)
            AttackRange = 6.5f,     // 较远的射程 (工程师拿枪很合理喵)
            AttackDamage = 25.0f,   // 单发伤害尚可
            AttackCooldown = 0.6f,  // 射速中等

            // --- 子弹 ---
            ProjectileSpriteId = 100, // 暂时复用战狼的子弹，或者以后加专属的
            ProjectileSpeed = 16.0f,  // 子弹飞得很快

            // --- 特性 ---
            FleeHealthPercent = 0.0f, // 英雄拥有钢铁意志，死战不退！
            ExplodeOnDeath = false,
            IsFlyer = false,

            // --- 【核心修改】变成移动电桩喵！ ---
            RequiresPower = false,
            IsPowerNode = true,      // 我就是电桩！
            SupplyRange = 3.5f,      // 自带一圈光环，走到哪亮到哪
            ConnectionRange = 8.0f,  // 可以连接附近的电线杆
            EnergyGeneration = 100.0f, // 自带小型核电池，能带得动几个塔
            EnergyCapacity = 500.0f,   // 自身缓存
            // ----------------------------------
        };

        // 形态二：晋-启司 (Jin - Keys)
        // 设定：或许未来偏向战斗或重装建造？目前保持一致作为皮肤切换
        _blueprints["hero_jin_keys"] = new EntityBlueprint
        {
            Name = "晋-启司",
            SpriteId = 15,          // 主人指定的 Sprite 15
            LogicSize = Vector2Int.one,
            VisualScale = Vector2.one,
            UnitType = UnitType.Hero,
            Faction = 0,

            MaxHealth = 600f,

            MoveInterval = 0.25f,
            AttackRange = 6.5f,
            AttackDamage = 25.0f,
            AttackCooldown = 0.6f,

            ProjectileSpriteId = 100,
            ProjectileSpeed = 16.0f,

            FleeHealthPercent = 0.0f,
            ExplodeOnDeath = false,
            IsFlyer = false,
        };
    }

    public static EntityBlueprint Get(string key)
    {
        if (_blueprints.TryGetValue(key, out var bp)) return bp;
        Debug.LogError($"<color=red>找不到蓝图: {key}</color>");
        return default;
    }
}