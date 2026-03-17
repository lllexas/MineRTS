# Trigger 系统重构需求文档

> 📋 NekoGraph 触发器系统架构重构需求
> 文档版本：1.0.0
> 更新日期：2026-03-16
> 优先级：高

---

## 🎯 项目背景

### 当前 Trigger 系统架构

```
┌─────────────────────────────────────────────────────────────┐
│                    当前 Trigger 系统                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  TriggerData                                                │
│  ├── EventName: string                                      │
│  ├── Parameters: List<string>                               │
│  ├── HasTriggered: bool                                     │
│  └── 职责：跟 PostSystem 打交道（注册/注销监听）              │
│                                                             │
│  TriggerNodeData                                            │
│  ├── Trigger: TriggerData                                   │
│  ├── CurrentAmount: double                                  │
│  ├── RequiredAmount: double                                 │
│  └── 职责：节点数据载体                                      │
│                                                             │
│  TriggerNodeStrategy (单例)                                  │
│  ├── OnSignalEnter: 注册监听                                │
│  ├── ExtractAmountFromPayload: 解析 Payload                 │
│  ├── PropagateSignal: 传播信号                              │
│  └── 职责：跟 GraphRunner 打交道，管理 Trigger 生命周期        │
│                                                             │
│  TriggerRegistry (静态)                                      │
│  ├── 存储：EventName → TriggerTypeInfo                      │
│  ├── 存储：EventName → IMatchEvaluator                      │
│  └── 职责：元数据注册表                                      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 当前问题

#### 1. 硬编码的解析逻辑

**`TriggerNodeStrategy.ExtractAmountFromPayload` 方法：**

```csharp
private double ExtractAmountFromPayload(object payload)
{
    // 硬编码的类型判断
    if (payload is MissionArgs args) return args.Amount;
    if (payload is long l) return l;
    if (payload is int i) return i;
    if (payload is double d) return d;
    if (payload is float f) return f;
    return 1;  // 默认值
}
```

**问题：** 每增加一种新的 Payload 类型，就要修改这个方法。

---

#### 2. 无法复用的比较逻辑

**场景：生命值检测 vs 魔法值检测**

```csharp
// 生命值检测 Trigger
[TriggerType("HealthCheck", "❤️ 生命值检测", ...)]
需要：
  1. 参数类型转换：payload → EntityHandle
  2. 对象验证：检查实体是否存在
  3. 数值比较：entity.Health >= threshold

// 魔法值检测 Trigger
[TriggerType("ManaCheck", "💙 魔法值检测", ...)]
需要：
  1. 参数类型转换：payload → EntityHandle
  2. 对象验证：检查实体是否存在
  3. 数值比较：entity.Mana >= threshold
