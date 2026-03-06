# 大地图重构指南 - GPU 实例化版本

## 📁 文件结构

### 新增文件
```
Assets/Scripts/OutStage/BigMap/
├── NodeController.cs          // 节点控制器（MonoBehaviour）
├── BigMapEdgeRenderer.cs      // 连线 GPU 实例化渲染器
└── BigMapRuntimeRenderer.cs   // 运行时渲染器（重构版）
```

### 废弃文件（已标记 Obsolete）
```
Assets/Scripts/OutStage/BigMap/
├── RuntimeNodeElement.cs      // UI Toolkit 版本节点
└── RuntimeMapContainer.cs     // UI Toolkit 版本容器
```

### 已删除文件
```
Assets/Scripts/OutStage/BigMap/
└── EdgeController.cs          // LineRenderer 版本连线（已删除）
```

---

## 🎯 架构说明

### 渲染架构
```
┌─────────────────────────────────────────────────────────┐
│                   BigMapRuntimeRenderer                  │
│  职责：实例化节点 Prefab，管理节点位置映射表              │
├─────────────────────────────────────────────────────────┤
│  Node Prefab (GameObject + SpriteRenderer)              │
│  ├── UpperArc (SpriteRenderer) - 上弓形                 │
│  ├── LowerArc (SpriteRenderer) - 下弓形                 │
│  ├── Plaque (SpriteRenderer) - 匾额背景                 │
│  └── NameText (TextMeshPro) - 节点名称                  │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│                   BigMapEdgeRenderer                     │
│  职责：使用 Graphics.RenderMeshInstanced 批量渲染连线    │
│  架构：与 OverlayDrawSystem 相同的 GPU 实例化方案         │
├─────────────────────────────────────────────────────────┤
│  连线 Mesh: Quad (Pivot 在底部中心)                      │
│  材质：Custom/Effects/DashedLineInstancedPerInstance    │
│  绘制：Graphics.RenderMeshInstanced()                   │
└─────────────────────────────────────────────────────────┘
```

---

## 📦 Prefab 准备清单

### 1. 节点 Prefab (Node Prefab)

#### 层级结构
```
NodePrefab (GameObject)
├── UpperArc (SpriteRenderer)     ← 上弓形
├── LowerArc (SpriteRenderer)     ← 下弓形
├── Plaque (SpriteRenderer)       ← 匾额背景
└── NameText (TextMeshPro)        ← 节点名称
```

#### 组件配置
| GameObject | 组件 | 说明 |
|-----------|------|------|
| 根对象 | `NodeController` | 节点控制器脚本 |
| 根对象 | `CircleCollider2D` | 用于鼠标交互（Radius: 0.5） |
| UpperArc | `SpriteRenderer` | 圆形 Sprite，深灰藏青色 |
| LowerArc | `SpriteRenderer` | 圆形 Sprite，深灰藏青色 |
| Plaque | `SpriteRenderer` | 矩形 Sprite，近白色 |
| NameText | `TextMeshPro` | 节点名称，近黑色文字 |

#### 尺寸参考
| 元素 | Position | Scale |
|------|----------|-------|
| UpperArc | (0, 0.5, 0) | (0.8, 0.8, 1) |
| LowerArc | (0, -0.5, 0) | (0.8, 0.8, 1) |
| Plaque | (0, 0, 0) | (1.2, 0.3, 1) |
| NameText | (0, 0, -0.1) | (0.15, 0.15, 1) |

---

### 2. 连线 Mesh (Line Mesh)

#### 创建方式
**不需要 Prefab！** 连线使用 GPU 实例化渲染，Mesh 在 `BigMapEdgeRenderer` 中自动创建。

#### Mesh 规格
```
名称：BigMapLineQuad
顶点：4 个（Quad，Pivot 在底部中心）
UV: 标准 [0,0] 到 [1,1]
三角形：2 个三角形组成 Quad
```

#### 代码创建（自动）
```csharp
// BigMapEdgeRenderer.Awake() 中自动创建
Mesh mesh = new Mesh();
mesh.name = "BigMapLineQuad";
// ... 顶点、UV、三角形设置
```

---

## 🔧 场景配置

### 1. BigMapRuntimeRenderer 配置

```
GameObject: BigMapRenderer
└── 组件：BigMapRuntimeRenderer
    ├── Map Json File: [BigMapData.json]
    ├── Node Prefab: [NodePrefab]
    └── Map Root: [可留空，自动创建]
```

### 2. BigMapEdgeRenderer 配置

