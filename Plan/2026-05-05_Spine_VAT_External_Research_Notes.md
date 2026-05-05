# Spine VAT External Research Notes

## 日期
- 2026-05-05

## 范围

本笔记只记录和当前讨论直接相关的外部事实：

- `Spine` 官方运行时现状
- Unity 官方 2D 动画 GPU 变形现状
- `Spine -> GPU` 是否已有现成官方方案
- 对当前 `Spine -> VAT / GPU 顶点动画数据化` 方向的直接影响

---

## 1. Spine 官方导出与运行时的现状

### 1.1 官方标准导出仍然是 skeleton data + atlas

Spine 官方文档当前仍明确把标准导出定义为：

```text
JSON / binary skeleton data
+ texture atlas
```

没有官方现成的 `VAT` 导出格式。

参考：
- https://en.esotericsoftware.com/spine-runtimes
- https://en.esotericsoftware.com/spine-export/

### 1.2 官方运行时当前仍以 CPU 侧几何处理为主

Spine 官方论坛说明，运行时通常是在 CPU 侧批量生成几何，然后每帧提交到 GPU。

原话核心是：

```text
we batch geometry on the CPU and send that to the GPU each frame
```

并且官方明确表示：

- GPU skinning 可以实现
- 但并不是官方现成默认路径
- 渲染部分开源，允许项目方按自身场景做定制

参考：
- https://us.esotericsoftware.com/forum/d/7877-animation-processing-cpu-or-gpu

### 1.3 spine-unity 官方仍未提供现成 GPU skinning 落地

官方论坛在公开回复里说明：

- Unity 自带 2D Animation 更可能有相同或更好的性能
- 原因之一是它使用 GPU skinning
- `spine-unity` 计划支持 GPU skinning
- 但当时尚未实现

对应公开 issue：
- https://github.com/EsotericSoftware/spine-runtimes/issues/1843

参考：
- https://en.esotericsoftware.com/forum/d/15397-is-there-any-testing-about-2d-animation-vs-spine-performance

---

## 2. Unity 官方 2D 动画的现状

### 2.1 Unity 官方 2D Animation 已提供 GPU deformation

Unity 官方文档明确说明：

从 `2D Animation 10 (Unity 2023.1)` 开始，`Sprite Skin` 可以选择：

- CPU deformation
- GPU deformation

并且该 GPU deformation 仅在 `URP` 下可用。

参考：
- https://docs.unity3d.com/ja/Packages/com.unity.2d.animation%4013.0/manual/SpriteSkin.html

### 2.2 Unity 官方文档并没有说 GPU 一定更适合低多边形海量对象

Unity 文档反而给了一个很重要的使用建议：

- 很多低多边形对象：更倾向 CPU deformation
- 少量高多边形对象：更倾向 GPU deformation
- 最终都要以 profiling 为准

这说明：

```text
GPU deformation 存在
!=
任何 2D 角色海量同屏都天然最优
```

对当前项目的意义是：

- 不能只因为“GPU”三个字就默认收益成立
- 仍然要结合你们的实例化路径、顶点数、材质组织和绘制模型来算

---

## 3. 对当前 Spine VAT 方向的直接启发

### 3.1 现成官方可用路径并不存在

截至当前调研：

- Spine 官方没有现成 `Spine -> VAT` 导出链
- 官方运行时默认也不是 GPU 顶点动画回放
- 这条路线如果要走，基本可以视为项目自定义资产管线

这不是否定，而是边界确认。

### 3.2 方向本身并不违背官方结构

Spine 官方导出：

- skeleton data
- mesh / attachment / deform 等信息

官方运行时又是开源的。

因此：

```text
离线读取 Spine 数据
-> 烘焙出项目自定义的顶点动画数据
-> 运行时用自定义 shader / instancing 回放
```

在技术上是顺着官方开放边界走的，不是和官方体系对着干。

### 3.3 需要特别注意 attachment / mesh 复用问题

Spine 官方论坛有一个很关键的信息：

`FFD keys are per attachment`

也就是说：

- deform 是按 attachment 绑定的
- 不同 attachment 可能顶点数不同
- 也可能顶点顺序不同

这对当前方案的意义非常直接：

如果一段动画里存在：

- attachment 切换
- 不同 mesh 拓扑
- 不同 region / image 替换且不再共享同一套网格关系

那么：

```text
静态 uv + 静态拓扑 + 逐帧 xy
```

这个最理想的 VAT 假设就会被破坏。

参考：
- https://us.esotericsoftware.com/forum/d/2184-is-there-a-way-to-reuse-a-mesh-animation/14

### 3.4 自动化导出是有基础条件的

Spine 官方提供 CLI，可用于：

- export
- import
- pack

这意味着如果后面要做项目内离线烘焙工具，至少可以把：

- Spine 数据导出
- atlas/贴图打包

纳入自动化流程。

参考：
- https://en.esotericsoftware.com/spine-command-line-interface

---

## 4. 对 MineRTS 当前方案的判断

结合当前项目已有条件：

- 单位资产生产以 `Spine` 为主
- 当前渲染主线已是 `instancing`
- 小单位顶点规模不高
- 不希望把 `Spine` 连续动画降格成笨重 atlas 帧序列

外部资料支持以下判断：

### 4.1 继续依赖官方 spine-unity 直接解决批量 GPU 动画，不现实

因为官方现状仍偏：

```text
CPU side skinning / geometry build
```

至少目前没有一个“开箱即用、官方支持、直接适合海量同屏 Spine 小单位”的 GPU 批量解法。

### 4.2 自定义离线烘焙路线是合理研究方向

因为：

- 官方导出数据开放
- 官方运行时开源
- Unity 自身已经证明 2D GPU deformation 这件事在引擎层不是禁区

所以你们当前想研究的方向，实质上更像：

```text
Spine source
-> project-specific bake
-> custom GPU playback
```

这条路线是合理的。

### 4.3 真正的风险点不在“能不能做”，而在资源约束是否足够干净

最核心的问题会是：

- 动画中是否频繁切 attachment
- attachment 是否共享稳定拓扑
- uv 是否能静态化
- 是否允许限制部分 Spine 功能，换取稳定批量播放格式

也就是说，下一阶段最该验证的是资源规范，不是抽象概念。

---

## 当前结论

截至 `2026-05-05` 的外部调研结论：

- `Spine -> VAT` 没有现成官方落地方案
- `spine-unity` 目前公开信息仍偏 CPU 侧
- Unity 官方 2D Animation 已有 GPU deformation，但那是 Unity 自家 2D 骨骼体系，不是 Spine 现成替代物
- 所以如果 MineRTS 要走这条路，本质上是在做一条自定义的 `Spine 离线烘焙 -> GPU 回放` 管线

这条路不是现成插件路线，但技术方向是成立的，且与当前项目目标相符。
