# 工程施工建议 · 2026-03-21

---

## 🔄 NekoGraph 编辑器层大重构 · 2026-03-21

### 重构痛点

1. **新增 Pack 要改三处**：每新增一种 Pack 类型，必须配套新建 `*GraphView.cs`、`*GraphWindow.cs`、`*SearchWindow.cs`，三个文件重复代码超过 90%，纯粹仪式性负担。

2. **View 子类越权做了 Pack 的工作**：`MissionGraphView.SerializeToPack()` / `PopulateFromPack()` 实质上是序列化逻辑，但序列化属于数据本身（Pack），不属于视图层。职责错位导致修改数据结构时要同时改 View。

3. **SearchWindow 子类唯一区别是一个属性**：5 个 SearchWindow 子类的全部差异只有 `CurrentNodeSystem` 返回不同的枚举值，其余完全相同。这是最典型的过度子类化。

4. **NekoGraphWindow 用反射扫全程序集**：每次打开编辑器窗口都通过反射查找所有 `BaseGraphView`/`BaseSearchWindow` 子类，开销大且脆弱（依赖类名字符串匹配）。

5. **Pack 子类散落「敏捷字段」**：`VFSPackData.BoundMapID` 等字段实质上是节点图中某个节点的字段值的缓存，与 Pack 作为「纯节点容器」的定位矛盾，且消费方要针对具体子类 cast。

### 重构结果

| 删除 | 新增/替换 |
|------|-----------|
| 5 个 `*GraphView` 子类 | `BaseGraphView`（非泛型，无子类）|
| 5 个 `*GraphWindow` 子类 + `NekoGraphWindow` | `PackWindow`（文件驱动，一个窗口走天下）|
| 5 个 `*SearchWindow` 子类 | `NodeSearchWindow`（具体类，从 Pack 取 NodeSystem）|
| Pack 子类散装字段 | `BasePackData.SidePara`（`Dictionary<string,string>`，自动提取）|

**一站式注册原则**：新增 Pack 类型只需实现 `GetNodeSystem()` 一行，其余（窗口、搜索、序列化、SidePara 提取）全部自动。

**SidePara 机制**：在节点字段上标注 `[SideParaKey("key")]`，`BasePackData.OnBeforeSerialize()` 自动扫描所有节点提取到 `SidePara` 字典，运行时按 key 直接读取，无需 cast 到具体 Pack 子类。

---

> 当前阶段：Social CLI + NekoGraph Social 对话系统基础功能已完成，TUI 细节已修复。
> 本文梳理**接下来最值得做的事**，按优先级排列。
> **（2026-03-21 代码审计更新：修正了部分文档与代码不一致的地方喵~）**

---

## 📦 已完成现状速览

| 模块 | 状态 | 验证说明 |
|------|------|----------|
| NekoGraph 核心（Runner / SignalContext / Strategies） | ✅ 稳定 | `NodeStrategyFactory` 已注册全部基础节点策略 |
| Social Graph 节点（MsgContent / ChoiceText / MsgEnd） | ✅ 可用 | 三种策略均已实现并注册 |
| GraphVSF 虚拟文件树 | ✅ 可用 | 基于 `GraphAnalyser` 的 VFS 路径管理 |
| SocialCLI + `ls` / `cd` / `cat` / `pwd` | ✅ 可用 | 集成 VFS，支持表格输出 |
| MsgStrategy TUI（对话框 + 选项导航 + Enter 确认） | ✅ 可用 | `CatStrategies.MsgStrategy` 实现 |
| TUI 边框自适应 + CJK 宽度修复 | ✅ 已完成 | |
| TUI 模式光标隐藏 + 提示词替换 | ✅ 已完成 | |
| 退出 TUI 引导提示 | ✅ 已完成 | |
| `SocialManager.SendMessage()` 动态写入 VFS | ✅ 可用 | 写入后触发 `Social.NewMessageNotification` 事件 |
| `social_send` 命令 | ✅ 可用 | 实现在 `CommandRegistry.cs` 第 466 行 |
| `MarkAsRead` 持久化 | ✅ 已完成 | 已写回 VFS 的 `DataJson` 字段 |

---

## ✅ 已验证完成的功能（文档修正）

### ✅ `social_send` 命令工作正常

**实际命令名**：`social_send`（不是 `send`）

