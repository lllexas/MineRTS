using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using MineRTS.BigMap;

/// <summary>
/// 大地图拓扑编辑器窗口
/// 纯 UI Toolkit 实现，不使用 GraphView
/// </summary>
public class BigMapEditorWindow : EditorWindow
{
    // 数据模型
    private BigMapSaveData _saveData = new BigMapSaveData();

    // UI 元素引用
    private MapCanvasElement _mapCanvas;
    private NodeInspectorPanel _inspectorPanel;
    private ToolbarButton _saveButton;
    private ToolbarButton _loadButton;

    // 当前选中的节点
    private BigMapNodeData _selectedNode;
    private NodeVisualElement _selectedNodeVisual;

    // 文件路径
    private string _currentFilePath;

    [MenuItem("Tools/猫娘助手/BigMapNet拓扑编辑器")]
    public static void OpenWindow()
    {
        var window = GetWindow<BigMapEditorWindow>();
        window.titleContent = new GUIContent("BigMapNet");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }

    private void OnEnable()
    {
        // 初始化数据
        _saveData = new BigMapSaveData();

        // 构建UI
        ConstructRootLayout();
        GenerateToolbar();

        // 初始化画布
        if (_mapCanvas != null)
        {
            _mapCanvas.Initialize(_saveData);
            _mapCanvas.OnNodeSelected += OnNodeSelected;
            _mapCanvas.OnNodeDeselected += OnNodeDeselected;
        }
    }

    private void OnDisable()
    {
        // 清理事件
        if (_mapCanvas != null)
        {
            _mapCanvas.OnNodeSelected -= OnNodeSelected;
            _mapCanvas.OnNodeDeselected -= OnNodeDeselected;
        }
    }

    /// <summary>
    /// 构建根布局
    /// </summary>
    private void ConstructRootLayout()
    {
        // 清空根元素
        rootVisualElement.Clear();

        // 创建主容器，使用 Flexbox 布局
        var mainContainer = new VisualElement();
        mainContainer.style.flexDirection = FlexDirection.Column;
        mainContainer.style.flexGrow = 1;

        // 主体内容区域（画布 + 属性面板）
        var contentContainer = new VisualElement();
        contentContainer.style.flexDirection = FlexDirection.Row;
        contentContainer.style.flexGrow = 1;

        // 左侧画布区域 (70%)
        var canvasContainer = new VisualElement();
        canvasContainer.name = "canvas-container";
        canvasContainer.style.flexGrow = 0.7f;
        canvasContainer.style.flexShrink = 0;
        canvasContainer.style.flexBasis = new StyleLength(new Length(70, LengthUnit.Percent));
        canvasContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1.0f);

        // 创建画布元素
        _mapCanvas = new MapCanvasElement();
        _mapCanvas.name = "map-canvas";
        _mapCanvas.style.flexGrow = 1;
        canvasContainer.Add(_mapCanvas);

