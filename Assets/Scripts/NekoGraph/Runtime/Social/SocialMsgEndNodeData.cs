using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// 社交对话终结节点 - 社交系统专用喵~
    /// 信号进入时发射 Social.MsgFinished 事件
    /// </summary>
    [Serializable]
    [NodeType(NodeSystem.Social)]
    public class SocialMsgEndNodeData : BaseNodeData
    {
        [Header("Ports")]
        [InPort(0, "输入", NekoPortCapacity.Multi)]
        public string In;
    }
}
