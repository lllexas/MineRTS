using System;
using UnityEngine;

public enum UnitAnimationStateId
{
    None = 0,
    Idle = 1,
    Move = 2,
    Work = 3,
    Attack = 4,
    Death = 5,
    Stun = 6
}

public enum UnitAnimationFrameTier
{
    Small64 = 64,
    Medium128 = 128,
    Large256 = 256
}

[Serializable]
public struct UnitAnimationIntent
{
    public bool IsDead;
    public bool WantsMove;
    public bool WantsWork;
    public bool WantsAttack;
    public bool FlipX;
}

[Serializable]
public struct AtlasFrameCoord
{
    public int Row;
    public int Col;

    public AtlasFrameCoord(int row, int col)
    {
        Row = row;
        Col = col;
    }
}

[Serializable]
public struct UnitAtlasClipDef
{
    public UnitAnimationStateId State;
    public AtlasFrameCoord[] Frames;
    [Min(1)] public int TicksPerFrame;
    public bool Loop;
    public bool LockUntilComplete;
}

[Serializable]
public struct UnitAnimationPlaybackState
{
    public UnitAnimationStateId CurrentState;
    public int LocalFrame;

    /// <summary>
    /// Fractional seconds carried over since the last frame advance.
    /// Replaces the old tick-based TickRemainder for real-time animation.
    /// </summary>
    public float FrameTimeRemainder;

    /// <summary>
    /// Time.time of the last frame advance. Used to compute real-time delta
    /// between display frames, decoupled from game-logic ticks.
    /// </summary>
    public float LastAdvanceTime;

    public bool FlipX;

    /// <summary>
    /// BBBNexus LockUntilComplete: when set to a non-None state, the interceptor
    /// chain is bypassed and this state persists until the clip reaches its last
    /// frame. Used for attack / death / stun animations that must play through.
    /// Death always breaks the lock.
    /// </summary>
    public UnitAnimationStateId ActionLockState;

    /// <summary>
    /// Reset the playback to the first frame of a new state.
    /// currentTime should be Time.time (real wall-clock time, not game tick).
    /// Preserves ActionLockState (caller manages it).
    /// </summary>
    public void Reset(UnitAnimationStateId state, float currentTime, bool flipX)
    {
        CurrentState = state;
        LocalFrame = 0;
        FrameTimeRemainder = 0f;
        LastAdvanceTime = currentTime;
        FlipX = flipX;
    }
}

[Serializable]
public struct UnitAnimationFrameResult
{
    public UnitAnimationStateId State;
    public int LocalFrame;
    public AtlasFrameCoord FrameCoord;
    public bool FlipX;
}

/// <summary>
/// VA-style animation evaluation result.
/// LocalFrame is the frame index within the resolved clip.
/// The global buffer offset is resolved later by UnitVABufferManager.TryGetGlobalFrameIndex.
/// </summary>
[Serializable]
public struct UnitAnimationFrameVAResult
{
    public UnitAnimationStateId State;
    public int LocalFrame;
    public bool FlipX;
}
