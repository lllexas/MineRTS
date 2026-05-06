# Command 命令管道系统架构报告

**日期：** 2026 年 3 月 12 日  
**作者：** 架构重构小组  
**版本：** v2.0（管道重构版）

---

## 📋 目录

1. [系统概述](#系统概述)
2. [核心架构](#核心架构)
3. [命令管道机制](#命令管道机制)
4. [命令分类与列表](#命令分类与列表)
5. [使用指南](#使用指南)
6. [技术细节](#技术细节)
7. [扩展指南](#扩展指南)

---

## 系统概述

### 设计理念

Command 命令系统是一个**集中式命令执行框架**，用于在 RTS 游戏中执行各种游戏操作。系统采用**管道化设计**，支持命令之间的数据传递，允许构建复杂的命令链。

### 核心特性

| 特性 | 描述 |
|------|------|
| **统一入口** | 所有命令通过 `CommandRegistry.Execute()` 统一执行 |
| **管道支持** | 命令输出可作为下游命令的输入（`Payload` 机制） |
| **双模调用** | 支持控制台调用和 Graph 流程调用 |
| **类型安全** | 命令参数和输出类型明确，支持智能类型判断 |
| **自动注册** | 使用反射自动扫描带 `[CommandInfo]` 特性的方法 |

### 系统边界

```
┌─────────────────────────────────────────────────────────────┐
│                    Command 命令系统                          │
├─────────────────────────────────────────────────────────────┤
│  输入：string[] args + object payload                       │
│  输出：CommandOutput { Result, Message, Payload }           │
│                                                              │
│  调用方：DeveloperConsole（控制台）                          │
│         CommandNode（Graph 流程）                            │
│         CommandExecutor（队列执行）                          │
│                                                              │
│  被调用：EntitySystem、TimeSystem、IndustrialSystem 等      │
└─────────────────────────────────────────────────────────────┘
```

---

## 核心架构

### 组件关系图

```
┌──────────────────┐     ┌───────────────────┐     ┌─────────────────┐
│ DeveloperConsole │     │ CommandNode       │     │ CommandExecutor │
│ (控制台调用)      │     │ (Graph 节点)       │     │ (队列执行)       │
└────────┬─────────┘     └─────────┬─────────┘     └────────┬────────┘
         │                         │                        │
         │ string[] args           │ string[] args          │ string[] args
         │ payload = null          │ payload = context.Args │ payload = null
         ▼                         ▼                        ▼
┌────────────────────────────────────────────────────────────────┐
│                    CommandRegistry.Execute()                   │
│                   统一执行入口喵~                               │
└────────────────────────────────────────────────────────────────┘
         │
         │ 反射调用
         ▼
┌────────────────────────────────────────────────────────────────┐
│                    命令方法集合                                 │
│  Spawn(args, payload) → CommandOutput { Payload = EntityHandle }│
│  Army(args, payload)  → CommandOutput { Payload = List<...> }  │
│  Select(args, payload)→ CommandOutput { Payload = ... }        │
└────────────────────────────────────────────────────────────────┘
```

### 核心类说明

#### 1. `CommandRegistry`（命令注册表）

**职责：** 命令注册、执行、元数据管理

**核心 API：**
```csharp
// 统一执行入口
public static CommandOutput Execute(
    string commandName,     // 命令名（不区分大小写）
    string[] args,          // 字符串参数数组
    object payload = null,  // 管道数据（上游命令的输出）
    DeveloperConsole console = null)  // 控制台（用于日志输出）
```

**委托定义：**
```csharp
public delegate CommandOutput CommandHandlerWithOutput(
    DeveloperConsole console,
    string[] args,
    object payload);
```

#### 2. `CommandOutput`（命令输出）

**职责：** 封装命令执行结果

**结构：**
```csharp
public class CommandOutput
{
    public CommandResult Result { get; set; }   // 执行结果
    public string Message { get; set; }         // 日志消息（给人看的）
    public object Payload { get; set; }         // 管道数据（给下游命令用的）
}
```

**工厂方法：**
```csharp
CommandOutput.Success("操作成功", payloadObject);
CommandOutput.Fail("错误信息");
CommandOutput.Skip();
CommandOutput.Pending();
```

#### 3. `CommandInfoAttribute`（命令信息特性）

**职责：** 标记命令方法，提供元数据

**定义：**
```csharp
[AttributeUsage(AttributeTargets.Method)]
public class CommandInfoAttribute : Attribute
{
    public string Name { get; set; }        // 命令内部名
    public string DisplayName { get; set; } // 显示名
    public string Category { get; set; }    // 分类
    public string[] Parameters { get; set; }// 参数名列表
    public string Tooltip { get; set; }     // 提示信息
    public string Color { get; set; }       // 编辑器颜色 (R,G,B,A)
}
```

**使用示例：**
```csharp
[CommandInfo("spawn", "🏗️ 召唤单位", "Entity", 
    new[] { "BlueprintID", "Position (x,y)", "Team" },
    Tooltip = "在指定位置召唤单个单位喵~",
    Color = "0.2,0.6,0.2")]
public static CommandOutput Spawn(...) { ... }
```

#### 4. `CommandNodeStrategy`（命令节点策略）

**职责：** 在 Graph 流程中执行命令，传递管道数据

**核心逻辑：**
```csharp
public void OnSignalEnter(BaseNodeData data, SignalContext context, ...)
{
    // 1. 构建参数
    var args = BuildCommandArgs(command, context);
    
    // 2. 执行命令（传入上游 Payload）
    var output = CommandRegistry.Execute(cmdName, args, context.Args, null);
    
    // 3. 将输出 Payload 传递给下游
    if (output.Payload != null)
        context.Args = output.Payload;
}
```

---

## 命令管道机制

### 管道数据流

```
┌─────────────┐
│ TriggerNode │  事件触发
└──────┬──────┘
       │ SignalContext.Args = payload
       ▼
┌─────────────┐  args = ["x_dog", "0,0", "1"]
│ CommandNode │  payload = null
│   (spawn)   │
└──────┬──────┘
       │ output.Payload = EntityHandle { Id = 123 }
       │ context.Args = EntityHandle
       ▼
┌─────────────┐  args = []
│ CommandNode │  payload = EntityHandle
│   (select)  │
└──────┬──────┘
       │ output.Payload = List<EntityHandle> { 123 }
       │ context.Args = List<EntityHandle>
       ▼
┌─────────────┐  args = ["10,10"]
│ CommandNode │  payload = List<EntityHandle>
│   (move)    │
└──────┬──────┘
       │ output.Payload = "moved 3 units"
       ▼
    ...继续传播
```

### 管道参数说明

| 参数 | 来源 | 用途 | 示例 |
|------|------|------|------|
| `string[] args` | `CommandData.Parameter` 或控制台输入 | 传递字符串参数 | `["x_dog", "0,0", "1"]` |
| `object payload` | 上游命令的 `output.Payload` | 传递管道数据 | `EntityHandle { Id = 123 }` |

### 命令方法如何处理参数

```csharp
public static CommandOutput Select(DeveloperConsole console, string[] args, object payload)
{
    // payload 是上游命令的输出（可能是 EntityHandle 或 List<EntityHandle>）
    if (payload is EntityHandle handle)
    {
        // 处理单个单位
        SelectEntity(handle);
        return CommandOutput.Success($"Selected entity {handle.Id}", handle);
    }
    else if (payload is List<EntityHandle> handles)
    {
        // 处理单位列表
        foreach (var h in handles) SelectEntity(h);
        return CommandOutput.Success($"Selected {handles.Count} entities", handles);
    }
    else if (args.Length > 0)
    {
        // 控制台调用：从 args 解析
        if (int.TryParse(args[0], out int id))
        {
            var h = GetEntityById(id);
            SelectEntity(h);
            return CommandOutput.Success($"Selected entity {id}", h);
        }
    }
    
    return CommandOutput.Fail("Invalid entity");
}
```

---

## 命令分类与列表

### 命令分类

| 分类 | 标识 | 命令数量 | 描述 |
|------|------|----------|------|
| 🏗️ Entity | `Entity` | 4 | 实体相关（召唤、AI 绑定等） |
| 🔧 System | `System` | 13 | 系统操作（存档、加载、网络等） |
| ⚡ Debug | `Debug` | 6 | 调试功能（作弊、信息等） |
| ⏰ Time | `Time` | 5 | 时间控制（暂停、快进等） |
| 🎬 Story | `Story` | 3 | 剧情相关（CG、对话、解锁） |
| 🔧 Camera | `Debug` | 6 | 相机控制（移动、缩放等） |
| 🖼️ UI | `UI` | 4 | UI 界面（显示/隐藏面板） |
| **总计** | - | **41** | - |

### 完整命令列表

#### 🏗️ Entity 实体相关

| 命令名 | 显示名 | 参数 | Payload 输出 | 描述 |
|--------|--------|------|-------------|------|
| `spawn` | 🏗️ 召唤单位 | BlueprintID, Position, Team | `EntityHandle` | 在指定位置召唤单个单位 |
| `army` | 🏗️ 方阵召唤 | BlueprintID, Center, Size, Team | `List<EntityHandle>` | 以方阵形式召唤多个单位 |
| `ai_wave` | 🏗️ AI 波次绑定 | Team, BrainID, TargetPos | `List<EntityHandle>` | 将单位绑定到 AI 波次逻辑 |
| `hero` | 🏗️ 召唤英雄 | HeroID, Position, Team | `EntityHandle` | 召唤英雄单位 |

#### 🔧 System 系统相关

| 命令名 | 显示名 | 参数 | 描述 |
|--------|--------|------|------|
| `clear` | 🔧 清空单位 | (可选) Team | 清空所有单位或指定阵营单位 |
| `map_load` | 🗺️ 加载地图 | MapID | 加载指定地图数据 |
| `map_apply` | 🗺️ 应用地图 | MapID | 应用地图配置到视觉 |
| `save_new` | 🗺️ 新建存档 | SaveName | 创建新存档 |
| `save_load` | 🗺️ 加载存档 | SaveName | 加载指定存档 |
| `save_ram` | 🗺️ 内存存档 | - | 将当前状态保存到内存 |
| `save_now` | 🗺️ 立即存档 | - | 立即保存到磁盘 |
| `enter` | 🗺️ 进入关卡 | StageID | 进入指定关卡 |
| `leave` | 🗺️ 离开关卡 | - | 离开当前关卡（保存） |
| `leave_force` | 🗺️ 强制离开 | - | 强制离开（不保存） |
| `reset_stage` | 🗺️ 重置关卡 | StageID | 重置关卡到初始状态 |
| `net_rebuild` | 🔧 重建网络 | - | 重建物流网络 |
| `net_info` | 🔧 网络信息 | - | 显示物流网络信息 |

#### ⚡ Debug 调试相关

| 命令名 | 显示名 | 参数 | 描述 |
|--------|--------|------|------|
| `help` | ⚡ 帮助 | (可选) Command | 显示帮助信息 |
| `cheat_gold` | 🔧 金币作弊 | Amount | 获得指定数量金币 |
| `cheat_power` | ⚡ 无限电力 | Enable (0/1) | 开启/关闭无限电力 |
| `nav_info` | 🔧 导航信息 | - | 显示 NavMesh 调试信息 |
| `global_power` | ⚡ 全局电力 | Enable (0/1) | 全局电力覆盖开关 |

#### ⏰ Time 时间相关

| 命令名 | 显示名 | 参数 | 描述 |
|--------|--------|------|------|
| `timer_pause` | ⏰ 暂停时间 | - | 暂停游戏时间 |
| `timer_resume` | ⏰ 恢复时间 | - | 恢复游戏时间 |
| `timer_reset` | ⏰ 重置时间 | - | 重置计时器为 0 |
| `timer_skip` | ⏰ 时间快进 | Seconds | 跳过指定秒数 |

#### 🎬 Story 剧情相关

| 命令名 | 显示名 | 参数 | 描述 |
|--------|--------|------|------|
| `PlayCG` | 🎬 播放 CG | CGName | 播放过场动画 |
| `ShowDialogue` | 🎬 显示对话 | DialogueID, Speaker, Text | 显示剧情对话 |
| `UnlockStage` | 🎬 解锁章节 | StageID | 解锁新的剧情章节 |

#### 🔧 Camera 相机相关

| 命令名 | 显示名 | 参数 | 描述 |
|--------|--------|------|------|
| `cam_home` | 🔧 相机归位 | - | 相机回到地图中心 |
| `cam_goto` | 🔧 相机移动 | Position (x,y) | 相机移动到指定位置 |
| `cam_sync` | 🔧 相机同步 | - | 同步相机边界 |
| `cam_reset` | 🔧 相机重置 | - | 重置相机设置 |
| `cam_speed` | 🔧 相机速度 | Speed | 设置相机移动速度 |
| `cam_scroll` | 🔧 相机滚动 | Enable (0/1) | 开启/关闭相机滚动 |

#### 🖼️ UI 界面相关

| 命令名 | 显示名 | 参数 | 描述 |
|--------|--------|------|------|
| `ui_root` | 🖼️ 进入根界面 | - | 发送"进入根界面"事件 |
| `ui_hide_all` | 🖼️ 隐藏所有面板 | - | 发送"期望隐藏所有面板"事件 |
| `ui_show` | 🖼️ 显示面板 | UI_ID | 发送"期望显示面板"事件 |
| `ui_hide` | 🖼️ 隐藏面板 | UI_ID | 发送"期望隐藏面板"事件 |

---

## 使用指南

### 1. 控制台调用

在 Developer Console 中输入命令：

```bash
# 基础调用
spawn x_dog 0,0 1

# 带参数调用
cheat_gold 1000

# 查看帮助
help spawn
```

### 2. Graph 流程调用

在 NekoGraph 编辑器中创建 CommandNode：

```
1. 右键 → 🔧 通用/命令节点
2. 选择分类 → 选择命令
3. 填写参数
4. 连接到 TriggerNode 或其他节点
```

### 3. 管道调用示例

**场景：** 召唤单位 → 选择单位 → 移动单位

```
CommandNode (spawn)
  参数：x_dog, 0,0, 1
  ↓ (output.Payload = EntityHandle)
CommandNode (select)
  参数：(空，使用上游 payload)
  ↓ (output.Payload = List<EntityHandle>)
CommandNode (move)
  参数：10,10
  ↓ (使用上游 payload + 当前参数)
```

### 4. 代码调用

```csharp
// 直接调用命令
var output = CommandRegistry.Execute("spawn", new[] { "x_dog", "0,0", "1" }, null);

if (output.Result == CommandRegistry.CommandResult.Success)
{
    EntityHandle handle = output.Payload as EntityHandle;
    Debug.Log($"Spawned entity: {handle.Id}");
}
```

---

## 技术细节

### 命令执行流程

```
1. CommandRegistry.Execute() 被调用
   ↓
2. Initialize() 确保已初始化（反射扫描）
   ↓
3. 从 _commandHandlers 查找命令处理器
   ↓
4. 调用 handler.Invoke(console, args, payload)
   ↓
5. 执行命令方法，返回 CommandOutput
   ↓
6. 异常处理（捕获并返回 Fail）
   ↓
7. 返回 CommandOutput 给调用方
```

### 反射扫描机制

```csharp
// 扫描所有带 [CommandInfo] 的方法
var methods = typeof(CommandRegistry).GetMethods(...);

foreach (var method in methods)
{
    var attr = method.GetCustomAttribute<CommandInfoAttribute>();
    if (attr != null && 
        method.ReturnType == typeof(CommandOutput) &&
        Parameters match (DeveloperConsole, string[], object))
    {
        // 创建委托并注册
        var handler = Delegate.CreateDelegate(...);
        _commandHandlers[attr.Name.ToLower()] = handler;
    }
}
```

### Payload 传递机制

```csharp
// CommandNodeStrategy 中
var output = CommandRegistry.Execute(cmdName, args, context.Args, null);

// 如果命令输出了 Payload，传递给下游
if (output.Payload != null)
{
    context.Args = output.Payload;  // ← 关键：写入 SignalContext
}

// 下游节点从 context.Args 读取
// payload 可能是 EntityHandle、List<T> 或其他对象
```

---

## 扩展指南

### 添加新命令

**步骤 1：** 在 `CommandRegistry.cs` 中添加方法

```csharp
[CommandInfo("my_command", "🔧 我的命令", "Category", 
    new[] { "Param1", "Param2" },
    Tooltip = "描述信息喵~",
    Color = "0.5,0.5,0.5")]
public static CommandOutput MyCommand(
    DeveloperConsole console, 
    string[] args, 
    object payload)
{
    // 验证参数
    if (args.Length < 2)
        return CommandOutput.Fail("需要 2 个参数");
    
    // 处理 payload（上游输出）
    if (payload is EntityHandle handle)
    {
        // ...
    }
    
    // 执行逻辑
    // ...
    
    return CommandOutput.Success("操作成功", resultObject);
}
```

**步骤 2：** 编译后自动注册（反射扫描）

**步骤 3：** 在编辑器中使用或控制台调用

### Payload 类型设计

**推荐做法：**

| 命令类型 | 推荐 Payload 类型 |
|----------|------------------|
| 召唤单位 | `EntityHandle` |
| 批量操作 | `List<EntityHandle>` |
| 查询操作 | 查询结果对象 |
| 无输出 | `null` |

**注意事项：**

1. Payload 是 `object` 类型，下游需要类型判断
2. 如果命令没有输出，返回 `null`
3. 尽量保持 Payload 类型一致性（如召唤类命令都返回 `EntityHandle`）

### 与事件系统的区别

| 特性 | 命令系统 | 事件系统 (PostSystem) |
|------|----------|----------------------|
| 调用方式 | 集中式（CommandRegistry） | 分布式（Send/Subscribe） |
| 执行模式 | 同步执行 | 异步监听 |
| 数据传递 | Payload 管道 | 事件参数 |
| 适用场景 | 游戏操作（spawn、clear） | 状态通知（UI 刷新、任务完成） |
| 返回值 | CommandOutput | 无返回值 |

**设计原则：**

- **需要执行操作** → 使用命令系统
- **需要通知多个监听器** → 使用事件系统
- **命令可以触发事件** → 如 `ui_show` 命令发送"期望显示面板"事件

---

## 总结

Command 命令管道系统经过重构后，现已支持：

✅ **统一执行入口** - `CommandRegistry.Execute()`  
✅ **管道数据传递** - `Payload` 机制  
✅ **双模调用** - 控制台 + Graph 流程  
✅ **类型安全** - 明确的参数和输出类型  
✅ **自动注册** - 反射扫描 `[CommandInfo]`  
✅ **41 个可用命令** - 覆盖 Entity、System、Debug、Time、Story、Camera、UI 分类

**下一步扩展方向：**

1. 为更多命令添加 Payload 输出（如查询类命令）
2. 实现参数插值语法（如 `move $(selected_units) 10,10`）
3. 添加命令组合宏（一键执行多个命令）

---

*文档结束喵~* 🐱
