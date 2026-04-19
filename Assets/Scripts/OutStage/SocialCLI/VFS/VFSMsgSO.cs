using UnityEngine;
using NekoGraph;
using System;
using System.Collections.Generic;

/// <summary>
/// vfs.msg 的轻量载荷对象。
/// .msg 不再把整个对话 pack 塞进文件内容里，而是显式声明“消息资源”本身。
/// 如果需要进入进一步的对话/事件流程，再通过引用字段指向其他资源。
/// </summary>
[CreateAssetMenu(fileName = "NewVFSMsg", menuName = "MineRTS/VFS/Message")]
[VFSContentKind(VFSContentKind.UnityObject)]
public class VFSMsgSO : ScriptableObject
{
    [Serializable]
    public sealed class VFSMsgChoice
    {
        [Tooltip("选项标识。用于前后端约定具体选择。")]
        public string ChoiceTag;

        [Tooltip("显示给玩家的选项文本。")]
        [TextArea(1, 2)]
        public string Text;
    }

    [Header("Message")]
    [Tooltip("消息标签。纯标识，不直接承载运行时状态。")]
    public string MessageTag;

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

    [Header("Choices")]
    [Tooltip("消息可提供的选项列表。")]
    public List<VFSMsgChoice> Choices = new();
}
