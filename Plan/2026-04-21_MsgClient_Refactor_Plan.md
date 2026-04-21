# MsgClient Refactor Plan

## 背景

`.msg` 这条链最初为了尽快跑通，采用了：

- `.msg Query`
- `ConsoleClientRuntime.RegisterSessionFactory("social.msg", ...)`
- 直接构造 `VFSMsgSession`

这套方式在第一版可用，但随着：

- 挂起 / 恢复
- 已处理消息回看
- 二次 `cat`
- session 清屏 / 输入转接 / 局部刷新

逐步接入之后，问题开始集中暴露。

当前日志已经证明：

- `.msg Query` 能正常拿到 `VFSMsgSO`
- `ReplicaMeta` 能正常拿到 `IsResolved / SelectedChoiceIndex`
- `VFSMsgSession.BuildLines()` 也能正常构建内容

所以现在的问题已经不再是数据缺失，而是：

**`.msg` 缺少像 `LabClient / EntityClient` 那样的前端仲裁层。**


## 当前痛点

### 1. `.msg` 直接把 Query 和 Session 绑死

现在是：

- 后端 `.msg Query`
- 前端 `ConsoleClientRuntime` 直接起 `VFSMsgSession`

这意味着：

- Query 结果没有先进入一个 client 仲裁层
- session 生命周期和 viewer 生命周期没有统一入口
- 二次进入同一消息时，没有清晰的重入策略


### 2. `.msg` 既当资源协议，又当前端入口

目前 `.msg` 同时承担：

- 原始 query 包定义
- session 启动
- 已处理态判断
- 回看态显示
- 选择确认和后端恢复

职责偏多，导致后续任何一个显示问题，都会回流到 `.msg` 资源本体上。


### 3. 已处理态和未处理态缺少统一前端协调者

理论上 `.msg` 至少有两种前端模式：

- `inspect`
  - 未处理，允许交互
- `resolved`
  - 已处理，只允许回看

现在这些判断仍然散在 `VFSMsgSession` 里，不利于后续扩展。


## 设计原则

### 1. Query 只负责回原始包

`.msg Query` 应该只做：

- 读取 `VFSMsgSO`
- 读取复制体节点的 `InlineText`
- 构造 `VFSMsgQueryPayload`
- 返回通用 presentation type

不负责直接决定前端开什么 session。


### 2. MsgClient 负责前端仲裁

新增：

- `MsgClient`
- `MsgClientViewKeys`
- `MsgClientEvents`

职责：

- 接收 `.msg` query result
- 根据 `RequestName` 和 `ReplicaMeta`
  - 分发到 inspect / resolved / summary 等具名 view
- 决定：
  - 打开交互 session
  - 或打开只读 viewer


### 3. Session 只负责交互，不负责仲裁

`VFSMsgSession` 继续保留，但职责应收缩为：

- 显示消息正文
- 显示选项
- 处理输入
- 确认后调用 `ResumeSuspendedSignalToTarget(...)`

而不是承担：

- 谁来打开它
- 什么情况下该打开它
- 已处理态是否改成别的前端形式


### 4. 复制体 InlineText 继续作为状态落点

复制体 `.msg` 节点的 `InlineText` 继续保存：

- `BackendPackID`
- `BackendNodeID`
- `SignalId`
- `ChoiceTargetNodeIDs`
- `IsResolved`
- `SelectedChoiceIndex`

这是 `.msg` 回看态和持久化的正统状态来源。


## 目标结构

### 后端

- `.msg Query`
  - `presentationType = "msg"`
  - `requestName = 前端传入名`
  - payload = `VFSMsgQueryPayload`


### 前端

- `MsgClient`
  - `RegisterPresenter("msg", PresentRequest)`
  - 按 `RequestName` 再分发

- 例如：
  - `MsgClient.ViewRequested.inspect`
  - `MsgClient.ViewRequested.resolved`
  - `MsgClient.ViewRequested.summary`


