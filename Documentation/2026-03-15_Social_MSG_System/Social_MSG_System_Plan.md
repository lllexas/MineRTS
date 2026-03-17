# 2026-03-15 社交消息系统 (MSG System) 架构设计报告

## 1. 系统概述
本系统旨在基于 `SocialCLI` (模拟终端) 和 `NekoGraph` (剧情图) 实现一套高度沉浸式的、符合 Unix 哲学且具备 TUI (Terminal User Interface) 交互能力的模拟社交/剧情对话系统。

## 2. 核心架构组件

### 2.1 数据存储层 (VFS)
- **容器**：使用 `GraphAnalyser` 管理的运行时 `VFSInstance`。
- **节点**：消息在 VFS 中以 `.msg` 后缀的文件节点存在。
- **肚子 (DataJson)**：存储指向剧情图路径（`graphPath`）的轻量级 JSON。

### 2.2 逻辑控制层 (NekoGraph 核心框架) - 【全动态事件协议版】
系统采用“信息发射 + 信号阻塞 + 节点收束”的响应式架构：

- **正文发射器：SocialMsgContentNode (私有节点)**：
  - **职责**：信号进入时广播 `Social.ShowBody`（台词数据）。
  - **流转**：瞬时传导，不阻塞。

- **选项信息节点：ChoiceTextNode (私有节点)**：
  - **职责**：信号进入时广播 `Social.RegisterOption`（携带描述文本与编号）。
  - **流转**：瞬时传导，通常连接至 Trigger 节点。

- **信号阻塞器：TriggerNode (核心原生节点)**：
  - **职责**：阻塞信号，直到玩家输入匹配的数字（`SocialCLI.OptionN`）。

- **终结收束器：SocialMsgEndNode (私有节点)**：
  - **职责**：作为图的分支收束点。
  - **逻辑**：信号进入时广播 `Social.MsgFinished` 事件。
  - **意义**：通知 `CatStrategy` 对话逻辑已全部跑完，可以切换回普通命令模式并标记已读。

- **表现层控制器：CatStrategy (事件翻译官)**：
  - **职责**：管理交互模式的开启与关闭，根据事件动态更新 SocialCLI 界面。

### 2.3 业务控制层 (SocialManager)
- **职责**：发信、已读状态持久化、存档关联喵。

## 3. 下一步计划
1. [ ] 创建 `SocialMsgContentNode`, `ChoiceTextNode`, `SocialMsgEndNode` 及其策略喵。
2. [ ] 在 `Assets/Scripts/OutStage/SocialCLI/` 创建 `SocialManager.cs`。
3. [ ] 扩展 `CommandRegistry.Social.cs`，添加 `msg_send` 指令喵。
4. [ ] 重构 `SocialCLI.cs` 实现交互拦截逻辑。
