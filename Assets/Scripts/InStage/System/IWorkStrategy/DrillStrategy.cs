using UnityEngine;

public class DrillStrategy : IWorkStrategy
{
    public void Tick(int index, WholeComponent whole, float deltaTime)
    {
        ref var work = ref whole.workComponent[index];
        ref var inv = ref whole.inventoryComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var core = ref whole.coreComponent[index];
        ref var power = ref whole.powerComponent[index]; // 获取电力组件

        // 0. 获取蓝图配置 (实际开发建议缓存，不要每帧查字典)
        var bp = BlueprintRegistry.Get(core.BlueprintName);

        // 如果这玩意不需要电，那满意度默认 1.0，需求 0
        float satisfaction = 1.0f;

        if (bp.RequiresPower)
        {
            // 1. 获取电力系统给的满足率
            // (由 PowerSystem 上一帧计算填入)
            satisfaction = power.CurrentSatisfaction;

            // 如果没联网 (NetID == -1)，那 satisfaction 强制为 0
            if (power.NetID == -1) satisfaction = 0f;

            // 更新 IsPowered 标志位，仅用于 UI 显示亮灯/灭灯
            work.IsPowered = satisfaction > 0.01f;
        }

        // --- 逻辑分歧：决定我要申请多少电 ---

        ref var outSlot = ref inv.GetOutput(0);

        // 扫描矿物 (这里假设扫描开销不大，或者可以优化为不用每帧扫)
        // 为了性能，建议把 count 存到 WorkComponent 里，只有移动/建造时刷新
        ScanAreaMinerals(move.LogicalPosition, core.LogicSize, whole, out int resourceCount, out int resourceType);

        // 2. 判断是否处于“阻塞/无事可做”状态
        bool isIdle = false;

        if (outSlot.Count >= outSlot.MaxCapacity) isIdle = true; // 出口堵了
        if (resourceCount <= 0) isIdle = true;                   // 地下没矿了

        // 3. 设置下一帧的电力需求 (Demand)
        if (bp.RequiresPower)
        {
            if (isIdle)
            {
                power.Demand = bp.IdleEnergy; // 申请待机功耗 (例如 10W)

                // 如果是待机状态，通常就不跑进度条了，或者只跑待机动画
                // 即使有电，因为 idle，所以直接 return，不产出
                return;
            }
            else
            {
                power.Demand = bp.WorkEnergy; // 申请工作功耗 (例如 100W)
            }
        }

        // 4. 执行工作 (受电压影响)
        // 如果满足率只有 0.5 (电网供电不足)，矿机速度就减半
        if (satisfaction > 0.001f)
        {
            // 注意：satisfaction 乘在速度上！
            float realSpeed = work.WorkSpeed * resourceCount * satisfaction;

            work.Progress += deltaTime * realSpeed;

            if (work.Progress >= 1.0f)
            {
                // 尝试产出
                if (outSlot.TryAdd(resourceType, 1) > 0)
                {
                    work.Progress = 0f;
                }
                else
                {
                    // 没塞进去（极罕见情况，因为上面判断了 isIdle）
                    work.Progress = 1.0f;
                }
            }
        }
    }
    public void ScanAreaMinerals(Vector2Int center, Vector2Int size, WholeComponent whole, out int count, out int type)
    {
        count = 0; type = 0;
        int startX = center.x - (size.x - 1) / 2;
        int startY = center.y - (size.y - 1) / 2;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int idx = GridSystem.Instance.ToIndex(new Vector2Int(startX + x, startY + y));
                if (idx != -1 && MapRegistry.IsMineable(whole.groundMap[idx]))
                {
                    count++;
                    type = MapRegistry.GetResourceType(whole.groundMap[idx]);
                }
            }
        }
    }
}
