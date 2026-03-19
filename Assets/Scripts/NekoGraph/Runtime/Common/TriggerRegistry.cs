using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;

/// <summary>
/// 事件监听注册表 - 基于 TriggerEvent 枚举的自动同步喵！
/// 职责：为编辑器提供分类显示和协议校验喵~
/// </summary>
public static class TriggerRegistry
{
    public class TriggerMeta
    {
        public TriggerEvent Event;
        public EventInfoAttribute Info;
    }

    private static readonly Dictionary<TriggerEvent, TriggerMeta> _triggers = new Dictionary<TriggerEvent, TriggerMeta>();
    private static bool _isInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        _triggers.Clear();
        var enumType = typeof(TriggerEvent);
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);

        foreach (var field in fields)
        {
            var attr = field.GetCustomAttribute<EventInfoAttribute>();
            if (attr != null)
            {
                var eventValue = (TriggerEvent)field.GetValue(null);
                _triggers[eventValue] = new TriggerMeta { Event = eventValue, Info = attr };
            }
        }
        _isInitialized = true;
        // Debug.Log($"[TriggerRegistry] 自动同步完成，加载了 {_triggers.Count} 个事件契约喵~");
    }

    private static void EnsureInitialized()
    {
        if (!_isInitialized || _triggers.Count == 0)
        {
            Initialize();
        }
    }

    public static IEnumerable<TriggerMeta> GetAllTriggers()
    {
        EnsureInitialized();
        return _triggers.Values;
    }

    public static TriggerMeta GetMeta(TriggerEvent evt)
    {
        EnsureInitialized();
        return _triggers.TryGetValue(evt, out var meta) ? meta : null;
    }

    /// <summary>
    /// 根据事件名找到对应的枚举喵~
    /// </summary>
    public static TriggerEvent Parse(string eventName)
    {
        EnsureInitialized();
        if (Enum.TryParse<TriggerEvent>(eventName, out var result)) return result;
        return TriggerEvent.GameStarted; // 默认
    }
}
