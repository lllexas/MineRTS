using UnityEngine;

public class MoveSystem : SingletonMono<MoveSystem>
{
    private const float TICK_INTERVAL = TimeTicker.SecondsPerTick;
    private float _tickAccumulator = 0f;

    public void UpdateMovement(WholeComponent whole, float deltaTime)
    {
        var gridSystem = GridSystem.Instance;

        // 1. 时间累积与 Global Tick 推进
        _tickAccumulator += deltaTime;
        int ticksToProcess = 0;
        while (_tickAccumulator >= TICK_INTERVAL)
        {
            ticksToProcess++;
            _tickAccumulator -= TICK_INTERVAL;
            TimeTicker.GlobalTick++;
            GridSystem.Instance.AdvanceNavMeshTick(TimeTicker.GlobalTick);
        }
        TimeTicker.SubTickOffset = _tickAccumulator;

        // 2. 遍历实体
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            ref var move = ref whole.moveComponent[i];

            if (!core.Active || (core.Type & (UnitType.Building | UnitType.Projectile)) != 0) continue;
            Vector2Int size = core.LogicSize;

            // --- 阶段 A：逻辑 Tick 处理 ---
            for (int j = 0; j < ticksToProcess; j++)
            {
                if (move.BlockWaitTimerTicks > 0) move.BlockWaitTimerTicks--;
                if (move.MoveTimerTicks > 0) move.MoveTimerTicks--;

                // --- 卡死计时器（独立于阻塞冷却）---
                if (move.LogicalPosition != move.TargetGridPosition && !move.IsPathPending)
                {
                    // 只要本 Tick 没有成功移动（即没有执行 PerformMove），就累加
                    // 注意：成功移动后 MoveTimerTicks 会被设为正数，但这里我们不依赖它
                    move.StuckTimerTicks++;
                }
                else
                {
                    // 已到达目标或正在寻路，重置计时
                    move.StuckTimerTicks = 0;
                }

                // 触发重寻路：连续 15 Tick 无法前进（1.5 秒）
                if (move.StuckTimerTicks > 15)
                {
                    bool isAtFinalGoal = false;
                    if (move.Waypoints != null && move.WaypointIndex < move.Waypoints.Count)
                        isAtFinalGoal = move.Waypoints[move.WaypointIndex].IsEndpoint;

                    if (!isAtFinalGoal)
                    {
                        // --- 彻底重置移动状态，模仿 IssueCommand ---
                        // 保留目标位置不变
                        Vector2Int target = move.TargetGridPosition;

                        // 洗脑：清除所有临时状态
                        move.PreviousLogicalPosition = move.LogicalPosition;
                        move.NextStepTile = move.LogicalPosition;
                        move.HasNextStep = false;
                        move.Waypoints = null;
                        move.WaypointIndex = 0;
                        move.IsBlocked = false;
                        move.TargetPortalExit = null;
                        move.StuckTimerTicks = 0;           // 重置卡死计时
                        move.IsPathPending = false;          // 强制解锁，确保 RequestPath 能成功

                        // 恢复目标（其实没变，但保持清晰）
                        move.TargetGridPosition = target;

                        // 请求新路径
                        PathfindingSystem.Instance.RequestPath(i);
                    }
                }

                // 只有当完全空闲时，才尝试移动
                if (move.MoveTimerTicks <= 0 && move.BlockWaitTimerTicks <= 0)
                {
                    // 每一帧开始处理逻辑前，记录一下视觉起始点（用于插值）
                    // 注意：如果 ticksToProcess > 1，这里会捕捉到中间态，是正确的
                    move.LastVisualPosition = gridSystem.GridToWorld(move.LogicalPosition, size);

                    // 2. 检查是否到达终点
                    if (move.LogicalPosition != move.TargetGridPosition)
                    {
                        if (!move.IsPathPending && move.HasNextStep)
                        {
                            Vector2Int nextStep = move.NextStepTile;

                            // 尝试普通移动
                            if (gridSystem.IsAreaClear(nextStep, size, core.SelfHandle.Id))
                            {
                                PerformMove(i, nextStep, whole, ref move, ref core, size);
                            }
                            // 尝试易位
                            else if (TrySwapMove(i, nextStep, whole, ref move, ref core, size))
                            {
                                // 易位成功，移动已执行
                            }
                            else
                            {
                                move.IsBlocked = true;
                                move.BlockWaitTimerTicks = 2;
                                move.HasNextStep = false;
                            }
                        }
                    }
                }
            }

            // --- 阶段 B：视觉插值 ---
            float t = 1.0f;
            if (move.MoveIntervalTicks > 0)
            {
                // 计算平滑因子
                float remainingTime = move.MoveTimerTicks * TICK_INTERVAL - TimeTicker.SubTickOffset;
                t = 1.0f - Mathf.Clamp01(remainingTime / (move.MoveIntervalTicks * TICK_INTERVAL));
            }

            // 只有当正在移动中(Timer > 0)或者刚移动完，才进行插值
            // 如果 Timer <= 0，通常 t = 1，直接吸附到 LogicalPosition
            Vector2 targetVisualPos = gridSystem.GridToWorld(move.LogicalPosition, size);
            core.Position = Vector2.Lerp(move.LastVisualPosition, targetVisualPos, t);
        }

        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var m = ref whole.moveComponent[i];
            ref var core = ref whole.coreComponent[i];

