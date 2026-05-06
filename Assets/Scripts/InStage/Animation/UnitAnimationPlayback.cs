using UnityEngine;

public static class UnitAnimationPlayback
{
    public static UnitAnimationFrameResult Evaluate(
        UnitAtlasAnimationSetSO animationSet,
        UnitAnimationIntent intent,
        ref UnitAnimationPlaybackState playback,
        long currentTick)
    {
        UnitAnimationStateId targetState = UnitAnimationArbiter.Resolve(intent, playback, animationSet);
        if (playback.CurrentState != targetState)
        {
            playback.Reset(targetState, currentTick, intent.FlipX);
        }
        else if (playback.LastTick == 0)
        {
            playback.Reset(targetState, currentTick, intent.FlipX);
        }
        else
        {
            playback.FlipX = intent.FlipX;
            AdvanceFrames(animationSet, ref playback, currentTick);
        }

        return new UnitAnimationFrameResult
        {
            State = playback.CurrentState,
            LocalFrame = playback.LocalFrame,
            FrameCoord = ResolveFrameCoord(animationSet, playback),
            FlipX = playback.FlipX
        };
    }

    private static void AdvanceFrames(UnitAtlasAnimationSetSO animationSet, ref UnitAnimationPlaybackState playback, long currentTick)
    {
        if (animationSet == null)
        {
            playback.LastTick = currentTick;
            return;
        }

        if (!animationSet.TryGetClip(playback.CurrentState, out UnitAtlasClipDef clip))
        {
            playback.LastTick = currentTick;
            return;
        }

        long deltaTickLong = Mathf.Max(0, (int)(currentTick - playback.LastTick));
        playback.LastTick = currentTick;
        if (deltaTickLong <= 0)
        {
            return;
        }

        int accumulatedTicks = playback.TickRemainder + (int)deltaTickLong;
        if (clip.TicksPerFrame <= 0)
        {
            clip.TicksPerFrame = 1;
        }

        int frameAdvance = accumulatedTicks / clip.TicksPerFrame;
        playback.TickRemainder = accumulatedTicks % clip.TicksPerFrame;
        if (frameAdvance <= 0)
        {
            return;
        }

        if (clip.Loop)
        {
            int frameCount = Mathf.Max(1, clip.Frames?.Length ?? 0);
            playback.LocalFrame = (playback.LocalFrame + frameAdvance) % frameCount;
            return;
        }

        playback.LocalFrame = Mathf.Min(playback.LocalFrame + frameAdvance, Mathf.Max(0, (clip.Frames?.Length ?? 1) - 1));
    }

    private static AtlasFrameCoord ResolveFrameCoord(UnitAtlasAnimationSetSO animationSet, UnitAnimationPlaybackState playback)
    {
        if (animationSet == null)
        {
            return default;
        }

        if (!animationSet.TryGetClip(playback.CurrentState, out UnitAtlasClipDef clip))
        {
            return default;
        }

        int frameCount = clip.Frames?.Length ?? 0;
        if (frameCount <= 0)
        {
            return default;
        }

        int localFrame = Mathf.Clamp(playback.LocalFrame, 0, frameCount - 1);
        return clip.Frames[localFrame];
    }

    // -----------------------------------------------------------------------
    // VA (vertex animation) evaluation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Evaluate animation for a VA-enabled entity.
    /// Uses the same intent → state resolution as the atlas path
    /// (Death > Attack > Work > Move > Idle), but resolves frames against
    /// UnitVASO clips instead of atlas clip defs.
    /// Returns local frame and state; the caller resolves the global buffer offset.
    /// </summary>
    public static UnitAnimationFrameVAResult EvaluateVA(
        UnitVASO vaso,
        UnitAnimationIntent intent,
        ref UnitAnimationPlaybackState playback,
        long currentTick)
    {
        UnitAnimationStateId targetState = ResolveVAState(intent, ref playback, vaso);

        if (playback.CurrentState != targetState || playback.LastTick == 0)
        {
            playback.Reset(targetState, currentTick, intent.FlipX);
        }
        else
        {
            playback.FlipX = intent.FlipX;
            AdvanceVAFrames(vaso, ref playback, currentTick);
        }

        return new UnitAnimationFrameVAResult
        {
            State = playback.CurrentState,
            LocalFrame = playback.LocalFrame,
            FlipX = playback.FlipX
        };
    }

    private static UnitAnimationStateId ResolveVAState(
        UnitAnimationIntent intent,
        ref UnitAnimationPlaybackState playback,
        UnitVASO vaso)
    {
        if (intent.IsDead)
        {
            return UnitAnimationStateId.Death;
        }

        // Note: UnitVAClip does not have LockUntilComplete.
        // Interrupt/lock handling is deferred to a higher-level state machine.

        if (intent.WantsAttack)
        {
            return UnitAnimationStateId.Attack;
        }

        if (intent.WantsWork)
        {
            return UnitAnimationStateId.Work;
        }

        if (intent.WantsMove)
        {
            return UnitAnimationStateId.Move;
        }

        return UnitAnimationStateId.Idle;
    }

    private static void AdvanceVAFrames(UnitVASO vaso, ref UnitAnimationPlaybackState playback, long currentTick)
    {
        if (vaso == null)
        {
            playback.LastTick = currentTick;
            return;
        }

        if (!vaso.TryGetClip(playback.CurrentState, out UnitVAClip clip))
        {
            playback.LastTick = currentTick;
            return;
        }

        long deltaTickLong = Mathf.Max(0, (int)(currentTick - playback.LastTick));
        playback.LastTick = currentTick;
        if (deltaTickLong <= 0)
        {
            return;
        }

        int accumulatedTicks = playback.TickRemainder + (int)deltaTickLong;
        int ticksPerFrame = Mathf.Max(1, clip.TicksPerFrame);
        int frameAdvance = accumulatedTicks / ticksPerFrame;
        playback.TickRemainder = accumulatedTicks % ticksPerFrame;
        if (frameAdvance <= 0)
        {
            return;
        }

        if (clip.Loop)
        {
            int frameCount = Mathf.Max(1, clip.FrameCount);
            playback.LocalFrame = (playback.LocalFrame + frameAdvance) % frameCount;
            return;
        }

        playback.LocalFrame = Mathf.Min(playback.LocalFrame + frameAdvance, Mathf.Max(0, clip.FrameCount - 1));
    }
}
