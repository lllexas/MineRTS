# NekoGraph 面向对象架构分析文档

**最后更新**: 2026 年 3 月 11 日
**架构版本**: 4.0 (公共流程节点重构版)
**分析对象**: BaseGraphView, MissionGraphView, StoryGraphView 及其基类

---

## 📊 类层次结构总览

```
┌─────────────────────────────────────────────────────────────────┐
│  数据层（Runtime） - 不依赖 UnityEditor                        │
├─────────────────────────────────────────────────────────────────┤
│  BaseNodeData                                                   │
│  ├─ NodeID, EditorPosition, OutputConnections                   │
│  └─ CopyFrom()                                                  │
│                                                                  │
│  BasePackData                                                   │
│  ├─ PackID                                                      │
│  └─ Validate()                                                  │
│                                                                  │
│  BasePackData<T> : BasePackData                                 │
│  └─ Nodes: List<T>                                              │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ 依赖（泛型约束）
                              │
┌─────────────────────────────────────────────────────────────────┐
│  编辑器层（Editor） - 依赖 UnityEditor                          │
├─────────────────────────────────────────────────────────────────┤
│  BaseGraphView<TPack> : GraphView, INekoGraphNodeFactory        │
│  where TPack : BasePackData                                     │
│                                                                  │
│  ├─ NodeMap: Dictionary<string, BaseNode> 【中央情报局】        │
│  ├─ SelectedNodes                                               │
│  │                                                              │
│  ├─ 构造函数：SetupZoom, Manipulators, Grid, 序列化回调         │
│  │                                                              │
│  ├─ 节点管理                                                    │
│  │  ├─ CreateNode() - 终极节点工厂（反射创建）                  │
│  │  ├─ CreateAndAddNodeFromData() - 从数据创建节点              │
│  │  ├─ AddNode() - 添加节点到画布                               │
│  │  ├─ SyncNodePositionToData() - 同步位置到数据                │
│  │  └─ OnNodeAddedGeneric() / OnNodeRemovedGeneric()            │
│  │                                                              │
│  ├─ Copy & Paste                                                │
│  │  ├─ SerializeCopyElements() - 序列化选中节点                 │
│  │  ├─ UnserializePasteElements() - 反序列化粘贴                │
│  │  └─ OnNodePasted()                                           │
│  │                                                              │
│  ├─ 序列化/反序列化【邮件自动分拣系统】                         │
│  │  ├─ SerializeToPack() - 反射遍历 NodeMap 填充列表/单字段     │
│  │  ├─ PopulateFromPack() - 反射提取数据，HashSet 去重创建节点  │
│  │  ├─ RestoreConnections() - 调用静态工具恢复连线              │
│  │  └─ ValidatePack()                                           │
│  │                                                              │
│  ├─ 连线管理【连线自动捕获系统】                                │
│  │  ├─ CollectConnections() - 从画布读取连线回写 [OutPort] 字段  │
│  │  ├─ SyncConnectionsToFields() - 回写到字段                   │
│  │  ├─ RestoreConnectionsHelper<TG,TP>() - 静态工具恢复连线     │
│  │  ├─ SetInPortFieldValue() - 设置 [InPort] 字段值             │
│  │  ├─ GetPortByIndex() - 根据索引获取端口                      │
│  │  └─ GetPortIndexFromContainer() - 从容器获取端口索引         │
│  │                                                              │
│  ├─ 端口兼容性                                                   │
│  │  └─ GetCompatiblePorts() - 【彻底拥抱混沌】只检查节点/方向   │
│  │                                                              │
│  └─ 工厂接口实现                                                │
│     └─ ConvertScreenToLocal() - 屏幕坐标转画布坐标               │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ 继承
         ┌────────────────────┴────────────────────┐
         │                                         │
┌─────────────────────┐               ┌─────────────────────────┐
│ MissionGraphView    │               │ StoryGraphView          │
│ : BaseGraphView<    │               │ : BaseGraphView<        │
│   MissionPackData>  │               │   StoryPackData>        │
├─────────────────────┤               ├─────────────────────────┤
│ - _currentMapId     │               │ - Sequences             │
│ + CurrentMapId      │               │ + RootNode              │
│ + SetCurrentMapId() │               │ + SerializeToPack() 🔴  │
│                     │               │ + PopulateFromPack() 🔴 │
│ 【特色】            │               │                         │
│ • 地图 ID 联动机制   │               │ 【特色】                │
│ • 无特殊连接规则    │               │ • CSV 对话序列支持       │
│ • 完全继承基类      │               │ • Sequences 支持        │
└─────────────────────┘               └─────────────────────────┘
```

