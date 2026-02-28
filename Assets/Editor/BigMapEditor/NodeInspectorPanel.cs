using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;              // 修复 EditorWindow 和 EditorUtility 报错
using UnityEditor.UIElements;   // 修复 ToolbarSeparator 报错 (如果在其他文件里也有)
using MineRTS.BigMap;

/// <summary>
/// 节点属性面板
/// 显示和编辑选中节点的属性
/// </summary>
public class NodeInspectorPanel : VisualElement
{
    // 当前绑定的节点数据
    private BigMapNodeData _currentNodeData;

    // UI 控件
    private TextField _idField;
    private TextField _nameField;
    private Vector2Field _positionField;
    private TextField _typeField;
    private TextField _extraDataField;
    private Button _deleteButton;
    private VisualElement _edgeSection;
    private VisualElement _connectedEdgesContainer;

    private Label _emptyStateLabel;

    /// <summary>
    /// 构造函数
    /// </summary>
    public NodeInspectorPanel()
    {
        style.flexGrow = 1; style.paddingTop = 10;
        style.paddingLeft = 10; style.paddingRight = 10; style.paddingBottom = 10;

        _emptyStateLabel = new Label("未选中任何节点")
        {
            style = {
            fontSize = 14, unityTextAlign = TextAnchor.MiddleCenter,
            color = new Color(0.6f, 0.6f, 0.6f, 1.0f),
            flexGrow = 1, unityFontStyleAndWeight = FontStyle.Italic,
            paddingTop = 50
        }
        };
        Add(_emptyStateLabel);
    }

    /// <summary>
    /// 绑定节点数据
    /// </summary>
    public void BindNode(BigMapNodeData nodeData)
    {
        if (nodeData == null)
        {
            ClearPanel();
            return;
        }

        _currentNodeData = nodeData;

        // 清空当前内容
        this.Clear();

        // 创建标题
        var titleLabel = new Label("节点属性")
        {
            style =
            {
                fontSize = 16,
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 15,
                color = new Color(0.9f, 0.9f, 0.9f, 1.0f)
            }
        };
        Add(titleLabel);

        // 创建属性编辑区域
        var scrollView = new ScrollView();
        scrollView.style.flexGrow = 1;

        // 节点ID（只读）
        var idContainer = CreatePropertyContainer("节点ID");
        _idField = new TextField { value = nodeData.StageID, isReadOnly = true };
        idContainer.Add(_idField);
        scrollView.Add(idContainer);

        // 节点名称
        var nameContainer = CreatePropertyContainer("显示名称");
        _nameField = new TextField { value = nodeData.DisplayName };
        _nameField.RegisterCallback<FocusOutEvent>(evt =>
        {
            if (_currentNodeData != null && _currentNodeData.DisplayName != _nameField.value)
            {
                _currentNodeData.DisplayName = _nameField.value;
                MarkDataChanged();
            }
        });
        nameContainer.Add(_nameField);
        scrollView.Add(nameContainer);

        // 节点位置
        var positionContainer = CreatePropertyContainer("位置");
        _positionField = new Vector2Field { value = nodeData.Position };
        _positionField.RegisterCallback<FocusOutEvent>(evt =>
        {
            if (_currentNodeData != null && _currentNodeData.Position != _positionField.value)
            {
                _currentNodeData.Position = _positionField.value;
                MarkDataChanged();
            }
        });
        positionContainer.Add(_positionField);
        scrollView.Add(positionContainer);

        // 节点类型
        var typeContainer = CreatePropertyContainer("节点类型");
        _typeField = new TextField { value = nodeData.NodeType ?? "Default" };
        _typeField.RegisterCallback<FocusOutEvent>(evt =>
        {
            if (_currentNodeData != null && _currentNodeData.NodeType != _typeField.value)
            {
                _currentNodeData.NodeType = _typeField.value;
                MarkDataChanged();
            }
        });
        typeContainer.Add(_typeField);
        scrollView.Add(typeContainer);

        // 附加数据
        var extraDataContainer = CreatePropertyContainer("附加数据");
        _extraDataField = new TextField
        {
            value = nodeData.ExtraData ?? "",
            multiline = true,
            style =
            {
                height = 60,
                unityTextAlign = TextAnchor.UpperLeft
            }
        };
        _extraDataField.RegisterCallback<FocusOutEvent>(evt =>
        {
            if (_currentNodeData != null && _currentNodeData.ExtraData != _extraDataField.value)
            {
                _currentNodeData.ExtraData = _extraDataField.value;
                MarkDataChanged();
            }
        });
        extraDataContainer.Add(_extraDataField);
        scrollView.Add(extraDataContainer);

        // 分隔线
        var separator = new VisualElement();
        separator.style.height = 1;
        separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        separator.style.marginTop = 15;
        separator.style.marginBottom = 15;
        scrollView.Add(separator);

        // 连线信息部分
        _edgeSection = CreatePropertyContainer("连线信息");
        _connectedEdgesContainer = new VisualElement();
        _edgeSection.Add(_connectedEdgesContainer);
        scrollView.Add(_edgeSection);

        // 更新连线信息
        UpdateConnectedEdges();

        // 分隔线
        var separator2 = new VisualElement();
        separator2.style.height = 1;
        separator2.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        separator2.style.marginTop = 15;
        separator2.style.marginBottom = 15;
        scrollView.Add(separator2);

        // 删除按钮
        var buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = FlexDirection.Row;
        buttonContainer.style.justifyContent = Justify.FlexEnd;
        buttonContainer.style.marginTop = 10;

        _deleteButton = new Button(DeleteNode)
        {
            text = "删除节点",
            style =
            {
                backgroundColor = new Color(0.8f, 0.2f, 0.2f, 1.0f),
                color = Color.white,
                paddingTop = 5,
                paddingBottom = 5,
                paddingLeft = 15,
                paddingRight = 15
            }
        };
        buttonContainer.Add(_deleteButton);

        scrollView.Add(buttonContainer);

        // 添加到面板
        Add(scrollView);
    }

