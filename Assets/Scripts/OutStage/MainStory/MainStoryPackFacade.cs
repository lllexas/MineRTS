using System;
using NekoGraph;

/// <summary>
/// Main Story 领域的 Pack 门面实例。
/// </summary>
[Serializable]
public sealed class MainStoryPackFacade : PackFacadeBase
{
    public const string DefaultStoryPackID = "main_story";

    protected override string GetDefaultPackID() => DefaultStoryPackID;

    public BasePackData GetStoryPack(GraphAnalyser analyser, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        return analyser?.GetPack(ResolvedPackID, subjectLevel);
    }
}
