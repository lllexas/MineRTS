# VA 动画状态机：黑板架构设计

**日期**: 2026-05-06  
**参考**: BBBNexus 3C 架构 (`Doc/BBBNexus_Architecture_Research.md`)  
**状态**: ✅ 已实现

---

## 1. BBBNexus → MineRTS ECS 映射

| BBBNexus (OOP 单主角) | MineRTS (ECS 百单位) |
|---|---|
| `PlayerRuntimeData` (中央黑板) | `WholeComponent.animationIntentComponent[]` (ECS 组件数组) |
| `InputPipeline` (读硬件 → 写黑板) | 各 ECS 系统直接 PUSH 到黑板 (Move/Attack/Industrial/Death) |
| `IntentProcessors` | (ECS 组件本身就是意图源, 无需额外处理器) |
| `GlobalInterruptProcessor` (CheckInterrupts) | `VAInterceptorChain.Resolve(intent, playback)` |
| `StateInterceptorSO` (ScriptableObject) | `VAInterceptorFunc` (静态委托) |
| `PlayerBaseState.UpdateStateLogic()` | `UnitAnimationPlayback.ApplyVAState()` |
| `PlayerBaseState.CheckInterrupts()` | `UnitAnimationPlayback.ResolveVAState()` |
| `ResetIntent()` (LateUpdate 清零) | `UnitAnimationIntentBridge.ResetAll(whole)` |
| `IAnimationFacade` | `UnitVARenderService` (渲染提交) |
| `MotionDriver` | `MoveSystem` (游戏逻辑位移 + 真实时间动画帧推进) |
| `CharacterArbiter` | (RTS 无多控制域冲突 — 暂不需要) |

---

## 2. 完整管线

```
EntitySystem.UpdateSystem()                              // 每帧
  ├─ TimeSystem.UpdateGameTick()
  │
  ├─ AutoAISystem.UpdateAI()                             // AI 锁定目标
  ├─ AttackSystem.UpdateAttacks()
  │    └─ animationIntent[i].WantsAttack = true           // PUSH: 实际攻击时
  ├─ IndustrialSystem.UpdateIndustrial()
  │    └─ animationIntent[i].WantsWork = true              // PUSH: 有工作时
  ├─ MoveSystem.UpdateMovement()
  │    ├─ animationIntent[i].WantsMove = true              // PUSH: Timer > 0
  │    └─ animationIntent[i].FlipX = ...                   // PUSH: 朝向
  ├─ DeathSystem.UpdateDeaths()
  │    └─ animationIntent[i].IsDead = true                 // PUSH: 死亡时
  │
  ├─ DrawSystem.UpdateDraws()                             // 读黑板, 纯消费
  │    └─ TryEnqueueVA(whole, entityIndex)
  │         ├─ intent = whole.animationIntentComponent[id]  // 1. 读黑板
  │         ├─ ResolveVAState(intent, playback)              // 2. 仲裁
  │         ├─ ApplyVAState(target, vaso, intent, ref pb)    // 3. 状态转移+帧推进 (Time.time)
  │         ├─ Interpolator.Compute(vaso, pb, state)         // 4. 补间
  │         ├─ TryGetGlobalFrameIndices(...)                 // 5. 全局帧偏移
  │         └─ RenderService.Enqueue()                       // 6. 提交渲染
  │
  └─ IntentBridge.ResetAll(whole)                           // 帧末清零 (保留IsDead)
```

---

## 3. 黑板组件 (PUSH 模型)

### 数据结构

```csharp
[Serializable]
public struct UnitAnimationIntent
{
    public bool IsDead;       // DeathSystem PUSH (ResetAll 保留)
    public bool WantsMove;    // MoveSystem PUSH
    public bool WantsWork;    // IndustrialSystem PUSH
    public bool WantsAttack;  // AttackSystem PUSH
    public bool FlipX;        // MoveSystem PUSH
}
```

### 各系统 PUSH 规则

| 意图 | 推送者 | 条件 | 备注 |
|------|------|------|------|
| `IsDead` | DeathSystem | `health.Health <= 0` | 持久态, ResetAll 保留 |
| `WantsMove` | MoveSystem | `move.MoveTimerTicks > 0` | 仅用 Timer, 不用 PreviousLogicalPosition (避免原地踏步 bug) |
| `FlipX` | MoveSystem | `core.Rotation.x < 0` | 总是推送 (静止单位也有朝向) |
| `WantsAttack` | AttackSystem | 实际执行攻击时 (在范围 + 冷却过) | 距离检查: `dist <= AttackRange + 0.5f` |
| `WantsWork` | IndustrialSystem | `work.WorkType != WorkType.None` | 每帧推送 |

### 位置

`WholeComponent.animationIntentComponent[]` — 与其他 ECS 组件数组并列，走同样的稀疏序列化。

### 生命周期

