public static class MapRegistry
{
    // --- 逻辑分类查询 ---

    // 哪些地块是可以行走的？ (ID 0 和 1)
    public static bool IsWalkable(int tileId)
    {
        return tileId != 100;
    }

    // 哪些地块是矿物？ (ID 102 和 103)
    public static bool IsMineable(int tileId)
    {
        return tileId == 102 || tileId == 103;
    }

    // 根据地块 ID 获取对应的资源类型
    // 返回值对应 ResourceComponent.ResourceType (1: 矿A, 2: 矿B)
    public static int GetResourceType(int tileId)
    {
        return tileId switch
        {
            102 => 1, // 地板测试_8 产出 1号资源
            103 => 2, // 地板测试_9 产出 2号资源
            _ => 0    // 其他地块不产出资源
        };
    }
}