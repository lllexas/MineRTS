using System;
using System.Collections.Generic;
using NekoGraph;
using UnityEngine;

/// <summary>
/// 科技树/实验室领域门面。
/// 管理 Lab pack 中的 .labentry 条目，以及解锁状态。
/// 内部流转用 VFSNodeData 引用，不传字符串。
/// </summary>
[Serializable]
public sealed class LabFacade : PackFacadeBase
{
    public const string DefaultLabPackID = "lab_tree";
    public const string EntriesFolder = "/entries/";
    public const string LabChangedEvent = "Lab.Changed";

    protected override string GetDefaultPackID() => DefaultLabPackID;

    #region Pack & Root 管理

    public BasePackData GetLabPack(GraphAnalyser analyser, int subjectLevel)
    {
        return analyser?.GetPack(ResolvedPackID, subjectLevel);
    }

    public BasePackData EnsureLabPack(GraphAnalyser analyser, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        var pack = analyser?.EnsurePack(ResolvedPackID, subjectLevel);
        if (pack == null)
            return null;

        EnsureEntryRoots(analyser, subjectLevel);
        return pack;
    }

    public bool EnsureEntryRoots(GraphAnalyser analyser, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null)
            return false;

        var pack = analyser.EnsurePack(ResolvedPackID, subjectLevel);
        if (pack == null)
            return false;

