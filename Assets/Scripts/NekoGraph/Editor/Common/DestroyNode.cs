#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// 流程销毁节点 UI - 销毁 Pack 实例并重置状态喵~
/// </summary>
[NodeMenuItem("🔧 流程/销毁", typeof(DestroyNodeData))]
[NodeType(NodeSystem.Common)]
public class DestroyNode : BaseNode<DestroyNodeData>
{
    public DestroyNode() : base() { InitializeUI(); }
    public DestroyNode(DestroyNodeData data) : base(data) { InitializeUI(); }

    private void InitializeUI()
    {
        title = "💀 销毁";
        style.width = 150;
        titleContainer.style.backgroundColor = new Color(0.6f, 0.1f, 0.1f); // 🔴 暗红色

        var infoLabel = new Label("销毁当前 Pack 实例\n强制重置状态");
        infoLabel.style.fontSize = 10;
        infoLabel.style.marginTop = 5;
        infoLabel.style.whiteSpace = WhiteSpace.Normal;
        extensionContainer.Add(infoLabel);

        RefreshExpandedState();
    }

    public override void UpdateData() { }
}
#endif
