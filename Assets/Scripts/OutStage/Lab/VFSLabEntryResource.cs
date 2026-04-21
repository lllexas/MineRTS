using System;
using NekoGraph;
using UnityEngine;

/// <summary>
/// vfs.labentry 资源驱动。
/// Execute：从后台故事网投递 .labentry 复制体到 LabFacade。
/// Query：前台展示入口。
/// </summary>
[VFSResource(".labentry", typeof(LabEntrySO))]
public static class VFSLabEntryResource
{
    [VFSExecute]
    public static HandleResult Execute(
        VFSResolvedContent content,
        SignalContext context,
        BasePackData pack,
        GraphRunner runner,
        string packIDKey,
        Action continueAction)
    {
        var entry = content.GetUnityObject<LabEntrySO>();
        if (entry == null)
        {
            Debug.LogError("[VFSLabEntryResource] Execute 失败：LabEntrySO 为 null");
            return HandleResult.Error;
        }

        var labFacade = GraphHub.Instance?.GetFacade<LabFacade>();
        if (labFacade == null)
        {
            Debug.LogError("[VFSLabEntryResource] Execute 失败：LabFacade 未注册");
            return HandleResult.Error;
        }

        var analyser = GraphHub.Instance?.DefaultAnalyser;
        if (analyser == null)
        {
            Debug.LogError("[VFSLabEntryResource] Execute 失败：GraphAnalyser 不存在");
            return HandleResult.Error;
        }

        string deliveryKey = entry.EntryId ?? entry.name;
        bool delivered = labFacade.TryDeliverLabEntry(
            analyser,
            packIDKey,
            content?.Node,
            deliveryKey,
            entry,
            out string deliveredPath,
            subjectLevel: PackAccessSubjects.SystemMin);

        if (!delivered)
        {
            Debug.LogWarning($"[VFSLabEntryResource] Execute 投递失败 entry={deliveryKey}");
            return HandleResult.Error;
        }

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
            "[vfs_labentry] execute-deliver entry={0} fromPack={1} toPath={2}",
            deliveryKey, packIDKey, deliveredPath);

        return HandleResult.Push;
    }

    [VFSQuery]
    public static VFSQueryResult Query(VFSResolvedContent content, VFSQueryContext context)
    {
        var entry = content.GetUnityObject<LabEntrySO>();
        if (entry == null)
        {
            return VFSQueryResult.Create(
                presentationType: "error",
                title: "Broken .labentry",
                summary: "LabEntrySO is null",
                payload: null,
                isInteractive: false);
        }

        var blueprint = entry.EntityBlueprint;
        string title = string.IsNullOrWhiteSpace(entry.EntryId)
            ? (blueprint?.DisplayName ?? blueprint?.BlueprintId ?? "Unknown Entry")
            : entry.EntryId;

        string summary = string.IsNullOrWhiteSpace(entry.Description)
            ? BuildDefaultSummary(blueprint)
            : entry.Description;

        return VFSQueryResult.Create(
            presentationType: "lab",
            requestName: string.IsNullOrWhiteSpace(context?.RequestName) ? LabClientViewKeys.Inspect : context.RequestName,
            title: title,
            summary: summary,
            payload: new VFSLabEntryQueryPayload
            {
                Entry = entry,
                PackID = context?.PackID,
                VfsPath = context?.VfsPath,
                Node = context?.Node as VFSNodeData,
                SourceNodeId = context?.Node?.NodeID,
                FrontendContext = context?.FrontendContext,
                UnlockAction = BuildUnlockAction(context?.Node as VFSNodeData)
            },
            isInteractive: false);
    }

    private static Func<bool> BuildUnlockAction(VFSNodeData entryNode)
    {
        if (entryNode == null)
            return null;

        return () =>
        {
            var facade = GraphHub.Instance?.GetFacade<LabFacade>();
            var analyser = GraphHub.Instance?.DefaultAnalyser;
            if (facade == null || analyser == null)
                return false;

            if (facade.IsUnlocked(entryNode))
                return true;

            return facade.TryUnlockEntry(analyser, entryNode, out _, PackAccessSubjects.SystemMin);
        };
    }

    private static string BuildDefaultSummary(EntityBlueprintSO blueprint)
    {
        if (blueprint == null)
            return "No entity blueprint assigned.";

        return $"Unlocks: {blueprint.DisplayName ?? blueprint.BlueprintId} [{FormatFaction(blueprint.Faction)}]";
    }

    private static string FormatFaction(int faction)
    {
        return faction switch
        {
            0 => "Protocol",
            1 => "SunCity",
            2 => "Gaia",
            _ => $"F:{faction}"
        };
    }
}

public sealed class VFSLabEntryQueryPayload
{
    public LabEntrySO Entry;
    public string PackID;
    public string VfsPath;
    public VFSNodeData Node;
    public string SourceNodeId;
    public object FrontendContext;
    public Func<bool> UnlockAction;
}

/// <summary>
/// .labentry 复制体的运行时回指元数据。
/// 写入复制体节点的 InlineText 中。
/// </summary>
[Serializable]
public sealed class VFSLabEntryReplicaMeta
{
    public string BackendPackID;
    public string BackendNodeID;
    public string SignalId;
    public bool IsResolved;

    public string Serialize()
    {
        return JsonUtility.ToJson(this);
    }

    public static VFSLabEntryReplicaMeta Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonUtility.FromJson<VFSLabEntryReplicaMeta>(json);
        }
        catch
        {
            return null;
        }
    }
}
