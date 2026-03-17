using System;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// 对话结束节点数据 - 社交系统专用喵~
    /// 【msgPack 私有节点】
    /// 职责：退出 TUI 模式并标记 VFS 消息为已读
    /// </summary>
    [Serializable]
    [NodeType(NodeSystem.Social)]
    public class SocialFinishNodeData : BaseNodeData
    {
        [Header("End Settings")]
        [Tooltip("是否自动标记消息为已读")]
        public bool MarkAsRead = true;

        [Tooltip("结束时的提示语（可选）")]
        public string ClosingMessage = "--- 对话结束 ---";

        [Header("Ports")]
        [InPort(0, "输入", NekoPortCapacity.Multi)]
        public string In;
    }
}
