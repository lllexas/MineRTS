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
