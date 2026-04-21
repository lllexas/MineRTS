using UnityEngine;
using NekoGraph;
using Newtonsoft.Json;
using System.Collections.Generic;
using SpaceTUI;

/// <summary>
/// vfs.msg 资源驱动。
/// 这是对旧 MsgStrategy 的另起炉灶版本：先把“消息是什么”定义清楚，
/// 再决定 Execute 和 Query 分别如何服务运行态与显示态。
/// </summary>
[VFSResource(".msg", typeof(VFSMsgSO))]
public static class VFSMsgResource
{
    [VFSExecute]
    public static HandleResult Execute(
        VFSResolvedContent content,
        SignalContext context,
        BasePackData pack,
        GraphRunner runner,
        string packIDKey,
        System.Action continueAction)
    {
        var msg = content.GetUnityObject<VFSMsgSO>();
        if (msg == null)
        {
            Debug.LogError("[VFSMsgResource] Execute 失败：VFSMsgSO 为 null");
            return HandleResult.Error;
        }

        var socialBox = GraphHub.Instance?.GetFacade<SocialBoxFacade>();
        var analyser = GraphHub.Instance?.DefaultAnalyser;
        string deliveredPath = null;

        if (socialBox == null || analyser == null || pack == null || context == null)
        {
            Debug.LogWarning("[VFSMsgResource] Execute 跳过投递：SocialBoxFacade / Analyser / Pack / Context 缺失");
        }
        else
        {
            var sourceNode = content?.Node;
            bool delivered = socialBox.TryDeliverMessageCopy(
                analyser,
                pack.PackID,
                sourceNode,
                context.SignalId,
                out deliveredPath,
                PackAccessSubjects.SystemMin);

            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "[vfs_msg] execute-deliver delivered={0} sourcePack={1} sourceNode={2} signal={3} targetPath={4}",
                delivered,
                pack.PackID,
                context.CurrentNodeId,
                context.SignalId,
                deliveredPath ?? "(null)");
        }

        PostSystem.Instance.Send("VFS.Msg.Execute", new VFSMsgQueryPayload
        {
            Message = msg,
            PackID = pack?.PackID,
            PackIDKey = packIDKey,
            SourceNodeId = context?.CurrentNodeId,
            SignalId = context?.SignalId,
            VfsPath = deliveredPath
        });

        return HandleResult.Wait;
    }

    [VFSQuery]
    public static VFSQueryResult Query(VFSResolvedContent content, VFSQueryContext context)
    {
        var msg = content.GetUnityObject<VFSMsgSO>();
        var replicaMeta = VFSMsgReplicaMeta.FromNode(context?.Node);
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[vfs_msg] query node={0} msg={1} resolved={2} selectedIndex={3} choices={4}",
            context?.Node?.NodeID ?? "(null)",
            msg != null ? "ok" : "null",
            replicaMeta?.IsResolved.ToString() ?? "(null)",
            replicaMeta?.SelectedChoiceIndex.ToString() ?? "(null)",
            replicaMeta?.ChoiceTargetNodeIDs?.Count.ToString() ?? "(null)");

        if (msg == null)
        {
            return VFSQueryResult.Create(
                presentationType: "error",
                title: "Broken .msg",
                summary: "VFSMsgSO is null",
                payload: null,
                isInteractive: false);
        }

        return VFSQueryResult.Create(
            presentationType: "msg",
            requestName: context?.RequestName ?? MsgClientViewKeys.Inspect,
            title: string.IsNullOrWhiteSpace(msg.Title) ? msg.Sender : msg.Title,
            summary: msg.Body,
            payload: new VFSMsgQueryPayload
            {
                Message = msg,
                PackID = context?.PackID,
                VfsPath = context?.VfsPath,
                SourceNodeId = context?.Node?.NodeID,
                FrontendContext = context?.FrontendContext,
                ReplicaMeta = replicaMeta
            },
            isInteractive: true);
    }
}

/// <summary>
/// .msg 的 Query / Execute 薄上下文包。
/// 资源本体仍然是 VFSMsgSO；这里只补宿主和来源路径等运行时信息。
/// </summary>
public sealed class VFSMsgQueryPayload
{
    public VFSMsgSO Message;
    public string PackID;
    public string PackIDKey;
    public string VfsPath;
    public string SourceNodeId;
    public string SignalId;
    public object FrontendContext;
    public VFSMsgReplicaMeta ReplicaMeta;
}

public sealed class VFSMsgReplicaMeta
{
    public string BackendPackID;
    public string BackendNodeID;
    public string SignalId;
    public bool IsResolved;
    public int SelectedChoiceIndex = -1;
    public List<string> ChoiceTargetNodeIDs = new();

    public static VFSMsgReplicaMeta FromNode(VFSNodeData node)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.InlineText))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<VFSMsgReplicaMeta>(node.InlineText);
        }
        catch
        {
            return null;
        }
    }

    public static string Serialize(VFSMsgReplicaMeta meta)
    {
        if (meta == null)
            return string.Empty;

        return JsonConvert.SerializeObject(meta);
    }
}

