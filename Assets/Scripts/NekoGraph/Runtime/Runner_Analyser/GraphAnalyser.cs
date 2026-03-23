using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// GraphAnalyser - 静态图解析大脑喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 设计理念：
/// 1. 与 GraphRunner 形成"双子星"架构 - Runner 管动态生命，Analyser 管静态空间
/// 2. 直接持有 BasePackData，零冗余副本，pack.Nodes 就是唯一真相喵~
/// 3. 路径查询通过按需 BFS 完成，不预建索引喵~
///
/// 职责：
/// 1. 管理所有已挂载的 Pack（PackID → BasePackData）
/// 2. 提供 Unix 风格路径 IO 接口（读/写/删/mkdir）
/// 3. 通过 Resolve() 统一权限校验
///
/// 类比：
/// - GraphRunner: 💓 心脏（泵出信号，Update 驱动）
/// - GraphAnalyser: 🧠 大脑（存储地图，按需查询）
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class GraphAnalyser
{
    public static GraphAnalyser Instance => GraphHub.Instance?.DefaultAnalyser;
    // =========================================================
    //  Pack 注册表喵~
    // =========================================================

    /// <summary>
    /// 直接引用 UserModel.PackDataDict，零副本，读档后自动切换到新存档喵~
    /// </summary>
    private Dictionary<string, BasePackData> _packs;

    private Dictionary<string, BasePackData> Packs => _packs;
    private readonly int _subjectLevel;

    /// <summary>
    /// 默认 Pack ID（用于单实例模式）
    /// </summary>
    private string _defaultPackId = "default";

    public GraphAnalyser(Dictionary<string, BasePackData> packs = null, int subjectLevel = PackAccessSubjects.Player)
    {
        _subjectLevel = subjectLevel;
        SetPackDataDict(packs);
    }

    public void SetPackDataDict(Dictionary<string, BasePackData> packs)
    {
        _packs = packs ?? new Dictionary<string, BasePackData>();
        _packIDToGuid.Clear();
    }

    // =========================================================
    //  PackID → GUID 二级索引（O(1) 查询）喵~
    //  PackDataDict key = GUID，外部 API 全用 PackID，靠索引桥接
    // =========================================================

    /// <summary>PackID → GUID key，随挂载/卸载实时维护，VFS.IO_Ready 时整体重建喵~</summary>
    private readonly Dictionary<string, string> _packIDToGuid = new Dictionary<string, string>();

    public void RebuildIndex()
    {
        _packIDToGuid.Clear();
        var packs = Packs;
        if (packs == null) return;
        foreach (var kvp in packs)
            _packIDToGuid[kvp.Value.PackID] = kvp.Key;
    }

    private BasePackData FindPackByPackID(string packID)
    {
        if (!_packIDToGuid.TryGetValue(packID, out var guid)) return null;
        var packs = Packs;
        if (packs == null) return null;
        packs.TryGetValue(guid, out var pack);
        return pack;
    }

    private string FindGuidByPackID(string packID)
    {
        _packIDToGuid.TryGetValue(packID, out var guid);
        return guid;
    }

    // =========================================================
    //  BFS 核心算法（静态，无状态）喵~
    // =========================================================

    /// <summary>
    /// 获取节点在路径中的分量名喵~
    /// 目录：node.Name；文件：node.Name + node.Extension；兜底：node.NodeID
    /// </summary>
    private static string GetSegmentName(BaseNodeData node)
    {
        string name = node.Name;
        if (node is VFSNodeData vfs && vfs.IsFile)
            name += vfs.Extension;
        return string.IsNullOrEmpty(name) ? node.NodeID : name;
    }

    /// <summary>
    /// 从 Pack 根节点出发，按路径 BFS 查找目标节点喵~
    /// </summary>
    private static BaseNodeData BfsGetNode(BasePackData pack, string path)
    {
        if (string.IsNullOrEmpty(pack.RootNodeId) ||
            !pack.Nodes.TryGetValue(pack.RootNodeId, out var root))
            return null;

        path = VFSPathResolver.Normalize(path);
        if (path == "/") return root;

        var segments = path.Trim('/').Split('/');
        var current = root;

        foreach (var segment in segments)
        {
            if (current.OutputConnections == null) return null;
            BaseNodeData next = null;
            foreach (var conn in current.OutputConnections)
            {
                if (pack.Nodes.TryGetValue(conn.TargetNodeID, out var child) &&
                    GetSegmentName(child) == segment)
                {
                    next = child;
                    break;
                }
            }
            if (next == null) return null;
            current = next;
        }
        return current;
    }

    /// <summary>
    /// 获取路径对应节点的直接子节点列表喵~
    /// </summary>
    private static List<BaseNodeData> BfsGetChildren(BasePackData pack, string path)
    {
        var parent = BfsGetNode(pack, path);
        if (parent?.OutputConnections == null) return new List<BaseNodeData>();

        var result = new List<BaseNodeData>();
        foreach (var conn in parent.OutputConnections)
        {
            if (!pack.Nodes.TryGetValue(conn.TargetNodeID, out var child)) continue;
            if (child is VFSNodeData vfs && !vfs.IsEnabled) continue;
            result.Add(child);
        }
        return result;
    }

    // =========================================================
    //  权限网关 - 所有公开 IO 操作的统一入口喵~
    // =========================================================

    /// <summary>
    /// 解析 Pack 并校验权限喵~
    /// Hidden 始终拒绝；write=true 时 ReadOnly 也拒绝。
    /// 返回 null 表示无权限或 Pack 不存在。
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="write">是否需要写权限</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    private BasePackData Resolve(string packID, bool write, int subjectLevel)
    {
        var pack = FindPackByPackID(packID);
        if (pack == null)
        {
            Debug.LogWarning($"[GraphAnalyser] Pack 不存在：{packID} 喵~");
            return null;
        }

        PackAccessLevel accessLevel = GetPackAccessLevel(pack, subjectLevel);
        if (accessLevel == PackAccessLevel.Hidden)
        {
            Debug.LogWarning($"[GraphAnalyser] 拒绝访问：{packID} 已隐藏喵~");
            return null;
        }
        if (write && accessLevel != PackAccessLevel.ReadWrite)
        {
            Debug.LogWarning($"[GraphAnalyser] 拒绝写入：{packID} 权限为 {accessLevel} 喵~");
            return null;
        }
        return pack;
    }

    /// <summary>
    /// 获取对 Pack 的访问级别喵~
    /// </summary>
    /// <param name="pack">Pack 数据</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public PackAccessLevel GetPackAccessLevel(BasePackData pack, int subjectLevel)
    {
        if (pack == null)
            return PackAccessLevel.Hidden;

        if (GraphHub.Instance != null)
            return GraphHub.Instance.GetPackAccessLevel(subjectLevel, pack);

        if (subjectLevel < pack.ReadableFrom)
            return PackAccessLevel.Hidden;

        if (subjectLevel < pack.WritableFrom)
            return PackAccessLevel.ReadOnly;

        return PackAccessLevel.ReadWrite;
    }

    // =========================================================
    //  挂载 / 卸载喵~
    // =========================================================

    /// <summary>
    /// 从 MetaLib 加载并挂载 Pack 喵~
    /// </summary>
    public BasePackData LoadVFS(string packID)
    {
        if (string.IsNullOrEmpty(packID))
        {
            Debug.LogError("[GraphAnalyser] packID 不能为空喵~");
            return null;
        }

        var pack = MetaLib.GetPack<BasePackData>(packID);
        if (pack == null)
        {
            Debug.LogError($"[GraphAnalyser] 加载 Pack 失败：{packID}，请检查 MetaLib.json 配置喵！");
            return null;
        }

        var packs = Packs;
        if (packs == null) { Debug.LogWarning("[GraphAnalyser] 无当前用户，无法挂载 Pack 喵~"); return null; }
        string guid = FindGuidByPackID(packID) ?? Guid.NewGuid().ToString("N");
        packs[guid] = pack;
        _packIDToGuid[packID] = guid;
        Debug.Log($"[GraphAnalyser] Pack 已加载：{packID}（节点数：{pack.Nodes.Count}）");
        return pack;
    }

    /// <summary>
    /// 直接挂载已有的 BasePackData 喵~
    /// </summary>
    public BasePackData LoadVFSFromPack(BasePackData pack)
    {
        if (pack == null)
        {
            Debug.LogError("[GraphAnalyser] Pack 不能为空喵~");
            return null;
        }

        var packs = Packs;
        if (packs == null) { Debug.LogWarning("[GraphAnalyser] 无当前用户，无法挂载 Pack 喵~"); return null; }
        string guid = FindGuidByPackID(pack.PackID) ?? Guid.NewGuid().ToString("N");
        packs[guid] = pack;
        _packIDToGuid[pack.PackID] = guid;
        Debug.Log($"[GraphAnalyser] Pack 已挂载：{pack.PackID}（节点数：{pack.Nodes.Count}）");
        return pack;
    }

    // =========================================================
    //  运行时动态操作 API（Unix 风格）喵~
    // =========================================================

    /// <summary>
    /// 写入或创建文件喵~【echo "xxx" > path】
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="path">节点路径</param>
    /// <param name="content">文件内容</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public bool WriteFile(string packID, string path, string content, int subjectLevel)
    {
        var pack = Resolve(packID, write: true, subjectLevel: subjectLevel);
        if (pack == null) return false;

        path = VFSPathResolver.Normalize(path);
        var existing = BfsGetNode(pack, path);

        if (existing is VFSNodeData existingVfs)
        {
            existingVfs.DataJson = content;
            return true;
        }
        if (existing != null) return false; // 目录，不能直接写

        string parentPath = VFSPathResolver.GetParentPath(path);
        if (!EnsureDirectory(pack, parentPath))
        {
            Debug.LogError($"[GraphAnalyser] 创建父目录失败：{parentPath}");
            return false;
        }

        var parent = BfsGetNode(pack, parentPath);
        if (parent == null)
        {
            Debug.LogError($"[GraphAnalyser] 父目录不存在：{parentPath}");
            return false;
        }

        string fileName = VFSPathResolver.GetFileName(path);
        int dot = fileName.LastIndexOf('.');
        var newNode = new VFSNodeData
        {
            NodeID = "vfs_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = dot > 0 ? fileName.Substring(0, dot) : fileName,
            Extension = dot > 0 ? fileName.Substring(dot) : "",
            DataJson = content,
            IsEnabled = true
        };

        return AddNode(pack, newNode, parent.NodeID);
    }

    /// <summary>
    /// 创建目录喵~【mkdir -p path】
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="path">目录路径</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public bool CreateDirectory(string packID, string path, int subjectLevel)
    {
        var pack = Resolve(packID, write: true, subjectLevel: subjectLevel);
        return pack != null && EnsureDirectory(pack, VFSPathResolver.Normalize(path));
    }

    /// <summary>
    /// 删除节点喵~【rm -rf path】
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="path">节点路径</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public bool Delete(string packID, string path, int subjectLevel)
    {
        var pack = Resolve(packID, write: true, subjectLevel: subjectLevel);
        if (pack == null) return false;

        var node = BfsGetNode(pack, path);
        if (node == null) return false;

        return RemoveNode(pack, node.NodeID);
    }

    // =========================================================
    //  内部节点增删（直接操作 pack.Nodes）喵~
    // =========================================================

    private static bool AddNode(BasePackData pack, BaseNodeData node, string parentID)
    {
        if (pack.Nodes.ContainsKey(node.NodeID))
        {
            Debug.LogWarning($"[GraphAnalyser] 节点已存在：{node.NodeID} 喵~");
            return false;
        }

        pack.Nodes[node.NodeID] = node;

        if (!string.IsNullOrEmpty(parentID) && pack.Nodes.TryGetValue(parentID, out var parent))
        {
            if (!parent.OutputConnections.Exists(c => c.TargetNodeID == node.NodeID))
                parent.OutputConnections.Add(new ConnectionData(0, node.NodeID, 0));
            if (node is VFSNodeData vfs)
                vfs.ParentNodeID = parentID;
        }

        return true;
    }

    private static bool RemoveNode(BasePackData pack, string nodeID)
    {
        if (!pack.Nodes.Remove(nodeID)) return false;
        foreach (var n in pack.Nodes.Values)
            n.OutputConnections?.RemoveAll(c => c.TargetNodeID == nodeID);
        return true;
    }

    /// <summary>
    /// 确保目录存在（递归创建）喵~
    /// </summary>
    private static bool EnsureDirectory(BasePackData pack, string path)
    {
        path = VFSPathResolver.Normalize(path);
        if (path == "/" || string.IsNullOrEmpty(path)) return true;

        var existing = BfsGetNode(pack, path);
        if (existing is VFSNodeData vfs) return vfs.IsDirectory;
        if (existing != null) return false; // 路径被文件占用

        string parentPath = VFSPathResolver.GetParentPath(path);
        if (!EnsureDirectory(pack, parentPath)) return false;

        var parent = BfsGetNode(pack, parentPath);
        if (parent == null) return false;

        var dir = new VFSNodeData
        {
            NodeID = "dir_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = VFSPathResolver.GetFileName(path),
            Extension = "",
            IsEnabled = true
        };
        return AddNode(pack, dir, parent.NodeID);
    }

    // =========================================================
    //  查询接口喵~
    // =========================================================

    /// <summary>
    /// 按路径查询节点喵~
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="path">节点路径</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public BaseNodeData GetNode(string packID, string path, int subjectLevel)
    {
        var pack = Resolve(packID, write: false, subjectLevel: subjectLevel);
        return pack == null ? null : BfsGetNode(pack, path);
    }

    /// <summary>
    /// 查询默认 Pack 的节点喵~
    /// </summary>
    /// <param name="path">节点路径</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public BaseNodeData GetNode(string path, int subjectLevel) => GetNode(_defaultPackId, path, subjectLevel);

    /// <summary>
    /// 获取路径下的直接子节点列表喵~
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="path">节点路径</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public List<BaseNodeData> GetChildren(string packID, string path, int subjectLevel)
    {
        var pack = Resolve(packID, write: false, subjectLevel: subjectLevel);
        return pack == null ? new List<BaseNodeData>() : BfsGetChildren(pack, path);
    }

    /// <summary>
    /// 获取默认 Pack 路径下的子节点列表喵~
    /// </summary>
    /// <param name="path">节点路径</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public List<BaseNodeData> GetChildren(string path, int subjectLevel) => GetChildren(_defaultPackId, path, subjectLevel);

    /// <summary>
    /// 检查路径是否存在喵~
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="path">节点路径</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public bool PathExists(string packID, string path, int subjectLevel) => GetNode(packID, path, subjectLevel) != null;

    // =========================================================
    //  Pack 管理喵~
    // =========================================================

    /// <summary>
    /// 注册 Pack 喵~
    /// </summary>
    public void RegisterPack(BasePackData pack)
    {
        if (pack == null) return;
        var packs = Packs;
        if (packs == null) return;
        string guid = FindGuidByPackID(pack.PackID) ?? Guid.NewGuid().ToString("N");
        packs[guid] = pack;
        _packIDToGuid[pack.PackID] = guid;
        Debug.Log($"[GraphAnalyser] Pack 注册：{pack.PackID}");
    }

    /// <summary>
    /// 注销 Pack 喵~
    /// </summary>
    public void UnregisterPack(string packID)
    {
        var packs = Packs;
        if (packs == null) return;
        string guid = FindGuidByPackID(packID);
        if (guid != null && packs.Remove(guid))
        {
            _packIDToGuid.Remove(packID);
            Debug.Log($"[GraphAnalyser] Pack 注销：{packID}");
        }
    }

    /// <summary>
    /// 获取已挂载的 Pack 喵~
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public BasePackData GetPack(string packID, int subjectLevel)
    {
        var pack = FindPackByPackID(packID);
        if (pack == null) return null;
        
        // 检查读权限
        if (subjectLevel < pack.ReadableFrom)
            return null;
        
        return pack;
    }

    /// <summary>
    /// 设置默认 Pack ID 喵~
    /// </summary>
    public void SetDefaultPackId(string packID)
    {
        if (!string.IsNullOrEmpty(packID))
            _defaultPackId = packID;
    }

    /// <summary>
    /// 获取所有已挂载的 Pack ID 列表喵~（过滤隐藏的 Pack）
    /// </summary>
    /// <param name="subjectLevel">主体等级（强制传入，不允许默认值）喵~</param>
    public List<string> GetAllPackIds(int subjectLevel)
    {
        var result = new List<string>();
        foreach (var kvp in _packIDToGuid)
        {
            var pack = FindPackByPackID(kvp.Key);
            if (pack != null && subjectLevel >= pack.ReadableFrom)
            {
                result.Add(kvp.Key);
            }
        }
        return result;
    }

    // =========================================================
    //  调试信息喵~
    // =========================================================

    public string GetDebugInfo()
    {
        var packs = Packs;
        var sb = new System.Text.StringBuilder("=== GraphAnalyser 状态 ===\n");
        sb.Append($"Pack 数量：{packs?.Count ?? 0}\n");
        if (packs == null) return sb.ToString();
        foreach (var kvp in packs)
        {
            sb.Append($"\n  Pack：{kvp.Value.PackID}\n");
            sb.Append($"    节点数：{kvp.Value.Nodes.Count}\n");
            sb.Append($"    根节点：{kvp.Value.RootNodeId ?? "null"}\n");
            sb.Append($"    权限：{GetPackAccessLevel(kvp.Value, _subjectLevel)}\n");
        }
        return sb.ToString();
    }

    // =========================================================
    //  Unity 生命周期喵~
    // =========================================================

    private void AwakeCompatibility()
    {
        Debug.Log("[GraphAnalyser] 静态图解析大脑已启动喵~ 🧠");
    }
}
