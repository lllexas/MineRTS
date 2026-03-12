using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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
    // 包装器
    private class Handler
    {
        // 那个“活着的”对象，用于判断是否销毁。
        // 对于反射模式，它是 host object。
        // 对于委托模式，它是 action.Target (通常就是闭包所属的对象)。
        public object Target;
        public Action<object> Action;
        public int Priority;
    }

    // 主表：事件名 -> 处理器列表
    private readonly Dictionary<string, List<Handler>> _eventTable = new Dictionary<string, List<Handler>>();

    // 反向索引：对象实例 -> 事件名列表 (用于快速注销)
    private readonly Dictionary<object, HashSet<string>> _targetToEvents = new Dictionary<object, HashSet<string>>();

    // =========================================================
    // API 1: 发送事件 (通用)
    // =========================================================
    public void Send(string eventName, object data = null)
    {
        if (_eventTable.TryGetValue(eventName, out var list))
        {
            // 倒序遍历，安全删除
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var h = list[i];
                try
                {
                    // --- 僵尸检查 ---
                    // 如果 Target 是 Unity Object 且已被销毁 (== null)，或者 C# 对象被 GC (这很难发生因为被列表引用着，主要防 Unity 销毁)
                    // 注意：Static 方法的 Target 是 null，这种永远不会被自动清理，必须手动 Off
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
    // API 2: 传统订阅 (On / Off) - 临时/Lambda模式
    // =========================================================

    /// <summary>
    /// 传统方式订阅。适合动态逻辑或 Lambda。
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <param name="callback">回调 (必须接受 object 参数)</param>
    /// <param name="priority">优先级</param>
    public void On(string eventName, Action<object> callback, int priority = 0)
    {
        if (callback == null) return;
        // callback.Target 就是这个委托所属的对象实例 (如果是 Lambda，就是闭包类实例)
        AddHandler(eventName, callback.Target, callback, priority);
    }

    /// <summary>
    /// 取消订阅指定的回调。
    /// </summary>
    public void Off(string eventName, Action<object> callback)
    {
        if (callback == null) return;

        if (_eventTable.TryGetValue(eventName, out var list))
        {
            // 找到特定的那个委托并移除
            // Delegate 的判等会自动处理 Target 和 Method 的匹配
            list.RemoveAll(h => h.Action == callback);
        }
    }

    // =========================================================
    // API 3: 反射订阅 (Register / Unregister) - 标签模式
    // =========================================================

    /// <summary>
    /// 扫描对象上所有 [Subscribe] 标签并自动注册（包括基类中的标签）。
    /// </summary>
    public void Register(object target)
    {
        if (target == null) return;

        var type = target.GetType();
        
        // 递归扫描该类型及其所有基类，直到 System.Object
        while (type != null && type != typeof(object))
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var attrs = method.GetCustomAttributes<Subscribe>();
                if (attrs == null) continue;

                foreach (var attr in attrs)
                {
                    try
                    {
                        var action = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), target, method);
                        AddHandler(attr.EventName, target, action, attr.Priority);
                    }
                    catch
                    {
                        Debug.LogError($"[PostSystem] Register Error: {target.GetType().Name}.{method.Name} signature mismatch.");
                    }
                }
            }
            
            type = type.BaseType;
        }
    }

    /// <summary>
    /// 注销该对象上所有的 [Subscribe] 监听，同时也注销该对象上绑定的所有 On 监听。
    /// </summary>
    public void Unregister(object target)
    {
        if (target == null) return;

        if (_targetToEvents.TryGetValue(target, out var eventNames))
        {
            foreach (var name in eventNames)
            {
                if (_eventTable.TryGetValue(name, out var handlers))
                {
                    handlers.RemoveAll(h => h.Target == target);
                }
            }
            _targetToEvents.Remove(target);
        }
    }

    // =========================================================
    // 内部核心
    // =========================================================
    private void AddHandler(string eventName, object target, Action<object> action, int priority)
    {
        if (!_eventTable.TryGetValue(eventName, out var list))
        {
            list = new List<Handler>();
            _eventTable[eventName] = list;
        }

        // 优先级插入
        int index = 0;
        while (index < list.Count && list[index].Priority >= priority) index++;

        list.Insert(index, new Handler { Target = target, Action = action, Priority = priority });

        // 记录反向索引 (如果 target 不为 null)
        // 静态方法的 Target 是 null，这种情况下我们没法做反向索引注销，只能靠 Off 手动注销
        if (target != null)
        {
            if (!_targetToEvents.TryGetValue(target, out var eventSet))
            {
                eventSet = new HashSet<string>();
                _targetToEvents[target] = eventSet;
            }
            eventSet.Add(eventName);
        }
    }

    public void ClearAll()
    {
        _eventTable.Clear();
        _targetToEvents.Clear();
    }
}