**实现位置**：`CommandRegistry.cs` 第 466-490 行

```csharp
[CommandInfo("social_send", "💬 发送社交消息", "Social", new[] { "PackID", "VFSPath (optional)", "Sender (optional)" },
    Tooltip = "发送一条社交消息给玩家喵~\n示例：social_send event_01 /social/inbox/msg01.msg 指挥官",
    Color = "0.3,0.5,0.8")]
public static CommandOutput SocialSend(DeveloperConsole console, string[] args, object payload)
{
    // 实现完整：参数解析 → SocialManager.SendMessage → 事件触发
}
```

**功能验证**：
- ✅ 写入 VFS 路径正确（`/messages/` 目录）
- ✅ 触发 `Social.NewMessageNotification` 事件（`SocialManager.SendMessage` 第 60 行）
- ✅ `ls` 命令显示 `[NEW]` 标记（`CommandRegistry.Social.cs` 第 143-150 行）

---

### ✅ `MarkAsRead` 持久化已完成

**实现位置**：`SocialManager.cs` 第 71-83 行

```csharp
public void MarkAsRead(string vfsPath)
{
    var analyser = GraphAnalyser.Instance;
    if (analyser == null) return;

    var node = analyser.GetNode(SOCIAL_PACK_ID, vfsPath);
    if (node is VFSNodeData vfs)
    {
        var data = JsonUtility.FromJson<SocialManager.SocialMessageVFSData>(vfs.DataJson);
        if (data != null && !data.IsRead)
        {
            data.IsRead = true;
            vfs.DataJson = JsonUtility.ToJson(data);  // ✅ 写回 VFS
            analyser.WriteFile(SOCIAL_PACK_ID, vfsPath, vfs.DataJson);  // ✅ 持久化
            Debug.Log($"[SocialManager] 消息已标记为已读：{vfsPath}");
        }
    }
}
```

**验证结果**：已读状态会写回 VFS 的 `DataJson` 字段，游戏重启后不会丢失喵~

---

## 🔴 优先级 1 — 必须做，否则核心流程跑不通

### 1-A  多轮对话支持（SocialWaitNode / SocialDialogueNode）

**现状**：VFS 里已有 `SocialWaitNodeData` 和 `SocialDialogueNodeData` 的数据定义，
但 `SocialNodeStrategies.cs` 里**没有对应的 Strategy 实现**，也没注册进 `NodeStrategyFactory`。

目前一张图只能有一段正文 + 一次选择就结束；要做多轮，需要：

1. `SocialWaitNodeStrategy`：阻塞信号，等待外部事件（如玩家回信）后才放行
2. `SocialDialogueNodeStrategy`：支持多选项对话分支
3. 把两者注册进 `NodeStrategyFactory`

**参考现有实现**：`TriggerNodeStrategy.cs`（阻塞 + 事件唤醒的完整范例）

**关键文件**：
- 数据定义：`SocialWaitNodeData.cs`、`SocialDialogueNodeData.cs`
- 策略实现：需新建或扩展现有 `SocialNodeStrategies.cs`
- 注册位置：`NodeStrategyFactory.RegisterDefaultStrategies()`（`NodeStrategy.cs` 第 84-98 行）

---

## 🟡 优先级 2 — 体验提升，有空就做

### 2-A  Graph 编辑器支持 Social 节点

**现状**：`MissionGraphWindow`（Tools > 猫娘助手）目前可以编辑 Mission 图，
但 Social 图（`SocialPackData` / `MsgPackData`）用的是 **手写 JSON**，没有可视化编辑器支持。

需要在编辑器里：
- 注册 `SocialMsgContentNodeData`、`ChoiceTextNodeData`、`SocialMsgEndNodeData` 的绘制器
- 或者单独做一个 "Social Graph Window"（复用 `MissionGraphWindow` 框架）

这不阻塞游戏功能，但**手写 JSON 出错率高**，内容生产会很痛。

**相关文件**：
- `NekoGraph/Editor/Social/SocialGraphView.cs` — Social 图编辑器视图（已存在）
- `NekoGraph/Editor/Social/SocialMsgContentNode.cs` — 节点编辑器（已存在）
- `NekoGraph/Editor/Social/SocialMsgEndNode.cs` — 节点编辑器（已存在）

---

### 2-B  `ls` 命令时间戳显示

