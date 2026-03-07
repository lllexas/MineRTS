using System;
using UnityEngine;

/// <summary>
/// 触发器数据（Mission 和 Story 系统共用）喵~
/// </summary>
[Serializable]
public class TriggerData
{
    [Tooltip("是否使用枚举触发器（false=自定义事件名）")]
    public bool UseEnumTrigger = true;
    
    [Tooltip("枚举触发器类型")]
    public TriggerType TriggerType;
    
    [Tooltip("自定义事件名（中文，当 UseEnumTrigger=false 时使用）")]
    public string CustomEventName;
    
    [Tooltip("触发参数（如任务 ID、时间、区域 ID 等）")]
    public string TriggerParam;
}
