using System;
using System.Collections.Generic;

/// <summary>
/// Signature for an animation state interceptor.
/// Examines the unit's current intent and playback state. If the interceptor's
/// condition is met, it sets targetState and returns true. Otherwise returns false
/// and the chain continues to the next interceptor.
/// </summary>
public delegate bool VAInterceptorFunc(
    in UnitAnimationIntent intent,
    in UnitAnimationPlaybackState current,
    out UnitAnimationStateId targetState);

/// <summary>
/// Metadata describing one registered interceptor for editor introspection.
/// </summary>
[Serializable]
public struct VAInterceptorInfo
{
    public string Name;
    public int Priority;        // execution order (lower = higher priority)
    public bool Enabled;
    public string Description;

    [NonSerialized]
    public VAInterceptorFunc Func;
}

/// <summary>
/// Ordered chain of animation state interceptors.
/// All interceptors are static delegates — no per-unit allocation.
/// The chain runs in priority order (lowest priority value first).
/// The first interceptor that returns true determines the target state.
///
/// Usage (per entity, per frame):
///   targetState = VAInterceptorChain.Resolve(intent, playback);
///
/// The last interceptor (Idle) is the fallback — it always returns true.
/// </summary>
public static class VAInterceptorChain
{
    private static readonly List<VAInterceptorInfo> _interceptors = new List<VAInterceptorInfo>(8);
    private static bool _initialized;

    /// <summary>
    /// Exposed for editor introspection.
    /// </summary>
    public static IReadOnlyList<VAInterceptorInfo> Interceptors
    {
        get
        {
            EnsureInitialized();
            return _interceptors;
        }
    }

    /// <summary>
    /// Last-hit interceptor index per resolve call. For debug display.
    /// Set by Resolve(); read by debugger before next Resolve() overwrites it.
    /// </summary>
    public static int LastHitInterceptorIndex { get; private set; } = -1;
    public static string LastHitInterceptorName { get; private set; } = string.Empty;

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Run the chain. Returns the resolved target state.
    /// </summary>
    public static UnitAnimationStateId Resolve(
        in UnitAnimationIntent intent,
        in UnitAnimationPlaybackState current)
    {
        EnsureInitialized();

        for (int i = 0; i < _interceptors.Count; i++)
        {
            VAInterceptorInfo info = _interceptors[i];
            if (!info.Enabled || info.Func == null)
            {
                continue;
            }

            if (info.Func(intent, current, out UnitAnimationStateId target))
            {
                LastHitInterceptorIndex = i;
                LastHitInterceptorName = info.Name;
                return target;
            }
        }

        // Absolute fallback (shouldn't reach here if Idle interceptor is last and always-on).
        LastHitInterceptorIndex = _interceptors.Count - 1;
        LastHitInterceptorName = "Idle (fallback)";
        return UnitAnimationStateId.Idle;
    }

    /// <summary>
    /// Enable or disable a named interceptor at runtime (for debug toggling).
    /// </summary>
    public static void SetEnabled(string name, bool enabled)
    {
        EnsureInitialized();
        for (int i = 0; i < _interceptors.Count; i++)
        {
            if (_interceptors[i].Name == name)
            {
                var info = _interceptors[i];
                info.Enabled = enabled;
                _interceptors[i] = info;
                return;
            }
        }
    }

    /// <summary>
    /// Re-register the default chain. Safe to call multiple times.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _interceptors.Clear();

        Register("Death",  0, "Unit is dead or health <= 0",          DeathInterceptor.TryIntercept);
        Register("Stun",   1, "Unit is stunned (status effect)",       StunInterceptor.TryIntercept);
        Register("Attack", 2, "Unit has an active attack target",      AttackInterceptor.TryIntercept);
        Register("Work",   3, "Unit is performing work",               WorkInterceptor.TryIntercept);
        Register("Move",   4, "Unit is moving between tiles",          MoveInterceptor.TryIntercept);
        Register("Idle",   5, "Fallback — always active",              IdleInterceptor.TryIntercept);

        _initialized = true;
    }

    private static void Register(string name, int priority, string description, VAInterceptorFunc func)
    {
        _interceptors.Add(new VAInterceptorInfo
        {
            Name = name,
            Priority = priority,
            Enabled = true,
            Description = description,
            Func = func
        });
    }

    // ------------------------------------------------------------------
    // Interceptor implementations
    // ------------------------------------------------------------------

    private static class DeathInterceptor
    {
        public static bool TryIntercept(
            in UnitAnimationIntent intent,
            in UnitAnimationPlaybackState current,
            out UnitAnimationStateId target)
        {
            if (intent.IsDead)
            {
                target = UnitAnimationStateId.Death;
                return true;
            }

            target = default;
            return false;
        }
    }

    private static class StunInterceptor
    {
        public static bool TryIntercept(
            in UnitAnimationIntent intent,
            in UnitAnimationPlaybackState current,
            out UnitAnimationStateId target)
        {
            // Stun intent is not yet wired into UnitAnimationIntentBridge.
            // Placeholder — always passes through.
            target = default;
            return false;
        }
    }

    private static class AttackInterceptor
    {
        public static bool TryIntercept(
            in UnitAnimationIntent intent,
            in UnitAnimationPlaybackState current,
            out UnitAnimationStateId target)
        {
            if (intent.WantsAttack)
            {
                target = UnitAnimationStateId.Attack;
                return true;
            }

            target = default;
            return false;
        }
    }

    private static class WorkInterceptor
    {
        public static bool TryIntercept(
            in UnitAnimationIntent intent,
            in UnitAnimationPlaybackState current,
            out UnitAnimationStateId target)
        {
            if (intent.WantsWork)
            {
                target = UnitAnimationStateId.Work;
                return true;
            }

            target = default;
            return false;
        }
    }

    private static class MoveInterceptor
    {
        public static bool TryIntercept(
            in UnitAnimationIntent intent,
            in UnitAnimationPlaybackState current,
            out UnitAnimationStateId target)
        {
            if (intent.WantsMove)
            {
                target = UnitAnimationStateId.Move;
                return true;
            }

            target = default;
            return false;
        }
    }

    private static class IdleInterceptor
    {
        public static bool TryIntercept(
            in UnitAnimationIntent intent,
            in UnitAnimationPlaybackState current,
            out UnitAnimationStateId target)
        {
            target = UnitAnimationStateId.Idle;
            return true;
        }
    }
}
