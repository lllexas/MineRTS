using UnityEngine;

/// <summary>
/// MineRTS 实体比较器砖块喵~
/// NekoGraph 全域扫描自动发现，无需手动注册。
/// 依赖 EntitySystem，属于游戏专属扩展。
/// </summary>
public static class EntityComparers
{
    private static ComparerResult FastCompare(double val, string op, double target)
    {
        return op switch
        {
            ">"  => val > target  ? ComparerResult.Pass : ComparerResult.Fail,
            "<"  => val < target  ? ComparerResult.Pass : ComparerResult.Fail,
            ">=" => val >= target ? ComparerResult.Pass : ComparerResult.Fail,
            "<=" => val <= target ? ComparerResult.Pass : ComparerResult.Fail,
            "==" => Mathf.Approximately((float)val, (float)target) ? ComparerResult.Pass : ComparerResult.Fail,
            "!=" => !Mathf.Approximately((float)val, (float)target) ? ComparerResult.Pass : ComparerResult.Fail,
            _    => ComparerResult.Fail
        };
    }

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
}
