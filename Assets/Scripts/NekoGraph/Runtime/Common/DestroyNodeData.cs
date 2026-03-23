using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// 流程销毁节点数据 - 销毁 Pack 实例并重置状态喵~
    /// </summary>
    [Serializable]
    [NodeType(NodeSystem.Common)]
    public class DestroyNodeData : BaseNodeData
    {
        [Header("Ports")]
        [InPort(0, "输入", NekoPortCapacity.Multi)]
        public string In;
    }
}
