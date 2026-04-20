using System;
using System.Collections.Generic;
using NekoGraph;
using UnityEngine;

/// <summary>
/// 实体仓库领域门面。
/// 对业务层暴露"仓库中的实体"语义入口，避免业务代码直接散落完整路径。
/// 后端 API 尽量传递 VFSNodeData 引用，内部流转不走 BFS 查表。
/// </summary>
[Serializable]
public sealed class EntityWarehouseFacade : PackFacadeBase
{
    public const string DefaultWarehousePackID = "player_warehouse";
    public const string EntitiesFolder = "/entities/";

    protected override string GetDefaultPackID() => DefaultWarehousePackID;

    #region Pack & Root 管理

    public BasePackData GetWarehousePack(GraphAnalyser analyser, int subjectLevel)
    {
        return analyser?.GetPack(ResolvedPackID, subjectLevel);
    }

    public BasePackData EnsureWarehousePack(GraphAnalyser analyser, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        var pack = analyser?.EnsurePack(ResolvedPackID, subjectLevel);
        if (pack == null)
            return null;

        EnsureEntityRoots(analyser, subjectLevel);
        return pack;
    }

    public bool EnsureEntityRoots(GraphAnalyser analyser, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null)
            return false;

        var pack = analyser.EnsurePack(ResolvedPackID, subjectLevel);
        if (pack == null)
            return false;

