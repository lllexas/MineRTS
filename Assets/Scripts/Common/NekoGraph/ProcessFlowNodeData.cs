using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

// =========================================================
// 公共流程节点数据 - 通用流程控制系统
// 脱离 Story 专用，成为 Mission/Story 共用的流程节点喵~
// =========================================================

/// <summary>
/// 流程根节点 - 整个流程树的起始锚点（全图唯一）喵~
/// 用于 Mission 或 Story 系统的流程起点
/// </summary>
[Serializable]
public class RootNodeData : BaseNodeData
{
    [OutPort(0, "开始流程", NekoPortCapacity.Multi)]
    public List<string> _;
}

/// <summary>
/// 流程 ID 节点 (Spine) - 定义流程的逻辑骨架（阶段/步骤）喵~
/// 作为无线输电继电器，通过 ID 关联到 Leaf A 和 B 节点，进行信号同步。
/// B 节点收到信号时回调 Spine 节点，Spine 进行信号输出，通常指向下一个 Spine 节点。
/// </summary>
[Serializable]
public class SpineNodeData : BaseNodeData
{
    [Tooltip("流程 ID（与 Leaf 节点共享）")]
    public string ProcessID;

    [InPort(0, "信号输入", NekoPortCapacity.Multi)]
    [Tooltip("父节点 SpineID（用于恢复一对多连线）喵~")]
    public List<string> ParentSpineID;

    [OutPort(0, "信号输出", NekoPortCapacity.Multi)]
    [Tooltip("下一个 Spine 节点的 ID 列表（用于恢复一对多连线）喵~")]
    public List<string> NextSpineNodeIDs = new List<string>();
}

/// <summary>
/// 叶 ID 节点 A (LeafA) - 处理具体的执行演出喵~
/// 【跨平台安全·运行时纯净版】
/// </summary>
[Serializable]
public class LeafNode_A_Data : BaseNodeData
{
    [Tooltip("流程 ID（与 Spine 节点共享）")]
    public string ProcessID;

    [OutPort(0, "信号输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIds = new List<string>();
}

/// <summary>
/// 叶 ID 节点 B (LeafB) - 处理执行完毕的回调~
/// 【跨平台安全·运行时纯净版】
/// </summary>
[Serializable]
public class LeafNode_B_Data : BaseNodeData
{
    [Tooltip("流程 ID（与 Spine 节点共享）")]
    public string ProcessID;

    [InPort(0, "等待输入", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIds = new List<string>();
}
