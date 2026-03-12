# DeveloperConsole 管道命令使用指南

**日期：** 2026 年 3 月 12 日  
**版本：** v1.0

---

## 📋 快速开始

### 什么是管道命令？

管道命令允许你将多个命令串联起来，**上游命令的输出自动作为下游命令的输入**。

```bash
# 传统方式：需要手动指定每个参数
spawn x_dog 0,0 1
select 123  # 需要知道单位的 ID
move 10,10

# 管道方式：自动传递数据
spawn x_dog 0,0 1 | select | move 10,10
```

---

## 🔧 语法说明

### 基本语法

| 符号 | 作用 | 示例 |
|------|------|------|
| `|` | 管道分隔符 | `cmd1 | cmd2 | cmd3` |
| `;` | 命令分隔符（独立执行） | `cmd1; cmd2; cmd3` |
| 空格 | 参数分隔符 | `spawn x_dog 0,0 1` |

### 管道执行流程

```
输入：spawn x_dog 0,0 1 | select | move 10,10

执行步骤：
1. spawn x_dog 0,0 1
   → 输出：EntityHandle { Id = 123 }
   
2. select (payload = EntityHandle)
   → 输出：List<EntityHandle> { 123 }
   
3. move 10,10 (payload = List<EntityHandle>)
   → 输出："moved 1 units"
```

---

## 📚 使用示例

### 示例 1：召唤并选择单位

```bash
# 召唤单位并自动选择
spawn x_dog 0,0 1 | select

# 等价于 Graph 流程：
# CommandNode (spawn) → CommandNode (select)
```

### 示例 2：方阵召唤并移动

```bash
# 召唤方阵并移动到指定位置
army x_dog 0,0 3,3 1 | move 50,50

# 执行流程：
# 1. army 召唤 9 个单位 → 输出 List<EntityHandle>
# 2. move 接收单位列表 → 移动到 (50,50)
```

### 示例 3：多命令组合

```bash
# 清空单位 → 召唤新单位 → 选择 → 移动
clear | spawn x_dog 0,0 1 | select | move 10,10

# 注意：clear 的 payload = null，不影响下游
```

### 示例 4：分号 + 管道混合使用

```bash
# 在两个位置分别召唤并移动
spawn x_dog 0,0 1 | select | move 10,10; spawn x_cat 5,5 1 | select | move 20,20
```

---

## 🎯 管道数据传递规则

### Payload 类型

| 命令类型 | Payload 输出 | 下游可接收类型 |
|----------|-------------|---------------|
| `spawn` | `EntityHandle` | `EntityHandle` |
| `army` | `List<EntityHandle>` | `List<EntityHandle>` |
| `ai_wave` | `List<EntityHandle>` | `List<EntityHandle>` |
| `hero` | `EntityHandle` | `EntityHandle` |
| 其他命令 | `null` 或其他 | 根据命令定义 |

### 命令如何处理 Payload

```csharp
// 命令方法签名
public static CommandOutput Select(
    DeveloperConsole console,
    string[] args,
    object payload)  // ← 上游的输出
{
    // 智能判断 payload 类型
    if (payload is EntityHandle handle)
    {
        // 处理单个单位
        SelectEntity(handle);
        return CommandOutput.Success("OK", handle);
    }
    else if (payload is List<EntityHandle> handles)
    {
        // 处理单位列表
        foreach (var h in handles) SelectEntity(h);
        return CommandOutput.Success("OK", handles);
    }
    else if (args.Length > 0)
    {
        // 控制台调用：从 args 解析 ID
        if (int.TryParse(args[0], out int id))
        {
            var h = GetEntityById(id);
            SelectEntity(h);
            return CommandOutput.Success("OK", h);
        }
    }
    
    return CommandOutput.Fail("Invalid entity");
}
```

---

## ⚠️ 注意事项

### 1. 管道失败处理

如果管道中某个命令失败，**后续命令不会执行**：

```bash
# 如果 spawn 失败（如位置被阻挡）
spawn x_dog 999,999 1 | select | move 10,10

# 输出：
# > spawn x_dog 999,999 1 | select | move 10,10
# Pipeline failed at 'spawn': Area 999,999 is blocked!
# （select 和 move 不会执行）
```

### 2. Payload 类型匹配

确保上游输出的类型与下游期望的类型匹配：

```bash
# ✅ 正确：spawn 输出 EntityHandle，select 接收 EntityHandle
spawn x_dog 0,0 1 | select

# ⚠️ 可能失败：clear 输出 null，select 接收 null 可能报错
clear | select
```

### 3. 调试模式

开启调试模式可以看到管道传递的 Payload 类型：

```csharp
// 在 GraphRunner 中设置
GraphRunner.Instance.EnableDebugLog = true;

// 控制台输出：
# > spawn x_dog 0,0 1 | select | move 10,10
# Pipeline: spawn → Payload: EntityHandle
# Pipeline: select → Payload: List<EntityHandle>
# Pipeline: move → Payload: System.String
```

---

## 🔍 常见问题

### Q: 管道和分号有什么区别？

**管道 `|`**：上游输出作为下游输入（数据传递）
```bash
spawn x_dog 0,0 1 | select  # select 接收 spawn 的输出
```

**分号 `;`**：独立执行多个命令（无数据传递）
```bash
spawn x_dog 0,0 1; select 123  # select 需要手动指定 ID
```

### Q: 如何在管道中传递多个参数？

```bash
# 参数用空格分隔
spawn x_dog 0,0 1 | move 10,10

# move 命令接收：
# - args = ["10,10"]
# - payload = 上游的单位数据
```

### Q: 控制台调用和 Graph 调用有什么区别？

| 特性 | 控制台调用 | Graph 调用 |
|------|-----------|-----------|
| payload | `null`（首次） | `context.Args`（上游输出） |
| 参数来源 | 用户输入 | `CommandData.Parameter` |
| 日志输出 | Console | Debug.Log |

---

## 📖 相关文档

- [Command 命令管道系统架构报告.md](./Command 命令管道系统架构报告.md)
- [NekoGraph 架构分析.md](../NekoGraph 架构分析.md)

---

*文档结束喵~* 🐱
