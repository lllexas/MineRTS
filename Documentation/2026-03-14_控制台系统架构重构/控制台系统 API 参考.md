# 控制台系统架构重构 - 2026-03-14

## 📋 重构概述

本次重构移除了 `DeveloperConsole` 的单例模式，改为**组件化 + Inspector 引用**的架构设计。

### 核心设计理念

1. **UI 与逻辑分离** - 面板 UI 不持有逻辑层单例引用
2. **事件驱动输出** - 通过 PostSystem 发送输出事件
3. **组件化架构** - 面板和逻辑组件在同一 GameObject 上
4. **Inspector 引用** - 通过 `[SerializeField]` 拖拽或 `GetComponent` 获取引用

---

## 🏗️ 类继承关系

```
DeveloperConsole : MonoBehaviour
    ↑
    └─ SocialCLI : DeveloperConsole (社交终端特化)

ConsolePanelBase<T> : SpaceUIAnimator
    ├─ DeveloperConsolePanel : ConsolePanelBase<DeveloperConsole>
    └─ SocialPanelAnimator : ConsolePanelBase<SocialCLI>
```

### GameObject 结构

```
GameObject: DeveloperConsole
├─ DeveloperConsolePanel (UI)
└─ DeveloperConsole (Logic)

GameObject: SocialCLI
├─ SocialPanelAnimator (UI)
└─ SocialCLI (Logic)
```

---

## 📦 DeveloperConsole.cs - 核心逻辑层

### 公共 API

| 方法名 | 签名 | 说明 |
|-------|------|------|
| `AddCommand` | `void AddCommand(string key, Action<string[]> action)` | 注册一个命令，key 会自动转为小写 |
| `GetCommandKeys` | `IEnumerable<string> GetCommandKeys()` | 获取所有已注册的命令键 |
| `Log` | `virtual void Log(string message, Color color)` | 输出日志（事件驱动，发送到 PostSystem） |
| `ProcessCommand` | `virtual void ProcessCommand(string input)` | 处理命令字符串（支持分号和管道） |

### 受保护 API（子类专用）

| 方法名 | 签名 | 说明 |
|-------|------|------|
| `FireOutputEvents` | `void FireOutputEvents(string message, Color color)` | 发射输出事件到 PostSystem |

### 数据结构

```csharp
public class ConsoleOutputEvent
{
    public string message;
    public Color color;
}
```

### 命令格式

- **单命令**: `command arg1 arg2`
- **多命令（分号分隔）**: `cmd1 arg1; cmd2 arg2`
- **管道命令**: `cmd1 arg1 | cmd2 arg2`（payload 传递）

### 命令执行结果显示

当命令通过 `AddCommand` 注册后，执行流程如下：

```csharp
console.AddCommand(commandName, (args) =>
{
    // 1. 执行命令，获取 CommandOutput
    var output = handler.Invoke(console, args, null);
    
    // 2. 如果有消息，根据执行结果显示不同颜色
    if (!string.IsNullOrEmpty(output.Message))
    {
        // Success → 绿色，Failed → 红色
        console.Log(output.Message, 
            output.Result == CommandResult.Success ? Color.green : Color.red);
    }
});
```

| CommandResult | 显示颜色 | 说明 |
|--------------|---------|------|
| `Success` | 绿色 | 命令执行成功 |
| `Failed` | 红色 | 命令执行失败 |
| `Skipped` | - | 跳过执行（无消息） |
| `Pending` | - | 等待执行（异步场景） |

---

### 管道命令执行逻辑

管道命令使用 `|` 分隔符，支持在多个命令之间传递 `Payload` 数据。

**执行流程：**

```
输入：cmd1 arg1 | cmd2 arg2 | cmd3 arg3

┌─────────────────────────────────────────────────────────┐
│ 1. 执行 cmd1，payload = null                            │
│    → 获取 output.Payload                                │
│    → 如果 Failed，停止管道                              │
└─────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────┐
│ 2. 执行 cmd2，payload = cmd1 的 output.Payload          │
│    → 获取 output.Payload                                │
│    → 如果 Failed，停止管道                              │
└─────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────┐
│ 3. 执行 cmd3，payload = cmd2 的 output.Payload          │
│    → 获取 output.Payload                                │
│    → 如果 Failed，停止管道                              │
└─────────────────────────────────────────────────────────┘
```

**代码实现（DeveloperConsole.ExecutePipeline）：**

