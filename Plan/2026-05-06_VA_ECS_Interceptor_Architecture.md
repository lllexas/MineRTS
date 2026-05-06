# ECS 动画状态机：拦截器链架构设计

**日期**: 2026-05-06  
**参考**: BBBNexus 3C 架构 (G:\ProjectOfGame\Mi-Demo-Spring\Assets\BBBNexus)

---

## 1. 核心差异认知

BBBNexus 是 3D 单主角，OOP 没问题。MineRTS 是 2D ECS 百单位，必须数据驱动。

| | BBBNexus | MineRTS |
|------|------|------|
| 单位数 | 1 个 Player | 0~1024 个 Entity |
| 状态实例 | 每个状态一个 class 实例 (new MoveState) | 不能 per-unit per-state new |
| 输入源 | IInputSource (硬件) | ECS 组件 (MoveComponent, AttackComponent, ...) |
| 动画引擎 | Animancer (Unity Animator) | 自定义 VA buffer 回放 |
| 运行时机 | MonoBehaviour Update/LateUpdate | EntitySystem.UpdateSystem 固定管线 |

**结论：拦截器链和状态枚举可以搬，但状态行为必须用静态方法 + 数据驱动，不能 OOP。**

---

## 2. 目标架构

```
                                    ┌──────────────────────────┐
  ECS 组件 ──→ UnitAnimationIntentBridge.Build()              │
  (Move/Attack/           │                                   │
   Work/Health)           ↓                                   │
                    UnitAnimationIntent                       │
                    { IsDead, WantsMove,                       │
                      WantsAttack, WantsWork,                  │  帧级,
                      FlipX }                                  │  per-entity
                              │                                │
                              ↓                                │
                    ┌─────────────────────┐                   │
                    │  InterceptorChain    │ ← 静态, 全局共享  │
                    │  (顺序执行拦截器)     │                   │
                    │                      │                   │
                    │  1. DeathInterceptor │                   │
                    │  2. StunInterceptor  │                   │
                    │  3. AttackInterceptor│                   │
                    │  4. WorkInterceptor  │                   │
                    │  5. MoveInterceptor  │                   │
                    │  6. IdleInterceptor  │ (fallback)        │
                    │                      │                   │
                    │  首个返回 true 的    │                   │
                    │  拦截器决定 target   │                   │
                    │  StateId            │                   │
                    └─────────┬───────────┘                   │
                              │ targetStateId                  │
                              ↓                                │
                    ┌─────────────────────┐                   │
                    │  StateTransition     │ ← 静态方法         │
                    │                      │                   │
                    │  if newState ≠ old: │                   │
                    │    - 重置 frame=0   │                   │
                    │    - 重置 tick 余数  │                   │
                    │    - (未来) crossfade│                   │
                    │  else:               │                   │
                    │    - 推进 frame      │                   │
                    │    - 计算 sub-frame  │                   │
                    └─────────┬───────────┘                   │
                              │ clip + frameFloat              │
                              ↓                                │
                    ┌─────────────────────┐                   │
                    │  FrameInterpolator   │ (补间器, 后续实现)│
                    │  → bufferOffset×2   │                   │
                    │  → blendWeight      │                   │
                    └─────────────────────┘                   │
```

---

## 3. 数据结构设计

### 3.1 Interceptor (拦截器)

```csharp
// 静态委托，不是 class 实例。所有单位共享同一组拦截器。
// 签名: 输入 intent + 当前 playbackState → 输出 (是否拦截, 目标 stateId)

public delegate bool InterceptorFunc(
    in UnitAnimationIntent intent,
    in UnitAnimationPlaybackState current,
    out UnitAnimationStateId targetState
);
```

### 3.2 InterceptorChain

```csharp
// 全局静态数组，初始化一次
public static class VAInterceptorChain
{
    // 按优先级从高到低排列
    private static readonly InterceptorFunc[] _chain = {
        DeathInterceptor.TryIntercept,
        StunInterceptor.TryIntercept,
        AttackInterceptor.TryIntercept,
        WorkInterceptor.TryIntercept,
        MoveInterceptor.TryIntercept,
        IdleInterceptor.TryIntercept   // fallback, 永远返回 true
    };

    public static UnitAnimationStateId Resolve(
        in UnitAnimationIntent intent,
        in UnitAnimationPlaybackState current)
    {
        for (int i = 0; i < _chain.Length; i++)
        {
            if (_chain[i](intent, current, out UnitAnimationStateId target))
                return target;
        }
        return UnitAnimationStateId.Idle; // 保底
    }
}
```

### 3.3 各拦截器

```csharp
public static class DeathInterceptor
{
    public static bool TryIntercept(
        in UnitAnimationIntent intent,
        in UnitAnimationPlaybackState current,
        out UnitAnimationStateId target)
    {
        if (intent.IsDead)
        {
            target = UnitAnimationStateId.Death;
            return true;
        }
        target = default;
        return false;
    }
}

public static class MoveInterceptor
{
    public static bool TryIntercept(
        in UnitAnimationIntent intent,
        in UnitAnimationPlaybackState current,
        out UnitAnimationStateId target)
    {
        if (intent.WantsMove)
        {
            target = UnitAnimationStateId.Move;
            return true;
        }
        target = default;
        return false;
    }
}

// IdleInterceptor 是 fallback, 永远拦截
public static class IdleInterceptor
{
    public static bool TryIntercept(
        in UnitAnimationIntent intent,
        in UnitAnimationPlaybackState current,
        out UnitAnimationStateId target)
    {
        target = UnitAnimationStateId.Idle;
        return true;
    }
}
```