---

## 🏗️ 核心设计模式分析

### 1. 泛型约束 + 反射工厂模式

```csharp
public abstract class BaseGraphView<TPack> : GraphView, INekoGraphNodeFactory
    where TPack : BasePackData
```

**优点**:
- ✅ 类型安全：编译时确保 TPack 是有效的数据包类型
- ✅ 代码复用：基类处理所有通用逻辑，子类零样板代码
- ✅ 扩展性强：新增系统只需继承并指定 TPack 类型

**节点工厂方法**:
```csharp
public BaseNode CreateNode(Type nodeType, Vector2 position, BaseNodeData data = null)
{
    // 反射创建 Data（如果未提供）
    // 反射创建 Node 实例
    // 注入数据并添加到画布
}
```

---

### 2. 中央情报局模式（NodeMap）

```csharp
protected Dictionary<string, BaseNode> NodeMap = new Dictionary<string, BaseNode>();
```

**职责**:
- 📍 统一管理画布上所有节点的生死
- 🔍 提供 O(1) 时间复杂度的节点查找
- 🔗 连线恢复时快速定位目标节点

**生命周期管理**:
```csharp
// 添加节点时注册
AddNode(node) {
    AddElement(node);
    NodeMap.Add(node.Data.NodeID, node);
    OnNodeAddedGeneric(node);
}

// 移除节点时注销
OnGraphViewChanged(changes) {
    foreach (var element in changes.elementsToRemove) {
        if (element is BaseNode node) {
            NodeMap.Remove(node.Data.NodeID);
        }
    }
}
```

---

### 3. 邮件自动分拣系统（序列化/反序列化）

**SerializeToPack 流程**:
```
1. 遍历 NodeMap.Values
   ├─ node.UpdateData() - 同步 UI 控件值
   ├─ SyncNodePositionToData() - 同步位置
   └─ CollectConnections(node) - 捕获连线到字段

2. 反射遍历 TPack 字段
   ├─ 列表字段 List<T>
   │  └─ 类型匹配则 Add 到列表
   └─ 单字段 T
      └─ 类型匹配则赋值（只取第一个）

3. 填充基类 Nodes 列表（包含所有节点）
```

**PopulateFromPack 流程**:
```
1. 清空画布和 NodeMap

2. 反射遍历 TPack 字段
   ├─ 列表字段 → 提取所有 BaseNodeData
   └─ 单字段 → 提取有效 BaseNodeData

3. HashSet 自动去重

4. 统一创建节点
   └─ CreateAndAddNodeFromData(data, position)

5. 恢复连线
   └─ RestoreConnections()
```

**优点**:
- ✅ 零配置：无需手动编写序列化逻辑
- ✅ 自动去重：HashSet 确保节点不重复
- ✅ 类型安全：反射确保类型匹配

---

### 4. 连线自动捕获系统

**CollectConnections - 从画布捕获连线**:
```csharp
protected List<ConnectionData> CollectConnections(BaseNode node)
{
    // 遍历输出容器的每个 Port
    foreach (var outputPort in node.outputContainer.Children())
    {
        // 遍历该 Port 的所有连接 Edge
        foreach (var edge in outputPort.connections)
        {
            // 记录：FromPortIndex, TargetNodeID, ToPortIndex
            connections.Add(new ConnectionData(...));
        }
    }
    
    // 回写到 [OutPort] 字段
    SyncConnectionsToFields(data, connections);
    
    // 同时更新 OutputConnections 列表
    data.OutputConnections = connections;
}
```

