using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// 社交对话销毁节点 - 社交系统专用喵~
    /// 信号进入时销毁当前 Pack 实例，强制重置状态
    /// </summary>
    [Serializable]
    [NodeType(NodeSystem.Social)]
    public class SocialDestroyNodeData : BaseNodeData
    {
        [Header("Ports")]
        [InPort(0, "输入", NekoPortCapacity.Multi)]
        public string In;
    }
}
