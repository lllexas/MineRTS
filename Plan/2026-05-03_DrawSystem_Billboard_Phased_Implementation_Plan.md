# DrawSystem Billboard Phased Implementation Plan

## 日期
2026-05-03

## 范围

本计划只处理 **ECS 单位主绘制链路**，也就是 `DrawSystem`。

明确不在本阶段处理：

- `TransportDrawSystem` 的传送带/飞行物品
- `OverlayPowerSystem` 的电力光环与连线
- `BuildingController` 的 ghost `SpriteRenderer`
- 全局移除 `SpriteLib`

结论：

```text
SpriteLib 继续服务于静态/无动画对象
DrawSystem 单独迁移到 atlas billboard 渲染链
```

---

## 当前判断

### 1. `SpriteLib` 不再适合作为 ECS 单位主绘制入口

原因：

- 现有模型是 `SpriteId -> Mesh + Material`
- 新单位渲染目标是 `AnimationSetSO -> FrameCoord -> Atlas UV -> Billboard`
- `SpriteLib` 更适合静态 sprite 或无动画对象

因此：

```text
DrawSystem 从 SpriteLib 主链迁出
TransportDrawSystem 暂时继续保留 SpriteLib
```

### 2. 当前世界继续保持 XY

本次 billboard 改造不把世界掰到 XZ。

当前规则：

```text
logic.x -> world.x
logic.y -> world.y
layer    -> world.z
```

billboard 只改变“朝向”，不改变世界坐标协议。

### 3. 单位 billboard 的第一原则是“站地”

不先设计通用 `standPivot` 系统。

单位 atlas quad 的默认几何应为：

```text
x: -halfW ~ halfW
y: 0 ~ height
z: 0
```

也就是底边中心为默认脚底基线。

---

## 实施策略

采用两阶段。

---

## Phase 1

### 目标

先不接动画状态机。

对于每个 ECS 单位：

```text
如果蓝图上有 UnitAtlasAnimationSetSO
就固定绘制该动画集某个默认 frame
并以 billboard 方式站立显示
```

当前约定的默认 frame：

```text
clip = Idle（若存在）
frame = clip 的第 0 帧
```

如果后续要临时改成“直接使用 atlas 的 0,0 frame”，也可以，但推荐仍然优先走 `Idle[0]`，因为这更接近未来正式链路。

### Phase 1 要写的代码

#### 1. 新的单位 atlas instancing 渲染服务

新增一个只服务单位 atlas billboard 的渲染服务，例如：

```text
Assets/Scripts/InStage/Rendering/UnitAtlasBillboardRenderService.cs
```

职责：

- 按 atlas/material/render band 分批
- 接收单位 billboard draw request
- 使用 instancing 提交绘制

建议 request 结构：

```csharp
struct UnitAtlasBillboardDrawRequest
{
    public Texture2D AtlasTexture;
    public Rect UvRect;
    public Matrix4x4 Matrix;
    public int RenderQueue;
    public int RendererPriority;
}
```

说明：

- 第一阶段可以直接传 `Rect UvRect`
- 不必一开始就上 shader frameIndex 解码

#### 2. billboard 专用 mesh 规则

需要固定一套“站地 quad”。

建议：

- 不复用 `SpriteLib` 当前中心 pivot mesh
- 在新服务内或独立 helper 内生成 bottom-pivot quad mesh
- 先按 `64 / 128 / 256` 帧档生成少量缓存 mesh 即可

#### 3. billboard atlas shader

需要一个 atlas billboard shader，例如：

```text
Assets/Shaders/Sprite/AtlasBillboardInstancing.shader
```

第一阶段要求：

- 支持 instancing
- 支持 `_MainTex`
- 支持每实例 UV Rect
- 支持透明裁剪
- 保持 `ZWrite On`

不要求：

- frameIndex 直接在 GPU 解码
- 特殊特效

#### 4. DrawSystem 接入默认 atlas frame

