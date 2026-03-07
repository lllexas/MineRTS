#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;

// =========================================================
// 1. 窗口入口
// =========================================================
public class StoryGraphWindow : EditorWindow
{
    private StoryGraphView _graphView;
    private StoryNodeSearchWindow _searchWindow;
    private string _currentFilePath;
    private bool _rootNodeCreated;

    [MenuItem("Tools/猫娘助手/剧情编辑器 (Story Graph)")]
    public static void Open() => GetWindow<StoryGraphWindow>("Story Editor");

    private void OnEnable()
    {
        ConstructGraphView();
        GenerateToolbar();
    }

    private void OnDisable()
    {
        if (_graphView != null)
        {
            rootVisualElement.Remove(_graphView);
        }
    }

    private void ConstructGraphView()
    {
        _searchWindow = ScriptableObject.CreateInstance<StoryNodeSearchWindow>();
        _searchWindow.EditorWindow = this;

        _graphView = new StoryGraphView { name = "Story Graph" };
        _searchWindow.GraphView = _graphView;

        _graphView.nodeCreationRequest = context =>
            SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchWindow);

        _graphView.StretchToParentSize();
        rootVisualElement.Add(_graphView);

        // 自动创建根节点
        CreateRootNodeIfNotExists();
    }

    private void CreateRootNodeIfNotExists()
    {
        if (_graphView.RootNode == null)
        {
            _graphView.CreateRootNode(new Vector2(100, 100));
            _rootNodeCreated = true;
        }
    }

    private void GenerateToolbar()
    {
        var toolbar = new UnityEditor.UIElements.Toolbar();

        // 节点创建按钮
        toolbar.Add(new Button(() => CreateSpineNode()) { text = "➕ 树主干" });
        toolbar.Add(new Button(() => CreateLeafNode()) { text = "🍃 叶演出" });
        toolbar.Add(new Button(() => CreateTriggerNode()) { text = "🎬 触发器" });
        toolbar.Add(new Button(() => CreateCommandNode()) { text = "⚡ 命令" });

        toolbar.Add(new Button(ImportCSV) { text = "📥 导入 CSV" });
        toolbar.Add(new Button(SaveData) { text = "💾 保存 (JSON)" });
        toolbar.Add(new Button(LoadData) { text = "📂 读取 (JSON)" });

        rootVisualElement.Add(toolbar);
    }

    #region Node Creation

    private void CreateSpineNode()
    {
        var data = new SpineIDNodeData
        {
            NodeID = System.Guid.NewGuid().ToString(),
            StoryProcessID = ""
        };
        _graphView.CreateSpineNode(data, new Vector2(300, 100));
    }

    private void CreateLeafNode()
    {
        var data = new LeafIDNodeData
        {
            NodeID = System.Guid.NewGuid().ToString(),
            StoryProcessID = "",
            SequenceID = ""
        };
        _graphView.CreateLeafNode(data, new Vector2(500, 100));
    }

    private void CreateTriggerNode()
    {
        var data = new TriggerNodeData
        {
            NodeID = System.Guid.NewGuid().ToString(),
            Trigger = new TriggerData
            {
                UseEnumTrigger = true,
                TriggerType = TriggerType.Time,
                TriggerParam = "0"
            }
        };
        _graphView.CreateTriggerNode(data, new Vector2(700, 100));
    }

    private void CreateCommandNode()
    {
        var data = new CommandNodeData
        {
            NodeID = System.Guid.NewGuid().ToString(),
            Command = new CommandData
            {
                CommandType = "",
                CommandParam = ""
            }
        };
        _graphView.CreateCommandNode(data, new Vector2(900, 100));
    }

    #endregion

    #region CSV Import

    private void ImportCSV()
    {
        string path = EditorUtility.OpenFilePanel("导入 CSV", "Assets/Resources/Story", "csv");
        if (string.IsNullOrEmpty(path)) return;

        var sequences = StoryCSVImporter.ImportFromCSV(path);
        if (sequences == null) return;

        _graphView.Sequences = sequences;

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
                var leafData = new LeafIDNodeData
                {
                    NodeID = System.Guid.NewGuid().ToString(),
                    StoryProcessID = seq.SequenceID, // 默认用 SequenceID 作为故事进程 ID
                    SequenceID = seq.SequenceID
                };
                _graphView.CreateLeafNode(leafData, startPos + new Vector2(0, i * yOffset));
            }

            Debug.Log($"[StoryGraph] 自动创建了 {sequences.Count} 个 Leaf 节点");
        }

        Debug.Log($"[StoryGraph] CSV 导入成功！共 {sequences.Count} 个对话序列");
    }

    #endregion

    #region Save / Load

    private void SaveData()
    {
        string path = EditorUtility.SaveFilePanel("保存剧情", "Assets/Resources/Story", "New_Story.json", "json");
        if (string.IsNullOrEmpty(path)) return;

        var pack = _graphView.SerializeToPack();
        if (pack == null)
        {
            EditorUtility.DisplayDialog("保存失败", "请检查 Twin-ID 规则是否满足喵~", "确定");
            return;
        }

        string json = JsonUtility.ToJson(pack, true);
        System.IO.File.WriteAllText(path, json);
        AssetDatabase.Refresh();

        Debug.Log($"[StoryGraph] 保存成功：{path}");
        EditorUtility.DisplayDialog("保存成功", $"剧情数据已保存至：\n{path}", "确定");
    }

    private void LoadData()
    {
        string path = EditorUtility.OpenFilePanel("读取剧情", "Assets/Resources/Story", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = System.IO.File.ReadAllText(path);
        var pack = JsonUtility.FromJson<StoryPackData>(json);
        
        if (pack == null)
        {
            EditorUtility.DisplayDialog("读取失败", "JSON 格式错误或文件损坏", "确定");
            return;
        }

        _graphView.PopulateFromPack(pack);
        _rootNodeCreated = true;

        Debug.Log($"[StoryGraph] 读取成功：{path}");
    }

    #endregion
}

