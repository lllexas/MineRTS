#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using NekoGraph;

/// <summary>
/// 地图节点 UI - Mission 系统专用喵~
/// 【端口标签驱动·自动组装重构版】
/// 端口根据 Data 类的 [InPort]/[OutPort] 标签自动生成，无需手动创建喵~
/// </summary>
[NodeMenuItem("🎮 任务/地图节点", typeof(MapNodeData))]
[NodeType(NodeSystem.Mission)]
public class MapNode : BaseNode<MapNodeData>
{
    private TextField _mapIdField;
    private PopupField<string> _mapDropdown;
    private TextField _positionNameField;
    private Vector2IntField _positionField;

    private MissionGraphView _graphView;

    /// <summary>
    /// 无参构造函数 - 用于从菜单创建节点喵~
    /// </summary>
    public MapNode() : base()
    {
        InitializeUI();
    }

    /// <summary>
    /// 带参数构造函数 - 用于从数据加载节点喵~
    /// </summary>
    public MapNode(MapNodeData data) : base(data)
    {
        InitializeUI();
    }

    /// <summary>
    /// 初始化 UI 元素喵~
    /// 端口会自动根据 Data 类的标签"长"出来，这里只画特殊控件喵！
    /// </summary>
    private void InitializeUI()
    {
        title = "🗺️ 地图";
        style.width = 280;
        titleContainer.style.backgroundColor = new Color(0.2f, 0.5f, 0.7f); // 🔵 蓝绿色

        // --- 配置区域 ---
        var foldout = new Foldout() { text = "地图配置", value = true };

        // 地图选择下拉菜单
        var mapIds = GetAllMapIds();
        string currentMapId = string.IsNullOrEmpty(TypedData.MapID) ? "无" : TypedData.MapID;
        if (!mapIds.Contains(currentMapId)) mapIds.Add(currentMapId);

        _mapDropdown = new PopupField<string>("选择地图", mapIds, currentMapId);
        _mapDropdown.RegisterValueChangedCallback(evt =>
        {
            TypedData.MapID = evt.newValue == "无" ? "" : evt.newValue;
            if (_mapIdField != null)
            {
                _mapIdField.value = TypedData.MapID;
            }
        });
        foldout.Add(_mapDropdown);

        // 地图 ID（隐藏，用于数据同步）
        _mapIdField = new TextField("地图 ID");
        _mapIdField.value = TypedData.MapID;
        _mapIdField.RegisterValueChangedCallback(evt => TypedData.MapID = evt.newValue);
        _mapIdField.style.display = DisplayStyle.None; // 隐藏
        foldout.Add(_mapIdField);

        // 位置名称
        _positionNameField = new TextField("位置名称");
        _positionNameField.value = TypedData.PositionName;
        _positionNameField.RegisterValueChangedCallback(evt => TypedData.PositionName = evt.newValue);
        foldout.Add(_positionNameField);

        // 地图坐标
        _positionField = new Vector2IntField("选中位置");
        _positionField.value = TypedData.SelectedPosition;
        _positionField.RegisterValueChangedCallback(evt => TypedData.SelectedPosition = evt.newValue);
        foldout.Add(_positionField);

        // 选择坐标按钮
        var selectButton = new Button(() =>
        {
            if (string.IsNullOrEmpty(TypedData.MapID))
            {
                EditorUtility.DisplayDialog("错误", "请先选择地图喵~", "确定");
                return;
            }

            MapPreviewWindow.Open(TypedData.MapID, (selectedCoord) =>
            {
                TypedData.SelectedPosition = selectedCoord;
                _positionField.value = selectedCoord;

                // 更新位置别名（可选）
                if (string.IsNullOrEmpty(TypedData.PositionName))
                {
                    TypedData.PositionName = $"坐标 ({selectedCoord.x},{selectedCoord.y})";
                    _positionNameField.value = TypedData.PositionName;
                }

                Debug.Log($"[地图节点] 坐标已选择：{selectedCoord}");
            });
        })
        {
            text = "📍 选择坐标",
            style = { height = 24, marginTop = 5 }
        };
        foldout.Add(selectButton);

        extensionContainer.Add(foldout);

        RefreshExpandedState();
    }

    /// <summary>
    /// 获取所有地图 ID 列表（静态方法，可供其他类使用）喵~
    /// </summary>
    public static List<string> GetAllMapIds()
    {
        var mapIds = new List<string> { "无" };
        var allMaps = Resources.LoadAll<TextAsset>("Levels");
        foreach (var mapAsset in allMaps)
        {
            mapIds.Add(mapAsset.name);
        }
        return mapIds;
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
            if (_mapIdField != null)
            {
                _mapIdField.value = newMapId;
            }
        }
    }

    public override void UpdateData()
    {
        TypedData.MapID = _mapIdField.value;
        TypedData.PositionName = _positionNameField.value;
        TypedData.SelectedPosition = _positionField.value;
    }
}
#endif
