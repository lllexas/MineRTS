using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public enum CellOccupancyType
{
    Free,            // 完全空闲，随便走
    TerrainBlocked,  // 地形阻挡（水、深渊），死路一条
    Building,        // 建筑阻挡（工厂、墙），静态死路
    Unit,            // 单位占据（战友、敌人），动态阻挡（可以考虑排队或绕行）
    OutOfBounds      // 地图外
}
public partial class GridSystem : SingletonMono<GridSystem>
{
    private float _cellSize = 1.0f;
    // 基础坐标偏移（用于支持负数坐标，比如中心是 0,0）
    private int _minX, _minY;
    public int MinX => _minX;
    public int MinY => _minY;

    public float CellSize => _cellSize;

    public void Initialize(int minX, int minY, float cellSize = 1.0f)
    {
        _minX = minX;
        _minY = minY;
        _cellSize = cellSize;
    }

    // --- 核心翻译逻辑 ---

    // 2D 坐标 -> 1D 数组下标
    public int ToIndex(Vector2Int pos)
    {
        var whole = EntitySystem.Instance.wholeComponent;
        int x = pos.x - _minX;
        int y = pos.y - _minY;
        if (x < 0 || x >= whole.mapWidth || y < 0 || y >= whole.mapHeight) return -1;
        return y * whole.mapWidth + x;
    }

    public Vector2Int WorldToGrid(Vector2 worldPos) => new Vector2Int(Mathf.RoundToInt(worldPos.x / _cellSize), Mathf.RoundToInt(worldPos.y / _cellSize));
    public Vector2 GridToWorld(Vector2Int gridPos, Vector2Int size)
    {
        // 1. 还原出该单位占据的矩形左下角起始格子 (逻辑与 SetOccupantRect 保持一致)
        float startX = gridPos.x - (size.x - 1) / 2;
        float startY = gridPos.y - (size.y - 1) / 2;

        // 2. 视觉中心 = 起始点 + 尺寸的一半
        // 比如 1x1 在 (0,0): startX=0, center = 0 + 0.5 = 0.5 (正好是格子中心)
        // 比如 2x2 在 (0,0): startX=0, center = 0 + 1.0 = 1.0 (正好是四个格子交界中心)
        float centerX = (startX + size.x / 2.0f) * _cellSize;
        float centerY = (startY + size.y / 2.0f) * _cellSize;

        return new Vector2(centerX, centerY);
    }

    // 为了兼容旧代码，保留一个 1x1 的默认版本
    public Vector2 GridToWorld(Vector2Int gridPos) => GridToWorld(gridPos, Vector2Int.one);
    // --- 地图层访问接口 ---

    public bool IsInBounds(Vector2Int pos) => ToIndex(pos) != -1;

    // 获取占据者 ID (Handle.Id)
    public int GetOccupantId(Vector2Int pos)
    {
        int idx = ToIndex(pos);
        if (idx == -1) return -1;
        return EntitySystem.Instance.wholeComponent.gridMap[idx];
    }

