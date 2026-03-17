#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSGraphWindow - VFS 编辑器窗口喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 继承自 BaseGraphWindow<VFSGraphView, VFSPackData>
/// 只需要重写几个属性喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[Obsolete("已弃用：请使用 NekoGraphWindow（NekoGraph/✨ 打开新编辑器窗口）来代替。")]
public class VFSGraphWindow : BaseGraphWindow<VFSGraphView, VFSPackData>
{
    protected override string WindowTitle => "VFS Editor";
    protected override string DefaultFileName => "New_VFS.json";
    protected override string FileExtension => "json";
    protected override string FileDirectory => "Assets/Resources/NekoGraph/GraphVSF/Packs";
    protected override NodeSystem CurrentNodeSystem => NodeSystem.VFS;

    [MenuItem("Tools/猫娘助手/VFS 编辑器")]
    public static void Open() => GetWindow<VFSGraphWindow>("VFS Editor");

    /// <summary>
    /// 构建 GraphView 喵~
    /// 创建 SearchWindow 并注册右键菜单回调喵！
    /// </summary>
    protected override void ConstructGraphView()
    {
        // 创建 GraphView
        GraphView = new VFSGraphView { name = GetGraphViewName() };
        GraphView.StretchToParentSize();
        rootVisualElement.Add(GraphView);

        // 创建 SearchWindow 并赋值引用
        SearchWindowProvider = CreateSearchWindow();
        if (SearchWindowProvider != null)
        {
            SetupSearchWindow();  // 注册右键菜单回调喵~
        }

        OnGraphViewConstructed();
    }

    /// <summary>
    /// 创建 SearchWindow 提供者喵~
    /// </summary>
    protected override ScriptableObject CreateSearchWindow()
    {
        var searchWindow = ScriptableObject.CreateInstance<VFSNodeSearchWindow>();
        searchWindow.Initialize(this, GraphView);
        return searchWindow;
    }

    /// <summary>
    /// 获取 GraphView 名称喵~
    /// </summary>
    protected override string GetGraphViewName() => "VFS Graph";

    /// <summary>
    /// GraphView 构建完成回调喵~
    /// </summary>
    protected override void OnGraphViewConstructed()
    {
        // VFS 编辑器暂无特殊初始化喵~
    }
}

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSNodeSearchWindow - VFS 节点搜索窗口喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 继承自 BaseNodeSearchWindow
/// 显示 VFS 系统的节点（VFSNode、RootNode 等）喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class VFSNodeSearchWindow : BaseNodeSearchWindow
{
    protected override NodeSystem CurrentNodeSystem => NodeSystem.VFS;
}
#endif