**RestoreConnectionsHelper - 恢复连线**:
```csharp
protected static void RestoreConnectionsHelper<TG, TP>(
    TG graph,
    Dictionary<string, BaseNode> nodeMap)
    where TG : BaseGraphView<TP>
    where TP : BasePackData
{
    foreach (var node in nodeMap.Values)
    {
        foreach (var conn in node.Data.OutputConnections)
        {
            // 获取输出端口
            var outputPort = GetPortByIndex(node, conn.FromPortIndex, Direction.Output);
            
            // 获取输入端口
            var inputPort = GetPortByIndex(targetNode, conn.ToPortIndex, Direction.Input);
            
            // 连接
            var edge = outputPort.ConnectTo(inputPort);
            graph.AddElement(edge);
            
            // 设置目标节点的 [InPort] 字段值
            SetInPortFieldValue(targetNode.Data, conn.ToPortIndex, node.Data.NodeID);
        }
    }
}
```

**设计亮点**:
- ✅ 双写机制：同时更新 `OutputConnections` 和 `[OutPort]` 字段
- ✅ 静态工具：所有子类共用同一套恢复逻辑
- ✅ 端口索引：精确记录连接的端口位置

---

### 5. 端口标签驱动设计

**InPort/OutPort 标签**:
```csharp
[AttributeUsage(AttributeTargets.Field)]
public class InPortAttribute : Attribute
{
    public int Index { get; }
    public string PortName { get; }
    public NekoPortCapacity Capacity { get; }
}

[AttributeUsage(AttributeTargets.Field)]
public class OutPortAttribute : Attribute
{
    public int Index { get; }
    public string PortName { get; }
    public NekoPortCapacity Capacity { get; }
}
```

**使用示例**:
```csharp
[Serializable]
public class SpineNodeData : BaseNodeData
{
    public string ProcessID;
    
    [InPort(0, "信号输入", NekoPortCapacity.Multi)]
    public List<string> ParentSpineID;
    
    [OutPort(0, "信号输出", NekoPortCapacity.Multi)]
    public List<string> NextSpineNodeIDs;
}
```

**端口生成机制**:
```csharp
// BaseNode.GeneratePortsFromMetadata()
// 扫描 Data 类中带有 [InPort]/[OutPort] 标签的字段
// 自动生成对应的端口 UI
```

---

## 📋 方法分配对比表

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

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `.ctor()` | ✅ 设置 Zoom/Drag/Selection<br>✅ 添加背景网格<br>✅ 注册序列化回调 | ✅ 调用基类构造函数 | ✅ 调用基类构造函数 | 初始化画布基础功能 |
| `OnGraphViewChanged()` | ✅ 处理节点移除事件<br>✅ 从 NodeMap 注销 | ⚪ 继承基类 | ⚪ 继承基类 | 节点生命周期管理 |

---

### 2. 节点管理

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `CreateNode(Type, Vector2, BaseNodeData)` | ✅ `public` - 终极节点工厂（反射创建）<br>✅ 实现 INekoGraphNodeFactory 接口 | ⚪ 继承基类 | ⚪ 继承基类 | 通过反射创建任意节点类型 |
| `CreateAndAddNodeFromData(BaseNodeData, Vector2)` | ✅ `protected` - 从数据创建节点<br>✅ 调用 `GetNodeTypeFromData()` | ⚪ 继承基类 | ⚪ 继承基类 | 从数据创建并添加节点 |
| `GetNodeTypeFromData(BaseNodeData)` | ✅ `private` - 反射查找节点类型 | ⚪ 继承基类 | ⚪ 继承基类 | 根据数据类型查找节点类型 |
| `AddNode(BaseNode)` | ✅ `protected` - 添加到画布和 NodeMap<br>✅ 调用 `OnNodeAddedGeneric()` | ⚪ 继承基类 | ⚪ 继承基类 | 节点注册到中央情报局 |
| `SyncNodePositionToData(BaseNode)` | ✅ `protected` - 同步位置到数据 | ⚪ 继承基类 | ⚪ 继承基类 | 确保位置信息最新 |
| `OnNodeAddedGeneric(BaseNode)` | 🟡 `virtual` - 空实现 | ⚪ 继承基类 | ⚪ 继承基类 | 节点添加回调 |
| `OnNodeRemovedGeneric(BaseNode)` | 🟡 `virtual` - 空实现 | ⚪ 继承基类 | ⚪ 继承基类 | 节点移除回调 |

