# 2026-04-19 SocialBox Fullstack Guide

## 目的

这份文档记录今天 `SocialBox` 从启动装配、Hub 绑定、业务门面、VFS 资源协议、到 Query / Session 前端呈现的完整构建过程。

它的用途不是复盘聊天，而是给之后改造：

- `Warehouse`
- `.entity`
- `Lab`
- 其他资源驱动前后端系统

时作为直接可复用的操作指南与查缺清单。

---

## 一、我们最终做成了什么

`SocialBox` 现在已经是一条完整闭环：

1. `StartBoots` 在存档创建 / 加载时提供启动 pack
2. `GraphHub` 在运行时持有 facade
3. `SocialBoxFacade` 作为领域门面统一访问社交邮箱 pack
4. 后台故事网中的 `.msg` 原版节点通过 `Execute` 投递可见副本
5. 前台 `cat/query` 通过 `Query -> ClientRuntime -> Session`
6. 玩家选择后，通过 `ResumeSuspendedSignalToTarget(...)` 恢复后台剧情

这已经不是“能看见一条消息”而已，而是完整的前后端链路样板。

---

## 二、构建顺序

### Step 1. 先把启动装配从旧注册表迁到 `StartBoots`

关键点：

- 启动装配不再依赖旧 `SaveBootstrapRegistry`
- `StartBoots` 成为唯一正式入口
- Inspector 直接绑定 `.nekograph` / `TextAsset`
- 固定启动包直接信任资源自己的 `PackID`

这一层的意义是：

- 让运行时 pack 来源可见
- 让后面 `GraphHub -> Facade` 的绑定有明确起点

如果这一步没做，后面所有 facade 都会重新退回“猜字符串 packID”。

---

### Step 2. 建立 `StartBoots -> GraphHub -> Facade` 链

最终模型是：

- facade 是纯 C# 实例
- `StartBoots` 用 `SerializeReference` 持有 facade
- `GraphHub` 在运行时注册并持有这些 facade

也就是说：

- facade 不应该是静态常量盒
- 也不应该是 `MonoBehaviour`
- facade 是 Hub 内部的领域服务对象

这一步定下之后，业务访问 pack 的正式入口就变成：

- `GraphHub.GetFacade<SocialBoxFacade>()`

而不是散落在外面的：

- 旧字符串 packID
- 手工完整路径
- 临时 MetaLib 读取

---

### Step 3. 把“社交 pack 常量盒”升级成真正的业务门面

`SocialPackFacade` 不够，它只是在收纳：

- pack 名
- 路径字符串

最后真正成立的是 `SocialBoxFacade`，它的职责是：

- 确保前台社交 pack 存在
- 确保 `/contacts/`、`/messages/` 根目录存在
- 读写联系人盒子
- 读写消息节点
- 删除、交换消息
- 投递 `.msg` 副本

核心原则：

- 业务层不应再直接拼完整 VFS 路径
- 业务层访问邮箱，应当调 `SocialBoxFacade`

这一步是为了把：

- pack 名
- 常用路径
- 节点 CRUD
- 节点复制

全部收回到领域边界内。

---

### Step 4. 重新定义 `.msg` 的资源本体

`VFSMsgSO` 最后被收成很克制的结构：

- `MessageTag`
- `Sender`
- `Title`
- `Body`
- `Choices`

不要把运行时字段塞进 `SO`。

尤其不要把：

- `BackendPackID`
- `SignalId`
- 已读状态
- 回调 token

这些东西写进资源模板本体。

原因：

- `SO` 是内容模板
- 复制体才是运行时副本

这层如果混掉，后面所有持久化和恢复都会变脏。

---

### Step 5. 让 `.msg Execute` 只做后台投递，不直接开 UI

`.msg Execute` 的职责最终收成：

1. 读取原版消息资源
2. 调 `SocialBoxFacade.TryDeliverMessageCopy(...)`
3. 把原版 `.msg` 节点复制到邮箱 `/messages/`
4. 返回 `HandleResult.Wait`

关键判断：

- `Execute` 负责后台运行
- `Query` 负责前台展示入口
- `.msg Execute` 不应直接拉起前端交互

如果这里让 `Execute` 直接开 UI，前后端边界又会重新糊掉。

---

### Step 6. 复制体元数据写进节点 `InlineText`

复制体 `.msg` 节点最终采用：

- `Reference` 指向 `VFSMsgSO`
- `InlineText` 写运行时副本元数据 JSON

元数据包括：

- `BackendPackID`
- `BackendNodeID`
- `SignalId`
- `IsResolved`
- `ChoiceTargetNodeIDs`

这里的原则很重要：

- `SO` 负责内容
- 节点 `InlineText` 负责运行时回指

以后别的资源如果也要“副本 -> 回指后台原版”，也优先照这个分层做。

---

### Step 7. 正式重构 `Wait` 语义

这是今天最关键的一步之一。

旧实现的问题是：

- `Wait` 预先生成“指向子节点的挂起 signal”
- 这会导致等待的不是“当前执行上下文”
- 而是一堆未来分支的预制品

这不对。

现在新的语义是：

- `HandleResult.Wait` 挂起当前 signal 本体
- 当前 signal 停在当前节点
- 之后只能通过 `ResumeToTarget(...)` 明确选择下一跳

