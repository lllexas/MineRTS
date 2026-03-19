using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSInstance - VFS 运行时实例（只读文件树快照）喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 职责：
/// - 存储 VFS 节点的运行时快照
/// - 提供路径索引用于 O(1) 查询
/// - 管理根节点列表
///
/// 设计哲学：
/// - 纯数据类，不包含业务逻辑
/// - 与 GraphAnalyser 配合使用（GraphAnalyser 负责注入灵魂）
/// - 使用侧边索引（NodeToPath、Hierarchy）分离图结构与路径语义
///
/// 生命周期：
/// 1. MetaLib.GetPack<VFSPackData>(packID) → VFSPackData（沉睡数据）
/// 2. new VFSInstance() → VFSInstance（空壳实例）
/// 3. GraphAnalyser.LoadVFS() → InternalRebuildTree()（注入灵魂）
/// 4. PathIndex 生成完成，可以快如闪电地查询喵~！
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[Serializable]
public class VFSInstance
{
    /// <summary>
    /// 实例 ID (已废弃，统一使用 PackID 喵~)
    /// </summary>
    // public string InstanceID; 

    /// <summary>
    /// 图类型（固定为 "VFS"）
    /// </summary>
    public string GraphType;

    /// <summary>
    /// 源 JSON 文件名（即 PackID）
    /// </summary>
    public string SourceJsonFileName;

    /// <summary>
    /// 统一使用 PackID 访问喵~
    /// </summary>
    public string PackID => SourceJsonFileName;

    /// <summary>
    /// 节点字典：NodeID → BaseNodeData（通用类型）
    /// </summary>
    public Dictionary<string, BaseNodeData> NodeMap = new Dictionary<string, BaseNodeData>();

    /// <summary>
    /// 路径索引：FullPath → NodeID（不区分大小写）
    /// </summary>
    public Dictionary<string, string> PathIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 根节点 ID（唯一）
    /// </summary>
    public string RootNodeId;

    /// <summary>
    /// 是否已加载（由 GraphAnalyser 注入灵魂后设置为 true）
    /// </summary>
    public bool IsLoaded;

    // =========================================================
    //  【侧边索引层】- 运行时数据，不序列化喵~
    // =========================================================

    /// <summary>
    /// 节点到路径的映射：NodeID → FullPath
    /// 【NonSerialized】不保存到 JSON，每次运行时重新计算喵~
    /// </summary>
    [NonSerialized]
    public Dictionary<string, string> NodeToPath = new Dictionary<string, string>();

    /// <summary>
    /// 父子层级关系：NodeID → List&lt;ChildNodeID&gt;
    /// 【NonSerialized】不保存到 JSON，每次运行时重新计算喵~
    /// </summary>
    [NonSerialized]
    public Dictionary<string, List<string>> Hierarchy = new Dictionary<string, List<string>>();

    /// <summary>
    /// 创建 VFS 实例喵~
    /// </summary>
    /// <param name="packID">Pack ID (源文件名)</param>
    /// <param name="graphType">图类型（默认 "VFS"）</param>
    public VFSInstance(string packID, string graphType = "VFS")
    {
        SourceJsonFileName = packID;
        GraphType = graphType;
        NodeMap = new Dictionary<string, BaseNodeData>();
        PathIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        RootNodeId = null;
        IsLoaded = false;
    }

    // =========================================================
    //  运行时动态管理（数据觉醒与生长）喵~
    // =========================================================

