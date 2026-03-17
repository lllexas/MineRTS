using System;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// 等待节点数据 - 社交系统专用喵~
    /// 【msgPack 私有节点】
    /// 职责：显示文字后，等待玩家按回车继续
    /// </summary>
    [Serializable]
    [NodeType(NodeSystem.Social)]
    public class SocialWaitNodeData : BaseNodeData
    {
        [Header("Wait Content")]
        [Tooltip("发言人")]
        public string Speaker = "系统";

        [Tooltip("提示内容")]
        [TextArea(2, 4)]
        public string Content = "请按回车继续...";

        [Header("Ports")]
        [InPort(0, "输入", NekoPortCapacity.Multi)]
        public string In;

        [OutPort(0, "继续", NekoPortCapacity.Single)]
        public string Next;
    }
}