    // 设置占据 (矩形，支持 1x1 到 NxN)
    public void SetOccupantRect(Vector2Int center, Vector2Int size, int handleId)
    {
        var whole = EntitySystem.Instance.wholeComponent;
        int startX = center.x - (size.x - 1) / 2;
        int startY = center.y - (size.y - 1) / 2;

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int idx = ToIndex(new Vector2Int(startX + x, startY + y));
                if (idx != -1) whole.gridMap[idx] = handleId;
            }
        }
    }

    // 清理占据 (矩形)
    public void ClearOccupantRect(Vector2Int center, Vector2Int size) => SetOccupantRect(center, size, -1);

    public bool IsWalkable(Vector2Int pos)
    {
        int idx = ToIndex(pos);
        if (idx == -1) return false;
        var whole = EntitySystem.Instance.wholeComponent;

        // 1. 检查实体占据 (保持原样)
        int occupantId = whole.gridMap[idx];
        if (occupantId != -1)
        {
            EntityHandle handle = EntitySystem.Instance.GetHandleFromId(occupantId);
            if (EntitySystem.Instance.IsValid(handle)) return false;
            else whole.gridMap[idx] = -1; // 清理幽灵
        }

        // 2. 【核心修改】检查地块属性
        int tileId = whole.groundMap[idx];
        return MapRegistry.IsWalkable(tileId); // 调用我们刚才写的百科全书喵！
    }

    /// <summary>
    /// 检查一个矩形区域是否完全空闲
    /// 新增 ignoreId 参数：如果是单位自己占用的格子，不视为阻塞
    /// </summary>
    public bool IsAreaClear(Vector2Int center, Vector2Int size, int ignoreId = -1)
    {
        int startX = center.x - (size.x - 1) / 2;
        int startY = center.y - (size.y - 1) / 2;

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int pos = new Vector2Int(startX + x, startY + y);
                int idx = ToIndex(pos);

                if (idx == -1) return false; // 越界

                // 1. 检查实体占据
                int occupantId = EntitySystem.Instance.wholeComponent.gridMap[idx];
                if (occupantId != -1 && occupantId != ignoreId) // 【关键】如果是自己，就不算堵塞
                {
                    EntityHandle handle = EntitySystem.Instance.GetHandleFromId(occupantId);
                    if (EntitySystem.Instance.IsValid(handle)) return false;
                }

                // 2. 检查地形
                int tileId = EntitySystem.Instance.wholeComponent.groundMap[idx];
                if (!MapRegistry.IsWalkable(tileId)) return false;
            }
        }
        return true;
    }
    public CellOccupancyType GetCellOccupancy(Vector2Int pos)
    {
        int idx = ToIndex(pos);
        if (idx == -1) return CellOccupancyType.OutOfBounds;

        var whole = EntitySystem.Instance.wholeComponent;

        // 1. 先查地形层
        int tileId = whole.groundMap[idx];
        if (!MapRegistry.IsWalkable(tileId))
        {
            return CellOccupancyType.TerrainBlocked;
        }

        // 2. 查实体层
        int occupantId = whole.gridMap[idx];
        if (occupantId != -1)
        {
            EntityHandle handle = EntitySystem.Instance.GetHandleFromId(occupantId);
            if (EntitySystem.Instance.IsValid(handle))
            {
                int dataIndex = EntitySystem.Instance.GetIndex(handle);
                int type = whole.coreComponent[dataIndex].Type;

                // 根据位掩码判断类型
                if ((type & UnitType.Building) != 0)
                    return CellOccupancyType.Building;

                if ((type & (UnitType.Hero | UnitType.Minion)) != 0)
                    return CellOccupancyType.Unit;

                // 资源包或子弹暂不视为阻挡（除非有特殊逻辑）
                return CellOccupancyType.Free;
            }
            else
            {
                // 顺便清理一下过期的“幽灵”数据
                whole.gridMap[idx] = -1;
            }
        }

        return CellOccupancyType.Free;
    }

    /// <summary>
    /// 针对多格单位 (LogicSize) 的矩形探测版本
    /// </summary>
    public CellOccupancyType GetAreaOccupancy(Vector2Int center, Vector2Int size, int ignoreId = -1)
    {
        int startX = center.x - (size.x - 1) / 2;
        int startY = center.y - (size.y - 1) / 2;

        CellOccupancyType worstCase = CellOccupancyType.Free;

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int pos = new Vector2Int(startX + x, startY + y);
                // 如果检查到自己占据的格子，跳过
                if (GetOccupantId(pos) == ignoreId && ignoreId != -1) continue;

                CellOccupancyType current = GetCellOccupancy(pos);

                // 优先级判定：地形 > 建筑 > 单位
                if (current == CellOccupancyType.OutOfBounds || current == CellOccupancyType.TerrainBlocked)
                    return CellOccupancyType.TerrainBlocked;

                if (current == CellOccupancyType.Building)
                    worstCase = CellOccupancyType.Building;
                else if (current == CellOccupancyType.Unit && worstCase == CellOccupancyType.Free)
                    worstCase = CellOccupancyType.Unit;
            }
        }
        return worstCase;
    }
    /// <summary>
     /// 【核心包装】仅针对静态环境（地形+建筑）的通过性判定。
     /// 用于 NavMesh 矩形剖分，忽略动态单位。
     /// </summary>
    private bool IsWalkableStatic(Vector2Int pos)
    {
        CellOccupancyType occ = GetCellOccupancy(pos);

        // 对于 NavMesh 来说：
        // 1. 空地 (Free) 当然可以走。
        // 2. 有单位 (Unit) 也可以走，因为单位会动，不属于永久地形。
        if (occ == CellOccupancyType.Free || occ == CellOccupancyType.Unit)
        {
            return true;
        }

        // 3. 建筑 (Building)、地形阻挡 (TerrainBlocked)、越界 (OutOfBounds) 全是墙！
        return false;
    }
    public static Vector2Int GetMouseGridPos(Vector2Int size)
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -Camera.main.transform.position.z;
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

        // 偶数尺寸（2, 4）需要减去 0.5 偏移，使 [0.5, 1.5] 范围映射到逻辑坐标 0
        float offsetX = (size.x % 2 == 0) ? 0.5f : 0f;
        float offsetY = (size.y % 2 == 0) ? 0.5f : 0f;

        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x - offsetX + 0.01f), // 加个极小值防止浮点数打架
            Mathf.FloorToInt(worldPos.y - offsetY + 0.01f)
        );
    }
    /// <summary>
    /// 【新增】当加载新地图时，更新网格的边界参数
    /// </summary>
    public void UpdateMapSize(int minX, int minY, float cellSize)
    {
        _minX = minX;
        _minY = minY;
        _cellSize = cellSize;
        Debug.Log($"<color=cyan>[GridSystem]</color> 网格参数已更新: Min({minX},{minY}), CellSize: {cellSize}");
    }

    /// <summary>
    /// 【新增】彻底清空网格占据状态 (EntitySystem.ClearWorld 需要调用这个)
    /// </summary>
    public void ClearAll()
    {
        var whole = EntitySystem.Instance.wholeComponent;
        if (whole == null) return;

        // 1. 清理物理占据图
        if (whole.gridMap != null)
            System.Array.Fill(whole.gridMap, -1);

        // 2. 🔥【核心修复】清理 NavMesh 数据结构
        _allNodes.Clear();
        _nodeIdCounter = 0;

        // 3. 🔥【核心修复】清理格子到节点的映射表，必须全部填 -1
        if (_gridToNodeMap != null)
        {
            System.Array.Fill(_gridToNodeMap, -1);
        }

        Debug.Log("<color=orange>[GridSystem]</color> 物理占据与 NavMesh 拓扑已重置喵。");
    }
}

public class NavNode
{
    public int Id;
    public RectInt Area;
    public Vector2 Center;

