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
// Mission Graph 窗口入口
// =========================================================
public class MissionGraphWindow : BaseGraphWindow<MissionGraphView, MissionPackData>
{
    protected override string WindowTitle => "Mission Editor";
    protected override string DefaultFileName => "New_Mission.json";
    protected override string FileExtension => "json";
    protected override string FileDirectory => "Assets/Resources/Mission";

    /// <summary>
    /// 指定当前系统类型为 Mission 系统喵~
    /// </summary>
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Mission;

    [MenuItem("Tools/猫娘助手/任务编辑器 (Mission Graph)")]
    public static void Open() => GetWindow<MissionGraphWindow>("Mission Editor");

    /// <summary>
    /// 构建 GraphView 喵~
    /// </summary>
    protected override void ConstructGraphView()
    {
        // 先创建 GraphView
        GraphView = new MissionGraphView { name = GetGraphViewName() };
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
        var searchWindow = ScriptableObject.CreateInstance<MissionNodeSearchWindow>();
        searchWindow.Initialize(this, GraphView);
        return searchWindow;
    }

    protected override string GetGraphViewName() => "Mission Graph";

    /// <summary>
    /// GraphView 构建完成回调喵~
    /// </summary>
    protected override void OnGraphViewConstructed()
    {
        // 初始化当前地图 ID（如果有需要的话）
        if (GraphView.CurrentMapId != null)
        {
            GraphView.SetCurrentMapId(GraphView.CurrentMapId);
        }
    }

    #region Save / Load

    protected override void SaveData()
    {
        string path = EditorUtility.SaveFilePanel("保存任务", FileDirectory, DefaultFileName, FileExtension);
        if (string.IsNullOrEmpty(path)) return;

        var pack = GraphView.SerializeToPack();
        if (pack == null)
        {
            EditorUtility.DisplayDialog("保存失败", "数据验证失败，请检查是否符合规则喵~", "确定");
            return;
        }

        SaveToFile(path, pack);
        AssetDatabase.Refresh();

        Debug.Log($"[MissionGraph] 保存成功：{path}");
        EditorUtility.DisplayDialog("保存成功", $"任务数据已保存至：\n{path}", "确定");
    }

    protected override void LoadData()
    {
        string path = EditorUtility.OpenFilePanel("读取任务", FileDirectory, FileExtension);
        if (string.IsNullOrEmpty(path)) return;

        var pack = LoadFromFile(path);
        if (pack == null)
        {
            EditorUtility.DisplayDialog("读取失败", "JSON 格式错误或文件损坏喵~", "确定");
            return;
        }

        GraphView.PopulateFromPack(pack);
        CurrentFilePath = path;

        Debug.Log($"[MissionGraph] 读取成功：{path}");
    }

    #endregion
}

// =========================================================
// Search Window（节点创建搜索框）- 使用通用基类自动生成喵~
// =========================================================
public class MissionNodeSearchWindow : BaseNodeSearchWindow
{
    /// <summary>
    /// 指定当前系统类型为 Mission 系统喵~
    /// </summary>
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Mission;
}
#endif
