# Spine GPU Buffer Animation Plan

## 日期
- 2026-05-05

## 这份文档是写给谁的

这份文档写给接下来继续推进单位动画渲染方案的协作者。

如果你刚接手这个方向，请先明确：

- 当前项目单位动画资产来源是 `Spine`
- 当前项目渲染主线目标仍然是 `GPU instancing`
- 这里讨论的重点不是“传统 3D VAT”
- 这里最终倾向的实现也不是“把所有东西硬塞进一张顶点动画纹理”

本计划的目的是把当前已经讨论清楚的判断、边界和建议实现方式收束下来，避免后续又回到 `Spine -> 低帧 atlas` 或“先做一版再说”的模糊状态。

---

## 先说结论

当前方向应当收束为：

```text
Spine source animation
-> 离线烘焙逐帧顶点结果
-> 运行时使用 Mesh + BaseTexture + StructuredBuffer 回放
-> 继续走 instancing 批量绘制
```

也就是说：

- 不优先继续扩展 `Spine -> spritesheet atlas` 作为主动画格式
- 不优先做“纹理式 VAT 协议”作为第一实现
- 第一优先级是验证：

```text
Mesh + BaseTexture + GPU Buffer animation data
```

这条线能否稳定成立。

---

## 为什么原来的 atlas 方向不够合适

### 1. 当前资产生产事实不是手绘关键帧

当前单位动画不是手绘 sprite 关键帧生产，而是 `Spine`。

这意味着：

- 动画价值主要来自连续骨骼插值
- 主要优势是连续形变、节奏过渡、姿态衔接
- 不是依赖手绘拖影、夸张中间态、漫画式省略

### 2. `Spine -> 低帧 atlas` 会同时失去两边优点

对后续协作者请直接记住这一句：

```text
Spine 连续动画低采样导出 atlas
!=
手绘低帧动画
```

原因：

- 手绘低帧成立，前提是每一帧是人工设计过的关键画面
- `Spine` 低采样导帧只是机械抽样
- 它不会自动获得手绘拖影和手绘夸张形变

因此会出现一个最糟结果：

```text
既没有手绘关键帧表现力
也失去 Spine 的连续感
```

### 3. 若为了保连续感提高 atlas 采样，边际成本又会膨胀

atlas 的问题不是做不到，而是它的每帧新增成本是整帧颜色数据。

例如：

```text
64 * 64 * RGB8 = 12288 bytes / frame
```

如果一个 `Spine` 动作要保住连续感，采样帧数上去后，atlas 成本会直接跟着整帧纹理一起线性增长。

---

## 为什么这里不继续坚持“VAT 纹理”

最开始讨论时，曾经自然想到：

```text
BaseTexture
+ Mesh 元数据纹理
+ VAT 顶点动画纹理
```

但这条路不是最优雅的第一实现。

### 1. 当前绘制 API 决定了 Mesh 本身就可以承载静态元信息

当前渲染模型本来就是：

```text
指定 shader
+ 指定 mesh
+ 指定材质 / 纹理
+ 提交实例参数
```

在这个模型里：

- 拓扑
- 顶点顺序
- 静态 `uv`
- 必要时的 `vertex id`

本来就应该优先放进 `Mesh` 本体，而不是额外再做一张“mesh 元数据纹理”。

### 2. 真正需要按帧变化的只有顶点动画结果

对于当前目标模型，真正的动态量是：

```text
当前动画
当前帧
当前顶点的 xy
```

因此更自然的资源组织方式是：

- `BaseTexture` 继续负责颜色采样
- `Mesh` 继续负责静态几何与静态 `uv`
- `StructuredBuffer / GlobalBuffer` 负责真正的逐帧顶点动画数据

### 3. Buffer 比“几何数据伪装成纹理”更直观

如果第一版就强行把几何动画数据做成纹理式协议，会带来这些问题：

- 需要额外设计 texture layout
- 需要自己解释采样坐标和像素意义
- 需要处理纹理精度、过滤、导入设置、副作用
- 调试时看到的不是结构体，只是一张很难读的图

而如果使用 `StructuredBuffer`，就可以正大光明地表达：

```text
vertexId
animationId
frame
-> VertexAnimationData
```

这在当前项目里更符合实现直觉，也更容易维护。

