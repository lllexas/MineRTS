using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static PathfindingSystem;

/// <summary>
/// 预约记录结构：用于回溯和自清
/// </summary>
public struct ReservationRecord
{
    public NavPortal Portal;
    public int Lane;
    public ulong Mask;
}

public class ArbitrationSystem : SingletonMono<ArbitrationSystem>
{
    [Header("调度参数")]
    public int debugEntityId = -1; // 追踪特定单位
    public int lookAheadCount = 3; // 递归向前预看的门户数量
    public int baseSearchRadius = 8; // 基础搜索半径
    public int maxWaitTicks = 5; // Tier 3: 最大等待 Tick 数

    public void UpdateArbitration(WholeComponent whole, float deltaTime)
    {
        return; // 暂时关闭仲裁逻辑，专注于路径修正和预约机制的稳定性
        /*long currentTick = TimeTicker.GlobalTick;

        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var m = ref whole.moveComponent[i];
            ref var core = ref whole.coreComponent[i];

            // 1. 基础过滤
            if (!core.Active || (core.Type & (UnitType.Building | UnitType.Projectile)) != 0) continue;

            // 路径有效性检查
            if (m.Waypoints == null || m.Waypoints.Count == 0)
            {
                if (!m.IsPathPending && m.LogicalPosition != m.TargetGridPosition)
                    PathfindingSystem.Instance.RequestPath(i);
                continue;
            }

            // 索引越界修复
            if (m.WaypointIndex >= m.Waypoints.Count)
                m.WaypointIndex = m.Waypoints.Count - 1;

            if (m.LogicalPosition == m.TargetGridPosition) continue;

            // 过门契约保护：正在跨门的单位不参与仲裁
            if (m.CurrentReservedPortal != null && m.TargetPortalExit.HasValue)
            {
                if (m.LogicalPosition != m.TargetPortalExit.Value)
                    continue; // 还在门缝里，跳过
                else
                {
                    // 已到达出口，清除契约
                    m.CurrentReservedPortal = null;
                    m.TargetPortalExit = null;
                }
            }

            // 自清旧预约
            ClearActiveReservations(ref m);

            int wpIdx = m.WaypointIndex;
            var currentWaypoint = m.Waypoints[wpIdx];

            // 端点直接跳过，由 MoveSystem 处理推进
            if (currentWaypoint.IsEndpoint)
                continue;

            NavNode currentNode = GridSystem.Instance.GetNodeAt(m.LogicalPosition);
            if (currentNode == null) continue;

            bool success = TryReserveAndSplice(i, currentNode, currentWaypoint.Portal, 9999, wpIdx);

            if (success)
                m.IsBlocked = false;
            else
            {
                m.IsBlocked = true;
                m.BlockWaitTimerTicks = 2;
            }
        }*/
    }

