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

## Current .msg Direction

`.msg` 现已明确不再沿用旧 `MsgStrategy` 那套“把 pack 塞进文件内容然后直接拉起整条神秘链”的方式。

### 1. .msg 的资源本体

`.msg` 的资源本体应是一个轻量消息资源，而不是整个对话 pack。

当前方向：

- 用 `ScriptableObject` 承载 `.msg`
- `DataType = VFSMsgSO`
- `ContentKind = UnityObject`

这比把完整 pack 当作 `.msg` 载荷更合理，也更符合 Query / Execute 分离后的资源语义。

### 2. Query 与 Execute 的职责

`.msg` 的职责分工现已基本确定：

- `Query`
  - 负责前台显示入口
  - 返回 `VFSQueryResult`
  - 再由 `ConsoleClientRuntime` 决定是否开启 session

- `Execute`
  - 负责后端运行入口
  - 不再直接承担 console 前台显示责任
  - 主要用于故事网中的信号推进与消息投递

### 3. 不可见故事网 + 玩家可见复制体

`.msg` 的正确运行模型不是“前台 Query 直接驱动后端状态机”，而是：

1. 不可见故事 pack 持有原版 `vfs.msg`
2. 信号到达原版 `.msg` 时执行 `Execute`
3. `Execute` 将原版消息复制/投递为玩家可见的 `.msg` 副本，写入玩家可读 pack
4. 玩家通过 `ls / cat / Query / session` 访问的是这个复制体
5. 玩家在复制体上的交互，通过复制体中携带的后端锚点回到不可见故事 pack，恢复原始挂起信号

这样：

- 玩家可见文件树只是前台镜像
- 不可见故事网才是运行时真相

### 4. .msg 复制体需要携带的后端锚点

这里不需要再发明 callback token。

当前确认可直接复用 NekoGraph 现有运行时主键体系：

- `BackendPackID`
- `BackendNodeID`
- `SignalId`

原因：

- `BasePackData` 以 `PackID` 标识 pack
- `BaseNodeData` 以 `NodeID` 标识节点
- `SignalContext` 以 `SignalId` 标识挂起/恢复中的信号
- `SuspendedSignals` 本来就是按 `SignalId` 存储

因此，对 `.msg` 复制体来说，只要带上：

- 后端 pack id
- 后端当前 node id
- 当前 signal id

就已经足够稳定、可持久化、可恢复。

### 5. 防重复放行

玩家可见 `.msg` 复制体还需要额外带一个“已放行/已解决”状态位，用于避免重复触发后端恢复。

这类状态位应作为复制体的运行时状态，而不是原版静态资源内容的一部分。

## Console Session Direction

Console/TUI 这条线目前已明确改用 `session` 语义描述，而不是旧的 “input handler / slot”。

当前结构：

- `ConsoleManager`
  - session 宿主
- `ConsoleClientRuntime`
  - 宿主本地仲裁器
- `IConsoleSession`
  - 交互会话协议
- `TUISelectSlot`
  - 一种具体 console session

这里的关键共识：

- Query 不直接启动 session
- Query 只返回包
- `ConsoleClientRuntime` 负责仲裁并决定是否开启 session
- 仲裁器不是全局单例，而是绑定到每个具体 `ConsoleManager`

## PackID Primary Key Direction

Pack 这一层当前最大的结构问题，不是“多一个索引有点烦”，而是把两种完全不同语义的键塞进了同一个底层容器：

- `GraphAnalyser` 对外按 `PackID` 工作
- `GraphRunner` 对内按 `InstanceID / guid` 工作
- `EntityGraphContext.PackDataDict` 却把这两者混成同一个 `Dictionary<string, BasePackData>`

这直接导致：

- `GraphAnalyser` 需要 `_packIDToGuid` 二级索引来桥接
- `GraphRunner` 把共享 pack 字典当成运行时实例表使用
- `UserModel.PackDataDict` 也被迫用 `guid` 作为持久层 key

### Current judgement

`PackID` 应当成为统一主键。

无论是：

- `UserModel`
- `GraphHub`
- `EntityGraphContext`
- `GraphAnalyser`
- `GraphRunner`

都应围绕同一张：

- `PackID -> BasePackData`

的表来组织。

### Why this is now safe

过去把 `guid` 作为主键，唯一像样的理由是“同一个 pack 可以多实例叠加”，例如 aura / 光环叠加。

但现在随着 VFS 协议扩展，这种需求已经可以通过更局部、更清晰的机制处理，不再值得反过来污染整套 pack 容器模型。

换句话说：

- `PackID` 是业务身份
- `guid / instanceID` 如果还需要存在，也只能是特例运行时句柄

它不应该再是主 `PackDataDict` 的基础语义。

### Refactor target