### 视图层

- `VFSMsgSession`
  - 作为交互式 inspect session

- 未来可选：
  - `MsgViewerPanel : SpaceUIAnimator`
  - 用于非交互回看态


## 最小实施顺序

### 第 1 步

新增：

- `MsgClient`
- `MsgClientViewKeys`
- `MsgClientEvents`

模式参考：

- `LabClient`
- `EntityClient`


### 第 2 步

把 `.msg Query` 改成：

- `presentationType = "msg"`
- `requestName = context.RequestName ?? MsgClientViewKeys.Inspect`

不再直接返回 `"social.msg"`。


### 第 3 步

让 `MsgClient` 接住 `"msg"` presenter，然后判断：

- `ReplicaMeta == null || !IsResolved`
  - 打开 `VFSMsgSession`

- `ReplicaMeta.IsResolved == true`
  - 先仍然可复用 `VFSMsgSession`
  - 但由 `MsgClient` 明确决定这是 resolved 模式


### 第 4 步

把“已处理态”的前端判断，从资源层移动到 client/session 边界：

- session 只消费模式
- client 决定模式


### 第 5 步

如果后续需要：

- 再补 `MsgViewerPanel`
- 把 resolved 模式从 session 中拆出来


## 这次重构的价值

不是为了多一层类，而是为了把 `.msg` 拉回和：

- `.labentry`
- `.entity`

同一条正统链上：

**前端具名请求 -> 后端回原始包 -> client 仲裁 -> viewer/session 呈现**

这样后续：

- 二次 cat
- 回看态
- 已处理消息
- 多种显示器

才不会继续在 `.msg` 资源本体上堆职责。


## 已追加经验：二次 `cat` 黑屏的真正根因

在本轮抢修中，已经确认一个非常关键的经验：

### 表象

- 第一次 `cat .msg` 正常
- 选择后退出
- 第二次 `cat` 同一条 `.msg`
  - `Query` 正常
  - `VFSMsgSession.BuildLines()` 也正常
  - 但屏幕全黑


### 误判方向

一开始容易怀疑：

- `ReplicaMeta` 没写好
- `SelectedChoiceIndex` 没带回来
- `resolved` 模式没有正确构建正文

但日志已经证明这些都不是根因。


### 真正根因

真正问题在于：

- `ConsolePanelBase` 才是 `InputHandleHost` 的拥有者
- 但旧实现里 `ConsoleManager.EndSession()` 会顺手调用 `UnbindInputHandleHost()`

这样会导致：

1. 第一次 `.msg` session 退出
2. `InputHandleHost` 被一并解绑
3. 紧接着第二次 `cat`
4. `VFSMsgSession` 虽然构建出了 lines
5. 但 `WriteInputHandleRange(...)` 实际写入的是一个尚未重新绑定的宿主
6. 表现为“数据正常，但屏幕全黑”


### 正确修法

把 host 生命周期和 session 生命周期分开：

- `InputHandleHost`
  - 属于 `ConsolePanelBase`
  - 由 panel 自己绑定 / 解绑

- `Session`
  - 属于 `ConsoleManager`
  - 只负责 begin / end

因此已修正为：

- `ConsoleManager.EndSession()` 不再主动 `UnbindInputHandleHost()`


### 这条经验的意义

以后所有基于：

- `TUISelectSlot`
- `ConsoleSession`
- `Client -> Session`

的前端链路，都必须记住：

**不要让 session 退出顺手拆掉 panel 的渲染宿主。**

否则就会出现：

- 第一次打开正常
- 第二次打开黑屏
- 日志里一切正常
- 但实际写到了空气里


### 结论

这不是 `.msg` 特有 bug，而是：

**SpaceTUI 的 session 生命周期和 input-handle host 生命周期必须严格解耦。**

这是之后继续做：

- `.msg`
- `.entity`
- `.labentry`
- `Warehouse`
- 任何复合 session viewer

时都要遵守的基础规则。
