using NekoGraph;
using SpaceTUI;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// vfs.entity 资源驱动。
/// 当前阶段先明确协议边界：
/// - Query：仓库/面板查看入口
/// - Execute：战斗阶段召唤入口，必须显式提供生成参数
/// </summary>
[VFSResource(".entity", typeof(EntityBlueprintSO))]
public static class VFSEntityResource
{
    [VFSExecute]
    public static HandleResult Execute(
        VFSResolvedContent content,
        SignalContext context,
        BasePackData pack,
        GraphRunner runner,
        string packIDKey,
        System.Action continueAction)
    {
        var blueprint = content.GetUnityObject<EntityBlueprintSO>();
        if (blueprint == null)
        {
            Debug.LogError("[VFSEntityResource] Execute 失败：EntityBlueprintSO 为 null");
            return HandleResult.Error;
        }

        if (EntitySystem.Instance == null)
        {
            Debug.LogWarning("[VFSEntityResource] Execute 失败：EntitySystem 不存在，当前不在战斗阶段。");
            return HandleResult.Error;
        }

        if (context?.Args is not SpawnRequestArgs spawnArgs)
        {
            Debug.LogWarning("[VFSEntityResource] Execute 失败：缺少 SpawnRequestArgs，.entity 不会隐式猜测生成位置。");
            return HandleResult.Error;
        }

        if (GridSystem.Instance != null && !GridSystem.Instance.IsAreaClear(spawnArgs.GridPosition, blueprint.LogicSize))
        {
            Debug.LogWarningFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                "[VFSEntityResource] Execute 失败：目标格子被占用 blueprint={0} pos={1}",
                blueprint.BlueprintId,
                spawnArgs.GridPosition);
            return HandleResult.Error;
        }

        EntityHandle handle = EntitySystem.Instance.CreateEntityFromSO(
            blueprint,
            spawnArgs.GridPosition,
            spawnArgs.Faction);

