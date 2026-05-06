# TUI 交互式组件架构设计 - 2026-03-21

> 目标：把当前交互 TUI 重构到一条清晰的声明式主线上。
> 核心不是“生命周期句柄”，而是“系统应用声明一个输入把手，并插入控制台”。

---

## 1. 当前结论

这次重构已经明确以下主语：

1. `DeveloperConsole` 是控制台。
2. `IConsoleInputHandler` 是控制台输入把手。
3. `DeveloperConsole` 持有唯一的“当前输入把手插槽”。
4. `MsgStrategy` 不是输入把手，而是系统应用 / 虚拟用户。
5. `MsgStrategy` 的职责是：声明配置、创建把手、把把手插进控制台。

一句话概括：

- 控制台只提供插槽。
- 把手只负责交互。
- 系统应用自己掏出并配置把手。

---

## 2. 必须废弃的旧理解

以下方向已经确认不对：

1. 让 `TUITool` 持有生命周期对象。
2. 让 `DeveloperConsole` 负责创造具体的交互组件。
3. 让 `ICatStrategy : IConsoleInputHandler`。
4. 让 `MsgStrategy` 自己充当输入把手。
5. 用 `InteractiveTUIElement / Create-Update-Dispose` 作为主模型。

这些方案的问题都一样：

- 混淆了“策略 / 应用 / 用户意图”和“输入机制 / 交互把手”。
- 让控制台或策略承担了不属于自己的职责。
- 过度过程式，不贴近当前 TSS / TUI 的声明式路线。

---

## 3. 正确的比喻

为了统一语言，这里固定采用以下比喻：

- `DeveloperConsole` = 控制台
- `IConsoleInputHandler` = 控制台输入把手
- `MountInputHandler / UnmountInputHandler` = 插入 / 拔出把手
- `MsgStrategy` = 系统应用 / 虚拟用户

因此：

- 控制台输入把手，插在控制台的输入把手插槽里。
- 插槽只负责插入和拔出，不负责创造把手。
- 系统应用像虚拟用户一样，从异次元掏出一个把手，配置好后插上去。

这就是当前架构的唯一正确主语。

---

## 4. 声明式路线

这套系统从 `TSSStyle` 开始，走的就是接近 HTML 的路线。

关键点不在于“长得像标签”，而在于：

1. 应用层声明自己要什么。
2. 配置对象描述结构、样式、行为。
3. 运行时把这份声明变成一个可交互实例。
4. 宿主环境负责承载，不负责解释业务意图。

因此，交互 TUI 的正确方向是：

- `TSSStyle`：声明表现
- `TUISelectionConfig`：声明一个可选择组件
- `TUISelectSlot`：这份声明在运行时对应的输入把手实例
- `MsgStrategy`：声明并挂载这个实例的系统应用

这条路线更接近 HTML / 声明式 UI，而不是命令式字符串拼装。

---

## 5. 当前结构

### 5.1 DeveloperConsole

职责：

1. 持有当前唯一的 `IConsoleInputHandler`
2. 提供：
   - `CurrentInputHandler`
   - `HasInputHandler`
   - `MountInputHandler(...)`
   - `UnmountInputHandler(...)`
3. 在 `ConsolePanelBase` 捕获到键盘输入后，优先把输入导流给当前把手

注意：

- `DeveloperConsole` 不负责创造把手。
- `DeveloperConsole` 只负责“当前谁接管输入”。

### 5.2 IConsoleInputHandler

职责：

1. 消费控制台输入
2. 维护自己的交互状态
3. 更新自己的显示
4. 在确认时触发后端委托

它的身份就是：

- 输入把手

不是：

- 策略
- 应用
- 控制台

### 5.3 MsgStrategy

职责：

1. 作为系统应用组织消息场景
2. 收集选项数据
3. 构造 `TUISelectionConfig`
4. 创建 `TUIListSelectionHandler`
5. 调用 `_cli.MountInputHandler(...)`

因此：

- `MsgStrategy` 是虚拟用户
- `MsgStrategy` 不是输入把手

### 5.4 TUISelectSlot

职责：