这直接带来的好处：

- `SignalId` 语义稳定
- 存档恢复清楚
- `.msg`、`.choice`、未来其他等待型资源都能共享统一模型

也就是说：

- `Wait` 对应的不是 `Continue`
- 而是 `ResumeToTarget`

这是后续改造其他等待型资源时最不能忘的原则。

---

### Step 8. 不让挂起信号在读档时自动苏醒

`GraphRunner.OnUserLoaded(...)` 之前会把：

- `SuspendedSignals`

重新塞回：

- `ActiveSignals`

这会毁掉真正的等待机制。

现在已经改成：

- 挂起信号保持挂起
- 只有显式恢复时才继续推进

这一步如果漏掉，前面所有 `Wait` 改造都等于白做。

---

### Step 9. 用 `ResumeSuspendedSignalToTarget(...)` 完成第二联

现在的恢复方式是：

- 前台 session 读复制体 metadata
- 拿到：
  - `BackendPackID`
  - `BackendNodeID`
  - `SignalId`
  - 当前选项映射到的 `TargetNodeId`
- 调：
  - `GraphRunner.ResumeSuspendedSignalToTarget(...)`

这一步是 `.msg` 从“能显示”变成“真能和后台剧情闭环”的关键。

---

### Step 10. `Query -> ClientRuntime -> Session`

前台入口最终应统一成：

- `Query`
- `ConsoleClientRuntime`
- `Session`

对 `.msg` 来说，现在线路是：

1. `cat` 发现 `.msg` 有 `QueryHandler`
2. 构造 `VFSQueryContext`
3. `.msg Query` 返回 `VFSQueryResult("social.msg", payload)`
4. `ConsoleClientRuntime` 根据 `PresentationType` 找 factory
5. 创建 `VFSMsgSession`
6. `BeginSession(...)`

这里已经不再用反射，而是显式注册：

- `PresentationType -> SessionFactory`

这是之后所有前端资源的标准入口。

---

### Step 11. Session 必须站回现有 TUI 体系

`.msg` 的一个重要教训是：

- 不要自己手搓 `HandleKey`
- 不要自己再造一套输入系统

正确做法是：

- `VFSMsgSession : TUISelectSlot`

也就是说：

- session 负责生命周期和业务回调
- `TUISelectSlot` 负责选择输入内核

这一步是之后改造 `Warehouse`、`.entity` 等系统时必须遵守的。

---

### Step 12. TUI 不是二选一，而是可组合积木

今天 `.msg` 又额外证明了一件事：

- `box`
- `selection list`
- `TSSStyle`
- `TUISelectSlot`

它们本来就应该自由组合。

`.msg` 的最终形态是：

- 上半段：消息正文 box
- 下半段：选项选择区
- 运行在同一个 session 内

所以以后新界面不要再问：

- “用 box 还是用 selection list？”

而是应该问：

- 哪一块是静态内容
- 哪一块是交互区
- 它们怎样在一个 session 里组合

---

### Step 13. 刷新粒度要往 CSS/TUI 的心智靠

今天最后一步做的是：

- 进入 session 清屏
- 退出 session 不清屏
- 切换选项时不整页重刷
- 而是只刷新选项区自己的 range

所以现在 `.msg` 已经是：

- 静态区一次画
- 动态区小刷

这件事的意义很大：

- 说明 `TUISelectSlot` 可以继续被扩展成更细粒度的局部刷新框架
- 之后 `Warehouse`、`.entity` 等复杂界面也应该朝这个方向走

---

## 三、之后改别的系统时的复用顺序

以后如果要改 `Warehouse`、`.entity`、`Lab`、新的资源驱动系统，建议按这个顺序检查：

1. 启动 pack 来源是否已经归入 `StartBoots`
2. 是否已经有对应的 facade，并由 `GraphHub` 持有
3. 业务层是否还在直接拼路径
4. 资源本体是否和运行时副本状态分层
5. `Execute` 是否只做后台运行
6. `Query` 是否只做前台展示入口
7. 是否需要等待型语义
8. 如果需要等待，是否走新的 `Wait -> ResumeToTarget`
9. session 是否复用了 `TUISelectSlot`
10. 静态区 / 动态区是否拆开
11. 动态区是否能局部刷新

---

## 四、今天这条 SocialBox 链路最重要的几个原则

- facade 是 Hub 内部服务，不是静态工具箱
- 业务不要直接拼 packID 和路径
- `SO` 管内容模板，节点副本管运行时元数据
- `Execute` 跑后台，`Query` 进前台
- `Wait` 挂起当前 signal，不要预挂未来分支
- 恢复方式是 `ResumeToTarget`，不是 `Continue`
- session 复用 `TUISelectSlot`，不要重写输入系统
- `box + selection list + TSSStyle` 是可组合积木
- 动态区应该小刷，别整页重绘

---

## 五、一句话总纲

`SocialBox` 这次不是单独修了一个消息系统，而是第一次把：

- 启动装配
- Hub / Facade
- 领域 pack 访问
- VFS 资源协议
- Wait / ResumeToTarget
- Query / Session / TUI

这一整条前后端链路正式打通了。

之后改别的系统，应该优先照这份指南复用，不要再从零发明新的桥接结构。