1. **帧前**: 各系统顺次执行, 将自己管理的意图推入 `animationIntentComponent[i]`
2. **帧中**: DrawSystem 读黑板消费, 决定动画状态
3. **帧末**: `IntentBridge.ResetAll()` 清零帧级标志 (保留 `IsDead`)

### 设计原则

各系统 PUSH 自己的意图, 不使用中心化的 Build() 推导。每个系统最清楚自己的状态, 不需要别人"猜"。

---

## 4. 拦截器链

### 优先级 (高→低)

| 优先级 | 拦截器 | 条件 | 命中 StateId |
|------|------|------|------|
| 0 | Death | `intent.IsDead` | `Death` |
| 1 | Stun | (暂未接入, 预留) | `Stun` |
| 2 | Attack | `intent.WantsAttack` | `Attack` |
| 3 | Work | `intent.WantsWork` | `Work` |
| 4 | Move | `intent.WantsMove` | `Move` |
| 5 | Idle | **fallback**, 永远命中 | `Idle` |

### 实现

```csharp
public delegate bool VAInterceptorFunc(
    in UnitAnimationIntent intent,
    in UnitAnimationPlaybackState current,
    out UnitAnimationStateId targetState);

public static class VAInterceptorChain
{
    // 6 个静态嵌套类 (Death/Stun/Attack/Work/Move/Idle)
    // Resolve() 按优先级顺序执行，第一个返回 true 的拦截器胜出
    public static UnitAnimationStateId Resolve(
        in UnitAnimationIntent intent,
        in UnitAnimationPlaybackState current);
}
```

### 可见性

- `UnitVABufferManager` Inspector 展示只读拦截器链列表
- `UnitVAStateDebugger` OnGUI 在每个单位下方显示彩色状态标签 + 意图摘要

---

## 5. 状态转移与帧推进 (真实时间驱动)

动画帧推进完全脱离 ECS tick 循环, 使用 `Time.time` 真实时间。

`TicksPerFrame` 重新解释为"每帧占几个 1/60 秒烘焙单位" (`BakeFrameRate = 60f`):
- `TicksPerFrame = 1` → 1/60s per frame (60 FPS)
- `TicksPerFrame = 3` → 3/60s per frame (20 FPS)
- `TicksPerFrame = 6` → 6/60s per frame (10 FPS)

```csharp
// Step 1 — 纯函数，无副作用
public static UnitAnimationStateId ResolveVAState(
    in UnitAnimationIntent intent,
    in UnitAnimationPlaybackState playback)
{
    return VAInterceptorChain.Resolve(intent, playback);
}

// Step 2 — 状态转移 + 帧推进 (Time.time)
public static void ApplyVAState(
    UnitAnimationStateId targetState,
    UnitVASO vaso,
    in UnitAnimationIntent intent,
    ref UnitAnimationPlaybackState playback,
    float currentTime,       // Time.time (不是 GlobalTick!)
    out int localFrame)
{
    if (playback.CurrentState != targetState || playback.LastAdvanceTime == 0f)
        playback.Reset(targetState, currentTime, intent.FlipX);    // 切换: frame=0
    else
        AdvanceVAFrames(vaso, ref playback, currentTime);          // 持续: 按真实时间推进
}

// AdvanceVAFrames 内部:
float deltaTime = currentTime - playback.LastAdvanceTime;
float totalTime = playback.FrameTimeRemainder + deltaTime;
float frameDuration = clip.TicksPerFrame / BakeFrameRate;         // 1/60s * Ticks
int frameAdvance = (int)(totalTime / frameDuration);
playback.FrameTimeRemainder = totalTime % frameDuration;           // float 秒余数
```

### 数据结构变更

| 字段 | 旧 (tick 驱动) | 新 (真实时间) |
|------|------|------|
| 余数 | `int TickRemainder` | `float FrameTimeRemainder` |
| 上次推进时间 | `long LastTick` | `float LastAdvanceTime` |
| Reset 参数 | `long currentTick` | `float currentTime` |

---

## 6. 补间器 (Frame Interpolator)

### 问题

- 动画帧按真实时间推进 (60 FPS 基准)
- 显示帧: 可变 FPS (60-144Hz)
- 显示帧几乎不会恰好落在动画帧边界上

### 方案

```
frameDuration = clip.TicksPerFrame / 60f          // 每动画帧的秒数
blendFactor = clamp(FrameTimeRemainder / frameDuration, 0, 1)

frameA = playback.LocalFrame                      // 当前帧
frameB = loop ? (frameA+1) % frameCount            // 循环: 下一帧 (wrap)
              : (frameA >= last) ? frameA           // 非循环: 末帧停留
                                  : frameA+1
```

### Shader 侧

```hlsl
float2 vaPosA = _VAPositions[frameOffsetA * _VAVertexCount + vertexID];
float2 vaPosB = _VAPositions[frameOffsetB * _VAVertexCount + vertexID];
float2 vaPos = lerp(vaPosA, vaPosB, blendWeight);
```

