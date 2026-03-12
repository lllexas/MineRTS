using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 匹配评估器接口 - 定义事件 Payload 与 TriggerData 参数的比对契约喵~
/// </summary>
public interface IMatchEvaluator
{
    /// <summary>
    /// 检查 Payload 是否匹配触发器参数
    /// </summary>
    /// <param name="payload">事件载荷对象</param>
    /// <param name="parameters">TriggerData 中的参数列表</param>
    /// <returns>是否匹配</returns>
    bool Check(object payload, IReadOnlyList<string> parameters);
}

/// <summary>
/// 触发器注册表 - 统一管理所有触发器类型的定义和匹配契约喵~
/// 定义即契约：每个事件类型必须明确它的 Payload 类型和比对逻辑
/// </summary>
public static class TriggerRegistry
{
    /// <summary>
    /// 触发器类型信息喵~
    /// </summary>
    public class TriggerTypeInfo
    {
        public string EventName;          // 内部名（如 "Time", "UnitKilled"）
        public string DisplayName;        // 显示名（如 "⏰ 时间触发"）
        public string[] ParameterNames;   // 参数名列表（如 ["等待时间 (秒)"]）
        public string Tooltip;            // 提示信息
        public Color EditorColor;         // 编辑器中的颜色
        public Type PayloadType;          // Payload 的预期类型
        public IMatchEvaluator Evaluator; // 匹配评估器
    }

    // 注册表喵~
    private static Dictionary<string, TriggerTypeInfo> _types = new Dictionary<string, TriggerTypeInfo>();

    /// <summary>
    /// 初始化：注册所有内置触发器类型喵~
    /// </summary>
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        // 防止重复初始化
        if (_types.Count > 0) return;

        InitializeInternal();
    }

    /// <summary>
    /// 内部初始化方法（编辑器也可调用）喵~
    /// </summary>
    private static void InitializeInternal()
    {
        // 任务完成触发器
        RegisterType("MissionCompleted", "🏆 任务完成",
            new[] { "任务 ID" },
            "当指定任务完成时触发",
            new Color(0.2f, 0.6f, 0.2f),
            typeof(MissionArgs),
            new MissionCompletedMatchEvaluator());

        // 单位被击败触发器
        RegisterType("UnitKilled", "💀 单位被击败",
            new[] { "单位 BlueprintID" },
            "当指定单位被击败时触发（数量由 RequiredAmount 控制）",
            new Color(0.6f, 0.2f, 0.2f),
            typeof(string),
            new UnitKilledMatchEvaluator());

        // 到达区域触发器
        RegisterType("AreaReached", "📍 到达区域",
            new[] { "区域 ID 或坐标" },
            "当单位到达指定区域时触发",
            new Color(0.2f, 0.4f, 0.6f),
            typeof(string),
            new AreaReachedMatchEvaluator());

        // 自定义事件触发器
        RegisterType("Custom", "📢 自定义事件",
            new[] { "事件名", "参数 1(可选)", "参数 2(可选)" },
            "监听任意自定义事件",
            new Color(0.6f, 0.3f, 0.6f),
            typeof(object),
            new CustomMatchEvaluator());
    }

    /// <summary>
    /// 编辑器初始化入口喵~
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void EditorInitialize()
    {
        InitializeInternal();
    }
