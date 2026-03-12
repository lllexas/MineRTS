#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using NekoGraph;

/// <summary>
/// 叶 ID 节点 A (LeafA) - 处理具体的对话演出喵~
/// 【端口标签驱动·自动组装重构版】
/// 端口根据 Data 类的 [InPort]/[OutPort] 标签自动生成，无需手动创建喵~
/// </summary>
[NodeMenuItem("📋 流程/叶节点 A(演出)", typeof(LeafNode_A_Data))]
[NodeType(NodeSystem.Common)]
public class LeafNode_A : BaseNode<LeafNode_A_Data>
{
    private TextField _idField;
    private PopupField<string> _sequenceDropdown;

    /// <summary>
    /// 无参构造函数 - 用于从菜单创建节点喵~
    /// </summary>
    public LeafNode_A() : base()
    {
        InitializeUI();
    }

    /// <summary>
    /// 带参数构造函数 - 用于从数据加载节点喵~
    /// </summary>
    public LeafNode_A(LeafNode_A_Data data) : base(data)
    {
        InitializeUI();
    }

    /// <summary>
    /// 初始化 UI 元素喵~
    /// 端口会自动根据 Data 类的标签"长"出来，这里只画特殊控件喵！
    /// </summary>
    private void InitializeUI()
    {
        title = "🍃 叶演出 A";
        style.width = 250;
        titleContainer.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f); // 🟢 绿色

        // 流程 ID 输入框
        _idField = new TextField("流程 ID");
        _idField.value = TypedData.ProcessID;
        _idField.RegisterValueChangedCallback(evt => TypedData.ProcessID = evt.newValue);
        extensionContainer.Add(_idField);

        // 提示信息
        var infoLabel = new Label("此 ID 必须与一个 Spine 节点配对");
        infoLabel.style.fontSize = 9;
        infoLabel.style.marginTop = 5;
        infoLabel.style.color = Color.yellow;
        extensionContainer.Add(infoLabel);

        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        TypedData.ProcessID = _idField.value;
    }
}
#endif
