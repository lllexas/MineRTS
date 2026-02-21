using UnityEngine;

public class SpawnSystem : SingletonMono<SpawnSystem>
{
    /*// 定义搜索顺序：上、右、下、左 (顺时针)
    // 也可以扩展成周围8格
    private readonly Vector2Int[] _neighborOffsets = new Vector2Int[]
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        // 如果需要斜向，可以继续加...
    };

    public void UpdateSpawns(WholeComponent whole, float deltaTime)
    {
        // 1. 缓存当前数量，防止在循环中生成新东西导致死循环或索引错乱
        // 新生成的单位要在下一帧才开始逻辑
        int count = whole.entityCount;
        var entitySystem = EntitySystem.Instance;
        var gridSystem = GridSystem.Instance;

        for (int i = 0; i < count; i++)
        {
            // 直接引用，不拷贝
            ref var spawn = ref whole.spawnComponent[i];

            // 快速过滤：如果不是生产单位，直接跳过
            if (spawn.UnitTypeToSpawn == 0) continue;

            ref var core = ref whole.coreComponent[i];
            if (!core.Active) continue;

            // 2. 计时逻辑
            spawn.Timer -= deltaTime;
            if (spawn.Timer <= 0)
            {
                // 3. 尝试生成逻辑
                // 我们需要从 MoveComponent 知道兵营当前在哪（格坐标）
                ref var spawnerMove = ref whole.moveComponent[i];
                Vector2Int spawnerGridPos = spawnerMove.LogicalPosition;

                // 寻找一个空闲的邻居格子
                Vector2Int spawnPos = FindFreeNeighbor(spawnerGridPos, gridSystem);

                // 如果找到了有效位置 (不是 -1,-1)
                if (spawnPos.x != -1)
                {
                    // 实际生成！
                    // 注意：位置我们要把 网格坐标 转回 世界坐标
                    Vector3 worldPos = new Vector3(spawnPos.x, spawnPos.y, 0);

                    // 调用 EntitySystem 创建
                    entitySystem.CreateEntity(worldPos, core.Team, spawn.UnitTypeToSpawn);

                    // 重置计时器
                    spawn.Timer = spawn.SpawnInterval;
                }
                else
                {
                    // 没地方生了（被围住了）！
                    // 策略：重置一点点时间，下一帧再试，防止卡死逻辑
                    spawn.Timer = 0.5f;
                }
            }
        }
    }

    // 辅助方法：找空地
    private Vector2Int FindFreeNeighbor(Vector2Int center, GridSystem grid)
    {
        foreach (var offset in _neighborOffsets)
        {
            Vector2Int target = center + offset;

            // 检查：是否越界？是否被占？
            if (!grid.IsOccupied(target))
            {
                return target;
            }
        }
        // 如果周围都被堵死了，返回无效值
        return new Vector2Int(-1, -1);
    }*/
}