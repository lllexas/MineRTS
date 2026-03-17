#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using NekoGraph;

// =========================================================
// MissionNode_A - 任务发起点（Activation）喵~
// =========================================================

/// <summary>
/// 任务发起节点 UI - Mission 系统专用喵~
/// 【端口标签驱动·自动组装重构版】
/// </summary>
[NodeMenuItem("🎮 任务/任务发起点", typeof(MissionNode_A_Data))]
[NodeType(NodeSystem.Mission)]
public class MissionNode_A : BaseNode<MissionNode_A_Data>
{
    private TextField _idField;
    private TextField _titleField;
    private TextField _descField;
    private PopupField<string> _priorityDropdown;
    private Toggle _activeToggle;

    public MissionNode_A() : base()
    {
        InitializeUI();
    }

    public MissionNode_A(MissionNode_A_Data data) : base(data)
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        title = "🎮 任务发起";
        style.width = 300;
        titleContainer.style.backgroundColor = GetPriorityColor(TypedData.Priority);

        var foldout = new Foldout() { text = "任务配置", value = true };

        // 任务 ID
        _idField = new TextField("任务 ID");
        _idField.value = TypedData.MissionID;
        _idField.RegisterValueChangedCallback(evt => TypedData.MissionID = evt.newValue);
        foldout.Add(_idField);

        // 任务标题
        _titleField = new TextField("标题");
        _titleField.value = TypedData.Title;
        _titleField.RegisterValueChangedCallback(evt => TypedData.Title = evt.newValue);
        foldout.Add(_titleField);

        // 任务描述
        _descField = new TextField("描述") { multiline = true, style = { minHeight = 60 } };
        _descField.value = TypedData.Description;
        _descField.RegisterValueChangedCallback(evt => TypedData.Description = evt.newValue);
        foldout.Add(_descField);

        // 优先级
        var priorityChoices = new List<string> { "🌟 主线 (Main)", "⭐ 支线 (Side)", "📖 引导 (Tutorial)" };
        _priorityDropdown = new PopupField<string>("优先级", priorityChoices, GetPriorityString(TypedData.Priority));
        _priorityDropdown.RegisterValueChangedCallback(evt =>
        {
            TypedData.Priority = GetPriorityFromString(evt.newValue);
            titleContainer.style.backgroundColor = GetPriorityColor(TypedData.Priority);
        });
        foldout.Add(_priorityDropdown);

        // 激活状态
        _activeToggle = new Toggle("已激活");
        _activeToggle.value = TypedData.IsActive;
        _activeToggle.RegisterValueChangedCallback(evt => TypedData.IsActive = evt.newValue);
        foldout.Add(_activeToggle);

        extensionContainer.Add(foldout);
        RefreshExpandedState();
    }

    private string GetPriorityString(MissionPriority priority)
    {
        switch (priority)
        {
            case MissionPriority.Main: return "🌟 主线 (Main)";
            case MissionPriority.Side: return "⭐ 支线 (Side)";
            case MissionPriority.Tutorial: return "📖 引导 (Tutorial)";
            default: return "⭐ 支线 (Side)";
        }
    }

    private MissionPriority GetPriorityFromString(string value)
    {
        if (value.Contains("主线")) return MissionPriority.Main;
        if (value.Contains("支线")) return MissionPriority.Side;
        if (value.Contains("引导")) return MissionPriority.Tutorial;
        return MissionPriority.Side;
    }

    private Color GetPriorityColor(MissionPriority priority)
    {
        switch (priority)
        {
            case MissionPriority.Main: return new Color(1f, 0.6f, 0.2f);
            case MissionPriority.Side: return new Color(0.2f, 0.6f, 0.2f);
            case MissionPriority.Tutorial: return new Color(0.2f, 0.4f, 0.8f);
            default: return new Color(0.5f, 0.5f, 0.5f);
        }
    }

    public override void UpdateData()
    {
        TypedData.MissionID = _idField.value;
        TypedData.Title = _titleField.value;
        TypedData.Description = _descField.value;
        TypedData.Priority = GetPriorityFromString(_priorityDropdown.value);
        TypedData.IsActive = _activeToggle.value;
    }
}

// =========================================================
// MissionNode_S - 任务成功点（Success）喵~
// =========================================================