`DrawSystem` 中新增 ECS 单位 atlas 分支：

逻辑：

```text
蓝图有 AnimationSetSO
-> 取 Idle clip
-> 取第 0 帧
-> 取 FrameCoord -> UvRect
-> 构造 billboard matrix
-> 提交到 UnitAtlasBillboardRenderService
```

如果单位没有 `AnimationSetSO`：

```text
继续走旧 SpriteLib 路径
```

这样第一阶段能平滑过渡，不需要一次替换全部资源。

### Phase 1 验收标准

- ECS 单位能以 billboard 方式显示
- quad 默认脚踩地
- 不需要动画，也能看到 atlas 单位立起来
- 没有 `AnimationSetSO` 的单位仍能继续显示
- `TransportDrawSystem` 不受影响

---

## Phase 2

### 目标

在 Phase 1 的 billboard atlas 静态显示基础上，补齐动画播放链路：

```text
Intent Blackboard
-> State Arbitration
-> Playback
-> 当前 FrameCoord
-> billboard instanced draw
```

### Phase 2 要写的代码

#### 1. 意图黑板

每个 ECS 单位需要一个轻量动画输入视图，例如：

```text
IsDead
WantsMove
WantsWork
WantsAttack
FlipX
```

第一版可以不落成 ECS 新组件，先在 `DrawSystem` 或 bridge 层按帧生成。

#### 2. 状态仲裁

复用或完善现有：

- `UnitAnimationIntentBridge`
- `UnitAnimationArbiter`

优先级：

```text
Death > Attack > Work > Move > Idle
```

#### 3. Tick 播放

复用现有：

- `UnitAnimationPlayback`

输入：

```text
AnimationSetSO + Intent + CurrentTick
```

输出：

```text
FrameCoord
FlipX
```

#### 4. DrawSystem 正式切换

`DrawSystem` 不再只拿默认帧，而是：

```text
蓝图 + 实体状态
-> Intent
-> Playback.Evaluate(...)
-> FrameCoord
-> UvRect
-> billboard draw request
```

### Phase 2 验收标准

- 单位能根据 ECS 状态切换 `idle/move/work/attack/death`
- 动画按 tick 推进
- billboard 渲染继续稳定
- 渲染链不再依赖 `DrawComponent.SpriteId` 作为单位主来源

---

## 非目标

以下内容不在当前两阶段内：

- `TransportDrawSystem` atlas 化
- item 动画化
- 多方向资源
- UV frameIndex GPU 解码优化
- 建筑 ghost billboard 化
- overlay 几何重构

---

## 实施顺序

建议严格按下面顺序做：

1. 新建 atlas billboard shader
2. 新建 `UnitAtlasBillboardRenderService`
3. 做 bottom-pivot 站地 mesh 缓存
4. `DrawSystem` 接 `AnimationSetSO` 的默认帧显示
5. 验证站地、排序、深度
6. 再接 `IntentBridge + Arbiter + Playback`

---

## Phase 1 前置调研结论

### 当前代码落点

#### 必改文件

1. [DrawSystem.cs](G:/ProjectOfGame/MineRTS/Assets/Scripts/InStage/System/DrawSystem.cs)

作用：

- 当前 ECS 主绘制入口
- 现在仍按 `DrawComponent.SpriteId -> SpriteLib -> SpriteInstanceRenderService`
- 这里必须新增 atlas billboard 分支

Phase 1 中应改：

- 保留旧 `SpriteLib` 路径作为 fallback
- 对有 `AnimationSetSO` 的单位，改走 atlas billboard 渲染
- 第一阶段只取默认 frame，不接完整动画状态机

2. [InStageRenderSpace.cs](G:/ProjectOfGame/MineRTS/Assets/Scripts/InStage/Rendering/InStageRenderSpace.cs)

作用：

- 统一逻辑坐标到世界坐标
- 统一 sprite / billboard matrix 生成

当前状态：

