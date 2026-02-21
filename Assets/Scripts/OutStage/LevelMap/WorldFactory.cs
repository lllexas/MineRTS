using UnityEngine;

public static class WorldFactory
{
    /// <summary>
    /// 从静态 JSON 资源中创建一个全新的世界状态
    /// </summary>
    public static WholeComponent CreateNewWorldFromLevelID(string levelID)
    {
        // 1. 加载 JSON 文件 (假设放在 Resources/Levels/ 下)
        TextAsset jsonAsset = Resources.Load<TextAsset>($"Levels/{levelID}");
        if (jsonAsset == null)
        {
            Debug.LogError($"<color=red>严重错误：找不到关卡数据 {levelID}</color>");
            return null;
        }

        // 2. 解析元数据
        LevelMapData levelData = JsonUtility.FromJson<LevelMapData>(jsonAsset.text);

        // 3. 构建运行时 WholeComponent
        WholeComponent newWorld = new WholeComponent();

        // --- 基础信息填充 ---
        newWorld.sceneName = levelData.levelId;
        newWorld.mapWidth = levelData.width;
        newWorld.mapHeight = levelData.height;
        newWorld.entityCount = 0; // 初始没有动态实体
        newWorld.nextEntityId = 1;
        newWorld.minX = levelData.originX;
        newWorld.minY = levelData.originY;

        // --- 地图数据拷贝 ---
        // 注意：必须 Clone，否则改了 newWorld 可能会影响到缓存的 levelData
        newWorld.groundMap = (int[])levelData.groundMap.Clone();
        newWorld.gridMap = (int[])levelData.gridMap.Clone();
        newWorld.effectMap = (int[])levelData.effectMap.Clone();

        // --- 初始化实体组件数组 ---
        // 这里需要给一个初始容量，比如 1000 个实体
        // 建议：WholeComponent 内部应该有一个 InitializeArrays(int capacity) 方法
        int initialCap = 1024;
        InitArrays(newWorld, initialCap);

        Debug.Log($"<color=cyan>[WorldFactory]</color> 根据元数据成功构建新世界: {levelID}");
        return newWorld;
    }

    private static void InitArrays(WholeComponent wc, int cap)
    {
        // 帮主人把数组 new 出来，防止空指针
        wc.coreComponent = new CoreComponent[cap];
        wc.moveComponent = new MoveComponent[cap];
        wc.attackComponent = new AttackComponent[cap];
        wc.healthComponent = new HealthComponent[cap];
        wc.spawnComponent = new SpawnComponent[cap];
        wc.drawComponent = new DrawComponent[cap];
        wc.aiComponent = new AIComponent[cap];
        wc.userControlComponent = new UserControlComponent[cap];

        // 工业组件
        wc.resourceComponent = new ResourceComponent[cap];
        wc.inventoryComponent = new InventoryComponent[cap];
        wc.workComponent = new WorkComponent[cap];
        wc.conveyorComponent = new ConveyorComponent[cap];
        wc.powerComponent = new PowerComponent[cap];

        wc.projectileComponent = new ProjectileComponent[cap];
    }
}