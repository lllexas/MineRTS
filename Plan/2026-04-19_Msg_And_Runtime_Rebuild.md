# 2026-04-19 Msg And Runtime Rebuild

## Summary

今天原本只是想把 `.msg` 这条链整理一下。

结果实际做成了一次更大的重建：

- `.msg` 从旧 `MsgStrategy` 神秘桥接链，改造成了正式的 VFS 资源协议样板
- Console/TUI 侧从 “input handler / slot” 语义，扶正成 `session` 语义
- `StartBoots -> GraphHub -> Facade -> Pack` 的运行时装配链正式建立
- `PackID` 重新被确立为统一主键
- 社交邮箱被收成了真正的 `SocialBoxFacade`

也就是说，这一天不是单纯“修消息系统”，而是把一整套前后端通信和运行时组织方式重新铺了一遍。

## Initial Problem

最初的问题只是：

- `NekoGraph` 升级后，旧的 `.msg` 机制越来越难以理解
- `MsgStrategy` 依赖大量外部隐式约定
- `.msg` 看起来像文件，实际上背后却偷偷塞了整个 pack 和一堆运行时桥接
- 前端为了显示一个节点，必须知道后端路径、数据格式、事件名和拉起方式

结论很快明确：

- `CatStrategy` 可以留，但只能降级成前端会话壳
- VFS 后缀协议才应该是正式入口
- `.msg` 应该成为这套新协议的第一个样板

## Main Decisions

### 1. NekoGraph 被重新理解成后端

这一轮基本把共识钉死了：

- `GraphHub / GraphRunner / GraphAnalyser` 是后端运行时
- VFS 是统一资源树
- `EXEHandler` / `VFSResource` 是后端能力入口
- 前端不应该再自己知道太多后端内部结构

### 2. Query / Execute 分口

我们最终明确了：

- `Execute` 负责后台真实运行
- `Query` 负责前台显式读取与展示入口

也就是说：

- `.msg Execute` 不直接开 UI
- `.msg Query` 不直接承担后端状态机

前后端被正式拆开。

### 3. Console 交互被正式解释为 Session

原先很多 “input handler / slot” 一类命名，其实已经是一套 console 会话系统，只是说人话失败了。

这轮做了几件关键事：

- 把这层正式命名成 `session`
- `ConsoleManager` 成为 session 宿主
- `ConsoleClientRuntime` 成为每个 console 的本地仲裁器
- `TUISelectSlot` 被扶正为一种具体 session

于是：

- Query 不直接开 session
- Query 只返回包
- `ConsoleClientRuntime` 决定如何呈现

### 4. PackID 被重新扶正为统一主键

原先 pack 层被 `guid / instanceID` 和 `PackID` 双语义污染得很厉害。

这轮明确完成了世界观纠偏：

- `PackID` 是统一业务主键
- `GraphAnalyser`、`GraphRunner`、`GraphHub` 都围绕统一 `PackID -> BasePackData` 表工作
- `packInstanceID` 一类历史命名被改成了 `packIDKey`

这一步很重要，因为后面的 `.msg` 回指和副本恢复都依赖稳定的 pack 身份。

## StartBoots And Facade Rebuild

今天另一个很大的变化，是启动装配和业务 pack 访问层被重建了。

### 1. StartBoots 取代旧启动注册表

旧的 `SaveBootstrapRegistry` 已经彻底退场。

现在的启动逻辑变成：

- `StartBoots` 作为 Inspector 驱动的启动装配器
- 直接绑定 `.nekograph` / `TextAsset`
- 固定启动包直接信任资源自己的 `PackID`

这让启动装配从不可视的注册表，变成了场景内可直接配置的装配层。

### 2. Facade 不再是静态常量盒

我们一开始走过一段弯路：

- 想用静态 facade
- 想用字符串 binding key

最后都被推翻了。

最终确定的模型是：

- facade 是纯 C# 实例类
- `StartBoots` 用 `SerializeReference` 持有 facade
- `GraphHub` 在运行时注册并持有这些 facade

这样以后：

- `GraphHub.GetFacade<SocialBoxFacade>()`
- `GraphHub.GetFacade<MainStoryPackFacade>()`

就成了业务访问 pack 的正式入口。

### 3. SocialBoxFacade 成为邮箱业务门面

原先的 `SocialPackFacade` 只是 pack 名和路径约定收纳盒。

现在它已经被升级成真正的 `SocialBoxFacade`，负责：

- 前台社交包确保存在
- `/contacts/`、`/messages/` 目录组织
- 联系人盒子访问
- 消息节点读写、删除、交换
- `.msg` 副本投递

也就是说，“社交邮箱”终于从散落路径字符串，变成了正式业务门面。

## .msg Rebuild

今天的主线，最终还是回到了 `.msg` 本身。

### 1. 旧 MsgStrategy 被判定为历史垃圾

我们明确了：

