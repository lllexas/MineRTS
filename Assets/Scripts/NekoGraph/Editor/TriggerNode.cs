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
/// 监听器节点 - 事件分发中心喵！
/// 基于 TriggerEvent 枚举自动生成界面喵~
/// </summary>
[NodeMenuItem("🔔 事件监听器 (Trigger)", typeof(TriggerNodeData))]
[NodeType(NodeSystem.Common)]
public class TriggerNode : BaseNode<TriggerNodeData>
{
    private PopupField<string> _eventDropdown;
    private Label _protocolLabel;

    public TriggerNode() : base()
    {
        InitializeUI();
    }

    public TriggerNode(TriggerNodeData data) : base(data)
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        title = "🔔 事件监听器";
        titleContainer.style.backgroundColor = new Color(0.4f, 0.2f, 0.5f);
        style.width = 240;

        // --- 配置区域 ---
        var foldout = new Foldout() { text = "事件契约配置", value = true };

        // 1. 获取所有注册的事件喵~
        var allTriggers = TriggerRegistry.GetAllTriggers().ToList();
        var choices = allTriggers.Select(t => $"[{t.Info.Category}] {t.Info.DisplayName}").ToList();

        if (choices.Count == 0) choices.Add("None");

        // 2. 找到当前选项喵~
        var currentMeta = TriggerRegistry.GetMeta(TypedData.Event);
        string currentChoice = currentMeta != null 
            ? $"[{currentMeta.Info.Category}] {currentMeta.Info.DisplayName}" 
            : choices[0];

        // 3. 创建下拉列表喵~
        _eventDropdown = new PopupField<string>("选择事件", choices, currentChoice);
        _eventDropdown.RegisterValueChangedCallback(evt =>
        {
            // 通过解析显示名找回对应的 Meta 喵~
            string displayName = evt.newValue.Contains("] ") 
                ? evt.newValue.Split(new[] { "] " }, StringSplitOptions.None)[1] 
                : evt.newValue;
            
            var meta = allTriggers.FirstOrDefault(t => t.Info.DisplayName == displayName);
            if (meta != null)
            {
                TypedData.Event = meta.Event;
                UpdateProtocolUI(meta);
            }
        });
        foldout.Add(_eventDropdown);

        // 4. 协议提示标签喵~
        _protocolLabel = new Label();
        _protocolLabel.style.fontSize = 10;
        _protocolLabel.style.marginTop = 5;
        _protocolLabel.style.color = new Color(0.8f, 0.8f, 1f);
        _protocolLabel.style.whiteSpace = WhiteSpace.Normal;
        foldout.Add(_protocolLabel);

        extensionContainer.Add(foldout);

        // 初始化协议显示喵~
        if (currentMeta != null) UpdateProtocolUI(currentMeta);
        
        RefreshExpandedState();
    }

    private void UpdateProtocolUI(TriggerRegistry.TriggerMeta meta)
    {
        _protocolLabel.text = $"📜 协议：{meta.Info.Protocol}\nℹ️ {meta.Info.Tooltip ?? "监听指定全局事件，并转发携带的数据包喵~"}";
    }

    public override void UpdateData() { }
}
#endif
