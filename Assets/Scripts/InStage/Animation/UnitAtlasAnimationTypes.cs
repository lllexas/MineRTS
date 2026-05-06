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
    public int TickRemainder;
    public long LastTick;
    public bool FlipX;

    public void Reset(UnitAnimationStateId state, long currentTick, bool flipX)
    {
        CurrentState = state;
        LocalFrame = 0;
        TickRemainder = 0;
        LastTick = currentTick;
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
