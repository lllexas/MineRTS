using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// 正文发射器节点 - 社交系统专用喵~
    /// 信号进入时发射 Social.ShowBody 事件
    /// </summary>
    [Serializable]
    [NodeType(NodeSystem.Social)]
    public class SocialMsgContentNodeData : BaseNodeData
    {
        [Header("Message Content")]
        [Tooltip("发言人")]
        public string Speaker = "指挥官";

        [Tooltip("正文台词")]
        [TextArea(6, 12)]
        public string Body;

        [Header("Ports")]
        [InPort(0, "输入", NekoPortCapacity.Multi)]
        public string In;

        [OutPort(0, "下一步", NekoPortCapacity.Multi)]
        public List<string> Out = new List<string>();
    }
}