```

**问题：** 这三步逻辑完全一样，只是取的字段不同！但当前需要写两个独立的 Trigger 实现。

---

#### 3. "上帝思想"设计

**当前 `TriggerNodeStrategy` 一个人干了所有活：**

```csharp
trigger.Register(payload =>
{
    // 1. 参数类型转换
    double amount = ExtractAmountFromPayload(payload);
    
    // 2. 对象验证（匹配检查）
    bool isMatch = _evaluator.Check(payload, parameters);
    
    // 3. 数值比较（进度累积）
    node.CurrentAmount += amount;
    if (node.CurrentAmount >= node.RequiredAmount)
    {
        PropagateSignal(...);
    }
});
```

**问题：** 监听、解析、验证、比较全部耦合在一起，无法复用。

---

#### 4. Trigger 无法像 Command 那样一站式注册

**Command 的一站式注册：**

```csharp
[CommandInfo("spawn", "召唤单位", ...)]
public static CommandOutput Spawn(DeveloperConsole console, string[] args, object payload)
{
    // 完整逻辑都在这里！
}
```

**Trigger 无法做到：**

- ❌ 监听逻辑在 `TriggerData.Register()` 中
- ❌ 业务逻辑在 `TriggerNodeStrategy.RegisterTrigger()` 中
- ❌ 两处代码分散在不同的类里

**原因：** Trigger 是"持续监听"模式，需要状态机，不是简单的函数调用。

---

## 📋 重构目标

### 核心设计理念

**拆分职责：Listener + Comparer**

```
┌─────────────────────────────────────────────────────────────┐
│              Trigger 2.0：Listener + Comparer               │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Listener (监听器) - 负责监听总线事件                        │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 1. 订阅 PostSystem.On(EventName)                     │   │
│  │ 2. 接收 Payload                                      │   │
│  │ 3. 转发给 Comparer 进行判定                           │   │
│  │ 4. 如果匹配，触发回调                                 │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  Comparer (比较器) - 负责数值比较逻辑                        │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 1. 参数类型转换：payload → T                         │   │
│  │ 2. 对象验证：检查对象是否有效                         │   │
│  │ 3. 数值比较：value >= threshold                      │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  复用关系：                                                  │
│  HealthCheck → Listener<PostSystem> + Comparer<Entity, float>│
│  ManaCheck   → Listener<PostSystem> + Comparer<Entity, float>│
│  TimeCheck   → Listener<PostSystem> + Comparer<double, double>│
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

### 具体需求

#### 1️⃣ Listener 抽象（监听器）

**需求描述：**

创建一个独立的 Listener 抽象，负责监听 `PostSystem` 事件。所有 Trigger 共用同一个 Listener 实现。

**接口定义：**

```csharp
public interface IListener
{
    string EventName { get; set; }
    void Register(Action<object> callback);
    void Unregister();
}
```

**实现要求：**

```csharp
public class PostSystemListener : IListener
{
    // 所有 Trigger 共用这个实现
    // 只负责监听和转发，不关心业务逻辑
}
```

**验收标准：**

- ✅ 所有 Trigger 类型共用同一个 `PostSystemListener` 类
- ✅ Listener 不包含任何业务逻辑
- ✅ Listener 可以被多个 Trigger 实例复用

---

#### 2️⃣ Comparer 抽象（比较器）

**需求描述：**

创建一个独立的 Comparer 抽象，负责数值比较逻辑。支持泛型参数，实现高度复用。

**接口定义：**

```csharp
public interface IComparer
{
    bool Check(object payload, IReadOnlyList<string> parameters);
    double ExtractAmount(object payload);
}
```

**通用实现要求：**

```csharp
// 实体数值比较器 - 生命值、魔法值共用
public class EntityValueComparer<TSelector> : IComparer
    where TSelector : IFunc<EntityHandle, float>, new()
{
    // 1. 参数类型转换（统一实现）
    // 2. 对象验证（统一实现）
    // 3. 数值比较：_selector.Invoke(entity) >= threshold
}

// 选择器：取生命值
public class HealthSelector : IFunc<EntityHandle, float> { ... }

// 选择器：取魔法值
public class ManaSelector : IFunc<EntityHandle, float> { ... }
```

**验收标准：**

- ✅ 生命值和魔法值检测共用 `EntityValueComparer`
- ✅ 只需要更换 `Selector` 就可以切换检测目标
- ✅ 类型转换、对象验证逻辑统一实现

---

#### 3️⃣ TriggerRegistry 重构

**需求描述：**

将 `TriggerRegistry` 从"元数据注册表"升级为"Comparer 注册表"。

**接口定义：**

```csharp
public static class TriggerRegistry
{
    // 注册：事件名 → Comparer
    void Register<T>(string eventName, IComparer comparer, TriggerTypeInfo metadata);
    
    // 获取 Comparer
    IComparer GetComparer(string eventName);
    
    // 获取元数据
    TriggerTypeInfo GetTypeInfo(string eventName);
}
```

**注册方式：**

