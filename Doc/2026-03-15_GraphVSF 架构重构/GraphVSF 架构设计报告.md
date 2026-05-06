# GraphVSF 架构设计报告

**日期**: 2026 年 3 月 15 日  
**状态**: 架构设计完成  
**作者**: NekoTeam

---

## 目录

1. [设计背景](#一设计背景)
2. [架构设计](#二架构设计)
3. [核心类型设计](#三核心类型设计)
4. [GraphAnalyser 通用分析器](#四 graphanalyser-通用分析器)
5. [SocialCLI 集成](#五 socialcli 集成)
6. [编辑器工具](#六编辑器工具)
7. [实施计划](#七实施计划)
8. [总结](#八总结)

---

## 一、设计背景

### 1.1 需求

为 SocialCLI 设计一个**虚拟文件系统（VFS）**，用于管理社交数据的树状结构：

```
/social/
├── friends/
│   ├── list.json      # 好友列表数据
│   └── count.json     # 好友数量统计
├── requests/
│   ├── incoming/      # 收到的请求
│   └── outgoing/      # 发出的请求
├── blocks/            # 黑名单
├── groups/            # 群组
└── messages/          # 消息历史
```

### 1.2 设计原则

1. **Unix 哲学：一切皆文件** - 向 Linux 学习，目录也是文件的一种
2. **复用优先** - 使用原有的 `BasePackData`、`BaseNodeData`、`RootNodeData`
3. **最小化设计** - 只定义必要的类型，避免过度设计
4. **遵循项目规范** - Editor 代码放 `Editor/`，Runtime 代码放 `Runtime/`

### 1.3 原有架构分析

#### NekoGraph 现有结构

```
Assets/Scripts/NekoGraph/
├── Runtime/
│   ├── Base/
│   │   ├── BasePackData.cs       # 数据包基类
│   │   └── BaseNodeData.cs       # 节点数据基类
│   ├── Data/
│   │   └── ProcessFlowNodeData.cs  # RootNodeData, SpineNodeData 等
│   ├── Attributes/
│   │   ├── NodeTypeAttribute.cs
│   │   └── NodeMenuItemAttribute.cs
│   └── ...
└── Editor/
    └── _Base/
        ├── BaseGraphView.cs
        ├── BaseGraphWindow.cs
        ├── BaseNode.cs
        └── BaseNodeSearchWindow.cs
```

#### 问题：初始 VSF 设计的错误

1. **重复定义类型** - 定义了两份 `NodeSystem`、`NodeMenuItemAttribute`、`BasePackData`
2. **文件结构混乱** - 将 Editor 代码放在 `Common/` 目录下
3. **节点类型过多** - 定义了 `Root/Folder/File` 三种节点类型，过度复杂
4. **忽视现有架构** - 没有复用原有的基础设施

---

## 二、架构设计

### 2.1 文件结构

```
Assets/Scripts/
├── NekoGraph/
│   ├── Runtime/
│   │   ├── Runner_Analyser/        ← 图运行器/分析器（通用）
│   │   │   ├── GraphRunner.cs        # 动态图运行器（原有）
│   │   │   ├── RuntimeGraphInstance.cs  # 动态图实例（原有）
│   │   │   ├── SignalContext.cs      # 信号上下文（原有）
│   │   │   ├── INodeStrategy.cs      # 节点策略（原有）
│   │   │   │
│   │   │   └── GraphAnalyser.cs    # 静态图分析器（新）✨
│   │   │       └─→ 用于 VFS、Config、SkillTree 等静态图
│   │   │
│   │   ├── GraphVSF/               ← VFS 专用数据
│   │   │   ├── VFSNodeData.cs        # VFS 节点数据
│   │   │   └── VFSPackData.cs        # VFS 数据包
│   │   │
│   │   ├── Base/                   ← 原有基础类
│   │   │   ├── BasePackData.cs
│   │   │   └── BaseNodeData.cs
│   │   │
│   │   ├── Data/                   ← 原有节点数据
│   │   │   └── ProcessFlowNodeData.cs  # RootNodeData 等
│   │   │
│   │   └── Attributes/             ← 原有属性标签
│   │       ├── NodeTypeAttribute.cs
│   │       └── NodeMenuItemAttribute.cs
│   │
│   └── Editor/
│       ├── _Base/                  ← 原有通用编辑器基类
│       │   ├── BaseGraphView.cs
│       │   ├── BaseGraphWindow.cs
│       │   ├── BaseNode.cs
│       │   └── BaseNodeSearchWindow.cs
│       │
│       └── GraphVSF/               ← VFS 编辑器
│           ├── VFSGraphView.cs
│           ├── VFSGraphWindow.cs
│           ├── VFSNodeSearchWindow.cs
│           └── VFSNodeView.cs
│
├── OutStage/
│   └── SocialCLI/                  ← SocialCLI 集成
│       ├── SocialCLI.cs
│       └── CommandRegistry.Social.cs
│
└── Resources/
    └── NekoGraph/
        └── GraphVSF/
            └── Packs/              ← VFS 数据包 JSON
                └── social_tree.json
```

### 2.2 架构分层

```
┌─────────────────────────────────────────────────────────────┐
│                    Editor Layer (编辑器层)                    │
├─────────────────────────────────────────────────────────────┤
│  VFSGraphView    - 画布，处理节点连线、验证                   │
│  VFSGraphWindow  - 窗口，处理保存/加载、工具栏                 │
│  VFSNodeView     - 节点视图，处理 UI 编辑                      │
│  VFSNodeSearchWindow - 搜索窗口，处理节点创建菜单              │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                   Runtime Layer (运行时层)                   │
├─────────────────────────────────────────────────────────────┤
│  GraphAnalyser   - 静态图分析器（通用）                       │
│    ├─ BuildTreeStructure()  - 构建树形结构                   │
│    ├─ BuildPathIndex()      - 建立路径索引                   │
│    └─ GetChildren()         - 获取子节点                     │
│                                                              │
│  VFSNodeData     - VFS 节点数据（继承 BaseNodeData）          │
│  VFSPackData     - VFS 数据包（继承 BasePackData）            │
│  VFSLoader       - VFS 加载器（可选）                         │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                  Base Layer (基础层 - 原有)                   │
├─────────────────────────────────────────────────────────────┤
│  GraphRunner     - 动态图运行器（原有）                       │
│  BaseNodeData    - 节点数据基类（NodeID, EditorPosition...）  │
│  BasePackData    - 数据包基类（PackID, DisplayName, Nodes）   │
│  RootNodeData    - 根节点数据（复用作为 VFS 根目录）            │
│  NodeMenuItemAttribute - 节点菜单标签                        │
│  NodeTypeAttribute   - 节点系统类型标签                      │
└─────────────────────────────────────────────────────────────┘
```

### 2.3 设计哲学：动态 vs 静态

| 特性 | GraphRunner (动态) | GraphAnalyser (静态) |
|------|-------------------|---------------------|
| **用途** | 任务/剧情流程执行 | 文件树/配置树分析 |
| **驱动方式** | Update() 每帧驱动 | 按需调用 |
| **信号流动** | SignalContext 在节点间流动 | 无信号，只有查询 |
| **节点策略** | INodeStrategy 处理逻辑 | 无策略，只有数据结构 |
| **状态保存** | 需要保存 Signal 位置 | 只需保存树结构 |
| **典型应用** | Mission、Story | VFS、Config、SkillTree |

---

## 三、核心类型设计

### 3.1 统一节点数据类

```csharp
/// <summary>
/// VFS 统一节点数据类
/// 
/// 设计哲学：
/// - 继承 BaseNodeData，复用 NodeID、EditorPosition、OutputConnections
/// - 使用 Name + Extension 区分用途（类似 Linux 文件）
/// - 不定义 Root/Folder/File 枚举，用 Extension 是否为空判断
/// 
/// 示例：
/// - Name="social", Extension=""      → 目录 /social/
/// - Name="friends", Extension=""     → 目录 /social/friends/
/// - Name="list", Extension=".json"   → 文件 /social/friends/list.json
/// </summary>
[Serializable]
public class VFSNodeData : BaseNodeData
{
    // ==================== 基础信息 ====================
    
    /// <summary>
    /// 节点名称（如 "friends"）
    /// 用于路径的一段
    /// </summary>
    [Tooltip("节点名称")]
    public string Name;
    
    /// <summary>
    /// 扩展名（空=目录，".json"=文件）
    /// 类似 Linux 的设计：目录没有扩展名
    /// </summary>
    [Tooltip("扩展名（空=目录）")]
    public string Extension;
    
    // ==================== 路径信息 ====================
    
    /// <summary>
    /// 完整路径（如 "/social/friends/"）
    /// 由系统自动计算
    /// </summary>
    [Tooltip("完整路径")]
    public string FullPath;
    
    /// <summary>
    /// 父节点 ID（由系统自动维护）
    /// </summary>
    [Tooltip("父节点 ID")]
    public string ParentNodeID;
    
    // ==================== 数据（可选） ====================
    
    /// <summary>
    /// 数据内容（JSON 格式）
    /// 目录可为空，文件必须有数据
    /// </summary>
    [Tooltip("数据（JSON 格式）")]
    [TextArea(4, 8)]
    public string DataJson;
    
    // ==================== 元数据 ====================
    
    /// <summary>
    /// MIME 类型（可选，用于更细粒度的用途区分）
    /// 例如："application/json", "text/plain"
    /// </summary>
    [Tooltip("MIME 类型")]
    public string MimeType;
    
    /// <summary>
    /// 是否启用（被禁用的节点在查询时会被跳过）
    /// </summary>
    [Tooltip("是否启用")]
    public bool IsEnabled = true;
    
    /// <summary>
    /// 描述信息
    /// </summary>
    [Tooltip("描述")]
    [TextArea(2, 4)]
    public string Description;
    
    // ==================== 只读属性 ====================
    
    /// <summary>
    /// 是否是目录（根据 Extension 计算）
    /// Extension 为空 = 目录
    /// </summary>
    public bool IsDirectory => string.IsNullOrEmpty(Extension);
    
    /// <summary>
    /// 是否是文件（根据 Extension 计算）
    /// Extension 不为空 = 文件
    /// </summary>
    public bool IsFile => !string.IsNullOrEmpty(Extension);
    
    // ==================== 辅助方法 ====================
    
    /// <summary>
    /// 从另一个节点数据复制字段
    /// </summary>
    public void CopyFrom(VFSNodeData other)
    {
        if (other == null) return;
        base.CopyFrom(other);
        Name = other.Name;
        Extension = other.Extension;
        FullPath = other.FullPath;
        ParentNodeID = other.ParentNodeID;
        DataJson = other.DataJson;
        MimeType = other.MimeType;
        IsEnabled = other.IsEnabled;
        Description = other.Description;
    }
}
```

### 3.2 根节点复用

**直接复用原有的 `RootNodeData`** 作为 VFS 的根节点：

```csharp
// 原有的 RootNodeData（来自 ProcessFlowNodeData.cs）
public class RootNodeData : BaseNodeData
{
    [OutPort(0, "开始流程", NekoPortCapacity.Multi)]
    public List<string> _;
}

// VFS 使用方式：
// RootNodeData 作为入口点，OutputConnections 指向 VFSNodeData[]
```

**为什么不需要 `VFSRootNodeData`**：

1. `RootNodeData` 已经提供了根节点功能（一个输出端口）
2. VFS 的根节点只是一个特殊的目录（路径为 `/social/`）
3. 避免类型膨胀，遵循最小化设计原则

### 3.3 数据包设计

```csharp
/// <summary>
/// VFS 数据包
/// 
/// 继承自 BasePackData，复用：
/// - PackID
/// - DisplayName
/// - Description
/// - Author
/// - Version
/// - CreatedAt / ModifiedAt
/// - Nodes (List<BaseNodeData>)
/// </summary>
[Serializable]
public class VFSPackData : BasePackData
{
    /// <summary>
    /// 图类型（固定为 "VFS"）
    /// </summary>
    public string GraphType = "VFS";
    
    /// <summary>
    /// 根节点 ID 列表（可能有多个根，如 /social/、/config/）
    /// </summary>
    [Tooltip("根节点 ID 列表")]
    public List<string> RootNodeIds = new List<string>();
    
    /// <summary>
    /// 绑定的地图 ID（可选，用于关联特定关卡）
    /// </summary>
    [Tooltip("绑定的地图 ID")]
    public string BoundMapID;
    
    public VFSPackData()
    {
        DisplayName = "New VFS Pack";
        Description = "虚拟文件系统包";
    }
    
    /// <summary>
    /// 添加根节点
    /// </summary>
    public void AddRootNode(RootNodeData rootNode)
    {
        Nodes.Add(rootNode);
        RootNodeIds.Add(rootNode.NodeID);
        Touch();
    }
}
```

### 3.4 路径解析器

```csharp
/// <summary>
/// VFS 路径解析器（静态工具类）
/// 
/// 设计哲学：
/// - 类似 Linux 的路径处理
/// - 支持绝对路径和相对路径
/// - 支持 ..（上级目录）和 .（当前目录）
/// </summary>
public static class VFSPathResolver
{
    /// <summary>
    /// 规范化路径
    /// 
    /// 规则：
    /// - 确保以 / 开头和结尾
    /// - 替换多个连续的 / 为单个 /
    /// - 移除首尾空格
    /// 
    /// 示例：
    /// - "/social/friends" → "/social/friends/"
    /// - "social//friends" → "/social/friends/"
    /// - "  /social/  " → "/social/"
    /// </summary>
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path)) return "/";
        
        path = path.Trim();
        
        // 确保以/开头
        if (!path.StartsWith("/"))
            path = "/" + path;
        
        // 确保以/结尾
        if (!path.EndsWith("/"))
            path = path + "/";
        
        // 替换多个/为单个
        while (path.Contains("//"))
            path = path.Replace("//", "/");
        
        return path;
    }
    
    /// <summary>
    /// 合并路径（类似 Path.Combine）
    /// 
    /// 示例：
    /// - Combine("/social/", "friends/") → "/social/friends/"
    /// - Combine("/social/", "../") → "/"
    /// - Combine("/social/", "/config/") → "/config/"（绝对路径）
    /// </summary>
    public static string Combine(string basePath, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return Normalize(basePath);
        if (string.IsNullOrEmpty(basePath)) return Normalize(relativePath);
        
        // 绝对路径直接返回
        if (relativePath.StartsWith("/"))
            return Normalize(relativePath);
        
        // 处理特殊路径
        if (relativePath == ".") return Normalize(basePath);
        if (relativePath == "..") return GetParentPath(basePath);
        
        // 移除开头的 ./
        if (relativePath.StartsWith("./"))
            relativePath = relativePath.Substring(2);
        
        // 合并
        string result = basePath;
        if (!result.EndsWith("/"))
            result += "/";
        result += relativePath;
        
        return Normalize(result);
    }
    
    /// <summary>
    /// 获取父路径
    /// 
    /// 示例：
    /// - "/social/friends/" → "/social/"
    /// - "/social/" → "/"
    /// - "/" → "/"
    /// </summary>
    public static string GetParentPath(string path)
    {
        path = Normalize(path);
        if (path == "/") return "/";
        
        path = path.TrimEnd('/');
        int lastSlash = path.LastIndexOf('/');
        
        if (lastSlash <= 0) return "/";
        return path.Substring(0, lastSlash + 1);
    }
    
    /// <summary>
    /// 获取路径最后一段（文件名/目录名）
    /// 
    /// 示例：
    /// - "/social/friends/" → "friends"
    /// - "/social/" → "social"
    /// - "/" → ""
    /// </summary>
    public static string GetFileName(string path)
    {
        path = Normalize(path);
        if (path == "/") return "";
        
        path = path.TrimEnd('/');
        int lastSlash = path.LastIndexOf('/');
        
        if (lastSlash < 0 || lastSlash >= path.Length - 1)
            return path;
        
        return path.Substring(lastSlash + 1);
    }
    
    /// <summary>
    /// 分割路径段
    /// 
    /// 示例：
    /// - "/social/friends/" → ["social", "friends"]
    /// - "/" → []
    /// </summary>
    public static string[] SplitToSegments(string path)
    {
        path = Normalize(path);
        if (path == "/") return new string[0];
        
        path = path.Trim('/');
        if (string.IsNullOrEmpty(path)) return new string[0];
        
        return path.Split('/');
    }
    
    /// <summary>
    /// 从路径段构建路径
    /// 
    /// 示例：
    /// - FromSegments(["social", "friends"]) → "/social/friends/"
    /// - FromSegments([]) → "/"
    /// </summary>
    public static string FromSegments(IEnumerable<string> segments)
    {
        if (segments == null || !segments.Any()) return "/";
        return "/" + string.Join("/", segments.Where(s => !string.IsNullOrEmpty(s))) + "/";
    }
    
    /// <summary>
    /// 解析路径（支持相对路径）
    /// 
    /// 示例：
    /// - Resolve("/social/", "friends") → "/social/friends/"
    /// - Resolve("/social/friends/", "..") → "/social/"
    /// - Resolve("/social/", "/config/") → "/config/"
    /// </summary>
    public static string Resolve(string currentPath, string inputPath)
    {
        if (string.IsNullOrEmpty(inputPath)) return Normalize(currentPath);
        if (inputPath.StartsWith("/")) return Normalize(inputPath);
        return Combine(currentPath, inputPath);
    }
}
```

### 3.5 VFS 解释器

```csharp
/// <summary>
/// VFS 解释器（单例）
/// 
/// 职责：
/// 1. 管理 VFS 实例（类似 GraphRunner）
/// 2. 路径解析和节点查询
/// 3. 文件读写
/// 4. 树结构构建
/// 
/// 与 GraphRunner 的区别：
/// - GraphRunner: 动态信号流动，Update() 驱动
/// - VFSInterpreter: 静态查询，按需调用
/// </summary>
public class VFSInterpreter : SingletonMono<VFSInterpreter>
{
    /// <summary>
    /// VFS 实例字典：InstanceID → VFSInstance
    /// </summary>
    private Dictionary<string, VFSInstance> _instances;
    
    /// <summary>
    /// 默认实例 ID（单实例模式用）
    /// </summary>
    private string _defaultInstanceId = "default";
    
    protected override void Awake()
    {
        base.Awake();
        _instances = new Dictionary<string, VFSInstance>();
    }
    
    // ==================== 实例管理 ====================
    
    /// <summary>
    /// 注册 VFS 实例
    /// </summary>
    public void RegisterInstance(VFSInstance instance)
    {
        if (instance == null) return;
        if (_instances.ContainsKey(instance.InstanceID))
            UnregisterInstance(instance.InstanceID);
        
        _instances[instance.InstanceID] = instance;
        instance.IsLoaded = true;
    }
    
    /// <summary>
    /// 注销 VFS 实例
    /// </summary>
    public void UnregisterInstance(string instanceID)
    {
        if (_instances.TryGetValue(instanceID, out var instance))
        {
            instance.IsLoaded = false;
            instance.Clear();
            _instances.Remove(instanceID);
        }
    }
    
    /// <summary>
    /// 获取 VFS 实例
    /// </summary>
    public VFSInstance GetInstance(string instanceID)
    {
        _instances.TryGetValue(instanceID, out var instance);
        return instance;
    }
    
    /// <summary>
    /// 获取默认实例
    /// </summary>
    public VFSInstance GetDefaultInstance()
    {
        return GetInstance(_defaultInstanceId);
    }
    
    // ==================== 节点查询 ====================
    
    /// <summary>
    /// 根据路径获取节点
    /// </summary>
    public VFSNodeData GetNode(string path)
    {
        var instance = GetDefaultInstance();
        if (instance == null) return null;
        return instance.GetNodeByPath(path);
    }
    
    /// <summary>
    /// 列出目录的子节点
    /// </summary>
    public List<VFSNodeData> ListChildren(string path)
    {
        var instance = GetDefaultInstance();
        if (instance == null) return new List<VFSNodeData>();
        return instance.GetChildrenByPath(path);
    }
    
    /// <summary>
    /// 检查路径是否存在
    /// </summary>
    public bool PathExists(string path)
    {
        return GetNode(path) != null;
    }
    
    /// <summary>
    /// 检查是否是目录
    /// </summary>
    public bool IsDirectory(string path)
    {
        var node = GetNode(path);
        return node != null && node.IsDirectory;
    }
    
    /// <summary>
    /// 检查是否是文件
    /// </summary>
    public bool IsFile(string path)
    {
        var node = GetNode(path);
        return node != null && node.IsFile;
    }
    
    // ==================== 文件读写 ====================
    
    /// <summary>
    /// 读取文件内容
    /// </summary>
    public T ReadFile<T>(string path)
    {
        var node = GetNode(path);
        if (node == null || !node.IsFile)
        {
            Debug.LogWarning($"[VFSInterpreter] 路径不存在或不是文件：{path}");
            return default(T);
        }
        
        try
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(node.DataJson);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VFSInterpreter] 读取文件失败：{path}\n{e}");
            return default(T);
        }
    }
    
    /// <summary>
    /// 写入文件内容
    /// </summary>
    public void WriteFile<T>(string path, T value)
    {
        var node = GetNode(path);
        if (node == null || !node.IsFile)
        {
            Debug.LogWarning($"[VFSInterpreter] 路径不存在或不是文件：{path}");
            return;
        }
        
        try
        {
            node.DataJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VFSInterpreter] 写入文件失败：{path}\n{e}");
        }
    }
    
    // ==================== 树构建 ====================
    
    /// <summary>
    /// 根据连线关系构建树形结构
    /// 设置 ParentNodeID 和 FullPath
    /// </summary>
    public void BuildTreeStructure(VFSInstance instance)
    {
        if (instance == null) return;
        
        // 清空现有索引
        instance.PathIndex.Clear();
        
        // 从根节点开始 DFS 遍历
        foreach (var rootId in instance.RootNodeIds)
        {
            if (instance.NodeMap.TryGetValue(rootId, out var rootNode))
            {
                rootNode.ParentNodeID = null;
                rootNode.FullPath = "/";
                BuildSubTree(instance, rootNode, "/");
            }
        }
    }
    
    /// <summary>
    /// 递归构建子树
    /// </summary>
    private void BuildSubTree(VFSInstance instance, VFSNodeData parentNode, string parentPath)
    {
        foreach (var connection in parentNode.OutputConnections)
        {
            if (instance.NodeMap.TryGetValue(connection.TargetNodeID, out var childNode))
            {
                childNode.ParentNodeID = parentNode.NodeID;
                
                if (parentNode is RootNodeData)
                    childNode.FullPath = "/" + childNode.Name + "/";
                else
                    childNode.FullPath = parentPath + childNode.Name + "/";
                
                instance.PathIndex[childNode.FullPath] = childNode.NodeID;
                
                if (childNode.IsDirectory)
                    BuildSubTree(instance, childNode, childNode.FullPath);
            }
        }
    }
}

/// <summary>
/// VFS 运行时实例（只读文件树快照）
/// </summary>
public class VFSInstance
{
    public string InstanceID;
    public string GraphType;
    public string SourceJsonFileName;
    
    /// <summary>
    /// 节点字典：NodeID → VFSNodeData
    /// </summary>
    public Dictionary<string, VFSNodeData> NodeMap;
    
    /// <summary>
    /// 路径索引：FullPath → NodeID
    /// </summary>
    public Dictionary<string, string> PathIndex;
    
    /// <summary>
    /// 根节点 ID 列表
    /// </summary>
    public List<string> RootNodeIds;
    
    public bool IsLoaded;
    
    public VFSInstance(string instanceID, string graphType = "VFS", string sourceJsonFileName = null)
    {
        InstanceID = instanceID;
        GraphType = graphType;
        SourceJsonFileName = sourceJsonFileName;
        NodeMap = new Dictionary<string, VFSNodeData>();
        PathIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        RootNodeIds = new List<string>();
        IsLoaded = false;
    }
    
    /// <summary>
    /// 添加节点
    /// </summary>
    public void AddNode(VFSNodeData node)
    {
        if (node == null) return;
        if (NodeMap.ContainsKey(node.NodeID))
            NodeMap[node.NodeID] = node;
        else
            NodeMap[node.NodeID] = node;
        
        if (node is RootNodeData && !RootNodeIds.Contains(node.NodeID))
            RootNodeIds.Add(node.NodeID);
        
        if (!string.IsNullOrEmpty(node.FullPath))
            PathIndex[node.FullPath] = node.NodeID;
    }
    
    /// <summary>
    /// 根据路径获取节点
    /// </summary>
    public VFSNodeData GetNodeByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        path = VFSPathResolver.Normalize(path);
        if (PathIndex.TryGetValue(path, out var nodeID))
            return NodeMap.GetValueOrDefault(nodeID) as VFSNodeData;
        return null;
    }
    
    /// <summary>
    /// 获取子节点列表
    /// </summary>
    public List<VFSNodeData> GetChildren(string parentNodeID)
    {
        var children = new List<VFSNodeData>();
        foreach (var node in NodeMap.Values)
        {
            if (node.IsEnabled && node.ParentNodeID == parentNodeID)
                children.Add(node);
        }
        return children;
    }
    
    /// <summary>
    /// 根据路径获取子节点
    /// </summary>
    public List<VFSNodeData> GetChildrenByPath(string path)
    {
        var parentNode = GetNodeByPath(path);
        if (parentNode == null) return new List<VFSNodeData>();
        return GetChildren(parentNode.NodeID);
    }
    
    /// <summary>
    /// 清空
    /// </summary>
    public void Clear()
    {
        NodeMap.Clear();
        PathIndex.Clear();
        RootNodeIds.Clear();
        IsLoaded = false;
    }
}
```

---

## 四、GraphAnalyser 通用分析器

### 4.1 设计哲学

**GraphAnalyser** 是一个**通用静态图分析器**，用于分析任何树状/图状数据结构。

与 **GraphRunner** 的区别：

| 特性 | GraphRunner | GraphAnalyser |
|------|-------------|---------------|
| **用途** | 动态流程执行 | 静态结构分析 |
| **驱动方式** | `Update()` 每帧驱动 | 按需调用 |
| **核心概念** | Signal、Strategy | Tree、Index |
| **状态** | 活跃信号队列 | 树结构快照 |
| **典型应用** | Mission、Story | VFS、Config、SkillTree |

### 4.2 通用性设计

GraphAnalyser 设计为**泛型静态类**，可分析任何继承 `BaseNodeData` 的节点类型：

```csharp
/// <summary>
/// 静态图分析器（通用）
/// 
/// 设计哲学：
/// - 泛型方法，支持任何 BaseNodeData 子类
/// - 无状态，纯工具函数
/// - 与具体业务解耦
/// 
/// 适用场景：
/// - VFS: 构建文件树、路径索引
/// - Config: 构建配置树、键值索引
/// - SkillTree: 构建技能树、前置依赖分析
/// - Dialogue: 构建对话树、分支索引
/// </summary>
public static class GraphAnalyser
{
    // ==================== 树构建 ====================
    
    /// <summary>
    /// 构建树形结构（设置 ParentNodeID 和 FullPath）
    /// 
    /// 泛型设计：
    /// - T: 必须是 BaseNodeData 子类
    /// - 要求节点有 Name 字段（或类似字段）
    /// 
    /// 示例：
    /// - VFSNodeData: FullPath = "/social/friends/"
    /// - ConfigNodeData: FullPath = "/game/settings/"
    /// </summary>
    public static void BuildTreeStructure<T>(
        Dictionary<string, T> nodeMap,
        List<string> rootNodeIds,
        Func<T, string> getName,          // 获取节点名称
        Func<T, string> getExtension,     // 获取扩展名（可选，VFS 专用）
        string rootPath = "/") where T : BaseNodeData
    {
        // 清空现有 ParentNodeID
        foreach (var node in nodeMap.Values)
        {
            node.ParentNodeID = null;
        }
        
        // 从根节点开始 DFS 遍历
        foreach (var rootId in rootNodeIds)
        {
            if (nodeMap.TryGetValue(rootId, out var rootNode))
            {
                BuildSubTree(nodeMap, rootNode, rootPath, getName, getExtension);
            }
        }
    }
    
    /// <summary>
    /// 递归构建子树
    /// </summary>
    private static void BuildSubTree<T>(
        Dictionary<string, T> nodeMap,
        T parentNode,
        string parentPath,
        Func<T, string> getName,
        Func<T, string> getExtension) where T : BaseNodeData
    {
        foreach (var connection in parentNode.OutputConnections)
        {
            if (nodeMap.TryGetValue(connection.TargetNodeID, out var childNode))
            {
                childNode.ParentNodeID = parentNode.NodeID;
                
                // 计算 FullPath
                string name = getName(childNode);
                string ext = getExtension?.Invoke(childNode) ?? "";
                
                if (parentNode.NodeID == parentNode.NodeID) // 根节点的直接子节点
                    childNode.FullPath = "/" + name + ext + "/";
                else
                    childNode.FullPath = parentPath + name + ext + "/";
                
                // 递归处理子节点（如果是目录类型）
                if (string.IsNullOrEmpty(ext)) // 空扩展名 = 目录
                    BuildSubTree(nodeMap, childNode, childNode.FullPath, getName, getExtension);
            }
        }
    }
    
    // ==================== 索引构建 ====================
    
    /// <summary>
    /// 建立路径索引（FullPath → NodeID）
    /// 
    /// 用于 O(1) 复杂度的路径查询
    /// </summary>
    public static Dictionary<string, string> BuildPathIndex<T>(
        Dictionary<string, T> nodeMap) where T : BaseNodeData
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var node in nodeMap.Values)
        {
            if (!string.IsNullOrEmpty(node.FullPath))
                index[node.FullPath] = node.NodeID;
        }
        
        return index;
    }
    
    /// <summary>
    /// 建立名称索引（Name → NodeID）
    /// 
    /// 用于按名称快速查找
    /// </summary>
    public static Dictionary<string, List<string>> BuildNameIndex<T>(
        Dictionary<string, T> nodeMap,
        Func<T, string> getName) where T : BaseNodeData
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var kvp in nodeMap)
        {
            string name = getName(kvp.Value);
            if (!index.TryGetValue(name, out var list))
            {
                list = new List<string>();
                index[name] = list;
            }
            list.Add(kvp.Key);
        }
        
        return index;
    }
    
    // ==================== 查询方法 ====================
    
    /// <summary>
    /// 获取子节点列表
    /// 
    /// 根据 ParentNodeID 过滤
    /// </summary>
    public static List<T> GetChildren<T>(
        Dictionary<string, T> nodeMap,
        string parentNodeID) where T : BaseNodeData
    {
        var children = new List<T>();
        foreach (var node in nodeMap.Values)
        {
            if (node.ParentNodeID == parentNodeID)
                children.Add(node);
        }
        return children;
    }
    
    /// <summary>
    /// 根据路径获取节点
    /// 
    /// 使用路径索引进行 O(1) 查询
    /// </summary>
    public static T GetNodeByPath<T>(
        Dictionary<string, T> nodeMap,
        Dictionary<string, string> pathIndex,
        string path) where T : BaseNodeData
    {
        if (pathIndex.TryGetValue(path, out var nodeID))
            return nodeMap.GetValueOrDefault(nodeID) as T;
        return null;
    }
    
    // ==================== 验证方法 ====================
    
    /// <summary>
    /// 验证树结构是否有效
    /// 
    /// 检查：
    /// - 是否有循环依赖
    /// - 是否有孤立节点
    /// - 路径是否唯一
    /// </summary>
    public static bool ValidateTree<T>(
        Dictionary<string, T> nodeMap,
        List<string> rootNodeIds,
        out List<string> errors) where T : BaseNodeData
    {
        errors = new List<string>();
        
        // 检查根节点是否存在
        foreach (var rootId in rootNodeIds)
        {
            if (!nodeMap.ContainsKey(rootId))
                errors.Add($"根节点不存在：{rootId}");
        }
        
        // 检查是否有孤立节点（没有父节点，也不是根节点）
        foreach (var kvp in nodeMap)
        {
            var node = kvp.Value;
            bool isRoot = rootNodeIds.Contains(node.NodeID);
            bool hasParent = !string.IsNullOrEmpty(node.ParentNodeID) && 
                             nodeMap.ContainsKey(node.ParentNodeID);
            
            if (!isRoot && !hasParent)
                errors.Add($"孤立节点：{node.NodeID}");
        }
        
        // 检查路径是否唯一
        var pathSet = new HashSet<string>();
        foreach (var node in nodeMap.Values)
        {
            if (!string.IsNullOrEmpty(node.FullPath))
            {
                if (!pathSet.Add(node.FullPath))
                    errors.Add($"路径重复：{node.FullPath}");
            }
        }
        
        return errors.Count == 0;
    }
}
```

### 4.3 扩展性设计

GraphAnalyser 的泛型设计允许轻松扩展到新的图类型：

```csharp
// 示例：Config 树分析
public class ConfigNodeData : BaseNodeData
{
    public string Key;        // 配置键
    public string Value;      // 配置值
    public string Type;       // 配置类型
}

// 使用 GraphAnalyser
var nodeMap = new Dictionary<string, ConfigNodeData>();
var rootNodeIds = new List<string> { "root_config" };

GraphAnalyser.BuildTreeStructure(
    nodeMap,
    rootNodeIds,
    node => node.Key,      // 获取名称
    node => null,          // Config 没有扩展名
    "/config/"
);

var pathIndex = GraphAnalyser.BuildPathIndex(nodeMap);
var configNode = GraphAnalyser.GetNodeByPath(nodeMap, pathIndex, "/config/game/");
```

```csharp
// 示例：SkillTree 分析
public class SkillNodeData : BaseNodeData
{
    public string SkillName;
    public List<string> Prerequisites;  // 前置技能
}

// 使用 GraphAnalyser
GraphAnalyser.BuildTreeStructure(
    nodeMap,
    rootNodeIds,
    node => node.SkillName,
    node => null,
    "/skills/"
);

// 分析前置依赖
var skillsWithoutPrereqs = nodeMap.Values
    .Where(s => s.Prerequisites.Count == 0)
    .ToList();
```

### 4.4 VFS 专用扩展

对于 VFS，可以添加专用的扩展方法：

```csharp
/// <summary>
/// VFS 专用扩展方法
/// </summary>
public static class VFSAnalyserExtensions
{
    /// <summary>
    /// 构建 VFS 树结构
    /// </summary>
    public static void BuildVFSTree<T>(
        this GraphAnalyser analyser,
        Dictionary<string, T> nodeMap,
        List<string> rootNodeIds) where T : BaseNodeData
    {
        GraphAnalyser.BuildTreeStructure(
            nodeMap,
            rootNodeIds,
            node => GetNodeName(node),      // 获取节点名称
            node => GetNodeExtension(node), // 获取扩展名
            "/"
        );
    }
    
    private static string GetNodeName<T>(T node) where T : BaseNodeData
    {
        // 根据实际节点类型获取名称
        if (node is VFSNodeData vfsNode)
            return vfsNode.Name;
        return node.NodeID;
    }
    
    private static string GetNodeExtension<T>(T node) where T : BaseNodeData
    {
        if (node is VFSNodeData vfsNode)
            return vfsNode.Extension;
        return "";
    }
}
```

---

## 五、SocialCLI 集成

### 5.1 SocialCLI 修改

```csharp
public class SocialCLI : DeveloperConsole
{
    /// <summary>
    /// 当前目录节点（从 VFS 解释器获取）
    /// </summary>
    public VFSNodeData CurrentNode;
    
    /// <summary>
    /// 当前路径（从 CurrentNode 计算，只读）
    /// </summary>
    public string CurrentPath => CurrentNode?.FullPath ?? "/social/";
    
    /// <summary>
    /// 初始化 VFS
    /// </summary>
    private void InitializeVFS()
    {
        // 从 Resources 加载 VFS Pack
        var pack = VFSLoader.LoadPackFromResources("NekoGraph/GraphVSF/Packs/social_tree");
        if (pack == null)
        {
            Debug.LogWarning("[SocialCLI] 找不到 VFS Pack，创建默认社交文件树");
            pack = CreateDefaultSocialTree();
        }
        
        // 加载为 VFS 实例
        var instance = VFSLoader.LoadFromPack(pack, "SocialCLI_Default", "VFS");
        if (instance != null)
        {
            // 注册到 VFS 解释器
            var interpreter = VFSInterpreter.Instance;
            if (interpreter != null)
            {
                interpreter.RegisterInstance(instance);
                interpreter.SetDefaultInstanceId("SocialCLI_Default");
                
                // 设置当前节点为根节点
                if (instance.RootNodeIds.Count > 0)
                {
                    CurrentNode = instance.GetNode<VFSNodeData>(instance.RootNodeIds[0]);
                }
                
                Debug.Log($"[SocialCLI] VFS 初始化完成，当前路径：{CurrentPath}");
            }
        }
    }
    
    /// <summary>
    /// 创建默认的社交文件树
    /// </summary>
    private VFSPackData CreateDefaultSocialTree()
    {
        var pack = new VFSPackData
        {
            PackID = "social_tree_default",
            DisplayName = "社交文件树（默认）",
            Description = "SocialCLI 默认文件树结构"
        };
        
        // 创建根节点 /social/
        var rootNode = new RootNodeData
        {
            NodeID = "root_social",
            OutputConnections = new List<ConnectionData>()
        };
        pack.AddRootNode(rootNode);
        
        // 创建子目录
        CreateFolderNode("friends", "好友列表", rootNode, pack);
        CreateFolderNode("requests", "好友请求", rootNode, pack);
        CreateFolderNode("blocks", "黑名单", rootNode, pack);
        CreateFolderNode("groups", "群组", rootNode, pack);
        CreateFolderNode("messages", "消息历史", rootNode, pack);
        
        return pack;
    }
    
    /// <summary>
    /// 创建目录节点
    /// </summary>
    private VFSNodeData CreateFolderNode(string name, string desc, RootNodeData parent, VFSPackData pack)
    {
        var folder = new VFSNodeData
        {
            NodeID = "folder_" + name,
            Name = name,
            Extension = "",  // 空扩展名 = 目录
            Description = desc
        };
        
        parent.OutputConnections.Add(new ConnectionData(0, folder.NodeID, 0));
        pack.Nodes.Add(folder);
        
        return folder;
    }
}
```

### 5.2 cd 命令重构

```csharp
[CommandInfo("cd", "📂 切换目录", "Social", new[] { "path" },
    Tooltip = "切换当前目录\n示例：cd friends")]
[SocialCommand]
public static CommandOutput Cd(DeveloperConsole console, string[] args, object payload)
{
    var scli = console as SocialCLI;
    if (scli == null)
        return CommandOutput.Fail("此命令只能在社交终端执行");
    
    var interpreter = VFSInterpreter.Instance;
    if (interpreter == null)
        return CommandOutput.Fail("VFS 未初始化");
    
    // 无参数时返回根目录
    if (args.Length < 1)
    {
        scli.CurrentNode = interpreter.GetNode("/social/");
        return CommandOutput.Success($"当前目录：{scli.CurrentPath}");
    }
    
    string path = args[0];
    
    // 处理特殊路径
    if (path == "..")
    {
        // 返回上级目录
        if (scli.CurrentNode != null && !string.IsNullOrEmpty(scli.CurrentNode.ParentNodeID))
        {
            scli.CurrentNode = interpreter.GetNode(scli.CurrentNode.ParentNodeID);
        }
        else
        {
            scli.CurrentNode = interpreter.GetNode("/social/");
        }
    }
    else if (path == "/" || path == "/social/")
    {
        // 返回根目录
        scli.CurrentNode = interpreter.GetNode("/social/");
    }
    else
    {
        // 解析相对路径或绝对路径
        string targetPath = path.StartsWith("/")
            ? VFSPathResolver.Normalize(path)
            : VFSPathResolver.Resolve(scli.CurrentPath, path);
        
        var targetNode = interpreter.GetNode(targetPath);
        if (targetNode == null)
            return CommandOutput.Fail($"路径不存在：{targetPath}");
        
        if (!targetNode.IsDirectory)
            return CommandOutput.Fail($"不是目录：{targetPath}");
        
        scli.CurrentNode = targetNode;
    }
    
    return CommandOutput.Success($"当前目录：{scli.CurrentPath}");
}
```

### 5.3 ls 命令重构

```csharp
[CommandInfo("ls", "📋 列出目录", "Social", new[] { "path" },
    Tooltip = "列出目录内容\n示例：ls /social/friends/")]
[SocialCommand]
public static CommandOutput List(DeveloperConsole console, string[] args, object payload)
{
    var scli = console as SocialCLI;
    if (scli == null)
        return CommandOutput.Fail("此命令只能在社交终端执行");
    
    var interpreter = VFSInterpreter.Instance;
    if (interpreter == null)
        return CommandOutput.Fail("VFS 未初始化");
    
    string path = args.Length > 0 ? args[0] : scli.CurrentPath;
    
    var targetNode = interpreter.GetNode(path);
    if (targetNode == null)
        return CommandOutput.Fail($"路径不存在：{path}");
    
    if (!targetNode.IsDirectory)
        return CommandOutput.Fail($"不是目录：{path}");
    
    var children = interpreter.ListChildren(path);
    
    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"目录：{targetNode.FullPath}");
    
    if (children.Count == 0)
    {
        sb.AppendLine("  (空)");
    }
    else
    {
        foreach (var child in children)
        {
            if (!child.IsEnabled) continue;
            
            string icon = child.IsDirectory ? "[DIR]" : "[FILE]";
            string name = child.Name + (child.IsDirectory ? "/" : "");
            sb.AppendLine($"  {icon} {name}");
        }
    }
    
    return CommandOutput.Success(sb.ToString());
}
```

### 5.4 pwd 命令

```csharp
[CommandInfo("pwd", "📍 显示当前路径", "Social", null,
    Tooltip = "显示当前目录路径")]
[SocialCommand]
public static CommandOutput Pwd(DeveloperConsole console, string[] args, object payload)
{
    var scli = console as SocialCLI;
    if (scli == null)
        return CommandOutput.Fail("此命令只能在社交终端执行");
    
    return CommandOutput.Success($"当前目录：{scli.CurrentPath}");
}
```

---

## 六、编辑器工具

### 6.1 VFSGraphView

```csharp
#if UNITY_EDITOR
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using NekoGraph;

/// <summary>
/// VFS 画布
/// 
/// 继承自 BaseGraphView<VFSPackData>
/// 处理节点连线验证、树形结构可视化
/// </summary>
[GraphViewType(NodeSystem.Common)]
public class VFSGraphView : BaseGraphView<VFSPackData>
{
    /// <summary>
    /// 验证连接规则：
    /// - RootNodeData 可以连接 VFSNodeData
    /// - VFSNodeData（目录）可以连接 VFSNodeData
    /// - VFSNodeData（文件）不能有输出连接
    /// </summary>
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var compatiblePorts = new List<Port>();
        
        foreach (var port in ports.ToList())
        {
            // 跳过自己
            if (port.node == startPort.node) continue;
            // 跳过同方向
            if (port.direction == startPort.direction) continue;
            
            // VFS 特定验证
            if (!ValidateConnection(startPort, port)) continue;
            
            compatiblePorts.Add(port);
        }
        
        return compatiblePorts;
    }
    
    private bool ValidateConnection(Port outputPort, Port inputPort)
    {
        var outputNode = outputPort.node as VFSNodeView;
        var inputNode = inputPort.node as VFSNodeView;
        
        // 文件节点不能有输出连接
        if (outputNode?.Data?.IsFile == true)
        {
            Debug.LogWarning("文件节点不能有输出连接");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 节点添加回调 - 更新标题
    /// </summary>
    protected override void OnNodeAddedGeneric(BaseNode node)
    {
        base.OnNodeAddedGeneric(node);
        
        if (node is VFSNodeView vfsNode)
        {
            UpdateNodeTitle(vfsNode);
        }
    }
    
    private void UpdateNodeTitle(VFSNodeView node)
    {
        if (node.Data.IsDirectory)
            node.title = $"📂 {node.Data.Name}";
        else
            node.title = $"📄 {node.Data.Name}{node.Data.Extension}";
    }
}
#endif
```

### 6.2 VFSGraphWindow

```csharp
#if UNITY_EDITOR
using UnityEditor;
using NekoGraph;

/// <summary>
/// VFS 编辑器窗口
/// 
/// 继承自 BaseGraphWindow<VFSGraphView, VFSPackData>
/// 提供保存/加载、工具栏等功能
/// </summary>
public class VFSGraphWindow : BaseGraphWindow<VFSGraphView, VFSPackData>
{
    protected override string WindowTitle => "VFS Editor";
    protected override string DefaultFileName => "New_VFS.json";
    protected override string FileExtension => "json";
    protected override string FileDirectory => "Assets/Resources/NekoGraph/GraphVSF/Packs";
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Common;
    
    /// <summary>
    /// 创建搜索窗口
    /// </summary>
    protected override ScriptableObject CreateSearchWindow()
    {
        var searchWindow = ScriptableObject.CreateInstance<VFSNodeSearchWindow>();
        searchWindow.Initialize(this, GraphView);
        return searchWindow;
    }
    
    /// <summary>
    /// 菜单项：打开编辑器
    /// </summary>
    [MenuItem("GraphVSF/VFS Editor")]
    public static void OpenWindow()
    {
        var window = GetWindow<VFSGraphWindow>();
        window.titleContent = new GUIContent("VFS Editor");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }
}
#endif
```

### 6.3 VFSNodeSearchWindow

```csharp
#if UNITY_EDITOR
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using NekoGraph;
using System.Collections.Generic;

/// <summary>
/// VFS 节点搜索窗口
/// 
/// 继承自 BaseNodeSearchWindow
/// 提供节点创建菜单
/// </summary>
public class VFSNodeSearchWindow : BaseNodeSearchWindow
{
    protected override NodeSystem CurrentNodeSystem => NodeSystem.Common;
    
    public override List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        var tree = new List<SearchTreeEntry>
        {
            new SearchTreeGroupEntry(new GUIContent("创建 VFS 节点"), 0),
            
            new SearchTreeGroupEntry(new GUIContent("📁 根节点"), 1),
            new SearchTreeEntry(new GUIContent("   Root 节点（复用原有）"))
            {
                level = 2,
                userData = typeof(RootNodeData)
            },
            
            new SearchTreeGroupEntry(new GUIContent("📂 目录节点"), 1),
            new SearchTreeEntry(new GUIContent("   VFS 节点（Extension 为空）"))
            {
                level = 2,
                userData = typeof(VFSNodeData)
            },
        };
        
        return tree;
    }
}
#endif
```

### 6.4 VFSNodeView

```csharp
#if UNITY_EDITOR
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// VFS 节点视图
/// 
/// 继承自 BaseNode<VFSNodeData>
/// 提供节点 UI 编辑功能
/// </summary>
[NodeMenuItem("VFS/VFS 节点", typeof(VFSNodeData))]
[NodeType(NodeSystem.Common)]
public class VFSNodeView : BaseNode<VFSNodeData>
{
    private TextField _nameField;
    private TextField _extensionField;
    private TextField _descriptionField;
    private Toggle _enabledToggle;
    
    public VFSNodeView() : base() { InitializeUI(); }
    public VFSNodeView(VFSNodeData data) : base(data) { InitializeUI(); }
    
    private void InitializeUI()
    {
        UpdateTitle();
        style.width = 250;
        
        var foldout = new Foldout() { text = "节点配置", value = true };
        
        // 名称
        _nameField = new TextField("名称");
        _nameField.value = TypedData.Name;
        _nameField.RegisterValueChangedCallback(evt =>
        {
            TypedData.Name = evt.newValue;
            UpdateTitle();
        });
        foldout.Add(_nameField);
        
        // 扩展名（空=目录）
        _extensionField = new TextField("扩展名");
        _extensionField.value = TypedData.Extension;
        _extensionField.RegisterValueChangedCallback(evt =>
        {
            TypedData.Extension = evt.newValue;
            UpdateTitle();
        });
        foldout.Add(_extensionField);
        
        // 描述
        _descriptionField = new TextField("描述") { multiline = true, rows = 2 };
        _descriptionField.value = TypedData.Description;
        _descriptionField.RegisterValueChangedCallback(evt =>
            TypedData.Description = evt.newValue);
        foldout.Add(_descriptionField);
        
        // 启用状态
        _enabledToggle = new Toggle("已启用");
        _enabledToggle.value = TypedData.IsEnabled;
        _enabledToggle.RegisterValueChangedCallback(evt =>
            TypedData.IsEnabled = evt.newValue);
        foldout.Add(_enabledToggle);
        
        extensionContainer.Add(foldout);
        RefreshExpandedState();
    }
    
    private void UpdateTitle()
    {
        if (TypedData.IsDirectory)
            title = $"📂 {TypedData.Name}";
        else
            title = $"📄 {TypedData.Name}{TypedData.Extension}";
    }
    
    public override void UpdateData()
    {
        TypedData.Name = _nameField.value;
        TypedData.Extension = _extensionField.value;
        TypedData.Description = _descriptionField.value;
        TypedData.IsEnabled = _enabledToggle.value;
    }
}
#endif
```

---

## 七、实施计划

### Phase 1: 清理冗余代码

- [ ] 删除 `VSFRootNodeData`、`VSFFolderNodeData`、`VSFFileNodeData`
- [ ] 删除 `VSFNodeType` 枚举
- [ ] 将 `VFSNodeData` 改为非抽象类
- [ ] 简化 `VFSNodeData` 字段（使用 `Name` + `Extension` 替代）
- [ ] 更新 `VSFPackData` 继承 `BasePackData`

### Phase 2: 路径解析器

- [ ] 创建 `VFSPathResolver` 静态工具类
- [ ] 实现 `Normalize`、`Combine`、`GetParentPath` 等方法
- [ ] 实现 `Resolve`、`SplitToSegments`、`FromSegments` 等方法
- [ ] 编写单元测试

### Phase 3: VFS 解释器

- [ ] 创建 `VFSInterpreter` 单例类
- [ ] 创建 `VFSInstance` 类
- [ ] 实现 `GetNode`、`ListChildren` 方法
- [ ] 实现 `ReadFile<T>`、`WriteFile<T>` 方法
- [ ] 实现 `BuildTreeStructure` 方法

### Phase 4: SocialCLI 集成

- [ ] 修改 `SocialCLI.CurrentPath` 为 `CurrentNode`
- [ ] 添加 `InitializeVFS()` 方法
- [ ] 重构 `cd` 命令使用 `VFSInterpreter`
- [ ] 重构 `ls` 命令使用 `VFSInterpreter.ListChildren`
- [ ] 创建默认的 `social_tree.json` 测试数据

### Phase 5: 编辑器工具

- [ ] 创建 `VFSGraphView`
- [ ] 创建 `VFSGraphWindow`
- [ ] 创建 `VFSNodeSearchWindow`
- [ ] 创建 `VFSNodeView`
- [ ] 添加 `[NodeMenuItem]` 和 `[NodeType]` 标签
- [ ] 测试编辑器功能

---

## 八、总结

### 8.1 架构优势

1. **统一节点类型** - 只用 `VFSNodeData` 一种类型，通过 `Extension` 区分用途
2. **复用原有设施** - 使用 `RootNodeData`、`BasePackData`、`BaseNodeData`
3. **清晰的职责分离** - Runtime（`Runtime/GraphVSF/`）vs Editor（`Editor/GraphVSF/`）
4. **Unix 风格路径** - 使用扩展名区分文件/目录，而不是类型枚举

### 8.2 设计哲学

1. **一切皆文件** - 向 Linux 学习，目录也是文件的一种
2. **最小化设计** - 一种类型能解决的，不要用三种
3. **复用优先** - 不要重复定义已有的类型
4. **遵循规范** - Editor 代码放 `Editor/`，Runtime 代码放 `Runtime/`

### 8.3 教训

1. **先理解现有架构** - 不要重复造轮子（如 `NodeMenuItemAttribute`、`NodeSystem`）
2. **不要过度设计** - `Root/Folder/File` 三种类型是典型的过度设计
3. **学习 Unix 哲学** - 用扩展名区分用途，而不是类型枚举

---

**文档结束**
