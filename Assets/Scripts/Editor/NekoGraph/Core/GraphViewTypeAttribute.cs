#if UNITY_EDITOR
using System;
using NekoGraph;

/// <summary>
/// GraphView 类型标签 - 标记一个类是 BaseGraphView 的子类喵~
/// 用于类型检查和文档生成
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class GraphViewTypeAttribute : Attribute
{
    /// <summary>
    /// 节点类型（可选，用于标识这个 GraphView 支持的节点系统）
    /// </summary>
    public NodeSystem System { get; set; }

    /// <summary>
    /// 显示名称（可选，用于编辑器 UI）
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 构造函数喵~
    /// </summary>
    public GraphViewTypeAttribute()
    {
        System = NodeSystem.Common;
        DisplayName = "";
    }

    /// <summary>
    /// 构造函数喵~
    /// </summary>
    /// <param name="system">节点系统类型</param>
    public GraphViewTypeAttribute(NodeSystem system)
    {
        System = system;
        DisplayName = "";
    }
}
#endif