```csharp
[RuntimeInitializeOnLoadMethod]
private static void Initialize()
{
    // 生命值检测
    Register("HealthCheck", 
             new EntityValueComparer<HealthSelector>(),
             metadata);
    
    // 魔法值检测
    Register("ManaCheck", 
             new EntityValueComparer<ManaSelector>(),
             metadata);
    
    // 时间检测
    Register("Time", 
             new TimeComparer(),
             metadata);
}
```

**验收标准：**

- ✅ 支持动态注册新的 Comparer
- ✅ 通过反射自动扫描带 `[TriggerType]` 特性的类（可选）
- ✅ 向后兼容现有的 Trigger 类型

---

#### 4️⃣ TriggerNodeStrategy 重构

**需求描述：**

修改 `TriggerNodeStrategy`，使用 Listener + Comparer 模式，移除硬编码逻辑。

**修改前：**

```csharp
private void RegisterTrigger(TriggerNodeData node, ...)
{
    trigger.Register(payload =>
    {
        // 硬编码的解析逻辑
        double amount = ExtractAmountFromPayload(payload);
        bool isMatch = _evaluator.Check(payload, parameters);
        ...
    });
}
```

**修改后：**

```csharp
private void RegisterTrigger(TriggerNodeData node, ...)
{
    // 1. 创建 Listener（复用）
    var listener = new PostSystemListener { EventName = trigger.EventName };
    
    // 2. 获取 Comparer（从 Registry）
    var comparer = TriggerRegistry.GetComparer(trigger.EventName);
    
    // 3. 注册监听
    listener.Register(payload =>
    {
        // 4. 使用 Comparer 进行判定
        if (comparer?.Check(payload, trigger.Parameters) ?? true)
        {
            double amount = comparer?.ExtractAmount(payload) ?? 1.0;
            node.CurrentAmount += amount;
            
            if (node.CurrentAmount >= node.RequiredAmount)
            {
                PropagateSignal(node, context, instance);
            }
        }
    });
}
```

**验收标准：**

- ✅ 移除 `ExtractAmountFromPayload` 硬编码方法
- ✅ 使用 `TriggerRegistry.GetComparer()` 获取比较器
- ✅ 使用 `PostSystemListener` 进行事件监听
- ✅ 代码量减少 30% 以上

---

#### 5️⃣ 新增 Trigger 类型示例

**需求描述：**

提供新增 Trigger 类型的标准流程文档和示例代码。

**新增流程：**

```csharp
// 步骤 1：创建 Selector（如果需要）
public class ArmorSelector : IFunc<EntityHandle, float>
{
    public float Invoke(EntityHandle entity)
    {
        return EntitySystem.Instance.GetArmor(entity);
    }
}

// 步骤 2：注册 Comparer（在 TriggerRegistry.Initialize 中）
Register("ArmorCheck", 
         new EntityValueComparer<ArmorSelector>(),
         metadata);

// 完成！不需要修改其他代码
```

**验收标准：**

- ✅ 提供标准流程文档
- ✅ 提供至少 3 个示例（生命值、魔法值、护甲值）
- ✅ 新人可以在 10 分钟内完成新 Trigger 类型的添加

---

## 📊 技术指标

### 代码复用率

| 指标 | 当前 | 目标 |
|------|------|------|
| Listener 复用率 | 0% (每个 Trigger 各自实现) | 100% (共用 PostSystemListener) |
| Comparer 复用率 | 0% (每个 Trigger 各自实现) | 80% (共用 EntityValueComparer) |
| 硬编码逻辑行数 | ~200 行 | < 50 行 |

### 扩展性指标

| 指标 | 当前 | 目标 |
|------|------|------|
| 新增 Trigger 类型所需时间 | 30-60 分钟 | < 10 分钟 |
| 新增 Trigger 类型需修改文件数 | 2-3 个 | 1 个（仅注册） |
| 代码审查通过率 | 60% | > 90% |

### 性能指标

