using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 信号上下文 - 携带电流中流动的载荷数据喵~
/// 用于在节点之间传递事件信息和触发上下文
/// </summary>
[Serializable]
public class SignalContext
{
    /// <summary>
    /// 信号来源节点 ID
    /// </summary>
    public string SourceNodeId;

    /// <summary>
    /// 事件名称（如"击败目标"、"任务完成"等）
    /// </summary>
    public string EventName;

    /// <summary>
    /// 事件携带的数据（如单位 ID、位置坐标等）
    /// </summary>
    public object EventData;

    /// <summary>
    /// 信号传播深度（用于调试和防止无限循环）
    /// </summary>
    public int Depth;

    /// <summary>
    /// 自定义数据字典（用于扩展）
    /// </summary>
    public Dictionary<string, object> CustomData;

    public SignalContext(string eventName = null, object eventData = null, int depth = 0)
    {
        EventName = eventName;
        EventData = eventData;
        Depth = depth;
        CustomData = new Dictionary<string, object>();
    }

    /// <summary>
    /// 创建深层副本喵~
    /// </summary>
    public SignalContext Clone()
    {
        var clone = new SignalContext(EventName, EventData, Depth + 1);
        clone.SourceNodeId = SourceNodeId;
        foreach (var kvp in CustomData)
        {
            clone.CustomData[kvp.Key] = kvp.Value;
        }
        return clone;
    }

    /// <summary>
    /// 设置自定义数据喵~
    /// </summary>
    public void SetCustomData(string key, object value)
    {
        CustomData[key] = value;
    }

    /// <summary>
    /// 获取自定义数据喵~
    /// </summary>
    public T GetCustomData<T>(string key, T defaultValue = default)
    {
        if (CustomData.TryGetValue(key, out var value) && value is T tValue)
        {
            return tValue;
        }
        return defaultValue;
    }
}
