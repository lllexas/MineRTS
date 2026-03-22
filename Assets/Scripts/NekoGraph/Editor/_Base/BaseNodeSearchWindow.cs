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
/// 节点搜索窗口 - 具体类，无子类喵~
/// Pack 自己注册 GetNodeSystem()，SearchWindow 直接从 Pack 拿节点列表喵~
/// </summary>
public class NodeSearchWindow : ScriptableObject, ISearchWindowProvider
{
    public INekoGraphNodeFactory GraphView;
    public EditorWindow EditorWindow;
    private BasePackData _pack;

    public void Initialize(EditorWindow editorWindow, INekoGraphNodeFactory graphView, BasePackData pack)
    {
        EditorWindow = editorWindow;
        GraphView = graphView;
        _pack = pack;
    }

    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        var nodeTypes = NodeTypeHelper.GetNodeTypesForSystem(_pack.System);
        var tree = new List<SearchTreeEntry>
        {
            new SearchTreeGroupEntry(new GUIContent("创建节点"), 0)
        };

        foreach (var node in nodeTypes.Where(n => n.PathParts.Length == 1))
        {
            tree.Add(new SearchTreeEntry(new GUIContent(node.MenuItemAttr.MenuPath))
                { level = 1, userData = node.NodeType });
        }

        foreach (var group in nodeTypes.Where(n => n.PathParts.Length > 1).GroupBy(n => n.PathParts[0]))
        {
            tree.Add(new SearchTreeGroupEntry(new GUIContent(group.Key), 1));
            foreach (var nodeType in group)
            {
                tree.Add(new SearchTreeEntry(new GUIContent($"   {string.Join(" / ", nodeType.PathParts.Skip(1))}"))
                    { level = 2, userData = nodeType.NodeType });
            }
        }

        return tree;
    }

    public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
    {
        if (EditorWindow == null || GraphView == null) return false;
        if (entry.userData is not Type nodeType) return false;
        GraphView.CreateNode(nodeType, GraphView.ConvertScreenToLocal(context.screenMousePosition, EditorWindow));
        return true;
    }
}
#endif
