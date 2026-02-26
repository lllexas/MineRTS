using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// 1. A* 搜索元数据
public class NavPathNode
{
    public NavNode Node;
    public float G;
    public float H;
    public float F => G + H;
    public NavPathNode Parent;
    public int SessionId;

    public void Reset(NavNode node)
    {
        Node = node;
        G = float.MaxValue;
        H = 0;
        Parent = null;
    }
}

public partial class PathfindingSystem : SingletonMono<PathfindingSystem>
{
    private Queue<int> _pathRequests = new Queue<int>();
    private int _maxSearchesPerFrame = 10;

    private Stack<List<Vector2Int>> _pathListPool = new Stack<List<Vector2Int>>();
    private Dictionary<int, NavPathNode> _nodeMetadataMap = new Dictionary<int, NavPathNode>();

    private List<NavNode> _lastDebugNodePath = new List<NavNode>();

    [Header("Debug 设置")]
    public bool showSearchLogs = true; // 喵！在这里一键开关日志
    public struct Waypoint
    {
        public Vector2Int Pos;
        public bool IsVerticalGate; // 是否是垂直门（跨X）
        public bool IsHorizontalGate; // 是否是水平门（跨Y）
        public bool IsEndpoint; // 起点或终点

        // 【新增】门户的物理通行范围
        // 用于仲裁系统进行动态车道分配。
        // 如果是垂直门，这里存储 Y 的范围；如果是水平门，这里存储 X 的范围。
        public int RangeMin;
        public int RangeMax;
        public NavPortal Portal; // 新增：所属的门户对象
        public NavNode Node; // <--- 新增：所属的 NavNode
        public bool isRightOrUp; // 新增：从门前到门后的方向，true表示右或上，false表示左或下
    }
    public void RequestPath(int entityIndex)
    {
        var whole = EntitySystem.Instance.wholeComponent;
        if (!whole.moveComponent[entityIndex].IsPathPending)
        {
            _pathRequests.Enqueue(entityIndex);
            whole.moveComponent[entityIndex].IsPathPending = true;
        }
    }

    public void UpdatePathfinding(WholeComponent whole)
    {
        int processed = 0;
        while (_pathRequests.Count > 0 && processed < _maxSearchesPerFrame)
        {
            int idx = _pathRequests.Dequeue();
            if (whole.coreComponent[idx].Active)
                ComputePath(whole, idx);
            else
                whole.moveComponent[idx].IsPathPending = false;
            processed++;
        }
    }

