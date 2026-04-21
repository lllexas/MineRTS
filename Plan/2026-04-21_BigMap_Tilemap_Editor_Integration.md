# BigMap ↔ Tilemap 关卡编辑器一站式集成方案

## 日期
2026-04-21

## 背景与痛点

当前 BigMap、Tilemap、关卡 JSON 三者之间完全割裂，策划工作流存在明显断点：

### 痛点 1：BigMap 编辑器只管拓扑，不管关卡

`BigMapEditorWindow` 和 `NodeInspectorPanel` 中，`BigMapNodeData` 只有：
- `StageID`（节点唯一标识）
- `DisplayName`（显示名称）
- `Position`（画布位置）
- `NodeType`（字符串，实际无人使用）
- `ExtraData`（纯文本，无结构化语义）

**没有任何字段声明"这个节点对应哪个关卡模板"**。策划只能靠记忆或 `ExtraData` 手写备注。

### 痛点 2：Tilemap 编辑与 BigMap 节点零关联

Tilemap 在 `SampleScene` 中手工编辑，使用 Unity 原生的 Tile Palette 工具。但：
- 哪个 Tilemap 对应哪个 BigMap 节点？**没有记录**
- 关卡 JSON 导出后放到哪里？**靠手动操作**
- 文件名必须和 `StageID` 一致（因为 `WorldFactory.CreateNewWorldFromLevelID` 硬编码 `Resources/Levels/{levelID}.json`），**没有任何校验机制**

### 痛点 3："双联查表"的隐式约定

运行时链路：
```
BigMapNodeData.StageID
  → WorldFactory 按 StageID 去 Resources/Levels/{StageID}.json 硬查
  → 若查不到 → NullReference → 崩溃
```

这个**隐式文件名约定**没有任何地方显式声明，策划改个 StageID 忘了同步改 JSON 文件名就炸。

### 痛点 4：Tile 映射配置孤岛

`TilemapSyncManager` 中有 `TileIDMapping`（`TileBase → int tileID`）配置，但：
- 只在**运行时**生效（依赖 `EntitySystem` + `GridSystem`）
- 编辑器工作流中**无法复用**
- 策划在 Tile Palette 里选的 Tile 和最终 groundMap 的 int ID 之间的映射，**没有统一的配置源**

---

## 目标

让 **BigMap 编辑器成为关卡配置的总入口**。策划的工作流变成：

1. 在 BigMap 编辑器中创建节点
2. 选中节点 → 属性面板显示关卡模板状态
3. 点击【编辑关卡】→ 自动进入专用 Tilemap 编辑场景
4. 用 Tile Palette 画地图
5. 点击【保存并返回】→ 自动生成 JSON、自动关联回 StageID、自动返回 BigMap 编辑器
6. 节点属性面板显示"模板已存在"

---

## 设计思路：三层分离 + 桥梁传递

```
┌─────────────────────────────────────────────────────────────────┐
│  表现层（Editor UI）                                               │
│  ┌─────────────────┐     ┌─────────────────────────────────────┐│
│  │ BigMapEditorWindow│     │ TilemapLevelEditor (专用场景)        ││
│  │  - 节点拓扑画布    │◄───►│  - Grid + Tilemap                   ││
│  │  - 属性面板       │     │  - Tile Palette                     ││
│  │  - [编辑关卡] 按钮 │     │  - [保存并返回] 按钮                 ││
│  └─────────────────┘     └─────────────────────────────────────┘│
│           ▲                              │                       │
│           │                              │                       │
│           │     EditorSessionBridge      │                       │
│           │     (EditorPrefs 静态桥梁)    │                       │
│           │                              ▼                       │
│           └──────────────────────────────────────────────────────│
├─────────────────────────────────────────────────────────────────┤
│  数据层（运行时与编辑器共享）                                        │
│  ┌─────────────────────┐    ┌──────────────────────────────────┐│
│  │ BigMapSaveData      │    │ LevelMapData                      ││
│  │   Nodes[{StageID,   │    │   {levelId, width, height,        ││
│  │          DisplayName,│    │    originX, originY,              ││
│  │          Position,   │    │    groundMap[], gridMap[],        ││
│  │          LevelTemplateRef,│  effectMap[]}                    ││
│  │          ...}]       │    └──────────────────────────────────┘│
│  │   Edges[{...}]      │                                       │
│  └─────────────────────┘                                       │
├─────────────────────────────────────────────────────────────────┤
│  运行时层（游戏内）                                                │
│  ┌─────────────┐    ┌─────────────────────┐    ┌───────────────┐│
│  │ BigMapManager│───►│ GameFlowController   │───►│ EntitySystem   ││
│  │   (显示节点) │    │   (切换状态)         │    │   (LoadStage)  ││
│  └─────────────┘    └─────────────────────┘    └───────┬───────┘│
│                                                        │        │
│                                               ┌────────▼──────┐│
│                                               │ WorldFactory   ││
│                                               │  Resources/    ││
│                                               │  Levels/{ref}  ││
│                                               └───────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

**核心原则**：
- BigMap 编辑器只管**总入口**和**关联关系**
- TilemapLevelEditor 只管**画地图**和**导出 JSON**
- 两者通过 `EditorSessionBridge`（静态类 + EditorPrefs）传递切换状态
- 运行时逻辑（`WorldFactory`、`EntitySystem`）**不需要改**，只需把隐式约定变成显式字段

---

## 具体方案

### 1. BigMapNodeData 扩展（显式关联）

```csharp
public class BigMapNodeData
{
    public string StageID;           // 节点唯一标识（不变）
    public string DisplayName;
    public SerializableVector2 Position;
    public string NodeType;
    public string ExtraData;

