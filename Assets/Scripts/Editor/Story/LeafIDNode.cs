#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// 叶 ID 节点 (Leaf) - 处理具体的对话演出喵~
/// </summary>
public class LeafIDNode : BaseNode
{
    public LeafIDNodeData Data;
    public Port InputPort;
    public Port OutputPort;
    private TextField _idField;
    private PopupField<string> _sequenceDropdown;

    public LeafIDNode(LeafIDNodeData data)
    {
        Data = data;
        GUID = data.NodeID;
        title = "🍃 叶演出 ID";
        style.width = 250;
        this.titleContainer.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f); // 🟢 绿色

        // 输入端口：接收 Trigger
        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "触发输入";
        inputContainer.Add(InputPort);

        // 输出端口：仅连向 Command（支持一对多）
        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        OutputPort.portName = "输出到命令";
        outputContainer.Add(OutputPort);

        // 故事进程 ID 输入框
        _idField = new TextField("故事进程 ID");
        _idField.value = data.StoryProcessID;
        _idField.RegisterValueChangedCallback(evt => data.StoryProcessID = evt.newValue);
        extensionContainer.Add(_idField);

        // 剧情序列下拉框（从 CSV 导入的 Sequences 中选择）
        var sequenceChoices = GetSequenceChoices();
        _sequenceDropdown = new PopupField<string>("剧情序列", sequenceChoices, data.SequenceID);
        _sequenceDropdown.RegisterValueChangedCallback(evt => data.SequenceID = evt.newValue);
        extensionContainer.Add(_sequenceDropdown);

        // 提示信息
        var infoLabel = new Label("此 ID 必须与一个 SpineID 节点配对");
        infoLabel.style.fontSize = 9;
        infoLabel.style.marginTop = 5;
        infoLabel.style.color = Color.yellow;
        extensionContainer.Add(infoLabel);

        RefreshExpandedState();
    }

    private List<string> GetSequenceChoices()
    {
        var choices = new List<string> { "" };
        
        // 从 GraphView 获取 Sequences 列表
        var storyGraph = this.GetFirstAncestorOfType<StoryGraphView>();
        if (storyGraph != null && storyGraph.Sequences != null)
        {
            foreach (var seq in storyGraph.Sequences)
            {
                choices.Add(seq.SequenceID);
            }
        }
        
        return choices;
    }

    public override void UpdateData()
    {
        Data.StoryProcessID = _idField.value;
        Data.SequenceID = _sequenceDropdown.value;
    }
}
#endif
