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
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var atk = ref whole.attackComponent[index];

        switch (ai.CurrentCommand)
        {
            case UnitCommand.Stop:
                move.TargetGridPosition = move.LogicalPosition;
                ai.TargetEntity = EntityHandle.None;
                ai.CurrentState = AIState.Idle;
                ai.CurrentCommand = UnitCommand.None; // 执行完重置
                break;

            case UnitCommand.Move:
                HandleMoveOrder(index, whole);
                break;

            case UnitCommand.AttackMove:
                HandleAttackMoveOrder(index, whole, deltaTime);
                break;

            case UnitCommand.AttackTarget:
                HandleAttackTargetOrder(index, whole);
                break;

            case UnitCommand.Hold:
                HandleHoldOrder(index, whole, deltaTime);
                break;

            case UnitCommand.None:
                // 默认本能：如果没事干，就原地扫描，看见人就去打
                HandleIdleAutoScan(index, whole, deltaTime);
                break;
        }
    }

    // --- 脊髓本能逻辑段 ---

    private void HandleMoveOrder(int index, WholeComponent whole)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];

        move.TargetGridPosition = ai.CommandPos;
        ai.TargetEntity = EntityHandle.None; // Move 指令无视敌人

        if (move.LogicalPosition == ai.CommandPos)
        {
            ai.CurrentCommand = UnitCommand.None;
            ai.CurrentState = AIState.Idle;
        }
    }

    private void HandleAttackMoveOrder(int index, WholeComponent whole, float deltaTime)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var atk = ref whole.attackComponent[index];

        // 1. 如果当前正在打人
        if (EntitySystem.Instance.IsValid(ai.TargetEntity))
        {
            HandleAttackTargetOrder(index, whole);
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
            ai.CurrentCommand = UnitCommand.None;
        }
    }

    private void HandleAttackTargetOrder(int index, WholeComponent whole)
    {
        ref var ai = ref whole.aiComponent[index];
        ref var move = ref whole.moveComponent[index];
        ref var atk = ref whole.attackComponent[index];

        if (!EntitySystem.Instance.IsValid(ai.TargetEntity))
        {
            ai.TargetEntity = EntityHandle.None;
            // 如果是锁定的目标死了，看之前有没有 AttackMove 的坐标，没有就归位
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

    private void HandleHoldOrder(int index, WholeComponent whole, float deltaTime)
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

    private void HandleIdleAutoScan(int index, WholeComponent whole, float deltaTime)
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
            float distSq = (targetCore.Position - selfPos).sqrMagnitude;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                bestTarget = targetCore.SelfHandle;
            }
        }
        return bestTarget;
    }
    // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
}