using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;

/// <summary>
/// 比较结果枚举喵~
/// </summary>
public enum ComparerResult
{
    Pass,           // 逻辑判定通过
    Fail,           // 逻辑判定失败
    TypeMismatch    // Payload 类型不匹配
}

/// <summary>
/// 比较器元数据特性喵~
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ComparerInfoAttribute : Attribute
{
    public EventProtocol Protocol { get; }
    public string Name { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string[] ParamNames { get; }
    public string Tooltip { get; set; }

    public ComparerInfoAttribute(EventProtocol protocol, string name, string displayName, string category, string[] paramNames = null)
    {
        Protocol = protocol;
        Name = name;
        DisplayName = displayName;
        Category = category;
        ParamNames = paramNames ?? Array.Empty<string>();
    }
}

/// <summary>
/// 比较器注册表 - 一站式定义与扫描喵~
/// </summary>
public static partial class ComparerRegistry
{
    public class ComparerMeta
    {
        public ComparerInfoAttribute Info;
        public MethodInfo Method;
    }

    private static readonly Dictionary<string, ComparerMeta> _comparers = new Dictionary<string, ComparerMeta>();
    private static bool _isInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        _comparers.Clear();
        var methods = typeof(ComparerRegistry).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<ComparerInfoAttribute>();
            if (attr != null)
            {
                _comparers[attr.Name] = new ComparerMeta { Info = attr, Method = method };
            }
        }
        _isInitialized = true;
        // Debug.Log($"[ComparerRegistry] 自动注册完成，加载了 {_comparers.Count} 个比较逻辑喵~");
    }

    private static void EnsureInitialized()
    {
        if (!_isInitialized || _comparers.Count == 0)
        {
            Initialize();
        }
    }

    public static ComparerResult Execute(string name, object payload, string[] args)
    {
        EnsureInitialized();
        if (!_comparers.TryGetValue(name, out var meta)) return ComparerResult.Fail;
        
        try
        {
            return (ComparerResult)meta.Method.Invoke(null, new object[] { payload, args });
        }
        catch (Exception e)
        {
            Debug.LogError($"[ComparerRegistry] 执行 {name} 出错: {e.Message} 喵~");
            return ComparerResult.Fail;
        }
    }

    public static IEnumerable<ComparerMeta> GetAllComparers()
    {
        EnsureInitialized();
        return _comparers.Values;
    }

    public static ComparerMeta GetMeta(string name)
    {
        EnsureInitialized();
        return _comparers.TryGetValue(name, out var meta) ? meta : null;
    }

    // =========================================================
    // 🎭 内置比较逻辑一览喵 (主人可以随时在这里添加喵~)
    // =========================================================

    [ComparerInfo(EventProtocol.String, "id_match", "🆔 ID 匹配", "通用", new[] { "预期 ID" }, Tooltip = "检查 Payload 是否为指定的 ID 字符串喵~")]
    public static ComparerResult CheckID(object payload, string[] args)
    {
        if (payload is string str) return str == args[0] ? ComparerResult.Pass : ComparerResult.Fail;
        if (payload != null && payload.ToString() == args[0]) return ComparerResult.Pass;
        return ComparerResult.TypeMismatch;
    }

    [ComparerInfo(EventProtocol.Numeric, "value_compare", "🔢 数值比较", "通用", new[] { "运算符", "比较值" }, Tooltip = "支持 > < >= <= == != 喵~")]
    public static ComparerResult CompareValue(object payload, string[] args)
    {
        if (payload == null) return ComparerResult.Fail;
        
        double val;
        if (payload is int i) val = i;
        else if (payload is float f) val = f;
        else if (payload is double d) val = d;
        else if (payload is long l) val = l;
        else if (!double.TryParse(payload.ToString(), out val)) return ComparerResult.TypeMismatch;

        string op = args[0];
        double target = double.Parse(args[1]);

        return op switch
        {
            ">" => val > target ? ComparerResult.Pass : ComparerResult.Fail,
            "<" => val < target ? ComparerResult.Pass : ComparerResult.Fail,
            ">=" => val >= target ? ComparerResult.Pass : ComparerResult.Fail,
            "<=" => val <= target ? ComparerResult.Pass : ComparerResult.Fail,
            "==" => Mathf.Approximately((float)val, (float)target) ? ComparerResult.Pass : ComparerResult.Fail,
            "!=" => !Mathf.Approximately((float)val, (float)target) ? ComparerResult.Pass : ComparerResult.Fail,
            _ => ComparerResult.Fail
        };
    }
}
