using System;
using UnityEngine;
using UnityEngine.UIElements;
using MineRTS.BigMap;

/// <summary>
/// 节点可视化元素
/// </summary>
public class NodeVisualElement : VisualElement
{
    private BigMapNodeData _nodeData;

    private bool _isPointerDown = false;
    private bool _isDragging = false;
    private Vector2 _pointerDownPosition;

    // 【修改点】记录拖拽起始的逻辑坐标
    public Vector2 DragStartLogicPosition { get; private set; }

    private bool _isSelected = false;

    private const float NODE_SIZE = 12.0f;
    private const float SELECTED_NODE_SIZE = 16.0f;
    private static readonly Color NORMAL_COLOR = new Color(0.2f, 0.6f, 1.0f, 1.0f);
    private static readonly Color SELECTED_COLOR = new Color(1.0f, 0.8f, 0.2f, 1.0f);
    private static readonly Color DRAGGING_COLOR = new Color(1.0f, 0.4f, 0.2f, 1.0f);

    // 【修改点】拖拽事件直接传递屏幕偏移量
    public delegate void DragMovedHandler(NodeVisualElement nodeVisual, Vector2 screenDelta);
    public delegate void SelectedHandler(string nodeId);
    public delegate void RightClickHandler(NodeVisualElement nodeVisual);
    public delegate void DragStartedHandler(NodeVisualElement nodeVisual);
    public delegate void DragFinishedHandler(NodeVisualElement nodeVisual);

    public event DragMovedHandler OnDragMoved;
    public event SelectedHandler OnSelected;
    public event RightClickHandler OnRightClick;
    public event DragStartedHandler OnDragStarted;
    public event DragFinishedHandler OnDragFinished;

    public BigMapNodeData NodeData => _nodeData;

    public NodeVisualElement(BigMapNodeData nodeData)
    {
        _nodeData = nodeData ?? throw new ArgumentNullException(nameof(nodeData));

        style.width = NODE_SIZE;
        style.height = NODE_SIZE;
        style.position = Position.Absolute;
        style.backgroundColor = NORMAL_COLOR;

        style.borderTopLeftRadius = NODE_SIZE / 2;
        style.borderTopRightRadius = NODE_SIZE / 2;
        style.borderBottomLeftRadius = NODE_SIZE / 2;
        style.borderBottomRightRadius = NODE_SIZE / 2;

        style.borderTopWidth = 1; style.borderBottomWidth = 1;
        style.borderLeftWidth = 1; style.borderRightWidth = 1;

        Color bColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
        style.borderTopColor = bColor; style.borderBottomColor = bColor;
        style.borderLeftColor = bColor; style.borderRightColor = bColor;

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);

        focusable = true;
    }

    public void SetSelected(bool selected)
    {
        if (_isSelected == selected) return;
        _isSelected = selected;

        if (_isSelected)
        {
            style.width = SELECTED_NODE_SIZE; style.height = SELECTED_NODE_SIZE;
            style.backgroundColor = SELECTED_COLOR;
            style.borderTopWidth = 2; style.borderBottomWidth = 2;
            style.borderLeftWidth = 2; style.borderRightWidth = 2;

            Color selColor = new Color(1.0f, 1.0f, 1.0f, 0.8f);
            style.borderTopColor = selColor; style.borderBottomColor = selColor;
            style.borderLeftColor = selColor; style.borderRightColor = selColor;

            style.borderTopLeftRadius = SELECTED_NODE_SIZE / 2; style.borderTopRightRadius = SELECTED_NODE_SIZE / 2;
            style.borderBottomLeftRadius = SELECTED_NODE_SIZE / 2; style.borderBottomRightRadius = SELECTED_NODE_SIZE / 2;
            Focus();
        }
        else
        {
            style.width = NODE_SIZE; style.height = NODE_SIZE;
            style.backgroundColor = NORMAL_COLOR;
            style.borderTopWidth = 1; style.borderBottomWidth = 1;
            style.borderLeftWidth = 1; style.borderRightWidth = 1;

            Color norColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
            style.borderTopColor = norColor; style.borderBottomColor = norColor;
            style.borderLeftColor = norColor; style.borderRightColor = norColor;

            style.borderTopLeftRadius = NODE_SIZE / 2; style.borderTopRightRadius = NODE_SIZE / 2;
            style.borderBottomLeftRadius = NODE_SIZE / 2; style.borderBottomRightRadius = NODE_SIZE / 2;
        }
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button == 0) // 左键
        {
            _isPointerDown = true;
            _pointerDownPosition = (Vector2)evt.position; // 记录屏幕绝对坐标
            DragStartLogicPosition = _nodeData.Position;  // 记录此刻的逻辑坐标

            this.CapturePointer(evt.pointerId);
            evt.StopPropagation(); // 拦截，防止画布拖拽
        }
        else if (evt.button == 1) // 右键：用于连线
        {
            OnRightClick?.Invoke(this);
            evt.StopPropagation(); // 绝对拦截！防止画布接收到右键从而创建节点
        }
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (_isPointerDown && !_isDragging)
        {
            // 引入 5 像素的死区 (Deadzone)，严格区分点选和拖拽
            float distance = Vector2.Distance(_pointerDownPosition, (Vector2)evt.position);
            if (distance > 5.0f)
            {
                _isDragging = true;
                style.backgroundColor = DRAGGING_COLOR;
                OnDragStarted?.Invoke(this);
            }
        }

        if (_isDragging)
        {
            // 算出屏幕坐标差值，扔给画布去计算逻辑坐标缩放
            Vector2 screenDelta = (Vector2)evt.position - _pointerDownPosition;
            OnDragMoved?.Invoke(this, screenDelta);
            evt.StopPropagation();
        }
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.button != 0) return;

        _isPointerDown = false;

        if (_isDragging)
        {
            _isDragging = false;
            style.backgroundColor = _isSelected ? SELECTED_COLOR : NORMAL_COLOR;
            OnDragFinished?.Invoke(this);
        }
        else
        {
            // 如果没拖出死区，视为纯点击！
            OnSelected?.Invoke(_nodeData.StageID);
        }

        this.ReleasePointer(evt.pointerId);
        evt.StopPropagation();
    }
}