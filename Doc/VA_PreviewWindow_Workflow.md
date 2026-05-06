# VA Preview Window 流程说明

**日期**: 2026-05-06  
**状态**: ✅ 已实现（帧标签编辑待加）

---

## 1. 入口

用户在 `UnitVASO` 的 Inspector 中点击 clip 旁边的小眼睛按钮。

**位置**: `UnitVASOEditor.cs`
- `OnInspectorGUI` (line 29) 渲染 clips 列表
- 每个 clip 调用 `DrawClipPreviewButton(unitVA, clipIndex)` (line 172)
- 小眼睛按钮通过 `GUIContent GetEyeContent(bool active)` (line 305) 提供图标

---

## 2. 激活预览会话

点击小眼睛 → `UnitVAClipPreviewSession.Toggle(asset, clipIndex)` (line 299)

**`UnitVAClipPreviewSession`** (静态会话状态):
```
ActiveAsset              — 当前预览的 UnitVASO
ActiveClipIndex          — 当前预览的 clip 索引
CurrentFrame             — 当前帧号 (0-based)
IsPlaying                — 是否自动播放
PlaybackSpeed            — 播放速度倍率
Changed                  — 事件，通知所有订阅者刷新
```

`Open()` 方法：
1. 设置 ActiveAsset / ActiveClipIndex
2. CurrentFrame = 0, IsPlaying = false
3. 调用 `UnitVAClipPreviewWindow.ShowWindow()` 打开预览窗口
4. 调用 `NotifyChanged()` 通知 Inspector 刷新

---

## 3. Inspector 切换

`UnitVASOEditor.OnInspectorGUI` 第二行即检查：

```csharp
if (UnitVAClipPreviewSession.IsActive && UnitVAClipPreviewSession.ActiveAsset == unitVA)
{
    DrawDedicatedPreviewInspector(unitVA);  // 替换整个 Inspector
    return;
}
```

### `DrawDedicatedPreviewInspector` 内容

| 区域 | 方法 | 内容 |
|------|------|------|
| 工具栏 | inline | "Unit VA Clip Preview" + Close 按钮 |
| 元数据 | inline | Asset名、Clip名、State、帧数、顶点数、Loop、LockUntilComplete |
| 播放控制 | `DrawPreviewPlaybackControls` | Play/Pause、First、Last 按钮、Frame 滑条、Speed 滑条、Sample FPS |
| **帧网格** | `DrawPreviewFrameOrder` | 6列滚动网格，每个帧一个按钮，当前帧高亮蓝色，点击选帧 |

### 播放控制详情

- **Play/Pause**: 切换 `IsPlaying`，`UnitVAClipPreviewWindow.Update()` 按 `BakeSampleFps * PlaybackSpeed` 自动推进帧
- **First/Last**: 直接设置 CurrentFrame
- **Frame 滑条**: IntSlider 0..FrameCount-1
- **Speed 滑条**: 0.1x ~ 4x
- **Sample FPS**: 只读，显示当前资产烘焙帧率

---

## 4. 可视化预览窗口

**`UnitVAClipPreviewWindow`** (独立 EditorWindow)

### Update() — 自动播放驱动

```
delta = now - lastUpdateTime
frameAccumulator += delta * BakeSampleFps * PlaybackSpeed
wholeFrames = floor(frameAccumulator)
CurrentFrame += wholeFrames  (loop 时 wrap, 非 loop 时停在末帧)
```

### OnGUI() — 渲染

- `DrawToolbar`: 显示 asset名 / clip名 + Close 按钮
- `DrawPreview`: 使用 `PreviewRenderUtility` 渲染当前帧 mesh
  - 通过 `UnitVAPreviewMeshBuilder.TryBuildFrameMesh` 构建帧 mesh
  - 贴 `asset.BaseTexture`
  - 正交相机自动适配 clip bounds
  - 左下角显示 "Frame N / Total"

### 生命周期

- `OnEnable`: 订阅 `UnitVAClipPreviewSession.Changed`
- `OnDisable`: 取消订阅，释放 PreviewRenderUtility / Mesh / Material
- 两个窗口（PreviewWindow + Inspector）通过 `Changed` 事件同步刷新

---

## 5. 帧网格 (`DrawPreviewFrameOrder`)

```
Frame Order  ← 标题
┌──┬──┬──┬──┬──┬──┐
│0 │1 │2 │3 │4 │5 │  ← 每帧一个按钮 (42px)
├──┼──┼──┼──┼──┼──┤
│6 │7 │8 │9 │10│11│    当前帧 → 蓝色高亮
├──┼──┼──┼──┼──┼──┤    点击 → 设置 CurrentFrame + 暂停播放
│...                   滚动区域 minHeight=120px
└──┴──┴──┴──┴──┴──┘
```

**当前实现**: 纯帧号，无 tag 标记。

**待加**: 
- 有 tag 的帧按钮变色/加标记点
- 下方/侧边显示当前选中帧的 tag 编辑区
- tag 数据存储在 `UnitVAClip.FrameTags` 列表中

---

## 6. 关闭预览

- Preview Window Close 按钮 → `UnitVAClipPreviewSession.Close()` → `CloseIfOpen()`
- Inspector Close 按钮 → 同上
- 关闭时：`ActiveAsset` / `ActiveClipIndex` 清零，PreviewWindow 关闭，Inspector 恢复

---

## 7. 当前缺失

| 缺失功能 | 位置 |
|------|------|
| Tag 数据存储 | `UnitVAClip` 缺少 `FrameTags` 列表 |
| Tag 枚举定义 | 缺少 `UnitVAEventTag` |
| 帧网格 tag 视觉标记 | `DrawPreviewFrameOrder` 未读取 tag |
| Tag 编辑控件 | 预览 Inspector 未显示当前帧 tag 编辑 |
| 运行时 tag 跨越检测 | `AdvanceVAFrames` 未查 FrameTags |
| ECS 事件组件 | 缺少接收跨越 tag 的组件 |

---

## 8. 关键文件

| 文件 | 职责 |
|------|------|
| `UnitVASOEditor.cs` | 主 Inspector + 预览 Inspector (`DrawDedicatedPreviewInspector`) |
| `UnitVAClipPreviewWindow.cs` | 独立 EditorWindow，渲染当前帧 mesh 预览 |
| `UnitVAClipPreviewSession.cs` | 静态会话状态 (当前资产/clip/帧/播放) |
| `UnitVAPreviewMeshBuilder.cs` | 从 UnitVASO + clip + frameIndex 构建预览 mesh |
| `UnitVASO.cs` | 数据资产: `UnitVAClip` + `UnitVAFrame` |
