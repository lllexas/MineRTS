using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 围棋规则系统
/// 基于网格级DSU的围棋规则实现，用于处理单位包围与吃子
/// </summary>
public class GoRuleSystem : SingletonMono<GoRuleSystem>
{
    // 网格尺寸（与地图相同）
    private int _mapWidth;
    private int _mapHeight;
    private int _totalCells;
    private int _minX;
    private int _minY;

    // --- 核心DSU数据结构 ---
    private int[] _dsuParent;      // 并查集父节点数组
    private int[] _gridTeam;       // 格子状态：-1=空气（可通行），-2=墙壁（建筑/障碍），≥0=单位（阵营ID）
    private Dictionary<int, HashSet<int>> _rootLiberties; // 每个根节点的气眼集合

    // --- 对象池复用（避免GC）---
    private Stack<HashSet<int>> _hashSetPool = new Stack<HashSet<int>>();

    // --- 邻居方向偏移（4方向：右、上、左、下）---
    // 性能优化：使用整数数组代替Vector2Int，避免结构体分配
    private static readonly int[] _dx = { 1, 0, -1, 0 };
    private static readonly int[] _dy = { 0, 1, 0, -1 };

    /// <summary>
    /// 初始化围棋规则系统，预分配内存
    /// </summary>
    public void Initialize(int mapWidth, int mapHeight, int minX = -64, int minY = -64)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _minX = minX;
        _minY = minY;
        _totalCells = mapWidth * mapHeight;

        // 分配固定大小数组（0 GC）
        _dsuParent = new int[_totalCells];
        _gridTeam = new int[_totalCells];

        // 初始化并查集（每个节点都是自己的根）
        for (int i = 0; i < _totalCells; i++)
        {
            _dsuParent[i] = i;
        }

        _rootLiberties = new Dictionary<int, HashSet<int>>();

