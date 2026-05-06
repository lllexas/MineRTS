# NekoGraph 编辑器架构分析文档

**最后更新**: 2026 年 3 月 11 日
**架构版本**: 4.0 (公共流程节点重构版)
**分析对象**: BaseGraphView, MissionGraphView, StoryGraphView, BaseGraphWindow, BaseNodeSearchWindow

---

## 📊 架构总览

```
┌─────────────────────────────────────────────────────────────────┐
│              BaseGraphWindow<TView, TPack>                      │
│  - EditorWindow 基类                                            │
│  - 工具栏生成 (保存/读取)                                        │
│  - 依赖：NodeTypeHelper (反射缓存)                               │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ 继承
         ┌────────────────────┴────────────────────┐
         │                                         │
┌─────────────────────┐               ┌─────────────────────────┐
│  MissionGraphWindow │               │   StoryGraphWindow      │
│  - Mission 系统专用  │               │  - Story 系统专用       │
│  - CurrentNodeSystem = Mission     │  - CurrentNodeSystem = Story    │
└─────────────────────┘               └─────────────────────────┘
         │                                         │
         │ 创建                                    │ 创建
         ▼                                         ▼
┌─────────────────────┐               ┌─────────────────────────┐
│ MissionNodeSearchWindow │           │ StoryNodeSearchWindow   │
│ - BaseNodeSearchWindow    │         │ - BaseNodeSearchWindow  │
│ - CurrentNodeSystem = Mission   │   │ - CurrentNodeSystem = Story   │
└──────────────┬──────────────┘               └──────────────┬──────────────┘
               │                                             │
               │ Initialize(editorWindow, graphView)         │
               └──────────────────────┬──────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────┐
│              INekoGraphNodeFactory (接口)                       │
│  - CreateNode(Type, Vector2)                                    │
│  - ConvertScreenToLocal(Vector2, EditorWindow)                  │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ 实现
                              │
┌─────────────────────────────────────────────────────────────────┐
│        BaseGraphView<TPack> : GraphView                         │
│  - 通用 GraphView 基类                                          │
│  - 提供画布基础功能：Zoom, Drag, Selection                      │
│  - 【彻底拥抱混沌】无 PortType 校验、无连接规则限制              │
│  - GetCompatiblePorts 只检查：不是同一节点 + 方向相反            │
│  - 提供 Copy/Paste 基础实现                                     │
│  - 提供连线恢复通用逻辑 (RestoreConnectionsHelper)              │
│  - 实现节点工厂方法 CreateNode(Type, Vector2)                   │
│  - 【中央情报局】NodeMap 统一管理所有节点生死喵~                 │
│  - 【连线自动捕获】CollectConnections 捕获所有连接不校验         │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ 继承
         ┌────────────────────┴────────────────────┐
         │                                         │
┌─────────────────────┐               ┌─────────────────────────┐
│   MissionGraphView  │               │    StoryGraphView       │
│  - Mission 系统专用  │               │  - Story 系统专用       │
│  - 0 条连接规则 ✅    │               │  - 0 条连接规则 ✅       │
│  - 8 种节点类型      │               │  - 6 种节点类型         │
│  - SetCurrentMapId 联动│              │  - Sequences 支持       │
└─────────────────────┘               └─────────────────────────┘
```

---

## 🏗️ 核心架构组件

### 1. 节点工厂接口 (`INekoGraphNodeFactory`)

```csharp
public interface INekoGraphNodeFactory
{
    BaseNode CreateNode(Type nodeType, Vector2 position, BaseNodeData data = null);
    Vector2 ConvertScreenToLocal(Vector2 screenPosition, EditorWindow window);
}
```

**设计目的**: 解耦 SearchWindow 和 GraphView 的依赖，面向接口编程，避免泛型约束问题喵~

---

### 2. 节点类型辅助类 (`NodeTypeHelper`)

**命名空间**: `NekoGraph.Editor`

**核心方法**:
- `GetNodeTypesForSystem(NodeSystem system)`: 获取指定系统可用的节点类型列表（带缓存）
- `ClearCache()`: 清除所有缓存
- `ClearCache(NodeSystem system)`: 清除指定系统的缓存
- `TryGetNodeType(Type nodeType, out NodeTypeInfo info)`: 尝试获取节点类型信息

