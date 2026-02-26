# GPU实例化渲染流程详解

## 概述

MineRTS作为一款2D实时战略游戏，面临着大量单位同时渲染的性能挑战。传统的`SpriteRenderer`组件每个单位都需要独立的Draw Call，当单位数量达到数百时性能急剧下降。为解决此问题，项目采用了**GPU实例化（GPU Instancing）** 技术，将相同材质的多个物体合并为单个Draw Call，大幅提升渲染性能。

## 为什么选择GPU实例化？

### 性能对比
- **传统方式**: 每个单位1个Draw Call → 1000个单位 = 1000个Draw Call
- **GPU实例化**: 相同材质的单位合并 → 1000个单位 ≈ 1-10个Draw Call
- **性能提升**: 10-100倍的Draw Call减少，CPU到GPU的数据传输大幅降低

### 技术选型考量
1. **2D精灵渲染**：单位主要是2D Sprite，适合实例化
2. **批量相似性**：同种单位使用相同纹理，完美契合实例化前提
3. **动态位置**：单位位置、旋转、缩放每帧变化，需要动态更新实例数据
4. **Unity版本支持**：Unity 2022.3 + URP 14.0 完整支持GPU实例化

## 核心组件架构

### 1. SpriteLib（精灵资源库）
**文件**: `Assets/Scripts/InStage/SpriteLib.cs`

**职责**:
- 管理所有单位Sprite资源
- 为每个Sprite创建专用的Mesh和Material实例
- 建立Sprite ID到渲染资源的映射关系

**关键实现**:
```csharp
// 为每个Sprite创建独立材质实例
var mat = new Material(targetShader);
mat.enableInstancing = true;
mat.renderQueue = 4000;  // 基础渲染队列
mat.SetTexture("_MainTex", sprite.texture);

// 根据Sprite在图集中的位置创建裁切Mesh
Mesh mesh = CreateMeshForSprite(sprite);
```

### 2. DrawSystem（绘制系统）
**文件**: `Assets/Scripts/InStage/System/DrawSystem.cs`

**职责**:
- 每帧收集所有单位的渲染数据
- 组织实例化绘制批次
- 管理渲染顺序和深度

**工作流程**:
1. **数据收集**: 遍历ECS的WholeComponent，提取位置、旋转、缩放等变换数据
2. **批次分组**: 按Sprite ID和Sorting Layer分组单位
3. **矩阵计算**: 为每个单位生成变换矩阵（TRS Matrix）
4. **实例化绘制**: 使用`Graphics.RenderMeshInstanced`批量渲染

### 3. SimpleInstancing Shader
**文件**: `Assets/Shaders/Sprite/SimpleInstancing.shader`

**特性**:
- 支持GPU实例化的自定义Shader
- 使用HLSL编写，兼容URP渲染管线
- 通过实例化缓冲区传递每单位颜色

**关键代码**:
```hlsl
// 实例化属性缓冲区
UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(Props)

// 顶点着色器传递实例ID
UNITY_SETUP_INSTANCE_ID(v);
UNITY_TRANSFER_INSTANCE_ID(v, o);

// 片元着色器读取实例颜色
float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
```

## 渲染管线详解

### 第1阶段：数据准备（DrawSystem.UpdateWithInstancing）

```csharp
// 1. 清理上一帧数据
foreach (var list in _conveyorMatrices.Values) list.Clear();
foreach (var list in _unitMatrices.Values) list.Clear();

// 2. 遍历所有实体，收集矩阵数据
for (int i = 0; i < count; i++)
{
    ref var core = ref whole.coreComponent[i];
    if (!core.Active) continue;

    // 计算变换矩阵
    Vector3 pos = new Vector3(core.Position.x, core.Position.y + jumpOffset, zPos);
    Quaternion rot = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, core.Rotation));
    Vector3 scaleVal = new Vector3(core.VisualScale.x * stretchX, core.VisualScale.y * stretchY, 1);

    // 添加到对应分组
    Matrix4x4 matrix = Matrix4x4.TRS(pos, rot, scaleVal);
    targetDict[spriteId].Add(matrix);
}
```

