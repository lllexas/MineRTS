# 工程施工建议 · 2026-03-18

> 当前阶段：Social CLI + NekoGraph Social 对话系统基础功能已完成，TUI 细节已修复。
> 本文梳理**接下来最值得做的事**，按优先级排列。

---

## 📦 已完成现状速览

| 模块 | 状态 |
|------|------|
| NekoGraph 核心（Runner / SignalContext / Strategies） | ✅ 稳定 |
| Social Graph 节点（MsgContent / ChoiceText / MsgEnd） | ✅ 可用 |
| GraphVSF 虚拟文件树 | ✅ 可用 |
| SocialCLI + `ls` / `cd` / `cat` / `pwd` | ✅ 可用 |
| MsgStrategy TUI（对话框 + 选项导航 + Enter 确认） | ✅ 可用 |
| TUI 边框自适应 + CJK 宽度修复 | ✅ 刚修 |
| TUI 模式光标隐藏 + 提示词替换 | ✅ 刚修 |
| 退出 TUI 引导提示 | ✅ 刚修 |
| `SocialManager.SendMessage()` 动态写入 VFS | ✅ 可用 |

---

## 🔴 优先级 1 — 必须做，否则核心流程跑不通

### 1-A  `send` 命令重新接通

**问题**：`send` 触发游戏内发信，但命令本身有时路径错误（主人已排查是参数传法问题）。
需确保 `SocialManager.SendMessage(packID, sender, vfsPath)` 的 VFS 路径最终写到 `/messages/` 下，并且：

- `send` 写入后立即触发 `Social.NewMessageNotification` 事件
- 终端上方（或通知区域）应有 **[NEW]** 提示反馈，让玩家知道信有没有发成功

**关键文件**：`CommandRegistry.Social.cs` → `Send` 命令、`SocialManager.cs`

---

### 1-B  多轮对话支持（SocialWaitNode / SocialDialogueNode）

VFS 里已有 `SocialWaitNodeData` 和 `SocialDialogueNodeData` 的数据定义，
但 `SocialNodeStrategies.cs` 里**没有对应的 Strategy 实现**，也没注册进 `NodeStrategyFactory`。

目前一张图只能有一段正文 + 一次选择就结束；要做多轮，需要：

1. `SocialWaitNodeStrategy`：阻塞信号，等待外部事件（如玩家回信）后才放行
2. `SocialDialogueNodeStrategy`：类似 MsgContent，但支持多段正文串联（"下一页" 逻辑）
3. 把两者注册进 `NodeStrategyFactory`

**参考现有实现**：`TriggerNodeStrategy.cs`（阻塞 + 事件唤醒的完整范例）

---

### 1-C  `MarkAsRead` 持久化

`SocialManager.MarkAsRead(vfsPath)` 目前只在内存里改 `IsRead = true`，
**游戏重启后 [NEW] 标记会复活**。

需要把已读状态写回 VFS 的 `DataJson`（`VFSNodeData.DataJson` 字段），
或单独维护一个 "已读列表" 存进 `SaveManager`。

两种方案各有利弊，建议写回 VFS（改动最小）：

```csharp
// SocialManager.MarkAsRead 里追加：
msgData.IsRead = true;
analyser.WriteFile(SOCIAL_VFS_ID, vfsPath, JsonUtility.ToJson(msgData));
```

---

## 🟡 优先级 2 — 体验提升，有空就做

### 2-A  Graph 编辑器支持 Social 节点

`MissionGraphWindow`（Tools > 猫娘助手）目前可以编辑 Mission 图，
但 Social 图（`SocialPackData` / `MsgPackData`）用的是 **手写 JSON**，没有可视化编辑器支持。

需要在编辑器里：
- 注册 `SocialMsgContentNodeData`、`ChoiceTextNodeData`、`SocialMsgEndNodeData` 的绘制器
- 或者单独做一个 "Social Graph Window"（复用 `MissionGraphWindow` 框架）

这不阻塞游戏功能，但**手写 JSON 出错率高**，内容生产会很痛。

---

### 2-B  `ls` 命令显示增强

当前 `ls` 输出的格式比较朴素：
```
[DIR]  messages/
[FILE] test.msg
```

可以参考真实 terminal 的 `ls -l` 风格，增加：
- **时间戳**（`Timestamp` 字段已有）：`2026-03-18 12:30`
- **发件人**（`Sender` 字段已有）：右侧对齐显示
- **未读数**统计在目录行尾：`messages/  (2 unread)`

实现成本低，观感提升大。关键文件：`CommandRegistry.Social.cs → List()`

---

### 2-C  `send` 命令 Tab 补全 / PackID 提示

现在 `send test_event_01 "指挥官"` 需要玩家记住 PackID 字符串，
可以让 `send` 不带参数时列出所有可用的 PackID（`MetaLib.GetAllMetas()`），
或者支持 Tab 补全（`SocialCLI` 里已有命令注册框架可以扩展）。