    /// <summary>
    /// 尝试在当前节点中寻找可用门户（包括主门偏移或备选门），并根据结果修正路径。
    /// </summary>
    /// <param name="entityId">单位ID</param>
    /// <param name="currentNode">单位当前所在节点</param>
    /// <param name="primaryPortal">原计划的首选门户</param>
    /// <param name="baseSearchRadius">基础搜索半径（传递给螺旋搜索）</param>
    /// <param name="currentWpIndex">当前路径点索引（指向被阻塞的门前点）</param>
    /// <returns>是否成功完成预约和路径修正</returns>
    private bool TryReserveAndSplice(int entityId, NavNode currentNode, NavPortal primaryPortal, int baseSearchRadius, int currentWpIndex)
    {
        ref var move = ref EntitySystem.Instance.wholeComponent.moveComponent[entityId];
        ref var core = ref EntitySystem.Instance.wholeComponent.coreComponent[entityId];

        // 1. 调用螺旋搜索，尝试获取可用门户和车道
        if (!TryReserveAnyPortalSpiral(entityId, currentNode, primaryPortal, baseSearchRadius,
                                       out NavPortal chosenPortal, out int lane, out ulong mask))
            return false;

        // 2. 记录预约
        var record = new ReservationRecord { Portal = chosenPortal, Lane = lane, Mask = mask };
        move.ActiveReservations = move.ActiveReservations ?? new List<ReservationRecord>();
        move.ActiveReservations.Add(new ReservationRecord { Portal = chosenPortal, Lane = lane, Mask = mask });

        bool isPrimary = (chosenPortal.Id == primaryPortal.Id);

        if (isPrimary)
        {
            int laneCoord = chosenPortal.Min + lane;

            // 修正门前点和门后点的坐标
            for (int offset = 0; offset <= 1; offset++)
            {
                int idx = currentWpIndex + offset;
                if (idx >= move.Waypoints.Count) break;
                var wp = move.Waypoints[idx];
                wp.Pos = chosenPortal.IsVertical
                    ? new Vector2Int(wp.Pos.x, laneCoord)
                    : new Vector2Int(laneCoord, wp.Pos.y);
                move.Waypoints[idx] = wp;
            }

            Vector2Int entryCell = GetEntryCell(chosenPortal, currentNode, lane);

            // 如果单位已经站在入口，立即转正预约并推进到门后点
            if ((Vector2Int)move.LogicalPosition == entryCell)
            {
                // 预约转正
                move.CurrentReservedPortal = chosenPortal;
                move.CurrentReservedLane = lane;
                move.CurrentReservedMask = mask;
                move.TargetPortalExit = GetExitCell(chosenPortal, currentNode, lane);
                move.ActiveReservations.Remove(record); // 从临时列表中移除

                move.IsBlocked = false;
                move.HasNextStep = false; // 等待Boids计算从入口到门后点的步进
            }
            else
            {
                // 尚未到达入口，只需更新坐标，保持原索引
                move.HasNextStep = false;
                move.IsPathStale = false;
            }
        }
        else
        {
            // === 情况2：备选门成功（门户跳变）===
            // 计算跳变门的入口和出口格子
            Vector2Int entryCell = GetEntryCell(chosenPortal, currentNode, lane);
            Vector2Int exitCell = GetExitCell(chosenPortal, currentNode, lane);

            // 从出口到终点同步寻路
            List<Waypoint> newTail = PathfindingSystem.Instance.ComputePathImmediate(exitCell, move.TargetGridPosition, core.LogicSize);
            if (newTail == null || newTail.Count == 0)
            {
                // 寻路失败，释放预约
                chosenPortal.Timetables[lane] &= ~mask;
                move.ActiveReservations.RemoveAt(move.ActiveReservations.Count - 1); // 移除刚添加的预约
                return false;
            }

            // 将 newTail 的第一个点（exitCell）改造为标准的门后锚点
            Waypoint gateExitWp = newTail[0];
            gateExitWp.IsEndpoint = false;
            gateExitWp.IsVerticalGate = chosenPortal.IsVertical;
            gateExitWp.IsHorizontalGate = !chosenPortal.IsVertical;
            gateExitWp.Portal = chosenPortal;
            gateExitWp.RangeMin = chosenPortal.Min;
            gateExitWp.RangeMax = chosenPortal.Max;
            gateExitWp.Node = GridSystem.Instance.GetNodeAt(exitCell); // 门后点属于出口节点
            newTail[0] = gateExitWp;

            // 构造门前锚点
            Waypoint gateEntryWp = new Waypoint
            {
                Pos = entryCell,
                IsVerticalGate = chosenPortal.IsVertical,
                IsHorizontalGate = !chosenPortal.IsVertical,
                Portal = chosenPortal,
                RangeMin = chosenPortal.Min,
                RangeMax = chosenPortal.Max,
                Node = currentNode
            };

            // 拼接路径
            List<Waypoint> finalPath = new List<Waypoint>();
            // 保留从起点到 currentWpIndex-1 的旧路径点（即已经规划但尚未执行的部分）
            for (int i = 0; i < currentWpIndex; i++)
                finalPath.Add(move.Waypoints[i]);

            // 插入跳变门的门前点
            finalPath.Add(gateEntryWp);
            // 接上改造后的新路径（注意 newTail 的第一个点已是门后点，剩余部分正常）
            finalPath.AddRange(newTail);

            move.Waypoints = finalPath;
            move.WaypointIndex = currentWpIndex; // 指向新插入的门前点
            move.IsPathStale = false;
            move.HasNextStep = false;
        }

        return true;
    }