    private void ComputePath(WholeComponent whole, int idx)
    {
        ref var move = ref whole.moveComponent[idx];
        ref var core = ref whole.coreComponent[idx];

        Vector2Int startPos = move.LogicalPosition;
        Vector2Int endPos = move.TargetGridPosition;

        // --- 1. 启动日志 ---
        if (showSearchLogs)
            Debug.Log($"<color=yellow>[ComputePath]</color> 单位 {idx} 发起请求: {startPos} -> {endPos}");

        NavNode startNode = GridSystem.Instance.GetNodeAt(startPos);
        NavNode endNode = GridSystem.Instance.GetNodeAt(endPos);

        if (startNode == null)
        {
            if (showSearchLogs) Debug.LogWarning($"<color=orange>[Pathfinding]</color> 单位 {idx} 起点格不在任何 Node 内，尝试周边救命逻辑...");

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    startNode = GridSystem.Instance.GetNodeAt(startPos + new Vector2Int(x, y));
                    if (startNode != null) break;
                }
                if (startNode != null) break;
            }
        }

        // --- 2. 终点抢救逻辑 (Bresenham 视线回退法) ---
        if (endNode == null)
        {
            if (showSearchLogs) Debug.LogWarning($"<color=orange>[Pathfinding]</color> 单位 {idx} 终点格 {endPos} 在墙内/建筑内，尝试视线回退抢救...");

            // 使用 Bresenham 视线回退法寻找最近的合法格子
            Vector2Int correctedEndPos = GridSystem.Instance.GetValidTargetByLineOfSight(endPos, startPos);
            if (correctedEndPos != endPos)
            {
                if (showSearchLogs) Debug.Log($"<color=cyan>[Pathfinding]</color> 终点修正: {endPos} -> {correctedEndPos}");

                // 更新目标位置
                endPos = correctedEndPos;
                move.TargetGridPosition = correctedEndPos;

                // 重新获取终点节点
                endNode = GridSystem.Instance.GetNodeAt(correctedEndPos);
            }
        }

        // --- 3. 节点有效性检查 ---
        if (startNode == null || endNode == null)
        {
            if (showSearchLogs)
            {
                string reason = "";
                if (startNode == null) reason += "【起点 Node 找不到】";
                if (endNode == null) reason += "【终点 Node 找不到】";
                Debug.LogError($"<color=red>[Pathfinding Error]</color> 单位 {idx} 寻路中断: {reason} | Target: {endPos}");
            }

            move.Waypoints = null;
            move.IsPathPending = false;
            return;
        }

        ClearMetadataMap();

        // --- 3. A* 房间搜索 ---
        List<NavNode> nodePath = FindNodePath(startNode, endNode, startPos, endPos);
        if (nodePath != null)
        {
            // 欧拉折线算法
            List<Waypoint> waypoints = PlanMinimalEulerPath(startPos, endPos, nodePath, core.LogicSize);

            move.Waypoints = waypoints;
            move.WaypointIndex = 0;

            move.HasNextStep = false;
            move.IsBlocked = false;
            move.BlockWaitTimer = 0f;

            _lastDebugNodePath = nodePath;

            // --- 4. 成功日志 ---
            if (showSearchLogs)
                Debug.Log($"<color=cyan>[Pathfinding Success]</color> 单位 {idx} 寻路成功，生成了 {waypoints.Count} 个路点喵！");
        }
        else
        {
            // --- 5. 算法失败日志 ---
            if (showSearchLogs)
                Debug.LogWarning($"<color=red>[Pathfinding Fail]</color> 单位 {idx} 的 A* 搜索未能连接两个房间，地图可能是断开的。");

            move.Waypoints = null;
        }

        move.IsPathPending = false;
    }

    // 在 PathfindingSystem 类里增加一个计数器
    private int _currentSearchSessionId = 0;

    private List<NavNode> FindNodePath(NavNode start, NavNode end, Vector2Int actualStart, Vector2Int actualEnd)
    {
        // 防御性编程：检查节点是否有效
        if (start == null || end == null)
        {
            Debug.LogWarning($"[PathfindingSystem] 寻路失败: 起始节点{(start == null ? "null" : "valid")} -> 目标节点{(end == null ? "null" : "valid")}. NavMesh可能尚未就绪。");
            return null;
        }

        _currentSearchSessionId++; // 每次寻路，身份证号 +1

        // 我们改用一个更简单的 OpenList 结构，或者确保重置逻辑万无一失
        List<NavPathNode> openList = new List<NavPathNode>();
        HashSet<int> openSet = new HashSet<int>(); // 快速检查是否在 openList 中
        HashSet<int> closedList = new HashSet<int>();

        NavPathNode startPathNode = GetMetadata(start);
        // 重置所有元数据（这里建议直接在 GetMetadata 里通过 SessionId 判断是否需要重置）
        ResetNodeMeta(startPathNode);

        startPathNode.G = 0;
        startPathNode.H = GetManhattanDist(actualStart, actualEnd);
        openList.Add(startPathNode);
        openSet.Add(start.Id);

        while (openList.Count > 0)
        {
            // 1. 找到 F 最小的
            int bestIdx = 0;
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].F < openList[bestIdx].F) bestIdx = i;
                else if (openList[i].F == openList[bestIdx].F && openList[i].H < openList[bestIdx].H) bestIdx = i;
            }

            NavPathNode current = openList[bestIdx];
            if (current.Node == end) return RetraceNodePath(current);

            openList.RemoveAt(bestIdx);
            openSet.Remove(current.Node.Id);
            closedList.Add(current.Node.Id);

            foreach (int neighborId in current.Node.Neighbors)
            {
                if (closedList.Contains(neighborId)) continue;

                // 如果这个邻居没有对应的门户数据，说明它是“虚假邻居”，直接跳过喵！
                if (!current.Node.Portals.TryGetValue(neighborId, out NavPortal portal))
                {
                    continue;
                }

                NavNode neighbor = GridSystem.Instance.GetNode(neighborId);
                // --- 优化点：更严谨的代价计算 ---
                // 代价 = 当前点到门口的距离 + 门口到邻居中心的距离
                float stepCost = Vector2.Distance(current.Node.Center, neighbor.Center);
                float tentativeG = current.G + stepCost + 0.1f;

                NavPathNode neighborMeta = GetMetadata(neighbor);

                // 如果这个节点是属于旧的搜索，强制重置它
                if (neighborMeta.SessionId != _currentSearchSessionId)
                {
                    ResetNodeMeta(neighborMeta);
                    neighborMeta.SessionId = _currentSearchSessionId;
                }

                if (tentativeG < neighborMeta.G)
                {
                    neighborMeta.Parent = current;
                    neighborMeta.G = tentativeG;
                    // H 应该始终相对于最终目标点
                    neighborMeta.H = GetManhattanDist(neighbor.Center, new Vector2(actualEnd.x, actualEnd.y));

                    if (!openSet.Contains(neighborId))
                    {
                        openList.Add(neighborMeta);
                        openSet.Add(neighborId);
                    }
                }
            }
        }
        return null;
    }

    // 辅助：重置元数据
    private void ResetNodeMeta(NavPathNode meta)
    {
        meta.G = float.MaxValue;
        meta.H = 0;
        meta.Parent = null;
        meta.SessionId = _currentSearchSessionId;
    }
    private float GetManhattanDist(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private List<Waypoint> PlanManhattanWaypoints(Vector2Int start, Vector2Int end, List<NavNode> nodePath, Vector2Int unitSize)
    {
        int count = nodePath.Count - 1;
        if (count < 1) return new List<Waypoint> {
        new Waypoint { Pos = start, IsEndpoint = true },
        new Waypoint { Pos = end, IsEndpoint = true }
    };

        // 1. 【性能优化】预计算调整后的节点边界
        // 这里的 Area 已经是 RectInt (网格坐标)，不再需要任何 RoundToInt
        RectInt[] nodeBounds = new RectInt[nodePath.Count];
        for (int k = 0; k < nodePath.Count; k++)
        {
            RectInt area = nodePath[k].Area;
            // 计算单位左下角点的合法挪动范围：[x, x + width - unitSize.x]
            // 注意：这里不使用 cs，直接在网格空间操作，效率最高
            nodeBounds[k] = new RectInt(
                area.x,
                area.y,
                Mathf.Max(1, area.width - unitSize.x + 1),
                Mathf.Max(1, area.height - unitSize.y + 1)
            );
        }

        Vector2Int[] xRanges = new Vector2Int[count];
        Vector2Int[] yRanges = new Vector2Int[count];

        // 2. 【核心优化】利用最新的 NavPortal 科技获取区间
        for (int i = 0; i < count; i++)
        {
            NavNode nodeA = nodePath[i];
            NavNode nodeB = nodePath[i + 1];

            // 🔥 变态级效率：直接拿预计算好的门户对象
            NavPortal portal = nodeA.Portals[nodeB.Id];

            RectInt bA = nodeBounds[i];
            RectInt bB = nodeBounds[i + 1];

            // 计算两个有效矩形的逻辑交集 (Intersection)
            // 这一步是保证“单位体积”能通过的关键
            int sharedMinX = Mathf.Max(bA.xMin, bB.xMin);
            int sharedMaxX = Mathf.Min(bA.xMax - 1, bB.xMax - 1);
            int sharedMinY = Mathf.Max(bA.yMin, bB.yMin);
            int sharedMaxY = Mathf.Min(bA.yMax - 1, bB.yMax - 1);

            if (portal.IsVertical) // 跨 X 轴的门 (左右相邻)
            {
                // X 坐标是固定的 (门户所在的线)
                // 需要判断门是在 A 的左边还是右边来确定 X
                int portalX = portal.FixedCoord;
                // 如果 A 在左，B 在右，单位过门时的左下角 X 就是 portalX
                // 如果 A 在右，B 在左，单位过门时的左下角 X 就是 portalX - unitSize.x + 1
                int fixedX = (nodeA.Area.x < nodeB.Area.x) ? portalX : portalX - unitSize.x + 1;

                xRanges[i] = new Vector2Int(fixedX, fixedX);
                yRanges[i] = new Vector2Int(sharedMinY, sharedMaxY);
            }
            else // 跨 Y 轴的门 (上下相邻)
            {
                int portalY = portal.FixedCoord;
                int fixedY = (nodeA.Area.y < nodeB.Area.y) ? portalY : portalY - unitSize.y + 1;

                xRanges[i] = new Vector2Int(sharedMinX, sharedMaxX);
                yRanges[i] = new Vector2Int(fixedY, fixedY);
            }
        }

        // 3. 拉绳子 (保持 $O(N)$，目前已经是最优)
        int[] bestX = Run1DStringPulling(start.x, end.x, xRanges);
        int[] bestY = Run1DStringPulling(start.y, end.y, yRanges);

        // 4. 生成双重锚点与 Waypoint 数据注入
        List<Waypoint> finalWaypoints = new List<Waypoint>();
        finalWaypoints.Add(new Waypoint
        {
            Pos = start,
            IsEndpoint = true,
            RangeMin = start.x,
            RangeMax = start.x,
            Node = nodePath[0] // 起点属于第一个节点
        });

        for (int i = 0; i < count; i++)
        {
            NavPortal portal = nodePath[i].Portals[nodePath[i + 1].Id];
            Vector2Int pivot = new Vector2Int(bestX[i], bestY[i]);

            // 获取自由轴的范围用于仲裁
            Vector2Int activeRange = portal.IsVertical ? yRanges[i] : xRanges[i];

            // 门前锚点
            finalWaypoints.Add(new Waypoint
            {
                Pos = ClampToRect(pivot, nodeBounds[i]),
                IsVerticalGate = portal.IsVertical,
                IsHorizontalGate = !portal.IsVertical,
                RangeMin = activeRange.x,
                RangeMax = activeRange.y,
                Portal = portal,   // 🔥 关键赋值
                Node = nodePath[i] // 门前点属于前一个节点
            });

            // 门后锚点
            finalWaypoints.Add(new Waypoint
            {
                Pos = ClampToRect(pivot, nodeBounds[i + 1]),
                IsVerticalGate = portal.IsVertical,
                IsHorizontalGate = !portal.IsVertical,
                RangeMin = activeRange.x,
                RangeMax = activeRange.y,
                Portal = portal,   // 🔥 关键赋值
                Node = nodePath[i + 1] // 门后点属于后一个节点
            });
        }

        // 终点
        finalWaypoints.Add(new Waypoint
        {
            Pos = end,
            IsEndpoint = true,
            RangeMin = end.x,
            RangeMax = end.x,
            Node = nodePath[nodePath.Count - 1] // 终点属于最后一个节点
        });

        return finalWaypoints;
    }

    private Vector2Int ClampToRect(Vector2Int p, RectInt r)
    {
        return new Vector2Int(
            Mathf.Clamp(p.x, r.xMin, r.xMin + r.width - 1), // width是格数，坐标是0-indexed，所以-1
            Mathf.Clamp(p.y, r.yMin, r.yMin + r.height - 1)
        );
    }

    // 通用的 1D 拉绳子算法 (String Pulling on 1D Grid)
    private int[] Run1DStringPulling(int startVal, int endVal, Vector2Int[] ranges)
    {
        int count = ranges.Length;
        int[] results = new int[count];

        // --- Forward Scan (计算可达范围) ---
        // reachable[i] 表示：要想通过第 i 个门，且来自于合法的前驱路径，
        // 我们在第 i 个门上能选择的有效区间是多少。
        Vector2Int[] reachable = new Vector2Int[count];

        Vector2Int currentRange = new Vector2Int(startVal, startVal);

        for (int i = 0; i < count; i++)
        {
            // 核心逻辑：我们目前的“光束”范围和“门”的物理范围取交集
            int low = Mathf.Max(currentRange.x, ranges[i].x);
            int high = Mathf.Min(currentRange.y, ranges[i].y);

            // 如果交集为空（理论上不应发生，除非路断了），就硬挤到最近的边界
            if (low > high)
            {
                if (currentRange.x > ranges[i].y) { low = ranges[i].y; high = ranges[i].y; }
                else { low = ranges[i].x; high = ranges[i].x; }
            }

            reachable[i] = new Vector2Int(low, high);
            currentRange = reachable[i];
        }

        // --- Backward Scan (确定最终点) ---
        // 从终点往回拉，每次选择最靠近“目标”的点
        int target = endVal;
        for (int i = count - 1; i >= 0; i--)
        {
            // 在可达范围内，选一个离 target 最近的值
            int chosen = Mathf.Clamp(target, reachable[i].x, reachable[i].y);
            results[i] = chosen;
            target = chosen;
        }

        return results;
    }
    private long TriangleAreaX2(Vector2Int a, Vector2Int b, Vector2Int c) => (long)(b.x - a.x) * (c.y - a.y) - (long)(c.x - a.x) * (b.y - a.y);

    // --- 🔥 终极 4 方向阶梯铺设 (Reservation Aware Bresenham) ---
    private void ExpandCornersToManhattanSteps(List<Waypoint> waypoints, List<Vector2Int> results)
    {
        results.Clear();
        if (waypoints.Count == 0) return;

        // 初始点
        Vector2Int current = waypoints[0].Pos;
        results.Add(current);

        // 记录上一步的主要方向，用于制造“强迫症”抖动
        // 初始设为非法值或随意值，让第一步自由选择
        bool lastStepWasX = false;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            Waypoint to = waypoints[i + 1];
            Vector2Int target = to.Pos;

            int dx = target.x - current.x;
            int dy = target.y - current.y;
            int absDx = Mathf.Abs(dx);
            int absDy = Mathf.Abs(dy);
            int sx = (dx > 0) ? 1 : -1;
            int sy = (dy > 0) ? 1 : -1;

            // --- 👮 约束 1 & 2：扣留过门所需的“过路费” ---
            // 如果目标是垂直门（跨越 X 轴），我们必须保留最后一步作为 X 位移。
            // 只有当我们在那个轴上有位移量时才扣留 (abs > 0)。
            int reservedDx = 0;
            int reservedDy = 0;

            if (to.IsVerticalGate && absDx > 0)
            {
                reservedDx = 1;
            }
            else if (to.IsHorizontalGate && absDy > 0)
            {
                reservedDy = 1;
            }

            // 计算可以拿来“挥霍”搞抖动的自由步数
            // 注意：约束2（总路程最小）在这里天然满足，因为我们只移动 absDx + absDy 次
            int freeDx = absDx - reservedDx;
            int freeDy = absDy - reservedDy;
            int freeSteps = freeDx + freeDy;

            // --- 🎭 阶段一：在 Box 内部进行强迫症抖动 (Constraint 3) ---
            for (int s = 0; s < freeSteps; s++)
            {
                bool moveX = false;

                // 决策逻辑：
                // 1. 如果某一轴的自由步数用完了，强制走另一轴
                if (freeDx == 0) moveX = false;
                else if (freeDy == 0) moveX = true;
                // 2. 如果都有剩余，则执行“强迫症”切换：
                //    如果上一步走了 X，这步尽量走 Y，反之亦然。
                else
                {
                    moveX = !lastStepWasX;
                }

                // 执行移动
                if (moveX)
                {
                    current.x += sx;
                    freeDx--;
                    lastStepWasX = true;
                }
                else
                {
                    current.y += sy;
                    freeDy--;
                    lastStepWasX = false;
                }

                // 避免重复添加点（虽然逻辑上不会重复，但为了保险）
                if (results.Count == 0 || results[results.Count - 1] != current)
                    results.Add(current);
            }

            // --- 🚪 阶段二：支付扣留的“过门费” (Constraint 1) ---
            // 此时我们已经到达了门的边缘，或者已经完成了非过门路径的所有自由步数。
            // 剩下的就是那神圣的最后一步。
            if (reservedDx > 0)
            {
                current.x += sx;
                lastStepWasX = true; // 记录这一步，虽然对于下一段路影响不大，但保持状态一致
                results.Add(current);
            }
            else if (reservedDy > 0)
            {
                current.y += sy;
                lastStepWasX = false;
                results.Add(current);
            }

            // 理论上此时 current 应该严格等于 target
            // 如果为了绝对安全，可以在这里强制 current = target (不过上面的数学逻辑应该是严丝合缝的)
        }
    }

    /// <summary>
    /// 🔥 【路径拼接手术】
    /// 当仲裁系统决定跳变时调用。
    /// 它会保留单位当前到跳变点的路径，然后从跳变点的出口重新寻路到终点，最后缝合在一起。
    /// </summary>
    /// <param name="entityId">单位ID</param>
    /// <param name="jumpPortal">决定借道的邻居门户</param>
    /// <param name="laneIdx">该门户选定的车道</param>
    /// <param name="currentWpIndex">当前正在仲裁的 Waypoint 索引（即原来的门前锚点）</param>
    public void ForcePathSplicing(int entityId, NavPortal jumpPortal, int laneIdx, int currentWpIndex)
    {
        ref var move = ref EntitySystem.Instance.wholeComponent.moveComponent[entityId];
        ref var core = ref EntitySystem.Instance.wholeComponent.coreComponent[entityId];

        // 1. 计算跳变点的几何坐标 (Pivot Points)
        // 這是我们确定的“转折点”
        Vector2Int jumpEntry, jumpExit;

        // 根据车道计算精确坐标
        if (jumpPortal.IsVertical)
        {
            // 垂直门：X固定，Y由车道决定
            int y = jumpPortal.Min + laneIdx;
            // 判定方向：我们要从当前的 LogicalPosition 走向门
            // 简单的几何判断：哪边离单位近，哪边是入口
            int distToMin = Mathf.Abs(move.LogicalPosition.x - jumpPortal.FixedCoord); // 实际上需要判断左右
                                                                                       // 更严谨的方法：利用 NavNode 的拓扑关系。
                                                                                       // 但这里为了简化，我们假设 jumpPortal 是邻接的，所以入口就是 FixedCoord 附近

            // 修正：利用 jumpPortal 连接的两个 Node，看哪个包含当前单位
            // 如果单位在 NodeA，那么入口就是 NodeA 这一侧
            NavNode nodeA = GridSystem.Instance.GetNode(jumpPortal.NodeA);
            bool unitInA = nodeA.Area.Contains(move.LogicalPosition);

            // 如果单位在A，入口X是门的X（注意NavMesh边界重合问题，通常是 FixedCoord）
            // 这里我们用 Waypoint 生成时的 ClampToRect 逻辑反推
            // 简单处理：入口和出口分别在 FixedCoord 的两侧（或者就是 FixedCoord 本身）

            // 让我们沿用 Waypoint 的定义：门前点和门后点
            // 对于垂直门，X 坐标通常是 FixedCoord (如果我们在格子上)
            // 或者是 NodeA.xMax-1 和 NodeB.xMin

            // 既然是跳变，我们直接用“门的位置”作为关键点
            jumpEntry = new Vector2Int(jumpPortal.FixedCoord, y);

            // 出口：为了让 A* 能够正确开始，我们需要给它一个明确的“门对面”的坐标
            // 如果我们在左边，出口在右边 (FixedCoord + 1)，反之亦然
            // 我们可以通过比较 LogicalPos.x 和 FixedCoord 来决定
            int dir = (move.LogicalPosition.x < jumpPortal.FixedCoord) ? 1 : -1;
            jumpExit = new Vector2Int(jumpPortal.FixedCoord + dir, y);
        }
        else
        {
            // 水平门同理
            int x = jumpPortal.Min + laneIdx;
            int dir = (move.LogicalPosition.y < jumpPortal.FixedCoord) ? 1 : -1;
            jumpEntry = new Vector2Int(x, jumpPortal.FixedCoord);
            jumpExit = new Vector2Int(x, jumpPortal.FixedCoord + dir);
        }

        // 2. 发起局部重寻路 (Local Repath)
        // 从 jumpExit (转折后的新起点) -> Original Target
        NavNode startNode = GridSystem.Instance.GetNodeAt(jumpExit);
        NavNode endNode = GridSystem.Instance.GetNodeAt(move.TargetGridPosition);

        if (startNode == null || endNode == null) return; // 异常保护

        // 强制同步计算（必须立刻拿到结果，否则单位会停顿）
        // 注意：这里调用的是底层的 FindNodePath，不走队列
        ClearMetadataMap(); // 记得清理 A* 缓存
        List<NavNode> newRoute = FindNodePath(startNode, endNode, jumpExit, move.TargetGridPosition);

        if (newRoute == null) return; // 死路，无法跳变

        // 3. 生成后半截路径的骨架 (String Pulling)
        List<Waypoint> newTail = PlanManhattanWaypoints(jumpExit, move.TargetGridPosition, newRoute, core.LogicSize);

        // 4. 缝合手术 (Splicing)
        List<Waypoint> finalPath = new List<Waypoint>();

        // [头部]：保留从 0 到 currentWpIndex - 1 的点 (即单位还没走完的、到达跳变点之前的老路)
        // 注意：currentWpIndex 是我们正准备去的那个“原来的门前锚点”。
        // 我们现在的单位位置可能在 index-1 和 index 之间。
        // 保守起见，我们保留到 currentWpIndex 之前
        for (int i = 0; i < currentWpIndex; i++)
        {
            finalPath.Add(move.Waypoints[i]);
        }

        // [中间]：插入跳变门户的 Entry 和 Exit
        // 构造新的 Waypoint
        Waypoint wpEntry = new Waypoint
        {
            Pos = jumpEntry,
            Portal = jumpPortal,
            RangeMin = jumpPortal.Min,
            RangeMax = jumpPortal.Max,
            IsVerticalGate = jumpPortal.IsVertical,
            IsHorizontalGate = !jumpPortal.IsVertical
        };

        // 注意：PlanManhattanWaypoints 生成的列表包含了起点(jumpExit)和终点
        // 我们需要的“门后点”其实就是 newTail[0] (即 jumpExit)
        // 但为了数据结构一致性，我们需要把 wpEntry 加进去
        finalPath.Add(wpEntry);

        // [尾部]：接上新计算的路径
        // newTail[0] 是 jumpExit (作为 Endpoint 类型)，我们可能需要把它修正为 PortalExit 类型
        // 或者直接接上去。PlanManhattanWaypoints 返回的第一个点是 StartPos。
        // 我们把 newTail 全部接上
        finalPath.AddRange(newTail);

        // 5. 应用到单位
        move.Waypoints = finalPath;
        // 修正索引：我们刚刚把头部保留了 currentWpIndex 个点，又加了一个 wpEntry
        // 所以单位的下一个目标应该是 wpEntry，索引是 currentWpIndex
        move.WaypointIndex = currentWpIndex;

        // 6. 重置状态
        move.IsPathStale = false; // 路径已更新，不再陈旧
                                  // 顺便把 NextStepTile 强制指引向 jumpEntry，确保下一帧移动系统执行
                                  // move.NextStepTile = ... (由仲裁系统下一帧计算或这里直接赋值)

        Debug.Log($"[Splicing] 单位 {entityId} 路径缝合完成。跳变至 Portal {jumpPortal.Id}");
    }

    /// <summary>
    /// 同步计算从 start 到 end 的路径骨架，返回 Waypoint 列表。
    /// 用于路径拼接等需要立即得到结果的场景。
    /// </summary>
    public List<Waypoint> ComputePathImmediate(Vector2Int start, Vector2Int end, Vector2Int unitSize)
    {
        NavNode startNode = GridSystem.Instance.GetNodeAt(start);
        NavNode endNode = GridSystem.Instance.GetNodeAt(end);

        // 终点抢救逻辑 (Bresenham 视线回退法)
        if (endNode == null)
        {
            // 使用 Bresenham 视线回退法寻找最近的合法格子
            Vector2Int correctedEnd = GridSystem.Instance.GetValidTargetByLineOfSight(end, start);
            if (correctedEnd != end)
            {
                // 更新终点位置
                end = correctedEnd;
                // 重新获取终点节点
                endNode = GridSystem.Instance.GetNodeAt(end);
            }
        }

        if (startNode == null || endNode == null)
            return null;

        ClearMetadataMap(); // 清空 A* 缓存

        List<NavNode> nodePath = FindNodePath(startNode, endNode, start, end);
        if (nodePath == null)
            return null;

        List<Waypoint> waypoints = PlanManhattanWaypoints(start, end, nodePath, unitSize);
        return waypoints;
    }


    private void ClearMetadataMap()
    {
        // 简单暴力地重置所有已缓存节点的数值
        foreach (var kv in _nodeMetadataMap)
        {
            kv.Value.G = float.MaxValue;
            kv.Value.Parent = null;
        }
    }

    private NavPathNode GetMetadata(NavNode node)
    {
        if (!_nodeMetadataMap.TryGetValue(node.Id, out var meta))
        {
            meta = new NavPathNode();
            meta.Node = node;
            _nodeMetadataMap[node.Id] = meta;
        }
        // 注意：这里不再调用 Reset(node)，Reset 的职责交给单次寻路前的清理
        return meta;
    }

    private List<Vector2Int> GetPathListFromPool() => _pathListPool.Count > 0 ? _pathListPool.Pop() : new List<Vector2Int>(64);

    public void RecyclePath(List<Vector2Int> path) { if (path == null) return; path.Clear(); _pathListPool.Push(path); }

    /// <summary>
    /// 清理寻路系统内部状态，防止残留数据导致崩溃
    /// </summary>
    public void Clear()
    {
        _pathRequests.Clear();
        _nodeMetadataMap.Clear();
        _lastDebugNodePath?.Clear();
        _currentSearchSessionId = 0;
        _pathListPool.Clear();
        Debug.Log("<color=orange>[PathfindingSystem]</color> 寻路系统状态已清理。");
    }

    private List<NavNode> RetraceNodePath(NavPathNode endNode)
    {
        List<NavNode> path = new List<NavNode>();
        NavPathNode curr = endNode;
        while (curr != null) { path.Add(curr.Node); curr = curr.Parent; }
        path.Reverse(); return path;
    }
}

