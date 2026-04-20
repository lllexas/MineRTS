# 2026-04-20 TUI Session Repair Guide

## 目的

这份文档记录这次 `.msg` session 抢修里，和 `TUI` 使用方式直接相关的经验。

它不是复盘业务逻辑，而是记录：

- `session` 应该负责什么
- `TUISelectSlot` 应该负责什么
- 输入转接应该怎么接
- 清屏时机应该怎么卡
- 局部刷新为什么会失效

后面做：

- `Warehouse`
- `.entity`
- `Lab`
- 其他需要 `session + slot + 局部刷新`

的界面时，优先看这份。

---

## 一、职责切分

### 1. `session` 负责生命周期，不负责重写一套输入系统

正确模型：

- `session`
  - 负责进入 / 退出
  - 负责业务确认回调
  - 负责和 `ConsoleManager` 的关系
- `TUISelectSlot`
  - 负责方向键
  - 负责数字直选
  - 负责确认 / 取消
  - 负责选中态与重渲染节奏

错误模型：

- `session` 自己写一整套
  - `HandleKey`
  - `HandleNavigation`
  - `Render`
  - `Confirm`

这会把已经写好的 `TUI` 输入内核又绕开一次。

一句话：

**`session` 是容器，`TUISelectSlot` 是交互内核。**

---

### 2. 优先组合静态区和动态区，不要把整页都当成可变内容

像 `.msg` 这种界面，天然应该拆成：

- 静态区
  - title
  - message box
- 动态区
  - options
  - help text

静态区应该一次画完，动态区才应该参与局部刷新。

这和 CSS/TUI 的心智一致：

- 不变块不重画
- 可变块单独刷新

---

## 二、进入 Session 的正确时机

### 1. 不要在 `TUISelectSlot` 构造函数里预渲染

这次踩到的坑之一就是：

- `new VFSMsgSession(...)`
- `TUISelectSlot` 构造时立刻 `Render()`

但这时：

- 还没 `BeginSession`
- 还没 `ClearConsole`
- 还没确定真正的 input handle 起始行

这会导致：

- 先写一轮脏内容
- 后面 session 正式进入后又重画
- 局部刷新范围也可能基于旧行号

正确做法：

- 构造时只准备数据
- `OnSessionEnter(...)` 再真正 `Render()`

一句话：

**先进入 session，再开始画。**

---

### 2. 进入 session 时先清屏，再取起始行，再渲染

这次最关键的 bug 就在这里。

错误顺序：

1. 先读 `InputHandleStartLine`
2. 再 `ClearConsole()`
3. 再 `Render()`

问题是：

- `ClearConsole()` 会同步清掉 buffer
- 清屏前读到的 `startLine` 已经失效
- 后面所有局部刷新都往旧绝对行号写

表现出来就是：

- 上下键看似没刷新
- 回车后才一起显出来
- 选项区像被追加罗列

正确顺序：

1. `_renderedHeight = 0`
2. `ClearConsole()`
3. 重新读取 `InputHandleStartLine`
4. `Render()`

一句话：

**清屏先于定位，定位先于绘制。**

---

## 三、输入转接的正确心智

### 1. session 模式下，不能只转发特殊键

这次另一个坑是：

- `ConsolePanelBase`
  - 只把 `Up/Down/Enter/Esc` 这种特殊键转给 session
  - 却没把普通 submit 文本转给 `HandleSubmit(...)`

结果就是：

- `TUISelectSlot.HandleSubmit(...)`
- 里面现成的数字直选逻辑

完全没有被走到。

正确做法：

- 特殊键：
  - 走 `session.HandleKey(...)`
- 普通字符输入：
  - 提取出 submit 文本
  - 走 `session.HandleSubmit(...)`

这样：

- `1-9` 数字直选
- 其他纯输入式 session 逻辑

才能真正复用 `TUISelectSlot` 的成熟实现。

一句话：

**session 输入转接不是只转方向键，而是要把 submit 语义也转过去。**

---

### 2. session 消费了回车后，要阻止输入框残留内容

如果 `Return` 被 session 拿去做：

- confirm
- close
- resume

那就不能再让 `TMP_InputField` 残留换行或脏字符。

这次的处理原则是：

- 若 session 已消费回车
- 需要清理 `inputField.text`
- 并同步更新输入预览

否则会出现：

- session 已确认
- 但底部输入预览还残留字符

---

## 四、局部刷新的原则

### 1. `TryRenderSelectionChange(...)` 的前提是：刷新区间必须稳定

想做小刷，前提不是“代码里多写个局部更新函数”。

真正前提是：

- 静态区高度稳定
- 动态区起始行可精确计算
- 动态区旧高度可精确知道

否则局部刷新一定会错位。

所以局部刷新要依赖三件事：

1. `startLine` 正确
2. 动态区 `offset` 正确
3. 旧 `renderedHeight` / 动态区高度正确

这次 `.msg` 的错位，本质就是第 1 条先炸了。

---

### 2. 局部刷新失败时，优先怀疑“范围计算”，不要先怀疑“没生成新内容”

这次现象是：

- 上下切换不立刻变
- 回车后才显出来
- 而且像是追加

直觉上容易怀疑：

- `BuildLines()` 没重跑
- 选中态没更新

但这次实际问题不是内容没生成，而是：

- `ReplaceRange(...)` 用了错误的起始行
- 于是“替换”变成了“往别的地方插”

所以以后看到：

- 小刷不生效
- 内容延后出现
- 屏幕上像罗列追加

先查：

- `startLine`
- `offset`
- `height`

再查业务内容本身。

---

## 五、这次修出来的通用规则

### 规则 1

`session` 不重写输入系统，优先继承或组合 `TUISelectSlot`。

### 规则 2

构造时不预渲染，正式进入 session 时再渲染。

### 规则 3

进入 session 时：

- 先清屏
- 再算起始行
- 再渲染

### 规则 4

session 输入转接要覆盖：

- `HandleKey`
- `HandleSubmit`

不要只转发特殊键。

### 规则 5

动态区局部刷新必须建立在：

- 正确的 `startLine`
- 稳定的静态区高度
- 正确的动态区高度

之上。

### 规则 6

如果小刷表现成“追加”，优先查范围，不要先查内容。

---

## 六、对以后新 TUI 模板的建议

以后做新的交互式 TUI，建议默认套这个模型：

1. `XXXSession : TUISelectSlot`
2. `BuildLines()` 负责静态区 + 动态区的组合
3. `TryRenderSelectionChange(...)` 只刷新动态区
4. `HandleConfirm()` 只做业务确认
5. `HandleCancel()` 只做退出

不要再从头写一遍：

- key 分发
- 方向键切换
- 数字直选
- 选中态刷新

这些已经是 `TUISelectSlot` 的责任。

---

## 七、一句话总结

这次 `.msg` 抢修的真正教训不是“修了一堆怪 bug”，而是：

**TUI 要成立，靠的不是多写逻辑，而是把**

- session 生命周期
- slot 输入分发
- 清屏时机
- 局部刷新范围

**这四件事严格卡在正确层次里。**

只要层次错一点，表现就会非常诡异。  
一旦层次顺了，很多 bug 会自然消失。
