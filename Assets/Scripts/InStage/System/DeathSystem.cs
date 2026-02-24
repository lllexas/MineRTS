using UnityEngine;

/// <summary>
/// 死亡系统
/// 统一处理所有实体的死亡判定、效果触发和清理工作
/// 职责：检查Health <= 0且IsAlive==true的实体，触发死亡效果，调用DestroyEntity
/// 设计原则：所有扣血系统只负责扣血，死亡统一由此系统处理
/// </summary>
public class DeathSystem : SingletonMono<DeathSystem>
{
    /// <summary>
    /// 主更新方法：检查并处理所有死亡实体
    /// 应在所有扣血系统（AttackSystem, ProjectileSystem, GoRuleSystem）之后调用
    /// 应在依赖IsAlive的系统（AI, 绘制）之前调用
    /// </summary>
    public void UpdateDeaths(WholeComponent whole, float deltaTime)
    {
        // 倒序遍历（因为DestroyEntity会使用swap-back机制）
        for (int i = whole.entityCount - 1; i >= 0; i--)
        {
            ref var core = ref whole.coreComponent[i];
            ref var health = ref whole.healthComponent[i];

            if (!core.Active) continue;

            // 检查死亡条件：生命值≤0 且 实体仍活跃
            if (health.Health <= 0 && core.Active)
            {
                // 标记为死亡状态（如果尚未标记）
                health.IsAlive = false;

                // 触发死亡效果（任务广播、殉爆等）
                OnEntityDied(whole, i);

                // 立即清理实体（也可设计为延迟清理用于死亡动画）
                EntitySystem.Instance.DestroyEntity(core.SelfHandle);
            }
        }
    }

    /// <summary>
    /// 实体死亡时的效果处理
    /// </summary>
    private void OnEntityDied(WholeComponent whole, int entityIndex)
    {
        ref var core = ref whole.coreComponent[entityIndex];
        ref var health = ref whole.healthComponent[entityIndex];

        // 任务广播逻辑（玩家阵营击败非玩家阵营）
        if (health.LastAttackerFaction == 1 && core.Team != 1)
        {
            string deadUnitBP = core.BlueprintName;
            if (!string.IsNullOrEmpty(deadUnitBP))
            {
                // 1. 广播具体蓝图名 (如：击败 5 个“爬行者”)
                PostSystem.Instance.Send("击败目标", deadUnitBP);

                // 2. 广播通用击败信号 (如：击败任意 10 个敌人)
                PostSystem.Instance.Send("击败任意目标", 1);
            }
        }

        // 殉爆逻辑
        if (health.ExplodeOnDeath)
        {
            TriggerDeathExplosion(whole, entityIndex);
        }

        // 调试日志
        if (GoRuleSystem.Instance != null && GoRuleSystem.Instance.enableDebugLog)
        {
            Debug.Log($"[DeathSystem] 实体 {entityIndex} ({core.BlueprintName}) 死亡");
        }
    }

    /// <summary>
    /// 触发死亡爆炸效果
    /// 未来可扩展为爆炸伤害周围单位
    /// </summary>
    private void TriggerDeathExplosion(WholeComponent whole, int entityIndex)
    {
        ref var core = ref whole.coreComponent[entityIndex];
        ref var attack = ref whole.attackComponent[entityIndex];

        // 简化的殉爆逻辑：未来可在此实现范围伤害
        if (GoRuleSystem.Instance != null && GoRuleSystem.Instance.enableDebugLog)
        {
            Debug.Log($"[DeathSystem] 实体 {entityIndex} 殉爆 (范围{attack.AttackRange})");
        }

        // TODO: 实现范围伤害逻辑，可复用AttackSystem的伤害机制
        // 例如：查找周围单位，调用ApplyDamage等
    }

    /// <summary>
    /// 外部直接调用的强制死亡方法（用于特殊情况）
    /// </summary>
    public void KillEntityDirectly(EntityHandle handle, int attackerFaction = -1)
    {
        var entitySystem = EntitySystem.Instance;
        if (!entitySystem.IsValid(handle)) return;

        int index = entitySystem.GetIndex(handle);
        var whole = entitySystem.wholeComponent;

        ref var core = ref whole.coreComponent[index];
        ref var health = ref whole.healthComponent[index];

        if (!core.Active || !health.IsAlive) return;

        // 设置死亡状态
        health.LastAttackerFaction = attackerFaction;
        health.Health = 0;
        health.IsAlive = false;

        // 触发死亡效果
        OnEntityDied(whole, index);

        // 可选：记录击杀者阵营用于任务广播
        // 当前暂不处理，需要重构任务系统

        // 清理实体
        entitySystem.DestroyEntity(handle);
    }
}