---

## 当前推荐的运行时结构

后续协作者可以先按这个结构理解整条链路。

### 1. Mesh

职责：

- 存拓扑
- 存静态 `uv`
- 存顶点顺序
- 必要时存 `vertexId`

它是绘制对象本体，不按帧变化。

### 2. BaseTexture

职责：

- 作为颜色采样源

注意：

- 这不是逐帧 sheet
- 它应尽量作为整段动画共享的基准纹理

### 3. Per-instance 参数

职责：

- 告诉 shader 当前实例要播放什么

至少包括：

- `animationId`
- `frameIndex`
- `baseTextureIndex` 或材质变体信息
- 可能的 `flip/tint/state flags`

这里应放小参数，不应放大块逐顶点动画数据。

### 4. GlobalBuffer / StructuredBuffer

职责：

- 存放真正的逐帧顶点动画结果

这部分是当前方案的核心。

shader 在顶点阶段需要能够完成：

```text
读取当前 vertexId
读取当前实例 animationId / frameIndex
根据布局规则算出 buffer 偏移
取出当前顶点当前帧的 xy
生成最终顶点位置
```

---

## 当前推荐的数据分层

后续实现时，先按“两静一动”的心智模型处理：

### 静态层 1：BaseTexture

- 颜色来源

### 静态层 2：Mesh

- 拓扑
- uv
- 顶点顺序
- 顶点 identity

### 动态层：Animation Buffer

- 每帧每顶点 `xy`

这三层里，真正按帧增长的只有最后一层。

这也是当前方案相比 atlas 的关键优势：

```text
atlas 每增一帧 -> 增一整帧颜色纹理
buffer 动画每增一帧 -> 增一份所有顶点的 xy 数据
```

---

## 当前关于精度的判断

结合现有讨论，后续协作者请直接按以下判断工作：

### 1. 不把 8 位方案当正式目标

虽然 8 位 `xy` 在数学上未必完全不可用，
但它过于激进，不适合作为当前主方向的正式落地精度。

主要原因：

- 小单位轮廓对抖动很敏感
- 我们现在是在保护 `Spine` 连续动画观感
- 既然已经走到 GPU buffer 方案，没有必要在最关键的数据精度上过度冒险

### 2. 当前下限应视为 16 位 `xy`

也就是优先评估：

- `RG16`
- 或 `RG16F`

### 3. 不需要 32 位 `xy`

对当前三头身小单位，这基本属于过度。

---

## 当前关于资源量级的判断

当前讨论中的典型单位，顶点规模约：

```text
~300 vertices
```

在这个量级下，如果逐帧只存 `xy`：

- 16 位 `xy` 每顶点每帧约 4 bytes
- 单帧约 `300 * 4 = 1200 bytes`

对比 atlas 的示例：

```text
64 * 64 * RGB8 = 12288 bytes / frame
```

因此在这个量级下，结论很明确：

```text
Spine 小单位逐帧顶点动画 buffer
在每帧边际成本上
显著优于整帧 atlas
```

这也是本方向成立的一个关键前提。

---

## 当前最重要的工程边界

后续协作者不要先在 shader 细节上打转，先确认以下边界。

### 1. 运行时真正需要编码的不是“纹理 uv”，而是 buffer 布局

即使不做 VAT 纹理，仍然必须定义：

- `vertexId` 如何稳定取得
- `animationId` 如何映射到数据块
- `frameIndex` 如何映射到偏移
- 不同角色 / 不同 mesh 的数据区间如何组织

也就是说，我们摆脱的不是编码，而是：

```text
从 texture layout encoding
转成 buffer layout encoding
```

### 2. CBuffer 只放小参数

不要把大块逐顶点动画数据往常量缓冲里塞。

这里要区分清楚：

- `CBuffer / instance data`
  用于当前实例的小参数
- `GlobalBuffer / StructuredBuffer`
  用于大规模逐顶点逐帧数据

### 3. 第一版不要同时追求过多 Spine 运行时特性

如果后面原型阶段出现这些能力：

- attachment 切换
- 多拓扑切换
- 多 region 动态切换
- 大量 slot 级运行时组合

那么复杂度会快速上升。

所以第一版原型要主动收束资源规范，而不是默认保留全部 Spine 灵活性。