    // 邻接表：保留邻居 Id，方便 A* 迭代
    public HashSet<int> Neighbors = new HashSet<int>();

    // 🔥【新增】快速查找：邻居 Id -> 对应门户的直接引用
    // 这样 A* 访问到邻居时，可以 O(1) 拿到几何数据
    public Dictionary<int, NavPortal> Portals = new Dictionary<int, NavPortal>();

    public NavNode(int id, RectInt area, float cellSize)
    {
        Id = id;
        Area = area;
        Center = new Vector2(area.x + area.width * 0.5f, area.y + area.height * 0.5f) * cellSize;
    }

    /// <summary>
    /// 获取该节点下，除了指定门户之外的所有其他门户
    /// </summary>
    /// <param name="excludePortalId">当前正在尝试但堵塞的门户 ID</param>
    /// <returns>备选门户列表</returns>
    public List<NavPortal> GetAlternativePortals(int excludePortalId)
    {
        List<NavPortal> alternatives = new List<NavPortal>();
        foreach (var kvp in Portals)
        {
            if (kvp.Value.Id != excludePortalId)
            {
                alternatives.Add(kvp.Value);
            }
        }
        // 喵！这里可以根据距离进行排序，优先尝试离当前单位近的门
        return alternatives;
    }

    public enum Rotation { CW, CCW }

    /// <summary>
    /// 严谨获取本房间边界上邻接的下一个门户。
    /// 逻辑：先试直线方向，如果直线门不属于我，说明到头了，立刻尝试拐角方向。
    /// </summary>
    public NavPortal GetAdjacentPortalInNode(NavPortal current, Rotation dir)
    {
        if (current == null) return null;

        // 1. 几何技巧：判定当前门在本房间的哪面墙上
        bool isNorth = !current.IsVertical && current.FixedCoord == Area.yMax;
        bool isSouth = !current.IsVertical && current.FixedCoord == Area.yMin;
        bool isWest = current.IsVertical && current.FixedCoord == Area.xMin;
        bool isEast = current.IsVertical && current.FixedCoord == Area.xMax;

        NavPortal next = null;

        if (dir == Rotation.CW) // 顺时针巡航
        {
            if (isNorth) next = GetMyPortal(current.E, current.ES); // 北墙向东看
            else if (isEast) next = GetMyPortal(current.S, current.WS); // 东墙向南看
            else if (isSouth) next = GetMyPortal(current.W, current.WN); // 南墙向西看
            else if (isWest) next = GetMyPortal(current.N, current.EN); // 西墙向北看
        }
        else // 逆时针巡航
        {
            if (isNorth) next = GetMyPortal(current.W, current.WN); // 北墙向西看
            else if (isWest) next = GetMyPortal(current.S, current.ES); // 西墙向南看
            else if (isSouth) next = GetMyPortal(current.E, current.EN); // 南墙向东看
            else if (isEast) next = GetMyPortal(current.N, current.WN); // 东墙向北看
        }

        return next;
    }

    /// <summary>
    /// 内部决策：在两个可能的拓扑邻居中，选出真正属于本房间的那一个
    /// </summary>
    private NavPortal GetMyPortal(NavPortal straight, NavPortal corner)
    {
        // 优先检查直线方向
        if (straight != null && (straight.NodeA == this.Id || straight.NodeB == this.Id))
        {
            return straight;
        }

        // 如果直线方向没有门，或者那个门是隔壁房间的（边界到头了），则检查拐角
        if (corner != null && (corner.NodeA == this.Id || corner.NodeB == this.Id))
        {
            return corner;
        }

        return null; // 这个方向真的没门了，喵
    }
}
//---------------------------
// 改动 1：定义门户实体
//---------------------------
public class NavPortal
{
    public int Id;
    public bool IsVertical; // 垂直门(跨X), false: 水平门(跨Y)
    public int FixedCoord;  // 门所在的固定轴坐标
    public int Min;         // 自由轴范围
    public int Max;

    // 关联的两个节点
    public int NodeA;
    public int NodeB;


    // 邻接
    public NavPortal N;
    public NavPortal EN;
    public NavPortal E;
    public NavPortal ES;
    public NavPortal S;
    public NavPortal WS;
    public NavPortal W;
    public NavPortal WN;


    // 获取该门户的中心点（用于仲裁参考）
    public float Center => (Min + Max) * 0.5f;

    /// <summary>
    /// 核心：时刻表。
    /// Key: 门户内的相对索引 (0 到 Max-Min)。
    /// Value: ulong 掩码。每一位代表一个逻辑 Tick。
    /// 1 表示该 Tick 该格已被预定，0 表示空闲。
    /// </summary>
    public ulong[] Timetables;

    public void InitializeTimetable()
    {
        int laneCount = Max - Min + 1;
        Timetables = new ulong[laneCount];
    }

    /// <summary>
    /// 尝试在特定车道预定一段时间
    /// </summary>
    /// <param name="laneIdx">相对门户 Min 的偏移</param>
    /// <param name="startTick">起始时刻 (相对于全局当前 Tick 的偏移)</param>
    /// <param name="duration">占用多久 (比如 3 Ticks)</param>
    public bool TryReserve(int laneIdx, int startTick, int duration, out ulong requestMask)
    {
        requestMask = 0;
        // 参数有效性检查
        if (laneIdx < 0 || laneIdx >= Timetables.Length || startTick < 0 || duration <= 0 || duration >= 64 || startTick + duration > 64)
            return false;

        // 构造请求掩码，例如 duration=3, startTick=5 -> 0b11100000 (低位在右)
        requestMask = ((1UL << duration) - 1) << startTick;

        if ((Timetables[laneIdx] & requestMask) == 0)
        {
            Timetables[laneIdx] |= requestMask;
            return true;
        }
        return false;
    }
}

