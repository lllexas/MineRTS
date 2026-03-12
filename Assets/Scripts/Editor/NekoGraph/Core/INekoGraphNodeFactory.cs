#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 节点工厂接口 - 用于解耦 SearchWindow 和 GraphView 的依赖喵~
/// 面向接口编程，避免泛型约束问题喵~
/// </summary>
public interface INekoGraphNodeFactory
{
    /// <summary>
    /// 创建节点喵~
    /// </summary>
    BaseNode CreateNode(Type nodeType, Vector2 position, BaseNodeData data = null);
    Vector2 ConvertScreenToLocal(Vector2 screenPosition, EditorWindow window);
}
#endif