| 指标 | 当前 | 目标 |
|------|------|------|
| Trigger 注册耗时 | < 1ms | < 1ms (不变) |
| Trigger 匹配耗时 | < 0.1ms | < 0.1ms (不变) |
| 内存占用 | 基准 | +5% (可接受) |

---

## 📋 交付物清单

### 核心代码

1. `IListener.cs` - Listener 接口定义
2. `PostSystemListener.cs` - PostSystem 监听器实现
3. `IComparer.cs` - Comparer 接口定义
4. `EntityValueComparer.cs` - 实体数值比较器通用实现
5. `Selector/` - 选择器接口和实现
   - `IFunc.cs` - 选择器接口
   - `HealthSelector.cs` - 生命值选择器
   - `ManaSelector.cs` - 魔法值选择器
6. `TriggerRegistry.cs` - 重构后的注册表
7. `TriggerNodeStrategy.cs` - 重构后的策略类
8. `TriggerTypeInfo.cs` - Trigger 元数据结构

### 文档

1. `Trigger 系统架构设计.md` - 架构设计文档
2. `Trigger 开发指南.md` - 开发者使用指南
3. `Trigger 类型列表.md` - 内置 Trigger 类型清单

### 测试

1. `TriggerSystemTests.cs` - 单元测试
2. `ListenerTests.cs` - Listener 测试
3. `ComparerTests.cs` - Comparer 测试

---

## 🎯 验收标准

### 功能验收

- ✅ 现有的所有 Trigger 类型正常工作
- ✅ 生命值和魔法值检测共用同一套比较逻辑
- ✅ 新增 Trigger 类型只需注册 Comparer
- ✅ 向后兼容现有的 JSON 配置文件

### 代码质量验收

- ✅ 通过代码审查（至少 2 人审核）
- ✅ 单元测试覆盖率 > 80%
- ✅ 无编译警告
- ✅ 符合项目代码规范

### 文档验收

- ✅ 架构设计文档完整
- ✅ 开发指南清晰易懂
- ✅ 示例代码可运行

---

## 📅 项目排期（建议）

| 阶段 | 内容 | 预计时间 |
|------|------|----------|
| **Phase 1** | Listener + Comparer 接口设计 | 2 小时 |
| **Phase 2** | 通用 Comparer 实现 | 4 小时 |
| **Phase 3** | TriggerRegistry 重构 | 4 小时 |
| **Phase 4** | TriggerNodeStrategy 重构 | 4 小时 |
| **Phase 5** | 迁移现有 Trigger 类型 | 4 小时 |
| **Phase 6** | 单元测试和文档 | 4 小时 |
| **合计** | | **22 小时** |

---

## 🎓 技术难点说明

### 难点 1：泛型 Comparer 的设计

**问题：** 如何设计一个通用的 Comparer，支持不同类型的 Payload 和数值？

**建议方案：** 使用泛型 + 选择器模式

```csharp
public class EntityValueComparer<TSelector> : IComparer
    where TSelector : IFunc<EntityHandle, float>, new()
{
    private TSelector _selector = new TSelector();
    
    public bool Check(object payload, IReadOnlyList<string> parameters)
    {
        // 统一实现
    }
}
```

---

### 难点 2：向后兼容性

**问题：** 如何保证重构不影响现有的 Trigger 配置？

**建议方案：**

1. 保留现有的 `TriggerData` 结构
2. 保留现有的 JSON 格式
3. 在 `TriggerRegistry` 中提供默认 Comparer（兜底）

---

### 难点 3：性能优化

**问题：** 引入抽象层后，如何保证性能不下降？

**建议方案：**

1. 使用值类型（struct）实现 Comparer
2. 缓存 Comparer 实例，避免重复创建
3. 使用委托而非虚方法（减少虚调用开销）

---

## 📞 联系方式

如有疑问，请联系项目维护者。

---

**文档版本**: 1.0.0
**创建日期**: 2026-03-16
**维护者**: 猫娘助手开发团队 喵~ (=^･ω･^=)