public partial class GridSystem
{
    // 全局门户查找表：Key 是 (IsVertical, FixedCoord)
    // Value 是该直线上的所有门户，按 Min 排序
    private Dictionary<(bool, int), List<NavPortal>> _collinearGroups = new Dictionary<(bool, int), List<NavPortal>>();

    // 快速索引：通过节点对 (NodeA, NodeB) 找门户
    private Dictionary<(int, int), NavPortal> _portalLookup = new Dictionary<(int, int), NavPortal>();

    private int _portalIdCounter = 0;

    // 清理逻辑（在 RebuildNavMesh 开始时调用）
    private void ClearPortals()
    {
        _collinearGroups.Clear();
        _portalLookup.Clear();
        _portalIdCounter = 0;
    }
    private void FinalizePortalTopology()
    {
        List<NavPortal> allPortals = new List<NavPortal>(_portalLookup.Values);

        // 清空所有邻接引用
        foreach (var p in allPortals)
        {
            p.N = p.S = p.E = p.W = null;
            p.EN = p.ES = p.WN = p.WS = null;
        }

        // 建立连接
        for (int i = 0; i < allPortals.Count; i++)
        {
            for (int j = i + 1; j < allPortals.Count; j++)
            {
                NavPortal a = allPortals[i];
                NavPortal b = allPortals[j];

                if (a.IsVertical == b.IsVertical)
                {
                    // 【共线邻接】在你的系统中，Max 应该等于 Min
                    if (a.FixedCoord == b.FixedCoord)
                    {
                        if (a.Max == b.Min)
                        {
                            if (a.IsVertical) { a.N = b; b.S = a; }
                            else { a.E = b; b.W = a; }
                        }
                        else if (b.Max == a.Min)
                        {
                            if (a.IsVertical) { b.N = a; a.S = b; }
                            else { b.E = a; a.W = b; }
                        }
                    }
                }
                else
                {
                    // 【转角邻接】异向门户，边缘坐标重合
                    NavPortal v = a.IsVertical ? a : b;
                    NavPortal h = a.IsVertical ? b : a;

                    // 垂直门的北端 (Max) 接触水平门
                    if (v.Max == h.FixedCoord)
                    {
                        // 垂直门线刚好是水平门的起始点 (东向转角)
                        if (v.FixedCoord == h.Min) { v.EN = h; h.WN = v; }
                        // 垂直门线刚好是水平门的终点 (西向转角)
                        else if (v.FixedCoord == h.Max) { v.WN = h; h.EN = v; }
                    }
                    // 垂直门的南端 (Min) 接触水平门
                    else if (v.Min == h.FixedCoord)
                    {
                        if (v.FixedCoord == h.Min) { v.ES = h; h.WS = v; }
                        else if (v.FixedCoord == h.Max) { v.WS = h; h.ES = v; }
                    }
                }
            }
        }
    }
    private void RegisterPortal(NavNode nodeA, NavNode nodeB, bool isVert, int fixedCoord, int min, int max)
    {
        var portal = new NavPortal
        {
            Id = _portalIdCounter++,
            IsVertical = isVert,
            FixedCoord = fixedCoord,
            Min = min,
            Max = max,
            NodeA = nodeA.Id,
            NodeB = nodeB.Id
        };

        portal.InitializeTimetable(); // 必须要有这一行！
        // --- 🔥 关键绑定逻辑 ---
        // 把门户塞进两个节点的字典里，通过对方的 ID 作为索引
        nodeA.Portals[nodeB.Id] = portal;
        nodeB.Portals[nodeA.Id] = portal;

        // 存入节点对查找表 (逻辑保持不变)
        var key = nodeA.Id < nodeB.Id ? (nodeA.Id, nodeB.Id) : (nodeB.Id, nodeA.Id);
        _portalLookup[key] = portal;

        // 存入共线组 (逻辑保持不变)
        var lineKey = (isVert, fixedCoord);
        if (!_collinearGroups.TryGetValue(lineKey, out var list))
        {
            list = new List<NavPortal>();
            _collinearGroups[lineKey] = list;
        }
        list.Add(portal);
    }

