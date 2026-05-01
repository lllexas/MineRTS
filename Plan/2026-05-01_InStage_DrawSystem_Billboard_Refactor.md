# InStage DrawSystem Billboard Refactor Plan

## 日期
2026-05-01

## 背景

当前局内渲染链路是在敏捷迭代中分开长出来的：

- `DrawSystem` 负责 ECS 单位、建筑、传送带等主体绘制
- `TransportDrawSystem` 负责传送带物品、建筑端口飞行物品等非 ECS/附属物绘制
- `SpriteLib` 负责从 `Sprite` 生成 instancing 材质和 quad mesh
- `CameraController` 和 `GridSystem.GetMouseGridPos` 仍然强依赖 2D 正交相机

现在准备从纯 2D 正交表现转向“分层 2D + 透视相机 + billboard”的表现方式。上层 ECS、地图、寻路和存档数据暂时不应被 3D 化；主要改造点应集中在绘制系统。

核心判断：

- ECS 逻辑层仍使用 `Vector2` / `Vector2Int`
- 地图数据仍使用 `groundMap/gridMap/effectMap`
- 渲染层负责把 2D 逻辑坐标投射到 3D 表现空间
- ECS 绘制和非 ECS item 绘制应该共享同一套绘制服务

---

## 当前结构调查结论

### DrawSystem

文件：

```text
Assets/Scripts/InStage/System/DrawSystem.cs
```

当前职责：

- 遍历 `WholeComponent.entityCount`
- 读取 `CoreComponent/DrawComponent/MoveComponent/HealthComponent/WorkComponent`
- 按 `SpriteId` 分批
- 通过 `SpriteLib.GetMesh/GetMaterial` 获取资源
- 使用 `Graphics.RenderMeshInstanced` 绘制
- 使用固定 `zPos`、`renderQueue`、`rendererPriority` 区分层级

当前典型坐标：

```csharp
Vector3 pos = new Vector3(core.Position.x, core.Position.y + jumpOffset, zPos);
Quaternion rot = Quaternion.Euler(0, 0, angle);
Matrix4x4.TRS(pos, rot, scale);
```

问题：

- quad 仍躺在 XY 平面
- 固定 z 值只适合当前正交相机分层
- billboard 逻辑不存在
- 材质缓存、矩阵批处理逻辑和 `TransportDrawSystem` 重复

### TransportDrawSystem

文件：

```text
Assets/Scripts/InStage/System/TransportDrawSystem.cs
```

当前职责：

- 从 `TransportSystem.Instance.GetLines()` 获取传送带线和线上的 item
- 根据 item 在路径上的距离计算插值位置
- 遍历 `WorkComponent` 的端口任务，绘制飞行物品
- 按 `SpriteId` 分批并调用 `Graphics.RenderMeshInstanced`

当前典型坐标：

```csharp
Vector3 finalPos = new Vector3(pos.x, pos.y, finalZ);
Matrix4x4.TRS(finalPos, Quaternion.identity, scale);
```

问题：

- 和 `DrawSystem` 重复维护 batch、material cache、RenderParams
- 未来散落物品也适合走这条线，但当前命名和职责偏运输系统
- 同样依赖 XY 平面与固定 z 分层

### SpriteLib

文件：

```text
Assets/Scripts/InStage/SpriteLib.cs
```

当前职责：

- 保存 `unitSprites`
- 为每个 sprite 创建材质
- 为每个 sprite 创建 quad mesh

当前 mesh 是 XY 平面、中心 pivot：

```text
(-halfW, -halfH, 0)
( halfW, -halfH, 0)
(-halfW,  halfH, 0)
( halfW,  halfH, 0)
```

问题：

- 中心 pivot 对“站立广告牌”不理想，单位更需要 bottom pivot
- API 目前只有 `GetMesh(spriteId)`，没有 pivot mode
- 短期可以兼容，长期应支持中心/脚底 pivot

### CameraController / GridSystem

相关文件：

```text
Assets/Scripts/InStage/Controller/CameraController.cs
Assets/Scripts/InStage/System/GridSystem.cs
```

当前问题：

- `CameraController` 以 `orthographicSize` 为核心处理缩放和边界
- `GridSystem.GetMouseGridPos` 使用 `Camera.main.ScreenToWorldPoint`
- 透视相机下需要改为 screen ray 命中地面平面

---

## 设计目标

### 保持兼容

第一阶段不改以下数据结构：

```text
CoreComponent.Position: Vector2
MoveComponent.LogicalPosition: Vector2Int
GridSystem: Vector2 / Vector2Int
WholeComponent.groundMap/gridMap/effectMap
LevelMapData
TransportSystem 的 item 路径逻辑
```

也就是说，逻辑世界仍然是二维网格。

### 统一渲染入口

两个现有 draw system 不应各自维护 instancing 细节。目标结构：