---

## 当前建议的原型路线

### Step 1

先选一个真实三头身单位作为样本。

要求：

- 顶点规模约 300
- 一个循环动作
- 一个非循环动作
- 尽量保证拓扑稳定

### Step 2

离线烘焙出：

- 一个静态 `Mesh`
- 一个共享 `BaseTexture`
- 一份逐帧 `xy` 动画 buffer 数据

### Step 3

定义第一版 buffer 布局协议。

至少回答：

- 顶点如何寻址
- 动画如何分段
- 帧如何偏移
- 多角色是否共享同一总 buffer

### Step 4

在现有 instancing 绘制链里做最小 shader 原型。

验证：

- 当前实例参数是否足够
- `vertexId + animationId + frameIndex` 的取数链路是否稳定
- 16 位 `xy` 观感是否足够
- 是否比 atlas 路径更符合当前资产现实

### Step 5

原型稳定后，再决定是否继续扩展：

- 资源规范
- 多动作组织
- 多单位共享 buffer
- attachment / region 约束

---

## 编辑器转换工具

后续协作者需要明确：

这个方向如果要落地，不能停留在“运行时 shader 怎么写”，必须配一套编辑器侧的离线转换工具。

目标不是让运行时直接吃 `.spine`，而是：

```text
.spine source
-> 编辑器转换工具
-> Mesh + BaseTexture + VABuffer
-> 运行时直接加载项目自定义资产
```

也就是说，运行时主链不应继续依赖完整 `Spine` 运行时求值。

### 1. 转换工具的目标输出

对每个可转换的单位动画资产，工具最终至少要产出三类结果：

#### Mesh

静态网格资源，负责：

- 顶点拓扑
- 顶点顺序
- 静态 `uv`
- 必要时的 `vertexId`

#### BaseTexture

基准纹理资源，负责：

- 颜色来源
- 运行时 shader 的纹理采样对象

#### VABuffer

动画数据资源，负责：

- 每个动画片段
- 每一帧
- 每个顶点的 `xy`

这里的 `VABuffer` 可以在资产层表现为：

- 二进制 blob
- `ScriptableObject` 持有的 byte array / NativeArray 序列化数据
- 或后处理生成的 GPU-friendly buffer asset

第一版不必过早纠结最终容器形式，但必须保证它表达的是：

```text
animationId + frameIndex + vertexId -> xy
```

### 2. 转换工具的推荐职责边界

请把工具职责收束为“离线烘焙器”，不要把它做成运行时播放器。

它需要负责：

- 载入 `.spine` 源资产
- 解析 skeleton / slots / attachments / mesh deform
- 在编辑器中离线求值动画
- 把每一帧的最终顶点结果烘出来
- 收敛为运行时所需三类资产

它不需要负责：

- 游戏运行时播放控制
- 运行时状态机
- 运行时事件调度
- 运行时 `Spine` skeleton 逻辑

### 3. `.spine -> 目标资产` 的基本流程

后续协作者可以先按下面的转换流程理解。

#### Step A：读取源资产

输入至少包括：

- `.spine` 工程文件或对应可导出数据
- atlas / texture 资源
- 目标动作列表

这里的第一工程判断是：

```text
我们是直接读取 Spine 可导出数据，
还是借助 spine-unity 在编辑器里完成离线求值。
```

#### Step B：确定可转换约束

在真正烘焙前，要先判定该资产是否满足第一版约束，例如：

- 拓扑是否稳定
- 是否允许 attachment 切换
- uv 是否静态
- 是否单 atlas page
- 是否存在超出当前方案的 slot 组合

不满足约束的资产要在编辑器里明确报错，而不是硬烘。

#### Step C：离线逐帧求值

对每个目标动画片段：

- 以约定采样率推进时间
- 求出每一帧最终顶点结果
- 把每个顶点当前帧的 `xy` 记录下来

这里请注意：

- 这一步是“离线动画求值”
- 不是运行时播放
- 允许依赖 `Spine` 官方求值能力

#### Step D：构建静态 Mesh

从可转换的最终结构中抽出：

- 顶点顺序
- 三角形拓扑
- 静态 `uv`

然后生成项目自己的静态 `Mesh` 资产。

