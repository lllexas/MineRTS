# DrawSystem Billboard Phase 2 Progress

## 日期
2026-05-03

## 目标

记录 `DrawSystem` billboard 二阶段当前已接入的链路、现状、已知问题，以及下一步可继续改进的点。

---

## 当前范围

当前只处理：

- `DrawSystem`
- `UnitAtlasAnimationSetSO`
- `UnitAnimationIntentBridge`
- `UnitAnimationArbiter`
- `UnitAnimationPlayback`
- `UnitAtlasBillboardRenderService`

明确还没处理：

- `TransportDrawSystem`
- `OverlayDrawSystem`
- `BuildingController` 的 ghost
- atlas 动画的 GPU 侧 frameIndex 优化
- 多方向资源

---

## 当前进度

### 1. Phase 1 已完成

`DrawSystem` 已经具备 atlas billboard 基础链路：

```text
EntityBlueprintSO.AnimationSetSO
-> atlas texture
-> frame uv
-> billboard matrix
-> UnitAtlasBillboardRenderService
```

当前静态显示能力已具备：

- atlas 单位可以站起来显示
- `SpriteLib` 继续作为无动画单位 fallback
- gizmo 可用于查看 quad 边框和锚点

### 2. Phase 2 主链已接入

当前 `DrawSystem` 已不再只取默认帧，而是正式接入：

```text
UnitAnimationIntentBridge
-> UnitAnimationArbiter
-> UnitAnimationPlayback
-> FrameCoord
-> atlas billboard draw
```

### 3. 已完成的运行时持久状态

当前每个 atlas 单位已经有自己的播放状态缓存：

```text
key = core.CreationIndex
value = UnitAnimationPlaybackState
```

作用：

- 避免每帧从 0 开始播
- 支持跨帧保持 `CurrentState / LocalFrame / TickRemainder / LastTick`
- 支持实体消失后自动回收缓存

### 4. 当前状态优先级

现已接入：

```text
Death > Attack > Work > Move > Idle
```

实现位置：

- `UnitAnimationIntentBridge`
- `UnitAnimationArbiter`

### 5. 当前播放节拍

现已接入：

```text
currentTick = TimeTicker.GlobalTick
```

也就是动画完全走逻辑 tick，而不是走 `deltaTime` 浮点累计。

### 6. 当前 flipX

现已接入：

- `UnitAnimationIntent.FlipX`
- `UnitAnimationFrameResult.FlipX`
- `DrawSystem` 中通过负 `scale.x` 进行翻转

前提：

```text
animationSet.AllowFlipX == true
```

---

## 当前实现事实

### DrawSystem 当前 atlas 路径

对于有 `AnimationSetSO` 的单位：

```text
Build intent
-> Evaluate playback
-> Get frame coord
-> Get uv rect
-> Build billboard matrix
-> Enqueue atlas draw
```

对于没有 `AnimationSetSO` 的单位：

```text
继续走 SpriteLib / SpriteInstanceRenderService
```

### 当前脚底锚点

当前按项目现状采用：

```text
core.Position 视为格子中心
footAnchor = (x + 0, y - 1/6)
```

这只是当前约定，不是最终不可变真理。后续如果资源基线或格子语义调整，这里仍可能微调。

### 当前 billboard 旋转状态

目前为了定位问题，`InStageRenderSpace.MakeBillboardMatrix(...)` 中的 billboard 旋转仍处于调试状态。

现阶段重点不是继续修改动画系统，而是确认：

- quad 站地
- 锚点正确
- atlas 内容基线合理

再继续收 billboard 旋转约束。

---

## 当前已知问题

### 1. billboard 旋转还没正式收口

这是当前最明显的未完成项。

现在已经确认：

- 锚点可以锁到 `z=0`
- atlas quad 是 bottom-pivot

但“既朝向正确又不把底边掀离地面”的旋转约束还没有最终定版。

### 2. IntentBridge 仍然偏朴素

当前判断规则：

- `WantsMove = move.LogicalPosition != move.PreviousLogicalPosition || move.Timer > 0`
- `WantsWork = work.WorkType != None`
- `WantsAttack = attack.TargetEntityId != 0 && attack.TargetEntityId != -1`

这足够跑通，但还比较粗。

后续可能需要细化：

- 攻击前摇是否单独表达
- 工作是否需要细分类型
- 移动意图是否应区分“真移动”和“阻塞抖动”

### 3. DrawSystem 仍直接持有动画桥接职责

当前是为了快速闭环，直接在 `DrawSystem` 里做：

```text
intent build
playback evaluate
frame resolve
```

这没错，但后续如果逻辑继续增长，可以考虑抽出一层：

```text
UnitAtlasAnimationRuntimeBridge
```

让 `DrawSystem` 只拿结果，不直接知道播放细节。

### 4. flipX 目前只靠负 scale

这是第一版最省事的办法。

后续要验证两件事：

- shader / instancing / ZWrite 下负 scale 是否始终稳定
- 某些 atlas 是否更适合通过 UV 翻转而不是矩阵负缩放

### 5. draw.AnimationFrame 的语义仍不统一

现在 atlas 单位会把当前 `LocalFrame` 写回：

```text
draw.AnimationFrame = frameResult.LocalFrame
```

但项目里已有旧逻辑把它当别的用途使用，例如电池帧显示。

后续要决定：

- 继续把它当通用“当前帧”字段
- 还是保留给旧静态逻辑，atlas 单位改用别的字段

---

## 下一步建议

建议顺序如下。

### Step 1

先收 billboard 旋转：

- 保证脚底不离开 `XY`
- 保证朝向正确
- 不再混淆 full billboard 和 upright billboard

### Step 2

验证当前状态切换：

- `Idle`
- `Move`
- `Work`
- `Attack`
- `Death`

重点看：

- 切换时机
- `TicksPerFrame`
- `LockUntilComplete`

### Step 3

收 `IntentBridge` 判定质量：

- 移动是否过度触发
- 攻击是否过早/过晚触发
- 工作状态是否需要更细粒度

### Step 4

根据运行时结果决定是否抽一层动画运行时桥接服务，减轻 `DrawSystem` 直接持有的职责。

---

## 当前结论

一句话总结当前状态：

```text
二阶段主链已经接上了，
现在剩下的不是“有没有动画播放”，
而是 billboard 旋转约束、状态判定细节、以及代码职责收口。
```
