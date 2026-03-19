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
/// 2. 作为 VFS 管理总包，负责从"死数据"到"活对象"的觉醒
/// 3. 提供路径索引查询，快如闪电喵~
///
/// 职责：
/// 1. 管理所有 VFS 实例（InstanceID → VFSInstance）
/// 2. 加载并初始化 VFS 实例（拓扑扫描、路径注入、索引构建）
/// 3. 提供通用节点查询接口
///
/// 类比：
/// - GraphRunner: 💓 心脏（泵出信号，Update 驱动）
/// - GraphAnalyser: 🧠 大脑（存储地图，按需查询）
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class GraphAnalyser : SingletonMono<GraphAnalyser>
{
    // =========================================================
    //  实例管理字典喵~
    // =========================================================

    /// <summary>
    /// 所有的 VFS 实例都在这里排队喵~
    /// PackID → VFSInstance
    /// </summary>
    private Dictionary<string, VFSInstance> _vfsInstances = new Dictionary<string, VFSInstance>();

    /// <summary>
    /// 默认 Pack ID（用于单实例模式）
    /// </summary>
    private string _defaultPackId = "default";

    // =========================================================
    //  入口：加载并初始化 VFS 实例喵~
    // =========================================================

    /// <summary>
    /// 加载并初始化一个 VFS 实例喵~
    /// 【数据的觉醒祭坛】
    /// </summary>
    /// <param name="packID">资源包 ID（用于从 Resources 加载）</param>
    /// <returns>初始化完成的 VFS 实例</returns>
    public VFSInstance LoadVFS(string packID)
    {
        if (string.IsNullOrEmpty(packID))
        {
            Debug.LogError("[GraphAnalyser] packID 不能为空喵~");
            return null;
        }

        // 1. 调用 MetaLib 加载（沉睡状态）- 彻底拥抱 PackID 体系喵~
        var pack = MetaLib.GetPack<VFSPackData>(packID);
        if (pack == null)
        {
            Debug.LogError($"[GraphAnalyser] 加载 VFS Pack 失败：{packID}，请检查 MetaLib.json 配置喵！");
            return null;
        }

        // 2. 转换为运行时实例 (PackID 作为唯一标识)
        var instance = new VFSInstance(packID, "VFS");

        // 3. 从 PackData 填充 NodeMap（所有节点类型）
        foreach (var node in pack.Nodes)
        {
            // 添加到 NodeMap（所有节点类型，包括 RootNodeData 和 VFSNodeData）
            instance.NodeMap[node.NodeID] = node;

            // 如果是根节点，添加到根节点列表
            if (node is RootNodeData)
            {
                instance.RootNodeId = node.NodeID;
            }
        }

        // 4. 【总包核心动作】赋予灵魂！
        // 调用递归算法，填满 ParentNodeID 和 FullPath
        InternalRebuildTree(instance);

        // 5. 注册到总包字典
        if (_vfsInstances.ContainsKey(packID))
            _vfsInstances[packID] = instance;
        else
            _vfsInstances.Add(packID, instance);

        Debug.Log($"[GraphAnalyser] VFS 实例加载完成：{packID} (节点数：{instance.NodeMap.Count}, 路径索引：{instance.PathIndex.Count})");

        return instance;
    }

    /// <summary>
    /// 从已有的 VFSPackData 创建实例喵~
    /// </summary>
    /// <param name="pack">VFS 数据包</param>
    /// <returns>初始化完成的 VFS 实例</returns>
    public VFSInstance LoadVFSFromPack(VFSPackData pack)
    {
        if (pack == null)
        {
            Debug.LogError("[GraphAnalyser] Pack 不能为空喵~");
            return null;
        }

        string packID = pack.PackID;

        // 1. 创建运行时实例
        var instance = new VFSInstance(packID, "VFS");

        // 2. 从 PackData 填充 NodeMap（所有节点类型）
        foreach (var node in pack.Nodes)
        {
            // 添加到 NodeMap（所有节点类型，包括 RootNodeData 和 VFSNodeData）
            instance.NodeMap[node.NodeID] = node;

            // 如果是根节点，添加到根节点列表
            if (node is RootNodeData)
            {
                instance.RootNodeId = node.NodeID;
            }
        }

        // 3. 赋予灵魂 - 构建树形结构
        InternalRebuildTree(instance);

        // 4. 注册到总包字典
        if (_vfsInstances.ContainsKey(packID))
            _vfsInstances[packID] = instance;
        else
            _vfsInstances.Add(packID, instance);

        Debug.Log($"[GraphAnalyser] VFS 实例创建完成：{packID} (节点数：{instance.NodeMap.Count}, 路径索引：{instance.PathIndex.Count})");

        return instance;
    }

    // =========================================================
    //  运行时动态操作 API (Unix 风格) 喵~
    // =========================================================

    /// <summary>
    /// 在 VFS 中创建或写入文件喵~
    /// 【echo "xxx" > path】的底层实现喵！
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="path">目标完整路径（如 /social/messages/m1.json）</param>
    /// <param name="content">文件内容</param>
    /// <returns>是否操作成功</returns>
    public bool WriteFile(string packID, string path, string content)
    {
        var instance = GetInstance(packID);
        if (instance == null) return false;

        path = VFSPathResolver.Normalize(path);
        var existingNode = instance.GetNodeByPath(path);

        if (existingNode != null)
        {
            // 1. 文件已存在，直接更新内容喵~
            if (existingNode is VFSNodeData vfs)
            {
                vfs.DataJson = content;
                return true;
            }
            return false; // 可能是目录，不能直接写
        }
        else
        {
            // 2. 文件不存在，需要新建喵~
            string parentPath = VFSPathResolver.GetParentPath(path);
            
            // 递归创建父目录喵~（如果不存在）
            if (!EnsureDirectoryExists(packID, parentPath))
            {
                Debug.LogError($"[GraphAnalyser] 创建父目录失败：{parentPath}");
                return false;
            }
            
            var parentNode = instance.GetNodeByPath(parentPath);
            if (parentNode == null)
            {
                Debug.LogError($"[GraphAnalyser] 父目录不存在：{parentPath}");
                return false;
            }

            string fileName = VFSPathResolver.GetFileName(path);
            string nameWithoutExt = fileName;
            string ext = "";

            // 拆分文件名和扩展名
            int dotIndex = fileName.LastIndexOf('.');
            if (dotIndex > 0)
            {
                nameWithoutExt = fileName.Substring(0, dotIndex);
                ext = fileName.Substring(dotIndex);
            }

            var newNode = new VFSNodeData
            {
                NodeID = "vfs_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = nameWithoutExt,
                Extension = ext,
                DataJson = content,
                IsEnabled = true
            };

            if (instance.AddNodeRuntime(newNode, parentNode.NodeID))
            {
                // ✅ 局部更新已完成，不需要全量重建树
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 确保目录存在喵~（递归创建）
    /// 【mkdir -p path】的底层实现喵！
    /// </summary>
    private bool EnsureDirectoryExists(string packID, string path)
    {
        var instance = GetInstance(packID);
        if (instance == null) return false;

        path = VFSPathResolver.Normalize(path);
        
        // 如果目录已存在，直接返回成功喵~
        if (instance.PathExists(path))
        {
            var node = instance.GetNodeByPath(path);
            return node is VFSNodeData vfs && vfs.IsDirectory;
        }

        // 如果是根目录，认为已存在喵~
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return true;
        }

        // 递归创建父目录喵~
        string parentPath = VFSPathResolver.GetParentPath(path);
        if (!string.IsNullOrEmpty(parentPath) && parentPath != "/")
        {
            if (!EnsureDirectoryExists(packID, parentPath))
            {
                return false;
            }
        }

        // 创建当前目录喵~
        return CreateDirectoryInternal(packID, path);
    }

    /// <summary>
    /// 创建目录的内部方法（不检查父目录）喵~
    /// </summary>
    private bool CreateDirectoryInternal(string packID, string path)
    {
        var instance = GetInstance(packID);
        if (instance == null) return false;

        path = VFSPathResolver.Normalize(path);
        if (instance.PathExists(path)) return true;

        string parentPath = VFSPathResolver.GetParentPath(path);
        var parentNode = instance.GetNodeByPath(parentPath);
        if (parentNode == null) return false;

        var newDir = new VFSNodeData
        {
            NodeID = "dir_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = VFSPathResolver.GetFileName(path),
            Extension = "", // 空扩展名代表目录
            IsEnabled = true
        };

        if (instance.AddNodeRuntime(newDir, parentNode.NodeID))
        {
            // ✅ 局部更新已完成，不需要全量重建树
            return true;
        }

        return false;
    }

    /// <summary>
    /// 在 VFS 中创建目录喵~
    /// 【mkdir path】的底层实现喵！
    /// </summary>
    public bool CreateDirectory(string packID, string path)
    {
        return EnsureDirectoryExists(packID, path);
    }

    /// <summary>
    /// 删除路径对应的节点喵~
    /// 【rm -rf path】喵！
    /// </summary>
    public bool Delete(string packID, string path)
    {
        var instance = GetInstance(packID);
        if (instance == null) return false;

        var node = instance.GetNodeByPath(path);
        if (node == null) return false;

        if (instance.RemoveNodeRuntime(node.NodeID))
        {
            InternalRebuildTree(instance);
            return true;
        }
        return false;
    }

    // =========================================================
    //  核心：内部树构建喵~（通用版本，不依赖 VFSNodeData）
    // =========================================================

    /// <summary>
    /// 核心动作：根据连线关系构建整个文件树喵~
    /// 生成路径索引和层级关系（侧边索引）
    /// </summary>
    public void InternalRebuildTree(VFSInstance instance)
    {
        if (instance == null || instance.NodeMap == null) return;

        // 清空侧边索引
        instance.NodeToPath.Clear();
        instance.PathIndex.Clear();
        instance.Hierarchy.Clear();

        // 从根节点开始遍历
        if (!string.IsNullOrEmpty(instance.RootNodeId) &&
            instance.NodeMap.TryGetValue(instance.RootNodeId, out var root))
        {
            AnalyzeRecursive(root, "/", instance);
        }

        Debug.Log($"[GraphAnalyser] VFS 树重构完成喵！节点数：{instance.NodeMap.Count}, 路径索引：{instance.PathIndex.Count}");
    }

    /// <summary>
    /// 通用递归分析器 - 不依赖具体节点类型喵~
    /// 只认识 BaseNodeData，适用于任何图结构
    /// </summary>
    private void AnalyzeRecursive(BaseNodeData node, string currentPath, VFSInstance instance)
    {
        // 1. 记录路径映射
        instance.NodeToPath[node.NodeID] = currentPath;
        instance.PathIndex[currentPath] = node.NodeID;

        // 2. 遍历物理连线
        if (node.OutputConnections == null) return;

        foreach (var conn in node.OutputConnections)
        {
            if (instance.NodeMap.TryGetValue(conn.TargetNodeID, out var child))
            {
                // 记录父子层级关系
                if (!instance.Hierarchy.ContainsKey(node.NodeID))
                    instance.Hierarchy[node.NodeID] = new List<string>();
                instance.Hierarchy[node.NodeID].Add(child.NodeID);

                // 计算子路径（需要包含扩展名喵~）
                string childName = child.Name;
                if (child is VFSNodeData vfsChild && vfsChild.IsFile)
                {
                    childName += vfsChild.Extension;
                }

                if (string.IsNullOrEmpty(childName))
                    childName = child.NodeID;
                
                // 灵活处理斜杠：如果当前路径是 "/"，直接拼接
                string nextPath;
                if (currentPath == "/")
                    nextPath = "/" + childName;
                else
                    nextPath = currentPath.EndsWith("/") ? currentPath + childName : currentPath + "/" + childName;

                // 如果是目录（VFSNodeData 且 Extension 为空），添加结尾斜杠
                if (child is VFSNodeData vfs && vfs.IsDirectory)
                    nextPath += "/";

                AnalyzeRecursive(child, nextPath, instance);
            }
        }
    }

    // =========================================================
    //  访问：通用节点查询接口喵~
    // =========================================================

    /// <summary>
    /// 全系统通用的节点查询接口喵~
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="path">路径（如 "/social/friends/"）</param>
    /// <returns>BaseNodeData，如果不存在则返回 null</returns>
    public BaseNodeData GetNode(string packID, string path)
    {
        if (_vfsInstances.Count == 0)
        {
            Debug.LogWarning("[GraphAnalyser] 未初始化！请先调用 vfs_mount 命令加载 VFS 数据包喵~");
            return null;
        }

        if (!_vfsInstances.TryGetValue(packID, out var instance))
        {
            Debug.LogWarning($"[GraphAnalyser] 实例不存在：{packID}。可用实例：{string.Join(", ", _vfsInstances.Keys)}");
            return null;
        }

        // 直接通过总包里的路径索引查，快如闪电喵！
        return instance.GetNodeByPath(path);
    }

    /// <summary>
    /// 获取默认实例的节点喵~
    /// </summary>
    public BaseNodeData GetNode(string path)
    {
        return GetNode(_defaultPackId, path);
    }

    /// <summary>
    /// 获取子节点列表喵~
    /// </summary>
    /// <param name="packID">Pack ID</param>
    /// <param name="path">父路径</param>
    /// <returns>子节点列表</returns>
    public List<BaseNodeData> GetChildren(string packID, string path)
    {
        if (_vfsInstances.Count == 0)
        {
            Debug.LogWarning("[GraphAnalyser] 未初始化！请先调用 vfs_mount 命令加载 VFS 数据包喵~");
            return new List<BaseNodeData>();
        }

        if (!_vfsInstances.TryGetValue(packID, out var instance))
        {
            Debug.LogWarning($"[GraphAnalyser] 实例不存在：{packID}。可用实例：{string.Join(", ", _vfsInstances.Keys)}");
            return new List<BaseNodeData>();
        }

        return instance.GetChildrenByPath(path);
    }

    /// <summary>
    /// 获取默认实例的子节点列表喵~
    /// </summary>
    public List<BaseNodeData> GetChildren(string path)
    {
        return GetChildren(_defaultPackId, path);
    }

    /// <summary>
    /// 检查路径是否存在喵~
    /// </summary>
    public bool PathExists(string packID, string path)
    {
        return GetNode(packID, path) != null;
    }

    // =========================================================
    //  实例管理喵~
    // =========================================================

    /// <summary>
    /// 注册 VFS 实例到总包喵~
    /// </summary>
    public void RegisterInstance(VFSInstance instance)
    {
        if (instance == null) return;

        if (_vfsInstances.ContainsKey(instance.PackID))
            _vfsInstances[instance.PackID] = instance;
        else
            _vfsInstances.Add(instance.PackID, instance);

        instance.IsLoaded = true;
        Debug.Log($"[GraphAnalyser] 实例注册：{instance.PackID}");
    }

    /// <summary>
    /// 注销 VFS 实例喵~
    /// </summary>
    public void UnregisterInstance(string packID)
    {
        if (_vfsInstances.TryGetValue(packID, out var instance))
        {
            instance.IsLoaded = false;
            instance.Clear();
            _vfsInstances.Remove(packID);
            Debug.Log($"[GraphAnalyser] 实例注销：{packID}");
        }
    }

    /// <summary>
    /// 获取 VFS 实例喵~
    /// </summary>
    public VFSInstance GetInstance(string packID)
    {
        _vfsInstances.TryGetValue(packID, out var instance);
        return instance;
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
    /// 获取所有实例 ID (PackID) 列表喵~
    /// </summary>
    public List<string> GetAllInstanceIds()
    {
        return new List<string>(_vfsInstances.Keys);
    }

    // =========================================================
    //  调试信息喵~
    // =========================================================

    /// <summary>
    /// 获取调试信息喵~
    /// </summary>
    public string GetDebugInfo()
    {
        string info = $"=== GraphAnalyser 状态 ===\n";
        info += $"实例数量：{_vfsInstances.Count}\n";

        foreach (var kvp in _vfsInstances)
        {
            info += $"\n  实例：{kvp.Key}\n";
            info += $"    节点数：{kvp.Value.NodeMap.Count}\n";
            info += $"    路径索引：{kvp.Value.PathIndex.Count}\n";
            info += $"    根节点：{kvp.Value.RootNodeId ?? "null"}\n";
        }

        return info;
    }

    // =========================================================
    //  Unity 生命周期喵~
    // =========================================================

    protected override void Awake()
    {
        base.Awake();
        Debug.Log("[GraphAnalyser] 静态图解析大脑已启动喵~ 🧠");
    }
}