public partial class PathfindingSystem : SingletonMono<PathfindingSystem>
{
    // --- 内部辅助结构定义 ---
    private struct PortalEdge
    {
        public Vector2Int Left;   // 缩减后的左端点（格子坐标）
        public Vector2Int Right;  // 缩减后的右端点
        public NavPortal Portal;  // 所属门户
        public NavNode NodeA;     // 门前房间
        public NavNode NodeB;     // 门后房间
    }

    /// <summary>
    /// 【核心】计算矩形走廊里的最小欧拉折线。
    /// 引入了共线保护逻辑，防止在窄走廊中产生多余的折线抖动喵！
    /// </summary>
    public List<Waypoint> PlanMinimalEulerPath(Vector2Int start, Vector2Int end, List<NavNode> nodePath, Vector2Int unitSize)
    {
        if (nodePath == null || nodePath.Count == 0) return null;

        int count = nodePath.Count - 1;
        if (count < 1)
        {
            return new List<Waypoint> {
            new Waypoint { Pos = start, IsEndpoint = true, Node = nodePath[0] },
            new Waypoint { Pos = end, IsEndpoint = true, Node = nodePath[0] }
        };
        }

        PortalEdge[] edges = new PortalEdge[count];
        for (int i = 0; i < count; i++)
            edges[i] = CalculateShrunkEdge(nodePath[i], nodePath[i + 1], unitSize);

        List<Waypoint> waypoints = new List<Waypoint>();
        waypoints.Add(new Waypoint { Pos = start, IsEndpoint = true, Node = nodePath[0] });

        Vector2Int apex = start;
        Vector2Int leftRay = edges[0].Left;
        Vector2Int rightRay = edges[0].Right;
        int leftIdx = 0;
        int rightIdx = 0;

        for (int i = 1; i <= count; i++)
        {
            Vector2Int curLeft = (i == count) ? end : edges[i].Left;
            Vector2Int curRight = (i == count) ? end : edges[i].Right;

            // --- 1. 漏斗右侧收缩判定 ---
            if (TriangleAreaX2(apex, rightRay, curRight) >= 0)
            {
                // 检查是否跨过了左边界：curRight 在 leftRay 的左侧 (>0) 说明发生了交叉
                if (TriangleAreaX2(apex, leftRay, curRight) > 0)
                {
                    // 拐点确定为当前的 leftRay
                    if (leftIdx < count) waypoints.Add(CreateGateWaypoint(leftRay, edges[leftIdx]));

                    apex = leftRay;
                    int nextI = leftIdx + 1;
                    i = nextI;

                    if (i < count)
                    {
                        leftRay = edges[i].Left; rightRay = edges[i].Right;
                        leftIdx = i; rightIdx = i;
                    }
                    else
                    {
                        leftRay = end; rightRay = end;
                        leftIdx = count; rightIdx = count;
                    }
                    continue;
                }
                // 正常收缩
                rightRay = curRight;
                rightIdx = i;
            }

            // --- 2. 漏斗左侧收缩判定 ---
            if (TriangleAreaX2(apex, leftRay, curLeft) <= 0)
            {
                // 检查是否跨过了右边界：curLeft 在 rightRay 的右侧 (<0) 说明发生了交叉
                if (TriangleAreaX2(apex, rightRay, curLeft) < 0)
                {
                    // 拐点确定为当前的 rightRay
                    if (rightIdx < count) waypoints.Add(CreateGateWaypoint(rightRay, edges[rightIdx]));

                    apex = rightRay;
                    int nextI = rightIdx + 1;
                    i = nextI;

                    if (i < count)
                    {
                        leftRay = edges[i].Left; rightRay = edges[i].Right;
                        leftIdx = i; rightIdx = i;
                    }
                    else
                    {
                        leftRay = end; rightRay = end;
                        leftIdx = count; rightIdx = count;
                    }
                    continue;
                }
                // 正常收缩
                leftRay = curLeft;
                leftIdx = i;
            }
        }

        waypoints.Add(new Waypoint { Pos = end, IsEndpoint = true, Node = nodePath[nodePath.Count - 1] });
        return waypoints;
    }

