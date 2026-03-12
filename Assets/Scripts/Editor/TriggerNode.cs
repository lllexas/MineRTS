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
/// 触发器节点 - 监听游戏事件（串并联逻辑）喵~
/// Mission 和 Story 系统共用
/// 【端口标签驱动·自动组装重构版】
/// 端口根据 Data 类的 [InPort]/[OutPort] 标签自动生成，无需手动创建喵~
/// </summary>
[NodeMenuItem("🔧 通用/触发器节点", typeof(TriggerNodeData))]
[NodeType(NodeSystem.Common)]
public class TriggerNode : BaseNode<TriggerNodeData>
{
    private PopupField<string> _typeDropdown;
    private List<TextField> _paramFields = new List<TextField>();
    private Label _tooltipLabel;
    private VisualElement _paramContainer;
    private FloatField _requiredAmountField;

    /// <summary>
    /// 无参构造函数 - 用于从菜单创建节点喵~
    /// </summary>
    public TriggerNode() : base()
    {
        InitializeUI();
    }

    /// <summary>
    /// 带参数构造函数 - 用于从数据加载节点喵~
    /// </summary>
    public TriggerNode(TriggerNodeData data) : base(data)
    {
        InitializeUI();
    }

    /// <summary>
    /// 初始化 UI 元素喵~
    /// 端口会自动根据 Data 类的标签"长"出来，这里只画特殊控件喵！
    /// </summary>
    private void InitializeUI()
    {
        title = "🎬 触发器";
        style.width = 300;
        titleContainer.style.backgroundColor = GetNodeColor();

        // --- 配置区域 ---
        var foldout = new Foldout() { text = "触发器配置", value = true };

        // 触发器类型下拉框
        var typeChoices = TriggerRegistry.GetAllTypes()
            .Select(t => t.DisplayName)
            .ToList();

        // 确保至少有一个选项
        if (typeChoices.Count == 0)
        {
            typeChoices.Add("Time"); // 默认选项
        }

        // 获取当前事件名的显示名，如果不在列表中则使用第一个选项
        string currentDisplayName = TriggerRegistry.GetDisplayNameFromEventName(TypedData.Trigger.EventName);
        if (!typeChoices.Contains(currentDisplayName))
        {
            currentDisplayName = typeChoices[0]; // 使用第一个有效选项
        }

        _typeDropdown = new PopupField<string>("触发类型", typeChoices, currentDisplayName);
        _typeDropdown.RegisterValueChangedCallback(evt =>
        {
            TypedData.Trigger.EventName = TriggerRegistry.GetEventNameFromDisplayName(evt.newValue);
            RebuildParamFields();
            titleContainer.style.backgroundColor = GetNodeColor();
        });
        foldout.Add(_typeDropdown);

        // 目标阈值输入（RequiredAmount）
        _requiredAmountField = new FloatField("目标阈值")
        {
            value = (float)TypedData.RequiredAmount,
            tooltip = "达到此值时从主输出端口触发信号喵~"
        };
        _requiredAmountField.RegisterValueChangedCallback(evt =>
        {
            TypedData.RequiredAmount = evt.newValue;
        });
        foldout.Add(_requiredAmountField);

        // 参数输入容器
        _paramContainer = new VisualElement();
        foldout.Add(_paramContainer);

        // 提示信息
        _tooltipLabel = new Label();
        _tooltipLabel.style.fontSize = 9;
        _tooltipLabel.style.marginTop = 5;
        _tooltipLabel.style.color = new Color(1f, 1f, 0.3f);
        foldout.Add(_tooltipLabel);

        extensionContainer.Add(foldout);

        // 初始化
        RebuildParamFields();
        RefreshExpandedState();
    }

    /// <summary>
    /// 根据当前事件类型重建参数输入框喵~
    /// </summary>
    private void RebuildParamFields()
    {
        _paramFields.Clear();
        _paramContainer.Clear();

        if (TriggerRegistry.TryGetTypeInfo(TypedData.Trigger.EventName, out var info))
        {
            // 更新提示信息
            _tooltipLabel.text = $"ℹ️ {info.Tooltip}";

            // 为每个参数创建输入框
            for (int i = 0; i < info.ParameterNames.Length; i++)
            {
                var field = new TextField(info.ParameterNames[i]);
                field.value = TypedData.Trigger.GetParam(i, "");

                int index = i;  // 闭包变量
                field.RegisterValueChangedCallback(evt =>
                {
                    TypedData.Trigger.SetParam(index, evt.newValue);
                });

                _paramFields.Add(field);
                _paramContainer.Add(field);
            }
        }
    }

    /// <summary>
    /// 获取节点颜色喵~
    /// </summary>
    private Color GetNodeColor()
    {
        if (TriggerRegistry.TryGetTypeInfo(TypedData.Trigger.EventName, out var info))
            return info.EditorColor;
        return new Color(0.4f, 0.1f, 0.4f); // 默认紫色
    }

    public override void UpdateData()
    {
        // 数据已经在回调中实时更新
        // 这里可以保存最终状态
        for (int i = 0; i < _paramFields.Count; i++)
        {
            TypedData.Trigger.SetParam(i, _paramFields[i].value);
        }
        TypedData.RequiredAmount = _requiredAmountField.value;
    }
}
#endif
