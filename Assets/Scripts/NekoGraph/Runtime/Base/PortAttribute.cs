using System;
using NekoGraph;

/// <summary>
/// 输入端口标签 - 标记字段对应的输入端口索引喵~
/// 【跨平台安全·运行时纯净版】
/// 使用 NekoPortCapacity 替代 UnityEditor 的 Port.Capacity 喵~
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class InPortAttribute : Attribute
{
    /// <summary>
    /// 端口索引喵~
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 端口名称喵~
    /// </summary>
    public string PortName { get; }

    /// <summary>
    /// 端口容量喵~
    /// </summary>
    public NekoPortCapacity Capacity { get; }

    public InPortAttribute(int index, string portName = "输入", NekoPortCapacity capacity = NekoPortCapacity.Multi)
    {
        Index = index;
        PortName = portName;
        Capacity = capacity;
    }
}

/// <summary>
/// 输出端口标签 - 标记字段对应的输出端口索引喵~
/// 【跨平台安全·运行时纯净版】
/// 使用 NekoPortCapacity 替代 UnityEditor 的 Port.Capacity 喵~
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class OutPortAttribute : Attribute
{
    /// <summary>
    /// 端口索引喵~
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 端口名称喵~
    /// </summary>
    public string PortName { get; }

    /// <summary>
    /// 端口容量喵~
    /// </summary>
    public NekoPortCapacity Capacity { get; }

    public OutPortAttribute(int index, string portName = "输出", NekoPortCapacity capacity = NekoPortCapacity.Multi)
    {
        Index = index;
        PortName = portName;
        Capacity = capacity;
    }
}