    /// <summary>
    /// 获取从当前节点穿过指定门户后，出口一侧的格子坐标（左下角）。
    /// </summary>
    private Vector2Int GetExitCell(NavPortal portal, NavNode currentNode, int laneIdx)
    {
        int laneCoord = portal.Min + laneIdx; // 自由轴上的格子坐标
        if (portal.IsVertical)
        {
            // 垂直门：判断单位在左还是右
            // 如果 currentNode 的右边界等于 FixedCoord，说明单位在门左边，出口在 FixedCoord
            // 如果 currentNode 的左边界等于 FixedCoord，说明单位在门右边，出口在 FixedCoord+1
            bool unitOnLeft = (currentNode.Area.xMax == portal.FixedCoord);
            int exitX = unitOnLeft ? portal.FixedCoord : portal.FixedCoord + 1;
            return new Vector2Int(exitX, laneCoord);
        }
        else
        {
            // 水平门：判断单位在下还是上
            bool unitOnBottom = (currentNode.Area.yMax == portal.FixedCoord);
            int exitY = unitOnBottom ? portal.FixedCoord : portal.FixedCoord + 1;
            return new Vector2Int(laneCoord, exitY);
        }
    }

    /// <summary>
    /// 在当前节点中，以螺旋顺序尝试所有门户（从主门开始，然后顺时针一个、逆时针一个，再顺时针两个、逆时针两个……），
    /// 每个门户内部调用 TryReservePortalLane 进行车道螺旋搜索。
    /// </summary>
    /// <param name="entityId">单位ID</param>
    /// <param name="currentNode">当前节点</param>
    /// <param name="primaryPortal">首选门户</param>
    /// <param name="baseSearchRadius">主门的搜索半径（备选门半径减半）</param>
    /// <param name="chosenPortal">输出选中的门户</param>
    /// <param name="chosenLane">输出选中的车道</param>
    /// <param name="mask">输出预约掩码</param>
    /// <returns>是否成功预约到任何门户</returns>
    private bool TryReserveAnyPortalSpiral(int entityId, NavNode currentNode, NavPortal primaryPortal, int baseSearchRadius,
                                       out NavPortal chosenPortal, out int chosenLane, out ulong mask)
    {
        chosenPortal = null;
        chosenLane = -1;
        mask = 0;

        // 拿到 move 的引用
        ref var move = ref EntitySystem.Instance.wholeComponent.moveComponent[entityId];

        // --- 1. 定义本地函数，显式要求传递 ref ---
        // 注意：这里显式声明 mParam 为 ref，不再直接“捕获”外层的 move
        bool TryReserveOneCandidate(NavPortal p, ref MoveComponent mParam, int radius, out int l, out ulong mMask)
        {
            int mIdx = p.Max - p.Min;
            // 使用传递进来的引用
            int optL = p.IsVertical ? mParam.LogicalPosition.y - p.Min : mParam.LogicalPosition.x - p.Min;
            return TryReservePortalLane(entityId, p, currentNode, optL, mIdx, radius, out l, out mMask);
        }

        // --- 2. 尝试主门 ---
        if (TryReserveOneCandidate(primaryPortal, ref move, baseSearchRadius, out chosenLane, out mask))
        {
            chosenPortal = primaryPortal;
            return true;
        }

        // --- 3. 螺旋双指针 ---
        NavPortal cwPtr = primaryPortal;
        NavPortal ccwPtr = primaryPortal;
        int triedCount = 1;
        int totalPortals = currentNode.Portals.Count;

        while (triedCount < totalPortals)
        {
            if (cwPtr != null)
            {
                cwPtr = currentNode.GetAdjacentPortalInNode(cwPtr, NavNode.Rotation.CW);
                if (cwPtr != null && cwPtr.Id != primaryPortal.Id)
                {
                    // 显式传 ref move
                    if (TryReserveOneCandidate(cwPtr, ref move, baseSearchRadius, out chosenLane, out mask))
                    {
                        chosenPortal = cwPtr;
                        return true;
                    }
                    triedCount++;
                }
                else { cwPtr = null; }
            }

            if (ccwPtr != null)
            {
                ccwPtr = currentNode.GetAdjacentPortalInNode(ccwPtr, NavNode.Rotation.CCW);
                if (ccwPtr != null && ccwPtr.Id != primaryPortal.Id)
                {
                    if (cwPtr != null && ccwPtr.Id == cwPtr.Id) { ccwPtr = null; }
                    else
                    {
                        if (TryReserveOneCandidate(ccwPtr, ref move, baseSearchRadius, out chosenLane, out mask))
                        {
                            chosenPortal = ccwPtr;
                            return true;
                        }
                        triedCount++;
                    }
                }
                else { ccwPtr = null; }
            }
            if (cwPtr == null && ccwPtr == null) break;
        }

        return false;
    }