**现状**：`ls` 命令已实现表格输出，但时间字段是硬编码的 `"3/19 12:00"`（TODO 项）

```csharp
// CommandRegistry.Social.cs 第 153 行
string time = "3/19 12:00"; // TODO: 从 VFSNodeData 读取实际时间
```

**待做**：从 `VFSNodeData` 的 `Timestamp` 字段读取实际时间并格式化显示。

**已实现功能**：
- ✅ `[NEW]` 标记显示（基于 `IsRead` 字段）
- ✅ 表格样式（边框、颜色、对齐）
- ✅ 文件大小显示

---

### 2-C  `social_send` 命令 PackID 提示

**现状**：`social_send test_event_01 "指挥官"` 需要玩家记住 PackID 字符串。

**建议改进**：
- 不带参数时列出所有可用的 PackID（`MetaLib.GetAllMetas()`）
- 或支持 Tab 补全（`SocialCLI` 里已有命令注册框架可以扩展）

---

### 2-D  新消息通知 UI

**现状**：`Social.NewMessageNotification` 事件已经在 `SocialManager.SendMessage()` 里发射（第 60 行），
但**没有任何 UI 订阅它**。

**最简实现**：在大地图 UI 上做一个小红点 / Toast 通知，订阅此事件后短暂显示。

**相关文件**：
- `SocialRootManager.cs` — Social 根 UI 管理
- `SocialPanelAnimator.cs` — Social 面板动画
- `BigMap/` — 大地图 UI 层

---

## 🟢 优先级 3 — 长期/架构方向

### 3-A  对话分支超过 4 个选项

**现状**：需检查 `MsgStrategy.SelectOption()` 里的 `switch` 是否写死了 1-4，对应 `TriggerEvent.SocialOption1~4`。
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

**现状**：`vfs_load` 命令已修复，优先使用 `PersistentVFSManager.EnsureVFS` 加载。

> **Update 2026-03-19**: `vfs_load` 命令已修复，优先使用 `PersistentVFSManager.EnsureVFS` 加载。
> 这意味着如果存档系统就绪，`vfs_load` 会尊重存档数据（或自动注册新数据到存档），不再盲目覆盖。

长期需要一个"VFS 差异存档"机制，把运行时的增删改存进 `SaveManager`，
下次启动时 Load + Apply Diff。

---

## 🔧 最近修复 (Recent Fixes)

### 2026-03-21 文档审计更新
- **修正**：`social_send` 命令实际工作正常，文档误报为"有问题"
- **修正**：`MarkAsRead` 持久化已完成，文档误报为"待做"
- **验证**：`NodeStrategyFactory` 已注册 3 个 Social 节点策略
- **待做**：`SocialWaitNodeStrategy` 和 `SocialDialogueNodeStrategy` 确实未实现

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
| 多轮对话支持 | `SocialNodeStrategies.cs`、`NodeStrategy.cs` |
| `social_send` 命令 | `CommandRegistry.cs` (466 行)、`SocialManager.cs` |
| `MarkAsRead` 持久化 | `SocialManager.cs` (71-83 行) — 已完成 ✅ |
| `ls` 时间戳显示 | `CommandRegistry.Social.cs → List()` (153 行 TODO) |
| TUI 选项上限 | `CatStrategies/MsgStrategy.cs → SelectOption()` |
| 编辑器 Social 支持 | `NekoGraph/Editor/Social/` 目录下已有基础文件 |
| 通知 UI | 大地图 UI 层（`BigMap/`）+ 订阅 `Social.NewMessageNotification` |
| VFS 持久化 | `SocialManager.cs`、`SaveManager.cs` |

---

## 📋 待办任务清单

- [ ] **实现 `SocialWaitNodeStrategy`** — 等待玩家交互后继续
- [ ] **实现 `SocialDialogueNodeStrategy`** — 多选项对话支持
- [ ] **注册两个新策略到 `NodeStrategyFactory`**
- [ ] **修复 `ls` 命令时间戳显示**（从 TODO 变为实际读取）
- [ ] **实现新消息通知 UI**（订阅事件并显示 Toast/红点）

---

> 💡 **小喵建议的施工顺序**：**多轮对话支持 → `ls` 时间戳 → 通知 UI** 喵~
> 多轮对话是核心功能，优先完成；体验优化可以逐步迭代喵！(=^･ω･^=)
