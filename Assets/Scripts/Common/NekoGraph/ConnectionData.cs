using System;
using UnityEngine;

/// <summary>
/// 连线数据结构 - 统一的连线数据格式喵~
/// 用于存储节点之间的连接关系
/// </summary>
[Serializable]
public struct ConnectionData
{
    [Tooltip("输出端口索引")]
    public int FromPortIndex;

    [Tooltip("目标节点 ID")]
    public string TargetNodeID;

    [Tooltip("输入端口索引")]
    public int ToPortIndex;

    public ConnectionData(int fromPortIndex, string targetNodeID, int toPortIndex)
    {
        FromPortIndex = fromPortIndex;
        TargetNodeID = targetNodeID;
        ToPortIndex = toPortIndex;
    }
}