    // --- 内部逻辑实现 ---

    /// <summary>
    /// 计算缩减后的门户边缘。这里 edges[i].Left/Right 存储的是单位左下角可达的极限角点。
    /// </summary>
    private PortalEdge CalculateShrunkEdge(NavNode a, NavNode b, Vector2Int unitSize)
    {
        NavPortal portal = a.Portals[b.Id];
        RectInt bA = GetAdjustedBounds(a.Area, unitSize);
        RectInt bB = GetAdjustedBounds(b.Area, unitSize);

        int minX = Mathf.Max(bA.xMin, bB.xMin);
        int maxX = Mathf.Min(bA.xMax - 1, bB.xMax - 1);
        int minY = Mathf.Max(bA.yMin, bB.yMin);
        int maxY = Mathf.Min(bA.yMax - 1, bB.yMax - 1);

        PortalEdge edge = new PortalEdge { Portal = portal, NodeA = a, NodeB = b };

        if (portal.IsVertical) // 垂直门（左右相邻）
        {
            int fx = (a.Area.x < b.Area.x) ? portal.FixedCoord : portal.FixedCoord - unitSize.x + 1;

            // --- 🔥 方向感知：决定左手边和右手边 ---
            if (a.Area.x < b.Area.x) // 向右走 (+X)
            {
                edge.Left = new Vector2Int(fx, maxY);  // 上方是左
                edge.Right = new Vector2Int(fx, minY); // 下方是右
            }
            else // 向左走 (-X)
            {
                edge.Left = new Vector2Int(fx, minY);  // 下方是左
                edge.Right = new Vector2Int(fx, maxY); // 上方是右
            }
        }
        else // 水平门（上下相邻）
        {
            int fy = (a.Area.y < b.Area.y) ? portal.FixedCoord : portal.FixedCoord - unitSize.y + 1;

            if (a.Area.y < b.Area.y) // 向上走 (+Y)
            {
                edge.Left = new Vector2Int(minX, fy);  // 左侧是左
                edge.Right = new Vector2Int(maxX, fy); // 右侧是右
            }
            else // 向下走 (-Y)
            {
                edge.Left = new Vector2Int(maxX, fy);  // 右侧是左
                edge.Right = new Vector2Int(minX, fy); // 左侧是右
            }
        }
        return edge;
    }