**子类特有方法**:
| 方法名 | MissionGraphView | StoryGraphView | 说明 |
|--------|------------------|----------------|------|
| `SetCurrentMapId(string)` | ✅ `public` - 设置地图 ID 并联动更新 | ⚪ 无 | 地图 ID 联动机制 |
| `RootNode` | ⚪ 无 | ✅ `public` - 查找根节点 | 快速获取根节点 |

---

### 3. Copy & Paste

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `SerializeCopyElements()` | ✅ `private` - 序列化选中节点<br>✅ 使用 `CopyPasteData` | ⚪ 继承基类 | ⚪ 继承基类 | 复制时序列化选中节点 |
| `UnserializePasteElements()` | ✅ `private` - 反序列化并粘贴 | ⚪ 继承基类 | ⚪ 继承基类 | 粘贴时反序列化并创建节点 |
| `OnNodePasted(BaseNode)` | 🟡 `virtual` - 空实现 | ⚪ 继承基类 | ⚪ 继承基类 | 节点粘贴回调 |

---

### 4. 序列化/反序列化【邮件自动分拣系统】

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `SerializeToPack()` | 🟡 `virtual` - 【邮件自动分拣系统】<br>✅ 遍历 NodeMap<br>✅ `UpdateData()`<br>✅ `SyncNodePositionToData()`<br>✅ `CollectConnections()`<br>✅ 反射填充列表和单字段 | ⚪ 继承基类<br>(使用基类反射) | 🔴 `override` - 完整实现<br>✅ 调用基类反射<br>✅ 保存 Sequences | 将画布内容序列化到数据包 |
| `PopulateFromPack()` | 🟡 `virtual` - 【邮件自动分拣系统】<br>✅ 清空画布和 NodeMap<br>✅ 从列表和单字段提取数据<br>✅ HashSet 自动去重 | ⚪ 继承基类 | 🔴 `override` - 完整实现<br>✅ 调用基类反射<br>✅ 恢复 Sequences | 从数据包填充画布 |
| `RestoreConnections()` | ✅ `protected` - 创建 nodeMap<br>✅ 调用 `RestoreConnectionsHelper<>()` | ⚪ 继承基类 | ⚪ 继承基类 | 恢复节点连线（通用入口） |
| `ValidatePack()` | 🟡 `virtual` - 调用 `pack.Validate()` | ⚪ 继承基类 | ⚪ 继承基类 | 验证数据包有效性 |

---

### 5. 连线管理【连线自动捕获系统】

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `CollectConnections(BaseNode)` | ✅ `protected` - 从画布读取连线<br>✅ 回写到 [OutPort] 字段<br>✅ 更新 OutputConnections | ⚪ 继承基类 | ⚪ 继承基类 | 连线数据捕获 |
| `SyncConnectionsToFields()` | ✅ `private` - 回写到字段<br>✅ 支持 string/List<string> | ⚪ 继承基类 | ⚪ 继承基类 | 字段同步 |
| `RestoreConnectionsHelper<>()` | ✅ `protected static` - 静态工具方法<br>✅ 遍历 NodeMap<br>✅ 创建 Edge<br>✅ 设置 [InPort] 字段值 | ⚪ 继承基类 | ⚪ 继承基类 | 连线恢复工具 |
| `SetInPortFieldValue()` | ✅ `private static` - 设置 [InPort] 字段值 | ⚪ 继承基类 | ⚪ 继承基类 | 字段值设置 |
| `GetPortByIndex()` | ✅ `private static` - 根据索引获取端口 | ⚪ 继承基类 | ⚪ 继承基类 | 端口查找 |
| `GetPortIndexFromContainer()` | ✅ `private static` - 从容器获取端口索引 | ⚪ 继承基类 | ⚪ 继承基类 | 端口索引获取 |

---

### 6. 端口兼容性

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `GetCompatiblePorts()` | ✅ `override` - 【彻底拥抱混沌】<br>✅ 只检查：不是同一节点<br>✅ 只检查：方向相反 | ⚪ 继承基类 | ⚪ 继承基类 | 端口兼容性过滤 |