        context.Args = new VFSEntityExecutionResult
        {
            Blueprint = blueprint,
            SpawnArgs = spawnArgs,
            Handle = handle
        };

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[vfs_entity] execute-spawn blueprint={0} pos={1} faction={2} handle={3}",
            blueprint.BlueprintId,
            spawnArgs.GridPosition,
            spawnArgs.Faction,
            handle.Index);

        return HandleResult.Push;
    }

    [VFSQuery]
    public static VFSQueryResult Query(VFSResolvedContent content, VFSQueryContext context)
    {
        var blueprint = content.GetUnityObject<EntityBlueprintSO>();
        if (blueprint == null)
        {
            return VFSQueryResult.Create(
                presentationType: "error",
                title: "Broken .entity",
                summary: "EntityBlueprintSO is null",
                payload: null,
                isInteractive: false);
        }

        return VFSQueryResult.Create(
            presentationType: "entity.inspect",
            title: string.IsNullOrWhiteSpace(blueprint.DisplayName) ? blueprint.BlueprintId : blueprint.DisplayName,
            summary: BuildSummary(blueprint),
            payload: new VFSEntityQueryPayload
            {
                Blueprint = blueprint,
                PackID = context?.PackID,
                VfsPath = context?.VfsPath,
                SourceNodeId = context?.Node?.NodeID,
                FrontendContext = context?.FrontendContext
            },
            isInteractive: false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterConsolePresentation()
    {
        ConsoleClientRuntime.RegisterPresenter("entity.inspect", (console, payload) =>
        {
            if (console == null || payload is not VFSEntityQueryPayload entityPayload || entityPayload.Blueprint == null)
                return;

            RenderInspect(console, entityPayload.Blueprint);
        });
    }

    private static string BuildSummary(EntityBlueprintSO blueprint)
    {
        return $"[{FormatFaction(blueprint.Faction)}] {FormatUnitType(blueprint.UnitType)}  HP:{blueprint.MaxHealth:0}  Size:{blueprint.LogicSize.x}x{blueprint.LogicSize.y}";
    }

    private static void RenderInspect(ConsoleManager console, EntityBlueprintSO blueprint)
    {
        int totalWidth = Mathf.Clamp(console.ConsoleWidth > 0 ? console.ConsoleWidth - 2 : 56, 36, 72);
        TSSStyle style = new TSSStyle
        {
            bleedX = 0,
            bleedY = 0,
            paddingX = 1,
            paddingY = 0,
            spacingX = 1,
            borderColor = new Color(0.32f, 0.62f, 0.72f),
            contentColor = new Color(0.90f, 0.96f, 1f),
            titleColor = new Color(0.22f, 0.86f, 1f),
            backgroundColor = null,
            alignment = TextAlignment.Left,
            expandArtSpaces = false
        };

        string title = $"ENTITY / {(string.IsNullOrWhiteSpace(blueprint.DisplayName) ? blueprint.BlueprintId : blueprint.DisplayName)}";
        string[] lines = TUITool.GenerateTextBoxWithTitle(BuildInspectLines(blueprint).ToArray(), title, totalWidth, style);
        foreach (string line in lines)
            console.Log(line, style.contentColor);
    }

    private static List<string> BuildInspectLines(EntityBlueprintSO blueprint)
    {
        var lines = new List<string>
        {
            $"ID        : {blueprint.BlueprintId}",
            $"Faction   : {FormatFaction(blueprint.Faction)}",
            $"UnitType  : {FormatUnitType(blueprint.UnitType)}",
            $"HP        : {blueprint.MaxHealth:0}",
            $"Size      : {blueprint.LogicSize.x} x {blueprint.LogicSize.y}",
            $"MoveTick  : {blueprint.MoveInterval:0.##}s",
            ""
        };

        if (blueprint.AttackRange > 0f || blueprint.AttackDamage > 0f)
        {
            lines.Add("Combat");
            lines.Add($"  Range    : {blueprint.AttackRange:0.##}");
            lines.Add($"  Damage   : {blueprint.AttackDamage:0.##}");
            lines.Add($"  Cooldown : {blueprint.AttackCooldown:0.##}s");
            if (blueprint.ProjectileSpriteId >= 0)
                lines.Add($"  Projectile: sprite#{blueprint.ProjectileSpriteId}  speed {blueprint.ProjectileSpeed:0.##}");
            lines.Add("");
        }

        if (blueprint.WorkType != WorkType.None || blueprint.RequiresPower)
        {
            lines.Add("Industry");
            lines.Add($"  WorkType : {blueprint.WorkType}");
            lines.Add($"  Speed    : {blueprint.WorkSpeed:0.##}");
            if (blueprint.DrillRange > 0)
                lines.Add($"  Drill    : {blueprint.DrillRange}");
            lines.Add($"  NeedPower: {(blueprint.RequiresPower ? "Yes" : "No")}");
            lines.Add("");
        }

        if (blueprint.IsPowerNode || blueprint.EnergyGeneration > 0f || blueprint.EnergyCapacity > 0f)
        {
            lines.Add("Power");
            lines.Add($"  Node     : {(blueprint.IsPowerNode ? "Yes" : "No")}");
            lines.Add($"  Supply   : {blueprint.SupplyRange:0.##}");
            lines.Add($"  Connect  : {blueprint.ConnectionRange:0.##}");
            lines.Add($"  Produce  : {blueprint.EnergyGeneration:0.##}");
            lines.Add($"  Capacity : {blueprint.EnergyCapacity:0.##}");
            lines.Add("");
        }

        if (blueprint.InputCount > 0 || blueprint.OutputCount > 0 || blueprint.DefaultCapacity > 0)
        {
            lines.Add("Inventory");
            lines.Add($"  Input    : {blueprint.InputCount}");
            lines.Add($"  Output   : {blueprint.OutputCount}");
            lines.Add($"  Capacity : {blueprint.DefaultCapacity}");
            lines.Add("");
        }

        lines.Add("Traits");
        lines.Add($"  Flyer    : {(blueprint.IsFlyer ? "Yes" : "No")}");
        lines.Add($"  Explode  : {(blueprint.ExplodeOnDeath ? "Yes" : "No")}");
        if (blueprint.FleeHealthPercent > 0f)
            lines.Add($"  FleeAt   : {blueprint.FleeHealthPercent:P0}");

        return lines;
    }

    private static string FormatFaction(int faction)
    {
        return faction switch
        {
            0 => "协议军",
            1 => "日之城",
            2 => "盖亚黎明",
            _ => $"Faction:{faction}"
        };
    }

    private static string FormatUnitType(int unitTypeMask)
    {
        if (unitTypeMask == UnitType.None)
            return "None";

        System.Collections.Generic.List<string> parts = new();
        if ((unitTypeMask & UnitType.Hero) != 0) parts.Add("Hero");
        if ((unitTypeMask & UnitType.Minion) != 0) parts.Add("Minion");
        if ((unitTypeMask & UnitType.Building) != 0) parts.Add("Building");
        if ((unitTypeMask & UnitType.ResourceItem) != 0) parts.Add("Resource");
        if ((unitTypeMask & UnitType.Projectile) != 0) parts.Add("Projectile");
        if ((unitTypeMask & UnitType.Flyer) != 0) parts.Add("Flyer");
        return string.Join("|", parts);
    }
}

public sealed class VFSEntityQueryPayload
{
    public EntityBlueprintSO Blueprint;
    public string PackID;
    public string VfsPath;
    public string SourceNodeId;
    public object FrontendContext;
}

/// <summary>
/// .entity Execute 成功后回传给后续节点的 payload。
/// 后续 .modifier 或 comparer 可直接基于这份结果继续处理。
/// </summary>
public sealed class VFSEntityExecutionResult
{
    public EntityBlueprintSO Blueprint;
    public SpawnRequestArgs SpawnArgs;
    public EntityHandle Handle;
}
