using UnityEngine;

/// <summary>
/// Consumes frame-tag events from AnimationEventComponent and performs attack
/// actions (melee damage / projectile spawn) at the correct animation frame.
///
/// Run AFTER DrawSystem (which writes CrossedTags) and BEFORE DeathSystem.
/// AttackSystem still handles: cooldown, target validation, and starting the
/// attack animation (WantsAttack + LastAttackTick). This system only fires
/// when the animation reaches the tagged hit frame.
/// </summary>
public class AttackEventSystem : SingletonMono<AttackEventSystem>
{
    public void UpdateEvents(WholeComponent whole)
    {
        var entitySystem = EntitySystem.Instance;

        for (int i = 0; i < whole.entityCount; i++)
        {
            ref CoreComponent core = ref whole.coreComponent[i];
            if (!core.Active) continue;

            UnitVAEventTag tags = whole.animationEventComponent[i].CrossedTags;
            if (tags == UnitVAEventTag.None) continue;

            ref AttackComponent attack = ref whole.attackComponent[i];

            // ── 近战出伤 ──
            if ((tags & UnitVAEventTag.MeleeHit) != 0)
            {
                TryMeleeHit(whole, i, ref core, ref attack);
            }

            // ── 远程发射 ──
            if ((tags & UnitVAEventTag.ProjectileSpawn) != 0)
            {
                TryProjectileSpawn(whole, i, ref core, ref attack);
            }
        }
    }

    private static void TryMeleeHit(WholeComponent whole, int entityIndex,
        ref CoreComponent core, ref AttackComponent attack)
    {
        EntityHandle targetHandle = EntitySystem.Instance.GetHandleFromId(attack.TargetEntityId);
        if (!EntitySystem.Instance.IsValid(targetHandle)) return;

        int targetIndex = EntitySystem.Instance.GetIndex(targetHandle);
        ref HealthComponent targetHealth = ref whole.healthComponent[targetIndex];
        if (!targetHealth.IsAlive) return;

        AttackSystem.Instance.ApplyDamage(whole, targetHandle, attack.AttackDamage, core.Team);
    }

    private static void TryProjectileSpawn(WholeComponent whole, int entityIndex,
        ref CoreComponent core, ref AttackComponent attack)
    {
        // Not a ranged unit — skip.
        if (attack.ProjectileSpriteId < 0) return;

        EntityHandle targetHandle = EntitySystem.Instance.GetHandleFromId(attack.TargetEntityId);
        if (!EntitySystem.Instance.IsValid(targetHandle)) return;

        int targetIndex = EntitySystem.Instance.GetIndex(targetHandle);
        ref HealthComponent targetHealth = ref whole.healthComponent[targetIndex];
        if (!targetHealth.IsAlive) return;

        // Re-use the existing projectile spawning logic from AttackSystem.
        SpawnProjectileFromUnit(whole, entityIndex, targetIndex, ref core, ref attack);
    }

    private static void SpawnProjectileFromUnit(WholeComponent whole, int attackerIndex, int targetIndex,
        ref CoreComponent attackerCore, ref AttackComponent attackerAtk)
    {
        ref CoreComponent targetCore = ref whole.coreComponent[targetIndex];

        EntityHandle bulletHandle = EntitySystem.Instance.CreateEntity(
            new Vector2Int(-999, -999),
            attackerCore.Team,
            UnitType.Projectile,
            Vector2Int.zero);

        if (bulletHandle == EntityHandle.None) return;
        int bulletIdx = EntitySystem.Instance.GetIndex(bulletHandle);

        ref CoreComponent bulletCore = ref whole.coreComponent[bulletIdx];
        bulletCore.Position = attackerCore.Position;
        bulletCore.VisualScale = new Vector2(0.4f, 0.4f);

        ref DrawComponent bulletDraw = ref whole.drawComponent[bulletIdx];
        bulletDraw.SpriteId = attackerAtk.ProjectileSpriteId;

        ref ProjectileComponent bulletProj = ref whole.projectileComponent[bulletIdx];
        bulletProj.SourceEntityId = attackerCore.SelfHandle.Id;
        bulletProj.TargetEntityId = targetCore.SelfHandle.Id;
        bulletProj.TargetPosition = targetCore.Position;
        bulletProj.Speed = attackerAtk.ProjectileSpeed > 0 ? attackerAtk.ProjectileSpeed : 12f;
        bulletProj.Damage = attackerAtk.AttackDamage;
        bulletProj.HitRadius = 0.4f;
        bulletProj.IsHoming = true;

        whole.moveComponent[bulletIdx].MoveIntervalTicks = -1;
        whole.aiComponent[bulletIdx].CurrentState = AIState.Idle;
    }
}
