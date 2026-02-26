# Shader透明部分深度写入问题分析

## 概述

2026年2月26日，在修复GPU实例化渲染顺序问题的过程中，发现将shader的`ZWrite`从`Off`改为`On`后，虽然解决了层间渲染顺序问题，但引入了透明部分深度写入的新问题。本文档分析两个关键shader的深度写入配置，并提出视觉权衡方案。

## 涉及Shader文件

### 1. DashedLine.shader
**路径**: `Assets/Shaders/PowerNet/DashedLine.shader`
**用途**: 绘制电力网络、选择框等虚线效果
**关键配置**:
```hlsl
Tags { "RenderType"="Transparent" "Queue"="Transparent" }
Blend SrcAlpha OneMinusSrcAlpha
ZWrite On  // 已从 Off 改为 On
Cull Off
```

**透明逻辑**:
```hlsl
float isGap = step(_DashRatio, frac(scrollingUV));
fixed4 col = i.color;
col.a *= (1.0 - isGap);  // 间隙部分alpha=0，实线部分alpha=原始值
```

### 2. SimpleInstancing.shader
**路径**: `Assets/Shaders/Sprite/SimpleInstancing.shader`
**用途**: GPU实例化渲染单位、建筑等精灵
**关键配置**:
```hlsl
Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
Blend SrcAlpha OneMinusSrcAlpha
ZWrite On  // 已从 Off 改为 On
```

**透明逻辑**:
- 依赖纹理的alpha通道
- 使用实例化颜色`_BaseColor`相乘

## 问题分析

### ZWrite On 的优点
1. **恢复深度测试**: 同一renderQueue内的实例可以基于Z值建立正确前后关系
2. **层间顺序生效**: `renderQueue`递进策略(3000<3010<3020)能够正常运作
3. **解决遮挡问题**: 单位不再被Tilemap"吞没"

### ZWrite On 的缺点
1. **透明部分写入深度**: 即使alpha=0的片段也会写入深度缓冲
2. **虚假遮挡**: 虚线间隙、精灵透明边缘会遮挡后面的物体
3. **半透明排序问题**: 半透明物体间的正确混合需要特定渲染顺序

### 具体问题表现
- **DashedLine虚线**: 间隙部分(alpha=0)写入深度，导致后方单位被错误遮挡
- **SimpleInstancing精灵**: 纹理边缘抗锯齿的透明像素写入深度，产生边缘"硬边"
- **视觉异常**: 透明部分本应"透过去"看到后面内容，实际却被遮挡

## 技术权衡

### 方案1：保持 ZWrite On（当前状态）
**优点**:
- 深度关系稳定可靠
- GPU实例化性能最优
- 渲染顺序完全可控

**缺点**:
- 透明区域产生虚假遮挡
- 需要配合alpha测试或clip操作
- 视觉上可能出现"孔洞"被错误填充

### 方案2：回归 ZWrite Off（原始状态）
**优点**:
- 透明部分不干扰深度
- 视觉上更"自然"的透明效果

**缺点**:
- 深度测试失效，层间顺序依赖renderQueue粗粒度控制
- 同一队列内实例深度关系不可控
- 可能回归单位被地形遮挡的问题

### 方案3：混合方案 - Alpha Test
**实现思路**:
```hlsl
// 在fragment shader中添加
clip(col.a - 0.001);  // alpha低于阈值时丢弃片段
```
或使用Unity的AlphaTest渲染状态。

**优点**:
- 完全透明部分不写入深度
- 保持深度测试的有效性
- 性能影响较小

**缺点**:
- 抗锯齿边缘可能产生锯齿
- 需要设置合适的alpha阈值
- 半透明过渡区域处理困难

### 方案4：动态ZWrite控制
**实现思路**:
```hlsl
// 根据alpha值动态决定是否写入深度
if (col.a > 0.99) ZWrite On;
else ZWrite Off;
```
或在两个Pass中分别处理不透明和透明部分。

**优点**:
- 精确控制深度写入
- 视觉质量最高

**缺点**:
- 实现复杂，可能破坏实例化
- 性能开销较大
- 需要修改shader架构

## 推荐解决方案

基于MineRTS项目的实际需求（2D RTS，GPU实例化为主），建议采用**方案3：Alpha Test**的变体：

### 具体修改建议

#### 1. DashedLine.shader 修改
```hlsl
// 在frag函数返回前添加
clip(col.a - 0.01);  // 1% alpha阈值，完全透明部分不写入深度
```

#### 2. SimpleInstancing.shader 修改
```hlsl
// 在frag函数返回前添加
half4 texColor = tex2D(_MainTex, i.uv);
half4 finalColor = texColor * color;
clip(finalColor.a - 0.01);  // 丢弃完全透明像素
return finalColor;
```

#### 3. 配套设置调整
- 在材质中启用Alpha Testing（如Unity支持）
- 调整阈值平衡视觉质量和深度正确性
- 对于需要柔和边缘的物体，考虑使用特殊处理

## 视觉权衡决策

### 优先保障的视觉效果
1. **单位可见性**: 确保单位始终显示在Tilemap之上
2. **选择框清晰**: 虚线框必须清晰可见，不被背景干扰
3. **层间顺序**: 传送带<单位<血条的基础顺序必须正确

### 可以接受的妥协
1. **轻微边缘锯齿**: AlphaTest导致的硬边在2D像素风格中可以接受
2. **透明混合限制**: 完全透明和完全不透明之间的过渡可以简化
3. **性能优先**: 在视觉质量可接受范围内，优先保持GPU实例化性能

### 不可接受的退化
1. **单位被地形遮挡**
2. **层间顺序混乱**
3. **选择框无法看清**

## 实施计划

### 阶段1：测试验证
1. 在测试场景中验证当前ZWrite On的问题表现
2. 记录具体的视觉异常案例
3. 确定可接受的alpha阈值范围

### 阶段2：方案实现
1. 修改shader添加alpha测试
2. 调整材质参数
3. 测试各种透明情况（虚线、精灵边缘、半透明UI）

### 阶段3：性能评估
1. 比较修改前后的渲染性能
2. 验证GPU实例化批次是否受影响
3. 确保修改不会引入新的渲染问题

## 结论

深度写入是2D游戏中GPU实例化渲染的核心挑战之一。在MineRTS项目中，我们选择了一种**实用主义的权衡**：

- **保持ZWrite On**以维护基本的深度关系和层间顺序
- **添加Alpha Test**以消除完全透明部分的深度干扰
- **接受轻微视觉妥协**以换取稳定的渲染性能和正确的游戏功能

这种方案在"完美视觉质量"和"可用游戏体验"之间找到了平衡点，是2D RTS项目在Unity URP和GPU实例化技术约束下的合理选择。

---

**记录日期**: 2026-02-26
**分析人员**: Claude Code
**相关文档**:
- `Documentation/2026-02-25/MineRTS_Depth_System_Fix_Report.md`
- `Assets/Shaders/PowerNet/DashedLine.shader`
- `Assets/Shaders/Sprite/SimpleInstancing.shader`