**NodeTypeInfo 结构**:
```csharp
public class NodeTypeInfo
{
    public Type NodeType;                    // 节点类型
    public NodeMenuItemAttribute MenuItemAttr;  // 菜单项标签
    public NodeTypeAttribute TypeAttr;          // 系统类型标签
    public string MenuPath;                  // 完整菜单路径
    public string[] PathParts;               // 菜单路径分割
}
```

---

### 3. 通用搜索窗口基类 (`BaseNodeSearchWindow`)

**特点**:
- ✅ 无泛型约束，彻底解耦
- ✅ 依赖 `INekoGraphNodeFactory` 接口
- ✅ 通过 `NodeTypeHelper` 反射生成菜单树
- ✅ 自动坐标转换

**核心方法**:
```csharp
public abstract class BaseNodeSearchWindow : ScriptableObject, ISearchWindowProvider
{
    public INekoGraphNodeFactory GraphView;
    public EditorWindow EditorWindow;
    protected abstract NodeSystem CurrentNodeSystem { get; }

    public void Initialize(EditorWindow editorWindow, INekoGraphNodeFactory graphView);
    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context);
    public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context);
}
```

---

### 4. 通用窗口基类 (`BaseGraphWindow<TView, TPack>`)

**特点**:
- ✅ 继承自 `EditorWindow`
- ✅ 自动生成工具栏（保存/读取）
- ✅ 通过 `NodeTypeHelper` 获取节点类型
- ✅ 支持子类扩展全局功能

**关键属性**:
```csharp
protected virtual NodeSystem CurrentNodeSystem => NodeSystem.Common;
```

---

### 5. 通用 GraphView 基类 (`BaseGraphView<TPack>`)

**特点**:
- ✅ 继承自 `GraphView`
- ✅ 实现 `INekoGraphNodeFactory` 接口
- ✅【彻底拥抱混沌】删除所有 PortType 校验和连接规则
- ✅ `GetCompatiblePorts` 只检查：不是同一节点 + 方向相反
- ✅ 提供通用连线恢复逻辑
- ✅【中央情报局】`NodeMap` 统一管理画布上所有节点
- ✅【连线自动捕获·混沌版】`CollectConnections` 捕获所有连接不校验

**核心字段**:
```csharp
protected Dictionary<string, BaseNode> NodeMap = new Dictionary<string, BaseNode>();
```

---

## 📋 详细方法分配对比表

### 图例说明
| 符号 | 含义 |
|------|------|
| ✅ | 本类实现 |
| 🟡 | 虚方法/空实现 |
| ⚪ | 继承基类 |
| 🔴 | 重写 override |
| 🆕 | 自有方法 |
| 📦 | 静态方法 |

---

