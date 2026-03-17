using System;

/// <summary>
/// 事件协议协议 - 定义 Payload 的标准形状喵~
/// </summary>
public enum EventProtocol
{
    None,       // 无参数
    Entity,     // 实体句柄 (EntityHandle/GameObject)
    Numeric,    // 数值 (float/int/double)
    String,     // 字符串 (ID/Name)
    Vector,     // 坐标 (Vector3)
    Boolean     // 布尔值 (Switch/State)
}

/// <summary>
/// 事件元数据特性喵~
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class EventInfoAttribute : Attribute
{
    public EventProtocol Protocol { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string Tooltip { get; set; }

    public EventInfoAttribute(EventProtocol protocol, string displayName, string category)
    {
        Protocol = protocol;
        DisplayName = displayName;
        Category = category;
    }
}

/// <summary>
/// 全局事件集约定义枚举喵！
/// Agent 和 程序员的唯一契约表喵~
/// </summary>
public enum GameEvent
{
    [EventInfo(EventProtocol.None, "游戏开始", "系统")]
    GameStarted,

    [EventInfo(EventProtocol.Entity, "单位死亡", "战斗")]
    UnitKilled,

    [EventInfo(EventProtocol.Numeric, "金钱变动", "经济")]
    MoneyChanged,

    [EventInfo(EventProtocol.String, "任务完成", "剧情")]
    MissionCompleted,

    [EventInfo(EventProtocol.Vector, "点击地面", "输入")]
    GroundClicked,

    [EventInfo(EventProtocol.Entity, "单位受伤", "战斗")]
    UnitDamaged,
    
    [EventInfo(EventProtocol.Boolean, "基地受袭状态", "警告")]
    BaseUnderAttack
}
