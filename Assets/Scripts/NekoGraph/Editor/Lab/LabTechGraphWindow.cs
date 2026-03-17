#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Collections.Generic;
using NekoGraph;

// =========================================================
// Lab 科技树编辑器窗口入口
// =========================================================

/// <summary>
/// Lab 科技树编辑器窗口 - 继承 BaseGraphWindow 喵~
/// </summary>
[Obsolete("已弃用：请使用 NekoGraphWindow（NekoGraph/✨ 打开新编辑器窗口）来代替。")]
public class LabTechGraphWindow : BaseGraphWindow<LabTechGraphView, LabTechPackData>
{
    protected override string WindowTitle => "Lab Tech Editor";
    protected override string DefaultFileName => "New_LabTech.json";
    protected override string FileExtension => "json";
    protected override string FileDirectory => "Assets/Resources/Lab";

    /// <summary>
    /// 指定当前系统类型为 Lab 科技树系统喵~
    /// </summary>
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Lab;

    [MenuItem("Tools/猫娘助手/科技树编辑器 (Lab Tech Graph)")]
    public static void Open() => GetWindow<LabTechGraphWindow>("Lab Tech Editor");

    /// <summary>
    /// 构建 GraphView 喵~
    /// </summary>
    protected override void ConstructGraphView()
    {
        // 先创建 GraphView
        GraphView = new LabTechGraphView { name = GetGraphViewName() };
        GraphView.StretchToParentSize();
        rootVisualElement.Add(GraphView);

        // 再创建 SearchWindow 并赋值引用
        SearchWindowProvider = CreateSearchWindow();
        if (SearchWindowProvider != null)
        {
            SetupSearchWindow();
        }

        OnGraphViewConstructed();
    }

    protected override ScriptableObject CreateSearchWindow()
    {
        var searchWindow = ScriptableObject.CreateInstance<LabTechNodeSearchWindow>();
        searchWindow.Initialize(this, GraphView);
        return searchWindow;
    }

    protected override string GetGraphViewName() => "Lab Tech Graph";

    /// <summary>
    /// GraphView 构建完成回调喵~
    /// </summary>
    protected override void OnGraphViewConstructed()
    {
        // Lab 科技树系统暂无特殊初始化喵~
    }

    #region Save / Load

    // 使用基类的 SaveData() 模板方法，不重写
    // 如果有特殊需求，可以重写 OnAfterSerialize() 来填充特殊字段

    // 使用基类的 LoadData() 模板方法，不重写
    // PackID 会自动同步到输入框

    #endregion
}

// =========================================================
// Search Window（节点创建搜索框）
// =========================================================

/// <summary>
/// Lab 科技树节点搜索窗口 - 继承 BaseNodeSearchWindow 喵~
/// </summary>
public class LabTechNodeSearchWindow : BaseNodeSearchWindow
{
    /// <summary>
    /// 指定当前系统类型为 Lab 科技树系统喵~
    /// </summary>
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Lab;
}

#endif