    /// <summary>
    /// 尝试预约指定门户的车道，自动计算到达时间，按螺旋顺序从最优车道开始搜索。
    /// 这是原子操作，不涉及后续路径。
    /// </summary>
    /// <param name="entityId">单位ID</param>
    /// <param name="portal">目标门户</param>
    /// <param name="unitNode">单位当前所在节点（用于计算门前格子）</param>
    /// <param name="optimalLane">理论最优车道索引（基于单位位置）</param>
    /// <param name="maxLaneIdx">最大车道索引（portal.Max - portal.Min）</param>
    /// <param name="searchRadius">搜索半径（左右各多少格）</param>
    /// <param name="chosenLane">输出选中的车道索引</param>
    /// <param name="mask">输出预约掩码</param>
    /// <returns>是否预约成功</returns>
    private bool TryReservePortalLane(int entityId, NavPortal portal, NavNode unitNode, int optimalLane, int maxLaneIdx, int searchRadius, out int chosenLane, out ulong mask)
    {
        chosenLane = -1;
        mask = 0;

        ref var move = ref EntitySystem.Instance.wholeComponent.moveComponent[entityId];
        long currentTick = TimeTicker.GlobalTick;

        int maxAttempts = searchRadius * 2 + 1;
        int actualMaxAttempts = Mathf.Min(maxAttempts, maxLaneIdx * 2 + 1);
        for (int i = 0; i < actualMaxAttempts; i++)
        {
            int offset = (i == 0) ? 0 : (i % 2 == 1 ? (i + 1) / 2 : -i / 2);
            int lane = Mathf.Clamp(optimalLane + offset, 0, maxLaneIdx);

            // 计算到达该车道的绝对时间
            long arrivalTick = CalculateArrivalTick(move.LogicalPosition, unitNode, portal, lane, move.MoveIntervalTicks);
            long offsetTicks = arrivalTick - currentTick;
            if (offsetTicks >= 60) continue; // 超出视界

            if (portal.TryReserve(lane, (int)offsetTicks, move.MoveIntervalTicks, out mask))
            {
                chosenLane = lane;
                return true;
            }
        }
        return false;
    }

    
    // --- 辅助函数保持不变 ---
    private bool ArePortalsAdjacent(NavPortal p1, NavPortal p2)
    {
        // 简单判定：是否出现在对方的 8 方向指针里
        if (p1.N == p2 || p1.S == p2 || p1.E == p2 || p1.W == p2 ||
            p1.EN == p2 || p1.ES == p2 || p1.WN == p2 || p1.WS == p2) return true;
        return false;
    }

    private void ClearActiveReservations(ref MoveComponent m)
    {
        if (m.ActiveReservations == null) return;
        foreach (var res in m.ActiveReservations)
            if (res.Portal != null) res.Portal.Timetables[res.Lane] &= ~res.Mask;
        m.ActiveReservations.Clear();
    }