```
GameObject: BigMapEdgeRenderer (Singleton)
└── 组件：BigMapEdgeRenderer
    ├── Line Mesh: [可留空，自动创建]
    ├── Dashed Line Material: [虚线材质]
    ├── Line Width: 0.15
    ├── Line Color: (0.4, 0.6, 0.7, 0.8)
    └── Texture Scale: 2.0
```

---

## 🎨 Shader 需求

### 虚线 Shader（必需）

**Shader 路径**: `Custom/Effects/DashedLineInstancedPerInstance`

如果找不到此 Shader，需要创建或修改 `BigMapEdgeRenderer` 使用其他可用 Shader。

#### Shader 属性需求
```hlsl
Properties
{
    _BaseColor ("Base Color", Vector) = (1,1,1,1)
    _LineLength ("Line Length", Float) = 1.0
    _DashRatio ("Dash Ratio", Range(0,1)) = 0.5
    _TextureScale ("Texture Scale", Float) = 2.0
}
```

#### 支持 GPU 实例化
```hlsl
UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _LineLength)
UNITY_INSTANCING_BUFFER_END(Props)
```

---

## 📝 使用示例

### 加载地图
```csharp
var renderer = BigMapRuntimeRenderer.Instance;
renderer.LoadMapData(jsonText);
renderer.SetNodeClickCallback(OnNodeClicked);
```

### 节点点击回调
```csharp
void OnNodeClicked(string stageId, string displayName)
{
    Debug.Log($"点击了关卡：{displayName}");
    GameFlowController.Instance.EnterStage(stageId);
}
```

### 设置节点选中
```csharp
renderer.SetNodeSelected(nodeId, true);
```

---

## ⚙️ 坐标系说明

### 新系统（GameObject + GPU 实例化）
```
JSON Position → Unity 世界坐标 → 摄像机渲染 → 屏幕坐标
```

**优势**：
- 无需 PPU 计算
- 无需每帧更新 Translate/Scale
- 连线使用 GPU 实例化，性能优秀
- 支持大量节点和连线

---

## ⚠️ 注意事项

### 1. NodeController 依赖
- 确保 Prefab 上所有 SpriteRenderer 和 TextMeshPro 已正确赋值
- CircleCollider2D 用于鼠标交互（必需）

### 2. BigMapEdgeRenderer
- 单例模式，场景中只能有一个实例
- 每帧 LateUpdate 自动渲染连线
- 需要虚线 Shader 支持 GPU 实例化

### 3. 摄像机设置
- 使用正交摄像机（Orthographic）
- Orthographic Size 控制缩放
- Position 控制平移

### 4. 性能优化
- 节点使用 SpriteRenderer，支持 GPU 实例化
- 连线使用 Graphics.RenderMeshInstanced，单 DrawCall
- 大量节点时考虑对象池

---

## 🚀 下一步

1. **创建 Node Prefab**
   - 按上述层级结构创建
   - 配置所有 SpriteRenderer
   - 添加 CircleCollider2D

2. **准备 Shader**
   - 确认 `Custom/Effects/DashedLineInstancedPerInstance` 可用
   - 或修改 BigMapEdgeRenderer 使用其他 Shader

3. **配置场景**
   - 创建 BigMapRenderer GameObject
   - 添加 BigMapRuntimeRenderer 组件
   - 拖拽 Node Prefab

4. **测试运行**
   - 加载 BigMapData.json
   - 验证节点和连线显示
   - 测试鼠标交互

---

## 📊 性能对比

| 方案 | 节点渲染 | 连线渲染 | Draw Call (100 节点 +200 连线) |
|------|---------|---------|-------------------------------|
| UI Toolkit | Painter2D | Painter2D | ~10-20 |
| GameObject + LineRenderer | SpriteRenderer | LineRenderer | ~200+ |
| **GameObject + GPU 实例化** | **SpriteRenderer** | **Graphics.RenderMeshInstanced** | **~2-5** ✅ |

---

## 🐛 常见问题

**Q: 连线不显示？**
- 检查虚线 Shader 是否正确
- 检查材质是否支持 GPU 实例化
- 确认节点位置已正确传递到 BigMapEdgeRenderer

**Q: 节点点击没反应？**
- 确认节点上有 CircleCollider2D
- 确认摄像机 Culling Mask 包含节点层

**Q: 性能问题？**
- 使用 Frame Debugger 检查 Draw Call
- 确认 GPU 实例化已启用（查看渲染统计）
