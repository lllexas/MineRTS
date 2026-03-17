using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// 交互式对话节点数据 - 社交系统专用喵~
    /// 【msgPack 私有节点】
    /// </summary>
    [Serializable]
    [NodeType(NodeSystem.Social)]
    public class SocialDialogueNodeData : BaseNodeData
    {
        [Header("Dialogue Content")]
        [Tooltip("发言人")]
        public string Speaker = "指挥官";

        [Tooltip("台词内容")]
        [TextArea(4, 8)]
        public string Content;

        [Header("Options")]
        [Tooltip("选项列表（将按顺序对应输出端口 0, 1, 2...）")]
        public List<string> OptionTexts = new List<string> { "继续" };

        [Header("Ports")]
        [InPort(0, "输入", NekoPortCapacity.Multi)]
        public string In;

        [OutPort(0, "选项分支", NekoPortCapacity.Multi)]
        public List<string> Options;
    }
}
