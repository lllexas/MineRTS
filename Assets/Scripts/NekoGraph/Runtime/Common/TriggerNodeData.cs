using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

/// <summary>
/// 监听器节点数据 - 强类型契约版喵！
/// </summary>
[Serializable]
public class TriggerNodeData : BaseNodeData
{
    [Tooltip("监听的全局事件枚举喵~")]
    public TriggerEvent Event = TriggerEvent.GameStarted;

    [InPort(0, "激活", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [OutPort(0, "信号包 (Signal)", NekoPortCapacity.Multi)]
    public List<string> SignalOutputs = new List<string>();

    public new void CopyFrom(BaseNodeData other)
    {
        base.CopyFrom(other);
        if (other is TriggerNodeData triggerOther)
        {
            Event = triggerOther.Event;
            InputNodeIDs = new List<string>(triggerOther.InputNodeIDs);
            SignalOutputs = new List<string>(triggerOther.SignalOutputs);
        }
    }

    /// <summary>
    /// 运行时注册方法喵~
    /// </summary>
    public void Register(Action<object> onTriggered)
    {
        // 底层订阅依然使用枚举名字符串作为 Key 喵~
        PostSystem.Instance.On(Event.ToString(), onTriggered);
    }

    /// <summary>
    /// 运行时注销方法喵~
    /// </summary>
    public void Unregister(Action<object> onTriggered)
    {
        PostSystem.Instance.Off(Event.ToString(), onTriggered);
    }
}
