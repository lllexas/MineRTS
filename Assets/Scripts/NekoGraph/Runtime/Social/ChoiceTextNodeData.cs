using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// 选项信息节点 - 社交系统专用喵~
    /// 信号进入时发射 Social.RegisterOption 事件
    /// </summary>
    [Serializable]
    [NodeType(NodeSystem.Social)]
    public class ChoiceTextNodeData : BaseNodeData
    {
        [Header("Option Data")]
        [Tooltip("选项编号 (1, 2, 3...)")]
        public int OptionIndex = 1;

        [Tooltip("选项描述文字")]
        public string Label = "继续";

        [Header("Ports")]
        [InPort(0, "输入", NekoPortCapacity.Multi)]
        public string In;

        [OutPort(0, "触发后流向", NekoPortCapacity.Multi)]
        public List<string> Out = new List<string>();
    }
}
