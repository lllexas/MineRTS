using System.Collections.Generic;
using UnityEngine;

public class BoidsSystem : SingletonMono<BoidsSystem>
{
    private Dictionary<Vector2Int, List<int>> _competitionTable = new Dictionary<Vector2Int, List<int>>();
    private const int MAX_RESOLUTION_LOOPS = 4;

    // 预定义 4 个正交移动向量
    private static readonly Vector2Int[] Directions = {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    public void UpdateTacticalBoids(WholeComponent whole)
    {
        _competitionTable.Clear();

        // ==========================================================
        // 第一轮：申报意图 (Proposal Phase)
        // 基于“100度广角”与“向心宗旨”
        // ==========================================================
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var m = ref whole.moveComponent[i];
            ref var core = ref whole.coreComponent[i];

            if (!core.Active || (core.Type & UnitType.Building) != 0) continue;
            if (m.Waypoints == null || m.WaypointIndex >= m.Waypoints.Count) continue;
            if (m.HasNextStep) continue;

            // 获取当前战略目标点
            Vector2Int waypointPos = m.Waypoints[m.WaypointIndex].Pos;

            // 执行核心抉择：寻找最佳候选格
            Vector2Int proposedTile = GetTacticalChoice(i, whole, waypointPos);

            m.NextStepTile = proposedTile;

            // 如果打算移动，注册竞争
            if (proposedTile != m.LogicalPosition)
            {
                if (!_competitionTable.TryGetValue(proposedTile, out var list))
                {
                    list = new List<int>();
                    _competitionTable[proposedTile] = list;
                }
                list.Add(i);
            }
        }

        // ==========================================================
        // 第二轮：竞争仲裁 (Competition Phase)
        // 规则：离目标越近、ID 越小的单位越优先
        // ==========================================================
        foreach (var kvp in _competitionTable)
        {
            List<int> candidates = kvp.Value;
            if (candidates.Count <= 1) continue;

            int winnerIdx = -1;
            float minTargetDist = float.MaxValue;

            foreach (int idx in candidates)
            {
                ref var m = ref whole.moveComponent[idx];
                // 评估标准：候选格子到目标的物理距离（谁更有资格填那个坑）
                float dist = (m.Waypoints[m.WaypointIndex].Pos - kvp.Key).sqrMagnitude;
                if (dist < minTargetDist)
                {
                    minTargetDist = dist;
                    winnerIdx = idx;
                }
            }

            foreach (int idx in candidates)
            {
                if (idx == winnerIdx) continue;
                whole.moveComponent[idx].NextStepTile = whole.moveComponent[idx].LogicalPosition;
            }
        }

        // ==========================================================
        // 第三轮：链式传导与易位 (Resolution Phase)
        // ==========================================================
        for (int loop = 0; loop < MAX_RESOLUTION_LOOPS; loop++)
        {
            bool anyChange = false;
            for (int i = 0; i < whole.entityCount; i++)
            {
                ref var m = ref whole.moveComponent[i];
                ref var core = ref whole.coreComponent[i];
                if (!core.Active || m.NextStepTile == m.LogicalPosition) continue;

                int occupantId = GridSystem.Instance.GetOccupantId(m.NextStepTile);
                if (occupantId == -1 || occupantId == core.SelfHandle.Id) continue;

                EntityHandle occupantHandle = EntitySystem.Instance.GetHandleFromId(occupantId);
                if (!EntitySystem.Instance.IsValid(occupantHandle)) continue;

                int occupantIdx = EntitySystem.Instance.GetIndex(occupantHandle);
                ref var otherMove = ref whole.moveComponent[occupantIdx];

                // 如果目标格的人也要动，看他能不能走掉
                if (otherMove.NextStepTile != otherMove.LogicalPosition)
                {
                    // 检查是否是同队易位 (Swap)
                    if (otherMove.NextStepTile == m.LogicalPosition && core.Team == whole.coreComponent[occupantIdx].Team)
                        continue; // 允许交换位置

                    // 链式依赖：如果对方被堵了，我也得停
                    // 这会在 resolution 循环的后续步骤中传导过来
                    continue;
                }
                else
                {
                    // 对方真的不动（已经到终点了，或者是建筑，或者是输了竞争）
                    m.NextStepTile = m.LogicalPosition;
                    anyChange = true;
                }
            }
            if (!anyChange) break;
        }

        // 最终下达指令
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var m = ref whole.moveComponent[i];
            if (whole.coreComponent[i].Active)
                m.HasNextStep = (m.NextStepTile != m.LogicalPosition);
        }
    }
    private Vector2Int GetTacticalChoice(int selfIdx, WholeComponent whole, Vector2Int target)
    {
        ref var m = ref whole.moveComponent[selfIdx];
        ref var core = ref whole.coreComponent[selfIdx];
        Vector2Int current = m.LogicalPosition;
        if (current == target) return current;

        // --- 1. 基础向量计算 ---
        Vector2 diff = new Vector2(target.x - current.x, target.y - current.y);
        float distSq = diff.sqrMagnitude;
        Vector2 toTargetDir = diff.normalized;

        // --- 2. 绝境感知与预判 ---
        bool isStuck = (m.BlockWaitTimerTicks > 0) && !m.Waypoints[m.WaypointIndex].IsEndpoint;
        if (!isStuck)
        {
            float maxD = -2f;
            Vector2Int desiredDir = Vector2Int.zero;
            foreach (var d in Directions)
            {
                float dot = Vector2.Dot(new Vector2(d.x, d.y), toTargetDir);
                if (dot > maxD) { maxD = dot; desiredDir = d; }
            }
            CellOccupancyType occ = GridSystem.Instance.GetAreaOccupancy(current + desiredDir, core.LogicSize, core.SelfHandle.Id);
            if (occ == CellOccupancyType.Building || occ == CellOccupancyType.TerrainBlocked) isStuck = true;
        }

        // --- 3. 计算战略偏好 (Strategic Bias) ---
        Vector2 leftSide = new Vector2(-toTargetDir.y, toTargetDir.x);
        Vector2 rightSide = new Vector2(toTargetDir.y, -toTargetDir.x);
        float sideBias = 0f; // 正数偏左，负数偏右

        // 如果有下一个路点，预判绕路方向
        if (m.WaypointIndex < m.Waypoints.Count - 1)
        {
            Vector2 nextWpPos = m.Waypoints[m.WaypointIndex + 1].Pos;
            Vector2 currWpPos = m.Waypoints[m.WaypointIndex].Pos;
            Vector2 nextPathDir = (nextWpPos - currWpPos).normalized;

            // 看看下一个路点倾向于哪边
            float leftDot = Vector2.Dot(nextPathDir, leftSide);
            float rightDot = Vector2.Dot(nextPathDir, rightSide);
            sideBias = leftDot - rightDot; // 差值代表偏好强度
        }

        float geometricLimit = isStuck ? -1.01f : -0.71f;
        Vector2 flowDir = GetLocalFlowDir(selfIdx, whole);
        Vector2Int bestCandidate = current;

        // 这里的 EvaluateCandidate 需要接收 sideBias
        float bestScore = EvaluateCandidate(selfIdx, current, Vector2Int.zero, distSq, whole, m.PreviousLogicalPosition, 0f, flowDir, isStuck, leftSide, rightSide, sideBias);

        foreach (var dir in Directions)
        {
            Vector2Int cand = current + dir;
            float dot = Vector2.Dot(new Vector2(dir.x, dir.y), toTargetDir);
            if (dot < geometricLimit) continue;

            float score = EvaluateCandidate(selfIdx, cand, dir, distSq, whole, m.PreviousLogicalPosition, dot, flowDir, isStuck, leftSide, rightSide, sideBias);

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = cand;
            }
        }

        return bestCandidate;
    }

    private float EvaluateCandidate(int selfIdx, Vector2Int cand, Vector2Int moveDir, float originalDistSq, WholeComponent whole, Vector2Int prevPos, float dot, Vector2 flowDir, bool isStuck, Vector2 leftSide, Vector2 rightSide, float sideBias)
    {
        ref var m = ref whole.moveComponent[selfIdx];
        ref var core = ref whole.coreComponent[selfIdx];

        // 1. 环境感知 (利用 GetAreaOccupancy)
        CellOccupancyType occupancy = GridSystem.Instance.GetAreaOccupancy(cand, core.LogicSize, core.SelfHandle.Id);

        // 绝对阻挡判定
        if (occupancy == CellOccupancyType.OutOfBounds || occupancy == CellOccupancyType.TerrainBlocked) return -999999f;
        if (occupancy == CellOccupancyType.Building) return -999999f;

        // --- 核心判定：是否在终点附近 ---
        bool isFinalGoal = m.Waypoints[m.WaypointIndex].IsEndpoint;

        // 2. 动态权重设置
        // 【终点特调】：稍微降低终点的引力权重 (10 -> 8)，让单位“没那么怕”稍微站远一点去填补空位
        float distWeight = isStuck ? 0.5f : (isFinalGoal ? 8.0f : 10.0f);
        float focusWeight = isStuck ? 2.0f : 25.0f;

        // 3. 基础引力评分
        Vector2Int target = m.Waypoints[m.WaypointIndex].Pos;
        float newDistSq = (moveDir == Vector2Int.zero) ? originalDistSq : (target - cand).sqrMagnitude;
        float score = -newDistSq * distWeight;

        if (moveDir != Vector2Int.zero) // 打算移动
        {
            // A. 折返阻尼 (严禁回头)
            if (cand == prevPos) return -999999f;

            // B. 单位占据惩罚 (钻人的代价)
            if (occupancy == CellOccupancyType.Unit) score -= 2000f;

            // C. 基础移动偏好
            score += 2.0f;

            // 专注奖励
            score += dot * focusWeight;

            // --- D. 绝境绕路逻辑 (isStuck 状态) ---
            if (isStuck)
            {
                Vector2 moveV = new Vector2(moveDir.x, moveDir.y);
                float dotL = Vector2.Dot(moveV, leftSide);
                float dotR = Vector2.Dot(moveV, rightSide);

                if (dotL > 0.7f || dotR > 0.7f)
                {
                    score += 50.0f; // 基础绕路奖金
                    if (dotL > 0.7f) score += sideBias * 30.0f;
                    else if (dotR > 0.7f) score -= sideBias * 30.0f;
                }

                if (dot < -0.5f) score -= 100.0f; // 惩罚后退

                float noise = ((selfIdx + cand.x * 7 + cand.y * 13) % 10);
                score += noise;
            }

            // --- 🔥 E. 轨道滑移补偿 (针对终点滑移倾向的加强) ---
            if (newDistSq > originalDistSq && dot < 0.1f)
            {
                float loss = (newDistSq - originalDistSq) * distWeight;

                if (isFinalGoal)
                {
                    // 【终点策略】：高倾向滑移。
                    // 使用 1.3 倍报销 + 3.0 分额外奖金。
                    // 1.3倍意味着滑移不仅不亏，还能小赚一点。
                    // 这会促使单位在“正前方有人”时，非常积极地向侧面滑动来寻找更好的切入点。
                    score += (loss * 1.3f) + 3.0f;
                }
                else
                {
                    // 【路径策略】：盈利模式。
                    // 1.5 倍报销 + 5 分奖励，鼓励激进绕行以保持移动动量。
                    score += (loss * 1.5f) + 5.0f;
                }
            }
        }
        else // 原地待命
        {
            // --- 🔥 F. 待命分 (静摩擦力) ---
            // 终点时设定为 20.0。
            // 逻辑：20分略大于（2分偏好+3分滑移奖金），但远小于（避免-2000分钻人惩罚）。
            // 这意味着：只要前面稍微有点挤，单位就会果断滑移；但如果周围很空，它会停下。
            float standbyBonus = isFinalGoal ? 20.0f : 15.0f;
            score += isStuck ? -20.0f : standbyBonus;
        }

        return score;
    }

    // 这就是我们要补的“只读不写”的观察函数
    private Vector2 GetLocalFlowDir(int selfIdx, WholeComponent whole)
    {
        Vector2 sum = Vector2.zero;
        int count = 0;
        ref var m = ref whole.moveComponent[selfIdx];
        ref var core = ref whole.coreComponent[selfIdx];

        // 既然 Directions 就在这，顺便查一下四周
        foreach (var dir in Directions)
        {
            Vector2Int neighborPos = m.LogicalPosition + dir;
            int occId = GridSystem.Instance.GetOccupantId(neighborPos);

            // 没人或者是自己，跳过
            if (occId == -1 || occId == core.SelfHandle.Id) continue;

            // 查组件 (用标准接口 IsValid)
            EntityHandle handle = EntitySystem.Instance.GetHandleFromId(occId);
            if (!EntitySystem.Instance.IsValid(handle)) continue;

            int nIdx = EntitySystem.Instance.GetIndex(handle);

            // 必须是同队才参考
            if (whole.coreComponent[nIdx].Team != core.Team) continue;

            ref var nM = ref whole.moveComponent[nIdx];

            // 核心：看邻居的动量
            // LogicalPosition - PreviousLogicalPosition 代表他上一帧的实际位移
            Vector2Int vel = nM.LogicalPosition - nM.PreviousLogicalPosition;

            if (vel != Vector2Int.zero)
            {
                sum.x += vel.x;
                sum.y += vel.y;
                count++;
            }
        }

        return count > 0 ? sum.normalized : Vector2.zero;
    }
}