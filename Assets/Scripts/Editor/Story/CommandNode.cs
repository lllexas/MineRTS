#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 命令节点 - 执行 RTS 动作（串并联逻辑）喵~
/// </summary>
public class CommandNode : BaseNode
{
    public CommandNodeData Data;
    public Port InputPort;
    public Port OutputPort;
    private TextField _typeField;
    private TextField _paramField;

    public CommandNode(CommandNodeData data)
    {
        Data = data;
        GUID = data.NodeID;
        title = "⚡ 命令";
        style.width = 250;
        this.titleContainer.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f); // 🔴 红色

        // 输入端口：接收 Leaf 或 Command
        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "输入";
        inputContainer.Add(InputPort);

        // 输出端口：连向 Command
        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        OutputPort.portName = "输出到命令";
        outputContainer.Add(OutputPort);

        // --- 配置区域 ---
        var foldout = new Foldout() { text = "命令配置", value = true };

        // 命令类型
        _typeField = new TextField("命令类型");
        _typeField.value = data.Command.CommandType;
        _typeField.RegisterValueChangedCallback(evt => data.Command.CommandType = evt.newValue);
        _typeField.tooltip = "例如：PlayCG, UnlockStage, SendEvent";
        foldout.Add(_typeField);

        // 命令参数
        _paramField = new TextField("命令参数");
        _paramField.value = data.Command.CommandParam;
        _paramField.RegisterValueChangedCallback(evt => data.Command.CommandParam = evt.newValue);
        _paramField.tooltip = "例如：ending.cg, stage_02, mission_start";
        foldout.Add(_paramField);

        extensionContainer.Add(foldout);

        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        Data.Command.CommandType = _typeField.value;
        Data.Command.CommandParam = _paramField.value;
    }
}
#endif