---

### 7. 工厂接口实现

| 方法名 | BaseGraphView | MissionGraphView | StoryGraphView | 说明 |
|--------|---------------|------------------|----------------|------|
| `ConvertScreenToLocal()` | ✅ `public` - 屏幕坐标转画布坐标<br>✅ 实现 INekoGraphNodeFactory 接口 | ⚪ 继承基类 | ⚪ 继承基类 | 坐标转换 |

---

### 8. MissionGraphView 特有方法

| 方法名 | 返回类型 | 功能 |
|--------|---------|------|
| `SetCurrentMapId(string)` | `void` | 设置当前地图 ID 并同步所有地图节点 |
| `CurrentMapId` | `string` | 获取当前地图 ID |

---

### 9. StoryGraphView 特有方法

| 方法名 | 返回类型 | 功能 |
|--------|---------|------|
| `RootNode` | `RootNode` | 从 NodeMap 中查找根节点 |
| `SerializeToPack()` | `StoryPackData` | 序列化 + Sequences 保存 |
| `PopulateFromPack()` | `void` | 反序列化 + Sequences 恢复 |

---

## 🎯 面向对象设计原则分析

### ✅ 单一职责原则 (SRP)

| 类 | 职责 |
|----|------|
| `BaseNodeData` | 节点数据基类（ID、位置、连线） |
| `BasePackData` | 数据包基类（PackID、验证） |
| `BaseGraphView` | 画布逻辑 + 节点工厂 + NodeMap 管理 + 连线捕获 |
| `MissionGraphView` | Mission 系统专用逻辑（地图 ID 联动） |
| `StoryGraphView` | Story 系统专用逻辑（Sequences 支持） |

**评价**: ✅ 每个类职责清晰，无上帝类

---

### ✅ 开放封闭原则 (OCP)

**扩展点**:
1. **新增节点类型**: 只需添加 `[NodeMenuItem]` 和 `[NodeType]` 标签
2. **新增系统**: 继承 `BaseGraphWindow` 和 `BaseGraphView`，指定 TPack
3. **自定义序列化**: 重写 `SerializeToPack()` / `PopulateFromPack()`
4. **节点生命周期回调**: 重写 `OnNodeAddedGeneric()` / `OnNodeRemovedGeneric()`

**评价**: ✅ 对扩展开放，对修改封闭

---

### ✅ Liskov 替换原则 (LSP)

- `MissionGraphView` 和 `StoryGraphView` 完全兼容基类方法
- 重写方法 (`SerializeToPack`, `PopulateFromPack`) 保持行为一致
- 子类可以无缝替换基类使用

**评价**: ✅ 子类完美符合基类契约

---

### ✅ 接口隔离原则 (ISP)

- `INekoGraphNodeFactory` 接口只包含 2 个方法：
  - `CreateNode(Type, Vector2, BaseNodeData)`
  - `ConvertScreenToLocal(Vector2, EditorWindow)`

**评价**: ✅ 接口精简，职责单一

---

### ✅ 依赖倒置原则 (DIP)

- `BaseGraphView` 依赖抽象 `BasePackData`，而非具体类
- `BaseNodeSearchWindow` 依赖 `INekoGraphNodeFactory` 接口
- `NodeTypeHelper` 通过反射解耦具体类型

**评价**: ✅ 面向抽象编程

---

### 🎉 混沌原则 (Chaos Principle)

**删除的校验**:
- ❌ PortType 校验
- ❌ ConnectionRule 校验
- ❌ 节点类型兼容性检查

**保留的规则**:
- ✅ 不是同一个节点
- ✅ 方向相反（一进一出）

**评价**: 🎉 把自由还给开发者，框架不做限制

---

## 📊 代码度量分析

### 类统计

| 类 | 行数 | 方法数 | 字段数 | 职责 |
|----|------|--------|--------|------|
| `BaseNodeData` | ~30 | 1 | 3 | 数据基类 |
| `BasePackData<T>` | ~20 | 1 | 1 | 数据包基类 |
| `BaseGraphView` | ~650 | 25+ | 2 | 画布核心 |
| `MissionGraphView` | ~40 | 1 | 1 | Mission 专用 |
| `StoryGraphView` | ~60 | 2 | 1 | Story 专用 |

