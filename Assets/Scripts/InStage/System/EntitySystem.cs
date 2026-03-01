using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class TimeTicker
{
    public const float SecondsPerTick = 0.1f;
    public const int TicksPerSecond = 10;

    // 全局逻辑节拍：每 0.1s 加 1，永不回头
    public static long GlobalTick = 0;

    // 视觉平滑偏移 (0.0 ~ 0.1)
    public static float SubTickOffset = 0;

    // 将全局 Tick 转换为位掩码中的索引 (0-63)
    // 这样我们就不用物理移动 ulong 的位，直接用取模逻辑
    public static int GetBitIndex(long tick) => (int)(tick % 64);

    // 构造一个从某刻开始，持续 duration 长的掩码
    public static ulong GetMask(long startTick, int duration)
    {
        ulong mask = 0;
        for (int i = 0; i < duration; i++)
        {
            mask |= (1UL << GetBitIndex(startTick + i));
        }
        return mask;
    }
    public static int ToTicks(float seconds) => Mathf.Max(0, Mathf.RoundToInt(seconds * TicksPerSecond));
}


/// <summary>
/// 本篇代码是一个ECS管线的总辖，负责调度各个子系统的运行，同时持有局内全体实体的数据。
/// 同一时间只存在一个，可以被刷新成各种关卡的形状。
/// </summary>
public class EntitySystem : SingletonMono<EntitySystem>
{
    private bool _initialized = false;
    public bool IsInitialized => _initialized;
    public WholeComponent wholeComponent;
    public int maxEntityCount = 1024;

    private int[] _idToDataIndex;
    private int[] _dataIndexToId;
    private int[] _idVersions;
    private Queue<int> _freeIds;
    private static int _nextCreationIndex = 0;

    public void Initialize(int maxEntityCount, int mapWidth, int mapHeight, int minX = -64, int minY = -64, float cellSize = 1.0f)
    {
        bool needsReallocation = !_initialized || this.maxEntityCount != maxEntityCount;
        this.maxEntityCount = maxEntityCount;

        if (wholeComponent == null)
        {
            wholeComponent = new WholeComponent();
        }

        // 更新基础属性
        wholeComponent.entityCount = 0;
        wholeComponent.mapWidth = mapWidth;
        wholeComponent.mapHeight = mapHeight;
        wholeComponent.minX = minX;
        wholeComponent.minY = minY;

        if (needsReallocation)
        {
            // 需要重新分配数组
            wholeComponent.coreComponent = new CoreComponent[maxEntityCount];
            wholeComponent.moveComponent = new MoveComponent[maxEntityCount];
            wholeComponent.attackComponent = new AttackComponent[maxEntityCount];
            wholeComponent.healthComponent = new HealthComponent[maxEntityCount];
            wholeComponent.spawnComponent = new SpawnComponent[maxEntityCount];
            wholeComponent.drawComponent = new DrawComponent[maxEntityCount];
            wholeComponent.aiComponent = new AIComponent[maxEntityCount];
            wholeComponent.userControlComponent = new UserControlComponent[maxEntityCount];
            wholeComponent.resourceComponent = new ResourceComponent[maxEntityCount];
            wholeComponent.inventoryComponent = new InventoryComponent[maxEntityCount];
            wholeComponent.workComponent = new WorkComponent[maxEntityCount];
            wholeComponent.conveyorComponent = new ConveyorComponent[maxEntityCount];
            wholeComponent.powerComponent = new PowerComponent[maxEntityCount];
            wholeComponent.projectileComponent = new ProjectileComponent[maxEntityCount];
            wholeComponent.goComponent = new GoComponent[maxEntityCount];

            // 重新分配地图数组
            wholeComponent.groundMap = new int[mapWidth * mapHeight];
            wholeComponent.gridMap = new int[mapWidth * mapHeight];
            wholeComponent.effectMap = new int[mapWidth * mapHeight];

            // 重新分配ID映射数组
            _idToDataIndex = new int[maxEntityCount];
            _dataIndexToId = new int[maxEntityCount];
            _idVersions = new int[maxEntityCount];
            _freeIds = new Queue<int>();

            for (int i = 0; i < maxEntityCount; i++)
            {
                _freeIds.Enqueue(i);
                _idVersions[i] = 1;
                _idToDataIndex[i] = -1;
            }
        }
        else
        {
            // 重用现有数组，只需清空内容
            Array.Clear(wholeComponent.coreComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.moveComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.attackComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.healthComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.spawnComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.drawComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.aiComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.userControlComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.resourceComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.inventoryComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.workComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.conveyorComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.powerComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.projectileComponent, 0, maxEntityCount);
            Array.Clear(wholeComponent.goComponent, 0, maxEntityCount);

            // 清空地图数组
            Array.Clear(wholeComponent.groundMap, 0, wholeComponent.groundMap.Length);
            Array.Clear(wholeComponent.gridMap, 0, wholeComponent.gridMap.Length);
            Array.Clear(wholeComponent.effectMap, 0, wholeComponent.effectMap.Length);

            // 重置ID映射
            _freeIds.Clear();
            for (int i = 0; i < maxEntityCount; i++)
            {
                _freeIds.Enqueue(i);
                _idVersions[i]++; // 增加版本号使旧句柄失效
                _idToDataIndex[i] = -1;
            }
        }

        // 初始化网格值为-1
        for (int i = 0; i < wholeComponent.gridMap.Length; i++)
            wholeComponent.gridMap[i] = -1;

        if (GridSystem.Instance != null)
            GridSystem.Instance.Initialize(minX, minY, cellSize);

        // 初始化围棋规则系统
        if (GoRuleSystem.Instance != null)
            GoRuleSystem.Instance.Initialize(mapWidth, mapHeight, minX, minY);

        _nextCreationIndex = 0;
        this._initialized = true;
        Debug.Log($"<color=green>EntitySystem 初始化成功: 工业组件已就绪喵！</color> (重用数组: {!needsReallocation})");
    }