    /// <summary>
    /// 当漏斗算法确定一个角点为拐点时，生成一个“门后出口格”的 Waypoint。
    /// </summary>
    private Waypoint CreateGateWaypoint(Vector2Int cornerPos, PortalEdge edge)
    {
        // 计算“跨过门”的那一格作为 Waypoint 的逻辑位置
        Vector2Int exitPos = cornerPos;
        if (edge.Portal.IsVertical)
            exitPos.x = (edge.NodeA.Area.x < edge.NodeB.Area.x) ? edge.Portal.FixedCoord : edge.Portal.FixedCoord - 1;
        else
            exitPos.y = (edge.NodeA.Area.y < edge.NodeB.Area.y) ? edge.Portal.FixedCoord : edge.Portal.FixedCoord - 1;

        bool isRightOrUp;
        if (edge.Portal.IsVertical)
            isRightOrUp = edge.NodeA.Area.x < edge.NodeB.Area.x; // 右为 true
        else
            isRightOrUp = edge.NodeA.Area.y < edge.NodeB.Area.y; // 上为 true

        return new Waypoint
        {
            Pos = exitPos,
            IsVerticalGate = edge.Portal.IsVertical,
            IsHorizontalGate = !edge.Portal.IsVertical,
            Portal = edge.Portal,
            Node = edge.NodeB, // 拐点被视为进入了下一个节点
            IsEndpoint = false,
            // 记录原始范围，供仲裁系统参考
            RangeMin = edge.Portal.IsVertical ? edge.Right.y : edge.Left.x,
            RangeMax = edge.Portal.IsVertical ? edge.Left.y : edge.Right.x,
            isRightOrUp = isRightOrUp
        };
    }