        Debug.Log($"[GoRuleSystem] 初始化完成，地图尺寸：{mapWidth}x{mapHeight}，总格子数：{_totalCells}，坐标偏移：({minX},{minY})");
    }

    /// <summary>
    /// 核心更新方法：执行围棋规则4步管线
    /// 应在每个逻辑Tick调用（移动系统之后，渲染系统之前）
    /// </summary>
    public void UpdateGoRules(WholeComponent whole)
    {
        if (_dsuParent == null || _gridTeam == null)
        {
            Debug.LogWarning("[GoRuleSystem] 系统未初始化，跳过围棋规则更新");
            return;
        }

        // 步骤一：投影网格 (Grid Projection)
        ProjectEntitiesToGrid(whole);

        // 步骤二：极速连气 (Grid Union)
        UnionAdjacentCells();

        // 步骤三：统计气眼 (Liberty Collection)
        CollectLiberties(whole);

        // 步骤四：审判执行 (Execution & Capture)
        ExecuteCapture(whole);
    }

    /// <summary>
    /// 步骤一：将ECS实体数据投影到网格
    /// 遍历所有活跃实体，将其占据的格子标记为相应阵营
    /// </summary>
    private void ProjectEntitiesToGrid(WholeComponent whole)
    {
        // 重置网格状态为-1（空气）
        for (int i = 0; i < _totalCells; i++)
        {
            _gridTeam[i] = -1;
        }

        // 重置并查集（每个节点独立）
        for (int i = 0; i < _totalCells; i++)
        {
            _dsuParent[i] = i;
        }

        // 遍历所有实体
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            if (!core.Active) continue;

            // 检查是否应投影到围棋网格
            // 地面单位（Hero/Minion）和建筑（Building）参与投影，飞行单位、子弹、掉落物不参与
            if (!ShouldProjectToGrid(core.Type)) continue;

            // 获取实体逻辑位置和尺寸
            Vector2Int logicalPos = whole.moveComponent[i].LogicalPosition;
            Vector2Int size = core.LogicSize;

            // 计算实体占据的矩形区域
            int halfWidth = (size.x - 1) / 2;
            int halfHeight = (size.y - 1) / 2;
            int startX = logicalPos.x - halfWidth;
            int startY = logicalPos.y - halfHeight;

            // 标记所有被占据的格子
            int firstCellIndex = -1; // 记录该实体的第一个格子索引，用于强制连通多格子单位
            for (int dx = 0; dx < size.x; dx++)
            {
                for (int dy = 0; dy < size.y; dy++)
                {
                    Vector2Int cellPos = new Vector2Int(startX + dx, startY + dy);
                    int index = GridPosToIndex(cellPos);
                    if (index >= 0 && index < _totalCells)
                    {
                        // 根据实体类型设置格子状态：建筑为墙壁(-2)，单位为阵营ID(≥0)
                        if ((core.Type & UnitType.Building) != 0)
                        {
                            _gridTeam[index] = -2; // 墙壁
                            // 建筑始终不是围棋棋子
                            whole.goComponent[i].IsGoPiece = false;
                        }
                        else
                        {
                            _gridTeam[index] = core.Team; // 单位
                            // 只有单位(Hero/Minion)才是围棋棋子
                            whole.goComponent[i].IsGoPiece = (core.Type & (UnitType.Hero | UnitType.Minion)) != 0;

                            // 强制连通同一个实体的所有格子，避免多格子单位"碎裂"
                            if (firstCellIndex == -1)
                            {
                                firstCellIndex = index;
                            }
                            else if (whole.goComponent[i].IsGoPiece) // 只有围棋棋子才需要连通
                            {
                                Union(firstCellIndex, index);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 步骤二：并查集连结相邻的同阵营格子
    /// 只检查右方和上方邻居，避免重复连结
    /// </summary>
    private void UnionAdjacentCells()
    {
        for (int y = 0; y < _mapHeight; y++)
        {
            for (int x = 0; x < _mapWidth; x++)
            {
                int index = y * _mapWidth + x;
                int team = _gridTeam[index];
                if (team == -1 || team == -2) continue; // 空气或墙壁跳过，只处理单位(≥0)

                // 检查右方邻居
                if (x + 1 < _mapWidth)
                {
                    int rightIndex = index + 1;
                    if (_gridTeam[rightIndex] == team)
                    {
                        Union(index, rightIndex);
                    }
                }

                // 检查上方邻居
                if (y + 1 < _mapHeight)
                {
                    int upIndex = index + _mapWidth;
                    if (_gridTeam[upIndex] == team)
                    {
                        Union(index, upIndex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 步骤三：统计每个棋块的气眼（空相邻格子）
    /// 极致性能优化：直接使用本地坐标，避免Vector2Int分配和复杂函数调用
    /// </summary>
    private void CollectLiberties(WholeComponent whole)
    {
        // 清空气眼字典，将HashSet归还对象池
        foreach (var hashSet in _rootLiberties.Values)
        {
            hashSet.Clear();
            _hashSetPool.Push(hashSet);
        }
        _rootLiberties.Clear();

        // 获取GridSystem实例引用（缓存，避免重复属性访问）
        var gridSys = GridSystem.Instance;

        // 遍历所有有单位的格子
        for (int y = 0; y < _mapHeight; y++)
        {
            for (int x = 0; x < _mapWidth; x++)
            {
                int index = y * _mapWidth + x;
                if (_gridTeam[index] == -1 || _gridTeam[index] == -2) continue; // 空气或墙壁跳过，只处理单位(≥0)

                int root = Find(index);

                // 获取或创建该根节点的气眼集合
                if (!_rootLiberties.TryGetValue(root, out var liberties))
                {
                    liberties = GetHashSetFromPool();
                    _rootLiberties[root] = liberties;
                }

                // 检查4个方向的邻居是否为气眼
                // 极致性能：直接使用坐标偏移数组，避免Vector2Int分配
                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = x + _dx[dir];
                    int ny = y + _dy[dir];

                    // 边界检查（使用本地坐标）
                    if (nx >= 0 && nx < _mapWidth && ny >= 0 && ny < _mapHeight)
                    {
                        int neighborIndex = ny * _mapWidth + nx;

                        // 【终极判定】：
                        // 1. _gridTeam == -1 确保了没有被任何建筑或地面单位占据（不论敌我）
                        // 2. IsTerrainWalkableFast 确保了地形不是墙壁或水
                        if (_gridTeam[neighborIndex] == -1 && gridSys.IsTerrainWalkableFast(neighborIndex))
                        {
                            liberties.Add(neighborIndex);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 步骤四：执行吃子判定，消灭无气的单位
    /// </summary>
    private void ExecuteCapture(WholeComponent whole)
    {
        // 遍历所有参与围棋的实体（只有IsGoPiece=true的单位，建筑已被过滤）
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            if (!core.Active) continue;
            if (!whole.goComponent[i].IsGoPiece) continue;

            // 获取实体中心格子对应的DSU根节点
            Vector2Int logicalPos = whole.moveComponent[i].LogicalPosition;
            int centerIndex = GridPosToIndex(logicalPos);
            if (centerIndex < 0 || centerIndex >= _totalCells) continue;

            int root = Find(centerIndex);

            // 获取气数并更新到组件
            int libertyCount = 0;
            if (_rootLiberties.TryGetValue(root, out var liberties))
            {
                libertyCount = liberties.Count;
            }
            whole.goComponent[i].CurrentLiberties = libertyCount;

            // 吃子判定：气数为0
            if (libertyCount == 0)
            {
                Debug.Log($"[GoRuleSystem] 单位 {i} (阵营{core.Team}) 被包围，执行吃子");

                // 直接设置血量为0，让HealthSystem处理死亡
                whole.healthComponent[i].Health -= 999; // 大幅伤

                // 可选：添加被捕获标记，供特效系统使用
                // 这里可以扩展，比如触发爆炸特效或播放声音
            }
        }
    }

    /// <summary>
    /// 判断单位类型是否应投影到围棋网格
    /// 地面单位（Hero/Minion）和建筑（Building）都参与投影，但处理方式不同：
    /// - 单位：标记为阵营ID(≥0)，参与围棋规则（需要气）
    /// - 建筑：标记为墙壁(-2)，不参与围棋规则（只需阻塞气）
    /// 飞行单位、子弹、掉落物不参与投影
    /// </summary>
    private bool ShouldProjectToGrid(int unitType)
    {
        // 飞行单位、子弹、掉落物不参与投影
        if ((unitType & UnitType.Flyer) != 0) return false;
        if ((unitType & UnitType.Projectile) != 0) return false;
        if ((unitType & UnitType.ResourceItem) != 0) return false;

        // 地面单位（Hero/Minion）和建筑（Building）都参与投影
        return (unitType & (UnitType.Hero | UnitType.Minion | UnitType.Building)) != 0;
    }

    /// <summary>
    /// 检查世界网格坐标是否在有效地图范围内
    /// </summary>
    private bool IsValidGridPosition(Vector2Int worldPos)
    {
        int localX = worldPos.x - _minX;
        int localY = worldPos.y - _minY;
        return localX >= 0 && localX < _mapWidth && localY >= 0 && localY < _mapHeight;
    }


    /// <summary>
    /// 世界网格坐标转换为1D数组索引
    /// </summary>
    private int GridPosToIndex(Vector2Int worldPos)
    {
        if (!IsValidGridPosition(worldPos)) return -1;
        int localX = worldPos.x - _minX;
        int localY = worldPos.y - _minY;
        return localY * _mapWidth + localX;
    }

    /// <summary>
    /// 1D数组索引转换为世界网格坐标
    /// </summary>
    private Vector2Int IndexToGridPos(int index)
    {
        if (index < 0 || index >= _totalCells) return new Vector2Int(-1, -1);
        int localX = index % _mapWidth;
        int localY = index / _mapWidth;
        return new Vector2Int(localX + _minX, localY + _minY);
    }

    // --- 并查集核心操作 ---
    private int Find(int x)
    {
        // 路径压缩
        if (_dsuParent[x] != x)
        {
            _dsuParent[x] = Find(_dsuParent[x]);
        }
        return _dsuParent[x];
    }

    private void Union(int x, int y)
    {
        int rootX = Find(x);
        int rootY = Find(y);
        if (rootX != rootY)
        {
            _dsuParent[rootY] = rootX;
        }
    }

    // --- 对象池管理 ---
    private HashSet<int> GetHashSetFromPool()
    {
        if (_hashSetPool.Count > 0)
        {
            return _hashSetPool.Pop();
        }
        return new HashSet<int>();
    }

    private void ReturnHashSetToPool(HashSet<int> set)
    {
        set.Clear();
        _hashSetPool.Push(set);
    }

    /// <summary>
    /// 调试方法：绘制围棋网格和棋块气数
    /// </summary>
    public void DrawDebugGizmos()
    {
        if (_gridTeam == null) return;

        // 这里可以添加Gizmos绘制逻辑
        // 比如用不同颜色显示不同阵营的格子
        // 或者显示每个棋块的气眼位置
    }
}