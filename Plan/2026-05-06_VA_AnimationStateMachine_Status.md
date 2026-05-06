# VA 动画状态机 & 仲裁运行现状

**日期**: 2026-05-06  
**状态**: MVP 占位，未细化

---

## 当前链路

```
ECS 组件 (每帧)
  ↓ UnitAnimationIntentBridge.Build(whole, entityIndex)
  ↓ 读取: CoreComponent, MoveComponent, WorkComponent, AttackComponent, HealthComponent
  ↓
UnitAnimationIntent
  ├─ IsDead      = !health.IsAlive
  ├─ WantsMove   = move.LogicalPosition != move.PreviousLogicalPosition || move.Timer > 0
  ├─ WantsWork   = work.WorkType != WorkType.None
  ├─ WantsAttack = attack.TargetEntityId != 0 && attack.TargetEntityId != -1
  └─ FlipX       = core.Rotation.x < 0
  ↓
UnitAnimationPlayback.EvaluateVA(vaso, intent, ref playback, currentTick)
  ↓
  ResolveVAState (内联仲裁，不在 Arbiter 类中)
    Death > Attack > Work > Move > Idle
  ↓
  AdvanceVAFrames (基于 tick 计数离散跳帧, 无 lerp)
  ↓
UnitAnimationFrameVAResult { State, LocalFrame, FlipX }
  ↓ TryGetGlobalFrameIndex(vaso, state, localFrame)
VASO.ClipMap[state] → globalFrameIndex → StructuredBuffer offset
```

---

## 已实现 vs 缺失

### 意图桥接 (UnitAnimationIntentBridge)

| 功能 | 状态 | 备注 |
|------|------|------|
| 移动意图 | 占位 | `LogicalPosition != PreviousLogicalPosition` — 仅检测"是否在跨格子"，不区分速度/加速度 |
| 工作意图 | 占位 | `WorkType != None` — 只有布尔判定，无法区分不同工作类型播不同动画 |
| 攻击意图 | 占位 | 只检查 target 是否存在，不考虑攻击冷却、弹道飞行中等子阶段 |
| 死亡意图 | 有 | `!health.IsAlive` — 基本可用 |
| 眩晕意图 | 无 | `Stun` 状态枚举存在但 IntentBridge 完全未处理 |
| FlipX | 有 | `core.Rotation.x < 0` — 基本可用 |
| 移动速度/跑 vs 走 | 无 | 无任何区分 |

### 状态仲裁 (ResolveVAState)

| 功能 | 状态 | 备注 |
|------|------|------|
| 优先级排序 | 有 | Death > Attack > Work > Move > Idle |
| LockUntilComplete | 无 | UnitVAClip 刻意移除了此字段，但仲裁器需要从上层行为系统处理 |
| 状态过渡缓冲 | 无 | 频繁切换时无去抖，可能每帧在 Move/Idle 之间来回跳（tick 边界处尤其明显） |
| 死亡锁帧 | 无 | Death clip 播完后会循环或回到 Idle，没有"停在最后一帧" |
| 眩晕 | 无 | 枚举存在但未接入 |
| 攻击锁帧 | 无 | 攻击动作可能只播 1 tick 就被 Move 打断 |

### 帧推进 (AdvanceVAFrames)

| 功能 | 状态 | 备注 |
|------|------|------|
| Tick → 帧映射 | 有 | `accumulatedTicks / TicksPerFrame` — 纯整数整除 |
| Loop | 有 | `clip.Loop` 控制 |
| Non-loop 终点停留 | 有 | 到达最后一帧后停止推进 |
| 帧间补间 (lerp) | 无 | 完全缺失，导致离散跳帧观感 |
| 帧率适配 | 无 | BakeSampleFps (60) vs TicksPerSecond (10) 的比值未纳入计算 |
| SubTick fractional frame | 无 | SubTickOffset 存在但 VA 播放器完全未使用 |

### Clip 解析 (TryGetGlobalFrameIndex)

| 功能 | 状态 | 备注 |
|------|------|------|
| State → Clip 查表 | 有 | `Dictionary<UnitAnimationStateId, ClipGPUInfo>` |
| 查表失败处理 | 有 | fallback `globalFrameIndex = 0` — 可能导致帧跳闪 |
| 多 clip 同 State | 缺陷 | 后注册的 clip 覆盖前者，无法支持同一 State 下多个变体 |

---

## 与 Atlas 动画系统的差异

| | Atlas 路径 | VA 路径 |
|------|-----------|---------|
| 仲裁器 | `UnitAnimationArbiter.Resolve` (独立类) | `ResolveVAState` (内联在 Playback 中) |
| LockUntilComplete | 支持 (AtlasClipDef.LockUntilComplete) | 不支持 (UnitVAClip 已移除) |
| 帧结果类型 | `AtlasFrameCoord` (纹理坐标) | `int globalFrameIndex` (buffer 偏移) |
| 意图源 | 相同 `UnitAnimationIntentBridge` | 相同 |

---

## 近期待解决

1. **帧率适配**：`BakeSampleFps`(60) / `TicksPerSecond`(10) = 6 倍速比未反映在帧推进中
2. **SubTick 补间**：利用 `SubTickOffset` 在 shader 端做相邻帧 lerp
3. **仲裁器统一**：Atlas 和 VA 共用同一套仲裁逻辑（而非现在各自内联）
4. **去抖**：Move/Idle 边界处避免每帧来回切换
5. **死亡锁帧**：Non-loop death clip 播完后停在最后一帧
6. **Attack/Work 细化**：区分子阶段，至少支持 LockUntilComplete 行为
