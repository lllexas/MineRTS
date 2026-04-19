using System;
using System.Collections.Generic;
using NekoGraph;
using UnityEngine;

/// <summary>
/// 社交邮箱领域门面。
/// 对业务层暴露“联系人盒子 / 消息盒子”的语义入口，避免业务代码直接散落完整路径。
/// </summary>
[Serializable]
public sealed class SocialBoxFacade : PackFacadeBase
{
    public const string DefaultFrontendPackID = "social_tree_default";
    public const string ContactsFolder = "/contacts/";
    public const string MessagesFolder = "/messages/";

    protected override string GetDefaultPackID() => DefaultFrontendPackID;

    public BasePackData GetFrontendPack(GraphAnalyser analyser, int subjectLevel)
    {
        return analyser?.GetPack(ResolvedPackID, subjectLevel);
    }

    public BasePackData EnsureFrontendPack(
        GraphAnalyser analyser,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        var pack = analyser?.EnsurePack(ResolvedPackID, subjectLevel);
        if (pack == null)
            return null;

        EnsureBoxRoots(analyser, subjectLevel);
        return pack;
    }

    public BasePackData GetBackendStoryPack(
        GraphAnalyser analyser,
        string packID = null,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        var mainStoryFacade = GraphHub.Instance?.GetFacade<MainStoryPackFacade>();
        string resolvedPackID = string.IsNullOrWhiteSpace(packID)
            ? (mainStoryFacade?.ResolvedPackID ?? MainStoryPackFacade.DefaultStoryPackID)
            : packID;
        return analyser?.GetPack(resolvedPackID, subjectLevel);
    }

    public bool EnsureBoxRoots(
        GraphAnalyser analyser,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null)
            return false;

        var pack = analyser.EnsurePack(ResolvedPackID, subjectLevel);
        if (pack == null)
            return false;