            if (m.Waypoints == null || m.Waypoints.Count == 0) continue;
            if (m.WaypointIndex >= m.Waypoints.Count) continue;

            var currentWp = m.Waypoints[m.WaypointIndex];
            bool shouldAdvance = false;
            // =========================================================
            // 判定分支 A：终点 (Endpoint)
            // 终点必须精确，不能模糊，否则单位会停在终点前 2 格发呆
            // =========================================================
            /*if (m.WaypointIndex == 0)
            {
                Debug.Log($"<color=cyan>[PathDebug]</color> 单位 {i} 正在检查 Index 0: " +
                          $"IsEndpoint={currentWp.IsEndpoint}, " +
                          $"Pos={currentWp.Pos}, " +
                          $"LogicalPos={m.LogicalPosition}, " +
                          $"WaypointsCount={m.Waypoints.Count}");
            }*/

            if (currentWp.IsEndpoint)
            {
                // 如果是路径的第一个点 (起点)
                if (m.WaypointIndex == 0)
                {
                    // Debug.Log("发现一个起点");
                    // 只要单位在起点格子上，或者离起点很近，直接无脑推进到下一个目标！
                    // 别在那等计时器了，赶紧看下一个路标喵！
                    if (m.LogicalPosition == currentWp.Pos)
                    {
                        // 只要后面还有路，就推进
                        if (m.Waypoints.Count > 1)
                        {
                            shouldAdvance = true;
                        }
                    }
                }
                // 如果是路径的最后一个点 (真正的终点)
                else if (m.WaypointIndex == m.Waypoints.Count - 1)
                {
                    // 终点必须精确到达，我们不推进 Index (因为后面没路了)
                    // 逻辑保持在原地，直到外部指令切换路径
                    if (m.LogicalPosition == currentWp.Pos)
                    {
                        // 已经到了，啥也不用做，HasNextStep 会在 Boids 里自然熄灭
                        shouldAdvance = false;
                    }
                }
            }
            // =========================================================
            // 判定分支 B：中间路点/门户 (Corner / Portal)
            // 允许“切弯”或者“拥挤通过”
            // =========================================================
            else
            {
                // 1. 计算距离
                Vector2Int delta = m.LogicalPosition - currentWp.Pos;
                int distChebyshev = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

                // --- 🔥 核心新增：过线判定 (Cross Line Check) ---
                bool hasCrossedPortalLine = false;
                if (currentWp.Portal != null)
                {
                    if (currentWp.IsVerticalGate)
                    {
                        // 垂直门看 X 轴。如果是向右走(isRightOrUp=true)，则 x >= 门坐标算过线
                        hasCrossedPortalLine = currentWp.isRightOrUp ?
                            (m.LogicalPosition.x >= currentWp.Portal.FixedCoord) :
                            (m.LogicalPosition.x < currentWp.Portal.FixedCoord);
                    }
                    else
                    {
                        // 水平门看 Y 轴
                        hasCrossedPortalLine = currentWp.isRightOrUp ?
                            (m.LogicalPosition.y >= currentWp.Portal.FixedCoord) :
                            (m.LogicalPosition.y < currentWp.Portal.FixedCoord);
                    }
                }
                else
                {
                    // 如果这个路点没有关联门户（单纯的房间内拐点），默认视为已过线，允许距离触发
                    hasCrossedPortalLine = true;
                }

                // --- 规则 1：距离容差 + 过线双重判定 ---
                if (distChebyshev <= 2 && hasCrossedPortalLine)
                {
                    shouldAdvance = true;
                }
                // --- 规则 2：几何切线 (Bisector Check) ---
                else
                {
                    // (这里的代码复用你原来的几何切线逻辑...)
                    Vector2 currPosWorld = gridSystem.GridToWorld(currentWp.Pos, core.LogicSize);
                    Vector2 myPosWorld = core.Position;

                    Vector2 prevPosWorld = (m.WaypointIndex > 0) ?
                        gridSystem.GridToWorld(m.Waypoints[m.WaypointIndex - 1].Pos, core.LogicSize) :
                        gridSystem.GridToWorld(m.PreviousLogicalPosition, core.LogicSize);

                    Vector2 nextPosWorld = (m.WaypointIndex < m.Waypoints.Count - 1) ?
                        gridSystem.GridToWorld(m.Waypoints[m.WaypointIndex + 1].Pos, core.LogicSize) :
                        currPosWorld;

                    Vector2 dirIn = (currPosWorld - prevPosWorld).normalized;
                    Vector2 dirOut = (nextPosWorld - currPosWorld).normalized;
                    Vector2 bisector = (dirIn + dirOut).normalized;
                    if (bisector.sqrMagnitude < 0.01f) bisector = dirIn;

                    Vector2 vectorToMe = myPosWorld - currPosWorld;
                    if (Vector2.Dot(vectorToMe, bisector) > 0)
                    {
                        shouldAdvance = true;
                    }
                }
            }