### 边界情况

| 情况 | 行为 |
|------|------|
| 循环 clip | frameB = (frameA+1) % frameCount，正常 blend |
| 非循环 clip 末帧 | frameB = frameA, blendWeight = 0 (原地停留) |
| 首帧 (LastAdvanceTime==0) | blendWeight = 0 |
| FrameTimeRemainder = 0 | blendWeight = 0 (恰在动画帧边界) |

---

## 7. GPU Buffer 查找

`UnitVABufferManager.TryGetGlobalFrameIndices()` 一次将 `(state, frameA, frameB)` 转换为两个全局 buffer 偏移：

```
globalFrameA = ClipMap[state].GlobalFrameStart + frameA
globalFrameB = ClipMap[state].GlobalFrameStart + frameB
```

RenderService 传递 3 个 instanced property: `_VA_FrameOffset`, `_VA_FrameOffset2`, `_VA_BlendWeight`。

---

## 8. TryEnqueueVA 完整步骤

```
TryEnqueueVA(whole, entityIndex):
  1. Lazy GPU buffer upload (RegisterVASO)
  2. Get/create playback state
  3. Read blackboard:  intent = animationIntentComponent[id]
  4. Resolve:          ResolveVAState(intent, playback) → targetState
  5. Apply:            ApplyVAState(target, vaso, intent, Time.time, out localFrame)
  6. Interpolate:      Interpolator.Compute(vaso, pb, state) → (frameA, frameB, blend)
  7. Buffer lookup:    TryGetGlobalFrameIndices(vaso, state, frameA, frameB) → (globalA, globalB)
  8. Billboard:        MakeBillboardMatrix(footAnchor, scale, camera, 0)
  9. Enqueue:          RenderService.Enqueue(vaso, matrix, globalA, globalB, blend)
```

---

## 9. Debug 可视化

`UnitVAStateDebugger` 提供双重调试:

**Inspector**: 拦截器链配置 + 实体状态列表

**OnGUI** (游戏内): 每个 VA 单位脚下绘制:
```
┌─────────┐
│  Move   │  ← 彩色状态标签
└─────────┘
攻✓ 移✓ → Move  ← 意图标志 + 仲裁胜者
```

颜色: Idle=灰 Move=青 Attack=红 Work=黄 Death=紫

---

## 10. 关键文件

| 文件 | 职责 |
|------|------|
| `UnitAtlasAnimationTypes.cs` | `UnitAnimationIntent`, `UnitAnimationPlaybackState`, `UnitAnimationStateId` |
| `UnitAnimationIntentBridge.cs` | 帧末 `ResetAll` (清零帧级意图, 保留 IsDead) |
| `VAInterceptorChain.cs` | 6 个拦截器 + Resolve + Inspector 列表 |
| `UnitAnimationPlayback.cs` | ResolveVAState + ApplyVAState + AdvanceVAFrames (Time.time 驱动) |
| `UnitVAInterpolator.cs` | 子帧补间: frameA/frameB/blendWeight |
| `UnitVABufferManager.cs` | GPU ComputeBuffer 管理 + TryGetGlobalFrameIndices 双帧查找 |
| `UnitVARenderService.cs` | 按 UnitVASO 批处理, 传递双帧偏移+混合权重 |
| `UnitVAShader.shader` | StructuredBuffer<float2> + SV_VertexID + lerp |
| `UnitVAStateDebugger.cs` | Inspector 视图 + OnGUI 游戏内覆盖 |
| `DrawSystem.cs` | TryEnqueueVA: 读黑板→仲裁→推进→补间→buffer→渲染 |
| `EntitySystem.cs` | WholeComponent 含 animationIntentComponent, UpdateSystem 末尾 ResetAll |
| `MoveSystem.cs` | PUSH WantsMove + FlipX |
| `AttackSystem.cs` | PUSH WantsAttack |
| `IndustrialSystem.cs` | PUSH WantsWork |
| `DeathSystem.cs` | PUSH IsDead |

---

## 11. 已知 Bug 修复记录

| Bug | 根因 | 修复 |
|------|------|------|
| 原地踏步 | `wantsMove` 用 `PreviousLogicalPosition != LogicalPosition`，后者永不追上 | 改为只用 `Timer > 0f` |
| 攻击动画不播放 | `wantsAttack` 用 `!= 0` 排除，但实体 ID 0 合法 | 改为只用 `!= -1` |
| 未进攻击范围就切攻击动画 | AI 锁定目标即设 `TargetEntityId`，不管距离 | 加距离检查 `dist <= AttackRange + 0.5f` |
| 动画帧推进锁死在 10Hz | 用 `GlobalTick` 驱动帧推进 | 改为 `Time.time` 真实时间, `BakeFrameRate = 60` |
