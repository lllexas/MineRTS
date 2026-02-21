using UnityEngine;
using AIBrain;
using System.Linq;

[AIBrainBarHere("Red_Dot_Wave")]
public class AttackWaveBrain : AIBrainBar
{
    private Vector2Int _targetPos;
    private bool _hasValidTarget = false;

    // 允许导演设立一个初始目标，如果不设，它会自己找
    public void SetTarget(Vector2Int target)
    {
        _targetPos = target;
        _hasValidTarget = true;
    }

    // --- 阶段 A：思考与索敌 ---
    public override bool ThinkingPass(AIBrainDecisionArgs args, WholeComponent whole)
    {
        Debug.Log("我在思考");
        // 1. 如果小队死光了，直接自杀
        if (ControlledUnits.Count == 0)
        {
            // 这里不需要做什么，AIBrainServer 会在 Update 里通过 PruneDeadUnits 自动把它清理掉
            return true;
        }

        // 2. 【波次专属逻辑】：不需要向 Server 申请新单位，直接要求保留自己的弟兄
        // 只要把 ControlledUnits 复制到 UnitsToSelect，Server 就知道“这些还是我的”
        foreach (var handle in ControlledUnits)
        {
            if (EntitySystem.Instance.IsValid(handle))
                args.UnitsToSelect.Add(handle);
        }

        // 3. 【宏观决策】：目标有效性检查
        // 如果我们还没有目标，或者之前的目标（那个建筑）已经被摧毁了
        if (!_hasValidTarget || !IsTargetBuildingAlive(whole, _targetPos))
        {
            // 重新寻找最近的玩家建筑
            _hasValidTarget = FindNearestPlayerBuilding(whole, out _targetPos);

            if (_hasValidTarget)
            {
                Debug.Log($"<color=red>[{Identifier}]</color> 目标已更新，锁定玩家建筑: {_targetPos}");
            }
            else
            {
                // 如果找不到任何建筑了，那就往地图中心或者玩家出生点冲（防止发呆）
                _targetPos = new Vector2Int(0, 0);
                Debug.Log($"<color=red>[{Identifier}]</color> 找不到玩家建筑，向世界中心 {_targetPos} 推进！");
            }
        }

        // 把战术意图写入计划
        // 这里的 Plan 只是个字符串，给 ExecutePass 看的
        args.ThinkingPlan = "ATTACK_BASE";

        return true;
    }

    // --- 阶段 E：下达脊髓指令 ---
    public override bool ExecutePass(AIBrainDecisionArgs args, WholeComponent whole)
    {
        string plan = args.ThinkingPlan as string;
        if (plan != "ATTACK_BASE") return true;

        // 遍历所有存活的手下，注入微操指令
        foreach (var handle in ControlledUnits)
        {
            int idx = EntitySystem.Instance.GetIndex(handle);
            if (idx == -1) continue;

            ref var ai = ref whole.aiComponent[idx];
            ref var move = ref whole.moveComponent[idx];

            // 优化：只有当单位当前指令不对，或者目标变了的时候，才重新下达指令
            // 这样可以避免每帧都重置寻路，导致单位抽搐
            bool needUpdate = (ai.CurrentCommand != UnitCommand.AttackMove) || (ai.CommandPos != _targetPos);

            if (needUpdate)
            {
                // 1. 设置脊髓指令
                ai.CurrentCommand = UnitCommand.AttackMove;
                ai.CommandPos = _targetPos;
                ai.CurrentState = AIState.Moving; // 强制唤醒

                // 2. 就像 A 键地板一样，必须重置移动组件的状态，让它重新寻路
                move.TargetGridPosition = _targetPos;
                move.IsPathPending = false;

                // 这里的关键：让 PathfindingSystem 为它算一条新路
                PathfindingSystem.Instance.RequestPath(idx);
            }
        }

        return true;
    }

    // ==========================================
    // 辅助索敌方法 (简单暴力版)
    // ==========================================

    private bool IsTargetBuildingAlive(WholeComponent whole, Vector2Int pos)
    {
        int occId = GridSystem.Instance.GetOccupantId(pos);
        if (occId == -1) return false;

        EntityHandle h = EntitySystem.Instance.GetHandleFromId(occId);
        if (!EntitySystem.Instance.IsValid(h)) return false;

        int idx = EntitySystem.Instance.GetIndex(h);
        ref var core = ref whole.coreComponent[idx];
        ref var health = ref whole.healthComponent[idx];

        // 必须是玩家队伍(1)，必须是建筑，必须活着
        return core.Team == 1 && (core.Type & UnitType.Building) != 0 && health.IsAlive;
    }

    private bool FindNearestPlayerBuilding(WholeComponent whole, out Vector2Int bestPos)
    {
        bestPos = Vector2Int.zero;
        float minDistSq = float.MaxValue;
        bool found = false;

        // 计算小队的大致中心点（为了找离大家都近的目标）
        Vector2 center = Vector2.zero;
        int count = 0;
        foreach (var handle in ControlledUnits)
        {
            int idx = EntitySystem.Instance.GetIndex(handle);
            if (idx != -1) { center += whole.coreComponent[idx].Position; count++; }
        }
        if (count > 0) center /= count;

        // 暴力遍历全图 (对于几十个建筑来说完全没问题)
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            ref var move = ref whole.moveComponent[i];
            ref var health = ref whole.healthComponent[i];

            // 只要是玩家的活建筑
            if (core.Active && core.Team == 1 && (core.Type & UnitType.Building) != 0 && health.IsAlive)
            {
                float distSq = (core.Position - center).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    // 使用建筑的世界坐标转换为网格坐标
                    bestPos = GridSystem.Instance.WorldToGrid(core.Position);
                    found = true;
                }
            }
        }

        // 修正：我们需要的是 GridPos，如果 core.Position 是中心点，我们要把它转回左下角或者中心格子
        // 简单的办法是直接由 GridSystem 来转，或者取 core.Position 的 RoundToInt
        if (found)
        {
            bestPos = GridSystem.Instance.WorldToGrid(bestPos); // 假设你有这个转换方法
            // 如果没有 WorldToGrid，可以用 new Vector2Int(Mathf.RoundToInt(bestPos.x), Mathf.RoundToInt(bestPos.y));
        }

        return found;
    }
}