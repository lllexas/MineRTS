#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using NekoGraph;
using Newtonsoft.Json;

// =========================================================
// MsgGraphWindow - 消息编辑器窗口喵~
// =========================================================
/// <summary>
/// 社交消息编辑器窗口 - 用于编辑社交对话图喵~
/// 【msgPack】专用编辑器
/// </summary>
public class MsgGraphWindow : BaseGraphWindow<MsgGraphView, MsgPackData>
{
    protected override string WindowTitle => "Msg Editor";
    protected override string DefaultFileName => "New_Msg.json";
    protected override string FileExtension => "json";
    protected override string FileDirectory => "Assets/Resources/NekoGraph/Packs/Social";

    /// <summary>
    /// 指定当前系统类型为 Social 系统喵~
    /// </summary>
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Social;

    [MenuItem("Tools/猫娘助手/📝 打开消息编辑器 (Msg Graph)")]
    public static void Open() => GetWindow<MsgGraphWindow>("Msg Editor");

    /// <summary>
    /// 构建 GraphView 喵~
    /// </summary>
    protected override void ConstructGraphView()
    {
        GraphView = new MsgGraphView { name = GetGraphViewName() };
        GraphView.StretchToParentSize();
        rootVisualElement.Add(GraphView);

        SearchWindowProvider = CreateSearchWindow();
        if (SearchWindowProvider != null)
        {
            SetupSearchWindow();
        }

        OnGraphViewConstructed();
    }

    protected override ScriptableObject CreateSearchWindow()
    {
        var searchWindow = ScriptableObject.CreateInstance<MsgNodeSearchWindow>();
        searchWindow.Initialize(this, GraphView);
        return searchWindow;
    }

    protected override string GetGraphViewName() => "Msg Graph";

    protected override void OnGraphViewConstructed()
    {
        CreateRootNodeIfNotExists();
    }

    private void CreateRootNodeIfNotExists()
    {
        if (GraphView.RootNode == null)
        {
            // 自动创建一个根节点喵~
            GraphView.CreateNode(typeof(RootNode), new Vector2(100, 100));
        }
    }

    #region Save / Load

    // 使用基类的 SaveData() 模板方法，不重写
    // 如果有特殊需求，可以重写 OnAfterSerialize() 来填充特殊字段

    protected override MsgPackData LoadFromFile(string path)
    {
        string json = System.IO.File.ReadAllText(path);
        var pack = JsonConvert.DeserializeObject<MsgPackData>(json, BaseGraphView<MsgPackData>.GraphJsonSettings);
        return pack;
    }

    // 使用基类的 LoadData() 模板方法，不重写
    // PackID 会自动同步到输入框

    #endregion
}

// =========================================================
// Search Window（节点创建搜索框）
// =========================================================
public class MsgNodeSearchWindow : BaseNodeSearchWindow
{
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Social;
}
#endif
