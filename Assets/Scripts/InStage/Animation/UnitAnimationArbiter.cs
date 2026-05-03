public static class UnitAnimationArbiter
{
    public static UnitAnimationStateId Resolve(UnitAnimationIntent intent, UnitAnimationPlaybackState playback, UnitAtlasAnimationSetSO animationSet)
    {
        if (intent.IsDead)
        {
            return UnitAnimationStateId.Death;
        }

        if (IsLocked(playback, animationSet))
        {
            return playback.CurrentState;
        }

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

    public static bool IsLocked(UnitAnimationPlaybackState playback, UnitAtlasAnimationSetSO animationSet)
    {
        if (animationSet == null)
        {
            return false;
        }

        if (!animationSet.TryGetClip(playback.CurrentState, out UnitAtlasClipDef clip))
        {
            return false;
        }

        int frameCount = clip.Frames?.Length ?? 0;
        return clip.LockUntilComplete && frameCount > 0 && playback.LocalFrame < frameCount - 1;
    }
}