#### Step E：生成 BaseTexture

把运行时所需的颜色采样源固定下来。

第一版应优先追求：

- 单一基准纹理
- 单一材质路径
- 清晰的采样约束

#### Step F：打包 VABuffer

把离线逐帧结果组织为运行时可直接寻址的数据：

- animation block
- frame block
- vertex block

这里真正要定义的是 buffer layout，而不是贴图布局。

#### Step G：生成一个可直接被项目消费的包装资产

建议最终对外暴露一个项目自定义资产，例如：

```text
UnitGpuAnimationAsset
```

内部引用：

- Mesh
- BaseTexture
- VABuffer
- 动画片段描述
- 帧数 / 采样率 / 顶点数 / 边界盒等元信息

这样运行时主链就不必知道 `.spine` 的细节。

### 4. spine-unity 与 Animancer Pro 在这里分别能做什么

这两个名字在后续讨论里很容易被误用，所以这里直接写清楚。

#### spine-unity

它更适合承担：

- 编辑器中读取和求值 `Spine` 动画
- 作为离线转换工具的“求值后端”

它不应被默认视为：

- 最终运行时主播放器
- 海量 instancing 方案本体

也就是说，当前更自然的用法是：

```text
借助 spine-unity 在编辑器里把动画求出来
再烘焙成项目自定义 GPU 资产
```

#### Animancer Pro

当前语境里，它不是这条链路的核心依赖。

它更可能有价值的地方在于：

- 如果后续项目里同时存在常规 Unity 动画角色
- 或需要更方便的动画片段管理界面
- 可以作为别的动画系统辅助工具

但对本问题：

```text
.spine -> Mesh + BaseTexture + VABuffer
```

Animancer Pro 不是关键转换后端。

因此请不要把“安装 Animancer Pro”误认为“已经解决了 Spine GPU Buffer 动画转换”。

### 5. 当前推荐的工具形态

第一版建议直接做一个编辑器工作流，而不是先做一套复杂通用导入框架。

推荐形态：

- 一个 `EditorWindow`
- 或 `ScriptedImporter + Bake` 按钮
- 或 `ScriptableObject` 资产上的 `Bake` 按钮

最低要求：

- 选中源 Spine 资产
- 选择目标动作
- 选择采样参数
- 点击转换
- 输出项目自定义 GPU 动画资产

### 6. 第一版工具必须输出的诊断信息

不要只输出成功结果，必须同时输出检查信息。

至少包括：

- 顶点数
- 三角形数
- 动作数
- 每动作帧数
- 采样率
- BaseTexture 尺寸
- VABuffer 总字节数
- 是否发生 attachment 切换
- 是否检测到不稳定拓扑

这些信息决定后续协作者能不能判断“这份资产适不适合进入主线”。

### 7. 第一版工具的成功标准

这套工具第一版不是为了覆盖所有 Spine 资产。

成功标准应当收束为：

- 能把一个受约束的小型 Spine 单位
- 稳定转换成
- `Mesh + BaseTexture + VABuffer`
- 并被现有 instancing shader 原型正确播放

只要这件事成立，后面才有资格继续泛化。

---

## 协作者注意事项

如果你接下来继续推进，请避免以下误区：

### 1. 不要再把问题退回成“spritesheet 该多少帧率”

这不是当前主问题。

### 2. 不要默认把“VAT”理解成“必须是纹理采样顶点动画”

在当前项目里，更自然的落地是 buffer 回放。

### 3. 不要急着追求全量兼容 Spine 全部特性

先让一条窄而稳定的主链成立，再谈泛化。

### 4. 不要把第一版原型复杂化成大而全工具链

先打通：

```text
1 unit
1 mesh
1 base texture
1 animation buffer
1 instanced shader path
```

这是最关键的闭环。

---

## 当前结论

当前方向已经收束为：

```text
Spine
-> 离线烘焙逐帧顶点结果
-> Mesh 提供静态几何与 uv
-> BaseTexture 提供颜色
-> StructuredBuffer 提供逐帧 xy
-> shader 根据 instance 参数回放
-> 继续走 instancing
```

如果后续没有新的强约束推翻这个判断，
那么接下来最值得做的不是继续讨论抽象术语，
而是直接做第一版最小原型。
