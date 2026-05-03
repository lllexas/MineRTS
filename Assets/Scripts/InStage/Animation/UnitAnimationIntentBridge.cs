using UnityEngine;

public static class UnitAnimationIntentBridge
{
    public static UnitAnimationIntent Build(WholeComponent whole, int entityIndex)
    {
        ref CoreComponent core = ref whole.coreComponent[entityIndex];
        ref MoveComponent move = ref whole.moveComponent[entityIndex];
        ref WorkComponent work = ref whole.workComponent[entityIndex];
        ref AttackComponent attack = ref whole.attackComponent[entityIndex];
        ref HealthComponent health = ref whole.healthComponent[entityIndex];

        bool wantsMove = move.LogicalPosition != move.PreviousLogicalPosition || move.Timer > 0f;
        bool wantsWork = work.WorkType != WorkType.None;
        bool wantsAttack = attack.TargetEntityId != 0 && attack.TargetEntityId != -1;
        bool flipX = core.Rotation.x < 0;

        return new UnitAnimationIntent
        {
            IsDead = !health.IsAlive || health.Health <= 0f,
            WantsMove = wantsMove,
            WantsWork = wantsWork,
            WantsAttack = wantsAttack,
            FlipX = flipX
        };
    }
}
