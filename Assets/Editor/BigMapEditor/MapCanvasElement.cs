using System;
using System.Collections.Generic;
using MineRTS.BigMap;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class MapCanvasElement : VisualElement
{
    private BigMapSaveData _saveData;

    private VisualElement _contentContainer;
    private Vector2 _canvasOffset = Vector2.zero;
    private float _canvasZoom = 1.0f;

    private bool _isPanning = false;
    private Vector2 _panStartPosition;
    private Vector2 _panStartOffset;

    private Dictionary<string, NodeVisualElement> _nodeVisuals = new Dictionary<string, NodeVisualElement>();
    private NodeVisualElement _selectedNode;

    public delegate void NodeSelectedHandler(BigMapNodeData nodeData, NodeVisualElement nodeVisual);
    public delegate void NodeDeselectedHandler();
    public event NodeSelectedHandler OnNodeSelected;
    public event NodeDeselectedHandler OnNodeDeselected;

    public Vector2 CanvasOffset { get => _canvasOffset; set { _canvasOffset = value; UpdateTransform(); } }
    public float CanvasZoom { get => _canvasZoom; set { _canvasZoom = Mathf.Clamp(value, 0.1f, 5.0f); UpdateTransform(); } }
    private IMGUIContainer _guiContainer;
    public MapCanvasElement()
    {
        style.flexGrow = 1;
        style.overflow = Overflow.Hidden;
        style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1.0f);

        _contentContainer = new VisualElement();
        _contentContainer.style.position = Position.Absolute;
        _contentContainer.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _contentContainer.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        Add(_contentContainer);

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<WheelEvent>(OnWheel);

        // 监听键盘按下的事件（全局捕获）
        RegisterCallback<KeyDownEvent>(OnKeyDown);
        // 必须设置这个，不然它收不到键盘事件
        focusable = true;

        // 【新增】创建一个铺满的 IMGUI 容器用于绘制边缘的刻度文字
        _guiContainer = new IMGUIContainer(OnGUIDrawTicks);
        _guiContainer.style.position = Position.Absolute;
        _guiContainer.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _guiContainer.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        // 必须设置不响应鼠标事件，否则会挡住下面的拖拽和滚轮！
        _guiContainer.pickingMode = PickingMode.Ignore;
        Add(_guiContainer);

        // 【关键修复】彻底废弃 ContextClickEvent！防误触。

        generateVisualContent += OnGenerateVisualContent;
        UpdateTransform();
    }

    public void Initialize(BigMapSaveData saveData)
    {
        _saveData = saveData ?? new BigMapSaveData();
        foreach (var nodeVisual in _nodeVisuals.Values) nodeVisual.RemoveFromHierarchy();
        _nodeVisuals.Clear();
        _selectedNode = null;

        foreach (var nodeData in _saveData.Nodes) CreateNodeVisual(nodeData);
        MarkDirtyRepaint();
    }

    private void CreateNodeVisual(BigMapNodeData nodeData)
    {
        var nodeVisual = new NodeVisualElement(nodeData);

        // 【关键修复】绑定新的拖拽事件
        nodeVisual.OnDragMoved += OnNodeDragMoved;

        nodeVisual.OnSelected += OnNodeVisualSelected;
        nodeVisual.OnRightClick += OnNodeRightClick;

        _contentContainer.Add(nodeVisual);
        _nodeVisuals[nodeData.StageID] = nodeVisual;
        UpdateNodePosition(nodeVisual);
    }

    public void CreateNodeAtPosition(Vector2 localPosition)
    {
        if (_saveData == null) return;
        Vector2 logicPosition = LocalToLogic(localPosition);
        var newNode = new BigMapNodeData(logicPosition, $"节点{_saveData.Nodes.Count + 1}");
        _saveData.Nodes.Add(newNode);
        CreateNodeVisual(newNode);
        OnNodeVisualSelected(newNode.StageID);
    }

    public void DeleteNode(string nodeId)
    {
        if (!_nodeVisuals.TryGetValue(nodeId, out var nodeVisual)) return;
        nodeVisual.RemoveFromHierarchy();
        _nodeVisuals.Remove(nodeId);
        _saveData.Nodes.RemoveAll(n => n.StageID == nodeId);
        _saveData.Edges.RemoveAll(e => e.FromNodeID == nodeId || e.ToNodeID == nodeId);

        if (_selectedNode != null && _selectedNode.NodeData.StageID == nodeId)
        {
            _selectedNode = null;
            OnNodeDeselected?.Invoke();
        }
        MarkDirtyRepaint();
    }

    /// <summary>
    /// 更新节点ID并同步所有引用
    /// </summary>
    public void UpdateNodeID(string oldID, string newID)
    {
        if (string.IsNullOrEmpty(oldID) || string.IsNullOrEmpty(newID) || oldID == newID)
            return;

        // 检查新ID是否已存在（排除自身）
        if (_saveData.Nodes.Exists(n => n.StageID == newID && n.StageID != oldID))
        {
            Debug.LogWarning($"节点ID '{newID}' 已存在，无法更新");
            return;
        }

        // 更新节点数据中的ID
        var node = _saveData.Nodes.Find(n => n.StageID == oldID);
        if (node != null)
        {
            node.StageID = newID;
        }

        // 更新_nodeVisuals字典
        if (_nodeVisuals.TryGetValue(oldID, out var visual))
        {
            _nodeVisuals.Remove(oldID);
            _nodeVisuals[newID] = visual;
            visual.NodeData.StageID = newID; // 确保节点视觉元素的数据同步
        }

        // 更新所有边中的引用
        foreach (var edge in _saveData.Edges)
        {
            if (edge.FromNodeID == oldID) edge.FromNodeID = newID;
            if (edge.ToNodeID == oldID) edge.ToNodeID = newID;
        }

        // 如果当前选中的节点是这个节点，更新_selectedNode引用
        if (_selectedNode != null && _selectedNode.NodeData.StageID == oldID)
        {
            _selectedNode.NodeData.StageID = newID;
        }

        // 触发重绘
        MarkDirtyRepaint();
    }

    // ==========================================
    // 核心数学：绝不使用双重缩放，只有数学映射！
    // ==========================================
    // 【关键修改】全局定义：1 个业务逻辑单位 = 100 个屏幕像素
    private const float PIXELS_PER_UNIT = 100.0f;

    // 逻辑坐标 -> 屏幕像素：先放大 100 倍，再应用缩放和平移
    // 逻辑坐标(Y向上) -> 屏幕像素(Y向下)：先翻转Y轴，再放大、缩放、平移
    private Vector2 LogicToLocal(Vector2 logicPos)
    {
        Vector2 invertedLogic = new Vector2(logicPos.x, -logicPos.y); // 【修改点】翻转 Y 轴
        return (invertedLogic * PIXELS_PER_UNIT) * _canvasZoom + _canvasOffset;
    }

    // 屏幕像素(Y向下) -> 逻辑坐标(Y向上)：先撤销平移、缩放、缩小，最后翻转Y轴
    private Vector2 LocalToLogic(Vector2 localPos)
    {
        Vector2 rawLogic = ((localPos - _canvasOffset) / _canvasZoom) / PIXELS_PER_UNIT;
        return new Vector2(rawLogic.x, -rawLogic.y); // 【修改点】翻转 Y 轴
    }

    private void UpdateTransform()
    {
        // 更新所有节点位置
        foreach (var nodeVisual in _nodeVisuals.Values) UpdateNodePosition(nodeVisual);

        // 1. 标记当前画布重绘（画网格和连线）
        MarkDirtyRepaint();

        // 2. 【关键修复】在这里标记 IMGUI 重绘文字，而不是在渲染回调里
        if (_guiContainer != null) _guiContainer.MarkDirtyRepaint();
    }

    private void UpdateNodePosition(NodeVisualElement nodeVisual)
    {
        if (nodeVisual == null || nodeVisual.NodeData == null) return;
        Vector2 localPos = LogicToLocal(nodeVisual.NodeData.Position);
        nodeVisual.style.left = localPos.x - nodeVisual.style.width.value.value / 2;
        nodeVisual.style.top = localPos.y - nodeVisual.style.height.value.value / 2;
    }

    // 获取纯净的本地坐标，用于画线
    private bool GetEdgeLocalPoints(string fromId, string toId, out Vector2 fromPos, out Vector2 toPos)
    {
        fromPos = toPos = Vector2.zero;
        if (!_nodeVisuals.TryGetValue(fromId, out var fromNode) || !_nodeVisuals.TryGetValue(toId, out var toNode)) return false;
        fromPos = LogicToLocal(fromNode.NodeData.Position);
        toPos = LogicToLocal(toNode.NodeData.Position);
        return true;
    }

    // ==========================================
    // 渲染：网格与连线
    // ==========================================
    private void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        DrawGridAndAxes(painter); // 画纯线条

        if (_saveData != null && _saveData.Edges.Count > 0)
        {
            foreach (var edge in _saveData.Edges)
            {
                if (GetEdgeLocalPoints(edge.FromNodeID, edge.ToNodeID, out Vector2 fromPos, out Vector2 toPos))
                {
                    painter.lineWidth = 2.0f;
                    painter.strokeColor = edge.Direction == EdgeDirection.Bidirectional
                        ? new Color(0.4f, 0.8f, 1.0f, 0.8f) : new Color(1.0f, 0.6f, 0.2f, 0.8f);

                    painter.BeginPath();
                    painter.MoveTo(fromPos);
                    painter.LineTo(toPos);
                    painter.Stroke();
                }
            }
        }
    }

    private void DrawGridAndAxes(Painter2D painter)
    {
        // 逻辑坐标步长为 1
        float logicGridSize = 1.0f;

        var canvasRect = this.worldBound;
        if (canvasRect.width <= 0 || canvasRect.height <= 0) return;

        // 【修改点：分离包围盒计算，修复Y轴颠倒】
        Vector2 topLeftLogic = LocalToLogic(Vector2.zero);
        Vector2 bottomRightLogic = LocalToLogic(new Vector2(canvasRect.width, canvasRect.height));

        float logicMinX = topLeftLogic.x - logicGridSize;
        float logicMaxX = bottomRightLogic.x + logicGridSize;
        float logicMinY = bottomRightLogic.y - logicGridSize; // 下边的逻辑Y值更小
        float logicMaxY = topLeftLogic.y + logicGridSize;     // 上边的逻辑Y值更大

        // 画辅助网格线（淡色）
        painter.lineWidth = 1.0f;
        painter.strokeColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        // 画竖线 (X轴方向循环)
        float startX = Mathf.Floor(logicMinX / logicGridSize) * logicGridSize;
        float endX = Mathf.Ceil(logicMaxX / logicGridSize) * logicGridSize;
        for (float x = startX; x <= endX; x += logicGridSize)
        {
            painter.BeginPath();
            painter.MoveTo(LogicToLocal(new Vector2(x, logicMinY)));
            painter.LineTo(LogicToLocal(new Vector2(x, logicMaxY)));
            painter.Stroke();
        }

        // 画横线 (Y轴方向循环)
        float startY = Mathf.Floor(logicMinY / logicGridSize) * logicGridSize;
        float endY = Mathf.Ceil(logicMaxY / logicGridSize) * logicGridSize;
        for (float y = startY; y <= endY; y += logicGridSize)
        {
            painter.BeginPath();
            painter.MoveTo(LogicToLocal(new Vector2(logicMinX, y)));
            painter.LineTo(LogicToLocal(new Vector2(logicMaxX, y)));
            painter.Stroke();
        }

        // 画绝对原点坐标轴 (加粗)
        painter.lineWidth = 2.0f;
        painter.strokeColor = new Color(0.5f, 0.2f, 0.2f, 0.8f); // X轴红
        painter.BeginPath();
        painter.MoveTo(LogicToLocal(new Vector2(logicMinX, 0)));
        painter.LineTo(LogicToLocal(new Vector2(logicMaxX, 0)));
        painter.Stroke();

        painter.strokeColor = new Color(0.2f, 0.5f, 0.2f, 0.8f); // Y轴绿
        painter.BeginPath();
        painter.MoveTo(LogicToLocal(new Vector2(0, logicMinY)));
        painter.LineTo(LogicToLocal(new Vector2(0, logicMaxY)));
        painter.Stroke();
    }

    /// <summary>
    /// 使用老式的 GUI 绘制悬浮刻度，实现“轴上优先，出界边缘吸附”
    /// </summary>
    private void OnGUIDrawTicks()
    {
        if (Event.current.type != EventType.Repaint) return;

        var canvasRect = this.layout;
        if (canvasRect.width <= 0) return;

        // 【修改点：分离包围盒计算，修复Y轴颠倒】
        Vector2 topLeftLogic = LocalToLogic(Vector2.zero);
        Vector2 bottomRightLogic = LocalToLogic(new Vector2(canvasRect.width, canvasRect.height));

        float logicMinX = topLeftLogic.x;
        float logicMaxX = bottomRightLogic.x;
        float logicMinY = bottomRightLogic.y; // 下边的逻辑Y值更小
        float logicMaxY = topLeftLogic.y;     // 上边的逻辑Y值更大

        GUIStyle tickStyle = new GUIStyle(EditorStyles.miniLabel);
        tickStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);

        float logicGridSize = 1.0f;

        // 1. X 轴刻度
        float realXAxisScreenY = LogicToLocal(new Vector2(0, 0)).y;
        float edgeY = Mathf.Clamp(realXAxisScreenY + 2f, 5f, canvasRect.height - 25f);
        tickStyle.alignment = TextAnchor.UpperCenter;

        float startX = Mathf.Floor(logicMinX / logicGridSize) * logicGridSize;
        float endX = Mathf.Ceil(logicMaxX / logicGridSize) * logicGridSize;

        for (float x = startX; x <= endX; x += logicGridSize)
        {
            int displayUnitX = Mathf.RoundToInt(x);
            Vector2 localPos = LogicToLocal(new Vector2(x, 0));
            Rect rect = new Rect(localPos.x - 25, edgeY, 50, 20);
            GUI.Label(rect, displayUnitX.ToString(), tickStyle);
        }

        // 2. Y 轴刻度
        float realYAxisScreenX = LogicToLocal(new Vector2(0, 0)).x;
        float edgeX = Mathf.Clamp(realYAxisScreenX + 5f, 5f, canvasRect.width - 40f);
        tickStyle.alignment = TextAnchor.MiddleLeft;

        float startY = Mathf.Floor(logicMinY / logicGridSize) * logicGridSize;
        float endY = Mathf.Ceil(logicMaxY / logicGridSize) * logicGridSize;

        for (float y = startY; y <= endY; y += logicGridSize)
        {
            int displayUnitY = Mathf.RoundToInt(y);
            if (displayUnitY == 0) continue;

            Vector2 localPos = LogicToLocal(new Vector2(0, y));
            Rect rect = new Rect(edgeX, localPos.y - 10, 50, 20);
            GUI.Label(rect, displayUnitY.ToString(), tickStyle);
        }
    }

    // ==========================================
    // 交互逻辑
    // ==========================================
    private void OnKeyDown(KeyDownEvent evt)
    {
        // 按下了 Delete 键，且有选中的节点
        if (evt.keyCode == KeyCode.Delete && _selectedNode != null)
        {
            string nodeIdToDelete = _selectedNode.NodeData.StageID;

            // 弹窗确认一下比较安全
            if (UnityEditor.EditorUtility.DisplayDialog("删除节点",
                $"确定要删除节点 '{_selectedNode.NodeData.DisplayName}' 及其所有连线吗？", "确定", "取消"))
            {
                DeleteNode(nodeIdToDelete);
            }

            evt.StopPropagation();
        }
    }


    private void OnPointerDown(PointerDownEvent evt)
    {
        this.Focus(); // 加在 OnPointerDown 最前面
        if (evt.target is NodeVisualElement) return;

        if (evt.button == 2) // 中键平移
        {
            _isPanning = true;
            _panStartPosition = (Vector2)evt.localPosition;
            _panStartOffset = _canvasOffset;
            this.CapturePointer(evt.pointerId);
        }
        else if (evt.button == 1) // 右键：弹出菜单
        {
            // 记录鼠标弹出的本地坐标，供菜单回调使用
            Vector2 clickLocalPos = (Vector2)evt.localPosition;

            // 创建 Unity 原生右键菜单
            GenericMenu menu = new GenericMenu();

            // 添加菜单项：新建节点
            menu.AddItem(new GUIContent("在此处创建据点节点 (Create Node)"), false, () =>
            {
                CreateNodeAtPosition(clickLocalPos);
            });

            // 可选：加个清理选中项的功能
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("取消选择 (Deselect All)"), false, () =>
            {
                if (_selectedNode != null)
                {
                    _selectedNode.SetSelected(false);
                    _selectedNode = null;
                    OnNodeDeselected?.Invoke();
                }
            });

            // 显示菜单
            menu.ShowAsContext();

            // 拦截事件
            evt.StopPropagation();
        }
        else if (evt.button == 0) // 左键点空白处：取消选中
        {
            if (_selectedNode != null)
            {
                _selectedNode.SetSelected(false);
                _selectedNode = null;
                OnNodeDeselected?.Invoke();
            }
        }
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (_isPanning)
        {
            Vector2 delta = (Vector2)evt.localPosition - _panStartPosition;
            _canvasOffset = _panStartOffset + delta;
            UpdateTransform();
        }
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (_isPanning) { _isPanning = false; this.ReleasePointer(evt.pointerId); }
    }

    private void OnWheel(WheelEvent evt)
    {
        float zoomDelta = -evt.delta.y * 0.05f;
        float oldZoom = _canvasZoom;

        CanvasZoom *= (1 + zoomDelta);

        // 【修改点】在 UI 纯像素空间计算缩放偏移，避免受到逻辑Y轴翻转的干扰
        Vector2 localMousePos = evt.localMousePosition;
        Vector2 mouseRawUI = (localMousePos - _canvasOffset) / oldZoom;
        _canvasOffset = localMousePos - mouseRawUI * _canvasZoom;

        UpdateTransform();
        evt.StopPropagation();
    }

    // ==========================================
    // 节点事件回调
    // ==========================================
    private void OnNodeDragMoved(NodeVisualElement nodeVisual, Vector2 screenDelta)
    {
        Vector2 logicDelta = (screenDelta / _canvasZoom) / PIXELS_PER_UNIT;
        logicDelta.y = -logicDelta.y; // 【修改点】屏幕向下的拖拽，等于逻辑Y轴的减小
        nodeVisual.NodeData.Position = nodeVisual.DragStartLogicPosition + logicDelta;

        UpdateNodePosition(nodeVisual);
        MarkDirtyRepaint();

        // 【新增】如果正在拖拽的是选中的节点，通知外部（Inspector）刷新数据
        if (_selectedNode == nodeVisual)
        {
            // 通过触发选中事件，让顶层 Window 把新数据再塞给 Inspector
            OnNodeSelected?.Invoke(nodeVisual.NodeData, nodeVisual);
        }
    }

    private void OnNodeVisualSelected(string nodeId)
    {
        if (!_nodeVisuals.TryGetValue(nodeId, out var nodeVisual)) return;

        // 1. 取消之前选中的节点
        if (_selectedNode != null && _selectedNode != nodeVisual)
        {
            _selectedNode.SetSelected(false);
            // 旧节点变小了，也要重新对齐中心
            UpdateNodePosition(_selectedNode);
        }

        // 2. 设置新选中的节点
        _selectedNode = nodeVisual;
        nodeVisual.SetSelected(true);

        // 【关键修复】新节点变大了，立刻重新计算它的屏幕坐标，防止中心偏移
        UpdateNodePosition(nodeVisual);

        // 3. 触发事件通知顶层窗口绑定数据
        OnNodeSelected?.Invoke(nodeVisual.NodeData, nodeVisual);
    }

    private void OnNodeRightClick(NodeVisualElement nodeVisual)
    {
        // 【关键修复】有了选中的A，右键B，自动连线！
        if (_selectedNode != null && _selectedNode != nodeVisual)
        {
            CreateEdge(_selectedNode.NodeData.StageID, nodeVisual.NodeData.StageID);
        }
    }

    private void CreateEdge(string fromNodeId, string toNodeId)
    {
        if (_saveData == null) return;

        // 检查连线是否已存在
        bool edgeExists = _saveData.Edges.Exists(e => (e.FromNodeID == fromNodeId && e.ToNodeID == toNodeId) ||
                                                      (e.FromNodeID == toNodeId && e.ToNodeID == fromNodeId));
        if (edgeExists) return;

        // 1. 数据层：添加连线
        _saveData.Edges.Add(new BigMapEdgeData(fromNodeId, toNodeId, EdgeDirection.Bidirectional));

        // 2. 画布层：标记重绘
        MarkDirtyRepaint();

        // 3. 【关键修复】如果当前正在观察其中一个节点，强制 Inspector 刷新连线列表
        if (_selectedNode != null)
        {
            string selId = _selectedNode.NodeData.StageID;
            if (selId == fromNodeId || selId == toNodeId)
            {
                // 再次调用选中回调，这会触发顶层 Window 里的 _inspectorPanel.BindNode()
                // 从而让连线列表（UpdateConnectedEdges）重新生成
                OnNodeSelected?.Invoke(_selectedNode.NodeData, _selectedNode);
            }
        }
    }
}