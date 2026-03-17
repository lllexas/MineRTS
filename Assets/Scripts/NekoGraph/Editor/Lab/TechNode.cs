#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using NekoGraph;

// =========================================================
// TechNode - 科技节点编辑器 UI
// =========================================================

/// <summary>
/// 科技节点 UI - Lab 科技树系统专用喵~
/// 【端口标签驱动·自动组装重构版】
/// </summary>
[NodeMenuItem("🧪 科技树/科技节点", typeof(TechNodeData))]
[NodeType(NodeSystem.Lab)]
public class TechNode : BaseNode<TechNodeData>
{
    private TextField _techIdField;
    private TextField _techNameField;
    private TextField _descField;
    private PopupField<string> _techTypeDropdown;
    private ObjectField _iconField;
    private TextField _commandNameField;
    private TextField _commandParamField;

    public TechNode() : base()
    {
        InitializeUI();
    }

    public TechNode(TechNodeData data) : base(data)
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        title = "🧪 科技节点";
        style.width = 320;
        titleContainer.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f); // 🔵 科技蓝

        var foldout = new Foldout() { text = "科技配置", value = true };

        // 科技 ID
        _techIdField = new TextField("科技 ID");
        _techIdField.value = TypedData.TechID;
        _techIdField.RegisterValueChangedCallback(evt => TypedData.TechID = evt.newValue);
        foldout.Add(_techIdField);

        // 科技名称
        _techNameField = new TextField("科技名称");
        _techNameField.value = TypedData.TechName;
        _techNameField.RegisterValueChangedCallback(evt => TypedData.TechName = evt.newValue);
        foldout.Add(_techNameField);

        // 科技描述
        _descField = new TextField("描述") { multiline = true, style = { minHeight = 80 } };
        _descField.value = TypedData.Description;
        _descField.RegisterValueChangedCallback(evt => TypedData.Description = evt.newValue);
        foldout.Add(_descField);

        // 科技图标
        _iconField = new ObjectField("图标") { objectType = typeof(Sprite) };
        _iconField.value = TypedData.Icon;
        _iconField.RegisterValueChangedCallback(evt => TypedData.Icon = evt.newValue as Sprite);
        foldout.Add(_iconField);

        // 科技类型
        var typeChoices = new List<string> 
        { 
            "🔓 解锁 (Unlock)", 
            "⬆️ 升级 (Upgrade)", 
            "📐 蓝图 (Blueprint)", 
            "✨ 被动 (Passive)", 
            "🌟 特殊 (Special)" 
        };
        _techTypeDropdown = new PopupField<string>("类型", typeChoices, GetTechTypeString(TypedData.TechType));
        _techTypeDropdown.RegisterValueChangedCallback(evt =>
        {
            TypedData.TechType = GetTechTypeFromString(evt.newValue);
        });
        foldout.Add(_techTypeDropdown);

        // 解锁奖励命令
        var rewardFoldout = new Foldout() { text = "解锁奖励", value = false };

        _commandNameField = new TextField("命令名");
        _commandNameField.value = TypedData.UnlockReward.CommandName;
        _commandNameField.RegisterValueChangedCallback(evt => TypedData.UnlockReward.CommandName = evt.newValue);
        rewardFoldout.Add(_commandNameField);

        _commandParamField = new TextField("参数");
        _commandParamField.value = TypedData.UnlockReward.Parameter;
        _commandParamField.RegisterValueChangedCallback(evt => TypedData.UnlockReward.Parameter = evt.newValue);
        rewardFoldout.Add(_commandParamField);

        extensionContainer.Add(foldout);
        extensionContainer.Add(rewardFoldout);
        RefreshExpandedState();
    }

    private string GetTechTypeString(TechType type)
    {
        switch (type)
        {
            case TechType.Unlock: return "🔓 解锁 (Unlock)";
            case TechType.Upgrade: return "⬆️ 升级 (Upgrade)";
            case TechType.Blueprint: return "📐 蓝图 (Blueprint)";
            case TechType.Passive: return "✨ 被动 (Passive)";
            case TechType.Special: return "🌟 特殊 (Special)";
            default: return "🔓 解锁 (Unlock)";
        }
    }

    private TechType GetTechTypeFromString(string value)
    {
        if (value.Contains("解锁")) return TechType.Unlock;
        if (value.Contains("升级")) return TechType.Upgrade;
        if (value.Contains("蓝图")) return TechType.Blueprint;
        if (value.Contains("被动")) return TechType.Passive;
        if (value.Contains("特殊")) return TechType.Special;
        return TechType.Unlock;
    }

    public override void UpdateData()
    {
        TypedData.TechID = _techIdField.value;
        TypedData.TechName = _techNameField.value;
        TypedData.Description = _descField.value;
        TypedData.Icon = _iconField.value as Sprite;
        TypedData.TechType = GetTechTypeFromString(_techTypeDropdown.value);
        TypedData.UnlockReward.CommandName = _commandNameField.value;
        TypedData.UnlockReward.Parameter = _commandParamField.value;
    }
}

#endif
