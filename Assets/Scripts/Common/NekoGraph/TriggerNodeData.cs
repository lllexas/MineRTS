using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

/// <summary>
/// 触发器节点数据 - Mission 和 Story 系统共用喵~
/// 继承 BaseNodeData，用于在 NekoGraph 编辑器中编辑
/// </summary>
[Serializable]
public class TriggerNodeData : BaseNodeData
{
    [Tooltip("触发器数据（事件名 + 参数列表）")]
    public TriggerData Trigger = new TriggerData();

    [Tooltip("输入节点 ID 列表（多对一）")]
    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [Tooltip("输出节点 ID 列表（一对多，条件满足时触发）")]
    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();

    [Tooltip("进度输出节点 ID 列表（一对多，每次进度变化都触发）")]
    [OutPort(1, "进度输出", NekoPortCapacity.Multi)]
    public List<string> ProgressOutputs = new List<string>();

    [Tooltip("当前累积进度（运行时使用）")]
    public double CurrentAmount;

    [Tooltip("目标进度阈值（达到时从主输出端口触发）")]
    public double RequiredAmount = 1;

    /// <summary>
    /// 从另一个节点数据复制基础字段喵~
    /// </summary>
    public new void CopyFrom(BaseNodeData other)
    {
        base.CopyFrom(other);
        if (other is TriggerNodeData triggerOther)
        {
            Trigger = new TriggerData();
            Trigger.EventName = triggerOther.Trigger.EventName;
            Trigger.Parameters = new List<string>(triggerOther.Trigger.Parameters);
            Trigger.HasTriggered = triggerOther.Trigger.HasTriggered;
            InputNodeIDs = new List<string>(triggerOther.InputNodeIDs);
            OutputNodeIDs = new List<string>(triggerOther.OutputNodeIDs);
            ProgressOutputs = new List<string>(triggerOther.ProgressOutputs);
            CurrentAmount = triggerOther.CurrentAmount;
            RequiredAmount = triggerOther.RequiredAmount;
        }
    }
}