    /// <summary>
    /// 核心创建逻辑：现在支持 LogicSize 和自动中心对齐。
    /// </summary>
    public EntityHandle CreateEntity(Vector2Int gridPos, int faction, int unitType, Vector2Int size)
    {
        if (!this._initialized) return EntityHandle.None;
        if (wholeComponent.entityCount >= maxEntityCount || _freeIds.Count == 0) return EntityHandle.None;

        // A. 账本分配
        int index = wholeComponent.entityCount;
        wholeComponent.entityCount++;
        int id = _freeIds.Dequeue();
        int version = _idVersions[id];
        _idToDataIndex[id] = index;
        _dataIndexToId[index] = id;
        EntityHandle handle = new EntityHandle { Id = id, Version = version };

        // B. 基础组件初始化
        ref var core = ref wholeComponent.coreComponent[index];
        core.Active = true;
        core.SelfHandle = handle;
        core.Team = faction;
        core.Type = unitType;
        core.CreationIndex = _nextCreationIndex++;

        // 【核心修改】存储逻辑尺寸，并利用它计算正确的中心点世界坐标
        core.LogicSize = size;
        core.VisualScale = Vector2.one; // 默认缩放为 1 (靠 PPU 控制大小)
        if ((unitType & UnitType.Projectile) == 0) // 只有普通单位才需要对齐网格中心。
        {
            core.Position = GridSystem.Instance.GridToWorld(gridPos, size);
        }
        core.Rotation = new Vector2Int(0,1);

        // C. 移动组件初始化
        ref var move = ref wholeComponent.moveComponent[index];
        move.LogicalPosition = gridPos;
        move.PreviousLogicalPosition = gridPos;
        move.TargetGridPosition = gridPos;
        move.LastVisualPosition = core.Position; // 确保视觉插值起点正确
        // --- 【修改点：移动 Tick 化】 ---
        move.MoveIntervalTicks = TimeTicker.ToTicks(0.5f); // 默认 0.5 秒转成 Tick
        move.MoveTimerTicks = 0;                           // 初始即可移动
        move.BlockWaitTimerTicks = 0;                      // 初始无阻塞
        // --------------------------------

        // D. 生命值默认值
        ref var health = ref wholeComponent.healthComponent[index];
        health.IsAlive = true;
        health.MaxHealth = 100f;
        health.Health = 100f;

        // E. 工业与渲染组件重置
        wholeComponent.drawComponent[index] = default;
        wholeComponent.drawComponent[index].TeamColor = Color.white;

        wholeComponent.resourceComponent[index] = default;
        wholeComponent.inventoryComponent[index] = default;
        wholeComponent.workComponent[index] = default;
        wholeComponent.conveyorComponent[index] = default;
        wholeComponent.powerComponent[index] = default;
        wholeComponent.projectileComponent[index] = default;
        wholeComponent.aiComponent[index] = default;
        wholeComponent.spawnComponent[index] = default;
        wholeComponent.userControlComponent[index] = default;
        wholeComponent.goComponent[index] = default;

        // F. 地图占据 (利用 GridSystem 进行矩形填充)
        // 🔥【关键修改 2】: 只有非子弹、非特效单位才占据格子！
        if ((unitType & UnitType.Projectile) == 0 && (unitType & UnitType.None) == 0)
        {
            GridSystem.Instance.SetOccupantRect(gridPos, size, handle.Id);

            // 🔥【核心新增】如果是建筑，立即触发局部 NavMesh 重建
            if ((unitType & UnitType.Building) != 0)
            {
                // 计算建筑覆盖的矩形区域
                int startX = gridPos.x - (size.x - 1) / 2;
                int startY = gridPos.y - (size.y - 1) / 2;
                RectInt buildArea = new RectInt(startX, startY, size.x, size.y);

                // 调用我们之前写的局部重剖逻辑
                GridSystem.Instance.RebuildNavMesh(buildArea);
            }
        }
        return handle;
    }
    public List<EntityHandle> SpawnArmy(string key, Vector2Int center, Vector2Int groupSize, int team)
    {
        List<EntityHandle> spawnedHandles = new List<EntityHandle>();
        EntityBlueprint bp = BlueprintRegistry.Get(key);
        if (string.IsNullOrEmpty(bp.Name)) return spawnedHandles;

        int startX = center.x - groupSize.x / 2;
        int startY = center.y - groupSize.y / 2;

        for (int x = 0; x < groupSize.x; x++)
        {
            for (int y = 0; y < groupSize.y; y++)
            {
                Vector2Int current = new Vector2Int(startX + x, startY + y);
                if (GridSystem.Instance.IsAreaClear(current, bp.LogicSize))
                {
                    var handle = CreateEntityFromBlueprint(key, current, team);
                    if (!handle.Equals(default)) spawnedHandles.Add(handle);
                }
            }
        }
        return spawnedHandles;
    }
    /// <summary>
    /// 核心销毁逻辑：现在利用 LogicSize 正确清理网格。
    /// </summary>
    public void DestroyEntity(EntityHandle handle)
    {
        if (!IsValid(handle)) return;

        int idToRemove = handle.Id;
        int indexToRemove = _idToDataIndex[idToRemove];
        int lastIndex = wholeComponent.entityCount - 1;
        int unitType = wholeComponent.coreComponent[indexToRemove].Type;
        Vector2Int logicalPos = wholeComponent.moveComponent[indexToRemove].LogicalPosition;
        Vector2Int size = wholeComponent.coreComponent[indexToRemove].LogicSize;

        // --- 在销毁前，通知电力系统处理断开逻辑 ---
        // --- 【修改点 1】精准过滤：只有可能是电力组件的才去打扰 PowerSystem ---
        ref var p = ref wholeComponent.powerComponent[indexToRemove];
        // 只有它是电网节点，或者它属于某个电网，才需要处理断开
        if (p.IsNode || p.NetID != -1)
        {
            PowerSystem.Instance.OnPowerEntityRemoved(handle, wholeComponent);
        }
        bool isConveyorInvolved = (wholeComponent.workComponent[indexToRemove].WorkType == WorkType.Conveyor);

        // 1. 清理地图上的占据信息
        ref var coreToRemove = ref wholeComponent.coreComponent[indexToRemove];
        ref var moveToRemove = ref wholeComponent.moveComponent[indexToRemove];
        // 【核心修改】使用 LogicSize 来清理网格占据
        GridSystem.Instance.ClearOccupantRect(moveToRemove.LogicalPosition, coreToRemove.LogicSize);
        // 🔥【核心新增】如果被摧毁的是建筑，还原该区域的 NavMesh
        if ((unitType & UnitType.Building) != 0)
        {
            int startX = logicalPos.x - (size.x - 1) / 2;
            int startY = logicalPos.y - (size.y - 1) / 2;
            RectInt destroyArea = new RectInt(startX, startY, size.x, size.y);

            // 重新剖分这块区域，它会自动把原来的“洞”补上，并和邻居合并成大矩形
            GridSystem.Instance.RebuildNavMesh(destroyArea);
        }

        // 2. 标记为不活跃
        coreToRemove.Active = false;

        // 3. Swap-back (如果不是最后一个，把最后一个搬过来填充空位)
        if (indexToRemove != lastIndex)
        {
            // A. 搬运所有组件数据 (确保新增的工业组件也被搬运)
            wholeComponent.coreComponent[indexToRemove] = wholeComponent.coreComponent[lastIndex];
            wholeComponent.moveComponent[indexToRemove] = wholeComponent.moveComponent[lastIndex];
            wholeComponent.attackComponent[indexToRemove] = wholeComponent.attackComponent[lastIndex];
            wholeComponent.healthComponent[indexToRemove] = wholeComponent.healthComponent[lastIndex];
            wholeComponent.spawnComponent[indexToRemove] = wholeComponent.spawnComponent[lastIndex];
            wholeComponent.drawComponent[indexToRemove] = wholeComponent.drawComponent[lastIndex];
            wholeComponent.aiComponent[indexToRemove] = wholeComponent.aiComponent[lastIndex];
            wholeComponent.userControlComponent[indexToRemove] = wholeComponent.userControlComponent[lastIndex];

            // --- 搬运工业相关组件 ---
            wholeComponent.resourceComponent[indexToRemove] = wholeComponent.resourceComponent[lastIndex];
            wholeComponent.inventoryComponent[indexToRemove] = wholeComponent.inventoryComponent[lastIndex];
            wholeComponent.workComponent[indexToRemove] = wholeComponent.workComponent[lastIndex];
            wholeComponent.conveyorComponent[indexToRemove] = wholeComponent.conveyorComponent[lastIndex];
            wholeComponent.powerComponent[indexToRemove] = wholeComponent.powerComponent[lastIndex];

            // --- 战斗辅助 ---
            wholeComponent.projectileComponent[indexToRemove] = wholeComponent.projectileComponent[lastIndex];

            // B. 更新映射表
            int idOfMovedEntity = _dataIndexToId[lastIndex];
            _idToDataIndex[idOfMovedEntity] = indexToRemove;
            _dataIndexToId[indexToRemove] = idOfMovedEntity;
        }

        if (isConveyorInvolved)
        {
            TransportSystem.Instance.MarkDirty();
        }
        // 4. 清理旧 ID 数据
        _idToDataIndex[idToRemove] = -1;
        wholeComponent.entityCount--;

        // 5. 回收 ID 并升级版本号
        _freeIds.Enqueue(idToRemove);
        _idVersions[idToRemove]++;
    }
    public EntityHandle CreateEntityFromBlueprint(string bpKey, Vector2Int gridPos, int faction)
    {
        EntityBlueprint bp = BlueprintRegistry.Get(bpKey);
        if (string.IsNullOrEmpty(bp.Name)) return EntityHandle.None;

        // 1. 调用原始创建方法
        EntityHandle handle = CreateEntity(gridPos, faction, bp.UnitType, bp.LogicSize);
        int index = GetIndex(handle);

        if (index != -1)
        {
            var whole = wholeComponent;

            // 2. 填充核心属性
            ref var core = ref whole.coreComponent[index];
            core.LogicSize = bp.LogicSize;
            core.VisualScale = bp.VisualScale;
            core.BlueprintName = bpKey;

            // 注意：Position 已经在 CreateEntity 里根据 LogicSize 算好中心点了喵！
            // 3. 填充移动属性 (🔥漏了这个！每个兵移速不一样)
            ref var move = ref whole.moveComponent[index];
            // --- 【修改点：读取蓝图秒数并转换为 Tick】 ---
            float intervalSec = bp.MoveInterval > 0 ? bp.MoveInterval : 0.5f;
            move.MoveIntervalTicks = TimeTicker.ToTicks(intervalSec);
            move.MoveTimerTicks = 0;
            move.BlockWaitTimerTicks = 0;
            // ----------------------------------------------
            move.IsFlyer = (bp.UnitType & UnitType.Flyer) != 0;

            // 4. 填充攻击属性 (🔥🔥🔥 之前这里全漏了！)
            ref var attack = ref whole.attackComponent[index];
            attack.AttackRange = bp.AttackRange;
            attack.AttackDamage = bp.AttackDamage;
            // ⚠️ 修改：将秒转换为tick
            attack.AttackCooldownTicks = TimeTicker.ToTicks(bp.AttackCooldown);
            attack.LastAttackTick = 0; // 初始化为0，表示从未攻击
            attack.ProjectileSpriteId = bp.ProjectileSpriteId;
            attack.ProjectileSpeed = bp.ProjectileSpeed;
            attack.TargetEntityId = -1; // 确保初始没有锁定奇怪的目标

            // 3. 填充渲染属性
            ref var draw = ref whole.drawComponent[index];
            draw.SpriteId = bp.SpriteId;
            draw.TeamColor = (faction == 1) ? Color.white : Color.red;

            // 4. 填充工业属性
            ref var work = ref whole.workComponent[index];
            work.WorkType = bp.WorkType;
            work.WorkSpeed = bp.WorkSpeed;
            work.DrillRange = bp.DrillRange;
            work.RequiresPower = bp.RequiresPower;

            // 5. 填充生命值
            ref var health = ref whole.healthComponent[index];
            health.MaxHealth = bp.MaxHealth;
            health.Health = bp.MaxHealth;
            health.ExplodeOnDeath = bp.ExplodeOnDeath; // 自爆虫需要这个

            // 6. 初始化围棋组件
            ref var go = ref whole.goComponent[index];
            // 只有地面单位（Hero/Minion）是围棋棋子，建筑不参与围棋规则
            bool isHeroOrMinion = (bp.UnitType & (UnitType.Hero | UnitType.Minion)) != 0;
            bool isBuilding = (bp.UnitType & UnitType.Building) != 0;
            go.IsGoPiece = isHeroOrMinion && !isBuilding; // 英雄或小兵且不是建筑
            go.CurrentLiberties = 0;

            // 库存属性

            ref var inv = ref whole.inventoryComponent[index];
            inv = default;
            inv.InputSlotCount = bp.InputCount;
            inv.OutputSlotCount = bp.OutputCount;

            // 初始化输入槽
            if (bp.InputCount >= 1) inv.Input0.MaxCapacity = bp.DefaultCapacity;
            if (bp.InputCount >= 2) inv.Input1.MaxCapacity = bp.DefaultCapacity;
            if (bp.InputCount >= 3) inv.Input2.MaxCapacity = bp.DefaultCapacity;
            if (bp.InputCount >= 4) inv.Input3.MaxCapacity = bp.DefaultCapacity;

            // 初始化输出槽
            if (bp.OutputCount >= 1) inv.Output0.MaxCapacity = bp.DefaultCapacity;
            if (bp.OutputCount >= 2) inv.Output1.MaxCapacity = bp.DefaultCapacity;
            if (bp.OutputCount >= 3) inv.Output2.MaxCapacity = bp.DefaultCapacity;
            if (bp.OutputCount >= 4) inv.Output3.MaxCapacity = bp.DefaultCapacity;

            // -- 电力属性 --
            ref var power = ref whole.powerComponent[index];
            power.NetID = -1; // 默认断网
            power.IsNode = bp.IsPowerNode;
            power.SupplyRange = bp.SupplyRange;
            power.ConnRange = bp.ConnectionRange;
            power.Production = bp.EnergyGeneration;
            power.Capacity = bp.EnergyCapacity;
            power.StoredEnergy = 0f;
            // 立即触发布线逻辑， 必须在这里告诉 PowerSystem：“嘿！有个电桩插在地上了，快看看能不能连网！”
            if (power.IsNode)
            {
                PowerSystem.Instance.OnPowerEntityBuilt(handle, whole);
            }

        }

        return handle;
    }