---

### 2-D  新消息通知 UI

`Social.NewMessageNotification` 事件已经在 `SocialManager.SendMessage()` 里发射，
但**没有任何 UI 订阅它**。

最简实现：在大地图 UI 上做一个小红点 / Toast 通知，订阅此事件后短暂显示。

---

## 🟢 优先级 3 — 长期/架构方向

### 3-A  对话分支超过 4 个选项

`MsgStrategy.SelectOption()` 里的 `switch` 写死了 1-4，对应 `TriggerEvent.SocialOption1~4`。
如果策划要 5 个以上的选项分支，需要：
- 扩展 `TriggerEvent` 枚举（加 Option5~8 等）
- 或改用动态事件名（`"Social.Option.{index}"`），摆脱枚举硬编码

推荐长期走动态事件名方案，与 `PostOffice` 协议解耦。

---

### 3-B  Social 图与 Mission 图的联动

目前 Social 对话图和 Mission 图是两套独立的 `RuntimeGraphInstance`，互不感知。
游戏叙事里必然有"完成对话 → 解锁任务"或"任务完成 → 触发对话"的需求。

联动方案（推荐用 PostSystem 做桥）：
- Social 图发射自定义事件 → Mission 图的 TriggerNode 监听
- 不需要修改任何核心结构，符合开闭原则

---

### 3-C  VFS 动态加载 / 热更新

目前 VFS 树从 JSON 加载后就是静态的（`VFSLoader.Load`），
`SocialManager.WriteFile` 虽然可以写，但**重启后会从原始 JSON 重新加载**（丢失运行时写入）。

长期需要一个"VFS 差异存档"机制，把运行时的增删改存进 `SaveManager`，
下次启动时 Load + Apply Diff。

> **Update 2026-03-19**: `vfs_load` 命令已修复，优先使用 `PersistentVFSManager.EnsureVFS` 加载。
> 这意味着如果存档系统就绪，`vfs_load` 会尊重存档数据（或自动注册新数据到存档），不再盲目覆盖。

---

## 🔧 最近修复 (Recent Fixes)

### 2026-03-19 VFS 加载逻辑冲突修复
- **问题**：`vfs_load` 命令直接调用 `GraphAnalyser.LoadVFS`，导致手动加载 VFS 时无视存档状态，覆盖进度。
- **修复**：修改 `CommandRegistry.cs` 中的 `VFSLoad` 方法。
  - 优先检查 `NekoGraph.PersistentVFSManager.Instance.IsReady`。
  - 如果就绪，调用 `EnsureVFS(instanceID, packID)`，利用存档数据恢复或注册。
  - 如果未就绪（如调试环境），回退到 `GraphAnalyser.LoadVFS`。
- **影响**：统一了控制台命令与存档系统的加载路径，防止误操作导致进度丢失。

### 2026-03-19 GraphVSF 架构重构：移除 InstanceID
- **目标**：简化架构，消除 `InstanceID` (如 "Social_01") 与 `PackID` (如 "social_tree_default") 共存带来的混淆。
- **变更**：
  - `VFSInstance` 移除 `InstanceID` 字段，统一使用 `PackID`。
  - `GraphAnalyser` 内部字典 Key 改为 `PackID`，所有 API (`GetNode`, `WriteFile` 等) 移除 `instanceID` 参数。
  - `PersistentVFSManager.EnsureVFS` 只接受 `packID`。
  - `SocialCLI` 废弃 `VFSInstanceID`，直接绑定 `VFSPackID`。
  - `CommandRegistry` 的 `vfs_load` 命令简化为 `vfs_load <PackID>`。
- **影响**：代码更清晰，加载路径统一。旧存档可能因为 Key 不匹配而无法读取 VFS 数据（开发阶段可接受）。

---

## 🗂️ 文件速查表

| 想改什么 | 主要文件 |
|---------|---------|
| 对话多轮支持 | `SocialNodeStrategies.cs`、`INodeStrategy.cs` |
| send / MarkAsRead | `CommandRegistry.Social.cs`、`SocialManager.cs` |
| ls 显示增强 | `CommandRegistry.Social.cs → List()` |
| TUI 选项上限 | `MsgStrategy.cs → SelectOption()`、`TriggerEvent.cs` |
| 编辑器 Social 支持 | `MissionGraphWindow.cs` 或新建 `SocialGraphWindow.cs` |
| 通知 UI | 大地图 UI 层（`BigMap/`）+ 订阅 `Social.NewMessageNotification` |
| VFS 持久化 | `SocialManager.cs`、`SaveManager.cs` |

---

> 小喵建议的施工顺序：**1-C → 1-A → 1-B → 2-B → 2-D**
> 先把已有功能闭环（持久化 + 通知），再扩展多轮对话，体验感会有明显跃升喵~
