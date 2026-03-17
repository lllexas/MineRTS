using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

// =========================================================
// Lab 科技树系统数据类型定义
// =========================================================
//
// 【架构说明】
//
// TechNode 是纯 UI 展示节点，不阻塞信号，信号直接透传喵~
// 解锁条件由 TriggerNode 实现，夹在两个 TechNode 之间喵~
//
// 流程图示例：
// ┌───────────┐    ┌──────────────┐    ┌───────────┐    ┌───────────┐
// │ TechNode A│───▶│ TriggerNode  │───▶│ Command   │───▶│TechNode B │
// │  (已解锁)  │    │ (解锁条件)    │    │ (解锁奖励) │    │ (新科技)  │
// │           │    │              │    │           │    │           │
// │           │    │ [进度输出] ──┼───▶│TechNode_R │    │           │
// └───────────┘    └──────────────┘    └───────────┘    └───────────┘
//
// 职责划分：
// - TechNode: UI 展示（科技信息/状态显示）
// - TriggerNode: 条件监听（解锁条件/进度追踪）
// - CommandNode: 执行动作（解锁科技/发放奖励）
// - TechNode_R: UI 刷新（每次进度变化都刷新）
//
// =========================================================

/// <summary>
/// 科技类型枚举喵~
/// </summary>
public enum TechType
{
    /// <summary>
    /// 解锁新建筑/单位
    /// </summary>
    Unlock,

    /// <summary>
    /// 升级现有功能
    /// </summary>
    Upgrade,

    /// <summary>
    /// 解锁蓝图配方
    /// </summary>
    Blueprint,

    /// <summary>
    /// 被动增益
    /// </summary>
    Passive,

    /// <summary>
    /// 特殊科技（剧情/事件相关）
    /// </summary>
    Special
}

/// <summary>
/// 科技节点数据 - 继承 BaseNodeData 喵~
/// 
/// 职责：
/// - 显示科技信息（名称/描述/图标/类型）
/// - 存储解锁后的奖励命令
/// - 信号纯透传，不阻塞，不修改 payload
/// 
/// 注意：
/// - 端口命名为"经入/经出"，表示信号纯经过，不操作喵~
/// - 解锁条件由 TriggerNode 实现，不在此节点定义喵~
/// </summary>
[Serializable]
public class TechNodeData : BaseNodeData
{
    [Header("科技基本信息")]
    [Tooltip("科技唯一 ID")]
    public string TechID;

    [Tooltip("科技名称")]
    public string TechName;

    [Tooltip("科技描述")]
    [TextArea(3, 5)]
    public string Description;

    [Tooltip("科技图标")]
    public Sprite Icon;

    [Tooltip("科技类型")]
    public TechType TechType;

    [Header("解锁奖励")]
    [Tooltip("解锁后执行的命令（由 CommandNode 执行）")]
    public CommandData UnlockReward = new CommandData();

    [Header("端口 - 信号经入/经出（纯透传）")]
    [Tooltip("前置科技信号经入")]
    [InPort(0, "信号经入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [Tooltip("信号经出到后续节点")]
    [OutPort(0, "信号经出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();
}
