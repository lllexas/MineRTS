#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAL
{
[Serializable]
public sealed class DialogSequenceEffectAnchorRecord
{
    public string EffectType;
    public string EffectFingerprint;
    public int FingerprintOccurrence;
    public string AnchorSpeaker;
    public string AnchorContentPreview;
    public string AnchorSignature;
    public int AnchorOccurrence;
    public int FallbackBucket;
    public int OriginalOrder;
}

public sealed class DialogSequenceAnchorMeta : ScriptableObject
{
    public string SequenceMetaId;
    public string SequenceAssetGuid;
    public List<DialogSequenceEffectAnchorRecord> Records = new();
}
}
#endif