### 第2阶段：材质准备（DrawBatch方法）

```csharp
private void DrawBatch(Dictionary<int, List<Matrix4x4>> batchDict, int priority, int renderQueue, Dictionary<int, Material> cache)
{
    foreach (var batch in batchDict)
    {
        int spriteId = batch.Key;
        List<Matrix4x4> matrices = batch.Value;

        // 获取或创建带特定renderQueue的材质副本
        if (!cache.TryGetValue(spriteId, out Material layeredMat))
        {
            Material baseMat = _spriteLib.GetMaterial(spriteId);
            layeredMat = new Material(baseMat);
            layeredMat.renderQueue = renderQueue;  // 设置渲染队列
            cache[spriteId] = layeredMat;
        }

        // 设置RenderParams并执行实例化绘制
        RenderParams rp = new RenderParams(layeredMat)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            matProps = _propertyBlock,
            rendererPriority = priority
        };

        Graphics.RenderMeshInstanced(rp, mesh, 0, matrices);
    }
}
```

### 第3阶段：GPU执行

1. **CPU端**: Unity收集所有实例的变换矩阵到连续内存
2. **数据传输**: 矩阵数据通过常量缓冲区发送到GPU
3. **GPU端**: 顶点着色器读取实例ID，从缓冲区获取对应变换矩阵
4. **顶点变换**: 应用实例特定的位置、旋转、缩放
5. **片元处理**: 可选读取实例特定的颜色、属性

## 渲染顺序管理系统

### 三层排序机制

#### 1. Render Queue（渲染队列） - 粗粒度
```csharp
// DrawSystem.cs 中的队列设置
Conveyor: Queue 3000, Priority 10
Items:    Queue 3005 (TransportDrawSystem)
Units:    Queue 3010, Priority 30
HealthBar:Queue 3020, Priority 50
```

**原理**: Unity按Queue值从小到大渲染，确保传送带→物品→单位→血条的顺序

#### 2. Sorting Layer（排序层） - 中粒度
```csharp
// 不同系统使用不同的Sorting Layer
"Conveyor": 传送带系统
"Units": 战斗单位、建筑
"Number": 血量数字
"UI": 选择框、界面元素
"Power": 电力网络覆盖
"PathPreview": 路径预览
```

**原理**: 同一Queue内，按Sorting Layer顺序渲染

#### 3. Z轴值 - 细粒度
```csharp
// DrawSystem中的Z值设置
传送带: z = -1f
单位:   z = -3f
血条:   z = -5f
选择框: z = -1f (但在"UI"层)
```

**原理**: 同一Sorting Layer内，按Z值从大到小渲染（值越大越靠前）

### 透明物体的特殊处理

**问题**: 透明物体需要从后往前渲染才能正确混合

**解决方案**:
1. **Queue="Transparent"**: 所有半透明物体使用Transparent队列
2. **ZWrite策略**:
   - 最初使用`ZWrite Off` → 透明自然但深度关系混乱
   - 改为`ZWrite On` → 深度正确但透明部分虚假遮挡
   - 当前方案: `ZWrite On + Alpha Test` → 平衡方案

3. **Alpha Test实现**:
```hlsl
// 在fragment shader中添加
clip(col.a - 0.1);  // 丢弃alpha<0.1的片段，防止透明部分写入深度
```

## 性能优化策略

### 1. 对象池复用
```csharp
// SelectionOverlaySystem中的LineRenderer对象池
private List<LineRenderer> _selectionBoxPool = new List<LineRenderer>();

// 需要时从池中获取，避免频繁创建销毁
LineRenderer GetSelectionBoxFromPool(int index)
{
    while (_selectionBoxPool.Count <= index)
    {
        // 创建新实例
        GameObject go = new GameObject($"SelectionBox_{_selectionBoxPool.Count}");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        _selectionBoxPool.Add(lr);
    }
    return _selectionBoxPool[index];
}
```

