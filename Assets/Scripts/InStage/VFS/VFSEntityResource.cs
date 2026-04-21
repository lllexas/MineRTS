using NekoGraph;
using SpaceTUI;
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
            Debug.LogWarning(
                $"[VFSEntityResource] Execute 失败：目标格子被占用 blueprint={blueprint.BlueprintId} pos={spawnArgs.GridPosition}");
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
            handle.Id);

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
                requestName: context?.RequestName,
                title: "Broken .entity",
                summary: "EntityBlueprintSO is null",
                payload: null,
                isInteractive: false);
        }

        return VFSQueryResult.Create(
            presentationType: "entity",
            requestName: string.IsNullOrWhiteSpace(context?.RequestName) ? EntityClientViewKeys.Inspect : context.RequestName,
            title: string.IsNullOrWhiteSpace(blueprint.DisplayName) ? blueprint.BlueprintId : blueprint.DisplayName,
            summary: BuildSummary(blueprint),
            payload: new VFSEntityQueryPayload
            {
                Blueprint = blueprint,
                PackID = context?.PackID,
                VfsPath = context?.VfsPath,
                Node = context?.Node as VFSNodeData,
                SourceNodeId = context?.Node?.NodeID,
                FrontendContext = context?.FrontendContext
            },
            isInteractive: false);
    }

    private static string BuildSummary(EntityBlueprintSO blueprint)
    {
        return $"[{FormatFaction(blueprint.Faction)}] {FormatUnitType(blueprint.UnitType)}  HP:{blueprint.MaxHealth:0}  Size:{blueprint.LogicSize.x}x{blueprint.LogicSize.y}";
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
    public VFSNodeData Node;
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
