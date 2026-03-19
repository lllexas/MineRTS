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
/// 职责：提供高性能的属性提取与逻辑判断，直接对接 EntitySystem 核心数组喵！
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
    // 🎭 辅助方法喵~
    // =========================================================

    private static ComparerResult FastCompare(double val, string op, double target)
    {
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

    // =========================================================
    // 🛡️ 实体系列 (Protocol: Entity - Payload: EntityHandle)
    // 直接随机访问数组，性能巅峰喵！
    // =========================================================

    [ComparerInfo(EventProtocol.Entity, "ent_hp_perc", "💔 实体: 血量百分比", "实体", new[] { "运算符", "百分比(0-1)" }, Tooltip = "检查实体的血量比例（0-1）喵~")]
    public static ComparerResult EntityHPPercent(object payload, string[] args)
    {
        if (payload is not EntityHandle handle) return ComparerResult.TypeMismatch;
        var system = EntitySystem.Instance;
        int idx = system.GetIndex(handle);
        if (idx == -1) return ComparerResult.Fail;

        ref var health = ref system.wholeComponent.healthComponent[idx];
        float percent = health.MaxHealth > 0 ? health.Health / health.MaxHealth : 0;
        
        return FastCompare(percent, args[0], double.Parse(args[1]));
    }

    [ComparerInfo(EventProtocol.Entity, "ent_team", "🚩 实体: 阵营检查", "实体", new[] { "目标阵营ID" }, Tooltip = "检查实体是否属于指定阵营喵~")]
    public static ComparerResult EntityTeamMatch(object payload, string[] args)
    {
        if (payload is not EntityHandle handle) return ComparerResult.TypeMismatch;
        var system = EntitySystem.Instance;
        int idx = system.GetIndex(handle);
        if (idx == -1) return ComparerResult.Fail;

        int targetTeam = int.Parse(args[0]);
        return system.wholeComponent.coreComponent[idx].Team == targetTeam ? ComparerResult.Pass : ComparerResult.Fail;
    }

    [ComparerInfo(EventProtocol.Entity, "ent_blueprint", "📄 实体: 蓝图匹配", "实体", new[] { "蓝图名称" }, Tooltip = "检查实体是否由指定的蓝图创建喵~")]
    public static ComparerResult EntityBlueprintMatch(object payload, string[] args)
    {
        if (payload is not EntityHandle handle) return ComparerResult.TypeMismatch;
        var system = EntitySystem.Instance;
        int idx = system.GetIndex(handle);
        if (idx == -1) return ComparerResult.Fail;

        return system.wholeComponent.coreComponent[idx].BlueprintName == args[0] ? ComparerResult.Pass : ComparerResult.Fail;
    }

    [ComparerInfo(EventProtocol.Entity, "ent_is_alive", "💓 实体: 是否存活", "实体", null, Tooltip = "检查实体是否处于存活状态喵~")]
    public static ComparerResult EntityIsAlive(object payload, string[] args)
    {
        if (payload is not EntityHandle handle) return ComparerResult.TypeMismatch;
        var system = EntitySystem.Instance;
        int idx = system.GetIndex(handle);
        if (idx == -1) return ComparerResult.Fail;

        return system.wholeComponent.healthComponent[idx].IsAlive ? ComparerResult.Pass : ComparerResult.Fail;
    }

    // =========================================================
    // 🔢 数值系列 (Protocol: Numeric - Payload: double/float/int)
    // =========================================================

    [ComparerInfo(EventProtocol.Numeric, "val_compare", "🔢 数值: 常规比较", "数值", new[] { "运算符", "比较值" }, Tooltip = "支持 > < >= <= == != 喵~")]
    public static ComparerResult NumericCompare(object payload, string[] args)
    {
        if (payload == null) return ComparerResult.Fail;
        
        double val;
        if (payload is int i) val = i;
        else if (payload is float f) val = f;
        else if (payload is double d) val = d;
        else if (!double.TryParse(payload.ToString(), out val)) return ComparerResult.TypeMismatch;

        return FastCompare(val, args[0], double.Parse(args[1]));
    }

    // =========================================================
    // 🔤 字符串系列 (Protocol: String - Payload: string)
    // =========================================================

    [ComparerInfo(EventProtocol.String, "str_match", "🆔 字符: 完全匹配", "字符", new[] { "预期文本" }, Tooltip = "检查字符串是否完全相等喵~")]
    public static ComparerResult StringMatch(object payload, string[] args)
    {
        if (payload is not string str) return ComparerResult.TypeMismatch;
        return str == args[0] ? ComparerResult.Pass : ComparerResult.Fail;
    }

    [ComparerInfo(EventProtocol.String, "str_contains", "🔍 字符: 包含关键词", "字符", new[] { "关键词" }, Tooltip = "检查字符串是否包含指定关键词喵~")]
    public static ComparerResult StringContains(object payload, string[] args)
    {
        if (payload is not string str) return ComparerResult.TypeMismatch;
        return str.Contains(args[0]) ? ComparerResult.Pass : ComparerResult.Fail;
    }

    [ComparerInfo(EventProtocol.String, "str_not_empty", "❗ 字符: 非空检查", "字符", null, Tooltip = "检查字符串是否不为空且不全为空格喵~")]
    public static ComparerResult StringNotEmpty(object payload, string[] args)
    {
        if (payload is not string str) return ComparerResult.TypeMismatch;
        return !string.IsNullOrWhiteSpace(str) ? ComparerResult.Pass : ComparerResult.Fail;
    }
}
