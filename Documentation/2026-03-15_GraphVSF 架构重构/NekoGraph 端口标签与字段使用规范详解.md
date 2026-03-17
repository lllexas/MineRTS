# NekoGraph 端口标签与字段使用规范详解

**版本**: 2.0  
**日期**: 2026 年 3 月 15 日  
**作者**: NekoTeam  
**状态**: 完整规范文档

---

## 目录

1. [基础概念](#一基础概念)
2. [端口标签详解](#二端口标签详解)
3. [字段类型详解](#三字段类型详解)
4. [底层原理](#四底层原理)
5. [完整示例分析](#五完整示例分析)
6. [实战指南](#六实战指南)
7. [常见错误与调试](#七常见错误与调试)
8. [附录](#八附录)

---

## 一、基础概念

### 1.1 NekoGraph 架构分层

```
┌─────────────────────────────────────────────────────────────────┐
│                        Editor Layer (编辑器层)                    │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │  BaseNode   │  │ BaseGraphView│ │ BaseNode    │              │
│  │  (节点视图)  │  │   (画布)     │  │ SearchWindow│              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
│         ↓                ↓                                        │
│  ┌─────────────────────────────────────────────────┐             │
│  │         端口生成、连线同步、序列化/反序列化            │             │
│  └─────────────────────────────────────────────────┘             │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                       Runtime Layer (运行时层)                   │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ BaseNodeData│  │ BasePackData│  │ GraphRunner │              │
│  │ (节点数据)  │  │ (数据包)    │  │ (运行器)    │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
│         ↓                ↓                ↓                       │
│  ┌─────────────────────────────────────────────────┐             │
│  │              数据存储、信号流动、策略执行               │             │
│  └─────────────────────────────────────────────────┘             │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 端口是什么

**端口（Port）** 是节点之间建立连接的接口，分为：
- **输入端口（Input Port）**：接收来自其他节点的信号/数据
- **输出端口（Output Port）**：向其他节点发送信号/数据

### 1.3 连线是什么

**连线（Connection）** 是端口之间的连接关系，由 `ConnectionData` 结构体表示：

```csharp
[Serializable]
public class ConnectionData
{
    public int FromPortIndex;      // 源端口索引
    public string TargetNodeID;    // 目标节点 ID
    public int ToPortIndex;        // 目标端口索引

    public ConnectionData(int fromPortIndex, string targetNodeID, int toPortIndex)
    {
        FromPortIndex = fromPortIndex;
        TargetNodeID = targetNodeID;
        ToPortIndex = toPortIndex;
    }
}
```

### 1.4 数据流 vs 信号流

| 概念 | 说明 | 示例 |
|------|------|------|
| **数据流** | 节点数据之间的静态连接关系 | VFS 树状结构、Config 树状结构 |
| **信号流** | 运行时信号在节点之间的动态流动 | Mission 流程、Story 剧情 |

---

## 二、端口标签详解

### 2.1 标签定义

NekoGraph 使用两个属性标签来标记端口字段：

```csharp
/// <summary>
/// 输入端口标签 - 标记字段为输入端口喵~
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class InPortAttribute : Attribute
{
    public int Index { get; }           // 端口索引
    public string PortName { get; }     // 端口名称
    public NekoPortCapacity Capacity { get; }  // 端口容量

    public InPortAttribute(int index, string portName, NekoPortCapacity capacity)
    {
        Index = index;
        PortName = portName;
        Capacity = capacity;
    }
}

/// <summary>
/// 输出端口标签 - 标记字段为输出端口喵~
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class OutPortAttribute : Attribute
{
    public int Index { get; }
    public string PortName { get; }
    public NekoPortCapacity Capacity { get; }

    public OutPortAttribute(int index, string portName, NekoPortCapacity capacity)
    {
        Index = index;
        PortName = portName;
        Capacity = capacity;
    }
}
```

### 2.2 标签参数详解

#### 2.2.1 Index（端口索引）

**作用**：
1. 决定端口在编辑器中的显示顺序
2. 用于 `ConnectionData.FromPortIndex` 和 `ConnectionData.ToPortIndex`
3. 输入端口和输出端口的索引**分别独立计数**

**规则**：
- 从 0 开始递增
- 同一方向的端口索引不能重复
- 索引可以不连续（但建议连续）

**示例**：
```csharp
// 正确：索引连续
[InPort(0, "输入 1", NekoPortCapacity.Multi)]
public List<string> Input1;

[InPort(1, "输入 2", NekoPortCapacity.Multi)]
public List<string> Input2;

// 正确：索引不连续但有效
[InPort(0, "主输入", NekoPortCapacity.Multi)]
public List<string> MainInput;

[InPort(5, "备用输入", NekoPortCapacity.Multi)]
public List<string> BackupInput;

// 错误：索引重复
[InPort(0, "输入 1", NekoPortCapacity.Multi)]
public List<string> Input1;

[InPort(0, "输入 2", NekoPortCapacity.Multi)]  // ❌ 索引重复！
public List<string> Input2;
```

#### 2.2.2 PortName（端口名称）

**作用**：
1. 在编辑器中显示为端口的标签
2. 帮助开发者识别端口用途

**命名建议**：
- 简洁明了，不超过 10 个字符
- 使用中文或英文，保持统一
- 体现端口用途

**示例**：
```csharp
// 好的命名
[OutPort(0, "输出", NekoPortCapacity.Multi)]
[OutPort(1, "进度输出", NekoPortCapacity.Multi)]
[InPort(0, "输入", NekoPortCapacity.Multi)]
[InPort(0, "信号经入", NekoPortCapacity.Multi)]

// 不好的命名
[OutPort(0, "", NekoPortCapacity.Multi)]  // ❌ 空字符串
[OutPort(0, "这是一个非常长的端口名称", NekoPortCapacity.Multi)]  // ❌ 太长
```

#### 2.2.3 Capacity（端口容量）

**定义**：
```csharp
public enum NekoPortCapacity
{
    Single,  // 单连接：只能连接一个目标端口
    Multi    // 多连接：可以连接多个目标端口
}
```

**选择规则**：

| 场景 | 推荐容量 | 字段类型 |
|------|----------|----------|
| 单一后继节点 | Single | `string` |
| 多个后继节点 | Multi | `List<string>` |
| 条件分支输出 | Multi | `List<string>` |
| 汇聚多个输入 | Multi | `List<string>` |

**示例**：
```csharp
// 单连接：Root 节点只有一个输出
[OutPort(0, "开始流程", NekoPortCapacity.Single)]
public string NextNodeID;

// 多连接：Trigger 节点可以输出到多个节点
[OutPort(0, "输出", NekoPortCapacity.Multi)]
public List<string> OutputNodeIDs;

// 多连接：Trigger 节点有进度输出
[OutPort(1, "进度输出", NekoPortCapacity.Multi)]
public List<string> ProgressOutputs;
```

### 2.3 标签使用完整示例

```csharp
[Serializable]
public class TriggerNodeData : BaseNodeData
{
    // ==================== 数据字段 ====================

    [Tooltip("触发器数据")]
    public TriggerData Trigger = new TriggerData();

    [Tooltip("当前累积进度")]
    public double CurrentAmount;

    [Tooltip("目标进度阈值")]
    public double RequiredAmount = 1;

    // ==================== 端口字段 ====================

    /// <summary>
    /// 输入端口 - 索引 0，多连接
    /// 接收来自前置节点的信号
    /// </summary>
    [Tooltip("输入节点 ID 列表（多对一）")]
    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    /// <summary>
    /// 输出端口 - 索引 0，多连接
    /// 条件满足时触发信号到后续节点
    /// </summary>
    [Tooltip("输出节点 ID 列表（一对多）")]
    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();

    /// <summary>
    /// 输出端口 - 索引 1，多连接
    /// 每次进度变化都触发信号
    /// </summary>
    [Tooltip("进度输出节点 ID 列表")]
    [OutPort(1, "进度输出", NekoPortCapacity.Multi)]
    public List<string> ProgressOutputs = new List<string>();
}
```

---

## 三、字段类型详解

### 3.1 string 字段

#### 3.1.1 基本用法

```csharp
[Serializable]
public class MyNodeData : BaseNodeData
{
    // 普通数据字段
    public string NodeName;
    public string Description;
    public string Category;

    // 作为单连接端口字段
    [OutPort(0, "输出", NekoPortCapacity.Single)]
    public string NextNodeID = "";
}
```

#### 3.1.2 初始化规则

```csharp
// ✅ 正确：声明时初始化
public string NextNodeID = "";

// ⚠️ 可以：在构造函数中初始化（但 NodeData 通常不用构造函数）
public string NextNodeID;

// ❌ 错误：使用 null（序列化后可能变成 null）
public string NextNodeID = null;  // 不要这样写
```

#### 3.1.3 使用场景

| 场景 | 示例 |
|------|------|
| 存储节点 ID（单连接） | `NextNodeID` |
| 存储文本数据 | `NodeName`, `Description` |
| 存储标识符 | `ProcessID`, `TechID` |

#### 3.1.4 注意事项

1. **空字符串 vs null**：始终初始化为空字符串 `""`，不要使用 `null`
2. **序列化**：string 字段会被自动序列化
3. **复制**：直接赋值即可，不需要深拷贝

### 3.2 List<string> 字段

#### 3.2.1 基本用法

```csharp
[Serializable]
public class MyNodeData : BaseNodeData
{
    // 作为多连接端口字段
    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();

    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();
}
```

#### 3.2.2 初始化规则

**铁律**：`List<string>` 字段**必须**在声明时初始化！

```csharp
// ✅ 正确：声明时初始化
public List<string> OutputNodeIDs = new List<string>();

// ❌ 错误：未初始化，会导致 NullReferenceException
public List<string> OutputNodeIDs;

// ❌ 错误：初始化为 null
public List<string> OutputNodeIDs = null;
```

**原因**：
1. Unity 序列化系统不会自动初始化集合
2. `BaseGraphView.CollectConnections()` 会直接访问字段
3. 未初始化会导致空引用异常

#### 3.2.3 添加和移除连接

```csharp
public class MyNodeData : BaseNodeData
{
    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();

    // 添加连接
    public void AddConnection(string targetNodeID)
    {
        if (!string.IsNullOrEmpty(targetNodeID) && !OutputNodeIDs.Contains(targetNodeID))
        {
            OutputNodeIDs.Add(targetNodeID);
        }
    }

    // 移除连接
    public void RemoveConnection(string targetNodeID)
    {
        OutputNodeIDs.Remove(targetNodeID);
    }

    // 清空连接
    public void ClearConnections()
    {
        OutputNodeIDs.Clear();
    }
}
```

#### 3.2.4 遍历连接

```csharp
// 方式 1：foreach 遍历
foreach (var targetNodeID in OutputNodeIDs)
{
    if (string.IsNullOrEmpty(targetNodeID)) continue;
    // 处理目标节点
}

// 方式 2：for 遍历（需要索引时）
for (int i = 0; i < OutputNodeIDs.Count; i++)
{
    var targetNodeID = OutputNodeIDs[i];
    // 处理目标节点
}

// 方式 3：LINQ 遍历
OutputNodeIDs
    .Where(id => !string.IsNullOrEmpty(id))
    .ToList()
    .ForEach(id => { /* 处理目标节点 */ });
```

#### 3.2.5 使用场景

| 场景 | 示例 |
|------|------|
| 多个后继节点 | `OutputNodeIDs` |
| 多个前置节点 | `InputNodeIDs` |
| 进度输出 | `ProgressOutputs` |
| 参数列表 | `Trigger.Parameters` |

#### 3.2.6 注意事项

1. **必须初始化**：`new List<string>()`
2. **检查 null**：访问前检查 `if (list != null)`
3. **检查空 ID**：遍历时检查 `if (!string.IsNullOrEmpty(id))`
4. **避免重复**：添加前检查 `if (!list.Contains(id))`
5. **深拷贝**：CopyFrom 时使用 `new List<string>(other.list)`

### 3.3 string vs List<string> 对比

| 特性 | string | List<string> |
|------|--------|--------------|
| **连接数** | 单个（Single） | 多个（Multi） |
| **容量标签** | `NekoPortCapacity.Single` | `NekoPortCapacity.Multi` |
| **初始化** | `= ""` | `= new List<string>()` |
| **添加连接** | `field = targetID` | `field.Add(targetID)` |
| **移除连接** | `field = ""` | `field.Remove(targetID)` |
| **遍历** | 直接使用 | foreach/for |
| **CopyFrom** | `field = other.field` | `field = new List<string>(other.field)` |
| **典型用途** | Root 节点输出、单一后继 | Trigger 输出、多个后继 |

---

## 四、底层原理

### 4.1 端口生成流程

```
┌─────────────────────────────────────────────────────────────────┐
│                    BaseNode 构造函数                              │
│                                                                  │
│  1. 调用 base(data) 注入 BaseNodeData                           │
│  2. 调用 GeneratePortsFromMetadata()                            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│              BaseNode.GeneratePortsFromMetadata()               │
│                                                                  │
│  1. 使用反射获取 Data 类型的所有字段                              │
│  2. 筛选带 [InPort] 标签的字段 → inputPortFields                 │
│  3. 筛选带 [OutPort] 标签的字段 → outputPortFields               │
│  4. 按 Index 排序                                                 │
│  5. 为每个字段创建 Port UI 元素                                   │
│     - 将 NekoPortCapacity 转换为 Port.Capacity                   │
│     - 添加到 inputContainer 或 outputContainer                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**核心代码分析**：

```csharp
protected void GeneratePortsFromMetadata()
{
    if (Data == null) return;

    // 如果已经长出端口了，就别再长一次了喵~
    if (inputContainer.childCount > 0 || outputContainer.childCount > 0) return;

    var type = Data.GetType();
    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    // 收集输入端口字段
    var inputPortFields = new List<(int Index, string Name, NekoPortCapacity Capacity, FieldInfo Field)>();
    var outputPortFields = new List<(int Index, string Name, NekoPortCapacity Capacity, FieldInfo Field)>();

    foreach (var field in fields)
    {
        // 检查 [InPort] 标签
        var inPortAttr = field.GetCustomAttribute<InPortAttribute>();
        if (inPortAttr != null)
        {
            inputPortFields.Add((inPortAttr.Index, inPortAttr.PortName, inPortAttr.Capacity, field));
        }

        // 检查 [OutPort] 标签
        var outPortAttr = field.GetCustomAttribute<OutPortAttribute>();
        if (outPortAttr != null)
        {
            outputPortFields.Add((outPortAttr.Index, outPortAttr.PortName, outPortAttr.Capacity, field));
        }
    }

    // 按索引排序
    inputPortFields = inputPortFields.OrderBy(x => x.Index).ToList();
    outputPortFields = outputPortFields.OrderBy(x => x.Index).ToList();

    // 生成输入端口
    foreach (var portInfo in inputPortFields)
    {
        Port.Capacity unityCapacity = portInfo.Capacity == NekoPortCapacity.Single
            ? Port.Capacity.Single
            : Port.Capacity.Multi;

        var port = InstantiatePort(Orientation.Horizontal, Direction.Input, unityCapacity, typeof(bool));
        port.portName = portInfo.Name;
        inputContainer.Add(port);
        InputPorts.Add(port);
    }

    // 生成输出端口
    foreach (var portInfo in outputPortFields)
    {
        Port.Capacity unityCapacity = portInfo.Capacity == NekoPortCapacity.Single
            ? Port.Capacity.Single
            : Port.Capacity.Multi;

        var port = InstantiatePort(Orientation.Horizontal, Direction.Output, unityCapacity, typeof(bool));
        port.portName = portInfo.Name;
        outputContainer.Add(port);
        OutputPorts.Add(port);
    }
}
```

### 4.2 连线同步流程

```
┌─────────────────────────────────────────────────────────────────┐
│              BaseGraphView.SerializeToPack()                    │
│                                                                  │
│  遍历 NodeMap.Values，对每个节点执行：                            │
│  1. node.UpdateData() - 同步 UI 控件的值                          │
│  2. SyncNodePositionToData(node) - 同步位置信息                  │
│  3. CollectConnections(node) - 从画布读取连线并回写到字段          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│              BaseGraphView.CollectConnections()                 │
│                                                                  │
│  1. 遍历 node.outputContainer 的所有 Port                        │
│  2. 对每个 Port，遍历其 connections（Edge 列表）                  │
│  3. 获取目标节点和目标端口索引                                   │
│  4. 创建 ConnectionData 对象                                     │
│  5. 添加到 connections 列表                                      │
│  6. 调用 SyncConnectionsToFields(data, connections)             │
│  7. 更新 data.OutputConnections = connections                    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│          BaseGraphView.SyncConnectionsToFields()                │
│                                                                  │
│  1. 按端口索引分组连线                                           │
│  2. 遍历所有带 [OutPort] 标签的字段                               │
│  3. 根据字段类型同步数据：                                        │
│     - List<string>: list.Clear(); list.AddRange(targetIds)      │
│     - string: field.SetValue(data, targetIds.FirstOrDefault())  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**核心代码分析**：

```csharp
protected List<ConnectionData> CollectConnections(BaseNode node)
{
    var connections = new List<ConnectionData>();
    var data = node.Data;

    // 遍历输出容器的每一个 Port
    int portIndex = 0;
    foreach (var element in node.outputContainer.Children())
    {
        if (element is Port outputPort)
        {
            // 遍历该 Port 连出去的所有 Edge
            foreach (var edge in outputPort.connections)
            {
                var inputNode = edge.input.node;
                if (inputNode is BaseNode targetNode && targetNode.Data != null)
                {
                    var targetNodeId = targetNode.Data.NodeID;
                    if (!string.IsNullOrEmpty(targetNodeId))
                    {
                        int toPortIndex = GetPortIndexFromContainer(targetNode.inputContainer, edge.input);

                        connections.Add(new ConnectionData(
                            portIndex,      // FromPortIndex
                            targetNodeId,   // TargetNodeID
                            toPortIndex     // ToPortIndex
                        ));
                    }
                }
            }
            portIndex++;
        }
    }

    // 回写到 [OutPort] 字段
    SyncConnectionsToFields(data, connections);

    // 同时更新 OutputConnections 列表
    data.OutputConnections = connections;

    return connections;
}

private void SyncConnectionsToFields(BaseNodeData data, List<ConnectionData> connections)
{
    var type = data.GetType();

    // 按端口索引分组连线
    var connectionsByPortIndex = connections.GroupBy(c => c.FromPortIndex)
        .ToDictionary(g => g.Key, g => g.Select(c => c.TargetNodeID).ToList());

    // 遍历所有带 [OutPort] 标签的字段
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        var outPortAttr = field.GetCustomAttribute<OutPortAttribute>();
        if (outPortAttr == null) continue;

        var portIndex = outPortAttr.Index;

        // 获取该端口的所有目标 ID
        if (!connectionsByPortIndex.TryGetValue(portIndex, out var targetIds))
        {
            targetIds = new List<string>();
        }

        // 处理 List<string> 字段
        if (field.FieldType == typeof(List<string>))
        {
            var list = field.GetValue(data) as List<string>;
            if (list == null)
            {
                list = new List<string>();
                field.SetValue(data, list);
            }
            else
            {
                list.Clear();
            }
            list.AddRange(targetIds);
        }
        // 处理 string 字段
        else if (field.FieldType == typeof(string))
        {
            field.SetValue(data, targetIds.FirstOrDefault() ?? "");
        }
    }
}
```

### 4.3 连线恢复流程

```
┌─────────────────────────────────────────────────────────────────┐
│              BaseGraphView.PopulateFromPack()                   │
│                                                                  │
│  1. 清空画布和 NodeMap                                           │
│  2. 遍历 pack.Nodes，创建并添加节点                              │
│  3. 调用 RestoreConnections()                                    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│              BaseGraphView.RestoreConnections()                 │
│                                                                  │
│  调用静态工具方法 RestoreConnectionsHelper<TG, TP>              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│          RestoreConnectionsHelper<TG, TP>()                     │
│                                                                  │
│  1. 遍历 NodeMap 中的所有节点                                     │
│  2. 对每个节点，遍历 data.OutputConnections                      │
│  3. 根据 ConnectionData 创建 Edge 连接                            │
│  4. 调用 SetInPortFieldValue 设置目标节点的 [InPort] 字段          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**核心代码分析**：

```csharp
protected static void RestoreConnectionsHelper<TG, TP>(
    TG graph,
    Dictionary<string, BaseNode> nodeMap)
    where TG : BaseGraphView<TP>
    where TP : BasePackData
{
    foreach (var kvp in nodeMap)
    {
        var node = kvp.Value;
        var data = node.Data;

        if (data.OutputConnections == null || data.OutputConnections.Count == 0) continue;

        foreach (var conn in data.OutputConnections)
        {
            if (string.IsNullOrEmpty(conn.TargetNodeID)) continue;
            if (!nodeMap.TryGetValue(conn.TargetNodeID, out var targetNode)) continue;

            // 获取输出端口
            var outputPort = GetPortByIndex(node, conn.FromPortIndex, Direction.Output);
            if (outputPort == null) continue;

            // 获取输入端口
            var inputPort = GetPortByIndex(targetNode, conn.ToPortIndex, Direction.Input);
            if (inputPort == null) continue;

            // 创建连线
            var edge = outputPort.ConnectTo(inputPort);
            graph.AddElement(edge);

            // 设置目标节点的 [InPort(ToPortIndex)] 字段值
            SetInPortFieldValue(targetNode.Data, conn.ToPortIndex, node.Data.NodeID);
        }
    }
}

private static void SetInPortFieldValue(BaseNodeData data, int portIndex, string sourceNodeID)
{
    var type = data.GetType();
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        var inPortAttr = field.GetCustomAttribute<InPortAttribute>();
        if (inPortAttr == null || inPortAttr.Index != portIndex) continue;

        // 处理 List<string> 字段
        if (field.FieldType == typeof(List<string>))
        {
            var list = field.GetValue(data) as List<string>;
            if (list == null)
            {
                list = new List<string>();
                field.SetValue(data, list);
            }
            if (!list.Contains(sourceNodeID))
            {
                list.Add(sourceNodeID);
            }
        }
        // 处理 string 字段
        else if (field.FieldType == typeof(string))
        {
            field.SetValue(data, sourceNodeID);
        }
        break;
    }
}
```

### 4.4 OutputConnections 与 [OutPort] 字段的关系

**重要概念**：

| 字段 | 位置 | 作用 | 更新时机 |
|------|------|------|----------|
| `BaseNodeData.OutputConnections` | 基类字段 | 存储连线的统一格式 | `CollectConnections()` 自动填充 |
| `[OutPort]` 字段 | 子类字段 | 编辑器端口生成、数据同步 | `CollectConnections()` 通过反射同步 |

**两者关系**：

```
┌─────────────────────────────────────────────────────────────────┐
│                    CollectConnections()                         │
│                                                                  │
│  从画布读取连线 → 生成 List<ConnectionData>                     │
│                                                                  │
│         ↓                        ↓                              │
│  ┌──────────────────┐  ┌──────────────────┐                     │
│  │ data.OutputCon-  │  │ [OutPort] 字段     │                     │
│  │ nections         │  │ (通过反射同步)     │                     │
│  │ (直接赋值)       │  │                  │                     │
│  └──────────────────┘  └──────────────────┘                     │
│                                                                  │
│  两者内容一致，但用途不同：                                       │
│  - OutputConnections: 运行时使用，统一格式                       │
│  - [OutPort] 字段：编辑器使用，类型安全                          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**注意事项**：

1. **不要手动修改** `OutputConnections`，由系统自动管理
2. **不要**用 `[OutPort]` 标签装饰 `OutputConnections`
3. **必须定义** `[OutPort]` 字段，否则编辑器不会生成端口
4. **两者保持一致**，由 `CollectConnections()` 自动同步

---

## 五、完整示例分析

### 5.1 RootNodeData - 流程根节点

```csharp
/// <summary>
/// 流程根节点 - 整个流程树的起始锚点（全图唯一）喵~
/// 用于 Mission 或 Story 系统的流程起点
/// </summary>
[Serializable]
public class RootNodeData : BaseNodeData
{
    // ==================== 端口字段 ====================

    /// <summary>
    /// 输出端口 - 索引 0，多连接
    /// 指向流程的第一个节点（通常是 Spine 节点）
    /// </summary>
    [OutPort(0, "开始流程", NekoPortCapacity.Multi)]
    public List<string> _;  // 字段名无所谓，端口名称更重要

    // ==================== 说明 ====================
    // Root 节点没有输入端口，因为它是流程的起点
    // Root 节点没有数据字段，因为它是纯结构节点
}
```

**分析**：
- **用途**：流程树的根节点，全图唯一
- **端口**：只有一个输出端口，多连接
- **字段**：没有数据字段，只有端口字段
- **特点**：字段名用 `_`，因为端口名称更重要

### 5.2 SpineNodeData - 流程主干节点

```csharp
/// <summary>
/// 流程 ID 节点 (Spine) - 定义流程的逻辑骨架（阶段/步骤）喵~
/// 作为无线输电继电器，通过 ID 关联到 Leaf A 和 B 节点，进行信号同步
/// </summary>
[Serializable]
public class SpineNodeData : BaseNodeData
{
    // ==================== 数据字段 ====================

    /// <summary>
    /// 流程 ID（与 Leaf 节点共享）
    /// 用于关联 Spine 节点和 Leaf 节点
    /// </summary>
    [Tooltip("流程 ID")]
    public string ProcessID;

    // ==================== 端口字段 ====================

    /// <summary>
    /// 输入端口 - 索引 0，多连接
    /// 接收来自父节点 Spine 的信号
    /// </summary>
    [InPort(0, "信号输入", NekoPortCapacity.Multi)]
    [Tooltip("父节点 SpineID")]
    public List<string> ParentSpineID;

    /// <summary>
    /// 输出端口 - 索引 0，多连接
    /// 发送信号到下一个 Spine 节点
    /// </summary>
    [OutPort(0, "信号输出", NekoPortCapacity.Multi)]
    [Tooltip("下一个 Spine 节点的 ID 列表")]
    public List<string> NextSpineNodeIDs = new List<string>();

    // ==================== 说明 ====================
    // Spine 节点是流程的中继点
    // 输入：来自父 Spine 的信号
    // 输出：到子 Spine 的信号
    // 数据：ProcessID 用于关联 Leaf 节点
}
```

**分析**：
- **用途**：流程的中继节点，定义流程骨架
- **端口**：一个输入端口，一个输出端口，都是多连接
- **数据**：`ProcessID` 用于关联 Leaf 节点
- **特点**：输入和输出端口都使用 `List<string>`

### 5.3 LeafNode_A_Data - 执行节点

```csharp
/// <summary>
/// 叶 ID 节点 A (LeafA) - 处理具体的执行演出喵~
/// </summary>
[Serializable]
public class LeafNode_A_Data : BaseNodeData
{
    // ==================== 数据字段 ====================

    /// <summary>
    /// 流程 ID（与 Spine 节点共享）
    /// </summary>
    [Tooltip("流程 ID")]
    public string ProcessID;

    // ==================== 端口字段 ====================

    /// <summary>
    /// 输出端口 - 索引 0，多连接
    /// 执行完成后发送信号到后续节点
    /// </summary>
    [OutPort(0, "信号输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIds = new List<string>();

    // ==================== 说明 ====================
    // LeafA 节点没有输入端口，因为信号通过 ProcessID 关联
    // LeafA 节点由 Spine 节点触发，执行具体操作
}
```

**分析**：
- **用途**：执行具体操作的节点
- **端口**：只有输出端口，没有输入端口
- **数据**：`ProcessID` 用于关联 Spine 节点
- **特点**：信号通过 ProcessID 关联，不是通过连线

### 5.4 LeafNode_B_Data - 回调节点

```csharp
/// <summary>
/// 叶 ID 节点 B (LeafB) - 处理执行完毕的回调喵~
/// </summary>
[Serializable]
public class LeafNode_B_Data : BaseNodeData
{
    // ==================== 数据字段 ====================

    /// <summary>
    /// 流程 ID（与 Spine 节点共享）
    /// </summary>
    [Tooltip("流程 ID")]
    public string ProcessID;

    // ==================== 端口字段 ====================

    /// <summary>
    /// 输入端口 - 索引 0，多连接
    /// 等待 LeafA 执行完成后接收信号
    /// </summary>
    [InPort(0, "等待输入", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIds = new List<string>();

    // ==================== 说明 ====================
    // LeafB 节点只有输入端口，没有输出端口
    // LeafB 节点由 LeafA 触发，发送回调信号到 Spine
}
```

**分析**：
- **用途**：处理执行完毕的回调
- **端口**：只有输入端口，没有输出端口
- **数据**：`ProcessID` 用于关联 Spine 节点
- **特点**：与 LeafA 配对使用

### 5.5 TriggerNodeData - 触发器节点

```csharp
/// <summary>
/// 触发器节点数据 - Mission 和 Story 系统共用喵~
/// </summary>
[Serializable]
public class TriggerNodeData : BaseNodeData
{
    // ==================== 数据字段 ====================

    /// <summary>
    /// 触发器数据（事件名 + 参数列表）
    /// </summary>
    [Tooltip("触发器数据")]
    public TriggerData Trigger = new TriggerData();

    /// <summary>
    /// 当前累积进度（运行时使用）
    /// </summary>
    [Tooltip("当前累积进度")]
    public double CurrentAmount;

    /// <summary>
    /// 目标进度阈值（达到时从主输出端口触发）
    /// </summary>
    [Tooltip("目标进度阈值")]
    public double RequiredAmount = 1;

    // ==================== 端口字段 ====================

    /// <summary>
    /// 输入端口 - 索引 0，多连接
    /// 接收来自前置节点的信号
    /// </summary>
    [Tooltip("输入节点 ID 列表（多对一）")]
    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    /// <summary>
    /// 输出端口 - 索引 0，多连接
    /// 条件满足时触发信号到后续节点
    /// </summary>
    [Tooltip("输出节点 ID 列表（一对多）")]
    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();

    /// <summary>
    /// 输出端口 - 索引 1，多连接
    /// 每次进度变化都触发信号
    /// </summary>
    [Tooltip("进度输出节点 ID 列表")]
    [OutPort(1, "进度输出", NekoPortCapacity.Multi)]
    public List<string> ProgressOutputs = new List<string>();

    // ==================== CopyFrom 方法 ====================

    /// <summary>
    /// 从另一个节点数据复制基础字段喵~
    /// </summary>
    public new void CopyFrom(TriggerNodeData other)
    {
        base.CopyFrom(other);
        if (other == null) return;

        Trigger = new TriggerData();
        Trigger.EventName = other.Trigger.EventName;
        Trigger.Parameters = new List<string>(other.Trigger.Parameters);
        Trigger.HasTriggered = other.Trigger.HasTriggered;

        InputNodeIDs = new List<string>(other.InputNodeIDs);
        OutputNodeIDs = new List<string>(other.OutputNodeIDs);
        ProgressOutputs = new List<string>(other.ProgressOutputs);

        CurrentAmount = other.CurrentAmount;
        RequiredAmount = other.RequiredAmount;
    }
}
```

**分析**：
- **用途**：监听事件、检查条件、触发信号
- **端口**：一个输入端口，两个输出端口（主输出、进度输出）
- **数据**：`TriggerData`、`CurrentAmount`、`RequiredAmount`
- **特点**：多输出端口设计，支持进度追踪

### 5.6 CommandNodeData - 命令节点

```csharp
/// <summary>
/// 命令节点数据 - Mission 和 Story 系统共用喵~
/// </summary>
[Serializable]
public class CommandNodeData : BaseNodeData
{
    // ==================== 数据字段 ====================

    /// <summary>
    /// 命令数据（命令名 + 参数）
    /// </summary>
    public CommandData Command = new CommandData();

    // ==================== 端口字段 ====================

    /// <summary>
    /// 输入端口 - 索引 0，多连接
    /// 接收来自前置节点的信号
    /// </summary>
    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    /// <summary>
    /// 输出端口 - 索引 0，多连接
    /// 执行完成后发送信号到后续节点
    /// </summary>
    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();
}
```

**分析**：
- **用途**：执行具体命令（生成单位、修改资源等）
- **端口**：一个输入端口，一个输出端口
- **数据**：`CommandData` 包含命令名和参数
- **特点**：简洁设计，专注于命令执行

### 5.7 TechNodeData - 科技树节点

```csharp
/// <summary>
/// 科技节点数据 - Lab 科技树系统专用喵~
/// </summary>
[Serializable]
public class TechNodeData : BaseNodeData
{
    // ==================== 数据字段 ====================

    [Header("基本信息")]
    [Tooltip("科技唯一 ID")]
    public string TechID;

    [Tooltip("科技名称")]
    public string TechName;

    [Tooltip("科技描述")]
    [TextArea(3, 5)]
    public string Description;

    [Tooltip("科技图标")]
    public Sprite Icon;

    [Tooltip("科技类型")]
    public TechType TechType;

    [Header("解锁奖励")]
    [Tooltip("解锁后执行的命令")]
    public CommandData UnlockReward = new CommandData();

    // ==================== 端口字段 ====================

    /// <summary>
    /// 输入端口 - 索引 0，多连接
    /// 前置科技信号经入
    /// </summary>
    [Tooltip("前置科技信号经入")]
    [InPort(0, "信号经入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    /// <summary>
    /// 输出端口 - 索引 0，多连接
    /// 信号经出到后续节点
    /// </summary>
    [Tooltip("信号经出到后续节点")]
    [OutPort(0, "信号经出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();
}
```

**分析**：
- **用途**：科技树节点，显示科技信息和状态
- **端口**：一个输入端口，一个输出端口
- **数据**：丰富的科技信息（ID、名称、描述、图标、类型、奖励）
- **特点**：信号纯透传，不阻塞

### 5.8 VFSNodeData - VFS 节点（重构目标）

```csharp
/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSNodeData - VFS 统一节点数据类喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 设计哲学：
/// - 继承 BaseNodeData，复用 NodeID、EditorPosition、OutputConnections
/// - 使用 Name + Extension 区分用途（类似 Linux 文件）
/// - Extension 为空 = 目录，Extension 不为空 = 文件
///
/// 示例：
/// - Name="social", Extension=""      → 目录 /social/
/// - Name="friends", Extension=""     → 目录 /social/friends/
/// - Name="list", Extension=".json"   → 文件 /social/friends/list.json
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[Serializable]
[NodeType(NodeSystem.Common)]
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

    // ==================== 数据内容 ====================

    /// <summary>
    /// 数据内容（JSON 格式）
    /// 目录可为空，文件必须有数据
    /// </summary>
    [Tooltip("数据（JSON 格式）")]
    [TextArea(4, 8)]
    public string DataJson;

    // ==================== 元数据 ====================

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

    // ==================== 端口字段 ====================

    /// <summary>
    /// 输出端口 - 索引 0，多连接
    /// 目录节点使用，指向子节点
    /// 文件节点不使用此端口（叶子节点）
    /// </summary>
    [Tooltip("子节点连接")]
    [OutPort(0, "子节点", NekoPortCapacity.Multi)]
    public List<string> ChildNodeIDs = new List<string>();

    // ==================== 只读属性 ====================

    /// <summary>
    /// 是否是目录（根据 Extension 计算）
    /// </summary>
    public bool IsDirectory => string.IsNullOrEmpty(Extension);

    /// <summary>
    /// 是否是文件（根据 Extension 计算）
    /// </summary>
    public bool IsFile => !string.IsNullOrEmpty(Extension);

    // ==================== CopyFrom 方法 ====================

    /// <summary>
    /// 从另一个节点数据复制字段
    /// </summary>
    public new void CopyFrom(VFSNodeData other)
    {
        if (other == null) return;
        base.CopyFrom(other);
        Name = other.Name;
        Extension = other.Extension;
        DataJson = other.DataJson;
        IsEnabled = other.IsEnabled;
        Description = other.Description;
        ChildNodeIDs = new List<string>(other.ChildNodeIDs);
    }
}
```

**分析**：
- **用途**：VFS 文件树节点，目录和文件统一类型
- **端口**：一个输出端口（目录节点使用）
- **数据**：`Name`、`Extension`、`DataJson`、`IsEnabled`、`Description`
- **特点**：通过 `Extension` 区分目录和文件

---

## 六、实战指南

### 6.1 设计新节点的步骤

**第一步：确定节点用途**
- 节点在流程中扮演什么角色？
- 是数据节点还是流程节点？
- 是运行时节点还是静态结构节点？

**第二步：确定数据字段**
- 节点需要存储哪些数据？
- 哪些数据需要序列化？
- 哪些数据是运行时临时状态？

**第三步：确定端口设计**
- 需要几个输入端口？
- 需要几个输出端口？
- 每个端口的容量是 Single 还是 Multi？

**第四步：选择字段类型**
- 单连接：使用 `string`
- 多连接：使用 `List<string>`
- 数据：根据类型选择

**第五步：添加标签**
- 为端口字段添加 `[InPort]` 或 `[OutPort]` 标签
- 确保索引正确、名称清晰、容量合适

**第六步：实现 CopyFrom**
- 调用 `base.CopyFrom(other)`
- 复制所有自定义字段
- `List<string>` 字段使用 `new List<string>(other.field)`

**第七步：创建编辑器节点**
- 继承 `BaseNode<T>`
- 添加 `[NodeMenuItem]` 和 `[NodeType]` 标签
- 实现 UI 初始化和 `UpdateData()` 方法

### 6.2 检查清单

在提交新节点类型之前，请检查以下项目：

**数据类检查**：
- [ ] 是否继承了 `BaseNodeData`？
- [ ] 是否添加了 `[Serializable]` 标签？
- [ ] 是否添加了 `[NodeType]` 标签？
- [ ] 端口字段是否有 `[InPort]`/`[OutPort]` 标签？
- [ ] `List<string>` 字段是否初始化了？
- [ ] 是否实现了 `CopyFrom` 方法？

**编辑器类检查**：
- [ ] 是否继承了 `BaseNode<T>`？
- [ ] 是否添加了 `[NodeMenuItem]` 标签？
- [ ] 是否实现了无参构造函数？
- [ ] 是否实现了带参数的构造函数？
- [ ] 是否实现了 `UpdateData()` 方法？

### 6.3 命名规范

**类命名**：
- 数据类：`XxxNodeData`（如 `TriggerNodeData`）
- 编辑器类：`XxxNode`（如 `TriggerNode`）

**字段命名**：
- 输入端口：`InputNodeIDs`、`ParentNodeIDs`
- 输出端口：`OutputNodeIDs`、`ChildNodeIDs`、`NextNodeIDs`
- 数据字段：使用 PascalCase，添加 `Tooltip`

**端口命名**：
- 输入：`"输入"`、`"信号经入"`、`"前置"`
- 输出：`"输出"`、`"信号经出"`、`"后继"`

---

## 七、常见错误与调试

### 7.1 常见错误

#### 错误 1：忘记初始化 List<string>

**症状**：`NullReferenceException`

**错误代码**：
```csharp
[OutPort(0, "输出", NekoPortCapacity.Multi)]
public List<string> OutputNodeIDs;  // ❌ 未初始化
```

**修复**：
```csharp
[OutPort(0, "输出", NekoPortCapacity.Multi)]
public List<string> OutputNodeIDs = new List<string>();  // ✅
```

#### 错误 2：忘记写 CopyFrom

**症状**：复制粘贴节点后数据丢失

**错误代码**：
```csharp
// ❌ 没有 CopyFrom 方法
```

**修复**：
```csharp
public new void CopyFrom(MyNodeData other)
{
    base.CopyFrom(other);
    // 复制所有字段
}
```

#### 错误 3：端口索引重复

**症状**：端口显示顺序混乱，连线错误

**错误代码**：
```csharp
[InPort(0, "输入 1", NekoPortCapacity.Multi)]
public List<string> Input1;

[InPort(0, "输入 2", NekoPortCapacity.Multi)]  // ❌ 索引重复
public List<string> Input2;
```

**修复**：
```csharp
[InPort(0, "输入 1", NekoPortCapacity.Multi)]
public List<string> Input1;

[InPort(1, "输入 2", NekoPortCapacity.Multi)]  // ✅ 索引递增
public List<string> Input2;
```

#### 错误 4：容量与类型不匹配

**症状**：只能连接一个目标，但期望多个

**错误代码**：
```csharp
[OutPort(0, "输出", NekoPortCapacity.Single)]
public List<string> OutputNodeIDs;  // ❌ 容量不匹配
```

**修复**：
```csharp
[OutPort(0, "输出", NekoPortCapacity.Multi)]
public List<string> OutputNodeIDs;  // ✅
```

#### 错误 5：忘记添加端口标签

**症状**：编辑器不生成端口

**错误代码**：
```csharp
public List<string> OutputNodeIDs;  // ❌ 没有标签
```

**修复**：
```csharp
[OutPort(0, "输出", NekoPortCapacity.Multi)]
public List<string> OutputNodeIDs;  // ✅
```

### 7.2 调试技巧

#### 技巧 1：检查端口是否生成

在编辑器中选中节点，检查：
- 输入容器是否有端口？
- 输出容器是否有端口？
- 端口名称是否正确？
- 端口顺序是否正确？

#### 技巧 2：检查连线是否同步

序列化后检查 JSON：
- `OutputConnections` 是否有数据？
- `[OutPort]` 字段是否有数据？
- 两者是否一致？

#### 技巧 3：检查数据是否序列化

保存后重新加载，检查：
- 节点数据是否恢复？
- 连线是否恢复？
- 端口连接是否正确？

#### 技巧 4：使用调试日志

在关键位置添加日志：
```csharp
Debug.Log($"[MyNodeData] OutputNodeIDs.Count = {OutputNodeIDs.Count}");
foreach (var id in OutputNodeIDs)
{
    Debug.Log($"  - {id}");
}
```

---

## 八、附录

### 8.1 相关类型索引

| 类型 | 位置 | 说明 |
|------|------|------|
| `BaseNodeData` | `Runtime/Base/` | 节点数据基类 |
| `BasePackData` | `Runtime/Base/` | 数据包基类 |
| `ConnectionData` | `Runtime/Base/` | 连线数据结构 |
| `InPortAttribute` | `Runtime/Attributes/` | 输入端口标签 |
| `OutPortAttribute` | `Runtime/Attributes/` | 输出端口标签 |
| `NodeTypeAttribute` | `Runtime/Attributes/` | 节点系统类型标签 |
| `NodeMenuItemAttribute` | `Runtime/Attributes/` | 节点菜单标签 |
| `BaseNode` | `Editor/_Base/` | 编辑器节点基类 |
| `BaseGraphView` | `Editor/_Base/` | 编辑器画布基类 |

### 8.2 参考示例

| 示例 | 位置 | 说明 |
|------|------|------|
| `TriggerNodeData` | `Runtime/Common/` | 触发器节点数据 |
| `CommandNodeData` | `Runtime/Common/` | 命令节点数据 |
| `TechNodeData` | `Runtime/Tech/` | 科技树节点数据 |
| `RootNodeData` | `Runtime/Common/` | 流程根节点数据 |
| `SpineNodeData` | `Runtime/Common/` | 流程主干节点数据 |
| `TriggerNode` | `Editor/` | 触发器节点编辑器 |
| `CommandNode` | `Editor/` | 命令节点编辑器 |
| `TechNode` | `Editor/Lab/` | 科技树节点编辑器 |

### 8.3 修订历史

| 版本 | 日期 | 修订内容 |
|------|------|----------|
| 1.0 | 2026-03-15 | 初始版本（不合格） |
| 2.0 | 2026-03-15 | 完整版，500 行以上 |

---

**文档结束**
