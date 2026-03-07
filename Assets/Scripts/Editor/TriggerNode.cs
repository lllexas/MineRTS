#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 触发器节点 - 监听游戏事件（串并联逻辑）喵~
/// Mission 和 Story 系统共用
/// </summary>
public class TriggerNode : BaseNode
{
    public TriggerNodeData Data;
    public Port InputPort;
    public Port OutputPort;
    private EnumField _triggerTypeField;
    private TextField _paramField;
    private Toggle _useEnumToggle;

    public TriggerNode(TriggerNodeData data)
    {
        Data = data; GUID = data.NodeID; title = "🎬 触发器";
        style.width = 250; titleContainer.style.backgroundColor = new Color(0.4f, 0.1f, 0.4f);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "条件"; inputContainer.Add(InputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        OutputPort.portName = "触发行为"; outputContainer.Add(OutputPort);

        // --- 使用 TriggerData 统一结构喵~ ---
        var foldout = new Foldout() { text = "触发器配置", value = true };

        // 使用枚举触发器开关
        _useEnumToggle = new Toggle("使用预设触发器");
        _useEnumToggle.value = Data.Trigger.UseEnumTrigger;
        _useEnumToggle.RegisterValueChangedCallback(evt =>
        {
            Data.Trigger.UseEnumTrigger = evt.newValue;
            UpdateUIVisibility();
        });
        foldout.Add(_useEnumToggle);

        // 枚举触发器类型
        _triggerTypeField = new EnumField("触发类型", Data.Trigger.TriggerType);
        _triggerTypeField.RegisterValueChangedCallback(evt =>
        {
            Data.Trigger.TriggerType = (TriggerType)evt.newValue;
            UpdateParamFieldHint();
        });
        foldout.Add(_triggerTypeField);

        // 触发参数
        _paramField = new TextField("参数");
        _paramField.value = Data.Trigger.TriggerParam;
        _paramField.RegisterValueChangedCallback(evt => Data.Trigger.TriggerParam = evt.newValue);
        foldout.Add(_paramField);

        extensionContainer.Add(foldout);

        // 初始化 UI 状态和提示
        UpdateUIVisibility();
        UpdateParamFieldHint();

        RefreshExpandedState();
    }

    private void UpdateUIVisibility()
    {
        bool useEnum = Data.Trigger.UseEnumTrigger;
        _triggerTypeField.style.display = useEnum ? DisplayStyle.Flex : DisplayStyle.None;
        _paramField.style.display = useEnum ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void UpdateParamFieldHint()
    {
        string hint = GetTriggerParamHint(Data.Trigger.TriggerType);
        string example = GetTriggerParamExample(Data.Trigger.TriggerType);

        // 在 tooltip 中同时显示提示和示例
        if (!string.IsNullOrEmpty(example))
        {
            _paramField.tooltip = $"{hint}\n\n示例：{example}";
        }
        else
        {
            _paramField.tooltip = hint;
        }

        // 尝试更新 TextField 的标签（如果支持）
        try
        {
            string labelText = GetTriggerParamLabel(Data.Trigger.TriggerType);
            // 直接尝试设置 label 属性（如果存在）
            // Unity UI Elements 的 TextField 有 label 属性
            _paramField.label = labelText;
        }
        catch
        {
            // 如果 label 属性不存在或不可写，忽略错误
        }
    }

    private string GetTriggerParamHint(TriggerType triggerType)
    {
        switch (triggerType)
        {
            case TriggerType.Time:
                return "触发时间（秒），浮点数。游戏运行达到此时间后触发事件。";
            case TriggerType.MissionCompleted:
                return "任务 ID。当指定任务完成时触发事件。";
            case TriggerType.AreaReached:
                return "区域标识符或坐标。当单位到达指定区域时触发。";
            case TriggerType.Custom:
                return "自定义事件名（中文）";
            default:
                return "触发条件参数";
        }
    }

    private string GetTriggerParamExample(TriggerType triggerType)
    {
        switch (triggerType)
        {
            case TriggerType.Time:
                return "例如：60.0, 120.5, 300";
            case TriggerType.MissionCompleted:
                return "例如：mission_tutorial_1, wave_1_clear";
            case TriggerType.AreaReached:
                return "例如：enemy_base, 128,64";
            default:
                return "";
        }
    }

    private string GetTriggerParamLabel(TriggerType triggerType)
    {
        switch (triggerType)
        {
            case TriggerType.Time:
                return "时间（秒）";
            case TriggerType.MissionCompleted:
                return "任务 ID";
            case TriggerType.AreaReached:
                return "区域参数";
            case TriggerType.Custom:
                return "事件名";
            default:
                return "参数";
        }
    }

    public override void UpdateData()
    {
        // 更新 TriggerData 数据
        if (Data != null)
        {
            Data.Trigger.UseEnumTrigger = _useEnumToggle.value;
            Data.Trigger.TriggerType = (TriggerType)_triggerTypeField.value;
            Data.Trigger.TriggerParam = _paramField.value;
        }
    }
}
#endif
