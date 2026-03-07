using UnityEngine;

public class AutoAISystem : SingletonMono<AutoAISystem>
{
    private const float SCAN_INTERVAL = 0.2f; // 脊髓扫描频率

    public void UpdateAI(WholeComponent whole, float deltaTime)
    {
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            if (!core.Active) continue;

            ref var ai = ref whole.aiComponent[i];
            ref var move = ref whole.moveComponent[i];
            ref var atk = ref whole.attackComponent[i];
            ref var health = ref whole.healthComponent[i];

            if (!health.IsAlive) continue;

            // 每一帧，根据当前的命令(Command)驱动状态机(State)
            ExecuteSpinalLogic(i, whole, deltaTime);
        }
    }

    private void ExecuteSpinalLogic(int index, WholeComponent whole, float deltaTime)
    {
        // 将执行逻辑委托给 UpdateCommand
        UpdateCommand(index, whole, deltaTime);
    }

    // --- 核心状态机方法 ---

    /// <summary>
    /// 统一的状态转换入口
    /// </summary>
    private void ChangeCommand(int index, WholeComponent whole, UnitCommand newCmd,
                              Vector2Int cmdPos, EntityHandle targetHandle)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var atk = ref whole.attackComponent[index];

        // 检查是否需要状态转换
        if (ai.CurrentCommand == newCmd &&
            (Vector2Int)ai.CommandPos == cmdPos &&
            ai.TargetEntity == targetHandle)
        {
            // 相同的命令和参数，无需转换
            return;
        }

        // 1. 退出旧状态
        ExitCommand(index, whole, ai.CurrentCommand);

        // 2. 更新 AI 组件数据
        ai.CurrentCommand = newCmd;
        ai.CommandPos = cmdPos;
        ai.TargetEntity = targetHandle;

        // 3. 进入新状态
        EnterCommand(index, whole, newCmd);
    }

    /// <summary>
    /// 状态进入逻辑
    /// </summary>
    private void EnterCommand(int index, WholeComponent whole, UnitCommand cmd)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var atk = ref whole.attackComponent[index];

        // 重置扫描计时器
        ai.ScanTimer = SCAN_INTERVAL;

        switch (cmd)
        {
            case UnitCommand.Move:
                // 普通移动：直接重置移动状态并请求路径
                ResetMovementState(index, ai.CommandPos);
                ai.CurrentState = AIState.Moving;
                break;

            case UnitCommand.AttackMove:
                // 攻击移动：先扫描敌人再决定是否移动
                ai.ScanTimer = 0; // 强制立即扫描
                if (ScanForEnemy(index, whole, 0))
                {
                    // 扫描到敌人，不调用 ResetMovementState，直接进入移动状态
                    // UpdateAttackMoveOrder 会处理攻击逻辑
                    ai.CurrentState = AIState.Moving;
                }
                else
                {
                    // 没扫描到敌人，向目标位置移动
                    ResetMovementState(index, ai.CommandPos);
                    ai.CurrentState = AIState.Moving;
                }
                break;

            case UnitCommand.AttackTarget:
                // 攻击目标：确保攻击组件目标重置
                atk.TargetEntityId = -1;
                ai.CurrentState = AIState.Moving; // 默认移动，Update会调整
                break;

            case UnitCommand.Stop:
                // Stop 是瞬时指令，EnterCommand 中不执行实际逻辑
                // 清理工作在 ExitCommand(Stop) 中执行
                break;

            case UnitCommand.Hold:
                // 坚守：停止移动
                move.TargetGridPosition = move.LogicalPosition;
                ai.CurrentState = AIState.Idle;
                break;

            case UnitCommand.None:
                ai.CurrentState = AIState.Idle;
                break;
        }
    }

    /// <summary>
    /// 状态每帧更新逻辑
    /// </summary>
    private void UpdateCommand(int index, WholeComponent whole, float deltaTime)
    {
        ref var ai = ref whole.aiComponent[index];

        switch (ai.CurrentCommand)
        {
            case UnitCommand.Stop:
                // Stop 是瞬时指令，执行后立即转换到 None
                ChangeCommand(index, whole, UnitCommand.None, Vector2Int.zero, EntityHandle.None);
                break;

            case UnitCommand.Move:
                UpdateMoveOrder(index, whole);
                break;

            case UnitCommand.AttackMove:
                UpdateAttackMoveOrder(index, whole, deltaTime);
                break;

            case UnitCommand.AttackTarget:
                UpdateAttackTargetOrder(index, whole);
                break;

            case UnitCommand.Hold:
                UpdateHoldOrder(index, whole, deltaTime);
                break;

            case UnitCommand.None:
                UpdateIdleAutoScan(index, whole, deltaTime);
                break;
        }
    }

    /// <summary>
    /// 状态退出逻辑
    /// </summary>
    private void ExitCommand(int index, WholeComponent whole, UnitCommand cmd)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var atk = ref whole.attackComponent[index];

        switch (cmd)
        {
            case UnitCommand.AttackTarget:
                // 清理攻击目标
                atk.TargetEntityId = -1;
                ai.TargetEntity = EntityHandle.None;
                break;

            case UnitCommand.Move:
            case UnitCommand.AttackMove:
                // 移动类命令：清理移动目标
                move.TargetGridPosition = move.LogicalPosition;
                break;

            case UnitCommand.Hold:
                // 坚守：清理攻击目标
                atk.TargetEntityId = -1;
                ai.TargetEntity = EntityHandle.None;
                break;

            case UnitCommand.Stop:
                // 停止：立即停止移动
                move.TargetGridPosition = move.LogicalPosition;
                // 清除折返记忆
                move.PreviousLogicalPosition = move.LogicalPosition;
                // 取消待执行指令
                move.HasNextStep = false;
                // 路径也清掉
                move.Waypoints = null;
                move.IsBlocked = false;
                move.TargetPortalExit = null;
                move.StuckTimerTicks = 0;
                move.IsPathPending = false;
                break;
        }

        // 通用清理
        ai.CurrentState = AIState.Idle;
    }

    // --- 脊髓本能逻辑段 ---

    private void UpdateMoveOrder(int index, WholeComponent whole)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];

        move.TargetGridPosition = ai.CommandPos;
        ai.TargetEntity = EntityHandle.None; // Move 指令无视敌人

        if (move.LogicalPosition == ai.CommandPos)
        {
            // 到达目标，通过 ChangeCommand 转换到 None 状态
            ChangeCommand(index, whole, UnitCommand.None, Vector2Int.zero, EntityHandle.None);
        }
    }

    private void UpdateAttackMoveOrder(int index, WholeComponent whole, float deltaTime)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var atk = ref whole.attackComponent[index];

        // 1. 如果当前正在打人
        if (EntitySystem.Instance.IsValid(ai.TargetEntity))
        {
            UpdateAttackTargetOrder(index, whole);
            return;
        }

        // 2. 如果没在打人，尝试扫描敌人
        if (ScanForEnemy(index, whole, deltaTime))
        {
            // 扫描到了，交给下一帧的 AttackTarget 处理
            return;
        }

        // 3. 没敌人，继续走
        move.TargetGridPosition = ai.CommandPos;
        if (move.LogicalPosition == ai.CommandPos)
        {
            // 到达目标，通过 ChangeCommand 转换到 None 状态
            ChangeCommand(index, whole, UnitCommand.None, Vector2Int.zero, EntityHandle.None);
        }
    }

    private void UpdateAttackTargetOrder(int index, WholeComponent whole)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var atk = ref whole.attackComponent[index];

        if (!EntitySystem.Instance.IsValid(ai.TargetEntity))
        {
            // 目标失效
            if (ai.CurrentCommand == UnitCommand.AttackTarget)
            {
                // 如果是 AttackTarget 命令，转换到 None 状态
                ChangeCommand(index, whole, UnitCommand.None, Vector2Int.zero, EntityHandle.None);
            }
            else
            {
                // 如果是 AttackMove 嵌套调用，只清除目标
                ai.TargetEntity = EntityHandle.None;
            }
            return;
        }

        int targetIdx = EntitySystem.Instance.GetIndex(ai.TargetEntity);
        Vector2 targetPos = whole.coreComponent[targetIdx].Position;
        float dist = Vector2.Distance(whole.coreComponent[index].Position, targetPos);

        if (dist <= atk.AttackRange)
        {
            // 够得着，停下打
            move.TargetGridPosition = move.LogicalPosition;
            atk.TargetEntityId = ai.TargetEntity.Id;
            ai.CurrentState = AIState.Attacking;
        }
        else
        {
            // 够不着，追！
            move.TargetGridPosition = new Vector2Int(Mathf.RoundToInt(targetPos.x), Mathf.RoundToInt(targetPos.y));
            ai.CurrentState = AIState.Moving;
        }
    }

    private void UpdateHoldOrder(int index, WholeComponent whole, float deltaTime)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var atk = ref whole.attackComponent[index];

        move.TargetGridPosition = move.LogicalPosition; // 死也不准动

        if (ScanForEnemy(index, whole, deltaTime))
        {
            int targetIdx = EntitySystem.Instance.GetIndex(ai.TargetEntity);
            float dist = Vector2.Distance(whole.coreComponent[index].Position, whole.coreComponent[targetIdx].Position);
            // 只有在射程内才打
            if (dist <= atk.AttackRange)
                atk.TargetEntityId = ai.TargetEntity.Id;
            else
                ai.TargetEntity = EntityHandle.None;
        }
    }

    private void UpdateIdleAutoScan(int index, WholeComponent whole, float deltaTime)
    {
        ref var ai = ref whole.aiComponent[index];
        // Idle 状态下的本能：扫描并攻击。打完后会自动回到 Idle
        if (ScanForEnemy(index, whole, deltaTime))
        {
            ai.CurrentState = AIState.Attacking;
        }
        else
        {
            ai.CurrentState = AIState.Idle;
        }
    }


    // >>>>>>>>>>> [修改：解封自动扫描逻辑] >>>>>>>>>>>
    private bool ScanForEnemy(int index, WholeComponent whole, float deltaTime)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var atk = ref whole.attackComponent[index];

        ai.ScanTimer -= deltaTime;
        if (ai.ScanTimer <= 0)
        {
            ai.ScanTimer = SCAN_INTERVAL;

            // 如果蓝图中没有配视野，给一个默认的警戒范围：射程 + 3格 (保底 8 格)
            float actualScanRange = ai.ScanRange > 0 ? ai.ScanRange : Mathf.Max(atk.AttackRange + 3.0f, 8.0f);

            EntityHandle nearest = FindNearestEnemyInRange(index, whole, actualScanRange);
            if (nearest != EntityHandle.None)
            {
                ai.TargetEntity = nearest;
                return true;
            }
        }
        return ai.TargetEntity != EntityHandle.None;
    }

    // [新增] 暴力但可靠的全图距离筛选
    private EntityHandle FindNearestEnemyInRange(int selfIndex, WholeComponent whole, float range)
    {
        ref var selfCore = ref whole.coreComponent[selfIndex];
        Vector2 selfPos = selfCore.Position;
        int selfTeam = selfCore.Team;

        float minDistSq = range * range; // 使用平方距离，省去开方的性能消耗
        EntityHandle bestTarget = EntityHandle.None;

        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var targetCore = ref whole.coreComponent[i];
            ref var targetHealth = ref whole.healthComponent[i];

            // 必须是活着的敌对实体
            if (!targetCore.Active || !targetHealth.IsAlive) continue;
            if (targetCore.Team == selfTeam) continue;

            // 只能攻击建筑、小兵或英雄（无视资源包、子弹等）
            if ((targetCore.Type & (UnitType.Building | UnitType.Minion | UnitType.Hero)) == 0) continue;

            // 距离计算 (如果以后有巨型 Boss，这里可以改成边缘距离)
            float distSq = ((Vector2)(targetCore.Position - selfPos)).sqrMagnitude;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                bestTarget = targetCore.SelfHandle;
            }
        }
        return bestTarget;
    }
    // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<

    // --- 公共API：外部系统调用 ---

    /// <summary>
    /// 移动重置逻辑（洗脑）：清除路径记忆，强制请求新路径
    /// </summary>
    private void ResetMovementState(int index, Vector2Int targetGrid)
    {
        var whole = EntitySystem.Instance.wholeComponent;
        ref var move = ref whole.moveComponent[index];

        // 目标位置设置
        move.TargetGridPosition = targetGrid;

        // 彻底洗脑：让 Boids 认为单位是刚出生的，没有任何移动惯性和折返阻尼
        move.PreviousLogicalPosition = move.LogicalPosition;
        move.NextStepTile = move.LogicalPosition;
        move.HasNextStep = false;

        // 寻路重置与强制请求
        move.Waypoints = null;
        move.WaypointIndex = 0;
        move.IsBlocked = false;
        move.TargetPortalExit = null;
        move.StuckTimerTicks = 0; // 顺便把卡死计时也清了

        // 即使 IsPathPending 是 true，我们也应该允许重新覆盖请求
        move.IsPathPending = false; // 强制解锁，确保 RequestPath 必成
        PathfindingSystem.Instance.RequestPath(index);
    }

    /// <summary>
    /// 请求单位移动到目标网格（普通移动指令）
    /// </summary>
    public void RequestMove(EntityHandle handle, Vector2Int targetGrid)
    {
        // Debug.Log($"<color=magenta>[AutoAISystem] RequestMove 单位 {handle.Id} -> 目标网格 {targetGrid}</color>");
        if (!EntitySystem.Instance.IsValid(handle)) return;
        int idx = EntitySystem.Instance.GetIndex(handle);
        var whole = EntitySystem.Instance.wholeComponent;

        // 建筑不能移动
        if ((whole.coreComponent[idx].Type & UnitType.Building) != 0) return;

        // 使用统一的状态转换
        ChangeCommand(idx, whole, UnitCommand.Move, targetGrid, EntityHandle.None);
    }

    /// <summary>
    /// 请求单位执行攻击移动（A地板）
    /// </summary>
    public void RequestAttackMove(EntityHandle handle, Vector2Int targetGrid)
    {
        // Debug.Log($"<color=magenta>[AutoAISystem] RequestAttackMove 单位 {handle.Id} -> 目标网格 {targetGrid}</color>");
        if (!EntitySystem.Instance.IsValid(handle)) return;
        int idx = EntitySystem.Instance.GetIndex(handle);
        var whole = EntitySystem.Instance.wholeComponent;

        // 只有战斗单位能攻击
        if ((whole.coreComponent[idx].Type & (UnitType.Minion | UnitType.Hero)) == 0) return;

        // 使用统一的状态转换
        ChangeCommand(idx, whole, UnitCommand.AttackMove, targetGrid, EntityHandle.None);
    }

    /// <summary>
    /// 请求单位攻击特定目标（A人）
    /// </summary>
    public void RequestAttackTarget(EntityHandle handle, Vector2Int targetGrid, EntityHandle targetEntity)
    {
        if (!EntitySystem.Instance.IsValid(handle)) return;
        int idx = EntitySystem.Instance.GetIndex(handle);
        var whole = EntitySystem.Instance.wholeComponent;

        // 只有战斗单位能攻击
        if ((whole.coreComponent[idx].Type & (UnitType.Minion | UnitType.Hero)) == 0) return;

        // 1. 判定是 A地板 还是 A人
        if (EntitySystem.Instance.IsValid(targetEntity))
        {
            // --- 情况 A: A人 (AttackTarget) ---
            // 只有当目标是敌军时才锁定攻击
            int tIdx = EntitySystem.Instance.GetIndex(targetEntity);
            if (whole.coreComponent[tIdx].Team != whole.coreComponent[idx].Team)
            {
                // 敌军：锁定攻击
                ChangeCommand(idx, whole, UnitCommand.AttackTarget, targetGrid, targetEntity);
            }
            else
            {
                // 如果A了友军，强制转换成 AttackMove（攻击移动）到那个坐标，而不是纯 Move（和平移动）
                ChangeCommand(idx, whole, UnitCommand.AttackMove, targetGrid, EntityHandle.None);
            }
        }
        else
        {
            // --- 情况 B: A地板 (AttackMove) ---
            // 实际上不应该进入这个分支，因为targetEntity为None时应调用RequestAttackMove
            // 但为了健壮性，这里也处理一下
            ChangeCommand(idx, whole, UnitCommand.AttackMove, targetGrid, EntityHandle.None);
        }
    }

    /// <summary>
    /// 请求单位停止所有动作（S键停止）
    /// </summary>
    public void RequestStop(EntityHandle handle)
    {
        if (!EntitySystem.Instance.IsValid(handle)) return;
        int idx = EntitySystem.Instance.GetIndex(handle);
        var whole = EntitySystem.Instance.wholeComponent;

        // 使用统一的状态转换
        ChangeCommand(idx, whole, UnitCommand.Stop, Vector2Int.zero, EntityHandle.None);
    }
}