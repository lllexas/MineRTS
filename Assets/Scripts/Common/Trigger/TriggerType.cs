using System;

/// <summary>
/// 触发器类型枚举（Mission 和 Story 系统共用）喵~
/// </summary>
public enum TriggerType 
{ 
    Time,              // 时间触发
    MissionCompleted,  // 任务完成
    AreaReached,       // 到达区域
    Custom             // 自定义事件（Story 系统专用）
}
