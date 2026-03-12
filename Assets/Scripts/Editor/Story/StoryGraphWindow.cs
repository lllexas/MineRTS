#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Collections.Generic;
using NekoGraph;
using Newtonsoft.Json;

// =========================================================
// 1. 窗口入口
// =========================================================
public class StoryGraphWindow : BaseGraphWindow<StoryGraphView, StoryPackData>
{
    private bool _rootNodeCreated;

    protected override string WindowTitle => "Story Editor";
    protected override string DefaultFileName => "New_Story.json";
    protected override string FileExtension => "json";
    protected override string FileDirectory => "Assets/Resources/Story";

    /// <summary>
    /// 指定当前系统类型为 Story 系统喵~
    /// </summary>
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Story;

    [MenuItem("Tools/猫娘助手/剧情编辑器 (Story Graph)")]
    public static void Open() => GetWindow<StoryGraphWindow>("Story Editor");

    /// <summary>
    /// 构建 GraphView 喵~
    /// </summary>
    protected override void ConstructGraphView()
    {
        // 先创建 GraphView
        GraphView = new StoryGraphView { name = GetGraphViewName() };
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
        var searchWindow = ScriptableObject.CreateInstance<StoryNodeSearchWindow>();
        searchWindow.Initialize(this, GraphView);
        return searchWindow;
    }

    protected override string GetGraphViewName() => "Story Graph";

    protected override void OnGraphViewConstructed()
    {
        // 自动创建根节点
        CreateRootNodeIfNotExists();
    }

    private void CreateRootNodeIfNotExists()
    {
        if (GraphView.RootNode == null)
        {
            // 使用基类的 CreateNode 方法创建根节点喵~
            GraphView.CreateNode(typeof(RootNode), new Vector2(100, 100));
            _rootNodeCreated = true;
        }
    }

    #region CSV Import

    private void ImportCSV()
    {
        string path = EditorUtility.OpenFilePanel("导入 CSV", "Assets/Resources/Story", "csv");
        if (string.IsNullOrEmpty(path)) return;

        var sequences = StoryCSVImporter.ImportFromCSV(path);
        if (sequences == null) return;

        GraphView.Sequences = sequences;

        // 为每个 Sequence 创建一个 Leaf 节点（可选）
        bool autoCreateLeaf = EditorUtility.DisplayDialog("自动创建叶节点",
            "是否为每个剧情序列自动创建 Leaf 节点？\n\n点击'是'自动创建\n点击'否'手动创建",
            "是", "否");

        if (autoCreateLeaf)
        {
            Vector2 startPos = new Vector2(400, 200);
            float yOffset = 80;

            for (int i = 0; i < sequences.Count; i++)
            {
                var seq = sequences[i];
                var leafData = new LeafNode_A_Data
                {
                    NodeID = System.Guid.NewGuid().ToString(),
                    ProcessID = seq.SequenceID, // 默认用 SequenceID 作为流程 ID
                };
                // 使用基类的 CreateNode 方法创建 Leaf 节点喵~
                GraphView.CreateNode(typeof(LeafNode_A), startPos + new Vector2(0, i * yOffset), leafData);
            }

            Debug.Log($"[StoryGraph] 自动创建了 {sequences.Count} 个 Leaf 节点");
        }

        Debug.Log($"[StoryGraph] CSV 导入成功！共 {sequences.Count} 个对话序列");
    }

    #endregion

    #region Save / Load

    protected override void SaveData()
    {
        string path = EditorUtility.SaveFilePanel("保存剧情", FileDirectory, DefaultFileName, FileExtension);
        if (string.IsNullOrEmpty(path)) return;

        var pack = GraphView.SerializeToPack();
        if (pack == null)
        {
            EditorUtility.DisplayDialog("保存失败", "序列化失败，请检查控制台错误喵~", "确定");
            return;
        }

        SaveToFile(path, pack);
        AssetDatabase.Refresh();

        Debug.Log($"[StoryGraph] 保存成功：{path}");
        EditorUtility.DisplayDialog("保存成功", $"剧情数据已保存至：\n{path}", "确定");
    }

    protected override StoryPackData LoadFromFile(string path)
    {
        string json = System.IO.File.ReadAllText(path);
        var pack = JsonConvert.DeserializeObject<StoryPackData>(json, BaseGraphView<StoryPackData>.GraphJsonSettings);

        if (pack == null)
        {
            EditorUtility.DisplayDialog("读取失败", "JSON 格式错误或文件损坏", "确定");
            return null;
        }

        return pack;
    }

    protected override void LoadData()
    {
        string path = EditorUtility.OpenFilePanel("读取剧情", FileDirectory, FileExtension);
        if (string.IsNullOrEmpty(path)) return;

        var pack = LoadFromFile(path);
        if (pack == null) return;

        GraphView.PopulateFromPack(pack);
        _rootNodeCreated = true;

        Debug.Log($"[StoryGraph] 读取成功：{path}");
    }

    #endregion
}

// =========================================================
// 2. Search Window（节点创建搜索框）- 使用通用基类自动生成喵~
// =========================================================
public class StoryNodeSearchWindow : BaseNodeSearchWindow
{
    /// <summary>
    /// 指定当前系统类型为 Story 系统喵~
    /// </summary>
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Story;
}
#endif
