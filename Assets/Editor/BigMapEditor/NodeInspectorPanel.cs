using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using MineRTS.BigMap;

/// <summary>
/// 节点属性面板
/// 显示和编辑选中节点的属性
/// </summary>
public class NodeInspectorPanel : VisualElement
{
    private BigMapNodeData _currentNodeData;

    private TextField _idField;
    private TextField _nameField;
    private Vector2Field _positionField;
    private TextField _typeField;
    private TextField _extraDataField;
    private Label _levelStatusLabel;
    private Button _deleteButton;
    private Button _editLevelButton;
    private VisualElement _edgeSection;
    private VisualElement _connectedEdgesContainer;

    private readonly Label _emptyStateLabel;

    public NodeInspectorPanel()
    {
        style.flexGrow = 1;
        style.paddingTop = 10;
        style.paddingLeft = 10;
        style.paddingRight = 10;
        style.paddingBottom = 10;

        _emptyStateLabel = new Label("未选中任何节点")
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

        Add(_emptyStateLabel);
    }

    public void BindNode(BigMapNodeData nodeData)
    {
        if (nodeData == null)
        {
            ClearPanel();
            return;
        }

        _currentNodeData = nodeData;
        Clear();

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

        var scrollView = new ScrollView();
        scrollView.style.flexGrow = 1;

        var idContainer = CreatePropertyContainer("节点ID (必须唯一)");
        _idField = new TextField { value = nodeData.StageID };
        _idField.RegisterCallback<FocusOutEvent>(_ => HandleStageIdChanged());
        idContainer.Add(_idField);
        scrollView.Add(idContainer);

        var nameContainer = CreatePropertyContainer("显示名称");
        _nameField = new TextField { value = nodeData.DisplayName };
        _nameField.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (_currentNodeData != null && _currentNodeData.DisplayName != _nameField.value)
            {
                _currentNodeData.DisplayName = _nameField.value;
                MarkDataChanged();
            }
        });
        nameContainer.Add(_nameField);
        scrollView.Add(nameContainer);

        var positionContainer = CreatePropertyContainer("位置");
        _positionField = new Vector2Field { value = (Vector2)nodeData.Position };
        _positionField.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (_currentNodeData != null && (Vector2)_currentNodeData.Position != _positionField.value)
            {
                _currentNodeData.Position = (SerializableVector2)_positionField.value;
                MarkDataChanged();
            }
        });
        positionContainer.Add(_positionField);
        scrollView.Add(positionContainer);

        var typeContainer = CreatePropertyContainer("节点类型");
        _typeField = new TextField { value = nodeData.NodeType ?? "Default" };
        _typeField.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (_currentNodeData != null && _currentNodeData.NodeType != _typeField.value)
            {
                _currentNodeData.NodeType = _typeField.value;
                MarkDataChanged();
            }
        });
        typeContainer.Add(_typeField);
        scrollView.Add(typeContainer);

        var extraDataContainer = CreatePropertyContainer("附加数据");
        _extraDataField = new TextField
        {
            value = nodeData.ExtraData ?? string.Empty,
            multiline = true
        };
        _extraDataField.style.height = 60;
        _extraDataField.style.unityTextAlign = TextAnchor.UpperLeft;
        _extraDataField.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (_currentNodeData != null && _currentNodeData.ExtraData != _extraDataField.value)
            {
                _currentNodeData.ExtraData = _extraDataField.value;
                MarkDataChanged();
            }
        });
        extraDataContainer.Add(_extraDataField);
        scrollView.Add(extraDataContainer);

        scrollView.Add(CreateSeparator());

        var templateContainer = CreatePropertyContainer("关卡关卡文件");

        var templateHint = new Label("当前节点固定对应 Resources/Levels/{StageID}.json")
        {
            style =
            {
                fontSize = 10,
                color = new Color(0.6f, 0.6f, 0.6f, 1.0f),
                marginBottom = 4
            }
        };
        templateContainer.Add(templateHint);

        _levelStatusLabel = new Label();
        _levelStatusLabel.style.fontSize = 11;
        _levelStatusLabel.style.marginTop = 4;
        templateContainer.Add(_levelStatusLabel);

        _editLevelButton = new Button(OpenLevelEditor)
        {
            text = "编辑关卡"
        };
        _editLevelButton.style.marginTop = 8;
        _editLevelButton.style.height = 28;
        templateContainer.Add(_editLevelButton);

        scrollView.Add(templateContainer);
        RefreshTemplateStatus();

        scrollView.Add(CreateSeparator());

        _edgeSection = CreatePropertyContainer("连线信息");
        _connectedEdgesContainer = new VisualElement();
        _edgeSection.Add(_connectedEdgesContainer);
        scrollView.Add(_edgeSection);
        UpdateConnectedEdges();

        scrollView.Add(CreateSeparator());

        var buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = FlexDirection.Row;
        buttonContainer.style.justifyContent = Justify.FlexEnd;
        buttonContainer.style.marginTop = 10;

        _deleteButton = new Button(DeleteNode)
        {
            text = "删除节点"
        };
        _deleteButton.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 1.0f);
        _deleteButton.style.color = Color.white;
        _deleteButton.style.paddingTop = 5;
        _deleteButton.style.paddingBottom = 5;
        _deleteButton.style.paddingLeft = 15;
        _deleteButton.style.paddingRight = 15;
        buttonContainer.Add(_deleteButton);

        scrollView.Add(buttonContainer);
        Add(scrollView);
    }

    public void Refresh()
    {
        if (_currentNodeData == null)
        {
            return;
        }

        if (_positionField != null && _positionField.value != (Vector2)_currentNodeData.Position)
        {
            _positionField.SetValueWithoutNotify(_currentNodeData.Position);
        }

        RefreshTemplateStatus();
        UpdateConnectedEdges();
    }

    public void ClearPanel()
    {
        Clear();
        _currentNodeData = null;
        Add(_emptyStateLabel);
    }

    public void RefreshTemplateStatus()
    {
        if (_currentNodeData == null || _levelStatusLabel == null)
        {
            return;
        }

        string stageId = _currentNodeData.StageID ?? string.Empty;
        string absolutePath = GetLevelAbsolutePath(stageId);
        bool exists = !string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath);
        _levelStatusLabel.text = exists
            ? $"关卡状态: 已存在\nAssets/Resources/Levels/{stageId}.json"
            : $"关卡状态: 尚未创建\nAssets/Resources/Levels/{stageId}.json";

        _levelStatusLabel.style.color = exists
            ? new Color(0.4f, 0.85f, 0.5f, 1.0f)
            : new Color(0.95f, 0.8f, 0.35f, 1.0f);
    }

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

    private VisualElement CreateSeparator()
    {
        var separator = new VisualElement();
        separator.style.height = 1;
        separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        separator.style.marginTop = 15;
        separator.style.marginBottom = 15;
        return separator;
    }

    private void HandleStageIdChanged()
    {
        if (_currentNodeData == null || _currentNodeData.StageID == _idField.value)
        {
            return;
        }

        string oldId = _currentNodeData.StageID;
        string newId = (_idField.value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newId))
        {
            EditorUtility.DisplayDialog("错误", "节点ID不能为空", "确定");
            _idField.value = oldId;
            return;
        }

        var window = EditorWindow.GetWindow<BigMapEditorWindow>();
        var saveData = window?.GetSaveData();
        if (saveData != null)
        {
            bool isDuplicate = saveData.Nodes.Exists(n => n.StageID == newId && n.StageID != oldId);
            if (isDuplicate)
            {
                EditorUtility.DisplayDialog("错误", $"节点ID '{newId}' 已存在，请使用其他ID", "确定");
                _idField.value = oldId;
                return;
            }
        }

        if (window != null)
        {
            window.UpdateNodeID(oldId, newId);
            _currentNodeData = saveData?.Nodes.Find(n => n.StageID == newId);
        }

        MarkDataChanged();
        RefreshTemplateStatus();
    }

    private void UpdateConnectedEdges()
    {
        if (_currentNodeData == null || _connectedEdgesContainer == null)
        {
            return;
        }

        _connectedEdgesContainer.Clear();

        var window = EditorWindow.GetWindow<BigMapEditorWindow>();
        if (window == null)
        {
            return;
        }

        var saveData = window.GetSaveData();
        if (saveData == null || saveData.Edges.Count == 0)
        {
            _connectedEdgesContainer.Add(CreateMutedLabel("该节点没有连线"));
            return;
        }

        int edgeCount = 0;
        foreach (var edge in saveData.Edges)
        {
            if (edge.FromNodeID == _currentNodeData.StageID || edge.ToNodeID == _currentNodeData.StageID)
            {
                edgeCount++;
                _connectedEdgesContainer.Add(CreateEdgeItem(edge));
            }
        }

        if (edgeCount == 0)
        {
            _connectedEdgesContainer.Add(CreateMutedLabel("该节点没有连线"));
        }
    }

    private Label CreateMutedLabel(string text)
    {
        return new Label(text)
        {
            style =
            {
                fontSize = 12,
                color = new Color(0.6f, 0.6f, 0.6f, 1.0f),
                unityFontStyleAndWeight = FontStyle.Italic
            }
        };
    }

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

        var infoLabel = new Label($"{(isOutgoing ? "→" : "←")} {otherNodeName}")
        {
            style =
            {
                fontSize = 11,
                color = new Color(0.8f, 0.8f, 0.8f, 1.0f),
                flexGrow = 1
            }
        };

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

        var deleteButton = new Button(() => DeleteEdge(edge))
        {
            text = "×"
        };
        deleteButton.style.fontSize = 12;
        deleteButton.style.width = 20;
        deleteButton.style.height = 20;
        deleteButton.style.paddingLeft = 0;
        deleteButton.style.paddingRight = 0;
        deleteButton.style.backgroundColor = new Color(0.4f, 0.2f, 0.2f, 1.0f);
        deleteButton.style.color = Color.white;

        container.Add(infoLabel);
        container.Add(directionLabel);
        container.Add(deleteButton);

        return container;
    }

    private void DeleteNode()
    {
        if (_currentNodeData == null)
        {
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "删除节点",
                $"确定要删除节点 '{_currentNodeData.DisplayName}' 吗？\n此操作也会删除与该节点相关的所有连线。",
                "删除",
                "取消"))
        {
            return;
        }

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

    private void DeleteEdge(BigMapEdgeData edge)
    {
        if (edge == null)
        {
            return;
        }

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

        if (!EditorUtility.DisplayDialog("删除连线", $"确定要删除与节点 '{otherNodeName}' 的连线吗？", "删除", "取消"))
        {
            return;
        }

        saveData?.Edges.Remove(edge);
        UpdateConnectedEdges();
        MarkDataChanged();
    }

    private void MarkDataChanged()
    {
        var window = EditorWindow.GetWindow<BigMapEditorWindow>();
        if (window != null)
        {
            window.RequestRepaint();
        }
    }

    private string GetLevelAbsolutePath(string stageId)
    {
        if (string.IsNullOrEmpty(stageId))
        {
            return string.Empty;
        }

        return Path.Combine(Application.dataPath, "Resources", "Levels", $"{stageId}.json");
    }

    private void OpenLevelEditor()
    {
        if (_currentNodeData == null)
        {
            return;
        }

        var window = EditorWindow.GetWindow<BigMapEditorWindow>();
        if (window == null)
        {
            Debug.LogError("无法获取 BigMapEditorWindow 实例");
            return;
        }

        window.OpenTilemapEditor(_currentNodeData);
    }
}