### 3.4 状态转移

```csharp
public static class VAStateTransition
{
    public static void Apply(
        UnitAnimationStateId targetState,
        in UnitAnimationIntent intent,
        ref UnitAnimationPlaybackState playback,
        long currentTick)
    {
        if (playback.CurrentState != targetState || playback.LastTick == 0)
        {
            // 状态切换：重置帧
            playback.Reset(targetState, currentTick, intent.FlipX);
        }
        else
        {
            // 同状态持续：推进帧
            playback.FlipX = intent.FlipX;
            VAAdvanceFrames(targetState, ref playback, currentTick);
        }
    }

    private static void VAAdvanceFrames(
        UnitAnimationStateId state,
        ref UnitAnimationPlaybackState playback,
        long currentTick)
    {
        // 需要 UnitVASO 来查 clip 的 TicksPerFrame / Loop / FrameCount
        // 所以实际签名要传入 UnitVASO
        // ...
    }
}
```

---

## 4. 整合到现有 ECS 管线

### 4.1 当前调用链

```
EntitySystem.UpdateSystem()
  → DrawSystem.UpdateDraws()
    → UpdateWithInstancing()
      → for each entity:
          TryEnqueueVA()
            → UnitAnimationIntentBridge.Build()      // 读 ECS
            → UnitAnimationPlayback.EvaluateVA()      // 仲裁+推进 (糊在一起)
            → TryGetGlobalFrameIndex()               // 查 buffer
            → Enqueue()
```

### 4.2 改造后的调用链

```
EntitySystem.UpdateSystem()
  → DrawSystem.UpdateDraws()
    → UpdateWithInstancing()
      → for each entity:
          TryEnqueueVA()
            → UnitAnimationIntentBridge.Build()      // 读 ECS, 不变
            → VAInterceptorChain.Resolve(intent, playback)  // NEW: 拦截器链
            → VAStateTransition.Apply(target, intent, playback, vaso, tick)  // NEW: 状态转移
            → TryGetGlobalFrameIndex()               // 查 buffer, 不变
            → Enqueue()                              // 不变
```

### 4.3 性能考量

- 拦截器链是 `InterceptorFunc[]` 静态数组, 无虚调用, 无 GC
- 每个拦截器是纯静态方法, `in` 参数传引用不拷贝结构体
- 最坏情况：6 个拦截器 × 1024 单位 = 6144 次函数调用/帧, 可忽略
- 比当前 `ResolveVAState` 的 if-else 链多了一层数组遍历, 但换来可扩展性

---

## 5. 拦截器的扩展点

每个拦截器未来可以扩展为包含：

```csharp
public static class AttackInterceptor
{
    public static bool TryIntercept(
        in UnitAnimationIntent intent,
        in UnitAnimationPlaybackState current,
        out UnitAnimationStateId target)
    {
        if (!intent.WantsAttack)
        {
            target = default;
            return false;
        }

        target = UnitAnimationStateId.Attack;
        return true;
    }

    // 未来扩展:
    // - 检查当前状态是否允许打断 (LockUntilComplete)
    // - 根据武器类型选择不同 Attack 变体 (Attack_Melee / Attack_Ranged)
    // - 根据攻击阶段选择子状态 (Attack_Windup / Attack_Swing / Attack_Recovery)
    // - 消费意图标志 (一次性攻击指令, 消费后清除)
}
```

---

## 6. 与 BBBNexus 的映射

| BBBNexus | MineRTS ECS |
|------|------|
| `BBBCharacterController` | `DrawSystem.TryEnqueueVA` (per-entity) |
| `InputPipeline` | `UnitAnimationIntentBridge.Build` |
| `MainProcessorPipeline.IntentProcessors` | 拦截器链 (Intent → StateId) |
| `StateInterceptorSO` | `InterceptorFunc` 委托 |
| `GlobalInterruptProcessor` | `VAInterceptorChain.Resolve` |
| `PlayerBaseState.CheckInterrupts()` | 拦截器链顺序执行 |
| `PlayerBaseState.UpdateStateLogic()` | 帧推进 + 状态内逻辑 |
| `MotionDriver` | MoveSystem (游戏逻辑位移) |
| `IAnimationFacade` | UnitVARenderService (渲染提交) |
| `PlayerRuntimeData` | ECS 组件数组 (已是数据驱动) |
| `PlayerStateRegistry` | InterceptorChain 静态数组 (不需要 factory, 状态已由 clip 定义) |

---

## 7. 建议实施顺序

1. **定义 `InterceptorFunc` 委托 + `VAInterceptorChain`** — 静态基础设施
2. **实现 6 个拦截器** — Death, Stun, Attack, Work, Move, Idle
3. **重构 `EvaluateVA`** — 拆成 `InterceptorChain.Resolve` + `StateTransition.Apply`
4. **验证行为等价** — 确保和现在的硬编码 if-else 结果一致
5. **添加 Stun 拦截器** — 验证可扩展性（之前 Stun 枚举存在但没接入）

这一步不改补间器, 不碰 Shader, 纯粹是逻辑层的重构。改完之后, 再加入子状态 (Attack 前摇/挥砍/后摇)、LockUntilComplete 等高级特性就都有地方放了。