- 旧 `MsgStrategy` 整体都应该废弃
- 它唯一值得保留的，只有少量 TUI 调用经验
- 后面的 pack 拉起、外部约定、神秘调用链都不再是未来方案

### 2. .msg 的资源本体被重新定义

`VFSMsgSO` 最终被收成一个非常克制的消息资源：

- `MessageTag`
- `Sender`
- `Title`
- `Body`
- `Choices`

其中 `Choices` 才是关键，因为 `.msg` 本来就是可交互消息，不是纯文本邮件。

### 3. 不可见故事网 + 玩家可见复制体

`.msg` 的核心运行模型今天算是正式定下来了：

1. 后台不可见故事 pack 持有原版 `.msg`
2. 信号打到原版 `.msg` 时走 `Execute`
3. `Execute` 不直接开 UI，而是把消息复制一份投递到玩家邮箱
4. 玩家看到的是复制体 `.msg`
5. 玩家对复制体的选择，再回到后台恢复原始挂起 signal

这一步是今天整个重建里最关键的架构判断。

### 4. 复制体元数据走 InlineText

复制体最终采用了很直接的约定：

- `Reference` 指向 `VFSMsgSO`
- `InlineText` 存运行时副本元数据 JSON

元数据包括：

- `BackendPackID`
- `BackendNodeID`
- `SignalId`
- `IsResolved`
- `ChoiceTargetNodeIDs`

这意味着：

- `SO` 负责内容模板
- 复制体节点负责运行时回指

资源模板和运行时状态没有再混在一起。

### 5. 第二联：通过 SuspendedSignals 恢复后台

今天另一个关键突破是想起来：

- `GraphRunner` 本来就有 `SuspendedSignals`

于是 `.msg` 第二联不需要发明 callback token，也不需要神秘闭包了。

我们最终做成了正式公共 API：

- `GraphRunner.ResumeSuspendedSignalToTarget(...)`

这让：

- `.msg` 选项确认
- `Choice` 恢复
- 存档后的 session 复活

都开始有了正式、可持久化的后端支撑。

## Important Fixes Found On The Way

今天还有几处“顺着查出来的历史洞”：

### 1. SocialCLI 实际没有真正吃到 facade 绑定

一开始社交面板看不到消息，我们怀疑是 facade 链没接上。

后来发现两层问题：

- `SocialCLI` 之前确实还在偏向旧默认包名
- 但更深一层的问题，是 `GraphAnalyser.BfsGetChildren("/")` 根本不会读 `RootNodeData._`

结果就是：

- 消息已经投进邮箱 pack
- 但 `ls /` 永远是空

这次已经修正了 `BfsGetChildren()` 对 root 节点子表的读取。

### 2. OutputConnections 与 `[OutPort]` 的关系被重新澄清

今天也重新理清了一个非常老的问题：

- `[OutPort]` 字段才是节点行为端口定义
- `OutputConnections` 是它们的统一连线抽象

这意味着：

- 比较器、Root、VFS 这类节点完全可以理论上统一回 `OutputConnections`
- 当前问题不是结构不够，而是运行层没有统一走它

所以还顺手记下了后续一轮“统一回 OutputConnections”的施工范围。

## What We Actually Achieved Today

如果只看成果，不看过程，今天真正落地的是：

- `.msg Execute` 已经能把原版消息投递到玩家邮箱
- 社交邮箱能真正看到这条消息
- 复制体节点已经带上完整后端回指元数据
- `VFSMsgSession` 已经能根据选项恢复后台挂起 signal
- `StartBoots -> GraphHub -> Facade -> Pack` 链已经工作
- `PackID` 主键语义已经统一

这说明今天不是停留在“讨论架构”，而是已经把一大段关键链路打通了。

## What Remains

当前还没完全收尾的部分主要是：

1. `.msg` 的正式 QueryHandler 展示链
- 现在 `cat` 还会直接把复制体 `InlineText` 吐出来
- 这行为不算错，但还不优雅
- 下一步应把 `.msg` 读取正式切到 `Query -> VFSQueryResult -> ConsoleClientRuntime -> VFSMsgSession`

2. 社交命令层的旧逻辑清理
- `CommandRegistry.Social` 里仍有旧 `.msg` 打开逻辑
- 还残留对旧 `MsgStrategy`、`DataJson` 的依赖

3. 更长期的运行层统一
- 运行层统一回 `OutputConnections`
- 减少对各种具体 `[OutPort]` 字段名的通用层硬编码

## Final Judgement

今天这轮重构虽然起点只是 `.msg`，但本质上完成的是：

一套从启动装配、运行时上下文、业务 facade、VFS 资源协议，到 console 前端会话仲裁的重新梳理。

更直白一点：

我们一开始只是想让消息系统别再神秘了。

结果一路把：

- 启动装配
- Pack 主键
- facade 架构
- console session
- `.msg` 资源协议
- 前后台恢复链

几乎整套前后端通信架构都重建了一遍。
