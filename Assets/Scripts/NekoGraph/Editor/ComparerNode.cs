#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// 比较器节点 - 逻辑判断中心喵~
/// 支持一站式从 ComparerRegistry 加载逻辑定义，并具备协议校验警报喵！
/// </summary>
[NodeMenuItem("⚖️ 比较器节点", typeof(ComparerNodeData))]
[NodeType(NodeSystem.Common)]
public class ComparerNode : BaseNode<ComparerNodeData>
{
    private PopupField<string> _comparerDropdown;
    private VisualElement _paramContainer;
    private List<TextField> _paramFields = new List<TextField>();
    private Label _tooltipLabel;

    public ComparerNode() : base() { InitializeUI(); }
    public ComparerNode(ComparerNodeData data) : base(data) { InitializeUI(); }

    private void InitializeUI()
    {
        title = "⚖️ 比较器";
        style.width = 280;
        titleContainer.style.backgroundColor = new Color(0.1f, 0.4f, 0.3f);

        var foldout = new Foldout() { text = "逻辑配置", value = true };
        var allComparers = ComparerRegistry.GetAllComparers();
        var choices = allComparers.Select(c => $"[{c.Info.Category}] {c.Info.DisplayName}").ToList();

        if (choices.Count == 0) choices.Add("None");

        var currentMeta = ComparerRegistry.GetMeta(TypedData.ComparerName);
        string currentChoice = currentMeta != null 
            ? $"[{currentMeta.Info.Category}] {currentMeta.Info.DisplayName}" 
            : choices[0];

        _comparerDropdown = new PopupField<string>("选择逻辑", choices, currentChoice);
        _comparerDropdown.RegisterValueChangedCallback(evt =>
        {
            string displayName = evt.newValue.Contains("] ") 
                ? evt.newValue.Split(new[] { "] " }, StringSplitOptions.None)[1] 
                : evt.newValue;
            
            var meta = ComparerRegistry.GetAllComparers().FirstOrDefault(c => c.Info.DisplayName == displayName);
            if (meta != null)
            {
                TypedData.ComparerName = meta.Info.Name;
                RebuildParams(meta);
                ValidateProtocol(); // 切换逻辑时立即校验喵！
            }
        });
        foldout.Add(_comparerDropdown);

        _paramContainer = new VisualElement();
        foldout.Add(_paramContainer);

        _tooltipLabel = new Label();
        _tooltipLabel.style.fontSize = 9;
        _tooltipLabel.style.marginTop = 5;
        _tooltipLabel.style.color = new Color(0.7f, 1f, 0.7f);
        _tooltipLabel.style.whiteSpace = WhiteSpace.Normal;
        foldout.Add(_tooltipLabel);

        extensionContainer.Add(foldout);
        if (currentMeta != null) RebuildParams(currentMeta);
        
        RefreshExpandedState();
    }

    private void RebuildParams(ComparerRegistry.ComparerMeta meta)
    {
        _paramContainer.Clear();
        _paramFields.Clear();
        _tooltipLabel.text = $"ℹ️ {meta.Info.Tooltip}\n🎯 要求协议：{meta.Info.Protocol}";

        var paramNames = meta.Info.ParamNames;
        for (int i = 0; i < paramNames.Length; i++)
        {
            int index = i;
            var field = new TextField(paramNames[i]);
            while (TypedData.Parameters.Count <= index) TypedData.Parameters.Add("");
            field.value = TypedData.Parameters[index];
            field.RegisterValueChangedCallback(evt => TypedData.Parameters[index] = evt.newValue);
            _paramFields.Add(field);
            _paramContainer.Add(field);
        }
    }

    /// <summary>
    /// 【协议校验魔法】检查上游节点是否符合契约喵！
    /// </summary>
    public void ValidateProtocol()
    {
        ClearError();
        var meta = ComparerRegistry.GetMeta(TypedData.ComparerName);
        if (meta == null) return;

        // 找到连到输入端口的边缘喵~
        var inputPort = GetInputPort(0);
        if (inputPort == null || !inputPort.connected) return;

        foreach (var edge in inputPort.connections)
        {
            // 找到源节点喵~
            var sourceNode = edge.output.node as BaseNode;
            if (sourceNode == null) continue;

            // 如果源节点是 Trigger，检查它的协议喵~
            if (sourceNode.Data is TriggerNodeData triggerData)
            {
                var triggerMeta = TriggerRegistry.GetMeta(triggerData.Event);
                if (triggerMeta != null && triggerMeta.Info.Protocol != meta.Info.Protocol)
                {
                    SetError($"协议不匹配！\n上游 [{triggerData.Event}] 提供 {triggerMeta.Info.Protocol}，但本比较器需要 {meta.Info.Protocol} 喵！");
                    return;
                }
            }
            // 如果源节点是另一个 Comparer，检查它的协议喵~
            else if (sourceNode.Data is ComparerNodeData otherComparerData)
            {
                var otherMeta = ComparerRegistry.GetMeta(otherComparerData.ComparerName);
                if (otherMeta != null && otherMeta.Info.Protocol != meta.Info.Protocol)
                {
                    SetError($"协议不匹配！\n上游比较器提供 {otherMeta.Info.Protocol}，但本比较器需要 {meta.Info.Protocol} 喵！");
                    return;
                }
            }
        }
    }

    // 每一帧更新数据时顺便校验一下喵~
    public override void UpdateData() 
    {
        ValidateProtocol();
    }
}
#endif
