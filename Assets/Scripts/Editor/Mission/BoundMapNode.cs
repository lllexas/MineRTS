#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using NekoGraph;

/// <summary>
/// 绑定地图节点 UI - 只能选择当前任务包绑定的地图喵~
/// 当选择地图时，会同步所有普通地图节点的选择
/// </summary>
[NodeMenuItem("🎮 任务/绑定地图节点", typeof(BoundMapNodeData))]
[NodeType(NodeSystem.Mission)]
public class BoundMapNode : BaseNode<BoundMapNodeData>
{
    private PopupField<string> _mapDropdown;

    /// <summary>
    /// 无参构造函数 - 用于从菜单创建节点喵~
    /// </summary>
    public BoundMapNode() : base()
    {
        InitializeUI();
    }

    /// <summary>
    /// 带参数构造函数 - 用于从数据加载节点喵~
    /// </summary>
    public BoundMapNode(BoundMapNodeData data) : base(data)
    {
        InitializeUI();
    }

    /// <summary>
    /// 初始化 UI 元素喵~
    /// </summary>
    private void InitializeUI()
    {
        title = "🔗 绑定地图";
        style.width = 250;
        titleContainer.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f); // 🟢 绿色

        // 绑定地图节点没有输入输出端口

        // --- 配置区域 ---
        var foldout = new Foldout() { text = "绑定地图配置", value = true };

        // 地图选择下拉菜单
        var mapIds = MapNode.GetAllMapIds();
        string currentMapId = string.IsNullOrEmpty(TypedData.MapID) ? "无" : TypedData.MapID;
        if (!mapIds.Contains(currentMapId)) mapIds.Add(currentMapId);

        _mapDropdown = new PopupField<string>("绑定地图", mapIds, currentMapId);
        _mapDropdown.RegisterValueChangedCallback(evt =>
        {
            string newMapId = evt.newValue == "无" ? "" : evt.newValue;
            TypedData.MapID = newMapId;

            // 同步所有地图节点的选择喵~
            SyncAllMapNodes(newMapId);
        });
        foldout.Add(_mapDropdown);

        // 提示信息
        var infoLabel = new Label("绑定地图节点用于指定任务包绑定的地图\n所有普通地图节点会同步此选择")
        {
            style = {
                fontSize = 9,
                marginTop = 5,
                color = Color.yellow
            }
        };
        foldout.Add(infoLabel);

        extensionContainer.Add(foldout);

        RefreshExpandedState();
    }

    /// <summary>
    /// 同步所有地图节点的选择喵~
    /// </summary>
    /// <param name="mapId">要同步的地图 ID</param>
    public static void SyncAllMapNodes(string mapId)
    {
        // 查找当前打开的 MissionGraphWindow
        var graphWindow = EditorWindow.GetWindow<MissionGraphWindow>();
        if (graphWindow == null) return;

        // 获取 GraphView 并同步所有地图节点
        var graphView = graphWindow.GetGraphView();
        if (graphView != null)
        {
            graphView.SetCurrentMapId(mapId);
            Debug.Log($"[BoundMapNode] 已同步所有地图节点到地图：{mapId}");
        }
    }

    /// <summary>
    /// 更新地图 ID 显示喵~
    /// </summary>
    public void UpdateMapId(string newMapId)
    {
        if (_mapDropdown != null)
        {
            string dropdownValue = string.IsNullOrEmpty(newMapId) ? "无" : newMapId;

            var choices = _mapDropdown.choices;
            if (choices.Contains(dropdownValue))
            {
                _mapDropdown.value = dropdownValue;
            }
            else
            {
                choices.Add(dropdownValue);
                _mapDropdown.choices = choices;
                _mapDropdown.value = dropdownValue;
            }

            TypedData.MapID = newMapId;
        }
    }

    public override void UpdateData()
    {
        // 地图 ID 已经通过下拉菜单回调更新到 TypedData.MapID
        // 这里只需要确保 NodeID 同步即可喵~
        TypedData.NodeID = guid;
    }
}
#endif