**评价**: ✅ BaseGraphView 虽大但职责单一，子类精简

---

### 继承深度

```
BaseNodeData (1 层)
    ├─ RootNodeData
    ├─ SpineNodeData
    ├─ LeafNode_A_Data
    ├─ LeafNode_B_Data
    ├─ MissionNode_A_Data
    └─ ...

BasePackData (1 层)
    └─ BasePackData<T> (2 层)
        ├─ MissionPackData
        └─ StoryPackData

GraphView (Unity 基类)
    └─ BaseGraphView (1 层)
        ├─ MissionGraphView (2 层)
        └─ StoryGraphView (2 层)
```

**评价**: ✅ 继承层次浅，易于理解

---

## 🔧 设计亮点总结

### 1. 中央情报局（NodeMap）
```csharp
protected Dictionary<string, BaseNode> NodeMap = new Dictionary<string, BaseNode>();
```
- 统一管理节点生死
- O(1) 查找性能
- 连线恢复必备

### 2. 邮件自动分拣系统
```csharp
// 反射遍历字段 → 类型匹配 → 填充列表/单字段
// HashSet 自动去重
```
- 零配置序列化
- 自动去重
- 类型安全

### 3. 连线自动捕获
```csharp
// CollectConnections: 画布 → 字段
// RestoreConnectionsHelper: 字段 → 画布
```
- 双写机制
- 静态工具复用
- 端口索引精确记录

### 4. 端口标签驱动
```csharp
[InPort(0, "信号输入", NekoPortCapacity.Multi)]
[OutPort(0, "信号输出", NekoPortCapacity.Multi)]
```
- 声明式定义
- 自动生成端口
- UI 与数据同步

### 5. 彻底拥抱混沌
```csharp
public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
{
    // 只检查：不是同一节点 + 方向相反
    // 删除所有 PortType 校验
}
```
- 把自由还给开发者
- 框架不做限制

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
```csharp
// 可选的校验接口，子类按需实现
public interface IConnectionValidator
{
    bool CanConnect(Port from, Port to);
}
```

### 建议 3: 优化 NodeMap 查询性能
```csharp
// 添加类型索引缓存
protected Dictionary<Type, List<BaseNode>> TypeIndex = new Dictionary<Type, List<BaseNode>>();
```

### 建议 4: 节点分组/注释功能
```csharp
// 继承 GraphView 的注释功能
public class NoteNode : BaseNode<NoteData>
{
    // 可自由连接的注释节点
}
```

---

## 🐱 架构版本演进

| 版本 | 日期 | 主要变更 |
|------|------|----------|
| 1.0 | 2026-03-XX | 初始版本，泛型约束地狱 |
| 2.0 | 2026-03-10 | 反射工厂 + 接口解耦重构 |
| 2.1 | 2026-03-10 | NodeMap 中央情报局 + 邮件自动分拣系统 |
| 3.0 | 2026-03-10 | **彻底拥抱混沌·主语驱动重构** - 删除所有 PortType 校验 |
| 4.0 | 2026-03-11 | **公共流程节点重构** - Root/Spine/Leaf 提取为 Mission/Story 共用 |

---

## 🎯 总结

### 架构优势
1. ✅ **高内聚低耦合**: 基类封装通用逻辑，子类专注业务特性
2. ✅ **类型安全**: 泛型约束 + 反射确保编译时检查
3. ✅ **零配置**: 邮件自动分拣系统无需手动序列化
4. ✅ **可扩展**: 新增节点/系统只需添加标签或继承
5. ✅ **开发者友好**: 彻底拥抱混沌，把自由还给开发者

### 架构特色
- 📍 **中央情报局**: NodeMap 统一管理节点
- 📬 **邮件自动分拣**: 反射序列化/反序列化
- 🔗 **连线自动捕获**: 双写机制 + 静态工具
- 🏷️ **端口标签驱动**: 声明式定义端口
- 🎉 **彻底拥抱混沌**: 删除所有死板校验

---

**文档结束** 🐱✨