目标不是简单“删掉一个索引”，而是纠正容器职责：

1. `PackDataDict` 统一改为 `PackID -> BasePackData`
2. `GraphAnalyser` 直接按 `PackID` 访问，不再维护 `_packIDToGuid`
3. `GraphRunner` 不再把共享 `PackDataDict` 当作 `InstanceID -> BasePackData` 的实例表
4. 若未来仍保留某些多实例运行时需求，应引入独立的运行时实例机制，而不是重新污染主 pack 表

### Migration order

1. 先统一 `UserModel`、`GraphHub`、`EntityGraphContext`、`GraphAnalyser` 的主键语义
2. 再改 `GraphRunner`，让它基于统一的 `PackID` 表运行
3. 最后评估是否还需要保留任何独立的运行时 `instanceID` 容器

## Facade And StartBoots Direction

当前进一步确认：

- 启动装配层与运行时动态层，应视为两套不同来源。
- `Facade` 关心的是“我使用哪一类 pack”，而不是额外再发明一个新的运行时 key。

### 启动装配层

对 `SocialFacade`、`MainStoryFacade` 一类启动期固定装配的领域包来说：

- 我们通常只在乎“这个 facade 绑定的那份 pack”
- Inspector 拖拽绑定的 `.nekograph` / `TextAsset` 自身携带的 `PackID`
- 这份 `PackID` 就应直接成为启动装配后进入存档的 pack 身份

也就是说：

- 启动装配层不需要再额外定义 `RuntimePackKey`
- `StartBoots` 不需要为固定启动包再包装一层人工 key

### 运行时动态层

`MetaLib` 仍然有价值，但主要保留给运行时动态场景：

- 临时小怪仓库
- 动态派生 pack
- 非 Inspector 直接装配的运行期内容

在这些场景里：

- `MetaLib` 的 `PackID`
- 动态创建逻辑
- 运行时按名字生成 / 查找 / 派生

才真正发挥作用。

### StartBoots 未来方向

`StartBoots` 更适合作为 `SaveManager` 附属的 `MonoBehaviour` 启动装配器。

理想中的启动槽位更像：

- `FacadeType`
- `TextAsset NekographAsset`
- `Required`

而不是：

- `FacadeType`
- `TextAsset NekographAsset`
- 人工定义的运行时 key

这里的关键判断是：

- 固定启动包：由绑定资产自己的 `PackID` 决定身份
- 动态运行包：由 `MetaLib` / 动态创建逻辑决定身份

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

## OutputConnections Unification Plan

当前进一步确认：

- `[OutPort]` / `[InPort]` 字段定义节点行为端口语义
- `OutputConnections` 是对这些端口的统一连线抽象
- `ConnectionData` 已经保留了：
  - `FromPortIndex`
  - `TargetNodeID`
  - `ToPortIndex`

这意味着运行层理论上可以统一基于 `OutputConnections` 做分发，而不必继续在通用层硬编码：

- `RootNodeData._`
- `VFSNodeData.ChildNodeIDs`
- `ComparerNodeData.PassOutputs / FailOutputs`
- 以及其他具体字段名

### Refactor scope

如果后续正式把运行层统一回 `OutputConnections`，需要覆盖的代码范围大致是：

1. `GraphRunner`
- 信号推进时统一通过 `OutputConnections` 获取下一跳
- 恢复挂起 signal 时，也统一以 `targetNodeId` / `port index` 语义处理

2. `NodeStrategy`
- 提供统一的“按输出端口索引取目标节点”辅助方法
- 比较器、分支、Root、VFS 等策略不再直接硬读具体 `[OutPort]` 字段

3. `GraphAnalyser`
- 通用查询尽量不再手认 `_`、`ChildNodeIDs`
- BFS / 子节点列举 / 目录遍历等需要重新区分：
  - 哪些是 VFS 专用父子语义
  - 哪些是通用图连线语义

4. `NodeData` / 编辑器同步层
- 保证 `[OutPort]` 字段与 `OutputConnections` 始终同步
- 明确谁是行为语义源，谁是统一连线视图

5. CLI / 编辑器 / Bridge 工具
- 统一使用 `FromPortIndex / ToPortIndex`
- 避免一部分工具读字段名，一部分工具读 `OutputConnections`

### Suggested execution order

1. 先在 `NodeStrategy` 层补统一出口读取工具
2. 再迁移 `Comparer / Flow / Root / VFS` 这批高频策略
3. 然后清理 `GraphRunner` 和 `GraphAnalyser` 中手认字段名的逻辑
4. 最后再处理编辑器与 CLI 的同步收口

### Current judgement

这次重构有价值，但不属于当前 `.msg` 双联落地的 blocker。

更合适的时机是：

- `.msg` 链条彻底跑通之后
- 单独开一轮“运行层统一口径”重构
