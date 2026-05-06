using System;

/// <summary>
/// Per-entity ECS component: frame tags crossed during VA animation playback this frame.
///
/// Written by DrawSystem.TryEnqueueVA when AdvanceVAFrames crosses a tagged frame.
/// Cleared at end of EntitySystem.UpdateSystem.
/// Read by downstream ECS systems (attack, audio, vfx) for deterministic event handling.
/// </summary>
[Serializable]
public struct AnimationEventComponent
{
    public UnitVAEventTag CrossedTags;
}