```csharp
private void ExecutePipeline(string input)
{
    Log($"> {input}", Color.cyan);

    string[] parts = input.Split('|');
    object payload = null;  // 初始 payload 为 null

    foreach (var part in parts)
    {
        // 解析命令名和参数
        string commandName = tokens[0].ToLower();
        string[] args = tokens.Skip(1).ToArray();

        // 执行命令，传入上游的 payload
        var output = CommandRegistry.Execute(commandName, args, payload, this);

        // 将输出 Payload 传递给下游
        payload = output.Payload;

        // 如果失败，停止管道
        if (output.Result == CommandRegistry.CommandResult.Failed)
        {
            Log($"Pipeline failed at '{commandName}': {output.Message}", Color.red);
            break;
        }

        // 调试日志（可选）
        if (GraphRunner.Instance != null && GraphRunner.Instance.EnableDebugLog)
        {
            Log($"Pipeline: {commandName} → Payload: {(payload != null ? payload.GetType().Name : "null")}", Color.gray);
        }
    }
}
```

**Payload 传递示例：**

```csharp
// cmd1 返回 Payload
[CommandInfo("cmd1", "命令 1", "Debug")]
public static CommandOutput Cmd1(DeveloperConsole console, string[] args, object payload)
{
    var result = new MyData { value = 42 };
    return CommandOutput.Success("cmd1 完成", payload: result);
}

// cmd2 接收上游 Payload
[CommandInfo("cmd2", "命令 2", "Debug")]
public static CommandOutput Cmd2(DeveloperConsole console, string[] args, object payload)
{
    var data = payload as MyData;  // 接收 cmd1 的 Payload
    // 使用数据进行操作
    return CommandOutput.Success($"cmd2 收到值：{data.value}");
}

// 管道执行：cmd1 | cmd2
// 输出：
// > cmd1 | cmd2
// cmd1 完成（绿色）
// Pipeline: cmd1 → Payload: MyData
// cmd2 收到值：42（绿色）
```

---

## 🛡️ SocialCLI.cs - 社交终端特化

### 公共 API

| 方法名 | 签名 | 说明 |
|-------|------|------|
| `ProcessCommand` | `override void ProcessCommand(string input)` | 安检后执行命令（白名单验证） |
| `Log` | `override void Log(string message, Color color)` | 输出到社交面板（发送 SocialCLI.Output 事件） |

### 公共字段

| 字段名 | 类型 | 说明 |
|-------|------|------|
| `CurrentPath` | `string` | 当前上下文路径（用于 cd 命令），默认 `/social/` |

### 安全机制

- 只允许执行带 `[SocialCommand]` 标记的命令
- 初始化时通过反射扫描建立白名单
- 未通过安检的命令会被拦截并显示警告

---

## 🖼️ DeveloperConsolePanel.cs - 开发终端 UI

### Inspector 配置

| 字段名 | 类型 | 说明 |
|-------|------|------|
| `enableTildeKeyToggle` | `bool` | 是否使用波浪键 (~) 切换面板 |
| `_developerConsole` | `DeveloperConsole` | 逻辑层引用（Inspector 拖拽或 GetComponent） |

### 核心方法

| 方法名 | 说明 |
|-------|------|
| `GetPrompt()` | 返回 `"> "` 提示符 |
| `OnSubmitCommand(string input)` | 提交命令到逻辑层 |
| `Toggle()` | 波浪键切换面板显示/隐藏 |
| `HandleOutput(object data)` | 订阅 `DeveloperConsole.Output` 事件 |

### 生命周期

- `Start()`: 自动获取 `_developerConsole` 引用（如果 Inspector 未赋值）
- `OnEnable()`: 注册 PostSystem 事件
- `OnDisable()`: 注销 PostSystem 事件
- `Update()`: 监听波浪键切换

---

## 🎭 SocialPanelAnimator.cs - 社交终端 UI

### Inspector 配置

| 字段名 | 类型 | 说明 |
|-------|------|------|
| `_socialCLI` | `SocialCLI` | 逻辑层引用（Inspector 拖拽或 GetComponent） |

### 核心方法

| 方法名 | 说明 |
|-------|------|
| `GetPrompt()` | 返回带路径的提示符，如 `"/social> "`（绿色） |
| `OnSubmitCommand(string input)` | 提交命令到社交 CLI |
| `HandleOutput(object data)` | 订阅 `SocialCLI.Output` 事件 |
| `OnShowPanel/OnHidePanel` | SpaceUIAnimator 事件回调 |
| `OnMouseEnterHandler/OnMouseExitHandler` | 鼠标悬停效果 |
| `OnMouseClickHandler` | 点击激活输入框 |

### 生命周期

- `Awake()`: 设置 `_uiID = "SocialPanel"`
- `Start()`: 自动获取 `_socialCLI` 引用，输出欢迎信息
- `OnEnable()`: 注册 PostSystem 事件
- `OnDisable()`: 注销 PostSystem 事件
- `OnDestroy()`: 取消 SpaceUIAnimator 事件订阅

