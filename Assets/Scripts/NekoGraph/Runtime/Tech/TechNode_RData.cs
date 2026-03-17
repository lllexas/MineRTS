using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

// =========================================================
// TechNode_R - 科技树 UI 刷新节点
// =========================================================
//
// 【职责说明】
//
// TechNode_R 是纯透传节点，用于刷新 Lab 面板 UI 喵~
// 通常连接在 TriggerNode 的"进度输出"端口上喵~
//
// 工作流程：
// 1. 收到信号 → 发送 UI 刷新事件
// 2. 信号透传到输出节点
//
// 使用场景：
// - 连接 TriggerNode 的 Port 1（进度输出），每次进度变化都刷新 UI
// - 连接 CommandNode 之后，解锁完成后刷新 UI
//
// =========================================================

/// <summary>
/// 科技树刷新节点数据 - 继承 BaseNodeData 喵~
/// 
/// 职责：
/// - 收到信号就发送 UI 刷新事件
/// - 信号纯透传，不阻塞，不修改 payload
/// 
/// 注意：
/// - 端口命名为"经入/经出"，表示信号纯经过喵~
/// - 此节点没有业务逻辑，只负责刷新 UI 喵~
/// </summary>
[Serializable]
public class TechNode_RData : BaseNodeData
{
    [Header("端口 - 信号经入/经出（纯透传）")]
    [Tooltip("信号经入")]
    [InPort(0, "信号经入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [Tooltip("信号经出到后续节点")]
    [OutPort(0, "信号经出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();
}
