# NekoGraph 端口标签使用规范

## 第一章：[InPort] 和 [OutPort] 标签详解

### 1.1 标签定义

`[InPort]` 和 `[OutPort]` 是 NekoGraph 框架中用于标记节点数据类端口字段的属性标签。

```csharp
[InPort(int index, string portName, NekoPortCapacity capacity)]
[OutPort(int index, string portName, NekoPortCapacity capacity)]
```

### 1.2 参数说明

| 参数 | 类型 | 说明 |
|------|------|------|
| index | int | 端口索引，从 0 开始，用于排序和识别 |
| portName | string | 端口显示名称，在编辑器中显示 |
| capacity | NekoPortCapacity | 端口容量，Single 或 Multi |

### 1.3 使用方法

```csharp
[Serializable]
public class MyNodeData : BaseNodeData
{
    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();
}
```

### 1.4 NekoPortCapacity 枚举

```csharp
public enum NekoPortCapacity
{
    Single,  // 单连接
    Multi    // 多连接
}
```

### 1.5 端口索引规则

1. 输入端口和输出端口的索引分别独立计数
2. 索引决定端口在编辑器中的显示顺序
3. 索引用于 `ConnectionData.FromPortIndex` 和 `ConnectionData.ToPortIndex`

---

## 第二章：string 字段用法

### 2.1 基本用法

string 字段用于存储单个节点 ID 或其他文本数据。

```csharp
[Serializable]
public class MyNodeData : BaseNodeData
{
    public string NodeName;
    public string Description;
}
```

### 2.2 作为端口字段

string 字段可以作为端口字段，但只能存储单个连接。

```csharp
[OutPort(0, "输出", NekoPortCapacity.Single)]
public string OutputNodeID = "";
```

### 2.3 与 List<string> 的区别

| 特性 | string | List<string> |
|------|--------|--------------|
| 连接数 | 单个 | 多个 |
| 容量 | Single | Multi |
| 初始化 | 空字符串 | new List<string>() |

---

## 第三章：List<string> 字段用法

### 3.1 基本用法

List<string> 字段用于存储多个节点 ID，支持多连接。

```csharp
[OutPort(0, "输出", NekoPortCapacity.Multi)]
public List<string> OutputNodeIDs = new List<string>();
```

### 3.2 初始化规则

1. 必须在声明时初始化
2. 使用 `new List<string>()`
3. 不能为 null

### 3.3 添加和移除连接

```csharp
// 添加连接
OutputNodeIDs.Add(targetNodeID);

// 移除连接
OutputNodeIDs.Remove(targetNodeID);

// 清空连接
OutputNodeIDs.Clear();
```

### 3.4 遍历连接

```csharp
foreach (var targetNodeID in OutputNodeIDs)
{
    // 处理目标节点
}
```

---

## 第四章：CopyFrom 方法

### 4.1 基本结构

```csharp
public new void CopyFrom(MyNodeData other)
{
    base.CopyFrom(other);
    // 复制自定义字段
}
```

### 4.2 string 字段复制

```csharp
Name = other.Name;
```

### 4.3 List<string> 字段复制

```csharp
InputNodeIDs = new List<string>(other.InputNodeIDs);
```

### 4.4 完整示例

```csharp
public new void CopyFrom(MyNodeData other)
{
    base.CopyFrom(other);
    Name = other.Name;
    Description = other.Description;
    InputNodeIDs = new List<string>(other.InputNodeIDs);
    OutputNodeIDs = new List<string>(other.OutputNodeIDs);
}
```

---

## 第五章：完整示例

### 5.1 TriggerNodeData 示例

```csharp
[Serializable]
public class TriggerNodeData : BaseNodeData
{
    [Tooltip("触发器数据")]
    public TriggerData Trigger = new TriggerData();

    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();

    [OutPort(1, "进度输出", NekoPortCapacity.Multi)]
    public List<string> ProgressOutputs = new List<string>();

    public new void CopyFrom(TriggerNodeData other)
    {
        base.CopyFrom(other);
        InputNodeIDs = new List<string>(other.InputNodeIDs);
        OutputNodeIDs = new List<string>(other.OutputNodeIDs);
        ProgressOutputs = new List<string>(other.ProgressOutputs);
    }
}
```

### 5.2 CommandNodeData 示例

```csharp
[Serializable]
public class CommandNodeData : BaseNodeData
{
    public CommandData Command = new CommandData();

    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();
}
```

### 5.3 TechNodeData 示例

```csharp
[Serializable]
public class TechNodeData : BaseNodeData
{
    public string TechID;
    public string TechName;
    public string Description;
    public TechType TechType;
    public CommandData UnlockReward = new CommandData();

    [InPort(0, "信号经入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [OutPort(0, "信号经出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();
}
```

---

## 第六章：常见错误

### 6.1 忘记初始化 List<string>

错误：
```csharp
[OutPort(0, "输出", NekoPortCapacity.Multi)]
public List<string> OutputNodeIDs;  // ❌ null 引用
```

正确：
```csharp
[OutPort(0, "输出", NekoPortCapacity.Multi)]
public List<string> OutputNodeIDs = new List<string>();  // ✅
```

### 6.2 使用错误的容量

错误：
```csharp
[OutPort(0, "输出", NekoPortCapacity.Single)]
public List<string> OutputNodeIDs;  // ❌ 容量不匹配
```

正确：
```csharp
[OutPort(0, "输出", NekoPortCapacity.Multi)]
public List<string> OutputNodeIDs;  // ✅
```

### 6.3 忘记写 CopyFrom

错误：
```csharp
// ❌ 没有 CopyFrom 方法，复制粘贴会丢失数据
```

正确：
```csharp
public new void CopyFrom(MyNodeData other)
{
    base.CopyFrom(other);
    // 复制所有字段
}
```

---

## 第七章：总结

1. `[InPort]` 和 `[OutPort]` 标签用于标记端口字段
2. string 字段用于单连接，List<string> 用于多连接
3. List<string> 必须初始化
4. 必须实现 CopyFrom 方法
5. 参考现有代码学习最佳实践
