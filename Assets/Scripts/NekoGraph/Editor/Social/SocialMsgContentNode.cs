#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// 正文发射器节点 UI - 社交系统专用喵~
/// 信号进入时发射正文，不阻塞流转
/// </summary>
[NodeMenuItem("📩 社交/正文发射器", typeof(SocialMsgContentNodeData))]
[NodeType(NodeSystem.Social)]
public class SocialMsgContentNode : BaseNode<SocialMsgContentNodeData>
{
    private TextField _speakerField;
    private TextField _bodyField;

    public SocialMsgContentNode() : base() { InitializeUI(); }
    public SocialMsgContentNode(SocialMsgContentNodeData data) : base(data) { InitializeUI(); }

    private void InitializeUI()
    {
        title = "📡 正文发射器";
        style.width = 300;
        titleContainer.style.backgroundColor = new Color(0.1f, 0.5f, 0.5f); // 🌊 深青色

        // 1. 发言人
        _speakerField = new TextField("发言人喵~");
        _speakerField.value = TypedData.Speaker;
        _speakerField.RegisterValueChangedCallback(evt => TypedData.Speaker = evt.newValue);
        extensionContainer.Add(_speakerField);

        // 2. 正文内容
        _bodyField = new TextField("正文台词喵~");
        _bodyField.multiline = true;
        _bodyField.style.minHeight = 80;
        _bodyField.value = TypedData.Body;
        _bodyField.RegisterValueChangedCallback(evt => TypedData.Body = evt.newValue);
        // 让正文框能自动换行喵~
        var inputElement = _bodyField.Q("unity-text-input");
        if (inputElement != null) inputElement.style.whiteSpace = WhiteSpace.Normal;
        
        extensionContainer.Add(_bodyField);

        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        TypedData.Speaker = _speakerField.value;
        TypedData.Body = _bodyField.value;
    }
}
#endif
