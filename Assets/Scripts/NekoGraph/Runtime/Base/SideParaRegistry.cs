using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// SidePara 提取注册表 - 启动时一次性反射扫描，之后零反射喵~
/// 收集所有带 [SideParaKey] 标签的节点字段，供 BasePackData.OnBeforeSerialize 调用喵~
/// </summary>
public static class SideParaRegistry
{
    // NodeType → List<(sideParaKey, fieldInfo)>
    private static readonly Dictionary<Type, List<(string key, FieldInfo field)>> _registry;

    static SideParaRegistry()
    {
        _registry = new Dictionary<Type, List<(string, FieldInfo)>>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!typeof(BaseNodeData).IsAssignableFrom(type) || type.IsAbstract) continue;

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attr = field.GetCustomAttribute<SideParaKeyAttribute>();
                    if (attr == null) continue;

                    if (!_registry.ContainsKey(type))
                        _registry[type] = new List<(string, FieldInfo)>();

                    _registry[type].Add((attr.Key, field));
                }
            }
        }
    }

    /// <summary>
    /// 从节点集合中提取所有 SidePara 喵~
    /// </summary>
    public static Dictionary<string, string> Extract(IEnumerable<BaseNodeData> nodes)
    {
        var result = new Dictionary<string, string>();
        foreach (var node in nodes)
        {
            if (node == null) continue;
            if (!_registry.TryGetValue(node.GetType(), out var list)) continue;

            foreach (var (key, field) in list)
            {
                var val = field.GetValue(node)?.ToString();
                if (!string.IsNullOrEmpty(val))
                    result[key] = val;
            }
        }
        return result;
    }
}