    // 【新增】关卡模板引用
    public string LevelTemplateRef = "";
    // 空字符串时运行时回退到 StageID（向后兼容）
}
```

运行时 `WorldFactory` 的加载逻辑改为：
```csharp
string levelRef = nodeData.LevelTemplateRef;
if (string.IsNullOrEmpty(levelRef)) levelRef = nodeData.StageID;
TextAsset jsonAsset = Resources.Load<TextAsset>($"Levels/{levelRef}");
```

### 2. EditorSessionBridge（场景切换桥梁）

Unity Editor 的 EditorWindow 和 Scene 之间没有直接的 API 可以传对象。用 `EditorPrefs` 做桥梁：

```csharp
public static class EditorSessionBridge
{
    private const string PREFIX = "MineRTS_TilemapEditor_";

    public static void SetSession(string stageID, string levelRef, string returnScene, string bigMapPath)
    {
        EditorPrefs.SetString(PREFIX + "StageID", stageID);
        EditorPrefs.SetString(PREFIX + "LevelRef", levelRef);
        EditorPrefs.SetString(PREFIX + "ReturnScene", returnScene);
        EditorPrefs.SetString(PREFIX + "BigMapPath", bigMapPath);
        EditorPrefs.SetBool(PREFIX + "IsReturning", false);
    }

    public static void MarkReturning() => EditorPrefs.SetBool(PREFIX + "IsReturning", true);

    public static (string stageID, string levelRef, string returnScene, string bigMapPath) GetSession()
    {
        // 读取并返回各字段
    }

    public static void ClearSession() { /* 清理所有 EditorPrefs */ }
}
```

### 3. TilemapLevelEditor（专用编辑场景 MonoBehaviour）

**职责**：
- 从 `EditorSessionBridge` 读取目标 StageID 和 LevelTemplateRef
- 尝试加载 `Resources/Levels/{ref}.json` → 填充 Tilemap
- 若不存在 → 初始化空白地图（默认 64x64，原点 -32, -32）
- 扫描 Tilemap → 生成 `LevelMapData` → 保存 JSON
- 提供 Inspector 按钮：【保存】、【保存并返回 BigMap】

**重要**：不依赖 `EntitySystem`、`GridSystem`、`TilemapSyncManager`。自己维护 `TileBase → int ID` 映射表（可以和 `TilemapSyncManager` 共享同一个配置源，比如一个 ScriptableObject）。

Inspector 布局：
```
[当前编辑关卡]
  StageID: Level_Test
  LevelTemplateRef: Level_Test
  状态: 🟢 已加载现有模板

[地图参数]
  Width: 64
  Height: 64
  Origin X: -32
  Origin Y: -32

[Tile 映射表]
  [Reorderable List]
    - Tile: GrassTile → ID: 1
    - Tile: WaterTile → ID: 2
    - Tile: RockTile  → ID: 3
    ...

[操作]
  [保存]                [保存并返回 BigMap]
```

### 4. NodeInspectorPanel 扩展（属性面板新增"关卡模板"区域）

在现有属性面板中，"附加数据"和"连线信息"之间插入：

```
────────── 关卡模板 ──────────
模板引用: [ Level_Test        ]
状态: 🟢 模板已存在 (Resources/Levels/Level_Test.json)
        [🎨 编辑关卡]  ← 大按钮
