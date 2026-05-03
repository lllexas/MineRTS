# InStage Billboard Atlas Animation Plan

## 日期
2026-05-02

## 背景

当前 InStage 的单位与物品绘制已经开始从分散实现收敛到统一的 instancing 绘制服务，但真正的 billboard 单位资源和动画播放方案还没有定。

当前渲染 API 仍然是：

```text
Sprite/Atlas Resource
-> Mesh + Material
-> Graphics.RenderMeshInstanced
```

因此现在需要先定清楚：

- billboard 单位的 atlas 资源规范
- tick 驱动的序列帧动画规则
- 逻辑尺寸和像素尺寸的对应关系
- atlas 采样保护策略

这一步先做设计，不急着实现。

---

## 当前结论

### 1. 动画最终方向采用 atlas/UV

当前 DrawSystem 用 `SpriteId` 分批是兼容现状的，但长期来看：

- 状态数会增加
- 帧数会增加
- 单位量会增加
- 透视 billboard 后资源将更依赖规整动画资产

因此最终方案应以 atlas/UV 为主，而不是长期依赖“每帧一个 SpriteId”。

结论：

```text
动画状态机产出 frameIndex
渲染层根据 frameIndex 在 atlas 中定位 UV
instancing 继续保留
```

### 2. 动画时间轴使用 tick

项目已有 tick 系统，因此动画推进直接绑定 tick：

```text
每 N 个 tick 进一帧
```

好处：

- 动画和逻辑节拍一致
- 不需要 per-entity 浮点时间驱动
- 可控、好调试、天然同步
- 批次不会因为完全自由的时间相位被打碎

### 3. 单位会在屏幕上表现得比较小

当前预期更接近 RTS 小单位，而不是大幅展示角色细节。

因此资源设计优先级应为：

- 强轮廓
- 强动作节奏
- 小尺寸下可读
- 低到中等动画帧率

而不是：

- 高分辨率细节
- 复杂多方向写实资源

---

## 核心规范

### 1. 基准像素密度

确定：

```text
BasePixelsPerCell = 64
```

含义：

- 逻辑上的 1 个格子，默认以 64 像素为基准帧规格参考
- 这是资源设计基准，不是世界缩放公式本身

### 2. 不使用 64+1 / 64+2 外扩策略

这条是硬约束。

不允许的方案：

```text
把 64x64 帧物理扩成 65x65、66x66
把采样保护建立在非 2 幂次方单帧外扩上
把每帧定义成怪尺寸依赖透明扩边补丁
```

原因：

- 资源规格会变脏
- atlas 管线难以统一
- Unity 资源管理和长期维护都不舒服
- shader/导入/切片规则会变得含糊

正确约束：

```text
FrameLogicalSize 如果定义为 64x64，那就是严格的 64x64
采样保护不能依赖 64+1 这种外扩做法
```

### 3. 内容留白和采样保护是两回事

必须明确区分：

#### 内容留白

这是美术构图问题。

意思是：

```text
角色内容在 64x64 内部摆放
不要求画满
底边中心作为脚底基线
顶部和左右允许自然留白
```

#### 采样保护

这是 atlas 采样问题。

本项目当前倾向：

```text
每帧逻辑区域严格固定
shader 对当前帧 UV 做 clamp
不依赖非 2 幂外扩做采样保护
```

---

## 帧规格方案

### 1. 不直接使用任意怪尺寸

虽然逻辑上可以说：

```text
Frame = LogicSize * 64
```

但实际资源规范不应完全照这个公式放任增长，否则会出现很多不规整尺寸。

正确做法：

```text
LogicSize 决定内容预算
实际资源帧尺寸收敛到少数标准档位
```

### 2. 标准帧档位

第一版建议只用三档：

```text
S = 64x64
M = 128x128
L = 256x256
```

### 3. LogicSize 到帧档位的建议映射

建议：

```text
1x1 -> 64x64
1x2 / 2x1 / 2x2 -> 128x128
更大体型 -> 256x256
```

说明：

- 这里不追求和 `LogicSize * 64` 严格一一对应
- 目标是让资源规格、图集排布、shader 计算都保持规整

### 4. Pivot 规范

统一采用：

```text
底边中心
```

含义：

- billboard 站立时脚底落在地面
- 不使用中心点作为角色默认 pivot

---

## Atlas 组织方式

### 1. 一个 ECS 单位类型的常用动画帧放在同一张纹理内

建议：

```text
一个单位类型 = 一张 atlas
```

例如：

```text
worker_atlas.png
dog_atlas.png
soldier_atlas.png
```

理由：

- 管理简单
- UV 规则清晰
- 不需要做过度复杂的跨图集调度
- 显存成本在当前目标下可接受

