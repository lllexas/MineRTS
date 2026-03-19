using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSPackData - VFS 数据包喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 继承自 BasePackData，复用：
/// - PackID
/// - DisplayName
/// - Description
/// - Author
/// - Version
/// - CreatedAt / ModifiedAt
/// - Nodes (List<BaseNodeData>)
///
/// 用于存储 VFS 图的完整数据喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[Serializable]
public class VFSPackData : BasePackData
{
    /// <summary>
    /// 绑定的地图 ID（可选，用于关联特定关卡）
    /// </summary>
    [Tooltip("绑定的地图 ID")]
    public string BoundMapID;

    public VFSPackData()
    {
        PackID = Guid.NewGuid().ToString("N");
        DisplayName = "New VFS Pack";
        Description = "虚拟文件系统包";
        Author = "NekoTeam";
        Version = "1.0.0";
    }

    /// <summary>
    /// 添加 VFS 节点
    /// </summary>
    /// <param name="node">VFS 节点数据</param>
    public void AddNode(VFSNodeData node)
    {
        if (node == null) return;
        Nodes[node.NodeID] = node;
        Touch();
    }

    /// <summary>
    /// 验证数据包是否有效喵~
    /// </summary>
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(PackID)) return false;
        if (Nodes == null || Nodes.Count == 0) return false;
        if (string.IsNullOrEmpty(RootNodeId)) return false;
        return true;
    }
}
