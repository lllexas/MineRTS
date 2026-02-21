using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapSyncManager : SingletonMono<TilemapSyncManager>
{
    [Header("配置引用")]
    public Tilemap targetTilemap;

    [System.Serializable]
    public struct TileIDMapping
    {
        public TileBase tileAsset;
        public int tileID;
    }

    [Header("ID 映射表")]
    public List<TileIDMapping> tileMappings = new List<TileIDMapping>();

    // 为了快速查找，内部转成字典
    private Dictionary<TileBase, int> _assetToID = new Dictionary<TileBase, int>();
    private Dictionary<int, TileBase> _idToAsset = new Dictionary<int, TileBase>();

    private void Start()
    {
        InitializeMapping();
    }

    public void InitializeMapping()
    {
        _assetToID.Clear();
        _idToAsset.Clear();
        foreach (var mapping in tileMappings)
        {
            if (mapping.tileAsset != null)
            {
                _assetToID[mapping.tileAsset] = mapping.tileID;
                _idToAsset[mapping.tileID] = mapping.tileAsset;
            }
        }
    }

    /// <summary>
    /// 【同步 A】从 Tilemap 读取数据并填充到 ECS 的 groundMap
    /// </summary>
    public void SyncFromTilemap()
    {
        var whole = EntitySystem.Instance.wholeComponent;
        var gridSys = GridSystem.Instance;

        if (whole == null || targetTilemap == null) return;

        InitializeMapping();

        // 遍历整个 ECS 地图尺寸
        for (int y = 0; y < whole.mapHeight; y++)
        {
            for (int x = 0; x < whole.mapWidth; x++)
            {
                // 1. 根据 ECS 索引推算出网格坐标
                // 注意：这里需要配合 GridSystem 的 minX, minY
                Vector2Int gridPos = new Vector2Int(x + GridSystem.Instance.MinX, y + GridSystem.Instance.MinY);

                // 2. 读取 Tilemap 上的 Tile
                TileBase tile = targetTilemap.GetTile((Vector3Int)gridPos);

                // 3. 转换 ID 并存入 groundMap
                if (tile != null && _assetToID.TryGetValue(tile, out int id))
                {
                    whole.groundMap[y * whole.mapWidth + x] = id;
                }
                else
                {
                    whole.groundMap[y * whole.mapWidth + x] = 0; // 默认地面
                }
            }
        }
        Debug.Log("<color=cyan>Tilemap 数据已成功同步至 groundMap 喵！</color>");
    }

    /// <summary>
    /// 【同步 B】将 groundMap 的数据应用到 Tilemap 进行渲染更新
    /// </summary>
    public void SyncToTilemap()
    {
        var whole = EntitySystem.Instance.wholeComponent;
        if (whole == null || targetTilemap == null) return;

        targetTilemap.ClearAllTiles();

        for (int y = 0; y < whole.mapHeight; y++)
        {
            for (int x = 0; x < whole.mapWidth; x++)
            {
                int id = whole.groundMap[y * whole.mapWidth + x];
                if (id == 0) continue; // 假设 0 是空或基础地面

                if (_idToAsset.TryGetValue(id, out TileBase asset))
                {
                    Vector2Int gridPos = new Vector2Int(x + GridSystem.Instance.MinX, y + GridSystem.Instance.MinY);
                    targetTilemap.SetTile((Vector3Int)gridPos, asset);
                }
            }
        }
        Debug.Log("<color=cyan>groundMap 已回显至 Tilemap 渲染喵！</color>");
    }
    public int GetTileID(TileBase tile)
    {
        if (tile == null) return 0;
        // 如果字典还没初始化，尝试初始化（防止编辑器下报错）
        if (_assetToID == null || _assetToID.Count == 0) InitializeMapping();

        if (_assetToID.TryGetValue(tile, out int id))
        {
            return id;
        }
        // 如果找不到，看情况是返回 0 还是报错
        // Debug.LogWarning($"未知的 Tile: {tile.name}");
        return 0;
    }
}