using UnityEngine;

/// <summary>
/// Per-entity animation intent blackboard (BBBNexus PlayerRuntimeData equivalent).
///
/// Each ECS system PUSHES its own intent directly into animationIntentComponent[i]:
///   DeathSystem     → IsDead
///   AttackSystem    → WantsAttack  (only when actually performing an attack)
///   IndustrialSystem → WantsWork   (when WorkType != None)
///   MoveSystem      → WantsMove    (when MoveTimerTicks > 0)
///   MoveSystem      → FlipX        (from Rotation.x)
///
/// DrawSystem reads the blackboard at render time.
/// ResetAll is called at the end of EntitySystem.UpdateSystem to clear
/// frame-level intent flags (BBBNexus ResetIntent equivalent).
///
/// Note: IsDead survives ResetAll — DeathSystem manages it explicitly.
/// </summary>
public static class UnitAnimationIntentBridge
{
    /// <summary>
    /// Reset frame-level animation intent flags for all active entities.
    /// Called at end of EntitySystem.UpdateSystem (BBBNexus ResetIntent equivalent).
    /// Preserves IsDead (managed by DeathSystem).
    /// </summary>
    public static void ResetAll(WholeComponent whole)
    {
        if (whole?.animationIntentComponent == null) return;

        for (int i = 0; i < whole.entityCount; i++)
        {
            if (whole.coreComponent[i].Active)
            {
                // Preserve IsDead — it's a persistent state, not a frame-level flag.
                bool wasDead = whole.animationIntentComponent[i].IsDead;
                whole.animationIntentComponent[i] = default;
                whole.animationIntentComponent[i].IsDead = wasDead;
                whole.animationEventComponent[i] = default;
            }
        }
    }
}
