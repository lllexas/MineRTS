#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

// =========================================================
// Lab 科技树 GraphView 画布
// =========================================================

/// <summary>
/// GraphView 画布 - Lab 科技树系统专用喵~
/// 【彻底拥抱混沌·主语驱动重构版】
/// 删除所有死板的端口校验，把自由还给开发者喵！
/// </summary>
[GraphViewType(NodeSystem.Lab)]
public class LabTechGraphView : BaseGraphView<LabTechPackData>
{
    public override NodeSystem CurrentSystem => NodeSystem.Lab;

    /// <summary>
    /// 节点移除回调（非泛型版本）喵~
    /// </summary>
    protected override void OnNodeRemovedGeneric(BaseNode node)
    {
        // Lab 科技树系统暂无特殊处理喵~
    }

    /// <summary>
    /// 节点粘贴回调喵~
    /// </summary>
    protected override void OnNodePasted(BaseNode node)
    {
        // 粘贴后生成新的 GUID，避免重复喵~
        if (node.Data != null)
        {
            node.Data.NodeID = System.Guid.NewGuid().ToString();
            node.SyncGUID(node.Data.NodeID);
        }
    }
}

#endif