        bool contactsOk = analyser.CreateDirectory(ResolvedPackID, ContactsFolder, subjectLevel);
        bool messagesOk = analyser.CreateDirectory(ResolvedPackID, MessagesFolder, subjectLevel);
        return contactsOk && messagesOk;
    }

    public bool EnsureContactsRoot(
        GraphAnalyser analyser,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        return analyser != null
            && analyser.EnsurePack(ResolvedPackID, subjectLevel) != null
            && analyser.CreateDirectory(ResolvedPackID, ContactsFolder, subjectLevel);
    }

    public bool EnsureMessagesRoot(
        GraphAnalyser analyser,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        return analyser != null
            && analyser.EnsurePack(ResolvedPackID, subjectLevel) != null
            && analyser.CreateDirectory(ResolvedPackID, MessagesFolder, subjectLevel);
    }

    public string ResolveContactPath(string contactKeyOrPath)
    {
        if (string.IsNullOrWhiteSpace(contactKeyOrPath))
            return ContactsFolder;

        if (contactKeyOrPath.StartsWith("/"))
            return VFSPathResolver.Normalize(contactKeyOrPath);

        return VFSPathResolver.Combine(ContactsFolder, contactKeyOrPath);
    }

    public string BuildContactDirectoryPath(string contactKey)
    {
        return ResolveContactPath(contactKey);
    }

    public VFSNodeData GetContactBox(
        GraphAnalyser analyser,
        string contactKeyOrPath,
        int subjectLevel)
    {
        return analyser?.GetNode(
            ResolvedPackID,
            ResolveContactPath(contactKeyOrPath),
            subjectLevel) as VFSNodeData;
    }

    public VFSNodeData GetOrCreateContactBox(
        GraphAnalyser analyser,
        string contactKey,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null || string.IsNullOrWhiteSpace(contactKey))
            return null;

        if (!EnsureContactsRoot(analyser, subjectLevel))
            return null;

        string path = BuildContactDirectoryPath(contactKey);
        if (!analyser.PathExists(ResolvedPackID, path, subjectLevel) &&
            !analyser.CreateDirectory(ResolvedPackID, path, subjectLevel))
        {
            return null;
        }

        return analyser.GetNode(ResolvedPackID, path, subjectLevel) as VFSNodeData;
    }

    public List<VFSNodeData> ListContacts(
        GraphAnalyser analyser,
        int subjectLevel)
    {
        return FilterVfsNodes(analyser?.GetChildren(ResolvedPackID, ContactsFolder, subjectLevel));
    }

    public string ResolveMessagePath(string messageFileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(messageFileNameOrPath))
            return MessagesFolder;

        if (messageFileNameOrPath.StartsWith("/"))
            return VFSPathResolver.Normalize(messageFileNameOrPath);

        return VFSPathResolver.Combine(MessagesFolder, messageFileNameOrPath);
    }

    public string BuildMessageFilePath(string messageKey)
    {
        return ResolveMessagePath($"{messageKey}.msg");
    }

    public VFSNodeData GetMessageNode(
        GraphAnalyser analyser,
        string messageFileNameOrPath,
        int subjectLevel)
    {
        return analyser?.GetNode(
            ResolvedPackID,
            ResolveMessagePath(messageFileNameOrPath),
            subjectLevel) as VFSNodeData;
    }

    public List<VFSNodeData> ListMessageNodes(
        GraphAnalyser analyser,
        int subjectLevel)
    {
        return FilterVfsNodes(analyser?.GetChildren(ResolvedPackID, MessagesFolder, subjectLevel));
    }

    public bool WriteMessage(
        GraphAnalyser analyser,
        string messageFileNameOrPath,
        string content,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null)
            return false;

        if (!EnsureMessagesRoot(analyser, subjectLevel))
            return false;

        return analyser.WriteFile(
            ResolvedPackID,
            ResolveMessagePath(messageFileNameOrPath),
            content,
            subjectLevel);
    }

    public bool DeleteMessage(
        GraphAnalyser analyser,
        string messageFileNameOrPath,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        return analyser != null
            && analyser.Delete(ResolvedPackID, ResolveMessagePath(messageFileNameOrPath), subjectLevel);
    }

    public bool SwapMessages(
        GraphAnalyser analyser,
        string messageAFileNameOrPath,
        string messageBFileNameOrPath,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        return analyser != null
            && analyser.SwapNodes(
                ResolvedPackID,
                ResolveMessagePath(messageAFileNameOrPath),
                ResolveMessagePath(messageBFileNameOrPath),
                subjectLevel);
    }

    public bool TryDeliverMessageCopy(
        GraphAnalyser analyser,
        string sourcePackID,
        string sourceNodeID,
        string deliveryKey,
        out string deliveredPath,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        deliveredPath = null;
        if (analyser == null || string.IsNullOrWhiteSpace(sourcePackID) || string.IsNullOrWhiteSpace(sourceNodeID))
            return false;

        var sourcePack = analyser.GetPack(sourcePackID, subjectLevel);
        if (sourcePack == null || sourcePack.Nodes == null)
            return false;

        if (!sourcePack.Nodes.TryGetValue(sourceNodeID, out var sourceNodeData) || sourceNodeData is not VFSNodeData sourceNode)
            return false;

        return TryDeliverMessageCopy(
            analyser,
            sourcePackID,
            sourceNodeID,
            deliveryKey,
            sourceNode,
            out deliveredPath,
            subjectLevel);
    }

    public bool TryDeliverMessageCopy(
        GraphAnalyser analyser,
        string sourcePackID,
        string sourceNodeID,
        string signalId,
        VFSNodeData sourceNode,
        out string deliveredPath,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        deliveredPath = null;
        if (analyser == null || sourceNode == null || sourceNode.IsDirectory)
            return false;

        if (!string.Equals(sourceNode.Extension, ".msg", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[social_box] reject-deliver sourcePack={sourcePackID} node={sourceNode.NodeID} extension={sourceNode.Extension}");
            return false;
        }

        if (!EnsureMessagesRoot(analyser, subjectLevel))
            return false;

        deliveredPath = BuildDeliveredMessageFilePath(sourceNode);
        if (!analyser.WriteFile(ResolvedPackID, deliveredPath, string.Empty, subjectLevel))
        {
            Debug.LogWarning($"[social_box] deliver-create-failed targetPack={ResolvedPackID} targetPath={deliveredPath}");
            return false;
        }

        if (analyser.GetNode(ResolvedPackID, deliveredPath, subjectLevel) is not VFSNodeData deliveredNode)
        {
            Debug.LogWarning($"[social_box] deliver-resolve-failed targetPack={ResolvedPackID} targetPath={deliveredPath}");
            return false;
        }

        CopyFileNode(sourceNode, deliveredNode);
        deliveredNode.InlineText = VFSMsgReplicaMeta.Serialize(new VFSMsgReplicaMeta
        {
            BackendPackID = sourcePackID,
            BackendNodeID = sourceNodeID,
            SignalId = signalId,
            IsResolved = false,
            ChoiceTargetNodeIDs = sourceNode.ChildNodeIDs != null
                ? new List<string>(sourceNode.ChildNodeIDs)
                : new List<string>()
        });

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[social_box] deliver-msg sourcePack={0} sourceNode={1} targetPack={2} targetPath={3}",
            sourcePackID,
            sourceNode.NodeID,
            ResolvedPackID,
            deliveredPath);
        return true;
    }

    public string BuildDeliveredMessageFilePath(VFSNodeData sourceNode)
    {
        string baseName = sourceNode == null || string.IsNullOrWhiteSpace(sourceNode.Name)
            ? "msg"
            : sourceNode.Name;

        return BuildMessageFilePath(baseName);
    }

    private static List<VFSNodeData> FilterVfsNodes(List<BaseNodeData> nodes)
    {
        var result = new List<VFSNodeData>();
        if (nodes == null)
            return result;

        foreach (var node in nodes)
        {
            if (node is VFSNodeData vfsNode)
                result.Add(vfsNode);
        }

        return result;
    }

    private static void CopyFileNode(VFSNodeData sourceNode, VFSNodeData targetNode)
    {
        if (sourceNode == null || targetNode == null)
            return;

        targetNode.Name = sourceNode.Name;
        targetNode.Extension = sourceNode.Extension;
        targetNode.ContentKind = sourceNode.ContentKind;
        targetNode.ContentSource = sourceNode.ContentSource;
        targetNode.InlineText = sourceNode.InlineText;
        targetNode.ReferencePath = sourceNode.ReferencePath;
        targetNode.AssetGuid = sourceNode.AssetGuid;
        targetNode.AssetPath = sourceNode.AssetPath;
        targetNode.UnityObjectTypeName = sourceNode.UnityObjectTypeName;
        targetNode.IsEnabled = sourceNode.IsEnabled;
        targetNode.Description = sourceNode.Description;
        targetNode.MimeType = sourceNode.MimeType;
    }
}