// =========================================================
// 2. Search Window（节点创建搜索框）
// =========================================================
public class StoryNodeSearchWindow : ScriptableObject, ISearchWindowProvider
{
    public StoryGraphView GraphView;
    public EditorWindow EditorWindow;

    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        var tree = new List<SearchTreeEntry>
        {
            new SearchTreeGroupEntry(new GUIContent("创建节点"), 0),
        };

        // 树主干节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("📗 树结构"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("   树主干 ID (Spine)"))
        {
            level = 2,
            userData = "SpineNode"
        });

        // 叶演出节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("🍃 叶演出"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("   叶演出 ID (Leaf)"))
        {
            level = 2,
            userData = "LeafNode"
        });

        // 触发器节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("🎬 触发器"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("   触发器 (Trigger)"))
        {
            level = 2,
            userData = "TriggerNode"
        });

        // 命令节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("⚡ 命令"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("   命令 (Command)"))
        {
            level = 2,
            userData = "CommandNode"
        });

        return tree;
    }

    public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
    {
        var windowMousePosition = EditorWindow.rootVisualElement.ChangeCoordinatesTo(
            EditorWindow.rootVisualElement.parent, context.screenMousePosition);
        var localMousePosition = EditorWindow.rootVisualElement.WorldToLocal(windowMousePosition);

        switch (SearchTreeEntry.userData.ToString())
        {
            case "SpineNode":
                var spineData = new SpineIDNodeData
                {
                    NodeID = System.Guid.NewGuid().ToString(),
                    StoryProcessID = ""
                };
                GraphView.CreateSpineNode(spineData, localMousePosition);
                return true;

            case "LeafNode":
                var leafData = new LeafIDNodeData
                {
                    NodeID = System.Guid.NewGuid().ToString(),
                    StoryProcessID = "",
                    SequenceID = ""
                };
                GraphView.CreateLeafNode(leafData, localMousePosition);
                return true;

            case "TriggerNode":
                var triggerData = new TriggerNodeData
                {
                    NodeID = System.Guid.NewGuid().ToString(),
                    Trigger = new TriggerData
                    {
                        UseEnumTrigger = true,
                        TriggerType = TriggerType.Time,
                        TriggerParam = "0"
                    }
                };
                GraphView.CreateTriggerNode(triggerData, localMousePosition);
                return true;

            case "CommandNode":
                var commandData = new CommandNodeData
                {
                    NodeID = System.Guid.NewGuid().ToString(),
                    Command = new CommandData
                    {
                        CommandType = "",
                        CommandParam = ""
                    }
                };
                GraphView.CreateCommandNode(commandData, localMousePosition);
                return true;
        }

        return false;
    }
}
#endif
