using UnityEngine;
using NekoGraph;

/// <summary>
/// vfs.msg 的轻量载荷对象。
/// .msg 不再把整个对话 pack 塞进文件内容里，而是显式声明“消息资源”本身。
/// 如果需要进入进一步的对话/事件流程，再通过引用字段指向其他资源。
/// </summary>
[CreateAssetMenu(fileName = "NewVFSMsg", menuName = "MineRTS/VFS/Message")]
[VFSContentKind(VFSContentKind.UnityObject)]
public class VFSMsgSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("消息唯一 ID，可选。用于前端缓存、已读记录或埋点。")]
    public string MessageId;

    [Tooltip("发件人显示名。")]
    public string Sender = "系统";

    [Tooltip("消息标题。")]
    public string Title = "未命名消息";

    [Header("Preview")]
    [Tooltip("列表态的一行摘要。")]
    [TextArea(2, 3)]
    public string Preview;

    [Tooltip("消息展开后要显示的正文。可以为空。")]
    [TextArea(4, 8)]
    public string Body;

    [Header("Runtime")]
    [Tooltip("初始是否为未读。运行时已读状态应由存档层维护，而不是回写资源本体。")]
    public bool DefaultUnread = true;

    [Tooltip("消息时间戳（Unix 秒，可选）。0 表示未指定。")]
    public long Timestamp;

    [Tooltip("后续若需要进入图驱动会话，可在这里引用目标 PackID。")]
    public string ConversationPackID;

    [Tooltip("后续若需要直接跳转到另一个 VFS 资源，可在这里填写路径。")]
    public string NextVfsPath;
}