---

## 🔧 CommandRegistry.Metadata.cs - 命令注册表

### 委托类型

```csharp
public delegate CommandOutput CommandHandlerWithOutput(
    DeveloperConsole console, 
    string[] args, 
    object payload
);

public delegate CommandResult CommandHandler(
    DeveloperConsole console, 
    string[] args
);
```

### 枚举类型

```csharp
public enum CommandResult
{
    Success,    // 执行成功
    Failed,     // 执行失败
    Skipped,    // 跳过执行
    Pending     // 等待执行
}
```

### 数据结构

```csharp
public class CommandOutput
{
    public CommandResult Result { get; set; }   // 执行结果
    public string Message { get; set; }         // 日志消息
    public object Payload { get; set; }         // 管道数据（传递给下游命令）
    
    // 静态工厂方法
    public static CommandOutput Success(string message = null, object payload = null);
    public static CommandOutput Fail(string error);
    public static CommandOutput Skip();
    public static CommandOutput Pending();
}
```

### 公共 API

| 方法名 | 签名 | 说明 |
|-------|------|------|
| `RegisterAll` | `void RegisterAll(DeveloperConsole console)` | 注册所有命令到指定控制台 |
| `Execute` | `CommandOutput Execute(string commandName, string[] args, object payload = null, DeveloperConsole console = null)` | 执行单个命令（返回 CommandOutput） |
| `GetAllMetadatas` | `Dictionary<string, CommandInfoAttribute> GetAllMetadatas()` | 获取所有命令元数据 |
| `TryGetMetadata` | `bool TryGetMetadata(string commandName, out CommandInfoAttribute metadata)` | 获取单个命令元数据 |
| `GetAllCommandNames` | `List<string> GetAllCommandNames()` | 获取所有命令名列表 |

### 兼容方法

```csharp
void RegisterEntityCommands(DeveloperConsole console);
void RegisterSystemCommands(DeveloperConsole console);
void RegisterTimeCommands(DeveloperConsole console);
void RegisterCameraCommands(DeveloperConsole console);
```

---

## 🏷️ 事件系统

### DeveloperConsole 事件

| 事件名 | 数据类型 | 说明 |
|-------|---------|------|
| `DeveloperConsole.Output` | `ConsoleOutputEvent` | 控制台输出事件 |

### SocialCLI 事件

| 事件名 | 数据类型 | 说明 |
|-------|---------|------|
| `SocialCLI.Output` | `ConsoleOutputEvent` | 社交终端输出事件（隔离） |

---

## 📝 使用示例

### 1. 注册命令

```csharp
[CommandInfo("spawn", "🏗️ 召唤单位", "Entity", new[] { "BlueprintID", "Position", "Team" })]
public static CommandOutput Spawn(DeveloperConsole console, string[] args, object payload)
{
    // 命令逻辑
    return CommandOutput.Success("单位已召唤");
}
```

### 2. 执行命令

```csharp
// 通过面板输入
// 用户输入：spawn x_dog 0,0 1

// 代码执行
CommandRegistry.Execute("spawn", new[] { "x_dog", "0,0", "1" }, null, console);
```

### 3. 管道命令

```csharp
// 用户输入：cmd1 arg1 | cmd2 arg2
// payload 会在命令之间传递
```

---

## 🔧 重构要点总结

1. **移除单例继承** - `DeveloperConsole` 改为普通 `MonoBehaviour`
2. **Inspector 引用** - 面板通过 `[SerializeField]` 获取逻辑层引用
3. **Fallback 机制** - `Start()` 中自动 `GetComponent` 作为后备
4. **事件隔离** - `DeveloperConsole.Output` 和 `SocialCLI.Output` 独立事件
5. **闭包修复** - `CommandRegistry.Metadata.cs` 中闭包使用传入的 `console` 参数

---

## 📁 相关文件

- `Assets/Scripts/InStage/UI/DeveloperConsole.cs` - 核心逻辑层
- `Assets/Scripts/OutStage/SocialCLI/SocialCLI.cs` - 社交终端逻辑
- `Assets/Scripts/InStage/UI/DeveloperConsolePanel.cs` - 开发终端 UI
- `Assets/Scripts/OutStage/BigMap/UI/Panels/SocialPanelAnimator.cs` - 社交终端 UI
- `Assets/Scripts/InStage/UI/CommandRegistry.Metadata.cs` - 命令注册表
- `Assets/Scripts/InStage/UI/CommandRegistryInfo.cs` - 命令元数据信息
- `Assets/Scripts/InStage/UI/CommandInfoAttribute.cs` - 命令特性定义