    // =========================================================
    // 工具方法
    // =========================================================

    public bool IsValid(EntityHandle handle)
    {
        if (handle.Id < 0 || handle.Id >= _idVersions.Length) return false;
        if (_idVersions[handle.Id] != handle.Version) return false;
        int dataIndex = _idToDataIndex[handle.Id];
        return dataIndex >= 0 && dataIndex < wholeComponent.entityCount;
    }
    public EntityHandle GetHandleFromId(int id)
    {
        if (id < 0 || id >= _idVersions.Length) return EntityHandle.None;
        return new EntityHandle { Id = id, Version = _idVersions[id] };
    }
    public int GetIndex(EntityHandle handle)
    {
        if (!IsValid(handle)) return -1;
        return _idToDataIndex[handle.Id];
    }

    // =========================================================
    //  【核心新增】 关卡流加载入口
    // =========================================================

    /// <summary>
    /// 请求进入某个关卡。
    /// 逻辑：
    /// 1. 检查 SaveManager 当前存档里有没有这个关卡的进度？
    /// 2. 有 -> 读取进度 (LoadECS)。
    /// 3. 无 -> 让 WorldFactory 读 JSON 创建新世界 -> 读取 (LoadECS)。
    /// </summary>
    public void LoadStage(string stageID)
    {
        Debug.Log($"<color=yellow>[EntitySystem]</color> 正在请求进入据点: {stageID}...");

        // 1. 获取当前用户存档 (如果没有加载存档，就无法进入)
        var user = MainModel.Instance.CurrentUser;
        if (user == null)
        {
            Debug.LogError("错误：没有加载任何用户存档 (User is null)，无法进入关卡！请先 NewGame 或 LoadGame。");
            return;
        }

        // 2. 尝试从存档(热数据)获取
        WholeComponent worldData = null;
        var stageRecord = user.GetStage(stageID);

        if (stageRecord != null && stageRecord.WorldData != null)
        {
            Debug.Log($"-> 发现据点存档，正在恢复现场...");
            worldData = stageRecord.WorldData; // 注意：LoadECS 内部会 Clone，这里传引用没问题
        }
        else
        {
            Debug.Log($"-> 初次抵达，正在生成地形...");
            // 3. 存档里没有，去读静态 JSON
            worldData = WorldFactory.CreateNewWorldFromLevelID(stageID);
        }

        if (worldData == null)
        {
            Debug.LogError($"无法加载关卡 {stageID}，请检查 Resources/Levels/ 下是否有对应 JSON。");
            return;
        }

        // 4. 执行加载
        LoadECS(worldData);

        // 5. 更新数据层的当前关卡指针 (方便下次保存)
        // 这一步最好封装一下，但这里直接写也行
        MainModel.Instance.SetCurrentStage(stageID);

        // 6. 自动加载绑定到该关卡的任务包
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.LoadMissionPackForStage(stageID);
        }
    }

    /// <summary>
    /// 【核心修复】将数据包注入系统，并重建所有缓存索引
    /// </summary>
    public void LoadECS(WholeComponent loadedData)
    {
        // 1. 清理当前残留
        ClearWorld(); // 先清空，保证干净

        // 2. 深拷贝数据 (确保我们操作的是副本，不影响存档源数据)
        this.wholeComponent = loadedData.Clone();
        // 3. 🔥【核心修改】使用数据中记录的真实偏移量
        // 不再是 calculatedMinX = -(width/2) 了！
        int realMinX = this.wholeComponent.minX;
        int realMinY = this.wholeComponent.minY;

        if (GridSystem.Instance != null)
        {
            // 告诉 GridSystem：这个地图是从 (realMinX, realMinY) 开始的
            GridSystem.Instance.UpdateMapSize(realMinX, realMinY, 1.0f);
        }

        // 🔥 【核心修复】必须同步通知围棋系统更新它的坐标系和一维数组长度！
        if (GoRuleSystem.Instance != null)
        {
            int realWidth = this.wholeComponent.mapWidth;
            int realHeight = this.wholeComponent.mapHeight;
            GoRuleSystem.Instance.UpdateMapSize(realWidth, realHeight, realMinX, realMinY);
        }

        // 保证 maxEntityCount 与数据一致
        if (this.wholeComponent.coreComponent != null)
            this.maxEntityCount = this.wholeComponent.coreComponent.Length;


        // 初始化可能为null的组件数组（向后兼容旧存档）
        // 核心组件数组
        if (wholeComponent.coreComponent == null)
            wholeComponent.coreComponent = new CoreComponent[maxEntityCount];
        if (wholeComponent.moveComponent == null)
            wholeComponent.moveComponent = new MoveComponent[maxEntityCount];
        if (wholeComponent.attackComponent == null)
            wholeComponent.attackComponent = new AttackComponent[maxEntityCount];
        if (wholeComponent.healthComponent == null)
            wholeComponent.healthComponent = new HealthComponent[maxEntityCount];
        if (wholeComponent.spawnComponent == null)
            wholeComponent.spawnComponent = new SpawnComponent[maxEntityCount];
        if (wholeComponent.drawComponent == null)
            wholeComponent.drawComponent = new DrawComponent[maxEntityCount];
        if (wholeComponent.aiComponent == null)
            wholeComponent.aiComponent = new AIComponent[maxEntityCount];
        if (wholeComponent.userControlComponent == null)
            wholeComponent.userControlComponent = new UserControlComponent[maxEntityCount];
        // 新组件数组
        if (wholeComponent.goComponent == null)
            wholeComponent.goComponent = new GoComponent[maxEntityCount];
        if (wholeComponent.powerComponent == null)
            wholeComponent.powerComponent = new PowerComponent[maxEntityCount];
        if (wholeComponent.projectileComponent == null)
            wholeComponent.projectileComponent = new ProjectileComponent[maxEntityCount];
        if (wholeComponent.resourceComponent == null)
            wholeComponent.resourceComponent = new ResourceComponent[maxEntityCount];
        if (wholeComponent.inventoryComponent == null)
            wholeComponent.inventoryComponent = new InventoryComponent[maxEntityCount];
        if (wholeComponent.workComponent == null)
            wholeComponent.workComponent = new WorkComponent[maxEntityCount];
        if (wholeComponent.conveyorComponent == null)
            wholeComponent.conveyorComponent = new ConveyorComponent[maxEntityCount];
        // 地图数组
        int mapSize = wholeComponent.mapWidth * wholeComponent.mapHeight;
        if (wholeComponent.groundMap == null)
            wholeComponent.groundMap = new int[mapSize];
        if (wholeComponent.gridMap == null)
            wholeComponent.gridMap = new int[mapSize];
        if (wholeComponent.effectMap == null)
            wholeComponent.effectMap = new int[mapSize];

        // 3. 【至关重要】重建 ID 映射表 (Rehydration)
        RebuildRuntimeIndices();

        // 4. 【至关重要】重建 GridSystem 占据信息
        RebuildGridOccupancy();

        // 🔥【核心新增】执行全图 NavMesh 矩形剖分 (逻辑拓扑层)
        // 告诉 GridSystem：把整个地图从 minX/minY 开始，按照 mapWidth/mapHeight 进行一次全量切分
        RectInt fullMapArea = new (wholeComponent.minX, wholeComponent.minY, wholeComponent.mapWidth, wholeComponent.mapHeight);
        GridSystem.Instance.RebuildNavMesh(fullMapArea);

        // 5. 🔥 激活系统！
        this._initialized = true;
        // 5. 视觉同步
        if (TilemapSyncManager.Instance != null)
        {
            // 激活 Tilemap 并同步数据
            TilemapSyncManager.Instance.SetTilemapActive(true);
            TilemapSyncManager.Instance.SyncToTilemap();
        }

        // 6. 摄像机自动化初始化（替代手动控制台命令）
        if (CameraController.Instance != null)
        {
            CameraController.Instance.InitializeCamera();
            Debug.Log("<color=cyan>[EntitySystem]</color> 摄像机自动化初始化完成");
        }
        else
        {
            Debug.LogWarning("<color=orange>[EntitySystem]</color> CameraController实例未找到，跳过摄像机初始化");
        }

        // 7. 重新计算一次物流网络 (防止存档里没存拓扑结构)
        TransportSystem.Instance.RebuildNetwork(this.wholeComponent);

        Debug.Log($"<color=green>[EntitySystem]</color> 世界加载完成！实体数: {wholeComponent.entityCount}");
    }

    /// <summary>
    /// 【修正版】重建索引逻辑
    /// </summary>
    private void RebuildRuntimeIndices()
    {
        // --- 🔥 关键修复：确保管理账本已分配 ---
        if (_idToDataIndex == null || _idToDataIndex.Length != maxEntityCount)
            _idToDataIndex = new int[maxEntityCount];

        if (_dataIndexToId == null || _dataIndexToId.Length != maxEntityCount)
            _dataIndexToId = new int[maxEntityCount];

        if (_idVersions == null || _idVersions.Length != maxEntityCount)
        {
            _idVersions = new int[maxEntityCount];
            // 初始化版本号为 1
            for (int i = 0; i < maxEntityCount; i++) _idVersions[i] = 1;
        }

        if (_freeIds == null)
            _freeIds = new Queue<int>();

        // --- 清理旧映射 ---
        for (int i = 0; i < maxEntityCount; i++)
        {
            _idToDataIndex[i] = -1;
        }
        _freeIds.Clear();

        HashSet<int> usedIds = new HashSet<int>();

        // --- 遍历当前存活实体，重新登记 ---
        if (wholeComponent != null)
        {
            for (int index = 0; index < wholeComponent.entityCount; index++)
            {
                ref var core = ref wholeComponent.coreComponent[index];
                int id = core.SelfHandle.Id;

                if (id >= 0 && id < maxEntityCount)
                {
                    _idToDataIndex[id] = index;
                    _dataIndexToId[index] = id;
                    // 恢复版本号（从 Handle 恢复）
                    _idVersions[id] = core.SelfHandle.Version;
                    usedIds.Add(id);
                }
            }
        }

        // --- 重建空闲 ID 队列 ---
        for (int id = 0; id < maxEntityCount; id++)
        {
            if (!usedIds.Contains(id))
            {
                _freeIds.Enqueue(id);
            }
        }
    }

    /// <summary>
    /// 根据实体位置，告诉 GridSystem 哪些格子被占了
    /// </summary>
    private void RebuildGridOccupancy()
    {
        GridSystem.Instance.ClearAll(); // 先清空网格

        for (int index = 0; index < wholeComponent.entityCount; index++)
        {
            ref var core = ref wholeComponent.coreComponent[index];
            ref var move = ref wholeComponent.moveComponent[index];

            // 只有非子弹单位才占格子
            if ((core.Type & UnitType.Projectile) == 0 && (core.Type & UnitType.None) == 0)
            {
                // 使用 LogicSize 和 LogicalPosition 恢复占据
                GridSystem.Instance.SetOccupantRect(move.LogicalPosition, core.LogicSize, core.SelfHandle.Id);
            }
        }
    }

    public void ClearWorld()
    {
        // 如果系统还没初始化，或者组件容器是空的，就没必要清理了喵
        if (!_initialized || wholeComponent == null)
        {
            Debug.Log("<color=orange>[EntitySystem]</color> 系统尚未初始化，无需清理喵。");
            return;
        }

        // 1. 强制回收所有 ID
        _freeIds.Clear();
        for (int i = 0; i < maxEntityCount; i++)
        {
            _freeIds.Enqueue(i);
            _idVersions[i]++; // 版本号激增，让外界持有的 Handle 全部失效
            _idToDataIndex[i] = -1;
        }

        // 2. 重置数据计数
        wholeComponent.entityCount = 0;

        // 3. 清空 GridSystem
        if (GridSystem.Instance != null)
            GridSystem.Instance.ClearAll();

        // 4. 通知各个子系统重置缓存 (比如 PowerSystem 的电网列表)
        if (PowerSystem.Instance != null)
        {
            var method = PowerSystem.Instance.GetType().GetMethod("Reset");
            if (method != null) method.Invoke(PowerSystem.Instance, null);
        }

        // 5. 清理用户控制系统
        if (UserControlSystem.Instance != null)
        {
            UserControlSystem.Instance.ClearAllSelection();
            UserControlSystem.Instance.playerTeam = 1;
        }

        // 6. 重置全局时间戳
        TimeTicker.GlobalTick = 0;
        TimeTicker.SubTickOffset = 0;

        // 7. 清理其他子系统
        if (AIBrainServer.Instance != null)
            AIBrainServer.Instance.ClearAll();

        if (TimeSystem.Instance != null)
        {
            TimeSystem.Instance.SetPaused(false);
            TimeSystem.Instance.ResetTimer();
        }

        if (IndustrialSystem.Instance != null)
            IndustrialSystem.Instance.GlobalPowerOverride = false;

        // 清理寻路系统
        if (PathfindingSystem.Instance != null)
            PathfindingSystem.Instance.Clear();

        // 停用 Tilemap 渲染
        if (TilemapSyncManager.Instance != null)
        {
            TilemapSyncManager.Instance.SetTilemapActive(false);
        }

        Debug.Log("<color=red>[EntitySystem]</color> 世界已核平，所有数据归零。");
    }
    private void Update()
    {
        if (this._initialized)
        {
            UpdateSystem(Time.deltaTime);
        }
    }

    private void UpdateSystem(float deltaTime)
    {
        // 🔥 第一步：更新游戏tick（必须首先调用）
        TimeSystem.Instance.UpdateGameTick(deltaTime);

        // 🔥 第二步：检查物流脏标记。
        // 如果上一帧发生了 Swap-back，这里会立即修正所有 Index 映射。
        TransportSystem.Instance.RebuildIfDirty(wholeComponent);
        PowerSystem.Instance.UpdatePower(wholeComponent, deltaTime);
        // 1. 工业逻辑：产出矿石并尝试注入传送带
        IndustrialSystem.Instance.UpdateIndustrial(wholeComponent, deltaTime);

        // 2. 【核心新增】物流逻辑：驱动传送带上的物品移动、处理排队和换线
        TransportSystem.Instance.UpdateNetwork(deltaTime);

        // 3. 基础逻辑：AI、攻击、基础网格移动
        AutoAISystem.Instance.UpdateAI(wholeComponent, deltaTime);
        AttackSystem.Instance.UpdateAttacks(wholeComponent, deltaTime);
        ProjectileSystem.Instance.UpdateProjectiles(wholeComponent, deltaTime);
        PathfindingSystem.Instance.UpdatePathfinding(wholeComponent);
        ArbitrationSystem.Instance.UpdateArbitration(wholeComponent, deltaTime);
        BoidsSystem.Instance.UpdateTacticalBoids(wholeComponent);
        MoveSystem.Instance.UpdateMovement(wholeComponent, deltaTime);

        // 4. 围棋规则结算（必须在移动之后，渲染之前）
        // 只有在发生逻辑Tick时才跑围棋管线（节省CPU）
        if (TimeSystem.Instance.TicksProcessedThisFrame > 0)
        {
            GoRuleSystem.Instance.UpdateGoRules(wholeComponent);
        }

        // 5. 死亡处理（必须在所有扣血系统之后，绘制之前）
        DeathSystem.Instance.UpdateDeaths(wholeComponent, deltaTime);


        // 6. 表现层渲染
        // 先画基础单位（建筑、小兵）
        DrawSystem.Instance.UpdateDraws(wholeComponent, deltaTime);
        SelectionOverlaySystem.Instance.UpdateRender();

        // 【核心新增】再画传送带上的物品（使用 Item Layer 盖在 Conveyor Layer 上）
        TransportDrawSystem.Instance.UpdateTransportDraws(wholeComponent);
    }
}