        return analyser.CreateDirectory(ResolvedPackID, EntitiesFolder, subjectLevel);
    }

    #endregion

    #region 路径解析

    public string ResolveEntityPath(string entityFileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(entityFileNameOrPath))
            return EntitiesFolder;

        if (entityFileNameOrPath.StartsWith("/"))
            return VFSPathResolver.Normalize(entityFileNameOrPath);

        return VFSPathResolver.Combine(EntitiesFolder, entityFileNameOrPath);
    }

    public string BuildEntityFilePath(string entityKey)
    {
        return ResolveEntityPath($"{entityKey}.entity");
    }

    #endregion

    #region 后端 API — 节点访问（入口查一次，返回引用）

    /// <summary>
    /// 从字符串路径解析实体节点。入口查表一次，后续内部流转用引用。
    /// </summary>
    public VFSNodeData ResolveEntityNode(GraphAnalyser analyser, string entityFileNameOrPath, int subjectLevel)
    {
        return analyser?.GetNode(
            ResolvedPackID,
            ResolveEntityPath(entityFileNameOrPath),
            subjectLevel) as VFSNodeData;
    }

    /// <summary>
    /// 获取或创建实体节点。先确保根目录存在，再获取或创建节点。
    /// </summary>
    public VFSNodeData GetOrCreateEntityNode(GraphAnalyser analyser, string entityKey, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null || string.IsNullOrWhiteSpace(entityKey))
            return null;

        if (!EnsureEntityRoots(analyser, subjectLevel))
            return null;

        string path = BuildEntityFilePath(entityKey);
        if (!analyser.PathExists(ResolvedPackID, path, subjectLevel))
        {
            if (!analyser.WriteFile(ResolvedPackID, path, string.Empty, subjectLevel))
                return null;
        }

        return analyser.GetNode(ResolvedPackID, path, subjectLevel) as VFSNodeData;
    }

    /// <summary>
    /// 列出仓库中所有实体节点（.entity 文件）。
    /// 列表查询不可避免，但内部流转不再重复查表。
    /// </summary>
    public List<VFSNodeData> ListEntityNodes(GraphAnalyser analyser, int subjectLevel)
    {
        var children = analyser?.GetChildren(ResolvedPackID, EntitiesFolder, subjectLevel);
        return FilterEntityNodes(children);
    }

    #endregion

    #region 后端 API — 节点 CRUD（内部流转用引用）

    /// <summary>
    /// 删除实体节点。接收节点引用，不走 BFS 查表。
    /// </summary>
    public bool DeleteEntityNode(GraphAnalyser analyser, VFSNodeData node, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null || node == null || node.IsDirectory)
            return false;

        string path = ResolveEntityPath(node.Name + node.Extension);
        return analyser.Delete(ResolvedPackID, path, subjectLevel);
    }

    /// <summary>
    /// 交换两个实体节点的位置。接收节点引用。
    /// </summary>
    public bool SwapEntityNodes(GraphAnalyser analyser, VFSNodeData nodeA, VFSNodeData nodeB, int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null || nodeA == null || nodeB == null)
            return false;

        string pathA = ResolveEntityPath(nodeA.Name + nodeA.Extension);
        string pathB = ResolveEntityPath(nodeB.Name + nodeB.Extension);
        return analyser.SwapNodes(ResolvedPackID, pathA, pathB, subjectLevel);
    }

    /// <summary>
    /// 写入或更新实体节点的引用信息。
    /// 传入引用信息（非 SO），facade 内部设置节点属性。
    /// </summary>
    public bool WriteEntityNode(GraphAnalyser analyser, string entityKeyOrPath,
        string referencePath, string assetGuid, string unityObjectTypeName,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        if (analyser == null)
            return false;

        if (!EnsureEntityRoots(analyser, subjectLevel))
            return false;

        string path = ResolveEntityPath(entityKeyOrPath);
        if (!analyser.PathExists(ResolvedPackID, path, subjectLevel))
        {
            if (!analyser.WriteFile(ResolvedPackID, path, string.Empty, subjectLevel))
                return false;
        }

        var node = analyser.GetNode(ResolvedPackID, path, subjectLevel) as VFSNodeData;
        if (node == null)
            return false;

        node.Extension = ".entity";
        node.ContentKind = VFSContentKind.UnityObject;
        node.ContentSource = VFSContentSource.Reference;
        node.ReferencePath = referencePath;
        node.AssetGuid = assetGuid;
        node.UnityObjectTypeName = unityObjectTypeName;
        node.MimeType = "application/vnd.miner.entity";

        return true;
    }

    #endregion

    #region 后端 API — 投递复制

    /// <summary>
    /// 从外部 pack 的 .entity 节点投递一份副本到仓库。
    /// 复制节点属性并写入来源追踪元数据。
    /// </summary>
    public bool TryDeliverEntityCopy(GraphAnalyser analyser,
        string sourcePackID,
        VFSNodeData sourceNode,
        string deliveryKey,
        out string deliveredPath,
        int subjectLevel = PackAccessSubjects.SystemMin)
    {
        deliveredPath = null;
        if (analyser == null || sourceNode == null || sourceNode.IsDirectory)
            return false;

        if (!string.Equals(sourceNode.Extension, ".entity", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[entity_warehouse] reject-deliver sourcePack={sourcePackID} node={sourceNode.NodeID} extension={sourceNode.Extension}");
            return false;
        }

        if (!EnsureEntityRoots(analyser, subjectLevel))
            return false;

        deliveredPath = BuildEntityFilePath(deliveryKey);
        if (!analyser.WriteFile(ResolvedPackID, deliveredPath, string.Empty, subjectLevel))
        {
            Debug.LogWarning($"[entity_warehouse] deliver-create-failed targetPack={ResolvedPackID} targetPath={deliveredPath}");
            return false;
        }

        var deliveredNode = analyser.GetNode(ResolvedPackID, deliveredPath, subjectLevel) as VFSNodeData;
        if (deliveredNode == null)
        {
            Debug.LogWarning($"[entity_warehouse] deliver-resolve-failed targetPack={ResolvedPackID} targetPath={deliveredPath}");
            return false;
        }

        CopyFileNode(sourceNode, deliveredNode);
        deliveredNode.InlineText = new VFSEntityReplicaMeta
        {
            SourcePackID = sourcePackID,
            SourceNodeID = sourceNode.NodeID,
            DeliveredAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        }.Serialize();

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[entity_warehouse] deliver-entity sourcePack={0} sourceNode={1} targetPack={2} targetPath={3}",
            sourcePackID,
            sourceNode.NodeID,
            ResolvedPackID,
            deliveredPath);

        return true;
    }

    #endregion

    #region 前端 API — 返回 SO / Payload

    /// <summary>
    /// 从实体节点解析出 EntityBlueprintSO。
    /// 前端 UI / session 调用此方法获取可展示的业务对象。
    /// </summary>
    public EntityBlueprintSO GetEntityBlueprint(VFSNodeData node)
    {
        if (node == null)
            return null;

        var resolved = VFSContentResolver.Resolve(node);
        if (resolved != null && resolved.HasUnityObject)
        {
            return resolved.GetUnityObject<EntityBlueprintSO>();
        }

        return null;
    }

    #endregion

    #region 辅助方法

    private static List<VFSNodeData> FilterEntityNodes(List<BaseNodeData> nodes)
    {
        var result = new List<VFSNodeData>();
        if (nodes == null)
            return result;

        foreach (var node in nodes)
        {
            if (node is VFSNodeData vfsNode &&
                !vfsNode.IsDirectory &&
                string.Equals(vfsNode.Extension, ".entity", StringComparison.OrdinalIgnoreCase))
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
/// 实体副本投递后的追踪元数据。
/// 写入副本节点的 InlineText 中。
/// </summary>
[Serializable]
public sealed class VFSEntityReplicaMeta
{
    public string SourcePackID;
    public string SourceNodeID;
    public long DeliveredAt;

    public string Serialize()
    {
        return JsonUtility.ToJson(this);
    }

    public static VFSEntityReplicaMeta Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonUtility.FromJson<VFSEntityReplicaMeta>(json);
        }
        catch
        {
            return null;
        }
    }
}