### 2. 材质实例缓存
```csharp
// DrawSystem中的材质缓存
private Dictionary<int, Material> _conveyorMatCache = new Dictionary<int, Material>();
private Dictionary<int, Material> _unitMatCache = new Dictionary<int, Material>();

// 避免每帧创建新材质实例
if (!cache.TryGetValue(spriteId, out Material layeredMat))
{
    Material baseMat = _spriteLib.GetMaterial(spriteId);
    layeredMat = new Material(baseMat);  // 克隆并修改队列
    layeredMat.renderQueue = renderQueue;
    cache[spriteId] = layeredMat;  // 缓存以供复用
}
```

### 3. 批次合并优化
- **按Sprite ID分组**: 相同纹理的单位合并批次
- **1023实例限制**: `Graphics.RenderMeshInstanced`每批最多1023个实例
- **自动分批**: 超过限制时Unity自动拆分为多个批次

## 限制与挑战

### GPU实例化的固有局限

1. **排序灵活性差**: 实例化批次内的物体无法单独控制渲染顺序
2. **透明混合困难**: 半透明物体需要特定渲染顺序，与实例化冲突
3. **属性更新开销**: 每帧更新所有实例的变换矩阵仍有CPU开销
4. **Shader复杂度**: 需要特殊Shader支持实例化属性

### MineRTS的应对方案

1. **分层渲染**: 将不同渲染需求的物体分配到不同Queue和Sorting Layer
2. **Alpha Test折中**: 用`clip()`解决透明深度问题，接受轻微视觉妥协
3. **动态批次**: 每帧重新组织批次，适应单位位置变化
4. **Shader定制**: 开发专用的`SimpleInstancing.shader`满足项目需求

## 与其他渲染系统的协作

### 1. Tilemap渲染（传统方式）
- Tilemap使用Unity内置的`TilemapRenderer`
- 默认Queue="Geometry" (~2000) 或 "Transparent" (~3000)
- 单位使用Queue=3010，确保显示在Tilemap之上

### 2. 传统SpriteRenderer（特殊场合）
- 少量需要特殊效果的单位仍使用传统方式
- 与实例化系统共存，通过Sorting Layer隔离

### 3. UI系统（Canvas）
- UI使用独立的Canvas渲染
- Screen Space - Overlay模式，不受场景深度影响
- 与游戏场景渲染完全分离

## 调试与监控

### 性能分析工具
1. **Frame Debugger**: 查看每帧的Draw Call和批次信息
2. **Profiler**: 监控CPU渲染线程和GPU时间
3. **Stats窗口**: 实时查看Draw Call、三角形数量

### 关键性能指标
- **目标Draw Call**: <100个（1000个单位场景）
- **帧率目标**: 60 FPS (移动设备), 144+ FPS (PC)
- **CPU渲染时间**: <5ms
- **GPU时间**: <10ms

## 总结

MineRTS的GPU实例化渲染系统是一个**分层、分批、深度优化**的解决方案：

### 技术栈总结
- **底层**: Custom/SimpleInstancing Shader + GPU Instancing
- **中层**: DrawSystem批次管理 + SpriteLib资源池
- **上层**: RenderQueue + Sorting Layer + Z轴三级排序

### 设计哲学
1. **性能优先**: 在视觉可接受范围内最大化渲染性能
2. **实用主义**: 接受GPU实例化的固有限制，寻找最佳折中
3. **渐进优化**: 从简单实现开始，逐步解决透明度、排序等难题
4. **工具链完整**: 配套调试、监控、分析工具确保系统健康

### 未来优化方向
1. **Compute Shader加速**: 使用Compute Shader预处理实例数据
2. **GPU Driven Rendering**: 更彻底的GPU端渲染管线
3. **LOD系统**: 根据距离动态调整渲染细节
4. ** occlusion Culling**: 2D视锥裁剪和遮挡剔除

这套系统成功支撑了MineRTS大规模单位渲染的需求，是2D RTS项目在Unity引擎下的高性能渲染实践典范。