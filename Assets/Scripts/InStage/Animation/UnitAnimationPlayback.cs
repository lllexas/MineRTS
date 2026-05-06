using UnityEngine;

public static class UnitAnimationPlayback
{
    /// <summary>
    /// Animation frame rate at which Spine data was baked. Used to convert
    /// TicksPerFrame (baked-frame count) into real-time seconds per animation frame.
    /// </summary>
    public const float BakeFrameRate = 60f;

    // -----------------------------------------------------------------------
    // Atlas animation path (billboard sprite-sheet)
    // -----------------------------------------------------------------------

    public static UnitAnimationFrameResult Evaluate(
        UnitAtlasAnimationSetSO animationSet,
        UnitAnimationIntent intent,
        ref UnitAnimationPlaybackState playback,
        float currentTime)
    {
        UnitAnimationStateId targetState = UnitAnimationArbiter.Resolve(intent, playback, animationSet);
        if (playback.CurrentState != targetState)
        {
            playback.Reset(targetState, currentTime, intent.FlipX);
        }
        else if (playback.LastAdvanceTime == 0f)
        {
            playback.Reset(targetState, currentTime, intent.FlipX);
        }
        else
        {
            playback.FlipX = intent.FlipX;
            AdvanceFrames(animationSet, ref playback, currentTime);
        }

        return new UnitAnimationFrameResult
        {
            State = playback.CurrentState,
            LocalFrame = playback.LocalFrame,
            FrameCoord = ResolveFrameCoord(animationSet, playback),
            FlipX = playback.FlipX
        };
    }

    private static void AdvanceFrames(UnitAtlasAnimationSetSO animationSet, ref UnitAnimationPlaybackState playback, float currentTime)
    {
        if (animationSet == null)
        {
            playback.LastAdvanceTime = currentTime;
            return;
        }

        if (!animationSet.TryGetClip(playback.CurrentState, out UnitAtlasClipDef clip))
        {
            playback.LastAdvanceTime = currentTime;
            return;
        }

        float deltaTime = Mathf.Max(0f, currentTime - playback.LastAdvanceTime);
        playback.LastAdvanceTime = currentTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        float totalTime = playback.FrameTimeRemainder + deltaTime;
        float frameDuration = Mathf.Max(1f / BakeFrameRate, clip.TicksPerFrame / BakeFrameRate);
        int frameAdvance = (int)(totalTime / frameDuration);
        playback.FrameTimeRemainder = totalTime % frameDuration;
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
    /// Step 1 — Resolve target animation state from the intent blackboard.
    /// If ActionLockState is active and the clip is still playing (non-looping,
    /// not yet at last frame), the lock overrides the interceptor chain.
    /// Death always breaks the lock (highest priority).
    /// </summary>
    public static UnitAnimationStateId ResolveVAState(
        in UnitAnimationIntent intent,
        in UnitAnimationPlaybackState playback)
    {
        // Death always breaks the action lock.
        if (intent.IsDead)
        {
            return UnitAnimationStateId.Death;
        }

        // Action lock: attack / work animations that must play through.
        if (playback.ActionLockState != UnitAnimationStateId.None)
        {
            return playback.ActionLockState;
        }

        return VAInterceptorChain.Resolve(intent, playback);
    }

    /// <summary>
    /// Step 2 — Apply state transition and advance frames using REAL TIME.
    /// Manages the ActionLockState (LockUntilComplete):
    ///   - Set on transition into a non-looping clip.
    ///   - Cleared when the clip reaches its last frame.
    ///   - Death breaks the lock (handled in ResolveVAState).
    /// </summary>
    public static void ApplyVAState(
        UnitAnimationStateId targetState,
        UnitVASO vaso,
        in UnitAnimationIntent intent,
        ref UnitAnimationPlaybackState playback,
        float currentTime,
        out int localFrame)
    {
        bool isStateChange = playback.CurrentState != targetState;

        // Clear action lock if state changed (e.g. lock released or broken).
        if (isStateChange && playback.ActionLockState != UnitAnimationStateId.None)
        {
            playback.ActionLockState = UnitAnimationStateId.None;
        }

        if (isStateChange || playback.LastAdvanceTime == 0f)
        {
            playback.Reset(targetState, currentTime, intent.FlipX);

            // Lock clips that must play through (e.g. attack, death).
            if (vaso != null && vaso.TryGetClip(targetState, out UnitVAClip clip) && clip.LockUntilComplete)
            {
                playback.ActionLockState = targetState;
            }
        }
        else
        {
            playback.FlipX = intent.FlipX;
            AdvanceVAFrames(vaso, ref playback, currentTime);

            // Release lock when clip reaches its last frame.
            if (playback.ActionLockState != UnitAnimationStateId.None)
            {
                if (vaso != null && vaso.TryGetClip(playback.CurrentState, out UnitVAClip currentClip))
                {
                    if (playback.LocalFrame >= Mathf.Max(1, currentClip.FrameCount) - 1)
                    {
                        playback.ActionLockState = UnitAnimationStateId.None;
                    }
                }
                else
                {
                    playback.ActionLockState = UnitAnimationStateId.None;
                }
            }
        }

        localFrame = playback.LocalFrame;
    }

    private static void AdvanceVAFrames(UnitVASO vaso, ref UnitAnimationPlaybackState playback, float currentTime)
    {
        if (vaso == null)
        {
            playback.LastAdvanceTime = currentTime;
            return;
        }

        if (!vaso.TryGetClip(playback.CurrentState, out UnitVAClip clip))
        {
            playback.LastAdvanceTime = currentTime;
            return;
        }

        float deltaTime = Mathf.Max(0f, currentTime - playback.LastAdvanceTime);
        playback.LastAdvanceTime = currentTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        float totalTime = playback.FrameTimeRemainder + deltaTime;
        float frameDuration = Mathf.Max(1f / BakeFrameRate, clip.TicksPerFrame / BakeFrameRate);
        int frameAdvance = (int)(totalTime / frameDuration);
        playback.FrameTimeRemainder = totalTime % frameDuration;
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
