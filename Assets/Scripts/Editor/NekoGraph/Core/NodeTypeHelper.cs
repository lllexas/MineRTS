#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NekoGraph;

namespace NekoGraph.Editor
{
    /// <summary>
    /// 节点类型信息 - 用于菜单生成和节点创建喵~
    /// </summary>
    public class NodeTypeInfo
    {
        /// <summary>
        /// 节点类型喵~
        /// </summary>
        public Type NodeType;

        /// <summary>
        /// 菜单项标签喵~
        /// </summary>
        public NodeMenuItemAttribute MenuItemAttr;

        /// <summary>
        /// 节点系统类型标签喵~
        /// </summary>
        public NodeTypeAttribute TypeAttr;

        /// <summary>
        /// 完整菜单路径喵~
        /// </summary>
        public string MenuPath;

        /// <summary>
        /// 菜单路径分割后的部分喵~
        /// </summary>
        public string[] PathParts;
    }

    /// <summary>
    /// 节点类型反射辅助类 - 统一管理节点类型的反射和缓存喵~
    /// </summary>
    public static class NodeTypeHelper
    {
        /// <summary>
        /// 缓存的节点类型信息列表喵~
        /// </summary>
        private static readonly Dictionary<NodeSystem, List<NodeTypeInfo>> _cachedNodeTypes = new Dictionary<NodeSystem, List<NodeTypeInfo>>();

        /// <summary>
        /// 获取指定系统可用的节点类型列表喵~
        /// </summary>
        /// <param name="system">节点系统类型喵~</param>
        /// <returns>节点类型信息列表喵~</returns>
        public static List<NodeTypeInfo> GetNodeTypesForSystem(NodeSystem system)
        {
            // 检查缓存喵~
            if (_cachedNodeTypes.TryGetValue(system, out var cached))
            {
                return cached;
            }

            // 获取所有程序集中带 [NodeMenuItem] 标签的类型喵~
            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        // 跳过无法加载的类型喵~
                        return e.Types.Where(t => t != null);
                    }
                })
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BaseNode)));

            var nodeTypes = new List<NodeTypeInfo>();

            foreach (var type in allTypes)
            {
                var menuItemAttr = type.GetCustomAttribute<NodeMenuItemAttribute>();
                if (menuItemAttr == null) continue;

                var typeAttr = type.GetCustomAttribute<NodeTypeAttribute>();

                // 根据系统类型过滤喵~
                // Common 类型的节点在所有系统中都显示喵！
                if (system != NodeSystem.Common && typeAttr != null)
                {
                    if (typeAttr.System != NodeSystem.Common && typeAttr.System != system) continue;
                }

                // 解析菜单路径喵~
                var pathParts = menuItemAttr.MenuPath.Split('/');

                nodeTypes.Add(new NodeTypeInfo
                {
                    NodeType = type,
                    MenuItemAttr = menuItemAttr,
                    TypeAttr = typeAttr,
                    MenuPath = menuItemAttr.MenuPath,
                    PathParts = pathParts
                });
            }

            // 按菜单路径排序喵~
            nodeTypes = nodeTypes.OrderBy(n => n.MenuPath).ToList();

            // 缓存结果喵~
            _cachedNodeTypes[system] = nodeTypes;

            return nodeTypes;
        }

        /// <summary>
        /// 清除所有缓存喵~
        /// </summary>
        public static void ClearCache()
        {
            _cachedNodeTypes.Clear();
        }

        /// <summary>
        /// 清除指定系统的缓存喵~
        /// </summary>
        public static void ClearCache(NodeSystem system)
        {
            _cachedNodeTypes.Remove(system);
        }

        /// <summary>
        /// 尝试获取节点类型信息喵~
        /// </summary>
        public static bool TryGetNodeType(Type nodeType, out NodeTypeInfo info)
        {
            info = null;

            var menuItemAttr = nodeType.GetCustomAttribute<NodeMenuItemAttribute>();
            if (menuItemAttr == null) return false;

            var typeAttr = nodeType.GetCustomAttribute<NodeTypeAttribute>();
            var pathParts = menuItemAttr.MenuPath.Split('/');

            info = new NodeTypeInfo
            {
                NodeType = nodeType,
                MenuItemAttr = menuItemAttr,
                TypeAttr = typeAttr,
                MenuPath = menuItemAttr.MenuPath,
                PathParts = pathParts
            };

            return true;
        }
    }
}
#endif