    public string GetNavMeshDebugInfo()
    {
        if (_allNodes == null || _allNodes.Count == 0) return "NavMesh is empty.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"<color=yellow>--- NavMesh Topology Stats ---</color>");
        sb.AppendLine($"Total Nodes: {_allNodes.Count} | Total Portals: {_portalLookup.Count}");

        foreach (var nodeEntry in _allNodes)
        {
            NavNode node = nodeEntry.Value;
            sb.AppendLine($"\n<b>[Node {node.Id}]</b> Area: {node.Area}");

            if (node.Portals.Count == 0)
            {
                sb.AppendLine("  <color=red>! No Portals connected.</color>");
                continue;
            }

            foreach (var portalEntry in node.Portals)
            {
                int neighborId = portalEntry.Key;
                NavPortal p = portalEntry.Value;

                // 1. 基础几何信息
                string type = p.IsVertical ? "VERT" : "HORZ";
                string axis = p.IsVertical ? "X" : "Y";
                sb.AppendLine($"  --> <b>Portal {p.Id}</b> (to Node {neighborId}) | {type} at {axis}={p.FixedCoord}, Range: {p.Min}-{p.Max}");

                // 2. 8 方向拓扑连接信息 (核心！)
                sb.Append("      Topology: ");
                sb.Append(FormatDir("N", p.N));
                sb.Append(FormatDir("S", p.S));
                sb.Append(FormatDir("E", p.E));
                sb.Append(FormatDir("W", p.W));
                sb.Append(FormatDir("EN", p.EN));
                sb.Append(FormatDir("ES", p.ES));
                sb.Append(FormatDir("WN", p.WN));
                sb.Append(FormatDir("WS", p.WS));
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    // 辅助格式化函数，让 Log 看起来更整齐
    private string FormatDir(string label, NavPortal target)
    {
        string id = target != null ? target.Id.ToString() : "-";
        return $"[{label}:{id}] ";
    }
}

public partial class GridSystem : SingletonMono<GridSystem>
{
    // --- NavMesh 核心存储 ---
    private int _nodeIdCounter = 0;
    // 所有的矩形节点 (Global Id -> Node)
    private Dictionary<int, NavNode> _allNodes = new Dictionary<int, NavNode>();
    // 全局映射表：格子索引 -> 所属 NodeId
    private int[] _gridToNodeMap;

    // --- 寻路对外接口 ---
    public NavNode GetNodeAt(Vector2Int gridPos)
    {
        int idx = ToIndex(gridPos);
        if (idx == -1 || _gridToNodeMap == null) return null;
        int id = _gridToNodeMap[idx];
        return id != -1 && _allNodes.ContainsKey(id) ? _allNodes[id] : null;
    }

    /// <summary>
    /// Bresenham 视线回退法：当目标点在墙内或建筑内时，沿着从目标点到起点的直线回退，
    /// 找到第一个合法的格子作为修正后的目标点。
    /// </summary>
    /// <param name="targetPos">非法的目标点（墙内/建筑内）</param>
    /// <param name="startPos">单位所在的起点</param>
    /// <returns>修正后的合法目标点，如果找不到则返回起点</returns>
    public Vector2Int GetValidTargetByLineOfSight(Vector2Int targetPos, Vector2Int startPos)
    {
        // 如果目标点本身就是合法的，直接返回
        if (GetNodeAt(targetPos) != null)
            return targetPos;

        // 标准 Bresenham 算法参数
        int dx = Mathf.Abs(targetPos.x - startPos.x);
        int dy = Mathf.Abs(targetPos.y - startPos.y);
        int sx = targetPos.x < startPos.x ? 1 : -1; // 从目标点向起点移动，方向与正常相反
        int sy = targetPos.y < startPos.y ? 1 : -1;
        int err = dx - dy;

        Vector2Int current = targetPos;

        // 最大步数限制，避免无限循环
        int maxSteps = dx + dy + 10;
        int steps = 0;

        while (steps < maxSteps)
        {
            // 如果已经到达起点，停止搜索
            if (current == startPos)
                break;

            // 标准 Bresenham 算法：决定移动方向
            int e2 = 2 * err;
            bool moveX = e2 > -dy;
            bool moveY = e2 < dx;

            if (moveX)
            {
                err -= dy;
                current.x += sx; // 向起点方向移动
            }
            if (moveY)
            {
                err += dx;
                current.y += sy; // 向起点方向移动
            }

            // 检查移动后的格子是否合法
            if (GetNodeAt(current) != null)
                return current;

            steps++;
        }

        // 如果整条线上都没有合法格子，返回起点（理论上不应该发生）
        return startPos;
    }

    // --- 核心：全局/局部重剖逻辑 ---

    /// <summary>
    /// 当建筑变动时，指定一个包围盒进行重构。
    /// 逻辑：
    /// 1. 找到所有与此范围相交的旧矩形。
    /// 2. 撤销它们，归还空间。
    /// 3. 对涉及的“总包围盒”重新执行贪婪切分。
    /// </summary>
    public void RebuildNavMesh(RectInt dirtyArea)
    {
        // 🔥【修复点】如果数组是刚创建的，必须填 -1 而不是留着默认的 0
        if (_gridToNodeMap == null || _gridToNodeMap.Length == 0)
        {
            _gridToNodeMap = new int[EntitySystem.Instance.wholeComponent.gridMap.Length];
            System.Array.Fill(_gridToNodeMap, -1);
        }
        // 1. 寻找受影响的旧节点
        HashSet<int> affectedNodeIds = new HashSet<int>();
        for (int y = dirtyArea.yMin; y <= dirtyArea.yMax; y++)
        {
            for (int x = dirtyArea.xMin; x <= dirtyArea.xMax; x++)
            {
                int id = GetNodeIdInternal(new Vector2Int(x, y));
                if (id != -1) affectedNodeIds.Add(id);
            }
        }

        // 2. 确定“重绘包围盒”：受灾节点构成的最大矩形
        // 这样可以保证新生成的长条矩形能和旧的边缘无缝对接
        RectInt rebuildBox = dirtyArea;
        foreach (int id in affectedNodeIds)
        {
            NavNode node = _allNodes[id];
            // 取并集
            int minX = Mathf.Min(rebuildBox.xMin, node.Area.xMin);
            int minY = Mathf.Min(rebuildBox.yMin, node.Area.yMin);
            int maxX = Mathf.Max(rebuildBox.xMax, node.Area.xMax);
            int maxY = Mathf.Max(rebuildBox.yMax, node.Area.yMax);
            rebuildBox = new RectInt(minX, minY, maxX - minX, maxY - minY);

            // 从全局图中抹除
            RemoveNodeInternal(id);
        }

        // 3. 执行贪婪切分 (横向扫描优先)
        GreedyRectPartition(rebuildBox);

        // 4. 重建所有受灾范围内及其边缘的邻接关系
        // 这一步是“奢侈”的，但保证了拓扑的绝对正确
        RebuildAdjacency(rebuildBox);

        // 5. 🔥 【最后一步】缝合共线门户
        // 只有执行了这一步，PrevCollinear 和 NextCollinear 才有值！
        FinalizePortalTopology();
    }

    private void GreedyRectPartition(RectInt box)
    {
        // 确保映射表已准备好
        if (_gridToNodeMap == null || _gridToNodeMap.Length == 0)
        {
            _gridToNodeMap = new int[EntitySystem.Instance.wholeComponent.gridMap.Length];
            System.Array.Fill(_gridToNodeMap, -1);
        }

        // 这里使用世界坐标进行循环，但在 visited 数组中使用相对 box 的局部坐标
        int bMinX = box.xMin;
        int bMinY = box.yMin;
        int bMaxX = box.xMax;
        int bMaxY = box.yMax;

        bool[,] visited = new bool[box.width + 1, box.height + 1];

        for (int y = bMinY; y < bMaxY; y++)
        {
            for (int x = bMinX; x < bMaxX; x++)
            {
                int localX = x - bMinX;
                int localY = y - bMinY;

                // 如果已经访问过，或者是静态阻挡，跳过
                if (visited[localX, localY] || !IsWalkableStatic(new Vector2Int(x, y)))
                    continue;

                // --- 1. 贪婪向右扩张 (确定宽度) ---
                int width = 0;
                while (x + width < bMaxX)
                {
                    if (visited[localX + width, localY] || !IsWalkableStatic(new Vector2Int(x + width, y)))
                        break;
                    width++;
                }

                // --- 2. 贪婪向下扩张 (确定高度) ---
                int height = 1;
                while (y + height < bMaxY)
                {
                    bool rowOk = true;
                    for (int i = 0; i < width; i++)
                    {
                        // 检查整行是否都可作为该矩形的延伸
                        if (visited[localX + i, localY + height] || !IsWalkableStatic(new Vector2Int(x + i, y + height)))
                        {
                            rowOk = false;
                            break;
                        }
                    }
                    if (!rowOk) break;
                    height++;
                }

                // --- 3. 确定了最大空矩形，生成节点 ---
                RectInt area = new RectInt(x, y, width, height);
                NavNode newNode = new NavNode(_nodeIdCounter++, area, _cellSize);
                _allNodes.Add(newNode.Id, newNode);

                // --- 4. 登记占用状态 ---
                for (int h = 0; h < height; h++)
                {
                    for (int w = 0; w < width; w++)
                    {
                        visited[localX + w, localY + h] = true;
                        int globalIdx = ToIndex(new Vector2Int(x + w, y + h));
                        if (globalIdx != -1)
                        {
                            _gridToNodeMap[globalIdx] = newNode.Id;
                        }
                    }
                }
            }
        }
    }

    private void RebuildAdjacency(RectInt box)
    {
        // 稍微外扩一圈，找到所有可能需要重连的节点
        HashSet<int> nearbyNodes = new HashSet<int>();
        for (int y = box.yMin - 1; y <= box.yMax + 1; y++)
        {
            for (int x = box.xMin - 1; x <= box.xMax + 1; x++)
            {
                int id = GetNodeIdInternal(new Vector2Int(x, y));
                if (id != -1) nearbyNodes.Add(id);
            }
        }

        // 清理旧邻居并重新探测
        foreach (int id in nearbyNodes)
        {
            if (!_allNodes.TryGetValue(id, out var node)) continue;
            node.Neighbors.Clear();

            // 探测四边
            DetectAndConnect(node);
        }
    }

    private void DetectAndConnect(NavNode node)
    {
        RectInt a = node.Area;
        // 探测上边缘外一排
        ScanBorder(new Vector2Int(a.x, a.yMax), new Vector2Int(a.width, 1), node);
        // 探测下边缘外一排
        ScanBorder(new Vector2Int(a.x, a.yMin - 1), new Vector2Int(a.width, 1), node);
        // 探测左边缘外一排
        ScanBorder(new Vector2Int(a.xMin - 1, a.y), new Vector2Int(1, a.height), node);
        // 探测右边缘外一排
        ScanBorder(new Vector2Int(a.xMax, a.y), new Vector2Int(1, a.height), node);
    }

    private void ScanBorder(Vector2Int start, Vector2Int size, NavNode self)
    {
        // 🔥 【核心修改 1】：引入一个临时集合，记录这一条边上探测到的所有唯一邻居 ID
        // 这样无论这条边多长，同一个邻居只会进入逻辑一次喵！
        HashSet<int> neighborsOnThisBorder = new HashSet<int>();

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int currentGrid = new Vector2Int(start.x + x, start.y + y);
                int otherId = GetNodeIdInternal(currentGrid);

                if (otherId != -1 && otherId != self.Id)
                {
                    neighborsOnThisBorder.Add(otherId);
                }
            }
        }

        // 🔥 【核心修改 2】：扫描完这条边后，对发现的每个邻居执行一次逻辑
        foreach (int otherId in neighborsOnThisBorder)
        {
            if (!_allNodes.TryGetValue(otherId, out var other)) continue;

            // 记录基础邻居关系（HashSet 会自动处理全局去重）
            self.Neighbors.Add(otherId);
            other.Neighbors.Add(self.Id);

            // 只有当 A < B 时才注册，防止一对邻居被注册两次
            // 且现在每个 ScanBorder 周期内，针对同一个 otherId 只会跑一次 RegisterPortal
            if (self.Id < otherId)
            {
                if (GetPortal(self, other, out _, out _, out _))
                {
                    RectInt a = self.Area;
                    RectInt b = other.Area;
                    bool isVert = (a.xMax == b.xMin || a.xMin == b.xMax);

                    int fixedCoord, min, max;
                    if (isVert)
                    {
                        fixedCoord = (a.xMax == b.xMin) ? a.xMax : a.xMin;
                        min = Mathf.Max(a.yMin, b.yMin);
                        max = Mathf.Min(a.yMax, b.yMax);
                    }
                    else
                    {
                        fixedCoord = (a.yMax == b.yMin) ? a.yMax : a.yMin;
                        min = Mathf.Max(a.xMin, b.xMin);
                        max = Mathf.Min(a.xMax, b.xMax);
                    }

                    // 只有在这里才会真正消耗 _portalIdCounter++ 喵！
                    RegisterPortal(self, other, isVert, fixedCoord, min, max);
                }
            }
        }
    }

    private void RemoveNodeInternal(int id)
    {
        if (!_allNodes.TryGetValue(id, out var node)) return;

        // 1. 从所有邻居的记录中抹除本节点
        foreach (var neighborId in node.Neighbors)
        {
            if (_allNodes.TryGetValue(neighborId, out var neighbor))
            {
                neighbor.Neighbors.Remove(id);
                neighbor.Portals.Remove(id); // 🔥 同时清理字典引用
            }
        }

        // 2. 清理门户全局查找表
        List<(int, int)> keysToRemove = new List<(int, int)>();
        foreach (var key in _portalLookup.Keys)
        {
            if (key.Item1 == id || key.Item2 == id) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove) _portalLookup.Remove(key);

        // 3. 清理格子映射图
        RectInt a = node.Area;
        for (int y = a.yMin; y < a.yMax; y++)
            for (int x = a.xMin; x < a.xMax; x++)
                _gridToNodeMap[ToIndex(new Vector2Int(x, y))] = -1;

        _allNodes.Remove(id);
    }

    private int GetNodeIdInternal(Vector2Int pos)
    {
        int idx = ToIndex(pos);
        if (idx == -1 || _gridToNodeMap == null) return -1;
        return _gridToNodeMap[idx];
    }
    
    /// <summary>
    /// 【核心接口】通过全局唯一的节点 ID 获取 NavNode 对象。
    /// 这是 PathfindingSystem 与 NavMesh 图交互的桥梁。
    /// </summary>
    /// <param name="id">要查找的节点 ID</param>
    /// <returns>如果找到则返回 NavNode，否则返回 null</returns>
    public NavNode GetNode(int id)
    {
        if (id != -1 && _allNodes.TryGetValue(id, out var node))
        {
            return node;
        }
        return null;
    }
    /// <summary>
    /// 【核心】计算两个相邻 NavNode 之间的公共边（门户）。
    /// 返回是否相邻，以及公共边的线段范围（世界坐标）。
    /// </summary>
    /// <param name="nodeA">当前节点</param>
    /// <param name="nodeB">邻居节点</param>
    /// <param name="portalLeft">门户左端点（相对于前进方向）</param>
    /// <param name="portalRight">门户右端点</param>
    /// <param name="overlapLen">接触长度（格子数）</param>
    public bool GetPortal(NavNode nodeA, NavNode nodeB, out Vector2 portalLeft, out Vector2 portalRight, out int overlapLen)
    {
        portalLeft = Vector2.zero;
        portalRight = Vector2.zero;
        overlapLen = 0;

        RectInt a = nodeA.Area;
        RectInt b = nodeB.Area;

        // 1. 判断接触方向
        // 情况A: B在A的右边 (垂直边)
        if (a.xMax == b.xMin)
        {
            int yMin = Mathf.Max(a.yMin, b.yMin);
            int yMax = Mathf.Min(a.yMax, b.yMax);
            overlapLen = yMax - yMin;
            if (overlapLen <= 0) return false;

            // 垂直边的X坐标
            float x = a.xMax * _cellSize;
            // Y区间转换世界坐标
            portalLeft = new Vector2(x, yMax * _cellSize); // 上方点
            portalRight = new Vector2(x, yMin * _cellSize); // 下方点
            return true;
        }
        // 情况B: B在A的左边 (垂直边)
        else if (a.xMin == b.xMax)
        {
            int yMin = Mathf.Max(a.yMin, b.yMin);
            int yMax = Mathf.Min(a.yMax, b.yMax);
            overlapLen = yMax - yMin;
            if (overlapLen <= 0) return false;

            float x = a.xMin * _cellSize;
            // 注意左右是相对于"从A走到B"的方向，所以左和右要反过来或者根据视点定
            // 这里为了漏斗算法方便，统一按"左侧墙角"和"右侧墙角"定义
            portalLeft = new Vector2(x, yMin * _cellSize);
            portalRight = new Vector2(x, yMax * _cellSize);
            return true;
        }
        // 情况C: B在A的上边 (水平边)
        else if (a.yMax == b.yMin)
        {
            int xMin = Mathf.Max(a.xMin, b.xMin);
            int xMax = Mathf.Min(a.xMax, b.xMax);
            overlapLen = xMax - xMin;
            if (overlapLen <= 0) return false;

            float y = a.yMax * _cellSize;
            portalLeft = new Vector2(xMin * _cellSize, y);
            portalRight = new Vector2(xMax * _cellSize, y);
            return true;
        }
        // 情况D: B在A的下边 (水平边)
        else if (a.yMin == b.yMax)
        {
            int xMin = Mathf.Max(a.xMin, b.xMin);
            int xMax = Mathf.Min(a.xMax, b.xMax);
            overlapLen = xMax - xMin;
            if (overlapLen <= 0) return false;

            float y = a.yMin * _cellSize;
            portalLeft = new Vector2(xMax * _cellSize, y);
            portalRight = new Vector2(xMin * _cellSize, y);
            return true;
        }

        return false;
    }
}

public partial class GridSystem
{
    [Header("Debug 可视化开关")]
    public bool drawNavMesh = true;
    public bool drawNeighbors = true;
    public bool showNodeIds = true;

