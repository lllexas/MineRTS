using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 触发器数据 - 响应式触发器核心喵~
/// 数据自治：持有监听句柄并管理自身的总线注册/取消行为
/// </summary>
[Serializable]
public class TriggerData
{
    [Tooltip("触发器事件名（对应 PostSystem 中的事件名）")]
    public string EventName = "生存时间增加";  // 如 "生存时间增加", "UnitKilled"

    [Tooltip("触发器参数列表，数量和含义由 EventName 决定")]
    public List<string> Parameters = new List<string>();  // 灵活支持多个参数

    [Tooltip("是否已触发（运行时状态）")]
    public bool HasTriggered;  // 运行时状态喵~

    // =========================================================
    // 运行时私有字段 - 不参与序列化
    // =========================================================
    private Action<object> _busHandler;      // 总线监听句柄
    private bool _isRegistered = false;      // 是否已注册到总线
    private IMatchEvaluator _evaluator;      // 匹配评估器（运行时缓存）

    // =========================================================
    // 公共 API - 数据自治核心
    // =========================================================

    /// <summary>
    /// 注册监听器到 PostSystem - "通电"喵~
    /// 从 TriggerRegistry 获取对应的 Evaluator，构造闭包处理匹配逻辑
    /// </summary>
    /// <param name="onTriggered">触发回调（Payload 已匹配通过）</param>
    public void Register(Action<object> onTriggered)
    {
        if (_isRegistered)
        {
            Debug.LogWarning($"[TriggerData] {EventName} 已经注册过了，重复注册被忽略喵~");
            return;
        }

        // 获取匹配评估器
        if (!TriggerRegistry.TryGetEvaluator(EventName, out _evaluator))
        {
            Debug.LogWarning($"[TriggerData] 未找到事件 [{EventName}] 的匹配评估器，使用默认评估器喵~");
            _evaluator = new DefaultMatchEvaluator();
        }

        // 构造总线处理器闭包
        _busHandler = (payload) =>
        {
            // 执行匹配判定
            bool isMatch = _evaluator?.Check(payload, Parameters) ?? true;

            if (isMatch)
            {
                // 标记为已触发
                HasTriggered = true;

                // 执行触发回调
                onTriggered?.Invoke(payload);

                // 自动注销（一次性触发器）
                Unregister();
            }
        };

        // 注册到 PostSystem
        PostSystem.Instance.On(EventName, _busHandler);
        _isRegistered = true;

        if (GraphRunner.Instance != null && GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[TriggerData] 监听已注册：{EventName} -> {Parameters.Count} 个参数");
        }
    }

    /// <summary>
    /// 注销监听器 - "断电"喵~
    /// 清理总线句柄，释放资源
    /// </summary>
    public void Unregister()
    {
        if (!_isRegistered || _busHandler == null)
        {
            return;
        }

        // 从 PostSystem 注销
        PostSystem.Instance.Off(EventName, _busHandler);

        // 清理句柄
        _busHandler = null;
        _evaluator = null;
        _isRegistered = false;

        if (GraphRunner.Instance != null && GraphRunner.Instance.EnableDebugLog)
        {
            Debug.Log($"[TriggerData] 监听已注销：{EventName}");
        }
    }

    /// <summary>
    /// 检查是否已注册喵~
    /// </summary>
    public bool IsRegistered => _isRegistered;

    // =========================================================
    // 辅助方法 - 参数安全访问
    // =========================================================

    /// <summary>
    /// 获取参数值（安全访问）喵~
    /// </summary>
    /// <param name="index">参数索引</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>参数值或默认值</returns>
    public string GetParam(int index, string defaultValue = "")
    {
        if (Parameters == null || index < 0 || index >= Parameters.Count)
            return defaultValue;
        return Parameters[index] ?? defaultValue;
    }

    /// <summary>
    /// 设置参数值（自动扩展列表）喵~
    /// </summary>
    /// <param name="index">参数索引</param>
    /// <param name="value">参数值</param>
    public void SetParam(int index, string value)
    {
        if (Parameters == null)
            Parameters = new List<string>();

        while (Parameters.Count <= index)
            Parameters.Add("");

        Parameters[index] = value;
    }

    /// <summary>
    /// 尝试将参数解析为浮点数喵~
    /// </summary>
    public bool TryGetParamAsFloat(int index, out float result, float defaultValue = 0f)
    {
        string param = GetParam(index, null);
        if (param == null)
        {
            result = defaultValue;
            return false;
        }

        if (float.TryParse(param, out result))
            return true;

        result = defaultValue;
        return false;
    }

    /// <summary>
    /// 尝试将参数解析为整数喵~
    /// </summary>
    public bool TryGetParamAsInt(int index, out int result, int defaultValue = 0)
    {
        string param = GetParam(index, null);
        if (param == null)
        {
            result = defaultValue;
            return false;
        }

        if (int.TryParse(param, out result))
            return true;

        result = defaultValue;
        return false;
    }

    // =========================================================
    // 默认匹配评估器（兜底用）
    // =========================================================
    private class DefaultMatchEvaluator : IMatchEvaluator
    {
        public bool Check(object payload, IReadOnlyList<string> parameters)
        {
            // 如果没有参数，直接匹配
            if (parameters == null || parameters.Count == 0)
                return true;

            // 尝试匹配第一个参数
            string condition = parameters[0];
            if (string.IsNullOrEmpty(condition))
                return true;

            if (payload is string strPayload)
            {
                return condition.ToLower() == strPayload.ToLower();
            }

            if (payload != null)
            {
                return condition.ToLower() == payload.ToString().ToLower();
            }

            return false;
        }
    }
}