─────────────────────────────
```

- `LevelTemplateRef` 文本字段（可编辑）
- 自动检查 `Resources/Levels/{ref}.json` 是否存在
- 【编辑关卡】按钮：调用 `BigMapEditorWindow.OpenTilemapEditor(node)`

### 5. BigMapEditorWindow 扩展

新增方法：
```csharp
public void OpenTilemapEditor(BigMapNodeData node)
{
    // 1. 先保存当前 BigMap
    AutoSave();

    // 2. 设置桥梁状态
    string levelRef = string.IsNullOrEmpty(node.LevelTemplateRef)
        ? node.StageID : node.LevelTemplateRef;
    EditorSessionBridge.SetSession(
        node.StageID,
        levelRef,
        EditorSceneManager.GetActiveScene().path,
        _currentFilePath
    );

    // 3. 打开 Tilemap 编辑场景
    EditorSceneManager.OpenScene("Assets/Scenes/TilemapLevelEditor.unity");
}
```

返回检测（`OnEnable` / `OnFocus`）：
```csharp
if (EditorSessionBridge.IsReturning)
{
    EditorSessionBridge.ClearReturning();
    // 重新加载 BigMap JSON
    // 刷新选中节点的状态标签
    _inspectorPanel?.RefreshTemplateStatus();
}
```

---

## 需要新建的文件

| 文件 | 类型 | 职责 |
|------|------|------|
| `Assets/Scenes/TilemapLevelEditor.unity` | Scene | 专用编辑场景：Grid + Tilemap + Camera |
| `Assets/Editor/TilemapLevelEditor/TilemapLevelEditor.cs` | Editor MonoBehaviour | 加载/保存 JSON、扫描 Tilemap、返回 BigMap |
| `Assets/Editor/BigMapEditor/EditorSessionBridge.cs` | 静态类 | EditorPrefs 桥梁，跨场景传递状态 |

## 需要修改的文件

| 文件 | 修改内容 |
|------|----------|
| `Assets/Scripts/OutStage/BigMap/BigMapSaveData.cs` | `BigMapNodeData` 新增 `LevelTemplateRef` 字段 |
| `Assets/Editor/BigMapEditor/NodeInspectorPanel.cs` | 新增"关卡模板"区域（Ref 字段、状态标签、编辑按钮） |
| `Assets/Editor/BigMapEditor/BigMapEditorWindow.cs` | 新增 `OpenTilemapEditor()`、工具栏按钮、返回检测 |
| `Assets/Scripts/OutStage/LevelMap/WorldFactory.cs` | 加载逻辑优先使用 `LevelTemplateRef`，回退到 `StageID` |

---

## 关键设计决策

### Q1: 是否允许多个 BigMap 节点共享同一个关卡模板？
**答：是**。`LevelTemplateRef` 和 `StageID` 解耦，允许不同节点指向同一个 JSON。这样策划可以复用关卡模板（比如"教程关"模板被多个新手节点引用）。

### Q2: Tile 映射表配置放在哪里？
**答：建议新建一个 `TileMappingConfig` ScriptableObject**（`Assets/Settings/TileMappingConfig.asset`），被 `TilemapSyncManager`（运行时）和 `TilemapLevelEditor`（编辑器）共同引用。这样策划只配置一次。

### Q3: gridMap 和 effectMap 怎么处理？
**答：第一阶段只处理 groundMap**。Tilemap 编辑场景里只有一个 `GroundTilemap`。gridMap（建筑占据）和 effectMap（装饰）留空或填 0。后续版本可增加多层 Tilemap。

### Q4: 地图尺寸和原点在哪里设置？
**答：在 TilemapLevelEditor 的 Inspector 里设置**。默认值 64x64、原点 -32,-32。保存时写入 `LevelMapData`。加载时从 JSON 恢复并调整 Tilemap 边界。

### Q5: 场景切换时 BigMap 的未保存更改怎么办？
**答：点击【编辑关卡】时自动保存 BigMap JSON**（调用现有的 `SaveData()` 逻辑）。Unity 场景本身的修改需要提醒用户保存。

---

## 端到端验证步骤

1. 打开 BigMap 编辑器（`Tools/猫娘助手/BigMapNet拓扑编辑器`）
2. 创建节点，StageID = `"Level_Test"`
3. 选中节点，属性面板显示：
   - 关卡模板：`Level_Test`（自动同步 StageID）
   - 状态：⚪ 尚未创建
4. 点击【编辑关卡】
5. 自动切换到 `TilemapLevelEditor` 场景，Tilemap 为空
6. 用 Tile Palette 画几笔地形（比如草地、水域）
7. 设置地图尺寸为 32x32，原点 (-16, -16)
8. 点击【保存并返回 BigMap】
9. 检查 `Assets/Resources/Levels/Level_Test.json` 已生成
10. 自动切回 BigMap 编辑器场景
11. 选中同一节点，状态显示：🟢 模板已存在
12. 运行游戏，点击该节点 → 进入关卡 → 验证地形正确加载

---

## 备注

- 运行时逻辑（`WorldFactory`、`EntitySystem.LoadStage`）的改动极小，只是把隐式约定变成显式字段。
- 这套方案的核心收益不是"减少代码量"，而是**把隐式约定变成显式配置，消灭策划的记忆负担**。
- MonoBehaviour 管场景生命周期是合理的——场景切换、Tilemap 渲染、Inspector 交互天然就是 MonoBehaviour 的领域。
