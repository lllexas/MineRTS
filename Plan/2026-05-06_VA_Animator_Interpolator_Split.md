# VA 动画补间器 & 状态机权责划分

**日期**: 2026-05-06

---

## 核心边界

```
                    ┌─────────────────────┐
  ECS 组件 ────────→│   动画状态机         │
  Move/Attack/      │   (Animator)        │
  Work/Health       │   决定 播什么         │
                    │   输出 StateId       │
                    └─────────┬───────────┘
                              │ stateId
                              ▼
                    ┌─────────────────────┐
  GlobalTick ──────→│   帧计数器           │
                    │   (Frame Counter)   │
                    │   决定 播到第几帧     │
                    │   输出 (clip, frameFloat)
                    └─────────┬───────────┘
                              │ clip + fractional frame
                              ▼
                    ┌─────────────────────┐
                    │   动画补间器         │
                    │   (Interpolator)    │
                    │   决定 顶点在哪       │
                    │   输出 bufferOffset + lerp
                    └─────────┬───────────┘
                              │ GPU 可用的数据
                              ▼
                         StructuredBuffer
```

---

## 权责明细

### 动画状态机 (纯逻辑, 与渲染无关)

| 职责 | 说明 | 现状 |
|------|------|------|
| 意图汇聚 | ECS 组件 → UnitAnimationIntent | 已有, 基本可用 |
| 状态仲裁 | Death > Attack > Work > Move > Idle | 已有, 待细化 |
| 子状态管理 | Attack: 前摇/挥砍/后摇; Work: 开采/运输 | 无 |
| LockUntilComplete | 不可打断动作的锁定 | 无 |
| 死亡锁帧 | 死亡动画播完定格 | 无 |
| 过渡/CrossFade | 状态切换时平滑混合 | 无 |
| 速度倍率 | 角色当前速度影响 walk 播放速率 | 无 |

**输出**: `UnitAnimationStateId` (单纯的状态枚举, 不包含帧信息)

### 帧计数器 (纯逻辑, tick-driven)

| 职责 | 说明 | 现状 |
|------|------|------|
| Tick → 帧映射 | `BakeSampleFps / TicksPerSecond` 倍率 | 缺失, 当前硬编码 1:1 |
| 帧推进 | 基于 deltaTick 累加, 支持 loop/non-loop | 已有 |
| Sub-frame 小数帧 | 用于补间的那一帧之间的进度 (0~1) | 缺失 |
| 帧率适配 | 外部传入 speedMultiplier 控制播放速率 | 缺失 |

**输入**: `stateId` + `GlobalTick` + `speedMultiplier`  
**输出**: `(clipIndex, frameFloat)` — 整数部分是帧号, 小数部分是 sub-frame fraction

### 动画补间器 (与 GPU 紧密耦合)

| 职责 | 说明 | 现状 |
|------|------|------|
| ClipIndex → BufferOffset | 通过 VABufferManager.ClipMap 查表 | 已有 |
| 相邻帧 lerp | floor(frame) 和 ceil(frame) 两帧顶点做 lerp | 缺失 |
| GPU buffer 偏移 | 解算为 StructuredBuffer 索引 | 已有 (globalFrameIndex) |
| 将补间参数传给 Shader | 两个 globalFrameIndex + blendWeight 作为 per-instance 参数 | 缺失 |

**输入**: `(clipIndex, frameFloat)` + `VABufferManager`  
**输出**: `(frameOffsetA, frameOffsetB, blendWeight)` — 发给 Shader 的三个 per-instance 值

---

## Shader 端的补间

当前 Shader 每实例只有一个 `_VA_FrameOffset` (int frame)。补间化后：

```hlsl
// 每实例三个 prop:
UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(float, _VA_FrameOffsetA)   // floor frame
    UNITY_DEFINE_INSTANCED_PROP(float, _VA_FrameOffsetB)   // ceil frame
    UNITY_DEFINE_INSTANCED_PROP(float, _VA_BlendWeight)    // 0~1
UNITY_INSTANCING_BUFFER_END(Props)

// Vertex shader:
uint indexA = frameOffsetA * vertexCount + vertexID;
uint indexB = frameOffsetB * vertexCount + vertexID;
float2 posA = _VAPositions[indexA];
float2 posB = _VAPositions[indexB];
float2 pos = lerp(posA, posB, blendWeight);
```

---

## 当前代码的混乱点 (为什么需要分)

`UnitAnimationPlayback.EvaluateVA` 做了三件事：
1. 仲裁状态 (属于状态机)
2. 推进帧 (属于帧计数器)
3. 返回 localFrame (属于帧计数器, 但 caller 又拿去查 buffer → 补间器)

`DrawSystem.TryEnqueueVA` 也做了三件事：
1. 调 EvaluateVA 获取意图+帧
2. 调 TryGetGlobalFrameIndex 查 buffer 偏移 (属于补间器)
3. 构建 draw request

**全都糊在一起。** 分开之后每层只做一件事，可以独立测试、独立优化。

---

## 建议实施顺序

1. **先拆帧计数器** — 把 `AdvanceVAFrames` 从 Playback 里抽出来, 加上 sub-frame 支持
2. **再拆补间器** — 帧计数器输出 frameFloat → 补间器解算双 frame offset + blend → 传给 Shader
3. **最后细化状态机** — LockUntilComplete、过渡、子状态等

第一版补间器可以直接在 CPU 侧做 lerp (更新 mesh vertices), 验证逻辑正确后再搬到 GPU Shader 端。GPU 端补间需要 Shader 多读一次 StructuredBuffer (但 latency 基本可忽略)。