        return analyser.CreateDirectory(ResolvedPackID, EntriesFolder, subjectLevel);
    }

    #endregion

    #region 路径解析

    public string ResolveEntryPath(string entryFileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(entryFileNameOrPath))
            return EntriesFolder;

        if (entryFileNameOrPath.StartsWith("/"))
            return VFSPathResolver.Normalize(entryFileNameOrPath);

        return VFSPathResolver.Combine(EntriesFolder, entryFileNameOrPath);
    }

    public string BuildEntryFilePath(string entryKey)
    {
        return ResolveEntryPath($"{entryKey}.labentry");
    }

    #endregion

    #region 后端 API — 节点访问（入口查一次，返回引用）

    public VFSNodeData ResolveEntryNode(GraphAnalyser analyser, string entryFileNameOrPath, int subjectLevel)
    {
        return analyser?.GetNode(
            ResolvedPackID,
            ResolveEntryPath(entryFileNameOrPath),
            subjectLevel) as VFSNodeData;
    }

    public VFSNodeData GetOrCreateEntryNode(GraphAnalyser analyser, string entryKey, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null || string.IsNullOrWhiteSpace(entryKey))
            return null;

        if (!EnsureEntryRoots(analyser, subjectLevel))
            return null;

        string path = BuildEntryFilePath(entryKey);
        if (!analyser.PathExists(ResolvedPackID, path, subjectLevel))
        {
            if (!analyser.WriteFile(ResolvedPackID, path, string.Empty, subjectLevel))
                return null;
        }

        return analyser.GetNode(ResolvedPackID, path, subjectLevel) as VFSNodeData;
    }

    public List<VFSNodeData> ListEntryNodes(GraphAnalyser analyser, int subjectLevel)
    {
        var children = analyser?.GetChildren(ResolvedPackID, EntriesFolder, subjectLevel);
        return FilterLabEntryNodes(children);
    }

    #endregion

    #region 后端 API — 节点 CRUD（内部流转用引用）

    public bool DeleteEntryNode(GraphAnalyser analyser, VFSNodeData node, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null || node == null || node.IsDirectory)
            return false;

        string path = ResolveEntryPath(node.Name + node.Extension);
        return analyser.Delete(ResolvedPackID, path, subjectLevel);
    }

    public bool SwapEntryNodes(GraphAnalyser analyser, VFSNodeData nodeA, VFSNodeData nodeB, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null || nodeA == null || nodeB == null)
            return false;

        string pathA = ResolveEntryPath(nodeA.Name + nodeA.Extension);
        string pathB = ResolveEntryPath(nodeB.Name + nodeB.Extension);
        return analyser.SwapNodes(ResolvedPackID, pathA, pathB, subjectLevel);
    }

    public bool WriteEntryNode(GraphAnalyser analyser, string entryKeyOrPath,
        string referencePath, string assetGuid, string unityObjectTypeName,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null)
            return false;

        if (!EnsureEntryRoots(analyser, subjectLevel))
            return false;

        string path = ResolveEntryPath(entryKeyOrPath);
        if (!analyser.PathExists(ResolvedPackID, path, subjectLevel))
        {
            if (!analyser.WriteFile(ResolvedPackID, path, string.Empty, subjectLevel))
                return false;
        }

        var node = analyser.GetNode(ResolvedPackID, path, subjectLevel) as VFSNodeData;
        if (node == null)
            return false;

        node.Extension = ".labentry";
        node.ContentKind = VFSContentKind.UnityObject;
        node.ContentSource = VFSContentSource.Reference;
        node.ReferencePath = referencePath;
        node.AssetGuid = assetGuid;
        node.UnityObjectTypeName = unityObjectTypeName;
        node.MimeType = "application/vnd.miner.labentry";

        return true;
    }

    /// <summary>
    /// 投递一个 .labentry 到 Lab pack。
    /// 基于 LabEntrySO 直接创建节点，写入回指元数据。
    /// </summary>
    public bool TryDeliverLabEntry(GraphAnalyser analyser,
        string sourcePackID,
        VFSNodeData sourceNode,
        string deliveryKey,
        LabEntrySO entry,
        out string deliveredPath,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        deliveredPath = null;
        if (analyser == null || entry == null)
            return false;

        if (!EnsureEntryRoots(analyser, subjectLevel))
            return false;

        string baseName = string.IsNullOrWhiteSpace(deliveryKey)
            ? (entry.EntryId ?? entry.name)
            : deliveryKey;
        deliveredPath = BuildEntryFilePath(baseName);

        if (analyser.PathExists(ResolvedPackID, deliveredPath, subjectLevel))
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                "[lab_facade] skip-already-exists path={0}", deliveredPath);
            return true;
        }

        if (!analyser.WriteFile(ResolvedPackID, deliveredPath, string.Empty, subjectLevel))
        {
            Debug.LogWarning($"[lab_facade] deliver-create-failed targetPack={ResolvedPackID} targetPath={deliveredPath}");
            return false;
        }

        var deliveredNode = analyser.GetNode(ResolvedPackID, deliveredPath, subjectLevel) as VFSNodeData;
        if (deliveredNode == null)
        {
            Debug.LogWarning($"[lab_facade] deliver-resolve-failed targetPack={ResolvedPackID} targetPath={deliveredPath}");
            return false;
        }

        // 直接设置节点属性（不依赖源节点复制）
        deliveredNode.Name = baseName;
        deliveredNode.Extension = ".labentry";
        deliveredNode.ContentKind = VFSContentKind.UnityObject;
        deliveredNode.ContentSource = VFSContentSource.Reference;
        string metaId = $"lab.{entry.EntryId ?? entry.name}";
        deliveredNode.ReferencePath = MetaLib.TryGetResourcePath(metaId, out var labEntryPath)
            ? labEntryPath
            : entry.name;
        deliveredNode.UnityObjectTypeName = typeof(LabEntrySO).AssemblyQualifiedName;
        deliveredNode.MimeType = "application/vnd.miner.labentry";

        deliveredNode.InlineText = new VFSLabEntryReplicaMeta
        {
            BackendPackID = sourcePackID,
            BackendNodeID = sourceNode?.NodeID,
            IsResolved = false
        }.Serialize();

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
            "[lab_facade] deliver-entry sourcePack={0} entryId={1} targetPack={2} targetPath={3}",
            sourcePackID, entry.EntryId, ResolvedPackID, deliveredPath);

        PostSystem.Instance?.Send(LabChangedEvent, deliveredPath);

        return true;
    }

    #endregion

    #region 后端 API — 解锁后投递实体到仓库

    /// <summary>
    /// 解锁条目：将 .labentry 引用的 EntityBlueprintSO 投递到仓库。
    /// 返回投递后的仓库路径。
    /// </summary>
    public bool TryUnlockEntry(GraphAnalyser analyser, VFSNodeData entryNode, out string warehousePath,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        warehousePath = null;
        if (analyser == null || entryNode == null)
            return false;

        var entry = GetLabEntry(entryNode);
        if (entry == null || entry.EntityBlueprint == null)
        {
            Debug.LogWarning($"[lab_facade] unlock-failed: no LabEntrySO or EntityBlueprint. node={entryNode.NodeID}");
            return false;
        }

        // 写入解锁标记到 lab entry 的 InlineText
        entryNode.InlineText = new VFSLabEntryUnlockMeta
        {
            UnlockedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        }.Serialize();

        var warehouseFacade = GraphHub.Instance?.GetFacade<EntityWarehouseFacade>();
        if (warehouseFacade == null)
        {
            Debug.LogWarning("[lab_facade] unlock-failed: EntityWarehouseFacade not registered");
            return false;
        }

        string entityKey = entry.EntityBlueprint.BlueprintId ?? entry.EntityBlueprint.name;
        string entityResourcePath = MetaLib.TryGetResourcePath(entityKey, out var metaPath)
            ? metaPath
            : entry.EntityBlueprint.name;
        bool written = warehouseFacade.WriteEntityNode(
            analyser,
            entityKey,
            entityResourcePath,
            null,
            typeof(EntityBlueprintSO).AssemblyQualifiedName,
            subjectLevel);

        if (written)
        {
            warehousePath = warehouseFacade.BuildEntityFilePath(entityKey);
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                "[lab_facade] unlocked entry={0} -> warehouse={1}",
                entry.EntryId, warehousePath);
            PostSystem.Instance?.Send(LabChangedEvent, entryNode.NodeID);
        }

        return written;
    }

    #endregion

    #region 前端 API — 返回 SO / Payload

    public LabEntrySO GetLabEntry(VFSNodeData node)
    {
        if (node == null)
            return null;

        var resolved = VFSContentResolver.Resolve(node);
        if (resolved != null && resolved.HasUnityObject)
        {
            return resolved.GetUnityObject<LabEntrySO>();
        }

        return null;
    }

    public bool IsUnlocked(VFSNodeData entryNode)
    {
        if (entryNode == null)
            return false;

        var meta = VFSLabEntryUnlockMeta.Deserialize(entryNode.InlineText);
        return meta != null && meta.UnlockedAt > 0;
    }

    #endregion

    #region 辅助方法

    private static List<VFSNodeData> FilterLabEntryNodes(List<BaseNodeData> nodes)
    {
        var result = new List<VFSNodeData>();
        if (nodes == null)
            return result;

        foreach (var node in nodes)
        {
            if (node is VFSNodeData vfsNode &&
                !vfsNode.IsDirectory &&
                string.Equals(vfsNode.Extension, ".labentry", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(vfsNode);
            }
        }

        return result;
    }

    private static void CopyFileNode(VFSNodeData sourceNode, VFSNodeData targetNode)
    {
        if (sourceNode == null || targetNode == null)
            return;

        targetNode.Name = sourceNode.Name;
        targetNode.Extension = sourceNode.Extension;
        targetNode.ContentKind = sourceNode.ContentKind;
        targetNode.ContentSource = sourceNode.ContentSource;
        targetNode.InlineText = sourceNode.InlineText;
        targetNode.ReferencePath = sourceNode.ReferencePath;
        targetNode.AssetGuid = sourceNode.AssetGuid;
        targetNode.AssetPath = sourceNode.AssetPath;
        targetNode.UnityObjectTypeName = sourceNode.UnityObjectTypeName;
        targetNode.IsEnabled = sourceNode.IsEnabled;
        targetNode.Description = sourceNode.Description;
        targetNode.MimeType = sourceNode.MimeType;
    }

    #endregion
}

/// <summary>
/// Lab entry 解锁元数据。
/// 写入条目的 InlineText 中。
/// </summary>
[Serializable]
public sealed class VFSLabEntryUnlockMeta
{
    public long UnlockedAt;

    public string Serialize()
    {
        return JsonUtility.ToJson(this);
    }

    public static VFSLabEntryUnlockMeta Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonUtility.FromJson<VFSLabEntryUnlockMeta>(json);
        }
        catch
        {
            return null;
        }
    }
}
