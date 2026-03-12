using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 节点数据基类 - 所有节点数据的抽象基类喵~
/// 运行时和编辑器共用，不能放在 Editor 目录下喵~
/// </summary>
[Serializable]
public abstract class BaseNodeData
{
    [Tooltip("节点唯一 ID")]
    public string NodeID;

    [Tooltip("编辑器中的位置")]
    public SerializableVector2 EditorPosition;

    [Tooltip("输出连线列表 - 由系统自动生成喵~")]
    public List<ConnectionData> OutputConnections = new List<ConnectionData>();

    /// <summary>
    /// 从另一个节点数据复制基础字段喵~
    /// </summary>
    public void CopyFrom(BaseNodeData other)
    {
        if (other == null) return;
        NodeID = other.NodeID;
        EditorPosition = other.EditorPosition;
        OutputConnections = other.OutputConnections;
    }
}