    private void OnDrawGizmos()
    {
        // 仅在运行时且有数据时绘制
        if (!Application.isPlaying || _allNodes == null || _allNodes.Count == 0) return;

        if (!drawNavMesh) return;

        foreach (var node in _allNodes.Values)
        {
            // 1. 绘制矩形本体
            Gizmos.color = new Color(0, 1, 1, 0.2f); // 半透明青色

            // 计算矩形在世界空间的大小和中心
            // Area 是网格坐标，需要乘以 CellSize
            Vector3 size = new Vector3(node.Area.width * _cellSize, node.Area.height * _cellSize, 0.1f);
            Vector3 center = new Vector3(
                (node.Area.x + node.Area.width * 0.5f) * _cellSize,
                (node.Area.y + node.Area.height * 0.5f) * _cellSize,
                0
            );

            Gizmos.DrawCube(center, size);

            // 绘制边框线，让矩形更清晰
            Gizmos.color = new Color(0, 1, 1, 0.5f);
            Gizmos.DrawWireCube(center, size);

            // 2. 绘制邻接连线
            if (drawNeighbors)
            {
                Gizmos.color = Color.red;
                foreach (int neighborId in node.Neighbors)
                {
                    // 为了防止重复画线，只画 Id 比自己大的邻居
                    if (neighborId > node.Id && _allNodes.TryGetValue(neighborId, out var neighbor))
                    {
                        // 在两个矩形中心点之间画一条红线
                        Gizmos.DrawLine(center, new Vector3(neighbor.Center.x, neighbor.Center.y, 0));
                    }
                }
            }

            // 3. 在场景中显示 ID (Handles 绘图)
#if UNITY_EDITOR
            if (showNodeIds)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.fontSize = 10;
                style.alignment = TextAnchor.MiddleCenter;

                Handles.Label(center, $"ID:{node.Id}", style);
            }
#endif
        }

