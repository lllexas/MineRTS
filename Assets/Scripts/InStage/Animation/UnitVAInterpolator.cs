using UnityEngine;

/// <summary>
/// Computes sub-frame interpolation between two vertex animation frames.
///
/// Game logic runs at 10 ticks/sec. Display frames run at variable FPS
/// (typically 60–144 Hz). Animation frames are baked at 60 FPS and
/// advanced by real wall-clock time (Time.time), not game ticks.
///
/// Even with real-time frame advancement, display frames will rarely
/// land exactly on animation frame boundaries. The interpolator uses
/// FrameTimeRemainder (carry-over seconds since last frame advance)
/// to compute how far we are through the current animation frame,
/// then outputs two neighbouring frames and a blend weight (0→1)
/// for the shader to lerp.
///
/// BBBNexus equivalent: MovementParameterProcessor computes AnimBlendX/Y
/// for the blend tree; we compute a simpler 1D blend between two
/// consecutive VA frames.
/// </summary>
public static class UnitVAInterpolator
{
    /// <summary>
    /// Result of interpolation computation: two local frames and a blend weight.
    /// frameA is always the current integer frame. frameB is the next frame
    /// (wrapped for looping clips, clamped for non-looping). BlendWeight 0 means
    /// "all frameA"; 1 means "all frameB".
    /// </summary>
    public struct VAInterpolationResult
    {
        public int LocalFrameA;
        public int LocalFrameB;
        public float BlendWeight;
    }

    /// <summary>
    /// Compute interpolation parameters for the current entity.
    /// Must be called AFTER ApplyVAState has advanced the playback state
    /// (so FrameTimeRemainder and LocalFrame are up to date).
    /// </summary>
    public static VAInterpolationResult Compute(
        UnitVASO vaso,
        in UnitAnimationPlaybackState playback,
        UnitAnimationStateId currentState)
    {
        // Guard: no VASO or clip → no interpolation.
        if (vaso == null || !vaso.TryGetClip(currentState, out UnitVAClip clip))
        {
            return new VAInterpolationResult
            {
                LocalFrameA = playback.LocalFrame,
                LocalFrameB = playback.LocalFrame,
                BlendWeight = 0f
            };
        }

        int frameCount = Mathf.Max(1, clip.FrameCount);

        // FrameDuration = how many seconds each animation frame lasts.
        // TicksPerFrame / 60: "ticks" are now 1/60-second baked-frame units.
        float frameDuration = Mathf.Max(1f / UnitAnimationPlayback.BakeFrameRate,
                                        clip.TicksPerFrame / UnitAnimationPlayback.BakeFrameRate);

        // Blend factor from real-time remainder within the current animation frame.
        float blendFactor = Mathf.Clamp01(playback.FrameTimeRemainder / frameDuration);

        // First-ever frame of a fresh playback → no interpolation.
        if (playback.LastAdvanceTime == 0f)
        {
            blendFactor = 0f;
        }

        int frameA = playback.LocalFrame;
        int frameB;

        if (clip.Loop)
        {
            // Looping: wrap around to frame 0.
            frameB = (frameA + 1) % frameCount;
        }
        else
        {
            // Non-looping: clamp at the last frame.
            if (frameA >= frameCount - 1)
            {
                frameB = frameA;
                blendFactor = 0f;
            }
            else
            {
                frameB = frameA + 1;
            }
        }

        return new VAInterpolationResult
        {
            LocalFrameA = frameA,
            LocalFrameB = frameB,
            BlendWeight = blendFactor
        };
    }
}
