# VFS Query And Runtime Refactor

## Background

MineRTS 目前围绕 VFS 文件节点存在两套不同层次的机制：

1. `VFSNodeStrategy => ExeRegistry => [EXEHandler]`
2. `ConsoleManager => SetActiveStrategy => CatStrategy`

前者是稳定且好扩展的后端能力入口。后者可以跑，但组织方式偏早期原型化，很多业务约定散落在外部代码里。

典型问题：

- `CatStrategy` 经常依赖外部隐式约定，而不是显式协议。
- 例如 `.msg` 的显示依赖 `DataJson` 实际上是一个有效 pack，且 pack 内部还要满足特定排列方式。
- 前端为了显示一个 VFS 节点，往往必须提前知道路径、数据格式、会话拉起方式和事件名。
- 这导致“表面解耦，实际把耦合转移到外部约定”。

当前判断：

- `EXEHandler` 方向是对的。
- `CatStrategy` 不应该继续承担业务协议主入口。
- 未来的核心抽象应当是“VFS 后缀能力协议”，而不是继续堆巨型桥接类。

## Current Consensus

### 1. NekoGraph 更像后端

NekoGraph 的核心能力应理解为：

- `GraphHub / GraphRunner / GraphAnalyser` 提供运行时上下文
- VFS 提供统一路径和权限模型
- `VFSNodeStrategy` 负责调度文件节点
- `ExeRegistry` 负责将后缀绑定到具体处理器

这套机制天然适合作为后端协议层。

### 2. CatStrategy 要降级

`CatStrategy` 仍然可以保留，但应降级为某种前端会话壳或 Console 输入接管机制，而不是后缀业务协议本身。

它未来最多负责：

- 接管当前 console 输入
- 承载某个前端会话
- 转发确认、方向键、流式输入等交互

它不应再负责：

- 解释后缀协议
- 解析业务数据
- 自己拼装 pack 运行语义
- 充当后端和前端之间的大型桥接器

### 3. 一个 VFS 后缀应有完整规范

当前只有 Execute 规范是不够的。

未来每个重要后缀应至少支持两类能力：

1. `Execute`
用途：
真正运行，允许副作用，依赖 `GraphRunner` 信号调度。

2. `Query` / `Preview`
用途：
在给定少量上下文的情况下，直接查询某个可访问 VFS 节点应该如何展示，供前端拉起显式显示、流式显示或交互式显示。

这意味着前端不需要再硬编码某些后缀的特殊桥接逻辑，而是统一依赖后缀能力协议。

## Design Direction

### Keep EXEHandler as the runtime entry

`[EXEHandler]` 保持为运行时执行入口。

它继续负责：

- 接收 `VFSResolvedContent`
- 拿到 `SignalContext / BasePackData / GraphRunner / packInstanceID`
- 决定 `HandleResult`
- 在需要时进入 `Wait`，由回调恢复图流

### Add a dedicated Query capability

不要把 `EXEHandlerAttribute` 硬改成万能入口。

更合适的方向是新增并列能力，例如：

- `[VFSQueryHandler]`

原因：

- `Execute` 和 `Query` 的语义不同
- 方法签名不同
- 生命周期不同
- 副作用要求不同

它们共享的只有“后缀”这个分发键。

### Query should return frontend-consumable description

`Query` 不应直接返回具体 UI。

更合理的是返回前端可消费的描述模型，例如：

- 展示类型
- 标题
- 摘要
- 是否可交互
- 初始 payload
- 可选操作
- 会话载荷

这样可以把前端渲染层做成统一 renderer，而不是让业务 handler 直接操纵面板。

## Why This Is Better

相对于现有 `CatStrategy` 巨型桥接逻辑，新协议有几个明显优势：

- 约定收回到后缀协议本身
- 前端不必再硬编码 `.msg` 之类的业务细节
- 后端不必依赖“DataJson 实际上是 pack”这类外部隐式知识
- Query 可以服务 `cat`、点击文件、悬停预览、详情面板等多种入口
- Execute 继续保持强权能和运行时接管能力

## Useful Reference

`G:\ProjectOfGame\GAL01\Assets\Scripts\Dialog\Runtime`

这套代码可作为当前重构的样板来源，尤其是：

- `DialogVFSHandler`
- `ChoiceVFSHandler`
- `DialogPlayer`
- `ChoicePlayer`

其中做对的组织方式是：

- VFSHandler 负责理解后端协议和运行时上下文
- Player 负责前端会话仲裁
- 纯 UI 面板只负责显示和交互
- 前端层不直接依赖 NekoGraph 内部细节

这比 `MsgStrategy` 当前一类“后端协议 + 前端显示 + 生命周期 + 事件订阅”全部塞在一个类里更健康。

## Proposed Minimal API Draft

当前先不实现，只记录方向。

### Attribute layer

- `EXEHandlerAttribute`
- `VFSQueryHandlerAttribute`

### Query side model

可能需要的类型：

- `VFSQueryContext`
- `VFSQueryResult`
- `VFSQueryRegistry`

### Possible responsibilities

`VFSQueryContext`：

- `GraphAnalyser`
- `PackID`
- `VfsPath`
- `SubjectLevel`
- 少量前端上下文

`VFSQueryResult`：

- `PresentationType`
- `Title`
- `Summary`
- `Payload`
- `IsInteractive`

## First Refactor Target

首个样板建议是 `.msg`。

当前 `.msg` 主要依赖 `MsgStrategy`，组织方式过重，适合作为第一批拆分对象。

目标方向：

1. 把 `.msg` 的后端协议收回到后缀 handler / query
2. 把当前消息显示拆成专门的 `ConversationPlayer` 或类似前端仲裁器
3. 让 console / `cat` / 文件点击等入口统一走 query 协议

## Non-Goals For Now

当前阶段不追求：

- 一次性替换所有旧调用点
- 立刻删除 `CatStrategy`
- 立刻把所有 CLI/TUI 交互统一成一个系统
- 先做庞大而抽象的总设计

当前更合理的是：

- 明确协议边界
- 先为 Query 开新能力口
- 用 `.msg` 做第一个可运行样板

