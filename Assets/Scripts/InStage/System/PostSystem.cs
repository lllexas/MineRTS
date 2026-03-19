using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;

// =========================================================
// 1. 标签定义 (反射模式专用)
// =========================================================
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class Subscribe : Attribute
{
    public string EventName { get; }
    public int Priority { get; }

    public Subscribe(string eventName, int priority = 0)
    {
        EventName = eventName;
        Priority = priority;
    }
}

// =========================================================
// 2. 双模事件总线 (PostSystem)
// =========================================================
public class PostSystem : SingletonMono<PostSystem>
{
    private class Handler
    {
        public object Target;
        public Action<object> Action;
        public int Priority;
    }

    private readonly Dictionary<string, List<Handler>> _eventTable = new Dictionary<string, List<Handler>>();
    private readonly Dictionary<object, HashSet<string>> _targetToEvents = new Dictionary<object, HashSet<string>>();

    // 缓存所有 TriggerEvent 的字符串形式，用于快速比对喵~
    private static HashSet<string> _triggerEventStrings;

    private void EnsureTriggerEventCache()
    {
        if (_triggerEventStrings == null)
        {
            _triggerEventStrings = new HashSet<string>(Enum.GetNames(typeof(TriggerEvent)));
        }
    }

    // =========================================================
    // API 1: 发送事件 (分级入口)
    // =========================================================

    /// <summary>
    /// 🚀【普通货运窗口】分发原始事件。
    /// ⚠️ 严禁在此直接发送 TriggerEvent 定义的契约事件喵！
    /// </summary>
    public void Send(string eventName, object data = null)
    {
#if UNITY_EDITOR || DEBUG
        EnsureTriggerEventCache();
        if (_triggerEventStrings.Contains(eventName))
        {
            Debug.LogError($"<color=red>[PostSystem] 阻断违规操作！</color> 事件 [{eventName}] 属于 NekoGraph 契约事件，" +
                           $"【严禁】直接调用 PostSystem.Send！请务必通过 PostOffice.Send() 进行强类型分发喵！");
            return; 
        }
#endif
        SendInternal(eventName, data);
    }

    /// <summary>
    /// 🔐【授权货运窗口】仅限 PostOffice 调用喵！
    /// </summary>
    public void SendFromPostOffice(string eventName, object data)
    {
        SendInternal(eventName, data);
    }

    private void SendInternal(string eventName, object data)
    {
        if (_eventTable.TryGetValue(eventName, out var list))
        {
            // 倒序遍历，安全删除喵~
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var h = list[i];
                try
                {
                    if (h.Target != null && h.Target.Equals(null))
                    {
                        list.RemoveAt(i);
                        continue;
                    }
                    h.Action.Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"<color=red>[PostSystem] {eventName} Error: {e}</color>");
                }
            }
        }
    }

    // =========================================================
    // API 2: 反射注册模式 (Register / Unregister)
    // =========================================================

    /// <summary>
    /// 自动扫描并注册对象中所有带有 [Subscribe] 特性的方法喵！
    /// 注意：沿继承链向上遍历，确保基类的 private 方法也能被正确扫描到喵~
    /// </summary>
    public void Register(object target)
    {
        if (target == null) return;

        // 沿继承链向上遍历，DeclaredOnly 每次只扫当前层自己声明的方法
        // 这样基类的 private [Subscribe] 方法也能被正确找到喵~
        var type = target.GetType();
        while (type != null && type != typeof(MonoBehaviour) && type != typeof(object))
        {
            var methods = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var attrs = method.GetCustomAttributes<Subscribe>();
                foreach (var attr in attrs)
                {
                    // 捕获当前迭代的 method，避免闭包陷阱喵~
                    var capturedMethod = method;
                    var paramCount = method.GetParameters().Length;
                    Action<object> action = (data) =>
                    {
                        if (paramCount == 0)
                            capturedMethod.Invoke(target, null);
                        else
                            capturedMethod.Invoke(target, new[] { data });
                    };

                    AddHandler(attr.EventName, target, action, attr.Priority);
                }
            }

            type = type.BaseType;
        }
    }

    /// <summary>
    /// 快速注销该对象订阅的所有事件喵！
    /// </summary>
    public void Unregister(object target)
    {
        if (target == null) return;
        if (_targetToEvents.TryGetValue(target, out var events))
        {
            foreach (var evtName in events.ToList())
            {
                if (_eventTable.TryGetValue(evtName, out var list))
                {
                    list.RemoveAll(h => h.Target == target);
                }
            }
            _targetToEvents.Remove(target);
        }
    }

    // =========================================================
    // API 3: 传统委托模式 (On / Off)
    // =========================================================

    public void On(string eventName, Action<object> callback, int priority = 0)
    {
        if (callback == null) return;
        AddHandler(eventName, callback.Target, callback, priority);
    }

    public void Off(string eventName, Action<object> callback)
    {
        if (callback == null) return;
        if (_eventTable.TryGetValue(eventName, out var list))
        {
            var handler = list.FirstOrDefault(h => h.Action == callback);
            if (handler != null)
            {
                list.Remove(handler);
                if (handler.Target != null && _targetToEvents.TryGetValue(handler.Target, out var events))
                {
                    events.Remove(eventName);
                }
            }
        }
    }

    private void AddHandler(string eventName, object target, Action<object> action, int priority)
    {
        if (!_eventTable.TryGetValue(eventName, out var list))
        {
            list = new List<Handler>();
            _eventTable[eventName] = list;
        }

        // 防止重复注册同一 Action 喵
        if (list.Any(h => h.Action == action)) return;

        list.Add(new Handler { Target = target, Action = action, Priority = priority });
        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        if (target != null)
        {
            if (!_targetToEvents.TryGetValue(target, out var events))
            {
                events = new HashSet<string>();
                _targetToEvents[target] = events;
            }
            events.Add(eventName);
        }
    }
}
