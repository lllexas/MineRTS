using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;

public class LevelBakerWindow : EditorWindow
{
    // --- 配置 ---
    private string levelId = "Level_01";
    private string savePath = "Assets/Resources/Levels/"; // 默认存到 Resources 方便加载

    // --- 场景引用 ---
    // 您可能有多个 Tilemap 对应不同的逻辑层
    public Tilemap groundTilemap;
    public Tilemap gridTilemap;
    public Tilemap effectTilemap;

    // 引用之前的同步管理器来获取 ID 映射
    public TilemapSyncManager syncManager;

    [MenuItem("Tools/猫娘助手/关卡烘焙机 (Level Baker)")]
    public static void ShowWindow()
    {
        GetWindow<LevelBakerWindow>("Level Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("🐱 关卡数据烘焙工厂", EditorStyles.boldLabel);

        GUILayout.Space(10);
        levelId = EditorGUILayout.TextField("Level ID", levelId);
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        GUILayout.Space(10);
        GUILayout.Label("拖入场景中的对象：", EditorStyles.label);
        syncManager = (TilemapSyncManager)EditorGUILayout.ObjectField("Sync Manager", syncManager, typeof(TilemapSyncManager), true);
        groundTilemap = (Tilemap)EditorGUILayout.ObjectField("Ground Tilemap", groundTilemap, typeof(Tilemap), true);
        gridTilemap = (Tilemap)EditorGUILayout.ObjectField("Grid Tilemap", gridTilemap, typeof(Tilemap), true);
        effectTilemap = (Tilemap)EditorGUILayout.ObjectField("Effect Tilemap", effectTilemap, typeof(Tilemap), true);

        GUILayout.Space(20);

        if (GUILayout.Button("开始烘焙 JSON (Bake)!", GUILayout.Height(40)))
        {
            BakeLevel();
        }
    }

    private void BakeLevel()
    {
        if (syncManager == null)
        {
            Debug.LogError("喵！找不到 TilemapSyncManager，无法解析 Tile ID！");
            return;
        }

        // 1. 强制初始化映射表（因为编辑器模式下 Start 可能没跑）
        // 这里利用反射或者简单地把 InitializeMapping 改为 public
        // 假设您已经把 TilemapSyncManager.InitializeMapping 改为 public 了
        syncManager.InitializeMapping();

        // 2. 计算边界 (Bounds)
        // 我们需要找到包含所有三个图层的最大矩形
        BoundsInt bounds = new BoundsInt();
        if (groundTilemap) bounds = GetMaxBounds(bounds, groundTilemap);
        if (gridTilemap) bounds = GetMaxBounds(bounds, gridTilemap);
        if (effectTilemap) bounds = GetMaxBounds(bounds, effectTilemap);

        // 稍作修正，防止空包
        if (bounds.size.x == 0 || bounds.size.y == 0)
        {
            Debug.LogError("所有 Tilemap 都是空的喵！无法烘焙！");
            return;
        }

        // 3. 构建数据对象
        LevelMapData data = new LevelMapData();
        data.levelId = levelId;
        data.width = bounds.size.x;
        data.height = bounds.size.y;
        data.originX = bounds.xMin;
        data.originY = bounds.yMin;
        Debug.Log($"地图烘焙范围: Origin({data.originX}, {data.originY}), Size({data.width}x{data.height})");

        // 4. 填充数据
        // 注意：Tilemap 的原点可能不是 (0,0)，我们需要把数据平移到从 0 开始的数组里
        data.groundMap = BakeLayer(groundTilemap, bounds);
        data.gridMap = BakeLayer(gridTilemap, bounds);
        data.effectMap = BakeLayer(effectTilemap, bounds);

        // 5. 序列化为 JSON
        string json = JsonUtility.ToJson(data, true); // true 表示格式化输出，好看一点

        // 6. 写入文件
        if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
        string finalPath = Path.Combine(savePath, $"{levelId}.json");
        File.WriteAllText(finalPath, json);

        AssetDatabase.Refresh(); // 刷新资源管理器
        Debug.Log($"<color=green>关卡 [{levelId}] 烘焙完成喵！保存至：{finalPath}</color>");
        Debug.Log($"地图尺寸: {data.width}x{data.height}, 原点偏移: {bounds.min}");
    }

    // 辅助：获取并集包围盒
    private BoundsInt GetMaxBounds(BoundsInt current, Tilemap map)
    {
        map.CompressBounds();
        if (current.size.x == 0) return map.cellBounds;

        int xMin = Mathf.Min(current.xMin, map.cellBounds.xMin);
        int yMin = Mathf.Min(current.yMin, map.cellBounds.yMin);
        int xMax = Mathf.Max(current.xMax, map.cellBounds.xMax);
        int yMax = Mathf.Max(current.yMax, map.cellBounds.yMax);

        return new BoundsInt(xMin, yMin, 0, xMax - xMin, yMax - yMin, 1);
    }

    private int[] BakeLayer(Tilemap map, BoundsInt bounds)
    {
        int size = bounds.size.x * bounds.size.y;
        int[] result = new int[size];

        if (map == null) return result;

        for (int y = 0; y < bounds.size.y; y++)
        {
            for (int x = 0; x < bounds.size.x; x++)
            {
                Vector3Int worldPos = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                TileBase tile = map.GetTile(worldPos);
                int id = 0;

                if (tile != null)
                {
                    id = syncManager.GetTileID(tile);

                    // 🔥【核心修改】Tile 丢失报警器
                    // 如果地图上有 Tile，但 ID 却是 0，说明忘记在 SyncManager 里注册了！
                    if (id == 0)
                    {
                        Debug.LogError($"<color=red>【严重警告】</color> 发现未注册的 Tile！\n" +
                                       $"位置: {worldPos}, 资源名: <b>{tile.name}</b>\n" +
                                       $"请在 TilemapSyncManager 的列表中添加它，并分配一个非 0 的 ID！");
                    }
                }
                result[y * bounds.size.x + x] = id;
            }
        }
        return result;
    }
}