### 2. Atlas 不追求极限压缩

不需要做 TMP 式高压缩或复杂 packing。

优先目标：

- 规则排列
- 容易从 frameIndex 反推 UV
- 资源容易维护

不优先：

- 极限面积利用率
- 不规则 packing
- 复杂 UV 表

### 3. Atlas 建议规则

建议采用：

```text
固定网格
统一帧尺寸
按连续 frameIndex 排列
状态区段连续存放
```

例如：

```text
idle   frame 0-3
move   frame 4-9
attack frame 10-13
death  frame 14-19
```

---

## 动画规则

### 1. 第一版状态集合

建议先收敛到：

```text
idle
move
work
attack
death
```

### 2. 第一版帧数建议

建议：

```text
idle: 4
move: 6
work: 4
attack: 4
death: 6
```

这对 RTS 小单位已经足够。

### 3. Tick 驱动建议

按当前 tick 系统，第一版建议：

```text
idle: 每 4 tick 一帧
move: 每 2 tick 一帧
work: 每 2 tick 一帧
attack: 每 1-2 tick 一帧
death: 每 1 tick 一帧
```

### 4. 第一版不做复杂方向集

建议：

```text
单朝向
必要时允许 flipX
```

不建议第一版就做：

```text
8 向
16 向
复杂多朝向资源集
```

原因：

- 小单位屏幕尺寸下收益有限
- 资源量和状态机复杂度显著上升

---

## 渲染层数据设计方向

### 1. 动画系统不应直接产出 SpriteId

长期正确抽象：

```text
AnimationState -> frameIndex
Renderer -> frameIndex 对应 atlas uv
```

而不是：

```text
AnimationState -> spriteId
```

这样后续替换渲染实现时，状态机不需要重写。

### 2. 每实例需要的关键参数

第一版 atlas 动画下，每实例绘制至少需要：

```text
atlas resource id
frameIndex
flipX
render band
world matrix
```

后续可能扩展：

```text
clip metadata id
team tint
animation variant
```

### 3. Shader 责任

shader 第一版只负责：

```text
根据 frameIndex 和 atlas 网格规则计算当前帧 UV
对当前帧 UV 范围做 clamp
正常输出 instanced sprite
```

不在第一版做：

```text
复杂插帧
骨骼动画
多图层混合
高阶材质特效驱动动画
```

---

## 对现有系统的影响

### 1. DrawSystem

后续需要从“固定 SpriteId 渲染”过渡到：

```text
根据 ECS 状态推导当前动画状态
根据 tick 推导 frameIndex
把 frameIndex 交给 atlas 渲染层
```

### 2. SpriteLib

当前 `SpriteLib` 主要围绕 `Sprite -> Mesh/Material`。

后续大概率要扩成：

```text
Atlas Resource -> Material
Frame Grid Metadata -> UV Rule
```

也就是说，`SpriteLib` 未来可能不再只是“单帧 sprite 仓库”，而是“单位 atlas 资源仓库”。

### 3. 渲染服务

已经抽出的 `SpriteInstanceRenderService` 后续应继续保留，但其输入会从：

```text
SpriteId + Matrix
```

逐步演进为：

```text
AtlasId + FrameIndex + Matrix + InstanceParams
```

---

## 分阶段计划

### Phase 1：先定规范，不改渲染

完成：

- `BasePixelsPerCell = 64`
- 标准帧档位 `64 / 128 / 256`
- Pivot = 底边中心
- atlas/UV 作为长期方向
- Tick 驱动动画
- 禁止 `64+1` 外扩策略

### Phase 2：补动画数据模型

目标：

- 定义单位 atlas 资源描述
- 定义 clip 的 `startFrame/frameCount/ticksPerFrame/loop`
- 定义 `frameIndex` 的计算方式

### Phase 3：改 DrawSystem 的动画状态选择

目标：

- 从 ECS 状态推导 `idle/move/work/attack/death`
- 基于 tick 得到当前 `frameIndex`

### Phase 4：改 shader / render service

目标：

- 从 `SpriteId` 风格过渡到 `Atlas + UV`
- 每实例传入 `frameIndex`
- shader 计算当前帧 UV 并做 clamp

### Phase 5：再接 billboard 和透视相机输入

这一步在 atlas 动画方向明确后再继续，不要倒序推进。

---

## 当前建议

当前最合理的下一步不是继续动 billboard 旋转，而是：

1. 先把 atlas 动画的数据格式设计出来
2. 定义 clip 元数据
3. 定义 DrawSystem 如何从 ECS 状态推导动画状态
4. 再改 shader 和实例参数

也就是说，接下来应从“资源与动画协议”入手，而不是先堆更多渲染实现。