    /// <summary>
    /// 运行时添加节点喵~
    /// 【动态生长核心】
    /// </summary>
    /// <param name="node">新节点数据</param>
    /// <param name="parentID">父节点 ID</param>
    /// <returns>是否添加成功</returns>
    public bool AddNodeRuntime(BaseNodeData node, string parentID = null)
    {
        if (node == null || string.IsNullOrEmpty(node.NodeID)) return false;

        // 1. 查重
        if (NodeMap.ContainsKey(node.NodeID))
        {
            Debug.LogWarning($"[VFSInstance] 节点已存在：{node.NodeID}");
            return false;
        }

        // 2. 建立连线关系 + 更新 Hierarchy
        if (!string.IsNullOrEmpty(parentID) && NodeMap.TryGetValue(parentID, out var parent))
        {
            // 在父节点的 OutputConnections 里添加一条连线指向新节点喵~
            // 如果已经有连线了就不加了（虽然理论上不会重复添加同一个 NodeID）
            if (!parent.OutputConnections.Exists(c => c.TargetNodeID == node.NodeID))
            {
                parent.OutputConnections.Add(new ConnectionData(0, node.NodeID, 0));
            }

            // 如果新节点是 VFSNodeData，也要设置它的 ParentNodeID（反向索引）
            if (node is VFSNodeData vfs)
            {
                vfs.ParentNodeID = parentID;
            }

            // 更新 Hierarchy（确保 GetChildren/ls 能找到新节点喵~）
            if (!Hierarchy.ContainsKey(parentID))
                Hierarchy[parentID] = new List<string>();
            if (!Hierarchy[parentID].Contains(node.NodeID))
                Hierarchy[parentID].Add(node.NodeID);
        }
        else if (node is RootNodeData)
        {
            RootNodeId = node.NodeID;
            if (!Hierarchy.ContainsKey(node.NodeID))
                Hierarchy[node.NodeID] = new List<string>();
        }

        // 3. 加入地图
        NodeMap[node.NodeID] = node;

        // 4. ✅ 局部更新：只更新新节点的路径索引（不需要全量重建树）
        if (node is VFSNodeData vfsNode)
        {
            UpdateNodePathIndex(vfsNode, parentID);
        }

        return true;
    }

    /// <summary>
    /// 更新单个节点的路径索引喵~（局部更新）
    /// 直接从 NodeToPath 查父节点路径，与 AnalyzeRecursive 生成的路径格式完全一致喵~
    /// </summary>
    private void UpdateNodePathIndex(VFSNodeData node, string parentID)
    {
        // 1. 从 NodeToPath 查父节点的已知路径（由 AnalyzeRecursive 正确建立）
        string parentPath = "";
        if (!string.IsNullOrEmpty(parentID))
        {
            NodeToPath.TryGetValue(parentID, out parentPath);
        }

        // 2. 拼接当前节点名（目录无扩展名，文件带扩展名）
        string nodeName = node.IsDirectory ? node.Name : (node.Name + node.Extension);

        string nodePath;
        if (string.IsNullOrEmpty(parentPath) || parentPath == "/")
            nodePath = "/" + nodeName;
        else
            nodePath = parentPath.TrimEnd('/') + "/" + nodeName;

        // 3. 目录路径加尾斜杠，与 AnalyzeRecursive 行为一致
        if (node.IsDirectory)
            nodePath += "/";

        // 4. 同时维护 NodeToPath，让这个节点的子节点也能正确查到父路径
        NodeToPath[node.NodeID] = nodePath;
        PathIndex[nodePath] = node.NodeID;
    }

    /// <summary>
    /// 运行时移除节点喵~
    /// 【数据凋零】
    /// </summary>
    /// <param name="nodeID">节点 ID</param>
    /// <returns>是否移除成功</returns>
    public bool RemoveNodeRuntime(string nodeID)
    {
        if (string.IsNullOrEmpty(nodeID) || !NodeMap.TryGetValue(nodeID, out var node))
            return false;

        // 1. 找到它的父节点，切断连线
        foreach (var kvp in NodeMap)
        {
            var potentialParent = kvp.Value;
            potentialParent.OutputConnections.RemoveAll(c => c.TargetNodeID == nodeID);
        }

        // 2. 如果是根节点，清空
        if (RootNodeId == nodeID) RootNodeId = null;

        // 3. 从地图移除
        NodeMap.Remove(nodeID);

        return true;
    }

    /// <summary>
    /// 将当前运行时实例转换为可存档的数据包喵~
    /// </summary>
    public VFSPackData ToPackData()
    {
        var pack = new VFSPackData
        {
            PackID = PackID,
            DisplayName = PackID + " Runtime Snapshot",
            Description = "由 VFSInstance 动态生成的快照数据包喵~",
            Nodes = new Dictionary<string, BaseNodeData>(NodeMap),
            RootNodeId = RootNodeId
        };
        return pack;
    }

    // =========================================================
    //  节点管理喵~
    // =========================================================

    /// <summary>
    /// 添加节点喵~
    /// </summary>
    /// <param name="node">节点数据</param>
    public void AddNode(BaseNodeData node)
    {
        if (node == null) return;

        if (NodeMap.ContainsKey(node.NodeID))
            NodeMap[node.NodeID] = node;
        else
            NodeMap.Add(node.NodeID, node);

        // 如果是根节点，添加到根节点列表
        if (node is RootNodeData)
            RootNodeId = node.NodeID;
    }

