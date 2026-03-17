#if UNITY_EDITOR
using System;

/// <summary>
/// 连线恢复方法标签 Attribute - 标记静态方法为某个节点类型的连线恢复方法喵~
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RestoreMethodAttribute : Attribute
{
    public Type NodeType { get; }

    public RestoreMethodAttribute(Type nodeType)
    {
        NodeType = nodeType;
    }
}
#endif