/// <summary>
/// 任务成功节点 UI - Mission 系统专用喵~
/// 任务完成时触发成功信号喵~
/// </summary>
[NodeMenuItem("🎮 任务/任务成功点", typeof(MissionNode_S_Data))]
[NodeType(NodeSystem.Mission)]
public class MissionNode_S : BaseNode<MissionNode_S_Data>
{
    private TextField _missionIdField;

    public MissionNode_S() : base()
    {
        InitializeUI();
    }

    public MissionNode_S(MissionNode_S_Data data) : base(data)
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        title = "✅ 任务成功";
        style.width = 200;
        titleContainer.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f); // 🟢 绿色

        var foldout = new Foldout() { text = "成功配置", value = true };

        // 关联任务 ID
        _missionIdField = new TextField("任务 ID");
        _missionIdField.value = TypedData.MissionID;
        _missionIdField.RegisterValueChangedCallback(evt => TypedData.MissionID = evt.newValue);
        foldout.Add(_missionIdField);

        extensionContainer.Add(foldout);
        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        TypedData.MissionID = _missionIdField.value;
    }
}

// =========================================================
// MissionNode_F - 任务失败点（Failure）喵~
// =========================================================

/// <summary>
/// 任务失败节点 UI - Mission 系统专用喵~
/// 任务失败时触发失败信号喵~
/// </summary>
[NodeMenuItem("🎮 任务/任务失败点", typeof(MissionNode_F_Data))]
[NodeType(NodeSystem.Mission)]
public class MissionNode_F : BaseNode<MissionNode_F_Data>
{
    private TextField _missionIdField;

    public MissionNode_F() : base()
    {
        InitializeUI();
    }

    public MissionNode_F(MissionNode_F_Data data) : base(data)
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        title = "❌ 任务失败";
        style.width = 200;
        titleContainer.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f); // 🔴 红色

        var foldout = new Foldout() { text = "失败配置", value = true };

        // 关联任务 ID
        _missionIdField = new TextField("任务 ID");
        _missionIdField.value = TypedData.MissionID;
        _missionIdField.RegisterValueChangedCallback(evt => TypedData.MissionID = evt.newValue);
        foldout.Add(_missionIdField);

        extensionContainer.Add(foldout);
        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        TypedData.MissionID = _missionIdField.value;
    }
}

// =========================================================
// MissionNode_R - 任务刷新点（Refresh）喵~
// =========================================================

/// <summary>
/// 任务刷新节点 UI - Mission 系统专用喵~
/// 用于刷新任务进度或重新激活任务喵~
/// </summary>
[NodeMenuItem("🎮 任务/任务刷新点", typeof(MissionNode_R_Data))]
[NodeType(NodeSystem.Mission)]
public class MissionNode_R : BaseNode<MissionNode_R_Data>
{
    private TextField _missionIdField;
    private Toggle _resetProgressToggle;
    private Toggle _reactivateToggle;

    public MissionNode_R() : base()
    {
        InitializeUI();
    }

    public MissionNode_R(MissionNode_R_Data data) : base(data)
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        title = "🔄 任务刷新";
        style.width = 250;
        titleContainer.style.backgroundColor = new Color(0.2f, 0.6f, 0.8f); // 🔵 蓝绿色

        var foldout = new Foldout() { text = "刷新配置", value = true };

        // 关联任务 ID
        _missionIdField = new TextField("任务 ID");
        _missionIdField.value = TypedData.MissionID;
        _missionIdField.RegisterValueChangedCallback(evt => TypedData.MissionID = evt.newValue);
        foldout.Add(_missionIdField);

        // 重置进度
        _resetProgressToggle = new Toggle("重置进度");
        _resetProgressToggle.value = TypedData.ResetProgress;
        _resetProgressToggle.RegisterValueChangedCallback(evt => TypedData.ResetProgress = evt.newValue);
        foldout.Add(_resetProgressToggle);

        // 重新激活
        _reactivateToggle = new Toggle("重新激活");
        _reactivateToggle.value = TypedData.Reactivate;
        _reactivateToggle.RegisterValueChangedCallback(evt => TypedData.Reactivate = evt.newValue);
        foldout.Add(_reactivateToggle);

        extensionContainer.Add(foldout);
        RefreshExpandedState();
    }

    public override void UpdateData()
    {
        TypedData.MissionID = _missionIdField.value;
        TypedData.ResetProgress = _resetProgressToggle.value;
        TypedData.Reactivate = _reactivateToggle.value;
    }
}

#endif