        // 4. 特殊高亮：鼠标指向的节点
        DrawMouseHighlight();
    }

    private void DrawMouseHighlight()
    {
        Vector2Int mouseGrid = GetMouseGridPos(Vector2Int.one);
        NavNode hoveredNode = GetNodeAt(mouseGrid);

        if (hoveredNode != null)
        {
            Gizmos.color = new Color(1, 0.92f, 0.016f, 0.5f); // 亮黄色
            Vector3 size = new Vector3(hoveredNode.Area.width * _cellSize, hoveredNode.Area.height * _cellSize, 0.2f);
            Vector3 center = new Vector3(
                (hoveredNode.Area.x + hoveredNode.Area.width * 0.5f) * _cellSize,
                (hoveredNode.Area.y + hoveredNode.Area.height * 0.5f) * _cellSize,
                0
            );
            Gizmos.DrawCube(center, size);

            // 顺便把它的邻居也高亮一下，方便检查拓扑
            Gizmos.color = Color.green;
            foreach (int nId in hoveredNode.Neighbors)
            {
                if (_allNodes.TryGetValue(nId, out var nNode))
                {
                    Vector3 nCenter = new Vector3(
                        (nNode.Area.x + nNode.Area.width * 0.5f) * _cellSize,
                        (nNode.Area.y + nNode.Area.height * 0.5f) * _cellSize,
                        0
                    );
                    Gizmos.DrawLine(center, nCenter);
                }
            }
        }
    }
}

public partial class GridSystem
{
    // 让仲裁系统能拿到所有的门户
    public IEnumerable<NavPortal> GetAllPortals() => _portalLookup.Values;

    // 【关键】每过一拍，清理一次时刻表
    public void AdvanceNavMeshTick(long currentTick)
    {
        int bitIdx = (int)(currentTick % 64);
        ulong clearMask = ~(1UL << bitIdx); // 清除当前这一位
        foreach (var portal in _portalLookup.Values)
        {
            if (portal.Timetables == null) continue;
            for (int i = 0; i < portal.Timetables.Length; i++)
            {
                portal.Timetables[i] &= clearMask;
            }
        }
    }
}