using System;

/// <summary>
/// 事件协议协议 - 定义 Payload 的标准形状喵~
/// </summary>
public enum EventProtocol
{
    None,       // 无参数
    Entity,     // 实体句柄 (EntityHandle)
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
public enum TriggerEvent
{
    // --- 系统 (System) ---
    [EventInfo(EventProtocol.None, "游戏开始", "系统")]
    GameStarted,

    [EventInfo(EventProtocol.Numeric, "游戏节拍更新", "系统")]
    GameTickUpdated,

    // --- 战斗 (Battle - Payload: EntityHandle) ---
    [EventInfo(EventProtocol.Entity, "单位出生", "战斗")]
    UnitSpawned,

    [EventInfo(EventProtocol.Entity, "单位死亡", "战斗")]
    UnitKilled,

    [EventInfo(EventProtocol.Entity, "单位受伤", "战斗")]
    UnitDamaged,

    // --- 经济 (Economy) ---
    [EventInfo(EventProtocol.Numeric, "金钱变动", "经济")]
    MoneyChanged,

    [EventInfo(EventProtocol.Numeric, "资源变动", "经济")]
    ResourceChanged,

    [EventInfo(EventProtocol.Entity, "建筑完工", "经济")]
    BuildingConstructed,

    // --- 剧情与科技 (Story & Tech) ---
    [EventInfo(EventProtocol.String, "任务完成", "剧情")]
    MissionCompleted,

    [EventInfo(EventProtocol.String, "科研完成", "科技")]
    ResearchCompleted,

    // --- 输入与交互 (Input) ---
    [EventInfo(EventProtocol.Vector, "点击地面", "输入")]
    GroundClicked,

    [EventInfo(EventProtocol.Entity, "单位被选中", "输入")]
    UnitSelected,

    // --- 社交互动 (Social - Payload: None) ---
    [EventInfo(EventProtocol.None, "社交选项 1", "社交")]
    SocialOption1,

    [EventInfo(EventProtocol.None, "社交选项 2", "社交")]
    SocialOption2,

    [EventInfo(EventProtocol.None, "社交选项 3", "社交")]
    SocialOption3,

    [EventInfo(EventProtocol.None, "社交选项 4", "社交")]
    SocialOption4,

    // --- 警告 (Warning) ---
    [EventInfo(EventProtocol.Boolean, "基地受袭状态", "警告")]
    BaseUnderAttack
}
