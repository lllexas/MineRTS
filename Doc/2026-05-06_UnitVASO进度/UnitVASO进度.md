# UnitVASO 进度记录

**日期**: 2026 年 5 月 6 日  
**状态**: Editor bake 与 VA 预览已跑通，等待 runtime 接入  
**作者**: NekoTeam

---

## 当前结论

当前单位动画链路已经从概念阶段推进到 Unity Editor 可验证阶段：

```text
概念图
-> Photoshop 拆分 / 修整素材
-> Spine 3.8 制作骨骼动画
-> spine-unity 导入 skeleton data + atlas
-> UnitVASO bake
-> UnitVASO preview 直接播放 baked 顶点动画
```

这里的 `UnitVASO` 不是传统纹理 VAT 资源。当前项目更合适的落点是：

```text
Mesh + BaseTexture + VA positions
```

`Mesh` 保存稳定拓扑和静态 UV，`BaseTexture` 保存颜色采样对象，`UnitVASO` 保存按 clip/frame 分组的顶点 xy 姿态数据。运行时 GPU buffer / instancing 数据可以从 `UnitVASO` 派生，而不是直接把 editor authoring asset 做成最终 buffer。

---

## 已完成内容

### 1. UnitVASO 数据结构

位置：

```text
Assets/Scripts/InStage/Animation/UnitVASO.cs
```

当前结构按 Spine 到 VA 的组织转换来设计：

```text
UnitVASO
-> Clips
-> Frames
-> Positions[]
```

关键口径：

- `UnitVASO.Mesh`: 静态拓扑、triangles、uv。
- `UnitVASO.BaseTexture`: Spine atlas 对应的主纹理。
- `UnitVAClip.SourceAnimationName`: Spine animation 名。
- `UnitVAClip.State`: 项目内 `UnitAnimationStateId`。
- `UnitVAClip.Loop`: 只描述 VA clip 自身循环语义。
- `UnitVAFrame.Positions`: 当前帧所有顶点 local xy，顺序必须匹配 `Mesh` 顶点顺序。

`LockUntilComplete` 不属于这个层级，已经从 `UnitVAClip` 中移除。打断、锁动作、优先级应由更上层动画状态机或行为系统处理。

### 2. Spine JSON 到 UnitVASO 的创建入口

位置：

```text
Assets/Scripts/InStage/Editor/UnitVASOEditor.cs
```

Project 窗口选中 Spine JSON 后：

```text
Create -> MineRTS -> Animation -> Unit VASO From Spine JSON
```

创建结果应当放在 Spine JSON 同目录，例如：

```text
Assets/Resources/SpineAssets/信徒/信徒_UnitVA.asset
```

创建流程会填充：

- `SourceJson`
- `SourceSkeletonDataAsset`
- `SourceAssetGuid`
- `SourceAssetPath`
- `SourceSpineVersion`
- `BaseTexture`
- 初始 clip 列表

状态名目前有一层名称猜测，例如 `Attack`、`Idle`、`Walk`、`Die`、`Stun`。

### 3. Spine bake 到 VA frames

Bake 按当前 `UnitVASO.BakeSampleFps` 从 spine-unity runtime 采样 animation：

```text
Spine.Animation.Apply(time)
-> Skeleton.UpdateWorldTransform()
-> MeshGenerator
-> Mesh vertices
-> UnitVAFrame.Positions
```

Bake 过程中要求拓扑稳定：

- 顶点数必须稳定。
- triangles 必须稳定。
- uv 必须稳定。

如果某个动画中途切换附件导致拓扑变化，当前 bake 会失败。这是有意为之，因为当前 runtime 目标是固定 mesh 拓扑下的顶点动画回放。

### 4. UnitVASO 预览窗口

位置：

```text
Assets/Scripts/InStage/Editor/UnitVAClipPreviewSession.cs
Assets/Scripts/InStage/Editor/UnitVAClipPreviewWindow.cs
Assets/Scripts/InStage/Editor/UnitVAPreviewMeshBuilder.cs
```

每个 clip 行左侧有小眼睛按钮。点击后：

- 打开独立 `Unit VA Clip Preview` 窗口。
- Inspector 切换为该 clip 的专用预览控制界面。
- 再次点击当前高亮小眼睛会关闭 preview。

预览不是 Spine 实时骨骼播放，而是直接播放 baked VA 数据：

```text
UnitVASO.Mesh.triangles / uv
+ UnitVAClip.Frames[frameIndex].Positions
+ UnitVASO.BaseTexture
```

