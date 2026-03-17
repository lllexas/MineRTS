#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

// =========================================================
// TechNode_R - 科技树刷新节点编辑器 UI
// =========================================================

/// <summary>
/// 科技树刷新节点 UI - Lab 科技树系统专用喵~
/// 用于刷新 Lab 面板 UI 的简单透传节点喵~
/// </summary>
[NodeMenuItem("🧪 科技树/刷新节点", typeof(TechNode_RData))]
[NodeType(NodeSystem.Lab)]
public class TechNode_R : BaseNode<TechNode_RData>
{
    public TechNode_R() : base()
    {
        InitializeUI();
    }

    public TechNode_R(TechNode_RData data) : base(data)
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        title = "🔄 刷新 UI";
        style.width = 200;
        titleContainer.style.backgroundColor = new Color(0.2f, 0.7f, 0.5f); // 🟢 青绿色

        var foldout = new Foldout() { text = "刷新节点配置", value = true };

        // 简单的说明文字
        var helpLabel = new Label("此节点用于刷新 Lab 面板 UI\n通常连接在 TriggerNode 的\n\"进度输出\"端口上喵~");
        helpLabel.style.whiteSpace = WhiteSpace.Normal;
        foldout.Add(helpLabel);

        extensionContainer.Add(foldout);
        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        // 刷新节点没有需要更新的字段喵~
    }
}

#endif
