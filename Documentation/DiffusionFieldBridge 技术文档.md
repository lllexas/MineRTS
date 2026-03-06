# DiffusionFieldBridge 技术文档

## 📋 目录

1. [系统概述](#系统概述)
2. [架构设计](#架构设计)
3. [核心组件详解](#核心组件详解)
4. [扩散场算法原理](#扩散场算法原理)
5. [SDF 空间符号距离场](#sdf 空间符号距离场)
6. [时间平滑系统](#时间平滑系统)
7. [渲染管线流程](#渲染管线流程)
8. [参数调优指南](#参数调优指南)
9. [性能优化](#性能优化)
10. [故障排查](#故障排查)

---

## 系统概述

### 功能定位

`DiffusionFieldBridge` 是一个用于 Unity 大地图系统的**动态扩散场效果桥接器**，主要功能包括：

- **视觉效果**：在相机视口背景上渲染柔和的密度扩散场，形成类似"能量场"的视觉效果
- **双模型并行**：支持扩散模型和 SDF（空间符号距离场）模型的切换/叠加
- **GPU 加速**：使用 ComputeShader 进行并行计算，实现实时动态效果
- **世界空间对齐**：支持世界空间或屏幕空间两种渲染模式

### 应用场景

```
┌─────────────────────────────────────────────────────────┐
│                    大地图背景效果                         │
│  ┌─────┐    ╭─────────────╮    ┌─────┐                  │
│  │起点 │ ──→│  扩散场背景  │───→│ Boss│                  │
│  └─────┘    ╰─────────────╯    └─────┘                  │
│                     ↓                                    │
│              柔和的能量梯度场                              │
└─────────────────────────────────────────────────────────┘
```

---

## 架构设计

### 继承关系

```
MonoBehaviour
    ↓
SingletonMono<ViewportBackgroundQuad>
    ↓
ViewportShaderBridge (抽象基类)
    ↓
DiffusionFieldBridge (具体实现)
```

### 桥接模式架构

```
┌──────────────────────────────────────────────────────────┐
│              ViewportBackgroundQuad (几何控制器)           │
│  职责：相机视口追踪、Quad 几何变换、桥接器管理               │
└────────────────────┬─────────────────────────────────────┘
                     │ 通知材质更新
                     ↓
┌──────────────────────────────────────────────────────────┐
│            DiffusionFieldBridge (Shader 桥接器)          │
│  职责：ComputeShader 管理、Ping-Pong 缓冲、参数同步        │
└────────────────────┬─────────────────────────────────────┘
                     │ 设置 ComputeShader 参数
                     ↓
┌──────────────────────────────────────────────────────────┐
│            DiffusionField.compute (计算着色器)            │
│  职责：GPU 并行计算扩散场、SDF 场、时间平滑                 │
└────────────────────┬─────────────────────────────────────┘
                     │ 输出 RenderTexture
                     ↓
┌──────────────────────────────────────────────────────────┐
│          DiffusionDisplay.shader (渲染着色器)            │
│  职责：三角形密铺渲染、几何着色、颜色混合                  │
└──────────────────────────────────────────────────────────┘
```

### 数据流

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│ BigMapData  │ ──→ │ GPU Buffer  │ ──→ │ ComputeShader│
│ (节点/边数据)│     │ (共享缓冲区) │     │  (扩散计算)  │
└─────────────┘     └─────────────┘     └─────────────┘
                                              │
                                              ↓
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│ 最终显示效果 │ ←── │ 材质渲染    │ ←── │ RenderTexture│
└─────────────┘     └─────────────┘     └─────────────┘
```

---

## 核心组件详解

### 1. DiffusionFieldBridge.cs

**核心职责**：
- 管理 ComputeShader 的生命周期和参数同步
- 管理 Ping-Pong 缓冲区（时间演化）
- 管理 SDF 缓冲区（空间距离场）
- 每帧更新 Shader 属性

**关键成员**：

```csharp
// Ping-Pong 缓冲区（时间演化）
private RenderTexture _bufferA;
private RenderTexture _bufferB;
private bool _useBufferAAsInput = true;

// SDF 缓冲区
private RenderTexture _sdfBuffer;        // 当前帧理论值
private RenderTexture _sdfSmoothBuffer;  // 平滑后持久化值

// 核心参数
[SerializeField] private float _gradientStep = 0.002f;    // 梯度下降步长
[SerializeField] private float _injectStrength = 1.0f;    // 注入强度
[SerializeField] private float _injectRadius = 0.05f;     // 注入半径
```

**执行流程**：

```
每帧 LateUpdate
    ↓
1. 检测相机参数变化 (orthoSize, aspect)
    ↓
2. 计算各向异性校正参数
    ↓
3. Dispatch ComputeShader Kernels:
   - CSDiffuseInject (扩散 + 注入)
   - CSSpatialSDF (空间 SDF 计算)
   - CSInertialSDFDamping (惯性阻尼平滑)
    ↓
4. 交换 Ping-Pong 缓冲区
    ↓
5. 设置材质纹理属性
    ↓
6. 更新完成
```

### 2. BigMapGPUBufferManager.cs

**核心职责**：单例管理器，将大地图节点/边数据转换为 GPU 可访问的 ComputeBuffer

**数据结构**：

```csharp
// 节点 GPU 数据 (16 字节对齐)
struct NodeGPUData
{
    public Vector4 PositionAndRadius;  // x,y:位置，z:半径，w:保留
    public Vector4 ColorAndFlags;      // x,y,z,w:颜色 (RGBA)
    public Vector4 Attributes;         // x:标志位，yzw:保留
}

// 边 GPU 数据
struct EdgeGPUData
{
    public Vector4 FromPosAndThickness;
    public Vector4 ToPosAndFlags;
    public Vector4 ColorAndAttributes;
}
```

**共享机制**：
- `DiffusionFieldBridge` 通过 `BigMapGPUBufferManager.Instance.GetNodeBuffer()` 获取节点数据
- 避免数据冗余，保证单一数据源

### 3. ViewportBackgroundQuad.cs

**核心职责**：
- 视口背景 Quad 控制器
- 管理所有 Shader 桥接器的生命周期
- 材质切换和模式管理
- GPU 缓冲区同步

**模式系统**：

```csharp
public enum ViewportMode
{
    None,           // 无模式 - 隐藏 Quad
    DefaultGrid,    // 基础网格渲染
    BigMap,         // 大地图扩散场模式
    MainMenu        // 主菜单模式
}
```

---

## 扩散场算法原理

### 核心思想

**线性势能场模型**：每个像素的值代表"距离最近喷泉（节点）的高度"

```
节点 (喷泉)
   │
   ↓ 注入能量 (InjectStrength)
   ↓
能量向四周扩散 (GradientStep)
   │
   ↓ 随距离线性衰减
   ↓
形成柔和的梯度场
```

### ComputeShader 核心算法

#### CSDiffuseInject Kernel

```hlsl
// 合并版本：扩散 + 注入 在一个 Kernel 中完成
[numthreads(8, 8, 1)]
void CSDiffuseInject(uint3 id : SV_DispatchThreadID)
{
    // 1. 注入逻辑：在节点位置维持密度
    float injectValue = 0;
    for (uint i = 0; i < _NodeCount; i++)
    {
        float dist = distance(worldPos, nodePos);
        if (dist < injectRadius)
        {
            float injectFactor = 1.0 - dist / injectRadius;
            injectValue = max(injectValue, injectFactor * _InjectStrength);
        }
    }

    // 2. 扩散逻辑：梯度下降
    // 读取 4-邻居的值
    float top    = _InputBuffer[id.xy + uint2(0, 1)].r;
    float bottom = _InputBuffer[id.xy - uint2(0, 1)].r;
    float left   = _InputBuffer[id.xy - uint2(1, 0)].r;
    float right  = _InputBuffer[id.xy + uint2(1, 0)].r;

    // 找出邻居中的最高点
    float maxNeighbor = max(top, max(bottom, max(left, right)));

    // 从最高邻居的高度，减去一个步长
    float baseStep = _GradientStep * _ZoomCorrection;
    float stepX = baseStep * _DiffusionAspectX * _DiffusionArtisticX;
    float stepY = baseStep * _DiffusionAspectY * _DiffusionArtisticY;

    float maxCorrected = max(
        top    - stepY,
        max(bottom - stepY, max(left - stepX, right - stepX))
    );
    
    float densityFromNeighbors = max(0, maxCorrected);

    // 3. 最终输出：取注入和扩散的最大值
    float newDensity = max(injectValue, densityFromNeighbors);
    _OutputBuffer[id.xy] = float4(newDensity, newDensity, newDensity, 1.0);
}
```

### 各向异性校正

**问题**：当相机 aspect ≠ 1 时，UV 空间拉伸导致扩散场变成扁平菱形

**解决方案**：

```
┌─────────────────────────────────────┐
│         校正前的扩散场               │
│           ╱╲                        │
│          ╱  ╲                       │
│         ╱    ╲  ← 扁平菱形          │
│        ╱      ╲                    │
│       ╱________╲                   │
└─────────────────────────────────────┘
              ↓ 应用各向异性校正
┌─────────────────────────────────────┐
│         校正后的扩散场               │
│           ╱╲                        │
│          ╱  ╲                       │
│         ╱    ╲  ← 正菱形            │
│        ╱      ╲                    │
│       ╱________╲                   │
└─────────────────────────────────────┘
```

**校正公式**：

```hlsl
// 自动校正：X 方向像素被拉长 aspect 倍
float autoAspectX = aspect;  // 相机宽高比
float autoAspectY = 1.0f;

// 艺术调整：可叠加呼吸灯效果
float finalArtX = _diffusionArtisticX + breathingX * _breathingAmplitudeX;
float finalArtY = _diffusionArtisticY + breathingY * _breathingAmplitudeY;

// 最终步长
float stepX = baseStep * autoAspectX * finalArtX;
float stepY = baseStep * autoAspectY * finalArtY;
```

### 缩放校正 (Zoom Correction)

**目的**：确保在不同相机缩放级别下，扩散场的世界空间尺度保持一致

```hlsl
// 计算缩放校正系数
float zoomCorrection = orthoSize / _referenceOrthoSize;

// 当 OrthoSize 变小（拉近）时，zoomCorrection 变小，扩散变慢
// 当 OrthoSize 变大（拉远）时，zoomCorrection 变大，扩散变快
float baseStep = _GradientStep * zoomCorrection;
```

---

## SDF 空间符号距离场

### 核心概念

SDF (Signed Distance Field) 是一种用距离表示形状的方法：

```
SDF(p) = 注入强度 - 距离 × 梯度

值 > 0: 在形状内部
值 = 0: 在形状边界
值 < 0: 在形状外部
```

### CSSpatialSDF Kernel

```hlsl
[numthreads(8, 8, 1)]
void CSSpatialSDF(uint3 id : SV_DispatchThreadID)
{
    float2 worldPos = ScreenToWorld(uv);
    
    float maxSDF = 0;
    for (uint i = 0; i < _NodeCount; i++)
    {
        float2 nodePos = _Nodes[i].PositionAndRadius.xy;
        float dist = distance(worldPos, nodePos);
        
        // 线性势能公式
        float val = max(0, _InjectStrength - dist * _SDFGradient);
        maxSDF = max(maxSDF, val);
    }
    
    _SDFBuffer[id.xy] = float4(maxSDF, maxSDF, maxSDF, 1.0);
}
```

### 三种模式对比

| 模式 | 公式 | 视觉效果 |
|------|------|----------|
| **扩散模型** | 迭代梯度下降 | 柔和、有拖影 |
| **SDF 模型** | 即时空间计算 | 硬朗、清晰边界 |
| **混合模式** | lerp(扩散，SDF, blend) | 可调节软硬程度 |

---

## 时间平滑系统

### 为什么需要时间平滑？

**问题**：
- 节点移动时，SDF 场瞬间跳变，产生视觉闪烁
- 相机移动时，扩散场高频抖动

**解决**：使用惯性阻尼平滑，让场的变化像"果冻"一样有粘滞感

### 平滑模式

```csharp
public enum SDFTemporalMode
{
    None,               // 无平滑，纯生硬 SDF
    SimpleSmooth,       // 对称平滑（单一速度）
    InertialDamping     // 非对称惯性阻尼（推荐）
}
```

### 惯性阻尼算法

**核心思想**：能量建立快，消散慢

```hlsl
[numthreads(8, 8, 1)]
void CSInertialSDFDamping(uint3 id : SV_DispatchThreadID)
{
    float targetValue = _CurrentIdealSDF[id.xy].r;  // 当前帧理论值
    float prevValue = _PrevSDFBuffer[id.xy].r;      // 上一帧平滑值
    
    float diff = targetValue - prevValue;
    
    // 非对称速度选择
    float currentSpeed = (diff > 0.0) ? _SDFAttackSpeed : _SDFDecaySpeed;
    
    // 指数平滑
    float factor = saturate(1.0 - exp(-currentSpeed * _DeltaTime));
    float smoothedValue = lerp(prevValue, targetValue, factor);
    
    _PrevSDFBuffer[id.xy] = float4(smoothedValue, smoothedValue, smoothedValue, 1.0);
}
```

**参数建议**：

| 参数 | 推荐值 | 效果 |
|------|--------|------|
| `_sdfAttackSpeed` | 15 | 能量快速建立 |
| `_sdfDecaySpeed` | 2 | 能量缓慢消散（值越小拖影越长） |

### 平滑效果对比

```
无平滑：
节点移动 → SDF 瞬间跳变 → 视觉闪烁 ❌

简单平滑：
节点移动 → SDF 均匀过渡 → 平滑但缺乏物理感 ⚠️

惯性阻尼：
节点移动 → SDF 快速建立 + 缓慢消散 → 粘滞拖影效果 ✅
```

---

## 渲染管线流程

### 完整渲染流程

```
┌─────────────────────────────────────────────────────────────┐
│                    第 1 帧：初始化                            │
├─────────────────────────────────────────────────────────────┤
│ 1. ViewportBackgroundQuad.Awake()                          │
│    - 获取 Camera 引用                                       │
│    - 初始化材质                                             │
│    - 初始化 Shader 桥接器列表                                │
│                                                             │
│ 2. DiffusionFieldBridge.OnMaterialReady()                  │
│    - InitializeComputeShader()                             │
│    - CreateRenderTextures()                                │
│    - InitializeBuffers() (清零)                            │
│                                                             │
│ 3. BigMapGPUBufferManager.Awake()                          │
│    - 创建 Node/Edge ComputeBuffer                          │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                  每帧：LateUpdate                           │
├─────────────────────────────────────────────────────────────┤
│ 1. ViewportBackgroundQuad.LateUpdate()                     │
│    - 检测相机参数变化 (orthoSize, aspect)                  │
│    - 按需更新 Quad 变换                                      │
│    - ApplyGPUBuffersToMaterial()                           │
│    - UpdateShaderBridges()                                 │
│                                                             │
│ 2. DiffusionFieldBridge.UpdateShaderProperties()           │
│    - 设置 ComputeShader 参数                                │
│      • 节点数据 (NodeBuffer)                                │
│      • 屏幕/相机参数                                        │
│      • 扩散参数 (GradientStep, InjectStrength)             │
│      • 各向异性校正参数                                     │
│                                                             │
│    - Dispatch ComputeShader Kernels:                       │
│      ① CSDiffuseInject (扩散 + 注入)                        │
│      ② CSSpatialSDF (空间 SDF)                              │
│      ③ CSInertialSDFDamping (时间平滑)                     │
│                                                             │
│    - 交换 Ping-Pong 缓冲区                                   │
│    - 设置材质纹理属性                                       │
│                                                             │
│ 3. DiffusionDisplay.shader (渲染 Pass)                     │
│    - 顶点着色器：传递 UV                                     │
│    - 片元着色器：                                            │
│      ① 三角形密铺映射                                        │
│      ② 中心采样 SDF 纹理                                      │
│      ③ 计算缩放三角形                                        │
│      ④ 边框 + 填充混合                                        │
│      ⑤ 输出最终颜色                                          │
└─────────────────────────────────────────────────────────────┘
```

### Ping-Pong 缓冲机制

```
帧 N:
┌──────────┐    ┌──────────┐
│ BufferA  │───→│ BufferB  │
│ (输入)   │    │ (输出)   │
└──────────┘    └──────────┘
      ↑               │
      └───────────────┘
         下一帧交换

帧 N+1:
┌──────────┐    ┌──────────┐
│ BufferB  │───→│ BufferA  │
│ (输入)   │    │ (输出)   │
└──────────┘    └──────────┘
```

**优点**：
- 避免读写冲突
- 实现时间演化
- 无需额外内存分配

---

## 参数调优指南

### 核心参数

#### 1. `_gradientStep` (梯度步长)

```csharp
// 推荐范围：0.0001 ~ 0.005
// 默认值：0.002
[SerializeField, Range(0.0001f, 0.005f)] 
private float _gradientStep = 0.002f;
```

**效果**：
- 值越大 → 坡度越陡 → 扩散范围越小
- 值越小 → 坡度越缓 → 扩散范围越大

**调优建议**：
```
柔和扩散场：0.001 ~ 0.002
中等扩散场：0.002 ~ 0.003
硬朗扩散场：0.003 ~ 0.005
```

#### 2. `_injectStrength` (注入强度)

```csharp
// 推荐范围：0.1 ~ 5.0
// 默认值：1.0
[SerializeField, Range(0.1f, 5.0f)] 
private float _injectStrength = 1.0f;
```

**效果**：
- 控制节点处的最大高度
- 影响 SDF 场的强度

#### 3. `_injectRadius` (注入半径)

```csharp
// 推荐范围：0.01 ~ 0.5
// 默认值：0.05
[SerializeField, Range(0.01f, 0.5f)] 
private float _injectRadius = 0.05f;
```

**效果**：
- 节点影响的最小半径
- 过小时可能出现空洞

#### 4. `_sdfGradient` (SDF 梯度)

```csharp
// 推荐范围：0.01 ~ 1.0
// 默认值：0.1
[SerializeField, Range(0.01f, 1.0f)] 
private float _sdfGradient = 0.1f;
```

**效果**：
- 值越大 → SDF 下降越快 → 场范围越小
- 值越小 → SDF 下降越缓 → 场范围越大

#### 5. 时间平滑参数

```csharp
// 惯性阻尼模式推荐值
[SerializeField] private float _sdfAttackSpeed = 15.0f;   // 建立速度
[SerializeField] private float _sdfDecaySpeed = 2.0f;    // 消散速度
```

**调优建议**：
```
快速响应：Attack=20, Decay=5
标准效果：Attack=15, Decay=2
强烈拖影：Attack=10, Decay=0.5
```

### 呼吸灯效果

```csharp
[Header("呼吸灯效果")]
[SerializeField] private bool _enableBreathing = false;
[SerializeField] private float _breathingPeriodX = 3.0f;   // 周期 (秒)
[SerializeField] private float _breathingAmplitudeX = 0.3f; // 振幅
```

**效果**：X/Y 方向扩散速度周期性变化，产生"呼吸"视觉效果

---

## 性能优化

### 1. 纹理尺寸选择

```csharp
[Tooltip("模拟纹理宽度（512=低配，1024=标配，2048=高配）")]
[SerializeField] private int _textureWidth = 1024;
[SerializeField] private int _textureHeight = 1024;
```

**性能对比**：
| 尺寸 | GPU 内存 | Dispatch 线程组 | 推荐平台 |
|------|---------|----------------|----------|
| 512×512 | ~1MB | 64×64 = 4096 | 移动端 |
| 1024×1024 | ~4MB | 128×128 = 16384 | PC 标配 |
| 2048×2048 | ~16MB | 256×256 = 65536 | PC 高配 |

### 2. Kernel 合并优化

**优化前**：
```hlsl
// 两个独立的 Kernel
CSInject()    // 注入
CSDiffuse()   // 扩散
```

**优化后**：
```hlsl
// 合并为一个 Kernel
CSDiffuseInject()  // 注入 + 扩散
```

**收益**：
- 减少 Dispatch 调用次数
- 减少纹理读写次数
- 提升 GPU 利用率

### 3. 属性 ID 缓存

```csharp
// 静态缓存 Property ID（避免每帧字符串哈希）
private static readonly int GradientStepID = Shader.PropertyToID("_GradientStep");
private static readonly int NodesID = Shader.PropertyToID("_Nodes");
```

### 4. 相机静止检测

```csharp
[Tooltip("启用则在相机移动时禁止注入（只有静止时才注入）")]
[SerializeField] private bool _blockInjectionWhenCameraMoving = true;
```

**效果**：相机移动时暂停注入，减少不必要的计算

---

## 故障排查

### 问题 1：扩散场不显示

**可能原因**：
1. ComputeShader 编译失败
2. GPU 缓冲区未就绪
3. 材质未正确设置

**排查步骤**：
```
1. 检查 Console 是否有 ComputeShader 编译错误
2. 确认 BigMapGPUBufferManager.Instance.IsReady == true
3. 检查 ViewportBackgroundQuad 的 Mode 配置
4. 调用 DiffusionFieldBridge.DebugLogCurrentState()
```

### 问题 2：扩散场呈扁平菱形

**原因**：各向异性校正未生效

**解决**：
```csharp
// 检查相机 aspect 是否正确传递
Debug.Log($"相机 aspect: {_targetCamera.aspect}");

// 检查 DiffusionAspectX 是否设置
_computeShader.GetFloat("_DiffusionAspectX", out float aspectX);
Debug.Log($"DiffusionAspectX: {aspectX}");
```

### 问题 3：SDF 场闪烁

**原因**：时间平滑未启用或参数不当

**解决**：
```csharp
// 启用惯性阻尼模式
_temporalMode = SDFTemporalMode.InertialDamping;

// 调整衰减速度（越小拖影越长）
_sdfDecaySpeed = 2.0f;  // 尝试降低到 1.0 或 0.5
```

### 问题 4：性能过低

**排查**：
```
1. 降低纹理尺寸：1024 → 512
2. 减少节点数量
3. 禁用呼吸灯效果
4. 使用 DiffusionOnly 模式（禁用 SDF）
```

### 调试工具

```csharp
// 启用调试信息
[SerializeField] private bool _showDebugInfo = true;

// 手动输出状态
diffusionBridge.DebugLogCurrentState();

// 强制重新初始化
diffusionBridge.ForceReinitialize();
```

---

## 附录：文件结构

```
Assets/
├── Scripts/OutStage/BigMap/
│   ├── DiffusionFieldBridge.cs      # 主桥接器
│   ├── ViewportShaderBridge.cs      # 桥接器基类
│   ├── ViewportBackgroundQuad.cs    # 几何控制器
│   └── BigMapGPUBufferManager.cs    # GPU 缓冲区管理
│
├── ComputeShaders/
│   └── DiffusionField.compute       # 扩散场计算着色器
│
└── Shaders/Bg/
    └── DiffusionDisplay.shader      # 渲染着色器
```

---

**文档版本**：1.0  
**最后更新**：2026 年 3 月 6 日  
**作者**：Qwen Code (喵~) 🐱