预览窗口已修正为固定参照系：相机会使用整个 clip 的 union bounds，不再每帧追当前 mesh bounds 居中。这样可以观察真实位移，而不是被相机抵消。

### 5. Spine 3.8.75 版本拦截

spine-unity 3.8 runtime 中存在对 `3.8.75` 的硬编码拒绝：

```text
Unsupported skeleton data, please export with a newer version of Spine.
```

当前已在本地 spine runtime 中移除这个 gate：

```text
Assets/Spine/Runtime/spine-csharp/SkeletonJson.cs
Assets/Spine/Runtime/spine-csharp/SkeletonBinary.cs
```

处理方式是允许 `3.8.75` 继续进入 3.8 reader，不改写 `skeletonData.version`。这样后续诊断仍能看到真实源版本。

注意：`Assets/Spine` 当前被 `.gitignore` 忽略。若后续协作者需要复现该补丁，需要确认本地 spine-unity runtime 中也做了同样修改，或者把补丁以项目内部说明 / patch 文件形式管理。

---

## 帧、段、姿态的统一口径

后续不要再混用 `frame`、`segment`、`pose`。

当前采用以下口径：

- `frame index`: Spine 时间轴上的帧边界编号。
- `segment`: 两个相邻 frame boundary 之间的时间段。
- `pose`: VA bake 后实际保存的一个顶点姿态。

例如：

```text
Source: 60 seg @ 60fps = 1s, 0..60f
```

含义是 Spine 源动画从 frame 0 到 frame 60，共 60 个时间段，总时长 1 秒。

Loop clip：

```text
Source: 60 seg @ 60fps = 1s, 0..60f
Bake: 60 poses, 0..59f, seam omitted
```

Non-loop clip：

```text
Source: 60 seg @ 60fps = 1s, 0..60f
Bake: 61 poses, 0..60f, endpoint kept
```

结论：Spine 源动画不要为了让 baked pose count 看起来等于 60 而改成 `0..59`。`0..59 @ 60fps` 是 `59/60s`，不是完整 1 秒。

---

## 当前样例资产

当前用于验证的样例路径：

```text
Assets/Resources/SpineAssets/信徒/
```

已有 UnitVASO：

```text
Assets/Resources/SpineAssets/信徒/信徒_UnitVA.asset
Assets/Resources/SpineAssets/信徒/信徒_UnitVA_Mesh.asset
```

如果改过 `BakeSampleFps`、Spine 源 JSON、loop 标记或 spine-unity runtime，需要重新执行 `Bake From Spine`。Inspector 会对当前 baked pose count 与预期 pose count 做基础诊断。

---

## 已知注意事项

### 1. BaseTexture alpha

建议 Spine 导出 atlas 时使用 straight alpha，不要 premultiply alpha。Unity Linear color space 下 PMA 会触发 spine-unity warning，并且后续自定义 shader 也更适合明确处理 straight alpha。

### 2. 拓扑稳定是当前方案前提

当前 VA preview 和后续 runtime 都假设：

```text
同一个 UnitVASO 的所有 clip/frame 共用同一个 Mesh 拓扑与 uv
```

如果 Spine 动画中存在附件切换导致顶点数或 triangles 改变，需要先在资产制作规范上规避，或后续设计多 mesh / 多段 clip 支持。

### 3. UnitVASO 是 authoring asset，不是最终 GPU buffer

不要在 `UnitVASO` 里直接手写最终 `StructuredBuffer` 数据。当前保留分层结构是为了：

- 方便从 Spine 组织形式转换。
- 方便 Inspector 检查 clip/frame。
- 方便 preview 验证。
- 后续 runtime 可以按实际渲染批处理需求再 flatten。

---

## 下一步

下一阶段应当接 runtime，建议顺序：

1. 做一个最小 runtime 播放组件，输入 `UnitVASO + State + Time`，在 CPU 侧临时更新 mesh vertices，验证逻辑口径。
2. 设计 GPU buffer flatten 结构，将 `clip -> frame -> vertex -> xy` 转成运行时连续 buffer。
3. 写 VA shader，通过 instance 参数决定当前 unit 采样哪个 clip/frame。
4. 接入现有单位绘制路径，先单单位，再 instancing。
5. 最后处理状态机层：loop、non-loop endpoint、动作打断、stun、death 等语义。

当前不要优先做更复杂的纹理 VAT 编码。现阶段的目标是让：

```text
UnitVASO.Mesh + UnitVASO.BaseTexture + VA positions
```

先在真实单位渲染路径中跑起来。