    /// <summary>
    /// 仅刷新数据变动（不重建 UI，性能高）
    /// </summary>
    public void Refresh()
    {
        if (_currentNodeData == null) return;

        // 仅在值不同时使用 SetValueWithoutNotify，防止光标丢失或死循环
        if (_positionField != null && _positionField.value != _currentNodeData.Position)
        {
            _positionField.SetValueWithoutNotify(_currentNodeData.Position);
        }
        // 更新连线信息
        UpdateConnectedEdges();
    }

    /// <summary>
    /// 清空面板
    /// </summary>
    public void ClearPanel()
    {
        // 彻底清空
        this.Clear();
        _currentNodeData = null;

        // 只在真正没有选中东西时，才添加这个提示标签
        if (_emptyStateLabel != null)
        {
            Add(_emptyStateLabel);
        }
    }

    /// <summary>
    /// 显示空状态
    /// </summary>
    private void ShowEmptyState()
    {
        var emptyLabel = new Label("未选中任何节点")
        {
            style =
            {
                fontSize = 14,
                unityTextAlign = TextAnchor.MiddleCenter,
                color = new Color(0.6f, 0.6f, 0.6f, 1.0f),
                flexGrow = 1,
                unityFontStyleAndWeight = FontStyle.Italic,
                paddingTop = 50
            }
        };
        Add(emptyLabel);
    }

    /// <summary>
    /// 创建属性容器
    /// </summary>
    private VisualElement CreatePropertyContainer(string labelText)
    {
        var container = new VisualElement();
        container.style.marginBottom = 10;

        var label = new Label(labelText)
        {
            style =
            {
                fontSize = 12,
                color = new Color(0.7f, 0.7f, 0.7f, 1.0f),
                marginBottom = 3
            }
        };
        container.Add(label);

        return container;
    }

    /// <summary>
    /// 更新连线信息
    /// </summary>
    private void UpdateConnectedEdges()
    {
        if (_currentNodeData == null || _connectedEdgesContainer == null)
            return;

        // 清空现有内容
        _connectedEdgesContainer.Clear();

        // 获取当前编辑器窗口
        var window = EditorWindow.GetWindow<BigMapEditorWindow>();
        if (window == null)
            return;

        var saveData = window.GetSaveData();
        if (saveData == null || saveData.Edges.Count == 0)
        {
            var noEdgesLabel = new Label("该节点没有连线")
            {
                style =
                {
                    fontSize = 12,
                    color = new Color(0.6f, 0.6f, 0.6f, 1.0f),
                    unityFontStyleAndWeight = FontStyle.Italic
                }
            };
            _connectedEdgesContainer.Add(noEdgesLabel);
            return;
        }

        // 查找与该节点相关的连线
        int edgeCount = 0;
        foreach (var edge in saveData.Edges)
        {
            if (edge.FromNodeID == _currentNodeData.StageID || edge.ToNodeID == _currentNodeData.StageID)
            {
                edgeCount++;

                // 创建连线项
                var edgeItem = CreateEdgeItem(edge);
                _connectedEdgesContainer.Add(edgeItem);
            }
        }

        // 如果没有找到连线
        if (edgeCount == 0)
        {
            var noEdgesLabel = new Label("该节点没有连线")
            {
                style =
                {
                    fontSize = 12,
                    color = new Color(0.6f, 0.6f, 0.6f, 1.0f),
                    unityFontStyleAndWeight = FontStyle.Italic
                }
            };
            _connectedEdgesContainer.Add(noEdgesLabel);
        }
    }

