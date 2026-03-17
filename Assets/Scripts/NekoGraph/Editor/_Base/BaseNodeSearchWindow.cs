#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using NekoGraph;
using NekoGraph.Editor;

/// <summary>
/// 通用节点搜索窗口基类 - 通过反射自动生成节点创建菜单喵~
/// 使用 INekoGraphNodeFactory 接口解耦，彻底移除泛型约束喵~
/// </summary>
public abstract class BaseNodeSearchWindow : ScriptableObject, ISearchWindowProvider
{
    /// <summary>
    /// 节点工厂接口引用喵~
    /// 面向接口编程，不再依赖具体类型喵~
    /// </summary>
    public INekoGraphNodeFactory GraphView;

    /// <summary>
    /// EditorWindow 引用喵~
    /// 用于坐标转换喵~
    /// </summary>
    public EditorWindow EditorWindow;

    /// <summary>
    /// 当前系统类型喵~
    /// </summary>
    protected abstract NodeSystem CurrentNodeSystem { get; }

    /// <summary>
    /// 初始化 SearchWindow 的引用喵~
    /// </summary>
    /// <param name="editorWindow">编辑器窗口喵~</param>
    /// <param name="graphView">节点工厂接口喵~</param>
    public void Initialize(EditorWindow editorWindow, INekoGraphNodeFactory graphView)
    {
        EditorWindow = editorWindow;
        GraphView = graphView;
    }

    /// <summary>
    /// 创建搜索树喵~
    /// 通过反射自动生成树状目录喵~
    /// </summary>
    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        var nodeTypes = GetNodeTypesForCurrentSystem();
        var tree = new List<SearchTreeEntry>
        {
            new SearchTreeGroupEntry(new GUIContent("创建节点"), 0),
        };

        // 1. 先把那些没有二级菜单的节点直接放在第一层喵~
        var rootNodes = nodeTypes.Where(n => n.PathParts.Length == 1).ToList();
        foreach (var node in rootNodes)
        {
            tree.Add(new SearchTreeEntry(new GUIContent(node.MenuItemAttr.MenuPath))
            {
                level = 1,
                userData = node.NodeType
            });
        }

        // 2. 再处理有路径分组的节点喵~
        var groupedNodes = nodeTypes.Where(n => n.PathParts.Length > 1).ToList();
        var groups = groupedNodes.GroupBy(n => n.PathParts[0]).ToList();

        foreach (var group in groups)
        {
            // 添加分组标题喵~
            tree.Add(new SearchTreeGroupEntry(new GUIContent(group.Key), 1));

            // 添加组内的节点类型喵~
            foreach (var nodeType in group)
            {
                var displayName = string.Join(" / ", nodeType.PathParts.Skip(1));

                tree.Add(new SearchTreeEntry(new GUIContent($"   {displayName}"))
                {
                    level = 2,
                    userData = nodeType.NodeType
                });
            }
        }

        return tree;
    }

    /// <summary>
    /// 选择条目时调用喵~
    /// 使用接口调用 GraphView.CreateNode 方法喵~
    /// </summary>
    public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
    {
        // 空检查喵~
        if (EditorWindow == null || GraphView == null)
        {
            Debug.LogError("[BaseNodeSearchWindow] EditorWindow 或 GraphView 为 null！");
            return false;
        }

        // 获取节点类型喵~
        var nodeType = entry.userData as Type;
        if (nodeType == null)
        {
            // 如果点击的是分组标题（userData 为 null），不做处理喵~
            return false;
        }

        // 1. 获取鼠标位置（通过接口让 GraphView 去算喵！）
        var localMousePosition = GraphView.ConvertScreenToLocal(context.screenMousePosition, EditorWindow);

        // 2. 告诉画布创建节点
        GraphView.CreateNode(nodeType, localMousePosition);

        return true;
    }

    /// <summary>
    /// 获取当前系统可用的节点类型列表喵~
    /// 使用 NodeTypeHelper 静态辅助类获取喵~
    /// </summary>
    protected List<NodeTypeInfo> GetNodeTypesForCurrentSystem()
    {
        return NodeTypeHelper.GetNodeTypesForSystem(CurrentNodeSystem);
    }
}
#endif
