#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// 选项信息节点 UI - 社交系统专用喵~
/// 广播选项描述文字，配合下游 TriggerNode 使用
/// </summary>
[NodeMenuItem("📩 社交/选项描述", typeof(ChoiceTextNodeData))]
[NodeType(NodeSystem.Social)]
public class ChoiceTextNode : BaseNode<ChoiceTextNodeData>
{
    private IntegerField _indexField;
    private TextField _labelField;

    public ChoiceTextNode() : base() { InitializeUI(); }
    public ChoiceTextNode(ChoiceTextNodeData data) : base(data) { InitializeUI(); }

    private void InitializeUI()
    {
        title = "🔘 选项描述";
        style.width = 250;
        titleContainer.style.backgroundColor = new Color(0.1f, 0.5f, 0.1f); // 🌿 深绿色

        // 1. 选项编号
        _indexField = new IntegerField("选项编号 (1,2...)喵");
        _indexField.value = TypedData.OptionIndex;
        _indexField.RegisterValueChangedCallback(evt => TypedData.OptionIndex = evt.newValue);
        extensionContainer.Add(_indexField);

        // 2. 选项文字
        _labelField = new TextField("按钮文字喵~");
        _labelField.value = TypedData.Label;
        _labelField.RegisterValueChangedCallback(evt => TypedData.Label = evt.newValue);
        extensionContainer.Add(_labelField);

        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        TypedData.OptionIndex = _indexField.value;
        TypedData.Label = _labelField.value;
    }
}
#endif