    /// <summary>
    /// 创建连线项
    /// </summary>
    private VisualElement CreateEdgeItem(BigMapEdgeData edge)
    {
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.justifyContent = Justify.SpaceBetween;
        container.style.alignItems = Align.Center;
        container.style.marginBottom = 5;
        container.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f, 1.0f);
        container.style.paddingTop = 5;
        container.style.paddingBottom = 5;
        container.style.paddingLeft = 5;
        container.style.paddingRight = 5;

        container.style.borderTopLeftRadius = 3;
        container.style.borderTopRightRadius = 3;
        container.style.borderBottomLeftRadius = 3;
        container.style.borderBottomRightRadius = 3;

        // 获取另一个节点的名称
        var window = EditorWindow.GetWindow<BigMapEditorWindow>();
        var saveData = window?.GetSaveData();
        string otherNodeId = edge.FromNodeID == _currentNodeData.StageID ? edge.ToNodeID : edge.FromNodeID;
        string otherNodeName = "未知节点";
        bool isOutgoing = edge.FromNodeID == _currentNodeData.StageID;

        if (saveData != null)
        {
            var otherNode = saveData.Nodes.Find(n => n.StageID == otherNodeId);
            if (otherNode != null)
            {
                otherNodeName = otherNode.DisplayName;
            }
        }

        // 连线信息
        var infoLabel = new Label($"{(isOutgoing ? "→" : "←")} {otherNodeName}")
        {
            style =
            {
                fontSize = 11,
                color = new Color(0.8f, 0.8f, 0.8f, 1.0f),
                flexGrow = 1
            }
        };

        // 方向标签
        string directionText = edge.Direction == EdgeDirection.Bidirectional ? "双向" : "单向";
        var directionLabel = new Label(directionText)
        {
            style =
            {
                fontSize = 10,
                color = edge.Direction == EdgeDirection.Bidirectional
                    ? new Color(0.4f, 0.8f, 1.0f, 1.0f)
                    : new Color(1.0f, 0.6f, 0.2f, 1.0f),
                marginRight = 5
            }
        };

        // 删除按钮
        var deleteButton = new Button(() => DeleteEdge(edge))
        {
            text = "×",
            style =
            {
                fontSize = 12,
                width = 20,
                height = 20,
                paddingLeft = 0,
                paddingRight = 0,
                backgroundColor = new Color(0.4f, 0.2f, 0.2f, 1.0f),
                color = Color.white
            }
        };

        container.Add(infoLabel);
        container.Add(directionLabel);
        container.Add(deleteButton);

        return container;
    }

    /// <summary>
    /// 删除节点
    /// </summary>
    private void DeleteNode()
    {
        if (_currentNodeData == null)
            return;

        if (!EditorUtility.DisplayDialog("删除节点",
            $"确定要删除节点 '{_currentNodeData.DisplayName}' 吗？\n此操作也会删除与该节点相关的所有连线。",
            "删除", "取消"))
            return;

        // 获取编辑器窗口并删除节点
        var window = EditorWindow.GetWindow<BigMapEditorWindow>();
        if (window != null)
        {
            window.DeleteNode(_currentNodeData.StageID);
        }
        else
        {
            Debug.LogError("无法获取 BigMapEditorWindow 实例");
        }
    }

    /// <summary>
    /// 删除连线
    /// </summary>
    private void DeleteEdge(BigMapEdgeData edge)
    {
        if (edge == null)
            return;

        // 获取另一个节点的名称
        var window = EditorWindow.GetWindow<BigMapEditorWindow>();
        var saveData = window?.GetSaveData();
        string otherNodeId = edge.FromNodeID == _currentNodeData.StageID ? edge.ToNodeID : edge.FromNodeID;
        string otherNodeName = "未知节点";

        if (saveData != null)
        {
            var otherNode = saveData.Nodes.Find(n => n.StageID == otherNodeId);
            if (otherNode != null)
            {
                otherNodeName = otherNode.DisplayName;
            }
        }

        if (!EditorUtility.DisplayDialog("删除连线",
            $"确定要删除与节点 '{otherNodeName}' 的连线吗？",
            "删除", "取消"))
            return;

        // 从数据中移除连线
        if (saveData != null)
        {
            saveData.Edges.Remove(edge);
        }

        // 更新显示
        UpdateConnectedEdges();

        // 标记数据已更改
        MarkDataChanged();
    }

    /// <summary>
    /// 标记数据已更改（需要重绘画布）
    /// </summary>
    private void MarkDataChanged()
    {
        // 1. 打印日志（主人可以留着调试，或者删掉）
        // Debug.Log("节点数据已更新，触发重绘");

        // 2. 获取当前的窗口实例
        var window = EditorWindow.GetWindow<BigMapEditorWindow>();
        if (window != null)
        {
            // 我们在 Window 里加一个公共方法来强制重绘画布
            window.RequestRepaint();
        }
    }
}