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
/// 1. VFSLoader.LoadPackFromResources() → VFSPackData（沉睡数据）
/// 2. new VFSInstance() → VFSInstance（空壳实例）
/// 3. GraphAnalyser.LoadVFS() → InternalRebuildTree()（注入灵魂）
/// 4. PathIndex 生成完成，可以快如闪电地查询喵~！
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[Serializable]
public class VFSInstance
{
    /// <summary>
    /// 实例 ID
    /// </summary>
    public string InstanceID;

    /// <summary>
    /// 图类型（固定为 "VFS"）
    /// </summary>
    public string GraphType;

    /// <summary>
    /// 源 JSON 文件名（或 PackID）
    /// </summary>
    public string SourceJsonFileName;

    /// <summary>
    /// 节点字典：NodeID → BaseNodeData（通用类型）
    /// </summary>
    public Dictionary<string, BaseNodeData> NodeMap = new Dictionary<string, BaseNodeData>();

    /// <summary>
    /// 路径索引：FullPath → NodeID（不区分大小写）
    /// </summary>
    public Dictionary<string, string> PathIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 根节点 ID 列表
    /// </summary>
    public List<string> RootNodeIds = new List<string>();

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
    /// <param name="instanceID">实例 ID</param>
    /// <param name="graphType">图类型（默认 "VFS"）</param>
    /// <param name="sourceJsonFileName">源 JSON 文件名（或 PackID）</param>
    public VFSInstance(string instanceID, string graphType = "VFS", string sourceJsonFileName = null)
    {
        InstanceID = instanceID;
        GraphType = graphType;
        SourceJsonFileName = sourceJsonFileName;
        NodeMap = new Dictionary<string, BaseNodeData>();
        PathIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        RootNodeIds = new List<string>();
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

        // 2. 建立连线关系
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
        }
        else if (node is RootNodeData)
        {
            // 如果没有父节点且是根节点类型，加入根列表
            if (!RootNodeIds.Contains(node.NodeID))
                RootNodeIds.Add(node.NodeID);
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
    /// </summary>
    private void UpdateNodePathIndex(VFSNodeData node, string parentID)
    {
        // 计算节点的完整路径
        string nodePath = BuildNodePath(node, parentID);
        
        // 添加到路径索引
        if (!string.IsNullOrEmpty(nodePath))
        {
            PathIndex[nodePath] = node.NodeID;
        }
    }

    /// <summary>
    /// 构建节点的完整路径喵~
    /// </summary>
    private string BuildNodePath(VFSNodeData node, string parentID)
    {
        if (node == null) return "";

        // 如果是根目录，直接返回 /
        if (string.IsNullOrEmpty(parentID) && node is RootNodeData)
        {
            return "/";
        }

        // 获取父节点路径
        string parentPath = "";
        if (!string.IsNullOrEmpty(parentID) && NodeMap.TryGetValue(parentID, out var parentNode))
        {
            if (parentNode is VFSNodeData parentVfs)
            {
                parentPath = BuildNodePath(parentVfs, parentVfs.ParentNodeID);
            }
        }

        // 拼接当前节点路径
        string nodeName = node.Name + (node.IsDirectory ? "" : node.Extension);
        if (string.IsNullOrEmpty(parentPath) || parentPath == "/")
        {
            return "/" + nodeName;
        }
        else
        {
            return parentPath + "/" + nodeName;
        }
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

        // 2. 从根节点列表移除
        RootNodeIds.Remove(nodeID);

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
            PackID = SourceJsonFileName ?? InstanceID,
            DisplayName = InstanceID + " Runtime Snapshot",
            Description = "由 VFSInstance 动态生成的快照数据包喵~",
            Nodes = new List<BaseNodeData>(NodeMap.Values),
            RootNodeIds = new List<string>(RootNodeIds)
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
        if (node is RootNodeData && !RootNodeIds.Contains(node.NodeID))
            RootNodeIds.Add(node.NodeID);
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
        RootNodeIds.Clear();
        IsLoaded = false;
    }

    // =========================================================
    /// <summary>
    /// 获取调试信息喵~
    /// </summary>
    public string GetDebugInfo()
    {
        string info = $"=== VFSInstance: {InstanceID} ===\n";
        info += $"图类型：{GraphType}\n";
        info += $"源文件：{SourceJsonFileName}\n";
        info += $"节点数：{NodeMap.Count}\n";
        info += $"路径索引：{PathIndex.Count}\n";
        info += $"根节点：{string.Join(", ", RootNodeIds)}\n";
        info += $"已加载：{IsLoaded}\n";

        return info;
    }
}
