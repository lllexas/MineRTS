# BBBNexus 3C 架构调研报告

**日期**: 2026-05-06  
**来源**: `G:\ProjectOfGame\Mi-Demo-Spring\Assets\BBBNexus`  
**目的**: 学习其动画状态机、拦截器链、仲裁器架构，指导 MineRTS VA 动画系统设计

---

## 项目概述

BBBNexus 是一个 Unity 3D 角色控制器框架，采用 **"意图驱动的管线化架构"**。核心原则是**逻辑决策**与**表现执行**严格分离。

## 目录结构

```
BBBNexus/
├── BBBCharacterController.cs       ← 根 MonoBehaviour，所有子系统的单一入口
├── Character/
│   ├── Arbitration/                 ← 仲裁器系统 (控制域冲突解决)
│   │   ├── ArbiterPipeline.cs
│   │   └── Arbiters/
│   │       ├── ActionArbiter.cs     ← 高优先级动作 (攻击/技能)
│   │       ├── CharacterArbiter.cs  ← 汇总仲裁 (Locomotion/Action/Status)
│   │       ├── HealthArbiter.cs
│   │       ├── StaminaArbiter.cs
│   │       ├── StatusEffectArbiter.cs
│   │       └── LODArbiter.cs
│   ├── ConfigData/                  ← ScriptableObject 配置模块
│   ├── Core/
│   │   ├── Animation/               ← 动画外观 (Facade)
│   │   │   ├── IAnimationFacade.cs
│   │   │   ├── AnimancerFacade.cs
│   │   │   └── AnimPlayOptions.cs
│   │   ├── Driver/MotionDriver.cs   ← 位移执行器
│   │   └── States/
│   │       ├── PlayerStateRegistry.cs
│   │       └── UpperBodyStateRegistry.cs
│   ├── Input/Base/IInputSource.cs   ← 输入源接口
│   ├── ProcessingPipelines/
│   │   ├── InputPipeline.cs         ← 输入采样+缓冲
│   │   └── MainProcessorPipeline.cs ← 意图处理器 + 参数处理器
│   ├── RunTimeData/
│   │   ├── PlayerRuntimeData.cs     ← 中央黑板
│   │   └── RuntimeDataDefinitions.cs
│   └── States/
│       ├── PlayerBrainSO.cs         ← 状态名册 + 拦截器配置
│       ├── Core/
│       │   ├── GlobalInterruptProcessor.cs
│       │   ├── Base/StateInterceptorSO.cs
│       │   └── Interceptors/        ← Death, Jump, Dodge, Roll, Vault...
│       ├── FullBody/                ← 全身状态 (Idle, Move, Jump, Fall, Roll...)
│       ├── UpperBody/               ← 上半身状态 (EmptyHands, HoldItem...)
│       └── Override/                ← 覆盖状态 (Death, StatusEffect)
├── Core/
│   └── StateMachine/
│       ├── StateMachine.cs          ← 通用状态机引擎
│       └── BaseState.cs             ← 抽象状态基类 (Enter/LogicUpdate/PhysicsUpdate/Exit)
└── Services/                        ← Hub, Equipment, Inventory, State 服务
```

## 核心架构模式

### 1. 分层状态机 (Layered State Machines)

三个独立的状态机实例并行运行：

| 层 | 管理类 | 职责 |
|------|------|------|
| 全身 (FullBody) | `BBBCharacterController.StateMachine` | 移动、跳跃、翻滚、死亡 |
| 上半身 (UpperBody) | `UpperBodyController.StateMachine` | 持枪、射击、空手 |
| 覆盖层 (Override) | `ActionArbiter` | 高优先级动作 (攻击、受击) |

每个状态机持有独立的 `StateMachine` 实例（`Core/StateMachine/StateMachine.cs`），提供 `ChangeState(newState)` 方法，自动调用 `CurrentState.Exit()` → `newState.Enter()`。

### 2. 拦截器模式 (Interceptor Pattern) ★ 已搬入 MineRTS

这是最核心的创新。每个状态在 `LogicUpdate` 中的**第一步**不是执行自身逻辑，而是先跑全局拦截器链：