### 1. 构造函数与初始化

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | BaseGraphWindow | BaseNodeSearchWindow |
|--------|---------------|------------------|----------------|-----------------|---------------------|
| `.ctor()` | ✅ 设置 Zoom/Drag/Selection<br>✅ 添加背景网格<br>✅ 注册序列化回调 | ✅ 调用基类构造函数 | ✅ 调用基类构造函数 | ⚪ 继承 EditorWindow | ⚪ 继承 ScriptableObject |
| `OnEnable()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | ✅ 调用 `ConstructGraphView()`<br>✅ 调用 `GenerateToolbar()` | ⚪ 无 |
| `OnDisable()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | ✅ 清理 GraphView | ⚪ 无 |
| `ConstructGraphView()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | ✅ 创建 SearchWindow<br>✅ 创建 GraphView<br>✅ 调用 `SetupSearchWindow()` | ⚪ 无 |
| `GetGraphViewName()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | 🟡 `virtual` - 返回 "NekoGraph" | ⚪ 无 |
| `CreateSearchWindow()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | 🟡 `virtual` - 返回 null | ⚪ 无 |
| `SetupSearchWindow()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | ✅ 设置 `nodeCreationRequest` 回调 | ⚪ 无 |
| `OnGraphViewConstructed()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | 🟡 `virtual` - 空实现 | ⚪ 无 |
| `Initialize()` | ⚪ 无 | ⚪ 无 | ⚪ 无 | ⚪ 无 | ✅ 设置 GraphView 和 EditorWindow 引用 |

---

### 2. 连接管理（彻底拥抱混沌版）

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `GetCompatiblePorts()` | ✅ `override` - 【混沌版】<br>只检查：1. 不是同一节点 2. 方向相反 | ⚪ 继承基类 | ⚪ 继承基类 | GraphView 核心方法，过滤可连接的端口 |
| ~~`IsValidConnection()`~~ | ❌ 已删除 | ❌ 已删除 | ❌ 已删除 | 死板的 PortType 校验 - 已删除！ |
| ~~`GetPortType()`~~ | ❌ 已删除 | ❌ 已删除 | ❌ 已删除 | 死板的端口类型映射 - 已删除！ |
| ~~`ValidateConnection()`~~ | ❌ 已删除 | ❌ 已删除 | ❌ 已删除 | 额外的连接验证 - 已删除！ |
| ~~`InitializeConnectionRules()`~~ | ❌ 已删除 | ❌ 已删除 | ❌ 已删除 | 连接规则初始化 - 已删除！ |
| ~~`AddConnectionRule()`~~ | ❌ 已删除 | ❌ 已删除 | ❌ 已删除 | 添加连接规则 - 已删除！ |
| ~~`NodeConnectionRule`~~ | ❌ 已删除 | ❌ 已删除 | ❌ 已删除 | 连接规则类 - 已删除！ |
| ~~`NodePortType`~~ | ❌ 已删除 | ❌ 已删除 | ❌ 已删除 | 端口类型枚举 - 已删除！ |

**🎉 现在开发者可以随意连接任何节点，不再受死板的规则限制喵~**

---

### 3. 节点生命周期管理

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `AddNode(BaseNode)` | ✅ `protected` - 添加到画布和 NodeMap<br>✅ 调用 `OnNodeAddedGeneric()` | ⚪ 继承基类 | ⚪ 继承基类 | 添加节点到画布 |
| `OnNodeAddedGeneric(BaseNode)` | 🟡 `virtual` - 空实现 | ⚪ 继承基类 | ⚪ 继承基类 | 节点添加回调（非泛型） |
| `OnNodeRemovedGeneric(BaseNode)` | 🟡 `virtual` - 空实现 | ⚪ 继承基类 | ⚪ 继承基类 | 节点移除回调（非泛型） |
| `OnGraphViewChanged()` | ✅ `private` - GraphView 变更回调<br>✅ 处理节点移除事件<br>✅ 从 NodeMap 清理 | ⚪ 继承基类 | ⚪ 继承基类 | 监听画布元素变化 |

---

### 4. 节点创建工厂方法

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `CreateNode(Type, Vector2, BaseNodeData)` | ✅ `public` - 终极节点工厂（反射创建）<br>✅ 实现 INekoGraphNodeFactory 接口 | ⚪ 继承基类 | ⚪ 继承基类 | 通过反射创建任意节点类型 |
| `CreateAndAddNodeFromData(BaseNodeData, Vector2)` | ✅ `protected` - 从数据创建节点<br>✅ 调用 `GetNodeTypeFromData()` | ⚪ 继承基类 | ⚪ 继承基类 | 从数据创建并添加节点 |
| `GetNodeTypeFromData(BaseNodeData)` | ✅ `private` - 反射查找节点类型 | ⚪ 继承基类 | ⚪ 继承基类 | 根据数据类型查找节点类型 |
| `ConvertScreenToLocal()` | ✅ `public` - 坐标转换<br>✅ 实现 INekoGraphNodeFactory 接口 | ⚪ 继承基类 | ⚪ 继承基类 | 屏幕坐标转画布坐标 |
| `SyncNodePositionToData(BaseNode)` | ✅ `protected` - 同步位置到数据 | ⚪ 继承基类 | ⚪ 继承基类 | 序列化前同步位置信息 |

**MissionGraphView 特有方法**:
| 方法名 | 返回类型 | 功能 |
|--------|---------|------|
| `SetCurrentMapId(string)` | `void` | 设置当前地图 ID 并同步所有地图节点 |
| `CurrentMapId` | `string` | 获取当前地图 ID |

**StoryGraphView 特有方法**:
| 方法名 | 返回类型 | 功能 |
|--------|---------|------|
| `RootNode` | `RootNode` | 从 NodeMap 中查找根节点 |

---

### 5. Copy & Paste

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `SerializeCopyElements()` | ✅ `private` - 序列化选中节点<br>✅ 使用 `CopyPasteData` | ⚪ 继承基类 | ⚪ 继承基类 | 复制时序列化选中节点 |
| `UnserializePasteElements()` | ✅ `private` - 反序列化并粘贴 | ⚪ 继承基类 | ⚪ 继承基类 | 粘贴时反序列化并创建节点 |
| `OnNodePasted(BaseNode)` | 🟡 `virtual` - 空实现 | ⚪ 继承基类 | ⚪ 继承基类 | 节点粘贴回调 |

---

### 6. 序列化/反序列化

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `SerializeToPack()` | 🟡 `virtual` - 【邮件自动分拣系统】<br>✅ 遍历 NodeMap<br>✅ `UpdateData()`<br>✅ `SyncNodePositionToData()`<br>✅ `CollectConnections()`<br>✅ 反射填充列表和单字段 | ⚪ 继承基类<br>(使用基类反射) | 🔴 `override` - 完整实现<br>✅ 调用基类反射<br>✅ 保存 Sequences | 将画布内容序列化到数据包 |
| `PopulateFromPack()` | 🟡 `virtual` - 【邮件自动分拣系统】<br>✅ 清空画布和 NodeMap<br>✅ 从列表和单字段提取数据<br>✅ HashSet 自动去重 | ⚪ 继承基类 | 🔴 `override` - 完整实现<br>✅ 调用基类反射<br>✅ 恢复 Sequences | 从数据包填充画布 |
| `RestoreConnections()` | ✅ `protected` - 创建 nodeMap<br>✅ 调用 `RestoreConnectionsHelper<>()` | ⚪ 继承基类 | ⚪ 继承基类 | 恢复节点连线（通用入口） |
| `ValidatePack()` | 🟡 `virtual` - 调用 `pack.Validate()` | ⚪ 继承基类 | ⚪ 继承基类 | 验证数据包有效性 |

---

### 7. 连线恢复（静态工具方法）

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `RestoreConnectionsHelper<>()` | ✅ `protected static` - 通用连线恢复逻辑<br>✅ 遍历 `OutputConnections`<br>✅ 调用 `GetPortByIndex()`<br>✅ 调用 `SetInPortFieldValue()` | ⚪ 继承基类 | ⚪ 继承基类 | 通用静态工具方法，所有子类共用 |
| `CollectConnections(BaseNode)` | ✅ `protected` - 【连线自动捕获·混沌版】<br>✅ 遍历所有输出端口<br>✅ 不管连了什么，全部捕获<br>✅ 回写到 [OutPort] 字段<br>✅ 类型对得上就填，对不上也不报错 | ⚪ 继承基类 | ⚪ 继承基类 | 【连线自动捕获系统】直接读取 UI 上的 Edge |
| `SyncConnectionsToFields()` | ✅ `private` - 将连线回写到字段<br>✅ 支持 string 和 List<string> | ⚪ 继承基类 | ⚪ 继承基类 | 同步连线数据到字段 |
| `SetInPortFieldValue()` | ✅ `private static` - 设置 `[InPort]` 字段值 | ⚪ 继承基类 | ⚪ 继承基类 | 设置目标节点的输入端口字段 |
| `GetPortByIndex()` | ✅ `private static` - 根据索引获取端口 | ⚪ 继承基类 | ⚪ 继承基类 | 从容器中获取指定索引的端口 |

---

### 8. 工具栏生成

| 方法名 | BaseGraphView | MissionGraphWindow | StoryGraphWindow | 说明 |
|--------|---------------|-------------------|------------------|------|
| `GenerateToolbar()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | ✅ 创建 Toolbar<br>✅ 调用 `AddDefaultToolbarButtons()`<br>✅ 调用 `AddCustomButtons()` |
| `AddDefaultToolbarButtons()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | ✅ 添加保存按钮<br>✅ 添加读取按钮 |
| `AddCustomButtons()` | ⚪ 无 | 🟡 `virtual` - 空实现 | 🟡 `virtual` - 空实现 | 子类可扩展全局功能 |
| `SaveData()` | ⚪ 无 | 🔴 `override` - Mission 专用保存 | 🔴 `override` - Story 专用保存 | ✅ 打开保存对话框<br>✅ 调用 `SerializeToPack()`<br>✅ 保存到文件 |
| `LoadData()` | ⚪ 无 | 🔴 `override` - Mission 专用加载 | 🔴 `override` - Story 专用加载 | ✅ 打开读取对话框<br>✅ 调用 `LoadFromFile()`<br>✅ 调用 `PopulateFromPack()` |
| `SaveToFile()` | ⚪ 无 | ⚪ 继承基类 | ⚪ 继承基类 | ✅ 将 JSON 写入文件 |
| `LoadFromFile()` | ⚪ 无 | ⚪ 继承基类 | 🔴 `override` - Story 专用加载 | ✅ 从文件读取 JSON |

---

### 9. SearchWindow 相关

| 方法名 | BaseGraphWindow | BaseNodeSearchWindow | MissionNodeSearchWindow | StoryNodeSearchWindow | 说明 |
|--------|-----------------|---------------------|------------------------|----------------------|------|
| `CreateSearchTree()` | ⚪ 无 | ✅ `public` - 生成树状目录<br>✅ 按一级菜单分组 | ⚪ 继承基类 | ⚪ 继承基类 | 创建搜索树 |
| `OnSelectEntry()` | ⚪ 无 | ✅ `public` - 处理选择<br>✅ 调用 `ConvertScreenToLocal()`<br>✅ 调用 `CreateNode()` | ⚪ 继承基类 | ⚪ 继承基类 | 选择条目时创建节点 |
| `GetNodeTypesForCurrentSystem()` | ✅ `protected` - 调用 `NodeTypeHelper` | ✅ `protected` - 调用 `NodeTypeHelper` | ⚪ 继承基类 | ⚪ 继承基类 | 获取当前系统可用节点类型 |
| `CurrentNodeSystem` | 🟡 `virtual` - 返回 `Common` | 🔴 `abstract` | ✅ `override` - 返回 `Mission` | ✅ `override` - 返回 `Story` | 指定所属系统类型 |

---

### 10. 特有字段和方法

#### MissionGraphView 特有

| 成员名 | 类型 | 说明 |
|--------|------|------|
| `_currentMapId` | `string` | 当前地图 ID |
| `CurrentMapId` | `getter` | 获取当前地图 ID |
| `SetCurrentMapId()` | `method` | 设置当前地图 ID 并同步所有地图节点 |

#### StoryGraphView 特有

| 成员名 | 类型 | 说明 |
|--------|------|------|
| `Sequences` | `List<DialogueSequence>` | CSV 导入的对话序列数据 |
| `RootNode` | `getter` | 从 NodeMap 中查找根节点 |

#### MissionGraphWindow 特有

| 成员名 | 类型 | 说明 |
|--------|------|------|
| `ConstructGraphView()` | `method` | 重写创建顺序：先 GraphView 后 SearchWindow |
| `OnGraphViewConstructed()` | `method` | 初始化当前地图 ID |

#### StoryGraphWindow 特有

| 成员名 | 类型 | 说明 |
|--------|------|------|
| `_rootNodeCreated` | `bool` | 根节点创建标记 |
| `ConstructGraphView()` | `method` | 重写创建顺序：先 GraphView 后 SearchWindow |
| `OnGraphViewConstructed()` | `method` | 自动创建根节点 |
| `CreateRootNodeIfNotExists()` | `method` | 检查并创建根节点 |
| `ImportCSV()` | `method` | CSV 导入功能 |

---

## 📊 方法数量统计

| 类 | 总方法数 | 继承 | 重写 | 自有 | 静态 |
|----|---------|------|------|------|------|
| **INekoGraphNodeFactory** | 2 | - | - | 2 (接口定义) | 0 |
| **NodeTypeInfo** | 0 | - | - | 0 (数据类) | 0 |
| **NodeTypeHelper** | 5 | - | - | 5 | 5 |
| **BaseNodeSearchWindow** | 4 | 2 | 0 | 4 | 0 |
| **BaseGraphWindow** | 15 | 2 | 0 | 15 | 0 |
| **BaseGraphView** | 22 | 1 (override) | 0 | 18 | 5 |
| **MissionGraphView** | 2 | 1 | 0 | 2 | 0 |
| **StoryGraphView** | 3 | 1 | 2 | 3 | 0 |
| **MissionGraphWindow** | 8 | 5 | 3 | 6 | 0 |
| **StoryGraphWindow** | 11 | 5 | 4 | 9 | 0 |
| **MissionNodeSearchWindow** | 1 | 3 | 1 | 1 | 0 |
| **StoryNodeSearchWindow** | 1 | 3 | 1 | 1 | 0 |

---

## 🎯 架构优化记录

### 2026-03-10 重构 #1：位置同步修复

#### 🐛 问题描述
存档时没有更新节点的坐标信息，用户拖动节点后位置不会保存到数据中。

#### ✅ 解决方案
在 `BaseGraphView.SerializeToPack()` 中添加 `SyncNodePositionToData(node)` 调用：

```csharp
foreach (var node in NodeMap.Values)
{
    node.UpdateData();
    SyncNodePositionToData(node);  // 新增：同步位置信息喵~
    CollectConnections(node);
}
```

---

### 2026-03-10 重构 #2：彻底拥抱混沌·主语驱动重构

#### ❌ 删除的死板代码
1. **`NodePortType` 枚举** - 完全删除
   - 原因：限制开发者自由，强制使用预定义的端口类型
2. **`NodeConnectionRule` 类** - 完全删除
   - 原因：死板的连接规则校验，阻碍创造性连接
3. **`GetPortType()` 方法** - 完全删除
   - 原因：强制子类为每个节点类型定义端口类型
4. **`IsValidConnection()` 方法** - 完全删除
   - 原因：基于 PortType 的校验逻辑过于严格
5. **`ValidateConnection()` 方法** - 完全删除
   - 原因：多余的验证层
6. **`InitializeConnectionRules()` 方法** - 完全删除
   - 原因：需要手动维护大量连接规则
7. **`AddConnectionRule()` 方法** - 完全删除
   - 原因：连接规则本身就是反人类的限制

#### ✅ 重构后的新架构

**`GetCompatiblePorts` 新逻辑**:
```csharp
public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
{
    var compatiblePorts = new List<Port>();

    foreach (var port in ports.ToList())
    {
        // 跳过自己（不是同一个节点）
        if (port.node == startPort.node)
            continue;

        // 跳过不同方向的端口（必须一进一出）
        if (port.direction == startPort.direction)
            continue;

        // ✅ 通过检查！开发者想怎么连就怎么连喵~
        compatiblePorts.Add(port);
    }

    return compatiblePorts;
}
```

**`CollectConnections` 新逻辑**:
```csharp
protected List<ConnectionData> CollectConnections(BaseNode node)
{
    var connections = new List<ConnectionData>();

    // 遍历输出容器的每一个 Port 喵~
    int portIndex = 0;
    foreach (var element in node.outputContainer.Children())
    {
        if (element is Port outputPort)
        {
            // 遍历该 Port 连出去的所有 Edge 喵~
            foreach (var edge in outputPort.connections)
            {
                var inputNode = edge.input.node;
                if (inputNode is BaseNode targetNode && targetNode.Data != null)
                {
                    var targetNodeId = targetNode.Data.NodeID;
                    if (!string.IsNullOrEmpty(targetNodeId))
                    {
                        connections.Add(new ConnectionData(
                            portIndex,
                            targetNodeId,
                            0  // 默认连接到目标节点的输入端口 0
                        ));
                    }
                }
            }
            portIndex++;
        }
    }

    // 回写到 [OutPort] 字段喵~
    SyncConnectionsToFields(data, connections);
    data.OutputConnections = connections;

    return connections;
}
```

#### 🔧 重构效果对比

| 项目 | 重构前 | 重构后 | 改善 |
|------|--------|--------|------|
| `NodePortType` 枚举值 | 10+ | 0 | -100% |
| `NodeConnectionRule` 实例 | Mission: 19, Story: 6 | 0 | -100% |
| `GetPortType` switch case | Mission: 8, Story: 5 | 0 | -100% |
| 连接校验逻辑行数 | ~100 行 | 0 | -100% |
| 开发者自由度 | 受限 | 无限 | +∞ |
| 代码可维护性 | 中等 | 高 | +50% |

---

## ⚠️ 已解决的架构问题

### 问题 1: 存档时节点位置不同步 ✅ 已解决

**解决方案**: 在 `SerializeToPack()` 中调用 `SyncNodePositionToData(node)` 方法。

```csharp
foreach (var node in NodeMap.Values)
{
    node.UpdateData();
    SyncNodePositionToData(node);  // 同步位置信息喵~
    CollectConnections(node);
}
```

### 问题 2: PortType 校验过于死板 ✅ 已解决

**问题描述**: 开发者只能按照预定义的规则连接节点，无法创造性地连接不同类型的节点。

**解决方案**: 删除所有 PortType 相关代码，`GetCompatiblePorts` 只检查最基本的规则：
1. 不是同一个节点
2. 方向相反（一进一出）

```csharp
// ✅ 重构后 - 开发者想怎么连就怎么连！
public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
{
    foreach (var port in ports.ToList())
    {
        if (port.node == startPort.node) continue;       // 不是同一节点
        if (port.direction == startPort.direction) continue; // 方向相反
        compatiblePorts.Add(port);  // ✅ 通过！
    }
}
```

### 问题 3: 连接规则维护成本高 ✅ 已解决

**问题描述**: 每新增一种节点类型，就需要在 `InitializeConnectionRules()` 中添加大量规则。

**解决方案**: 删除连接规则系统，让开发者通过代码或数据动态控制连接逻辑（如果需要的话）。

---

## 🎯 设计原则遵循

### ✅ 单一职责原则 (SRP)
- `BaseGraphView`: 画布逻辑 + 节点工厂 + NodeMap 管理 + 连线捕获
- `BaseGraphWindow`: 窗口管理 + 工具栏
- `BaseNodeSearchWindow`: 搜索窗口 + 菜单生成
- `NodeTypeHelper`: 反射缓存

### ✅ 开放封闭原则 (OCP)
- 新增节点类型只需添加 `[NodeMenuItem]` 和 `[NodeType]` 标签
- 无需修改 SearchWindow 或工具栏代码
- 子类可选择性重写序列化方法

### ✅ DRY 原则
- 所有节点类型信息统一由 `NodeTypeHelper` 管理
- SearchWindow 和 GraphWindow 共享同一份缓存
- 连线恢复逻辑统一由基类静态方法处理

### 🎉 混沌原则 (Chaos Principle)
- **删除所有死板的校验**：PortType、ConnectionRule 全部删除
- **把自由还给开发者**：想怎么连就怎么连，编译器不再限制
- **主语驱动**：开发者是主语，框架是工具，工具不应该限制主语

---

## 📝 未来扩展建议

### 建议 1: 添加工具栏全局功能
```csharp
protected override void AddCustomButtons(Toolbar toolbar)
{
    toolbar.Add(new Button(() => AutoLayout()) { text = "🧹 自动布局" });
    toolbar.Add(new Button(() => ValidateGraph()) { text = "🐞 验证逻辑" });
    toolbar.Add(new Button(() => ToggleMinimap()) { text = "🗺️ 小地图" });
}
```

### 建议 2: 可选的连接校验插件
如果项目确实需要连接校验，可以创建可选的插件系统：
```csharp
// 可选的校验接口，子类按需实现
public interface IConnectionValidator
{
    bool CanConnect(Port from, Port to);
}
```

### 建议 3: 优化 NodeMap 查询性能
对于频繁的类型查询，可以考虑添加类型索引缓存：
```csharp
protected Dictionary<Type, List<BaseNode>> TypeIndex = new Dictionary<Type, List<BaseNode>>();
```

---

## 🐱 架构版本演进

| 版本 | 日期 | 主要变更 |
|------|------|----------|
| 1.0 | 2026-03-XX | 初始版本，泛型约束地狱 |
| 2.0 | 2026-03-10 | 反射工厂 + 接口解耦重构 |
| 2.1 | 2026-03-10 | NodeMap 中央情报局 + 邮件自动分拣系统 |
| 3.0 | 2026-03-10 | **彻底拥抱混沌·主语驱动重构** - 删除所有 PortType 校验 |
| 4.0 | 2026-03-11 | **公共流程节点重构** - StoryRoot/SpineID/LeafID 节点提取为 Mission/Story 共用 |

---

**文档结束** 🐱✨