public class WholeComponent
{
    public int entityCount;
    public int mapWidth;
    public int mapHeight;
    public int minX;
    public int minY;
    public string sceneName;
    public int nextEntityId; // 用于生成唯一 ID 的计数器

    // --- 组件数组 ---
    public CoreComponent[] coreComponent;
    public MoveComponent[] moveComponent;
    public AttackComponent[] attackComponent;
    public HealthComponent[] healthComponent;
    public SpawnComponent[] spawnComponent;
    public DrawComponent[] drawComponent;
    public AIComponent[] aiComponent;
    public UserControlComponent[] userControlComponent;

    // --- 【新加入的工业组件数组】 ---
    public ResourceComponent[] resourceComponent;
    public InventoryComponent[] inventoryComponent;
    public WorkComponent[] workComponent;
    public ConveyorComponent[] conveyorComponent;
    public PowerComponent[] powerComponent;

    // --- 【战斗辅助】 ---
    public ProjectileComponent[] projectileComponent;

    // --- 【围棋规则】 ---
    public GoComponent[] goComponent;

    public int[] groundMap;
    public int[] gridMap;
    public int[] effectMap;

    public WholeComponent Clone()
    {
        WholeComponent clone = new WholeComponent
        {
            entityCount = this.entityCount,
            mapWidth = this.mapWidth,
            mapHeight = this.mapHeight,
            minX = this.minX,
            minY = this.minY,
            sceneName = this.sceneName,

            coreComponent = (this.coreComponent != null) ? (CoreComponent[])this.coreComponent.Clone() : null,
            moveComponent = (this.moveComponent != null) ? (MoveComponent[])this.moveComponent.Clone() : null,
            attackComponent = (this.attackComponent != null) ? (AttackComponent[])this.attackComponent.Clone() : null,
            healthComponent = (this.healthComponent != null) ? (HealthComponent[])this.healthComponent.Clone() : null,
            spawnComponent = (this.spawnComponent != null) ? (SpawnComponent[])this.spawnComponent.Clone() : null,
            drawComponent = (this.drawComponent != null) ? (DrawComponent[])this.drawComponent.Clone() : null,
            aiComponent = (this.aiComponent != null) ? (AIComponent[])this.aiComponent.Clone() : null,
            userControlComponent = (this.userControlComponent != null) ? (UserControlComponent[])this.userControlComponent.Clone() : null,

            // --- 克隆新组件 ---
            resourceComponent = (this.resourceComponent != null) ? (ResourceComponent[])this.resourceComponent.Clone() : null,
            inventoryComponent = (this.inventoryComponent != null) ? (InventoryComponent[])this.inventoryComponent.Clone() : null,
            workComponent = (this.workComponent != null) ? (WorkComponent[])this.workComponent.Clone() : null,
            conveyorComponent = (this.conveyorComponent != null) ? (ConveyorComponent[])this.conveyorComponent.Clone() : null,
            powerComponent = (this.powerComponent != null) ? (PowerComponent[])this.powerComponent.Clone() : null,

            projectileComponent = (this.projectileComponent != null) ? (ProjectileComponent[])this.projectileComponent.Clone() : null,

            // --- 克隆围棋组件 ---
            goComponent = (this.goComponent != null) ? (GoComponent[])this.goComponent.Clone() : null,

            groundMap = (this.groundMap != null) ? (int[])this.groundMap.Clone() : null,
            gridMap = (this.gridMap != null) ? (int[])this.gridMap.Clone() : null,
            effectMap = (this.effectMap != null) ? (int[])this.effectMap.Clone() : null
        };
        return clone;
    }
}