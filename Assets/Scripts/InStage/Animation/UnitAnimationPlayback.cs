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
}
