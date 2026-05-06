# Command 系统重构文档

**日期**: 2026-03-11  
**作者**: Catgirl Programmer (=^･ω･^=)  
**标签**: #Command #重构 #反射 #架构优化

---

## 📋 目录

1. [重构背景](#重构背景)
2. [问题分析](#问题分析)
3. [重构方案](#重构方案)
4. [实现细节](#实现细节)
5. [使用指南](#使用指南)
6. [架构对比](#架构对比)
7. [文件清单](#文件清单)

---

## 🎯 重构背景

### 原有架构的问题

在重构之前，Command 系统存在以下问题：

1. **执行逻辑重复定义**
   - `CommandExecutor.cs` 中定义了一套完整的 Handler 实现
   - `CommandRegistry.cs` 中用 Lambda 又定义了一套执行逻辑
   - 修改一个命令需要同时修改两处，容易遗漏

2. **元数据与执行逻辑分离**
   - `CommandRegistryInfo.cs` 手动维护命令元数据（显示名、分类、参数等）
   - 添加新命令需要在 `CommandRegistry.cs` 写执行逻辑，再在 `CommandRegistryInfo.cs` 注册元数据
   - 容易忘记同步更新，导致编辑器 UI 显示错误

3. **调用入口不统一**
   - `DeveloperConsole` 调用 `CommandRegistry.Register*Commands()`
   - `CommandNodeStrategy` 创建临时 Console 执行
   - `CommandExecutor` 用自己的 Handler 执行
   - 三个调用点，代码分散

### 重构目标

1. ✅ **执行逻辑只定义一次** —— 不再重复
2. ✅ **元数据与执行逻辑在一起** —— 用 Attribute 标记
3. ✅ **统一调用入口** —— 所有调用都走 `CommandRegistry.Execute()`
4. ✅ **自动注册** —— 用反射扫描，无需手动维护注册表

---

## 🔍 问题分析

### 原有架构的问题代码示例

```csharp
// ❌ CommandExecutor.cs 中定义一次
private void RegisterInternalHandlers()
{
    RegisterHandler("spawn", SpawnHandler);
}

private CommandResult SpawnHandler(string[] parameters)
{
    // 执行逻辑 A
    EntitySystem.Instance.CreateEntityFromBlueprint(...);
}

// ❌ CommandRegistry.cs 中又定义一次
public static void RegisterEntityCommands(DeveloperConsole console)
{
    console.AddCommand("spawn", (args) =>
    {
        // 执行逻辑 B（重复！）
        EntitySystem.Instance.CreateEntityFromBlueprint(...);
    });
}

// ❌ CommandRegistryInfo.cs 中还要再注册一次元数据
RegisterCommand("spawn", "🏗️ 召唤单位", "Entity", ...);
```

### 核心矛盾

一个命令需要定义"三个维度"的信息：

| 维度 | 说明 | 原有位置 |
|------|------|----------|
| 执行逻辑 | 如何执行这个命令 | `CommandExecutor` + `CommandRegistry` (重复!) |
| 元数据 | 显示名、分类、参数、提示 | `CommandRegistryInfo` |
| 触发方式 | 控制台/流程图/代码调用 | 分散在三个调用点 |

---

## 💡 重构方案

### 方案 A：方法组 + 反射（已采用）⭐

**核心思路**：
1. 每个命令是一个**静态方法**
2. 用 `[CommandInfo]` Attribute 标记，包含所有元数据
3. 运行时用**反射**自动扫描并注册
4. 所有调用点统一走 `CommandRegistry.Execute()`

**代码示例**：

```csharp
[CommandInfo("spawn", "🏗️ 召唤单位", "Entity", 
    new[] { "BlueprintID", "Position", "Team" },
    Tooltip = "在指定位置召唤单个单位喵~",
    Color = "0.2,0.6,0.2")]
public static CommandResult Spawn(DeveloperConsole console, string[] args)
{
    // 执行逻辑只写一次！
    if (args.Length < 3) return CommandResult.Failed;
    
    string blueprintId = args[0];
    Vector2Int pos = ParseGridPos(args[1]);
    int team = int.Parse(args[2]);
    
    EntitySystem.Instance.CreateEntityFromBlueprint(blueprintId, pos, team);
    return CommandResult.Success;
}
```

---

## 🛠️ 实现细节

### 1. CommandInfoAttribute 特性类

**文件**: `Assets/Scripts/InStage/UI/CommandInfoAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class CommandInfoAttribute : Attribute
{
    public string Name { get; set; }          // 命令内部名
    public string DisplayName { get; set; }   // 显示名
    public string Category { get; set; }      // 分类
    public string[] Parameters { get; set; }  // 参数名列表
    public string Tooltip { get; set; }       // 提示信息
    public string Color { get; set; }         // 编辑器颜色 (R,G,B)
    
    public Color ParsedColor { get; }         // 解析后的 Color 对象
}
```

### 2. CommandRegistry 核心类

**文件**: `Assets/Scripts/InStage/UI/CommandRegistry.cs` + `CommandRegistry.Metadata.cs`

#### 执行逻辑定义（CommandRegistry.cs）

所有命令都是静态方法，用 `[CommandInfo]` 标记：

```csharp
public static partial class CommandRegistry
{
    [CommandInfo("spawn", "🏗️ 召唤单位", "Entity", ...)]
    public static CommandResult Spawn(DeveloperConsole console, string[] args)
    {
        // 执行逻辑
    }
    
    [CommandInfo("army", "🏗️ 方阵召唤", "Entity", ...)]
    public static CommandResult Army(DeveloperConsole console, string[] args)
    {
        // 执行逻辑
    }
    
    // ... 所有命令
}
```

#### 反射自动注册（CommandRegistry.Metadata.cs）

```csharp
public static partial class CommandRegistry
{
    public delegate CommandResult CommandHandler(DeveloperConsole console, string[] args);
    
    private static Dictionary<string, CommandHandler> _commandHandlers;
    private static Dictionary<string, CommandInfoAttribute> _commandMetadatas;
    
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        // 反射扫描所有带 [CommandInfo] 的静态方法
        var type = typeof(CommandRegistry);
        var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        
        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<CommandInfoAttribute>();
            if (attr != null)
            {
                // 创建委托
                var handler = (CommandHandler)Delegate.CreateDelegate(typeof(CommandHandler), method);
                _commandHandlers[attr.Name.ToLower()] = handler;
                _commandMetadatas[attr.Name.ToLower()] = attr;
            }
        }
    }
    
    /// <summary>
    /// 统一的命令执行入口
    /// </summary>
    public static CommandResult Execute(string commandName, string[] args, DeveloperConsole console = null)
    {
        Initialize();
        
        if (_commandHandlers.TryGetValue(commandName.ToLower(), out var handler))
        {
            return handler.Invoke(console, args);
        }
        
        return CommandResult.Failed;
    }
}
```

### 3. CommandExecutor 委托执行

**文件**: `Assets/Scripts/InStage/System/CommandExecutor.cs`

```csharp
public class CommandExecutor : SingletonMono<CommandExecutor>
{
    public CommandRegistry.CommandResult ExecuteCommand(CommandData command)
    {
        // 委托给 CommandRegistry 执行
        return CommandRegistry.Execute(command.CommandName, command.Parameters?.ToArray(), null);
    }
    
    // 保留队列管理功能
    private Queue<CommandData> _commandQueue;
    private void ProcessQueue() { ... }
}
```

### 4. CommandRegistryInfo 从 Attribute 读取

**文件**: `Assets/Scripts/InStage/UI/CommandRegistryInfo.cs`

```csharp
public static class CommandRegistryInfo
{
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        // 从 CommandRegistry 的 [CommandInfo] 自动读取元数据
        var metadatas = CommandRegistry.GetAllMetadatas();
        
        foreach (var kvp in metadatas)
        {
            var attr = kvp.Value;
            // 转换为 CommandInfo 对象
            _commands[attr.Name] = new CommandInfo
            {
                CommandName = attr.Name,
                DisplayName = attr.DisplayName,
                Category = attr.Category,
                ParameterNames = attr.Parameters,
                Tooltip = attr.Tooltip,
                EditorColor = attr.ParsedColor
            };
        }
    }
}
```

### 5. CommandNodeStrategy 简化

**文件**: `Assets/Scripts/Common/NekoGraph/Runtime/Strategies/CommandTriggerStrategies.cs`

```csharp
public class CommandNodeStrategy : INodeStrategy
{
    public void OnSignalEnter(BaseNodeData data, SignalContext context, RuntimeGraphInstance instance)
    {
        if (data is CommandNodeData commandNode)
        {
            // 直接调用统一入口
            CommandRegistry.Execute(commandNode.Command.CommandName, args, null);
        }
    }
}
```

---

## 📖 使用指南

### 添加新命令

只需要**一步**：在 `CommandRegistry.cs` 中添加静态方法 + 贴上 Attribute

```csharp
[CommandInfo("my_command", "✨ 我的命令", "Debug", 
    new[] { "Param1", "Param2" },
    Tooltip = "这是一个新命令喵~",
    Color = "1,0,0")]
public static CommandResult MyCommand(DeveloperConsole console, string[] args)
{
    // 参数验证
    if (args.Length < 2)
    {
        console?.Log("Usage: my_command <param1> <param2>", Color.red);
        return CommandResult.Failed;
    }
    
    // 执行逻辑
    string param1 = args[0];
    int param2 = int.Parse(args[1]);
    
    // ... 你的逻辑
    
    console?.Log($"命令执行成功喵~", Color.green);
    return CommandResult.Success;
}
```

**自动完成**：
- ✅ 命令注册（反射自动扫描）
- ✅ 元数据同步（`CommandRegistryInfo` 自动读取）
- ✅ 控制台可用（`DeveloperConsole` 自动注册）
- ✅ 流程图节点可用（`CommandNode` 自动显示）

### Attribute 参数说明

| 参数 | 必填 | 说明 | 示例 |
|------|------|------|------|
| `name` | ✅ | 命令内部名（用于输入） | `"spawn"` |
| `displayName` | ✅ | 显示名称（UI 展示） | `"🏗️ 召唤单位"` |
| `category` | ✅ | 分类（用于分组） | `"Entity"` |
| `parameters` | ❌ | 参数名数组 | `new[] { "X", "Y" }` |
| `Tooltip` | ❌ | 提示信息 | `"在指定位置召唤单位"` |
| `Color` | ❌ | 编辑器颜色 (R,G,B) | `"0.2,0.6,0.2"` |

### 命令方法签名规范

```csharp
public static CommandResult CommandName(DeveloperConsole console, string[] args)
```

- **返回值**: `CommandResult` (Success/Failed/Skipped/Pending)
- **参数 1**: `DeveloperConsole console` - 用于输出日志（可选，可为 null）
- **参数 2**: `string[] args` - 命令行参数数组

---

## 📊 架构对比

### 重构前

```
┌─────────────────────────────────────────────────────┐
│                  命令定义                            │
├─────────────────────────────────────────────────────┤
│  CommandExecutor.cs  ──┐                            │
│  (Handler 实现)        │ 重复！❌                   │
│  CommandRegistry.cs ───┤                            │
│  (Lambda 实现)         │                            │
├─────────────────────────────────────────────────────┤
│  CommandRegistryInfo.cs                             │
│  (手动注册元数据)        容易遗漏！❌                │
└─────────────────────────────────────────────────────┘

调用点：
- DeveloperConsole → CommandRegistry.Register*Commands()
- CommandNode      → 临时 Console → CommandRegistry
- CommandExecutor  → 自己的 Handler
```

### 重构后

```
┌─────────────────────────────────────────────────────┐
│              CommandRegistry.cs                     │
│  + [CommandInfo] 特性（元数据 + 执行逻辑）           │
│  + 反射自动注册                                     │
│  + Execute() 统一入口                               │
└─────────────────┬───────────────────────────────────┘
                  │
        ┌─────────┼─────────┐
        ▼         ▼         ▼
┌───────────┐ ┌───────┐ ┌──────────┐
│Developer  │ │Command│ │Command   │
│Console    │ │Executor│ │Node      │
└───────────┘ └───────┘ └──────────┘
        │         │         │
        └─────────┴─────────┘
                  │
                  ▼
        CommandRegistry.Execute()
        (唯一执行入口) ✅
```

---

## 📁 文件清单

### 新建文件

| 文件路径 | 说明 |
|----------|------|
| `Assets/Scripts/InStage/UI/CommandInfoAttribute.cs` | 命令特性类 |
| `Assets/Scripts/InStage/UI/CommandRegistry.Metadata.cs` | 反射自动注册逻辑 |

### 修改文件

| 文件路径 | 修改内容 |
|----------|----------|
| `Assets/Scripts/InStage/UI/CommandRegistry.cs` | Lambda → 静态方法 + Attribute |
| `Assets/Scripts/InStage/UI/CommandRegistryInfo.cs` | 手动注册 → 从 Attribute 读取 |
| `Assets/Scripts/InStage/System/CommandExecutor.cs` | 删除 Handler，委托给 CommandRegistry |
| `Assets/Scripts/Common/NekoGraph/Runtime/Strategies/CommandTriggerStrategies.cs` | 简化为调用 Execute() |
| `Assets/Scripts/InStage/UI/DeveloperConsole.cs` | RegisterCommands 简化 |

---

## 🎯 重构收益

| 指标 | 重构前 | 重构后 | 改善 |
|------|--------|--------|------|
| 执行逻辑定义 | 2 处（重复） | 1 处 | ✅ 消除重复 |
| 元数据维护 | 手动注册 | 自动读取 | ✅ 减少遗漏 |
| 添加新命令 | 修改 2 个文件 | 修改 1 个方法 | ✅ 效率提升 |
| 调用入口 | 3 个独立入口 | 1 个统一入口 | ✅ 架构清晰 |

---

## 🐱 喵~ 总结

这次重构解决了 Command 系统长期存在的**重复定义**和**维护困难**问题，主要成果：

1. ✅ **执行逻辑只写一次** —— 静态方法 + Attribute
2. ✅ **元数据自动同步** —— 反射扫描，无需手动注册
3. ✅ **统一调用入口** —— `CommandRegistry.Execute()`
4. ✅ **扩展更方便** —— 添加新命令只需一个方法

现在写新命令就像写普通方法一样简单，贴上 Attribute 就自动注册了喵~！(=^･ω･^=)

---

**相关文档**:
- [MissionManager_TriggerSystem 架构分析.md](./MissionManager_TriggerSystem 架构分析.md)
- [NekoGraph 架构分析.md](../NekoGraph 架构分析.md)