```text
DrawSystem
  -> 只负责 ECS 数据遍历和动画意图

TransportDrawSystem / ItemDrawSystem
  -> 只负责 item 逻辑位置、运输插值、飞行插值

SpriteInstanceRenderService
  -> 负责 sprite instancing、材质缓存、RenderParams、提交绘制

InStageRenderSpace
  -> 负责 2D 逻辑坐标到 3D 表现坐标、billboard rotation、鼠标射线
```

### 世界轴约定

推荐采用 Unity 常见地面轴：

```text
逻辑 x -> 世界 X
逻辑 y -> 世界 Z
视觉高度 -> 世界 Y
```

示例：

```text
logic (10, 20)
-> ground world (10, 0, 20)
```

这样地面、墙、浮空层、广告牌高度都可以在 Y 轴表达。

---

## 目标架构

### InStageRenderSpace

建议新增：

```text
Assets/Scripts/InStage/Rendering/InStageRenderSpace.cs
```

职责：

```csharp
public static class InStageRenderSpace
{
    public static Vector3 LogicToGround(Vector2 logicPos);
    public static Vector3 LogicToWorld(Vector2 logicPos, float heightOffset);
    public static Quaternion GetBillboardRotation(Camera camera);
    public static Matrix4x4 MakeBillboardMatrix(
        Vector2 logicPos,
        Vector2 scale,
        float heightOffset,
        float verticalPivotOffset,
        Quaternion extraRotation);

    public static bool TryScreenToGround(Camera camera, Vector2 screenPos, out Vector2 logicPos);
}
```

第一版重点：

- 只处理 XZ 地面转换
- 只支持一个水平地面平面 `Y = 0`
- billboard rotation 面向当前主相机
- 鼠标拾取通过 `Plane(Vector3.up, Vector3.zero)` 完成

### SpriteInstanceRenderService

建议新增：

```text
Assets/Scripts/InStage/Rendering/SpriteInstanceRenderService.cs
```

职责：

- 按 `SpriteId + RenderBand` 收集矩阵
- 缓存材质副本
- 设置 `renderQueue`
- 设置 `rendererPriority`
- 统一调用 `Graphics.RenderMeshInstanced`

建议的渲染层语义：

```csharp
public enum InStageRenderBand
{
    GroundDecoration,
    Conveyor,
    Item,
    Unit,
    HealthBar,
    Overlay
}
```

初始映射可以保留原来的层级：

```text
Conveyor  -> queue 3000, priority 10
Item      -> queue 3005, priority 20
Unit      -> queue 3010, priority 30
HealthBar -> queue 3020, priority 50
Overlay   -> queue 3050, priority 60
```

### Sprite draw request

建议抽象为轻量 request：

```csharp
public struct SpriteDrawRequest
{
    public int SpriteId;
    public Vector2 LogicPosition;
    public Vector2 Scale;
    public float HeightOffset;
    public float VerticalPivotOffset;
    public InStageRenderBand Band;
    public Quaternion ExtraRotation;
}
```

第一版可以不公开复杂 API，只提供：

```csharp
public void Clear();
public void Add(SpriteDrawRequest request);
public void Flush(Camera camera);
```

---

## SpriteLib 改造策略

### 短期

保留现有 API：

```csharp
public Material GetMaterial(int spriteId)
public Mesh GetMesh(int spriteId)
```

原因：

- `DrawSystem`
- `TransportDrawSystem`
- UI 预览
- 建筑 ghost
- 其他临时调用

都可能依赖当前接口。第一阶段不应扩大破坏范围。

### 短期 mesh 方案

当前 XY quad 可以继续用于 billboard：

```text
local X = 横向宽度
local Y = 竖向高度
local Z = 0
```

只要最终 TRS 的 rotation 是 billboard rotation，它就会站起来面对相机。

### 长期 pivot 方案

增加 pivot mode：

```csharp
public enum SpriteMeshPivot
{
    Center,
    Bottom
}

public Mesh GetMesh(int spriteId, SpriteMeshPivot pivot)
```

其中 bottom pivot 顶点应类似：

```text
(-halfW, 0, 0)
( halfW, 0, 0)
(-halfW, height, 0)
( halfW, height, 0)
```

收益：

- 单位脚底落在地面更自然
- 建筑广告牌可以准确压在格子中心/底部
- 血条和头顶 UI 的高度更容易计算

---

## 分阶段实施计划

### Phase 1：新增渲染空间服务

新增：

```text
Assets/Scripts/InStage/Rendering/InStageRenderSpace.cs
```

完成：

- `LogicToWorld`
- `GetBillboardRotation`
- `MakeBillboardMatrix`
- `TryScreenToGround`

验收：

- 不接入业务系统
- 能在编辑器或调试代码中确认 `Vector2(x,y)` 会映射到 `Vector3(x,0,y)`
- 鼠标射线能落到 XZ 地面并返回逻辑坐标

### Phase 2：新增 SpriteInstanceRenderService

新增：

```text
Assets/Scripts/InStage/Rendering/SpriteInstanceRenderService.cs
```

