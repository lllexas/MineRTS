using NekoGraph;

/// <summary>
/// 兼容壳。
/// 新代码请改用 SocialBoxFacade。
/// </summary>
[System.Obsolete("Use SocialBoxFacade instead. This type is kept only as a compatibility alias.")]
public static class SocialPackFacade
{
    public const string FrontendPackID = SocialBoxFacade.DefaultFrontendPackID;
    public const string MessagesFolder = SocialBoxFacade.MessagesFolder;
    public const string DefaultBackendStoryPackID = MainStoryPackFacade.DefaultStoryPackID;

    public static BasePackData GetFrontendPack(GraphAnalyser analyser, int subjectLevel)
    {
        return GraphHub.Instance?.GetFacade<SocialBoxFacade>()?.GetFrontendPack(analyser, subjectLevel);
    }

    public static BasePackData EnsureFrontendPack(GraphAnalyser analyser, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        return GraphHub.Instance?.GetFacade<SocialBoxFacade>()?.EnsureFrontendPack(analyser, subjectLevel);
    }

    public static BasePackData GetBackendStoryPack(GraphAnalyser analyser, string packID = null, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        return GraphHub.Instance?.GetFacade<SocialBoxFacade>()?.GetBackendStoryPack(analyser, packID, subjectLevel);
    }

    public static string ResolveMessagePath(string messageFileNameOrPath)
    {
        return GraphHub.Instance?.GetFacade<SocialBoxFacade>()?.ResolveMessagePath(messageFileNameOrPath)
               ?? messageFileNameOrPath;
    }

    public static string BuildMessageFilePath(string messageKey)
    {
        return GraphHub.Instance?.GetFacade<SocialBoxFacade>()?.BuildMessageFilePath(messageKey)
               ?? $"/messages/{messageKey}.msg";
    }
}