            // =========================================================
            // 🔥【新增】视线安全检查 (Line of Sight Check) 🔥
            // 防止隔墙误判。只有当几何判定通过，且不是终点时，才进行射线检测。
            // =========================================================
            if (shouldAdvance && !currentWp.IsEndpoint)
            {
                // 如果中间有墙，驳回推进请求！
                if (!CheckLineOfSight(gridSystem, m.LogicalPosition, currentWp.Pos))
                {
                    shouldAdvance = false;
                }
            }

            // --- 执行推进 ---
            if (shouldAdvance)
            {
                // 释放门户预约 (如果有)
                if (m.CurrentReservedPortal != null && currentWp.Portal == m.CurrentReservedPortal)
                {
                    m.CurrentReservedPortal.Timetables[m.CurrentReservedLane] &= ~m.CurrentReservedMask;
                    m.CurrentReservedPortal = null;
                    m.TargetPortalExit = null;
                }

                // 索引 +1
                if (m.WaypointIndex < m.Waypoints.Count - 1)
                {
                    // Debug.Log("路径点推进！");
                    m.WaypointIndex++;
                    m.IsBlocked = false;
                    m.BlockWaitTimerTicks = 0;
                }
            }
        }
    }

    /// <summary>
    /// 使用 Bresenham 算法检测两点之间是否有静态阻挡（墙壁/建筑）。
    /// 忽略动态单位阻挡。
    /// </summary>
    private bool CheckLineOfSight(GridSystem grid, Vector2Int start, Vector2Int end)
    {
        int x0 = start.x;
        int y0 = start.y;
        int x1 = end.x;
        int y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = (x0 < x1) ? 1 : -1;
        int sy = (y0 < y1) ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            // 检查当前格子是否是墙 (忽略起点和终点本身，防止起点卡在墙边缘)
            if ((x0 != start.x || y0 != start.y) && (x0 != end.x || y0 != end.y))
            {
                CellOccupancyType type = grid.GetCellOccupancy(new Vector2Int(x0, y0));
                // 只有地形和建筑算作“视线阻挡”，单位不算
                if (type == CellOccupancyType.TerrainBlocked || type == CellOccupancyType.Building)
                {
                    return false; // 视线被阻挡
                }
            }

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return true; // 通路无阻
    }

    /// <summary>
    /// 尝试与目标格子上的单位交换位置（易位）。
    /// </summary>
    private bool TrySwapMove(int selfIdx, Vector2Int nextStep, WholeComponent whole, ref MoveComponent move, ref CoreComponent core, Vector2Int size)
    {
        int occupantId = GridSystem.Instance.GetOccupantId(nextStep);
        if (occupantId == -1 || occupantId == core.SelfHandle.Id) return false;

        EntityHandle occHandle = EntitySystem.Instance.GetHandleFromId(occupantId);
        if (!EntitySystem.Instance.IsValid(occHandle)) return false;

        int occIdx = EntitySystem.Instance.GetIndex(occHandle);
        ref var occMove = ref whole.moveComponent[occIdx];
        // 对方的目标格子正好是当前单位的位置
        if (occMove.NextStepTile == move.LogicalPosition)
        {
            PerformMove(selfIdx, nextStep, whole, ref move, ref core, size);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 执行实际的移动操作（清理旧格子、更新位置、占据新格子、处理门户、重置计时器等）。
    /// </summary>
    private void PerformMove(int selfIdx, Vector2Int nextStep, WholeComponent whole, ref MoveComponent move, ref CoreComponent core, Vector2Int size)
    {
        var gridSystem = GridSystem.Instance;

        // 清理旧格子
        gridSystem.ClearOccupantRect(move.LogicalPosition, size);

        // 更新坐标
        move.PreviousLogicalPosition = move.LogicalPosition;
        move.LogicalPosition = nextStep;

        // 占据新格子
        gridSystem.SetOccupantRect(nextStep, size, core.SelfHandle.Id);

        // 重置计时器
        move.MoveTimerTicks = move.MoveIntervalTicks;
        move.IsBlocked = false;

        // 旋转
        Vector2Int dir = nextStep - move.PreviousLogicalPosition;
        if (dir != Vector2Int.zero) core.Rotation = dir;

        // 消费指令
        move.HasNextStep = false;
        move.StuckTimerTicks = 0; // 成功移动，卡死计时清零
    }
}