完成：

- batch 收集
- material cache
- render band queue/priority
- `Flush(Camera camera)`

验收：

- 先不替换原系统
- 可以用测试调用绘制一个 sprite billboard
- 视觉结果与原 instancing shader 兼容

### Phase 3：迁移 DrawSystem

修改：

```text
Assets/Scripts/InStage/System/DrawSystem.cs
```

目标：

- 保留 ECS 遍历逻辑
- 保留跳跃、拉伸、血条显示判断
- 移除或下沉材质缓存、矩阵字典、DrawBatch 细节
- 将绘制请求提交给 `SpriteInstanceRenderService`

注意：

- 第一版可以保留 `UseDebugSpriteRenderers`
- 正交相机下尽量保持视觉接近当前版本
- 单位 rotation 先可以弱化，优先保证 billboard 成立

验收：

- ECS 单位、建筑、传送带仍可见
- GPU instancing 仍工作
- 没有数据层迁移

### Phase 4：迁移 TransportDrawSystem

修改：

```text
Assets/Scripts/InStage/System/TransportDrawSystem.cs
```

目标：

- 保留运输 item 的位置计算
- 保留飞行任务插值
- 移除本地 `_itemMatrices`
- 移除本地 `_itemMatCache`
- 统一提交 `SpriteDrawRequest`

高度建议：

```text
Conveyor item: heightOffset = 0.08
Dropped item:  heightOffset = 0.05
Flying item:   heightOffset = arcHeight / task interpolation
```

验收：

- 传送带物品仍可见
- 端口飞行物品仍可见
- item 与单位使用同一 billboard 和 batch 服务

### Phase 5：透视相机输入改造

修改：

```text
Assets/Scripts/InStage/Controller/CameraController.cs
Assets/Scripts/InStage/System/GridSystem.cs
```

目标：

- `CameraController` 支持 perspective mode
- 地面拖拽、推屏、缩放改为适配透视相机
- `GridSystem.GetMouseGridPos` 改为 ray-plane 命中

需要替换的思路：

```text
旧：ScreenToWorldPoint + orthographicSize
新：ScreenPointToRay + XZ ground plane
```

验收：

- 鼠标能正确选中格子
- 建筑预览位置正确
- 镜头移动不破坏边界约束

### Phase 6：补齐外延系统

后续逐个检查：

```text
BuildingController ghost
OverlayPowerSystem
PathPreviewSystem
HealthBar
selection / inspector hit test
BigMap 是否共用相机或独立相机
```

这些不应抢在 Phase 1-4 前面做。

---

## 数据层兼容性结论

第一阶段不需要改：

```text
LevelMapData
WholeComponent
CoreComponent.Position
MoveComponent
GridSystem 的格子数据
WorldFactory
Tilemap editor / JSON bake chain
SaveManager
```

需要注意：

- `TransportSystem` 中的 `WorldPos/StartPos/EndPos` 当前可能是 `Vector2` 或 `Vector3` 混用，第一阶段统一按“逻辑 XY 坐标”解释
- 如果后续真的引入高度地形，再考虑增加 terrain height 查询，而不是把 `core.Position` 改成 `Vector3`
- 地形高度、墙体浮空、分层 tile 表现，应先作为渲染层参数或 map visual metadata，而不是直接侵入 ECS 逻辑层

---

## 风险点

### 透明排序

当前 sprite shader：

```text
Blend SrcAlpha OneMinusSrcAlpha
ZWrite On
clip(alpha - 0.1)
```

透视 billboard 下，透明排序更敏感。`ZWrite On + alpha clip` 对硬边 sprite 可能可以接受，但半透明边缘、血条、overlay 需要单独检查。

### Billboard 旋转和单位朝向

当前单位用 `core.Rotation` 做 Z 轴旋转。billboard 后需要重新定义：

```text
面向相机 = billboard rotation
单位朝向 = sprite 变体 / 水平翻转 / shader 参数 / 轻微局部旋转
```

第一阶段可以优先保证 billboard，不强求方向表现完全正确。

### Mesh pivot

中心 pivot 会导致单位半截插入地面或浮起。短期可以通过 `VerticalPivotOffset` 修正，长期应在 `SpriteLib` 支持 bottom pivot mesh。

### 鼠标拾取

透视相机下所有点击、框选、建筑预览都依赖 ray-plane。必须集中到 `InStageRenderSpace`，避免各系统各写一套。

---

## 推荐第一批提交范围

第一批不切透视相机，只做内部重构：

```text
新增 InStageRenderSpace
新增 SpriteInstanceRenderService
DrawSystem 接入服务
TransportDrawSystem 接入服务
正交相机下保持视觉基本不变
```

第二批再切：

```text
Perspective Camera
Billboard rotation
GridSystem mouse picking
Building ghost 修正
Overlay 修正
```

这样可以把“抽象统一”和“相机表现变化”分开验证，降低回归风险。