#endif

    /// <summary>
    /// 注册触发器类型（完整版）喵~
    /// </summary>
    public static void RegisterType(string eventName, string displayName,
                                    string[] parameterNames, string tooltip,
                                    Color editorColor,
                                    Type payloadType,
                                    IMatchEvaluator evaluator)
    {
        _types[eventName] = new TriggerTypeInfo
        {
            EventName = eventName,
            DisplayName = displayName,
            ParameterNames = parameterNames,
            Tooltip = tooltip,
            EditorColor = editorColor,
            PayloadType = payloadType,
            Evaluator = evaluator
        };
    }

    /// <summary>
    /// 注册触发器类型（简化版，无评估器）喵~
    /// </summary>
    public static void RegisterType(string eventName, string displayName,
                                    string[] parameterNames, string tooltip,
                                    Color editorColor)
    {
        RegisterType(eventName, displayName, parameterNames, tooltip, editorColor, typeof(object), null);
    }

    /// <summary>
    /// 获取所有类型（用于 UI 下拉菜单）喵~
    /// </summary>
    public static IEnumerable<TriggerTypeInfo> GetAllTypes()
    {
        return _types.Values;
    }

    /// <summary>
    /// 获取类型信息喵~
    /// </summary>
    public static bool TryGetTypeInfo(string eventName, out TriggerTypeInfo info)
    {
        return _types.TryGetValue(eventName, out info);
    }

    /// <summary>
    /// 获取匹配评估器喵~
    /// </summary>
    public static bool TryGetEvaluator(string eventName, out IMatchEvaluator evaluator)
    {
        if (_types.TryGetValue(eventName, out var info))
        {
            evaluator = info.Evaluator;
            return evaluator != null;
        }
        evaluator = null;
        return false;
    }

    /// <summary>
    /// 根据显示名获取事件名喵~
    /// </summary>
    public static string GetEventNameFromDisplayName(string displayName)
    {
        foreach (var type in _types.Values)
        {
            if (type.DisplayName == displayName)
                return type.EventName;
        }
        return displayName;
    }

    /// <summary>
    /// 根据事件名获取显示名喵~
    /// </summary>
    public static string GetDisplayNameFromEventName(string eventName)
    {
        if (TryGetTypeInfo(eventName, out var info))
            return info.DisplayName;
        return eventName;
    }

    // =========================================================
    // 内置匹配评估器实现
    // =========================================================

    /// <summary>
    /// 任务完成触发器匹配评估器喵~
    /// Payload: MissionArgs
    /// </summary>
    private class MissionCompletedMatchEvaluator : IMatchEvaluator
    {
        public bool Check(object payload, IReadOnlyList<string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return false;

            string missionId = parameters[0];

            if (payload is MissionArgs args)
            {
                return args.IntKey.ToString() == missionId || 
                       args.StringKey == missionId;
            }

            if (payload is MissionNode_A_Data missionData)
            {
                return missionData.MissionID == missionId;
            }

            return false;
        }
    }

    /// <summary>
    /// 单位被击败触发器匹配评估器喵~
    /// Payload: string (BlueprintID)
    /// </summary>
    private class UnitKilledMatchEvaluator : IMatchEvaluator
    {
        public bool Check(object payload, IReadOnlyList<string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return false;

            string requiredId = parameters[0];

            if (payload is string killedId)
            {
                return killedId.ToLower() == requiredId.ToLower();
            }

            return false;
        }
    }

    /// <summary>
    /// 到达区域触发器匹配评估器喵~
    /// Payload: string (AreaID)
    /// </summary>
    private class AreaReachedMatchEvaluator : IMatchEvaluator
    {
        public bool Check(object payload, IReadOnlyList<string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return true; // 无条件匹配

            string requiredArea = parameters[0];

            if (payload is string areaId)
            {
                return areaId.ToLower() == requiredArea.ToLower();
            }

            return false;
        }
    }

    /// <summary>
    /// 自定义事件触发器匹配评估器喵~
    /// Payload: object (任意)
    /// </summary>
    private class CustomMatchEvaluator : IMatchEvaluator
    {
        public bool Check(object payload, IReadOnlyList<string> parameters)
        {
            // 如果没有参数，直接匹配
            if (parameters == null || parameters.Count == 0)
                return true;

            // 如果只有一个参数且为空，也匹配
            if (parameters.Count == 1 && string.IsNullOrEmpty(parameters[0]))
                return true;

            // 尝试匹配第一个参数
            string condition = parameters[0];

            if (payload is string strPayload)
            {
                return condition.ToLower() == strPayload.ToLower();
            }

            // 其他类型尝试 ToString 匹配
            if (payload != null)
            {
                return condition.ToLower() == payload.ToString().ToLower();
            }

            return false;
        }
    }
}
