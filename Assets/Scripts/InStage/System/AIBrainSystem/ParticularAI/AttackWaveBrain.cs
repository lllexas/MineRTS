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

    public override void Initialize(int teamId, string identifier)
    {
        base.Initialize(teamId, identifier);
        // 波次AI应该有较高优先级，以确保能控制分配给它的单位
        this.Priority = 10;
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
        Debug.Log($"<color=orange>[{Identifier}]</color> 目标检查: _hasValidTarget={_hasValidTarget}, _targetPos={_targetPos}");
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
        Debug.Log($"<color=yellow>[{Identifier}] ExecutePass 开始</color>");
        string plan = args.ThinkingPlan as string;
        if (plan != "ATTACK_BASE")
        {
            Debug.Log($"<color=yellow>[{Identifier}] 计划不是ATTACK_BASE: {plan}</color>");
            return true;
        }

        // 遍历所有存活的手下，注入微操指令
        foreach (var handle in ControlledUnits)
        {
            if (!EntitySystem.Instance.IsValid(handle)) continue;

            // 使用统一的移动接口，而不是直接操作底层组件
            // 传递EntityHandle.None作为目标实体，表示是A地板（AttackMove）
            // Debug.Log($"<color=green>[{Identifier}] 调用AutoAISystem.RequestAttackMove 单位 {handle.Id}: AttackMove -> {_targetPos}</color>");
            AutoAISystem.Instance.RequestAttackMove(handle, _targetPos);
        }

        Debug.Log($"<color=yellow>[{Identifier}] ExecutePass 结束，控制 {ControlledUnits.Count} 个单位</color>");
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
        int buildingCount = 0;

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
                buildingCount++;
                float distSq = ((Vector2)(core.Position - center)).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    // 使用建筑的世界坐标转换为网格坐标
                    bestPos = GridSystem.Instance.WorldToGrid(core.Position);
                    found = true;
                    Debug.Log($"<color=cyan>[{Identifier}] 找到更近的建筑: 位置={core.Position}, 网格={bestPos}, 距离={Mathf.Sqrt(distSq)}</color>");
                }
            }
        }

        // 修正：我们需要的是 GridPos，如果 core.Position 是中心点，我们要把它转回左下角或者中心格子
        // 简单的办法是直接由 GridSystem 来转，或者取 core.Position 的 RoundToInt
        // 注意：bestPos 已经在第152行通过 GridSystem.Instance.WorldToGrid(core.Position) 转换为网格坐标
        // 所以这里不需要再次转换
        // if (found)
        // {
        //     bestPos = GridSystem.Instance.WorldToGrid(bestPos); // 错误：重复转换
        // }

        Debug.Log($"<color=cyan>[{Identifier}] FindNearestPlayerBuilding 结果: found={found}, 建筑数量={buildingCount}, bestPos={bestPos}</color>");
        return found;
    }
}