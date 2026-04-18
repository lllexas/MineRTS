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
        string packIDKey,
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
        PostSystem.Instance.Send("VFS.Msg.Execute", new VFSMsgQueryPayload
        {
            Message = msg,
            PackID = pack?.PackID,
            PackIDKey = packIDKey,
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
            payload: new VFSMsgQueryPayload
            {
                Message = msg,
                PackID = context?.PackID,
                VfsPath = context?.VfsPath
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
}
