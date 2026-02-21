using UnityEngine;

public class AttackSystem : SingletonMono<AttackSystem>
{
    public void UpdateAttacks(WholeComponent whole, float deltaTime)
    {
        var entitySystem = EntitySystem.Instance;

        // 【关键手术 1：倒序遍历】
        // 既然系统里会调用 DestroyEntity (近战击杀)，必须从后往前扫，
        // 否则 Swap-back 机制会把最后一个实体补到当前位置，导致它被循环跳过喵！
        for (int i = whole.entityCount - 1; i >= 0; i--)
        {
            ref var core = ref whole.coreComponent[i];
            ref var attack = ref whole.attackComponent[i];
            ref var ai = ref whole.aiComponent[i]; // 引入 AI 组件引用

            if (!core.Active) continue;

            // --- 【关键手术 2：数据桥接】 ---
            // 如果单位有 AI 目标，则实时同步给攻击目标的 ID
            // 这样 AISystem 负责“找人”，AttackSystem 负责“开火”，逻辑就通了喵！
            if (entitySystem.IsValid(ai.TargetEntity))
            {
                attack.TargetEntityId = ai.TargetEntity.Id;
            }
            else
            {
                // 如果 AI 没目标了，攻击系统也要停火
                attack.TargetEntityId = -1;
            }

            // 1. 基础检查
            if (attack.TargetEntityId == -1) continue;

            // 2. 检查冷却
            if (Time.time < attack.LastAttackTime + attack.AttackCooldown) continue;

            // 3. 验证目标有效性
            EntityHandle targetHandle = entitySystem.GetHandleFromId(attack.TargetEntityId);
            if (!entitySystem.IsValid(targetHandle))
            {
                attack.TargetEntityId = -1;
                continue;
            }

            int targetIndex = entitySystem.GetIndex(targetHandle);
            ref var targetCore = ref whole.coreComponent[targetIndex];
            ref var targetHealth = ref whole.healthComponent[targetIndex];

            // 4. 再次确认目标是否活着
            if (!targetHealth.IsAlive)
            {
                attack.TargetEntityId = -1;
                continue;
            }

            // 5. 距离检查
            float dist = Vector2.Distance(core.Position, targetCore.Position);

            if (dist <= attack.AttackRange + 0.5f)
            {
                // --- 执行攻击 ---

                // A. 调整朝向 (看向目标)
                Vector2 dir = (targetCore.Position - core.Position).normalized;
                if (dir != Vector2.zero)
                {
                    // 将方向向量转为网格方向 Vector2Int (-1~1)
                    core.Rotation = new Vector2Int(Mathf.RoundToInt(dir.x), Mathf.RoundToInt(dir.y));
                }

                // B. 判定攻击模式
                if (attack.ProjectileSpriteId < 0)
                {
                    // === 近战逻辑 ===
                    // 使用统一的伤害函数，处理扣血、击杀、销毁、网格释放
                    ApplyDamage(whole, targetHandle, attack.AttackDamage, core.Team);

                    // 如果目标被打死了，清理本单位的目标锁定
                    if (!targetHealth.IsAlive)
                    {
                        attack.TargetEntityId = -1;
                        if (ai.TargetEntity == targetHandle) ai.TargetEntity = EntityHandle.None;
                    }
                }
                else
                {
                    // === 远程逻辑 ===
                    SpawnProjectile(whole, i, targetIndex);
                }

                // C. 重置冷却
                attack.LastAttackTime = Time.time;
            }
        }
    }

    private void SpawnProjectile(WholeComponent whole, int attackerIndex, int targetIndex)
    {
        ref var attackerCore = ref whole.coreComponent[attackerIndex];
        ref var attackerAtk = ref whole.attackComponent[attackerIndex];
        ref var targetCore = ref whole.coreComponent[targetIndex];

        // 1. 创建子弹实体 (位置 = 攻击者中心)
        EntityHandle bulletHandle = EntitySystem.Instance.CreateEntity(
            new Vector2Int(-999, -999),
            attackerCore.Team,
            UnitType.Projectile,
            Vector2Int.zero
        );

        if (bulletHandle == EntityHandle.None) return;
        int bulletIdx = EntitySystem.Instance.GetIndex(bulletHandle);

        // 2. 填充 Core (子弹出生在攻击者坐标)
        ref var bulletCore = ref whole.coreComponent[bulletIdx];
        bulletCore.Position = attackerCore.Position;
        bulletCore.VisualScale = new Vector2(0.4f, 0.4f);

        // 3. 填充 Draw
        ref var bulletDraw = ref whole.drawComponent[bulletIdx];
        bulletDraw.SpriteId = attackerAtk.ProjectileSpriteId;

        // 4. 填充 Projectile 逻辑属性
        ref var bulletProj = ref whole.projectileComponent[bulletIdx];
        bulletProj.SourceEntityId = attackerCore.SelfHandle.Id;
        bulletProj.TargetEntityId = targetCore.SelfHandle.Id;
        bulletProj.TargetPosition = targetCore.Position;
        bulletProj.Speed = attackerAtk.ProjectileSpeed > 0 ? attackerAtk.ProjectileSpeed : 12f;
        bulletProj.Damage = attackerAtk.AttackDamage;
        bulletProj.HitRadius = 0.4f;
        bulletProj.IsHoming = true;

        // 5. 确保子弹不被 MoveSystem 错误移动
        whole.moveComponent[bulletIdx].MoveIntervalTicks = -1;
        whole.aiComponent[bulletIdx].CurrentState = AIState.Idle; // 子弹不需要 AI
    }

    // 【关键手术 3：统一伤害结算】
    public void ApplyDamage(WholeComponent whole, EntityHandle targetHandle, float damage, int attackerFaction)
    {
        int targetIdx = EntitySystem.Instance.GetIndex(targetHandle);
        if (targetIdx == -1) return;

        ref var health = ref whole.healthComponent[targetIdx];
        if (!health.IsAlive) return;
        ref var targetCore = ref whole.coreComponent[targetIdx];

        health.Health -= damage;
        if (health.Health <= 0)
        {
            health.Health = 0;
            health.IsAlive = false;

            //------------------- 修改：增加阵营过滤的任务广播逻辑
            // 只有当“玩家阵营”击败“非玩家阵营”时才算任务进度喵！
            if (attackerFaction == 1 && targetCore.Team != 1)
            {
                string deadUnitBP = targetCore.BlueprintName;
                if (!string.IsNullOrEmpty(deadUnitBP))
                {
                    // 1. 广播具体蓝图名 (如：击败 5 个“爬行者”)
                    PostSystem.Instance.Send("击败目标", deadUnitBP);

                    // 2. 广播通用击败信号 (如：击败任意 10 个敌人)
                    PostSystem.Instance.Send("击败任意目标", 1);
                }
            }
            //-------------------

            // 击杀时彻底销毁，触发网格占位清理喵！
            EntitySystem.Instance.DestroyEntity(targetHandle);
            Debug.Log($"<color=orange>[Battle]</color> 目标 {targetHandle.Id} 已被摧毁喵！");
        }
    }
}