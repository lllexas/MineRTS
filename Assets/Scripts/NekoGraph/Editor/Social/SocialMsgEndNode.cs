#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// 社交终结节点 UI - 社交系统专用喵~
/// 宣告对话圆满结束，触发 TUI 退出逻辑
/// </summary>
[NodeMenuItem("📩 社交/结束会话", typeof(SocialMsgEndNodeData))]
[NodeType(NodeSystem.Social)]
public class SocialMsgEndNode : BaseNode<SocialMsgEndNodeData>
{
    public SocialMsgEndNode() : base() { InitializeUI(); }
    public SocialMsgEndNode(SocialMsgEndNodeData data) : base(data) { InitializeUI(); }

    private void InitializeUI()
    {
        title = "🏁 结束会话";
        style.width = 150;
        titleContainer.style.backgroundColor = new Color(0.4f, 0.1f, 0.4f); // 🍇 暗紫色

        var infoLabel = new Label("对话流程的终点\n到达此处后退出交互");
        infoLabel.style.fontSize = 10;
        infoLabel.style.marginTop = 5;
        infoLabel.style.whiteSpace = WhiteSpace.Normal;
        extensionContainer.Add(infoLabel);

        RefreshExpandedState();
    }

    public override void UpdateData() { }
}
#endif