    private Vector2Int GetManhattanStep(Vector2Int cur, Vector2Int dest)
    {
        Vector2Int diff = dest - cur;
        if (diff.x != 0) return cur + new Vector2Int(System.Math.Sign(diff.x), 0);
        if (diff.y != 0) return cur + new Vector2Int(0, System.Math.Sign(diff.y));
        return cur;
    }

    private long CalculateNextArrival(long currentArrival, MoveComponent m, int currentWpIdx)
    {
        var exitWp = m.Waypoints[currentWpIdx + 1];
        int nextWpIdx = currentWpIdx + 2;
        if (nextWpIdx >= m.Waypoints.Count) return currentArrival;
        var nextWp = m.Waypoints[nextWpIdx];
        int dist = Mathf.Abs(exitWp.Pos.x - nextWp.Pos.x) + Mathf.Abs(exitWp.Pos.y - nextWp.Pos.y);
        return currentArrival + m.MoveIntervalTicks + (long)dist * m.MoveIntervalTicks;
    }

    private void HandleEndpointBehavior(int id, ref MoveComponent m, CoreComponent core, int wpIdx)
    {
        if (wpIdx == m.Waypoints.Count - 1)
        {
            if (m.LogicalPosition == m.TargetGridPosition) return;
            Vector2Int step = GetManhattanStep(m.LogicalPosition, m.TargetGridPosition);
            if (GridSystem.Instance.IsAreaClear(step, core.LogicSize, core.SelfHandle.Id))
            { m.NextStepTile = step; m.HasNextStep = true; m.IsBlocked = false; }
            else { m.IsBlocked = true; m.BlockWaitTimerTicks = 2; }
        }
        else { m.WaypointIndex = wpIdx + 1; }
    }

    /// <summary>
    /// 计算单位到达指定门户的指定车道对应的门前格子所需的绝对 Tick 数。
    /// </summary>
    /// <param name="unitPos">单位当前逻辑位置（中心格子）</param>
    /// <param name="unitNode">单位当前所在节点（必须与门户相邻）</param>
    /// <param name="portal">目标门户</param>
    /// <param name="laneIdx">选定的车道索引（相对 Min 的偏移）</param>
    /// <param name="moveIntervalTicks">移动一格所需 Tick 数</param>
    /// <returns>从当前 Tick 开始的绝对到达 Tick</returns>
    private long CalculateArrivalTick(Vector2Int unitPos, NavNode unitNode, NavPortal portal, int laneIdx, int moveIntervalTicks)
    {
        Vector2Int entryCell = GetEntryCell(portal, unitNode, laneIdx);
        int dist = Mathf.Abs(unitPos.x - entryCell.x) + Mathf.Abs(unitPos.y - entryCell.y);
        long arrivalOffset = (long)dist * moveIntervalTicks;
        return TimeTicker.GlobalTick + arrivalOffset;
    }

    /// <summary>
    /// 获取指定门户、节点和车道对应的门前格子坐标（左下角）
    /// </summary>
    private Vector2Int GetEntryCell(NavPortal portal, NavNode unitNode, int laneIdx)
    {
        int laneCoord = portal.Min + laneIdx; // 自由轴坐标（网格线索引，也是格子左下角坐标）
        if (portal.IsVertical)
        {
            // 垂直门：固定轴 X = FixedCoord
            // 判断单位在节点的哪一侧：如果单位节点的右边界等于 FixedCoord，则单位在左边，门前格子 X = FixedCoord - 1
            // 如果单位节点的左边界等于 FixedCoord，则单位在右边，门前格子 X = FixedCoord
            bool unitOnLeft = (unitNode.Area.xMax == portal.FixedCoord);
            int entryX = unitOnLeft ? portal.FixedCoord - 1 : portal.FixedCoord;
            return new Vector2Int(entryX, laneCoord);
        }
        else
        {
            // 水平门：固定轴 Y = FixedCoord
            bool unitOnBottom = (unitNode.Area.yMax == portal.FixedCoord);
            int entryY = unitOnBottom ? portal.FixedCoord - 1 : portal.FixedCoord;
            return new Vector2Int(laneCoord, entryY);
        }
    }
}