    /// <summary>
    /// 缩减矩形边界，确保以该矩形为左下角点移动时，整个 unitSize 矩形都在原区域内。
    /// </summary>
    private RectInt GetAdjustedBounds(RectInt area, Vector2Int unitSize)
    {
        return new RectInt(
            area.x,
            area.y,
            Mathf.Max(1, area.width - unitSize.x + 1),
            Mathf.Max(1, area.height - unitSize.y + 1)
        );
    }

}
public partial class PathfindingSystem : SingletonMono<PathfindingSystem>
{
    [Header("Debug 可视化")]
    public bool showAstarBox = true;    // 保持：A* 搜出来的房间背景
    public bool drawEulerPaths = true;  // 开启：原教旨欧拉折线
    public int focusedEntityId = -1;   // 专注模式

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || EntitySystem.Instance == null || !EntitySystem.Instance.IsInitialized) return;

        var grid = GridSystem.Instance;
        var whole = EntitySystem.Instance.wholeComponent;

        // --- 1. 背景：NavNode 序列 (保持原样，淡淡的底色) ---
        if (showAstarBox && _lastDebugNodePath != null && _lastDebugNodePath.Count > 0)
        {
            Gizmos.color = new Color(0, 1, 0, 0.05f);
            foreach (var node in _lastDebugNodePath)
            {
                Vector3 center = new Vector3((node.Area.x + node.Area.width * 0.5f) * grid.CellSize, (node.Area.y + node.Area.height * 0.5f) * grid.CellSize, 0);
                Vector3 size = new Vector3(node.Area.width * grid.CellSize, node.Area.height * grid.CellSize, 0);
                Gizmos.DrawCube(center, size);
                Gizmos.color = new Color(0, 1, 0, 0.2f);
                Gizmos.DrawWireCube(center, size);
            }
        }

        if (!drawEulerPaths) return;

        // --- 2. 核心：原教旨欧拉折线绘制 ---
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            ref var move = ref whole.moveComponent[i];

            if (!core.Active || move.Waypoints == null || move.Waypoints.Count == 0) continue;
            if (focusedEntityId != -1 && core.SelfHandle.Id != focusedEntityId) continue;

            // --- 2a. 绘制拉紧的折线段 (The String) ---
            Gizmos.color = Color.yellow;

            // 路径起点：单位当前的视觉位置
            Vector3 lastPoint = core.Position;

            // 如果单位已经开始了路径，从当前的 WaypointIndex 开始画
            for (int j = move.WaypointIndex; j < move.Waypoints.Count; j++)
            {
                Vector3 nextPoint = grid.GridToWorld(move.Waypoints[j].Pos, core.LogicSize);

                // 绘制这一段折线
                Gizmos.DrawLine(lastPoint, nextPoint);

                // 在转折点绘制一个极小的标记（原教旨拐点）
                if (!move.Waypoints[j].IsEndpoint)
                {
                    Gizmos.DrawWireSphere(nextPoint, 0.05f * grid.CellSize);
                }

                lastPoint = nextPoint;
            }

            // --- 2b. 绘制约束门户 (仅在拐点处显示限制范围) ---
            // 只有当前目标 Waypoint 如果是门户，才画出那个“必须要穿过”的横杠
            if (move.WaypointIndex < move.Waypoints.Count)
            {
                var targetWp = move.Waypoints[move.WaypointIndex];
                if (!targetWp.IsEndpoint && targetWp.Portal != null)
                {
                    Gizmos.color = new Color(1, 0.92f, 0.016f, 0.5f); // 亮金色
                    Vector3 wpPos = grid.GridToWorld(targetWp.Pos, core.LogicSize);

                    if (targetWp.IsVerticalGate)
                    {
                        // 垂直门户，显示 Y 轴的通行区间
                        float yMin = targetWp.RangeMin * grid.CellSize;
                        float yMax = (targetWp.RangeMax + 1) * grid.CellSize; // +1 是为了画到格子边缘
                        float x = wpPos.x;
                        Gizmos.DrawLine(new Vector3(x, yMin, 0), new Vector3(x, yMax, 0));
                    }
                    else
                    {
                        // 水平门户，显示 X 轴的通行区间
                        float xMin = targetWp.RangeMin * grid.CellSize;
                        float xMax = (targetWp.RangeMax + 1) * grid.CellSize;
                        float y = wpPos.y;
                        Gizmos.DrawLine(new Vector3(xMin, y, 0), new Vector3(xMax, y, 0));
                    }
                }
            }

            // --- 2c. 战术指引 (青色，代表当前的瞬时意图) ---
            if (move.HasNextStep)
            {
                Gizmos.color = Color.cyan;
                Vector3 nextStepWorld = grid.GridToWorld(move.NextStepTile, core.LogicSize);
                Gizmos.DrawLine(core.Position, nextStepWorld);
            }
        }
    }
}