        // 右侧属性面板区域 (30%)
        var inspectorContainer = new VisualElement();
        inspectorContainer.name = "inspector-container";
        inspectorContainer.style.flexGrow = 0.3f;
        inspectorContainer.style.flexShrink = 0;
        inspectorContainer.style.flexBasis = new StyleLength(new Length(30, LengthUnit.Percent));
        inspectorContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
        inspectorContainer.style.borderLeftWidth = 1;
        inspectorContainer.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);

        // 创建属性面板
        _inspectorPanel = new NodeInspectorPanel();
        _inspectorPanel.name = "node-inspector";
        _inspectorPanel.style.flexGrow = 1;
        inspectorContainer.Add(_inspectorPanel);

        // 添加到内容容器
        contentContainer.Add(canvasContainer);
        contentContainer.Add(inspectorContainer);

        // 添加到主容器
        mainContainer.Add(contentContainer);

        // 添加到根元素
        rootVisualElement.Add(mainContainer);
    }

    /// <summary>
    /// 生成工具栏
    /// </summary>
    private void GenerateToolbar()
    {
        var toolbar = new UnityEditor.UIElements.Toolbar();

        // 保存按钮
        _saveButton = new ToolbarButton(SaveData)
        {
            text = "保存 (JSON)",
            tooltip = "将当前地图拓扑保存为 JSON 文件"
        };
        toolbar.Add(_saveButton);

        // 读取按钮
        _loadButton = new ToolbarButton(LoadData)
        {
            text = "读取 (JSON)",
            tooltip = "从 JSON 文件加载地图拓扑"
        };
        toolbar.Add(_loadButton);

        // 添加分隔符
        var separator = new VisualElement();
        separator.style.width = 1;
        separator.style.marginTop = 2;
        separator.style.marginBottom = 2;
        separator.style.marginLeft = 5;
        separator.style.marginRight = 5;
        separator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        toolbar.Add(separator);

        // 清空画布按钮
        var clearButton = new ToolbarButton(() =>
        {
            if (EditorUtility.DisplayDialog("清空画布", "确定要清空所有节点和连线吗？此操作不可撤销。", "确定", "取消"))
            {
                _saveData = new BigMapSaveData();
                _mapCanvas?.Initialize(_saveData);
                _inspectorPanel?.ClearPanel();
                _selectedNode = null;
                _selectedNodeVisual = null;
            }
        })
        {
            text = "清空画布",
            tooltip = "清空所有节点和连线"
        };
        toolbar.Add(clearButton);

        // 将工具栏添加到根元素（放在最前面）
        rootVisualElement.Insert(0, toolbar);
    }

    /// <summary>
    /// 保存数据到 JSON 文件
    /// </summary>
    private void SaveData()
    {
        // 先更新画布偏移量和缩放比例
        if (_mapCanvas != null)
        {
            _saveData.CanvasOffset = _mapCanvas.CanvasOffset;
            _saveData.CanvasZoom = _mapCanvas.CanvasZoom;
        }

        // 弹出保存文件对话框
        string path = EditorUtility.SaveFilePanel("保存大地图数据", "Assets/Resources", "BigMapData.json", "json");

        if (string.IsNullOrEmpty(path))
            return;

        // 确保路径在 Assets 目录下
        if (!path.StartsWith(Application.dataPath))
        {
            EditorUtility.DisplayDialog("错误", "请将文件保存在 Assets 目录下。", "确定");
            return;
        }

        // 转换为相对路径
        _currentFilePath = "Assets" + path.Substring(Application.dataPath.Length);

        try
        {
            // 序列化为 JSON
            string json = JsonUtility.ToJson(_saveData, true);

            // 写入文件
            System.IO.File.WriteAllText(path, json);

            // 刷新资源数据库
            AssetDatabase.Refresh();

            Debug.Log($"大地图数据已保存到: {_currentFilePath}");
            ShowNotification(new GUIContent("保存成功！"));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"保存失败: {ex.Message}");
            EditorUtility.DisplayDialog("保存失败", $"保存过程中发生错误:\n{ex.Message}", "确定");
        }
    }

    /// <summary>
    /// 从 JSON 文件加载数据
    /// </summary>
    private void LoadData()
    {
        // 弹出打开文件对话框
        string defaultPath = "Assets/Resources";
        string path = EditorUtility.OpenFilePanel("加载大地图数据", defaultPath, "json");

        if (string.IsNullOrEmpty(path))
            return;

        // 确保路径在 Assets 目录下
        if (!path.StartsWith(Application.dataPath))
        {
            EditorUtility.DisplayDialog("错误", "请从 Assets 目录下选择文件。", "确定");
            return;
        }

        // 转换为相对路径
        _currentFilePath = "Assets" + path.Substring(Application.dataPath.Length);

        try
        {
            // 读取文件
            string json = System.IO.File.ReadAllText(path);

            // 反序列化
            var loadedData = JsonUtility.FromJson<BigMapSaveData>(json);

            if (loadedData == null)
            {
                EditorUtility.DisplayDialog("加载失败", "无法解析 JSON 文件。", "确定");
                return;
            }

            // 更新数据
            _saveData = loadedData;

            // 重新初始化画布
            if (_mapCanvas != null)
            {
                _mapCanvas.Initialize(_saveData);
                _mapCanvas.CanvasOffset = _saveData.CanvasOffset;
                _mapCanvas.CanvasZoom = _saveData.CanvasZoom;
            }

            // 清空属性面板
            _inspectorPanel?.Clear();
            _selectedNode = null;
            _selectedNodeVisual = null;

            Debug.Log($"大地图数据已从 {_currentFilePath} 加载");
            ShowNotification(new GUIContent("加载成功！"));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"加载失败: {ex.Message}");
            EditorUtility.DisplayDialog("加载失败", $"加载过程中发生错误:\n{ex.Message}", "确定");
        }
    }

    /// <summary>
    /// 节点被选中时的回调
    /// </summary>
    private void OnNodeSelected(BigMapNodeData nodeData, NodeVisualElement nodeVisual)
    {
        // 如果点的是一个新节点
        if (_selectedNode != nodeData)
        {
            _selectedNode = nodeData;
            _selectedNodeVisual = nodeVisual;

            // 彻底重建属性面板
            _inspectorPanel?.BindNode(nodeData);
        }
        else
        {
            // 如果选中的还是这个节点（说明是拖拽导致的坐标更新事件）
            // 不要重建 UI，只刷新数字！
            _inspectorPanel?.Refresh();
        }
    }

    /// <summary>
    /// 节点取消选中时的回调
    /// </summary>
    private void OnNodeDeselected()
    {
        _selectedNode = null;
        _selectedNodeVisual = null;

        // 清空属性面板
        _inspectorPanel?.Clear();
    }
    public void RequestRepaint()
    {
        _mapCanvas?.MarkDirtyRepaint(); // 强制画布重新调用 OnGenerateVisualContent
    }

    /// <summary>
    /// 获取当前保存的数据（供外部访问）
    /// </summary>
    public BigMapSaveData GetSaveData()
    {
        return _saveData;
    }

    /// <summary>
    /// 设置保存的数据（供外部访问）
    /// </summary>
    public void SetSaveData(BigMapSaveData data)
    {
        _saveData = data;
        _mapCanvas?.Initialize(_saveData);
    }

    /// <summary>
    /// 删除节点（供属性面板调用）
    /// </summary>
    public void DeleteNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return;

        // 调用画布的删除节点方法
        _mapCanvas?.DeleteNode(nodeId);

        // 清空属性面板
        _inspectorPanel?.ClearPanel();
        _selectedNode = null;
        _selectedNodeVisual = null;
    }

    /// <summary>
    /// 处理键盘事件
    /// </summary>
    private void OnGUI()
    {
        // 处理全局键盘快捷键
        HandleKeyboardShortcuts();
    }

    /// <summary>
    /// 处理键盘快捷键
    /// </summary>
    private void HandleKeyboardShortcuts()
    {
        var currentEvent = Event.current;
        if (currentEvent.type == EventType.KeyDown)
        {
            // Delete 键：删除选中节点
            if (currentEvent.keyCode == KeyCode.Delete && _selectedNode != null)
            {
                DeleteNode(_selectedNode.StageID);
                currentEvent.Use();
            }
        }
    }

    /// <summary>
    /// 更新节点ID（当用户在属性面板中修改节点ID时调用）
    /// </summary>
    public void UpdateNodeID(string oldID, string newID)
    {
        if (_mapCanvas != null)
        {
            _mapCanvas.UpdateNodeID(oldID, newID);
        }
    }
}