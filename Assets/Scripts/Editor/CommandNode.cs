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
/// 命令节点 - 执行 RTS 动作（串并联逻辑）喵~
/// Mission 和 Story 系统共用
/// 支持动态参数输入和命令选择
/// 【端口标签驱动·自动组装重构版】
/// 端口根据 Data 类的 [InPort]/[OutPort] 标签自动生成，无需手动创建喵~
/// </summary>
[NodeMenuItem("🔧 通用/命令节点", typeof(CommandNodeData))]
[NodeType(NodeSystem.Common)]
public class CommandNode : BaseNode<CommandNodeData>
{
    private PopupField<string> _categoryDropdown;      // 分类选择下拉框喵~
    private PopupField<string> _commandDropdown;       // 命令选择下拉框喵~
    private List<TextField> _paramFields = new List<TextField>();
    private Label _tooltipLabel;
    private VisualElement _paramContainer;

    /// <summary>
    /// 无参构造函数 - 用于从菜单创建节点喵~
    /// </summary>
    public CommandNode() : base()
    {
        InitializeUI();
    }

    /// <summary>
    /// 带参数构造函数 - 用于从数据加载节点喵~
    /// </summary>
    public CommandNode(CommandNodeData data) : base(data)
    {
        InitializeUI();
    }

    /// <summary>
    /// 初始化 UI 元素喵~
    /// 端口会自动根据 Data 类的标签"长"出来，这里只画特殊控件喵！
    /// </summary>
    private void InitializeUI()
    {
        title = "⚡ 命令";
        style.width = 300;
        titleContainer.style.backgroundColor = GetNodeColor();

        // --- 配置区域 ---
        var foldout = new Foldout() { text = "命令配置", value = true };

        // 获取所有分类和当前命令的分类
        var categories = CommandRegistryInfo.GetAllCategories();
        string currentCategory = CommandRegistryInfo.GetCategoryFromCommandName(TypedData.Command.CommandName);
        if (!categories.Contains(currentCategory))
        {
            currentCategory = categories[0]; // 使用第一个有效分类
        }

        // 分类选择下拉框
        _categoryDropdown = new PopupField<string>("分类", categories, currentCategory);
        _categoryDropdown.RegisterValueChangedCallback(evt =>
        {
            // 分类改变时，更新命令下拉框
            UpdateCommandDropdown(evt.newValue);
        });
        foldout.Add(_categoryDropdown);

        // 命令类型下拉框（根据分类动态加载）
        var commandChoices = CommandRegistryInfo.GetCommandsInCategory(currentCategory);
        if (commandChoices.Count == 0)
        {
            commandChoices.Add("spawn"); // 默认选项
        }

        // 获取当前命令名的显示名，如果不在列表中则使用第一个选项
        string currentDisplayName = CommandRegistryInfo.GetDisplayNameFromCommandName(TypedData.Command.CommandName);
        if (!commandChoices.Contains(currentDisplayName))
        {
            currentDisplayName = commandChoices[0]; // 使用第一个有效选项
        }

        _commandDropdown = new PopupField<string>("命令", commandChoices, currentDisplayName);
        _commandDropdown.RegisterValueChangedCallback(evt =>
        {
            TypedData.Command.CommandName = CommandRegistryInfo.GetCommandNameFromDisplayName(evt.newValue);
            RebuildParamFields();
            titleContainer.style.backgroundColor = GetNodeColor();
        });
        foldout.Add(_commandDropdown);

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
    /// 更新命令下拉框的选项喵~
    /// </summary>
    private void UpdateCommandDropdown(string category)
    {
        var commandChoices = CommandRegistryInfo.GetCommandsInCategory(category);
        if (commandChoices.Count == 0)
        {
            commandChoices.Add("spawn");
        }

        _commandDropdown.choices = commandChoices;
        
        // 选择第一个命令并更新数据
        if (commandChoices.Count > 0)
        {
            string firstCommandName = CommandRegistryInfo.GetCommandNameFromDisplayName(commandChoices[0]);
            TypedData.Command.CommandName = firstCommandName;
            _commandDropdown.value = commandChoices[0];
            RebuildParamFields();
            titleContainer.style.backgroundColor = GetNodeColor();
        }
    }

    /// <summary>
    /// 根据当前命令类型重建参数输入框喵~
    /// </summary>
    private void RebuildParamFields()
    {
        _paramFields.Clear();
        _paramContainer.Clear();

        if (CommandRegistryInfo.TryGetCommandInfo(TypedData.Command.CommandName, out var info))
        {
            // 更新提示信息
            _tooltipLabel.text = $"ℹ️ {info.Tooltip}";

            // 为每个参数创建输入框
            for (int i = 0; i < info.ParameterNames.Length; i++)
            {
                var field = new TextField(info.ParameterNames[i]);
                field.value = TypedData.Command.GetParam(i, "");

                int index = i;  // 闭包变量
                field.RegisterValueChangedCallback(evt =>
                {
                    TypedData.Command.SetParam(index, evt.newValue);
                });

                _paramFields.Add(field);
                _paramContainer.Add(field);
            }
        }
        else
        {
            _tooltipLabel.text = "ℹ️ 未知命令，请检查命令名是否正确";
        }
    }

    /// <summary>
    /// 获取节点颜色喵~
    /// </summary>
    private Color GetNodeColor()
    {
        if (CommandRegistryInfo.TryGetCommandInfo(TypedData.Command.CommandName, out var info))
            return info.EditorColor;
        return new Color(0.8f, 0.2f, 0.2f); // 默认红色
    }

    public override void UpdateData()
    {
        // 数据已经在回调中实时更新
        // 这里可以保存最终状态
        for (int i = 0; i < _paramFields.Count; i++)
        {
            TypedData.Command.SetParam(i, _paramFields[i].value);
        }
    }
}
#endif
