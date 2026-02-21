using UnityEngine;
using System.Collections.Generic;

public class IndustrialSystem : SingletonMono<IndustrialSystem>
{
    [Header("调试开关")]
    public bool enableDebugLog = true;
    public bool showGizmos = true;

    // 【新增】全局无视电力开关 (上帝模式)
    public bool GlobalPowerOverride = false;
    public long Gold
    {
        get
        {
            if (SaveManager.Instance.CurrentUser != null)
                return (long)SaveManager.Instance.CurrentUser.Progression.GlobalMoney;
            return 0;
        }
        set
        {
            if (SaveManager.Instance.CurrentUser != null)
                SaveManager.Instance.CurrentUser.Progression.GlobalMoney = value;
        }
    }

    // --- 【核心新增】策略注册表 ---
    private Dictionary<WorkType, IWorkStrategy> _strategies = new Dictionary<WorkType, IWorkStrategy>();
    protected override void Awake()
    {
        base.Awake();
        RegisterStrategies(); // 初始化时注册
    }
    private void RegisterStrategies()
    {
        // 注册所有策略喵！
        _strategies[WorkType.Drill] = new DrillStrategy();
        _strategies[WorkType.Generator] = new GeneratorStrategy();
        _strategies[WorkType.Buffer] = new BufferStrategy();
        _strategies[WorkType.Battery] = new BatteryStrategy();
        _strategies[WorkType.Seller] = new SellerStrategy();
        // 未来可以加 FactoryStrategy 等...
    }
    public void UpdateIndustrial(WholeComponent whole, float deltaTime)
    {
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            if (!core.Active) continue;

            ref var work = ref whole.workComponent[i];
            if (work.WorkType == WorkType.None) continue;

            // 1. 机器内部运作（生产、燃烧、缓冲槽转移）
            HandleInternalWork(i, whole, deltaTime);

            // 2. 端口物流任务驱动（处理飞行、倒车、落地校验）
            UpdateHandoverTasks(i, whole, deltaTime);

            // 3. 寻找新的物流任务（预测并分配空闲端口）
            SearchNewPortTasks(i, whole);
        }
    }

    // ========================================================================
    // 1. 内部逻辑 (纯策略模式)
    // ========================================================================
    private void HandleInternalWork(int index, WholeComponent whole, float deltaTime)
    {
        ref var work = ref whole.workComponent[index];
        ref var power = ref whole.powerComponent[index];

        // 计算当前效率
        float efficiency = 1.0f;
        if (!GlobalPowerOverride && work.RequiresPower)
        {
            // 直接使用满足率作为效率系数喵！
            efficiency = power.CurrentSatisfaction;
        }

        // 如果效率太低（比如完全没电），策略依然Tick，但进度为0
        // 这样策略内部依然可以更新它的电力需求(Demand)
        if (_strategies.TryGetValue(work.WorkType, out var strategy))
        {
            // 【核心修改】将 deltaTime 乘以效率
            strategy.Tick(index, whole, deltaTime * efficiency);
        }
    }

    // ========================================================================
    // 2. 状态机：处理 8 个任务槽的物理更新
    // ========================================================================
    private void UpdateHandoverTasks(int index, WholeComponent whole, float deltaTime)
    {
        if (!IsEntityPowered(index, whole)) return;
        ref var work = ref whole.workComponent[index];
        ref var inv = ref whole.inventoryComponent[index];

        // 循环处理所有端口任务
        for (int p = 0; p < 8; p++)
        {
            ref var task = ref work.GetTask(p);
            if (!task.Active) continue;

            // --- 状态 A: 飞行中 (Flying) ---
            if (task.Status == HandoverStatus.Flying)
            {
                task.Progress += deltaTime * task.Speed;

                if (task.Progress >= 1.0f)
                {
                    task.Progress = 1.0f;

                    // 抵达终点，分情况处理
                    if (task.Mode == HandoverMode.Eject)
                    {
                        // Eject 必须进行【第二次校验】：事实插入
                        if (TransportSystem.Instance.TryAddItemAtDistance(task.TargetLineID, task.ItemType, task.TargetDistance))
                        {
                            // 成功：正式扣除库存，结束任务
                            ref var slot = ref inv.GetOutput(task.InvSlotIndex);
                            slot.Count--; 
                            slot.EjectReserved--;
                            if (slot.Count <= 0) slot.ItemType = 0;
                            
                            task.Active = false; // 任务结束
                        }
                        else
                        {
                            // 失败：传送带突然堵了，开始倒车
                            task.Status = HandoverStatus.Returning;
                        }
                    }
                    else if (task.Mode == HandoverMode.Grab)
                    {
                        // Grab 在起飞时已经拿到了物品，建筑内部空间也预留了(GrabIncoming)
                        // 所以这里几乎是 100% 成功，除非逻辑错误
                        ref var slot = ref inv.GetInput(task.InvSlotIndex);
                        slot.TryAdd(task.ItemType, 1);
                        slot.GrabIncoming--; // 释放占位
                        
                        // 特殊逻辑：售卖箱直接换钱
                        if (work.WorkType == WorkType.Seller)
                        {
                            slot.TryRemove(1);
                            AddGold(10);
                        }

                        task.Active = false; // 任务结束
                    }
                }
            }
            // --- 状态 B: 倒车中 (Returning) ---
            else if (task.Status == HandoverStatus.Returning)
            {
                // 只有 Eject 失败才会 Returning
                task.Progress -= deltaTime * task.Speed;
                if (task.Progress <= 0f)
                {
                    // 回到起点，释放预留量，物品算作“没发出去”
                    task.Active = false;
                    ref var slot = ref inv.GetOutput(task.InvSlotIndex);
                    slot.EjectReserved--;
                }
            }
        }
    }

    // ========================================================================
    // 3. 决策机：扫描端口并发起新任务
    // ========================================================================
    private void SearchNewPortTasks(int index, WholeComponent whole)
    {
        if (!IsEntityPowered(index, whole)) return;
        ref var core = ref whole.coreComponent[index];
        ref var work = ref whole.workComponent[index];
        ref var inv = ref whole.inventoryComponent[index];
        ref var move = ref whole.moveComponent[index];

        // --- 缓存箱特殊逻辑：动态极性检测 ---
        if (work.WorkType == WorkType.Buffer)
        {
            HandleBufferSmartPorts(index, whole);
            return;
        }

        var bp = BlueprintRegistry.Get(core.BlueprintName);
        if (bp.Ports == null) return;

        // 遍历蓝图定义的端口
        for (int p = 0; p < bp.Ports.Length && p < 6; p++)
        {
            ref var task = ref work.GetTask(p);
            if (task.Active) continue; // 该端口正忙，跳过

            var port = bp.Ports[p];
            
            // 计算端口在世界空间的坐标
            Vector2Int rotatedOffset = RotatePortOffsetWithSide(port.Offset, core.Rotation, core.LogicSize);
            Vector2Int rotatedDir = RotateDirection(port.Direction, core.Rotation);
            
            // 端口对准的那个逻辑格子
            Vector2Int targetGrid = move.LogicalPosition + rotatedOffset + rotatedDir;
            
            // 端口自身的世界坐标 (起点)
            Vector2 portWorldPos = GridSystem.Instance.GridToWorld(move.LogicalPosition + rotatedOffset);
            // 传送带格子的世界坐标 (终点)
            Vector2 beltWorldPos = GridSystem.Instance.GridToWorld(targetGrid);

            if (port.Type == PortType.DirectOut || port.Type == PortType.SideOut)
            {
                // 只有当有东西可吐时才计算
                ref var outSlot = ref inv.GetOutput(0);
                if (outSlot.AvailableToEject > 0)
                {
                    // 1. 确认对面是传送带
                    // 注意：这里的 rawSegIdx 是格子的起始索引 (例如 5.0)
                    if (TransportSystem.Instance.GetLineIDAtGrid(targetGrid, out int lineID, out float rawSegIdx))
                    {
                        // 【核心修正】逻辑目标必须是格子中心 (X.5)，而不是格子起点 (X.0)
                        float targetCenterDist = rawSegIdx + 0.5f;

                        float speed = TransportSystem.Instance.GetLineSpeed(lineID);
                        float dist = Vector2.Distance(portWorldPos, beltWorldPos); // 视觉距离：从端口到带子中心
                        float time = dist / speed; // 保持你的原速度算法不变

                        // 2. 【第一次校验】预测 (用中心位置去预测，因为我们要插到那里)
                        if (TransportSystem.Instance.PredictSpace(lineID, targetCenterDist, time))
                        {
                            // 3. 启动 Eject 任务
                            task.Active = true;
                            task.Mode = HandoverMode.Eject;
                            task.Status = HandoverStatus.Flying;
                            task.ItemType = outSlot.ItemType;
                            task.InvSlotIndex = 0;

                            task.StartPos = portWorldPos;
                            task.EndPos = beltWorldPos; // 视觉终点：世界坐标中心

                            task.Progress = 0f;
                            task.Speed = (time > 0.001f) ? (1.0f / time) : 10f; // 保持你的原时间计算

                            task.TargetLineID = lineID;
                            task.TargetDistance = targetCenterDist; // 【关键】逻辑终点：必须是 X.5

                            // 锁定库存
                            outSlot.EjectReserved++;
                        }
                    }
                }
            }
            // ------------ 情况 B: 这是一个输入端口 (In) ------------
            else if (port.Type == PortType.DirectIn || port.Type == PortType.SideIn)
            {
                ref var inSlot = ref inv.GetInput(0);
                if (inSlot.AvailableSpace > 0)
                {
                    // 调用精准抓取：不到中心点不触发，触发时位置正好在 beltWorldPos
                    var result = TransportSystem.Instance.TryTakeItemAtCenter(targetGrid);

                    if (result.success)
                    {
                        task.Active = true;
                        task.Mode = HandoverMode.Grab;
                        task.Status = HandoverStatus.Flying;
                        task.ItemType = result.itemType;
                        task.InvSlotIndex = 0;

                        // 既然是在中心点抓的，起点就是格子的世界中心，绝对不会跳变！
                        task.StartPos = beltWorldPos;
                        task.EndPos = portWorldPos;
                        task.Progress = 0f;
                        task.Speed = 4.0f; // 抓取飞行速度

                        inSlot.GrabIncoming++;
                    }
                }
            }
        }
    }


    // ================= 工具方法 =================

    private void CleanUpGroundItems(int index, WholeComponent whole)
    {
        ref var core = ref whole.coreComponent[index];
        ref var move = ref whole.moveComponent[index];
        Vector2Int size = core.LogicSize;

        int startX = move.LogicalPosition.x - (size.x - 1) / 2;
        int startY = move.LogicalPosition.y - (size.y - 1) / 2;

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int pos = new Vector2Int(startX + x, startY + y);
                int occId = GridSystem.Instance.GetOccupantId(pos);
                if (occId != -1 && occId != core.SelfHandle.Id)
                {
                    EntityHandle h = EntitySystem.Instance.GetHandleFromId(occId);
                    int tIdx = EntitySystem.Instance.GetIndex(h);
                    if (tIdx != -1 && (whole.coreComponent[tIdx].Type & UnitType.ResourceItem) != 0)
                    {
                        AddGold(10);
                        EntitySystem.Instance.DestroyEntity(h);
                    }
                }
            }
        }
    }


    private void HandleBufferSmartPorts(int index, WholeComponent whole)
    {
        ref var core = ref whole.coreComponent[index];
        ref var work = ref whole.workComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var inv = ref whole.inventoryComponent[index];

        // 1. 获取蓝图定义的原始端口（假设 Port[0] 是左，Port[1] 是右）
        var bp = BlueprintRegistry.Get(core.BlueprintName);

        // 我们需要计算两个端口面对的格子
        for (int p = 0; p < 2 && p < bp.Ports.Length; p++)
        {
            ref var task = ref work.GetTask(p);
            if (task.Active) continue;

            var port = bp.Ports[p];
            Vector2Int rotatedOffset = RotatePortOffsetWithSide(port.Offset, core.Rotation, core.LogicSize);
            Vector2Int rotatedDir = RotateDirection(port.Direction, core.Rotation); // 默认是 (0,-1) 的旋转
            Vector2Int targetGrid = move.LogicalPosition + rotatedOffset + rotatedDir;

            // 2. 核心检测：这个格子上的传送带是怎么流动的？
            int occupantId = GridSystem.Instance.GetOccupantId(targetGrid);
            if (occupantId == -1) continue;

            EntityHandle h = EntitySystem.Instance.GetHandleFromId(occupantId);
            int nIdx = EntitySystem.Instance.GetIndex(h);
            if (nIdx == -1 || whole.workComponent[nIdx].WorkType != WorkType.Conveyor) continue;

            Vector2Int convDir = whole.coreComponent[nIdx].Rotation; // 传送带朝向

            // 3. 极性判定逻辑
            // rotatedDir 是从建筑指向传送带的向量 (比如 0, -1)
            // convDir 是传送带自己的流动向量 (比如 1, 0)

            bool isInput = false;
            bool isOutput = false;

            // 情况 A：传送带直冲着建筑接口来 (垂直进入)
            if (convDir == -rotatedDir)
            {
                isInput = true;
            }
            // 情况 B：传送带背对着建筑接口去 (垂直离开)
            else if (convDir == rotatedDir)
            {
                isOutput = true;
            }
            // 情况 C：传送带横向经过 (平行流动)
            else
            {
                // 这里利用向量叉乘或者简单的坐标比较
                // 计算“建筑的右向量” (相对于当前朝向的右方)
                Vector2Int buildingRight = RotateDirection(Vector2Int.right, core.Rotation);

                // 如果传送带流向和建筑右向量一致，那么左边的口(p=0)是进，右边的口(p=1)是出
                if (convDir == buildingRight)
                {
                    if (p == 0) isInput = true; else isOutput = true;
                }
                // 反之，右边的口是进，左边的口是出
                else if (convDir == -buildingRight)
                {
                    if (p == 1) isInput = true; else isOutput = true;
                }
            }

            // 4. 根据判定结果执行逻辑
            if (isInput)
            {
                ref var inSlot = ref inv.GetInput(0);
                if (inSlot.AvailableSpace > 0)
                {
                    var result = TransportSystem.Instance.TryTakeItemAtCenter(targetGrid);
                    if (result.success)
                    {
                        StartBufferTask(ref task, HandoverMode.Grab, result.itemType, targetGrid, move.LogicalPosition + rotatedOffset);
                        inSlot.GrabIncoming++;
                    }
                }
            }
            else if (isOutput)
            {
                ref var outSlot = ref inv.GetOutput(0);
                if (outSlot.AvailableToEject > 0)
                {
                    if (TransportSystem.Instance.GetLineIDAtGrid(targetGrid, out int lineID, out float rawIdx))
                    {
                        float targetDist = rawIdx + 0.5f;
                        if (TransportSystem.Instance.PredictSpace(lineID, targetDist, 0.2f)) // 这里的0.2s是预估飞行时间
                        {
                            StartBufferTask(ref task, HandoverMode.Eject, outSlot.ItemType, move.LogicalPosition + rotatedOffset, targetGrid);
                            task.TargetLineID = lineID;
                            task.TargetDistance = targetDist;
                            task.Speed = 5.0f; // 吐出速度
                            outSlot.EjectReserved++;
                        }
                    }
                }
            }
        }
    }

    // 辅助方法喵
    private void StartBufferTask(ref HandoverTask task, HandoverMode mode, int itemType, Vector2Int startGrid, Vector2Int endGrid)
    {
        task.Active = true;
        task.Mode = mode;
        task.Status = HandoverStatus.Flying;
        task.ItemType = itemType;
        task.InvSlotIndex = 0;
        task.StartPos = GridSystem.Instance.GridToWorld(startGrid);
        task.EndPos = GridSystem.Instance.GridToWorld(endGrid);
        task.Progress = 0f;
        task.Speed = 5.0f;
    }



    /// <summary>
    /// 方案一：带尺寸补偿的坐标旋转
    /// </summary>
    /// <param name="offset">原始定义的偏移量</param>
    /// <param name="forward">当前的建筑朝向</param>
    /// <param name="size">建筑的逻辑尺寸 (2x2, 3x3等)</param>
    public Vector2Int RotatePortOffsetWithSide(Vector2Int offset, Vector2Int forward, Vector2Int size)
    {
        // 1. 计算几何中心 (Pivot) 
        // 1x1 -> (0,0); 2x2 -> (0.5, 0.5); 3x3 -> (0,0)
        // 这是因为 3x3 坐标范围是 -1,0,1，中点是 0
        // 而 2x2 坐标范围是 0,1，中点是 0.5
        float centerX = (size.x % 2 == 0) ? 0.5f : 0f;
        float centerY = (size.y % 2 == 0) ? 0.5f : 0f;

        // 如果是 2x2 且起始点是 (0,0)，我们需要把 offset 转换成相对于 (0.5, 0.5) 的坐标
        // 此时 (0,0) 变成 (-0.5, -0.5)
        float relX = offset.x - centerX;
        float relY = offset.y - centerY;

        float rotX, rotY;

        // 2. 执行旋转矩阵
        // Vector2Int.up (0,1) 是基准，不旋转
        if (forward == Vector2Int.up)
        {
            rotX = relX;
            rotY = relY;
        }
        else if (forward == Vector2Int.right)
        { // 顺时针 90度 (x,y) -> (y,-x)
            rotX = relY;
            rotY = -relX;
        }
        else if (forward == Vector2Int.down)
        {  // 180度 (x,y) -> (-x,-y)
            rotX = -relX;
            rotY = -relY;
        }
        else if (forward == Vector2Int.left)
        {  // 逆时针 90度 (x,y) -> (-y,x)
            rotX = -relY;
            rotY = relX;
        }
        else
        {
            rotX = relX; rotY = relY;
        }

        // 3. 还原回整数坐标
        return new Vector2Int(
            Mathf.RoundToInt(rotX + centerX),
            Mathf.RoundToInt(rotY + centerY)
        );
    }

    /// <summary>
    /// 方向向量（非坐标）的旋转是不需要补偿的，直接转喵！
    /// </summary>
    public Vector2Int RotateDirection(Vector2Int dir, Vector2Int forward)
    {
        if (forward == Vector2Int.up) return dir;
        if (forward == Vector2Int.right) return new Vector2Int(dir.y, -dir.x);
        if (forward == Vector2Int.down) return new Vector2Int(-dir.x, -dir.y);
        if (forward == Vector2Int.left) return new Vector2Int(-dir.y, dir.x);
        return dir;
    }

    public void AddGold(long amount)
    {
        Gold += amount;
        // 使用我们刚搓好的 PostSystem 广播出去，带上最新的金钱总数喵！
        // 广播 1：给 UI 用的总额
        PostSystem.Instance.Send("更新总金币", Gold);

        // 广播 2：给任务系统用的增量 (amount 而不是 Gold 喵！)
        // 直接传 long，PostSystem 会把它装箱成 object
        PostSystem.Instance.Send("金币更变", amount);
    }
    // 顺便加一个扣钱的方法喵
    public bool SpendGold(long cost)
    {
        if (Gold >= cost)
        {
            Gold -= cost;
            PostSystem.Instance.Send("更新总金币", Gold);
            return true;
        }
        return false; // 没钱买单喵...
    }
    // IndustrialSystem.cs 内部建议增加的私有辅助方法
    private bool IsEntityPowered(int index, WholeComponent whole)
    {
        if (GlobalPowerOverride) return true;
        ref var work = ref whole.workComponent[index];

        // 情况 A：这台机器根本不需要电，直接放行喵！
        if (!work.RequiresPower) return true;

        // 情况 B：机器需要电，但它的通电状态是 false（由 PowerSystem 判定）
        // 或者它根本还没连进任何 NetID
        ref var power = ref whole.powerComponent[index];
        if (!work.IsPowered || power.NetID == -1) return false;

        return true;
    }
}