- 已清理掉旧的 `XZ` 假设
- 现在保持 `XY` 世界协议

Phase 1 中应改：

- 不需要大改坐标协议
- 只需要确保 `MakeBillboardMatrix(...)` 满足站地 billboard 需求

3. [EntityBlueprintSO.cs](G:/ProjectOfGame/MineRTS/Assets/Scripts/InStage/Component/EntityBlueprintSO.cs)

作用：

- 已经提供 `AnimationSetSO`

当前状态：

- Phase 1 所需字段已经具备
- 不需要新增字段

#### 保持不动的文件

1. [TransportDrawSystem.cs](G:/ProjectOfGame/MineRTS/Assets/Scripts/InStage/System/TransportDrawSystem.cs)

- 继续走 `SpriteLib`
- 不属于本阶段

2. [OverlayDrawSystem.cs](G:/ProjectOfGame/MineRTS/Assets/Scripts/InStage/System/OverlayDrawSystem.cs)

- 电力光环和连线不依赖 `SpriteLib`
- 不属于 billboard atlas 第一阶段

3. [BuildingController.cs](G:/ProjectOfGame/MineRTS/Assets/Scripts/InStage/Controller/BuildingController.cs)

- ghost 仍然是 `SpriteRenderer`
- 不属于本阶段

4. [SpriteLib.cs](G:/ProjectOfGame/MineRTS/Assets/Scripts/InStage/SpriteLib.cs)

- 当前继续保留，服务静态对象与旧链路
- 本阶段不直接删除，不直接重构

### 新文件放置建议

#### 1. 新渲染服务

放在：

```text
Assets/Scripts/InStage/Rendering/UnitAtlasBillboardRenderService.cs
```

原因：

- 现有 `Rendering/` 目录已经承载渲染基础设施
- `SpriteInstanceRenderService` 与 `InStageRenderSpace` 也在这里
- 新服务属于同一层级，而不是系统层

#### 2. 新 shader

放在：

```text
Assets/Shaders/Sprite/AtlasBillboardInstancing.shader
```

原因：

- 当前 sprite 相关 shader 已在 `Assets/Shaders/Sprite/`
- 这是 `SimpleInstancing.shader` 的并行替代，而不是无关新类别

#### 3. 如需 mesh helper

如果第一阶段把 mesh 生成逻辑独立出去，建议放在：

```text
Assets/Scripts/InStage/Rendering/UnitBillboardMeshFactory.cs
```

如果实现很小，也可以先内聚在 `UnitAtlasBillboardRenderService` 内部，不急着拆。

### 当前可复用资产

#### 可直接复用

- `EntityBlueprintSO.AnimationSetSO`
- `UnitAtlasAnimationSetSO.GetFrameUvRect(...)`
- `UnitAtlasAnimationSetSO.FrameTier`
- `InStageRenderSpace.MakeBillboardMatrix(...)`
- `DrawSystem` 现有的血条逻辑

#### 当前不应复用

- `SpriteLib.GetMesh(...)`
- `SpriteLib.GetMaterial(...)`
- `SpriteInstanceRenderService` 作为 ECS atlas billboard 的最终渲染后端

原因：

- 它们的 key 仍然围绕 `SpriteId`
- 与 atlas frame/uv 驱动模式不匹配

### Phase 1 最小实现面

真正需要新增或改动的核心面只有四处：

1. 新 shader：`AtlasBillboardInstancing.shader`
2. 新服务：`UnitAtlasBillboardRenderService.cs`
3. `DrawSystem.cs` 接入 atlas billboard fallback 分支
4. 如有必要，对 `InStageRenderSpace.MakeBillboardMatrix(...)` 做小修

这就是第一阶段应控制住的修改面。

---

## 当前结论

一句话总结：

```text
第一阶段：先让单位 atlas 的默认帧站起来
第二阶段：再把意图黑板和状态仲裁接上
```

这条路和当前代码、资源、风险面是匹配的。