1. 作为交互输入把手基类
2. 管理：
   - 当前配置
   - 当前选中项
   - 导航 / 确认 / 取消
   - 重渲染
3. 由子类决定：
   - 如何导航
   - 如何把当前配置渲染成文本行

它代表的是：

- 通用选择交互机制

### 5.5 TUIListSelectionHandler

职责：

1. 作为 `TUISelectSlot` 的一个具体子类
2. 采用“线性列表”排列方式
3. 负责把当前选择状态渲染成一组列表行

注意：

- 它只是一个具体前端排列实现
- 不是整个选择系统本身

---

## 6. 当前代码形态

当前主链已经接近下面这种写法：

```csharp
private void MountOptionHandle()
{
    TUISelectionConfig config = BuildSelectionConfig();

    if (_cli.CurrentInputHandler is TUIListSelectionHandler existing)
    {
        existing.UpdateConfig(config, resetSelection: false);
        _cli.MountInputHandler(existing);
        return;
    }

    _cli.MountInputHandler(new TUIListSelectionHandler(config));
}
```

这段代码之所以是对的，是因为它满足：

1. `MsgStrategy` 自己构造配置
2. `MsgStrategy` 自己 new 把手
3. `MsgStrategy` 自己把把手插进控制台
4. `DeveloperConsole` 没有代为创造组件

这就是当前重构里最重要的方向修正。

---

## 7. TUISelectionConfig 的定位

`TUISelectionConfig` 不再只是视觉参数集合。

它现在应当被理解为：

- 一份声明式组件描述

其中包含：

1. 数据：`items`
2. 表现：`viewStyle`
3. 交互规则：`interaction`
4. 所属控制台：`console`

也就是说，它越来越像一个“终端里的 HTML 节点描述”。

这也是为什么当前可以接受把 `DeveloperConsole` 放进 `TUISelectionConfig`：

- 这不是为了偷懒乱耦合
- 而是为了让“声明 -> 运行实例”这条链保持直接、诚实、可读

---

## 8. 为什么现在比以前更对

以前的问题是：

1. 控制台想自己造把手
2. 策略想自己当把手
3. 面板层知道太多交互组件细节
4. 系统围着桥接层和工厂层绕来绕去

现在更对，是因为主语收干净了：

1. 控制台：只有插槽
2. 把手：只有交互
3. 策略：只有应用行为和把手装配
4. 配置：只有声明

这才接近我们真正要的“终端 HTML”路线。

---

## 9. 下一步建议

下一步不是继续发明新层，而是继续做表达收口。

### 9.1 继续保留的东西

1. `DeveloperConsole.CurrentInputHandler`
2. `DeveloperConsole.MountInputHandler(...)`
3. `DeveloperConsole.UnmountInputHandler(...)`
4. `TUISelectSlot` 作为输入把手基类
5. `MsgStrategy` 自己创建并挂载把手

### 9.2 后续可以继续清理的东西

1. `TUIListSelectionHandler` 的命名已经压短
2. `MsgStrategy` 中遗留的旧接口气味可以继续删除
3. `DeveloperConsole` 中用于显示写回的辅助命名还可以更自然
4. `TUISelectionConfig` 可以继续向更像“声明式节点”的方向整理

### 9.3 未来扩展方向

未来如果要支持：

- 九宫格选择
- 表格选择
- 环形选择
- 盒装选择

不需要改：

- `DeveloperConsole` 的输入插槽语义
- `MsgStrategy` 作为虚拟用户的主语
- `IConsoleInputHandler` 作为把手的身份

只需要新增新的把手实现，例如：

- `TUIGridHandle`
- `TUITableHandle`
- `TUIBoxHandle`

它们依然由系统应用自己创建并插入控制台。

---

## 10. 最终原则

这次重构最终固定以下原则：

1. 插槽属于 `DeveloperConsole`
2. 把手属于 `IConsoleInputHandler`
3. 策略属于系统应用 / 虚拟用户
4. 策略负责创建并插入把手
5. 控制台不负责创造把手
6. 整体路线是声明式，不是过程式句柄管理

最终口号：

> 系统应用像虚拟用户一样，声明一个把手，配置它，然后把它插进控制台。


