using NekoGraph;

/// <summary>
/// Social 领域的 Pack 门面。
/// 统一收束社交系统常用的 PackID、目录路径和建包访问约定。
/// </summary>
public static class SocialPackFacade
{
    /// <summary>
    /// 玩家可访问的社交前台 Pack。
    /// 当前承担消息收件箱等可见 VFS 内容。
    /// </summary>
    public const string FrontendPackID = "social_tree_default";

    /// <summary>
    /// 社交前台消息目录。
    /// </summary>
    public const string MessagesFolder = "/messages/";

    /// <summary>
    /// 默认的社交后台故事 Pack。
    /// 当前先沿用已有主进程图，后续可再拆分成更细的 Story / Social Process 包。
    /// </summary>
    public const string DefaultBackendStoryPackID = "main_process";

    public static BasePackData GetFrontendPack(GraphAnalyser analyser, int subjectLevel)
    {
        return analyser?.GetPack(FrontendPackID, subjectLevel);
    }

    public static BasePackData EnsureFrontendPack(GraphAnalyser analyser, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        return analyser?.EnsurePack(FrontendPackID, subjectLevel);
    }

    public static BasePackData GetBackendStoryPack(GraphAnalyser analyser, string packID = null, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        string resolvedPackID = string.IsNullOrWhiteSpace(packID) ? DefaultBackendStoryPackID : packID;
        return analyser?.GetPack(resolvedPackID, subjectLevel);
    }

    public static string ResolveMessagePath(string messageFileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(messageFileNameOrPath))
            return MessagesFolder;

        if (messageFileNameOrPath.StartsWith("/"))
            return messageFileNameOrPath;

        return VFSPathResolver.Combine(MessagesFolder, messageFileNameOrPath);
    }

    public static string BuildMessageFilePath(string messageKey)
    {
        return ResolveMessagePath($"{messageKey}.msg");
    }
}
