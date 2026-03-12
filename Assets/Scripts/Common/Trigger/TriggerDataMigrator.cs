using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TriggerData 迁移助手 - 用于将旧格式转换为新格式喵~
/// 旧格式：UseEnumTrigger + TriggerType + TriggerParam
/// 新格式：EventName + Parameters
/// </summary>
public static class TriggerDataMigrator
{
    /// <summary>
    /// 尝试迁移旧的 TriggerData 到新格式喵~
    /// </summary>
    /// <param name="data">要迁移的数据</param>
    /// <returns>是否成功迁移</returns>
    public static bool MigrateIfNeeded(TriggerData data)
    {
        if (data == null) return false;
        
        // 如果已经是新格式（EventName 不为空且是有效的注册类型），则不需要迁移
        if (!string.IsNullOrEmpty(data.EventName) && 
            TriggerRegistry.TryGetTypeInfo(data.EventName, out _))
        {
            return false; // 已经是新格式
        }
        
        // 旧格式数据通过 JsonUtility 反序列化后，字段会以 JSON 键名存储
        // 我们需要通过反射读取这些字段
        var type = data.GetType();
        
        // 读取旧字段（如果存在）
        var useEnumField = type.GetField("UseEnumTrigger");
        var triggerTypeField = type.GetField("TriggerType");
        var triggerParamField = type.GetField("TriggerParam");
        
        // 检查是否有旧字段
        if (triggerTypeField != null)
        {
            object triggerTypeValue = triggerTypeField.GetValue(data);
            string triggerParamValue = triggerParamField?.GetValue(data) as string;
            
            // 将 TriggerType 枚举转换为字符串事件名
            string eventName = ConvertTriggerTypeToEventName(triggerTypeValue);
            
            // 设置新格式
            data.EventName = eventName;
            data.Parameters = new List<string>();
            
            if (!string.IsNullOrEmpty(triggerParamValue))
            {
                data.Parameters.Add(triggerParamValue);
            }
            
            Debug.Log($"[TriggerMigrator] 成功迁移 TriggerData: {eventName} (param: {triggerParamValue})");
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 将旧的 TriggerType 枚举值转换为新的事件名字符串喵~
    /// （用于兼容旧数据格式）
    /// </summary>
    private static string ConvertTriggerTypeToEventName(object triggerTypeValue)
    {
        if (triggerTypeValue == null) return "Time";

        // 旧版 TriggerType 枚举定义（已废弃）：
        // Time = 0
        // MissionCompleted = 1
        // AreaReached = 2
        // Custom = 3

        int typeIndex = Convert.ToInt32(triggerTypeValue);
        
        switch (typeIndex)
        {
            case 0: return "Time";
            case 1: return "MissionCompleted";
            case 2: return "AreaReached";
            case 3: return "Custom";
            default: return "Time";
        }
    }
    
    /// <summary>
    /// 批量迁移 MissionPackData 中的所有触发器喵~
    /// </summary>
    public static int MigratePack(MissionPackData pack)
    {
        if (pack == null || pack.Triggers == null) return 0;
        
        int migratedCount = 0;
        foreach (var trigger in pack.Triggers)
        {
            if (trigger.Trigger != null && MigrateIfNeeded(trigger.Trigger))
            {
                migratedCount++;
            }
        }
        
        if (migratedCount > 0)
        {
            Debug.Log($"[TriggerMigrator] 迁移完成：{migratedCount} 个触发器");
        }
        
        return migratedCount;
    }
}