    /// <summary>
    /// 添加 VFS 节点喵~
    /// </summary>
    /// <param name="vfsNode">VFS 节点数据</param>
    public void AddNode(VFSNodeData vfsNode)
    {
        if (vfsNode == null) return;

        if (NodeMap.ContainsKey(vfsNode.NodeID))
            NodeMap[vfsNode.NodeID] = vfsNode;
        else
            NodeMap.Add(vfsNode.NodeID, vfsNode);
    }

    // =========================================================
    //  节点查询喵~
    // =========================================================

    /// <summary>
    /// 根据路径获取节点喵~
    /// </summary>
    /// <param name="path">路径（如 "/social/friends/"）</param>
    /// <returns>BaseNodeData，如果不存在则返回 null</returns>
    public BaseNodeData GetNodeByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        path = VFSPathResolver.Normalize(path);

        // 1. 直接查询
        if (PathIndex.TryGetValue(path, out var nodeID))
            return NodeMap.GetValueOrDefault(nodeID);

        // 2. 容错：如果查询失败，尝试反向斜杠策略
        if (path.EndsWith("/"))
        {
            // 如果是 "/path/" 没查到，试试 "/path" (可能是文件喵)
            string altPath = path.Substring(0, path.Length - 1);
            if (PathIndex.TryGetValue(altPath, out nodeID))
                return NodeMap.GetValueOrDefault(nodeID);
        }
        else
        {
            // 如果是 "/path" 没查到，试试 "/path/" (可能是目录喵)
            string altPath = path + "/";
            if (PathIndex.TryGetValue(altPath, out nodeID))
                return NodeMap.GetValueOrDefault(nodeID);
        }

        return null;
    }

    /// <summary>
    /// 根据 ID 获取节点喵~
    /// </summary>
    /// <typeparam name="T">节点类型</typeparam>
    /// <param name="nodeID">节点 ID</param>
    /// <returns>节点数据，如果不存在则返回 null</returns>
    public T GetNode<T>(string nodeID) where T : BaseNodeData
    {
        if (string.IsNullOrEmpty(nodeID)) return null;
        if (NodeMap.TryGetValue(nodeID, out var node))
            return node as T;
        return null;
    }

    /// <summary>
    /// 获取子节点列表（根据 Hierarchy 索引）喵~
    /// </summary>
    /// <param name="parentNodeID">父节点 ID</param>
    /// <returns>子节点列表</returns>
    public List<BaseNodeData> GetChildren(string parentNodeID)
    {
        var children = new List<BaseNodeData>();
        if (Hierarchy.TryGetValue(parentNodeID, out var childIds))
        {
            foreach (var childId in childIds)
            {
                if (NodeMap.TryGetValue(childId, out var child))
                {
                    // 检查是否启用（如果是 VFSNodeData）
                    if (child is VFSNodeData vfs && !vfs.IsEnabled) continue;
                    children.Add(child);
                }
            }
        }
        return children;
    }

    /// <summary>
    /// 根据路径获取子节点列表喵~
    /// </summary>
    /// <param name="path">父路径</param>
    /// <returns>子节点列表</returns>
    public List<BaseNodeData> GetChildrenByPath(string path)
    {
        var parentNode = GetNodeByPath(path);
        if (parentNode == null) return new List<BaseNodeData>();
        return GetChildren(parentNode.NodeID);
    }

    /// <summary>
    /// 检查路径是否存在喵~
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>是否存在</returns>
    public bool PathExists(string path)
    {
        return GetNodeByPath(path) != null;
    }

    // =========================================================
    //  实例管理喵~
    // =========================================================

    /// <summary>
    /// 清空实例数据喵~
    /// </summary>
    public void Clear()
    {
        NodeMap.Clear();
        PathIndex.Clear();
        NodeToPath.Clear();
        Hierarchy.Clear();
        RootNodeId = null;
        IsLoaded = false;
    }

    // =========================================================
    /// <summary>
    /// 获取调试信息喵~
    /// </summary>
    public string GetDebugInfo()
    {
        string info = $"=== VFSInstance: {PackID} ===\n";
        info += $"图类型：{GraphType}\n";
        info += $"源文件：{SourceJsonFileName}\n";
        info += $"节点数：{NodeMap.Count}\n";
        info += $"路径索引：{PathIndex.Count}\n";
        info += $"根节点：{RootNodeId ?? "null"}\n";
        info += $"已加载：{IsLoaded}\n";

        return info;
    }
}
