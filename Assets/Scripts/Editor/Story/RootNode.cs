#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;
using UnityEditor.Experimental.GraphView;

/// <summary>
/// 流程根节点 UI - 整个流程树的起始锚点（全图唯一）喵~
/// 用于 Mission 或 Story 系统的流程起点
/// 【端口标签驱动·自动组装重构版】
/// 端口根据 Data 类的 [InPort]/[OutPort] 标签自动生成，无需手动创建喵~
/// </summary>
[NodeMenuItem("📋 流程/根节点", typeof(RootNodeData))]
[NodeType(NodeSystem.Common)]
public class RootNode : BaseNode<RootNodeData>
{
    /// <summary>
    /// 无参构造函数喵~
    /// 用于从菜单创建节点喵~
    /// </summary>
    public RootNode() : base()
    {
        InitializeUI();
    }

    /// <summary>
    /// 使用数据初始化构造函数喵~
    /// 用于从加载数据喵~
    /// </summary>
    public RootNode(RootNodeData data) : base(data)
    {
        InitializeUI();
    }

    /// <summary>
    /// 初始化 UI 元素喵~
    /// 端口会自动根据 Data 类的标签"长"出来，这里只画特殊控件喵！
    /// </summary>
    private void InitializeUI()
    {
        title = "🌳 流程根节点";
        style.width = 200;
        titleContainer.style.backgroundColor = new Color(1f, 0.8f, 0f); // 🟡 金色

        // 根节点不可删除
        capabilities &= ~Capabilities.Deletable;

        var infoLabel = new Label("这是流程树的起点\n全图唯一，不可删除");
        infoLabel.style.fontSize = 10;
        infoLabel.style.marginTop = 5;
        extensionContainer.Add(infoLabel);

        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        // 根节点没有需要更新的数据
    }
}
#endif
