using UnityEngine;
using NekoGraph;

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
        string packInstanceID,
        System.Action continueAction)
    {
        var msg = content.GetUnityObject<VFSMsgSO>();
        if (msg == null)
        {
            Debug.LogError("[VFSMsgResource] Execute 失败：VFSMsgSO 为 null");
            return HandleResult.Error;
        }

        // 新 .msg 先只承担“消息资源”本身，不再默认把载荷解释成整个 pack。
        // 当前执行语义保持保守：发出一份显式消息展示请求，后续再决定是否挂接专门的 Player。
        PostSystem.Instance.Send("VFS.Msg.Execute", new VFSMsgPresentation
        {
            MessageId = msg.MessageId,
            Sender = msg.Sender,
            Title = msg.Title,
            Preview = msg.Preview,
            Body = msg.Body,
            Timestamp = msg.Timestamp,
            DefaultUnread = msg.DefaultUnread,
            ConversationPackID = msg.ConversationPackID,
            NextVfsPath = msg.NextVfsPath,
            PackID = pack?.PackID,
            PackInstanceID = packInstanceID,
            SourceNodeId = context?.CurrentNodeId
        });

        return HandleResult.Push;
    }

    [VFSQuery]
    public static VFSQueryResult Query(VFSResolvedContent content, VFSQueryContext context)
    {
        var msg = content.GetUnityObject<VFSMsgSO>();
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
            presentationType: "social.msg",
            title: string.IsNullOrWhiteSpace(msg.Title) ? msg.Sender : msg.Title,
            summary: string.IsNullOrWhiteSpace(msg.Preview) ? msg.Body : msg.Preview,
            payload: new VFSMsgPresentation
            {
                MessageId = msg.MessageId,
                Sender = msg.Sender,
                Title = msg.Title,
                Preview = msg.Preview,
                Body = msg.Body,
                Timestamp = msg.Timestamp,
                DefaultUnread = msg.DefaultUnread,
                ConversationPackID = msg.ConversationPackID,
                NextVfsPath = msg.NextVfsPath,
                PackID = context?.PackID,
                VfsPath = context?.VfsPath
            },
            isInteractive: true);
    }
}

/// <summary>
/// .msg 面向前端的统一展示包。
/// Execute 和 Query 都可以复用这一份显式数据，而不是再把业务约定散落在外部代码里。
/// </summary>
public sealed class VFSMsgPresentation
{
    public string MessageId;
    public string Sender;
    public string Title;
    public string Preview;
    public string Body;
    public long Timestamp;
    public bool DefaultUnread;
    public string ConversationPackID;
    public string NextVfsPath;
    public string PackID;
    public string PackInstanceID;
    public string VfsPath;
    public string SourceNodeId;
}