```csharp
// PlayerBaseState.LogicUpdate() — sealed override, 不可被子类覆盖
sealed override LogicUpdate():
    CheckInterrupts()    // ← 拦截器优先
    UpdateStateLogic()   // ← 状态自身逻辑
```

`GlobalInterruptProcessor` 按顺序遍历 `List<StateInterceptorSO>`，第一个返回 `true` 的拦截器导致状态切换，后续拦截器被跳过。

全身拦截器优先级：`Jump > Fall > Land > Dodge > Roll > Vault > TacticalMotion`

每个拦截器是一个 `ScriptableObject`，含单一方法：
```csharp
public abstract bool TryIntercept(
    BBBCharacterController player,
    PlayerBaseState currentState,
    out PlayerBaseState nextState
);
```

### 3. 仲裁器管道 (Arbiter Pipeline)

`ArbiterPipeline.ProcessUpdateArbiters()` 在 `Update` 最开头执行，按固定顺序运行仲裁器：

1. `ActionArbiter` — 处理高优先级动作请求（攻击等），通过 `ActionRequest` 结构体写入黑板，保存优先级和抗打断等级（Roll=100, Dodge=80）
2. `HealthArbiter` — 处理伤害请求
3. `StaminaArbiter` — 处理体力管理与恢复
4. `StatusEffectArbiter` — 处理状态效果（眩晕等）
5. `CharacterArbiter` — **汇总仲裁器**：将 Locomotion/Action/Status 三个域合并为 `CharacterControlContext`

控制域层次：`Death > Status(Hard) > Status(Soft) > Action > Locomotion`

`CharacterArbiter.IsLocomotionBlocked()` 被 Update 查询，若阻止则跳过全身状态机更新。

### 4. 动作仲裁 (Action Arbitration)

`ActionArbiter` 读取黑板帧级的 `ActionArbitrationContext`（自动保留最高优先级请求），决定是否应用：

- 若当前无活跃动作控制，且请求优先级 > 当前抗打断等级：应用请求，切换到 `OverrideState`
- 若当前已有动作控制，且新请求优先级 >= 当前：替换
- `OnClipEnd` 触发后清理，返回之前的状态

```csharp
struct ActionRequest {
    AnimationClip Clip;
    float FadeDuration;
    int Priority;        // 优先级
    int InterruptLevel;  // 抗打断等级
    float Speed;
    bool ApplyGravity;
}
```

### 5. 意图 → 状态 分离

`MainProcessorPipeline` 分两阶段运行：

**意图处理器**（状态逻辑之前）：
- `ViewRotationProcessor`
- `LocomotionIntentProcessor` — WASD→8方向量化、疾跑/行走、闪避/翻滚（短按Shift=闪避，长按=冲刺）
- `JumpOrVaultIntentProcessor`
- `ActionIntentProcessor`
- `ExtraActionIntentProcessor`
- `EojIntentProcessor` (装备切换)
- `HotbarIntentProcessor`

**参数处理器**（状态逻辑之后）：
- `MovementParameterProcessor` — 计算混合树参数 `AnimBlendX/Y`、下落高度
- `AimPointParameterProcessor`

### 6. InputPipeline — 帧级意图缓冲

```csharp
// 1. 从 IInputSource 读取原始输入
// 2. 应用防抖 (flicker buffer)
// 3. 将 just-pressed 事件扩展为时控缓冲 (actionBufferTime)
// 4. 仅通过 Consume*() 方法提供受控写入 → 单次消费语义
```

### 7. PlayerRuntimeData — 中央黑板

所有子系统共享的数据结构，按类别组织：

- **帧级意图标志**: `WantsToJump`, `WantsToDodge`, `WantsToRoll`, `WantsToVault`, `WantsToPrimaryAction` 等
- **运动状态**: `IsGrounded`, `VerticalVelocity`, `LocomotionState`, `CurrentSpeed`
- **动画混合参数**: `CurrentAnimBlendX/Y`, `CurrentRunCycleTime`
- **仲裁上下文**: `ActionArbitrationContext`, `StatusEffectContext`, `CharacterControlContext`
- **IK 目标**: `LeftHandGoal`, `RightHandGoal`

