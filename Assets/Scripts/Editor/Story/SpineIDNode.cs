#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 树 ID 节点 (Spine) - 定义剧情的逻辑骨架（章节/阶段）喵~
/// </summary>
public class SpineIDNode : BaseNode
{
    public SpineIDNodeData Data;
    public Port InputPort;
    public Port OutputPort;
    private TextField _idField;

    public SpineIDNode(SpineIDNodeData data)
    {
        Data = data;
        GUID = data.NodeID;
        title = "📗 树主干 ID";
        style.width = 250;
        this.titleContainer.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f); // 🔵 蓝色

        // 输入端口：接收 Root 或 Spine
        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
        InputPort.portName = "输入";
        inputContainer.Add(InputPort);

        // 输出端口：仅连向 Spine
        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        OutputPort.portName = "输出到主干";
        outputContainer.Add(OutputPort);

        // 故事进程 ID 输入框
        _idField = new TextField("故事进程 ID");
        _idField.value = data.StoryProcessID;
        _idField.RegisterValueChangedCallback(evt => data.StoryProcessID = evt.newValue);
        extensionContainer.Add(_idField);

        // 提示信息
        var infoLabel = new Label("此 ID 必须与一个 LeafID 节点配对");
        infoLabel.style.fontSize = 9;
        infoLabel.style.marginTop = 5;
        infoLabel.style.color = Color.yellow;
        extensionContainer.Add(infoLabel);

        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        Data.StoryProcessID = _idField.value;
    }
}
#endif