每帧 `LateUpdate` 末尾调用 `ResetIntent()` 清除所有帧级标志。

### 8. 动画外观 (Animation Facade)

`IAnimationFacade` 完全解耦玩法逻辑和底层动画引擎：

```csharp
interface IAnimationFacade {
    void PlayClip(AnimationClip, AnimPlayOptions);
    void PlayTransition(object transitionObj, AnimPlayOptions);
    void SetMixerParameter(Vector2, int layerIndex);
    void SetOnEndCallback(Action, int layerIndex);
    void SetLayerWeight(int, float, float);
    void PlayFullBodyAction(...);
    void StopFullBodyAction();
    void EnterHitStop(float);
}
```

当前实现 `AnimancerFacade` 使用 Animancer Pro。通过 `object` 参数解耦 Animancer 的 `ClipTransition` 类型。

`AnimPlayOptions` 结构体：
```csharp
struct AnimPlayOptions {
    int Layer;
    float FadeDuration;
    float Speed;
    float NormalizedTime;
}
```

### 9. 运动驱动 (MotionDriver)

两种模式：

1. **输入驱动移动** — 自由视角：角色朝向平滑旋转至移动方向；瞄准模式：朝向跟随相机
2. **动画曲线驱动移动** — 起步/停止/翻滚等，读取动画曲线中的速度/旋转数据

特性：
- 角色间阻挡检测 (`Physics.OverlapSphereNonAlloc`)
- 根运动消费 (`OnAnimatorMove` → `deltaPosition` → 阻挡过滤 → `CharacterController.Move`)
- Warp 空间变形 (翻越等, 动画根运动对齐到目标世界坐标)
- 打击暂停支持

### 10. 对象池 + 回调池

`BBBCharacterController` 实现 `IPoolable`，通过 `OnSpawned`/`OnDespawned` 管理生命周期。  
`AnimancerFacade` 池化 `CallbackWrapper` 实例避免动画结束回调的 GC 分配。

## BBBCharacterController Update 完整顺序

```
Update:
  1. ArbiterPipeline.ProcessUpdateArbiters()       ← 控制域仲裁
  2. InputPipeline.Update()                         ← 采样输入
  3. MainProcessorPipeline.UpdateIntentProcessors()  ← 意图处理
  4. InteractionSensor.Tick()
  5. InventoryController.Update()
  6. MainProcessorPipeline.UpdateParameterProcessors() ← 参数计算
  7. StateMachine.CurrentState.LogicUpdate()        ← 状态逻辑 (含拦截器)
  8. UpperBodyCtrl.Update()
  9. FacialController.Update()
  10. ActionController.Update()
  11. AudioController.Update()

LateUpdate:
  1. ConsumeFullBodyRootMotionIfNeeded()
  2. MotionDriver.UpdateMotion()                    ← 位移
  3. StateMachine.CurrentState.PhysicsUpdate()
  4. IkController.Update()
  5. ArbiterPipeline.ProcessLateUpdateArbiters()
  6. RuntimeData.ResetIntent()                      ← 清理帧级意图
```

## 对 MineRTS 的启发

| BBBNexus 模式 | MineRTS 对应实现 | 状态 |
|------|------|------|
| 拦截器链 (Interceptor Chain) | `VAInterceptorChain` | ✅ 已实现 |
| 帧级意图 + ResetIntent | `UnitAnimationIntent` (需加消费语义) | 🔶 意图有, 消费无 |
| 状态注册表 (State Registry) | 拦截器链静态数组 (ECS 无需 factory) | ✅ 已实现 |
| 仲裁器管道 (Arbiter Pipeline) | 暂不需要 — RTS 无多控制域冲突 | ⏸ 未来 |
| 动画外观 (Animation Facade) | `UnitVARenderService` (渲染提交) | ✅ 已有 |
| 运动驱动 (MotionDriver) | `MoveSystem` (游戏逻辑位移) | ✅ 已有 |
| 参数处理器 (Parameter Processors) | 补间器 (后续实现) | ⏸ 待做 |
| 中央黑板 (PlayerRuntimeData) | ECS 组件数组 (天然数据驱动) | ✅ 已有 |
