# BigMap ↔ Tilemap 关卡编辑器集成现状

## 日期
2026-04-22

## 当前结论

这条链路已经走通，且最终实现方案与最初设想有几处关键收敛：

- 不再使用 `LevelTemplateRef`
- `StageID` 同时作为：
  - BigMap 节点唯一标识
  - 关卡 JSON 文件名
  - 运行时首次加载入口
  - 后续存档身份
- Tile 映射改为统一的 `TileMappingConfig : ScriptableObject`
- BigMap 进入 Tilemap 编辑器时，会缓存当前 scene setup
- 保存并返回时，恢复原 scene 布局、恢复 active scene、尽量拉回 BigMap 编辑器窗口

换句话说，当前版本不是“模板引用系统”，而是“BigMap 驱动的 StageID 直连关卡编辑工作流”。

---

## 最终设计

### 1. StageID 即关卡身份

当前规则很简单：

```text
BigMap 节点 ID = StageID
关卡 JSON = Assets/Resources/Levels/{StageID}.json
运行时首次加载 = Resources/Levels/{StageID}
后续存档身份 = StageID
```

这样做的原因：

- 项目当前的数据生命周期里，静态 JSON 只参与首次初始化
- 初始化完成后，数据跟着节点/存档走，而不是跟着“模板引用”走
- 因此 `LevelTemplateRef` 没有足够收益，反而增加理解和维护成本

### 2. TileMappingConfig 统一三端映射

当前使用统一的 `TileMappingConfig`：

- `TilemapSyncManager` 使用它做运行时 Tile ↔ ID 映射
- `LevelBakerWindow` 使用它烘焙 Tilemap → JSON
- `TilemapLevelEditor` 使用它做 JSON → Tilemap 回填，以及保存时的 Tile → ID 解析

这份配置是显式 Inspector 引用，不走固定路径，不自动创建。

### 3. SceneSetup 缓存恢复

从 BigMap 进入 Tilemap 编辑器时，不再只记录一个返回场景路径，而是缓存整个 `SceneSetup[]`。

返回时调用 `EditorSceneManager.RestoreSceneManagerSetup(...)`，恢复：

- 哪些 scene 已打开
- 哪些 scene 处于 loaded / unloaded
- 哪个 scene 是 active

这保证了多场景编辑工作流不会被 Tilemap 编辑器破坏。

---

## 已实现效果

### BigMap 侧

在 `Tools/猫娘助手/BigMapNet拓扑编辑器` 中：

- 节点属性面板会显示当前节点对应关卡文件状态
- 状态检查目标为 `Assets/Resources/Levels/{StageID}.json`
- 点击“编辑关卡”后：
  - 自动保存当前 BigMap JSON
  - 缓存当前 scene setup
  - 打开 `Assets/Scenes/TilemapLevelEditor.unity`
  - 主动把当前节点 `StageID` 注入 `TilemapLevelEditor`
  - 立即加载对应的 JSON

### Tilemap 编辑器侧

`TilemapLevelEditor` 当前支持：

- 打开已有 `StageID.json` 时自动回填 `GroundTilemap`
- 若文件不存在，初始化为空白地图
- 直接把 Tilemap 保存成 `Assets/Resources/Levels/{StageID}.json`
- Inspector 中提供：
  - `保存`
  - `保存并返回 BigMap`
  - `重新读取当前模板`

其中：

- `保存并返回 BigMap` 是主按钮，已做淡青色强调
- 返回后恢复原 scene 排布
- 若 BigMap 编辑器窗口仍开着，会尝试重新聚焦

### 运行时侧

运行时已经回到简单稳定的模式：

- `EntitySystem.LoadStage(stageID)`
- `WorldFactory.CreateNewWorldFromLevelID(stageID)`
- `Resources.Load<TextAsset>($"Levels/{stageID}")`

也就是说，运行时不再依赖额外解析层。

---

## 关键文件

### 已新增

- `Assets/Scripts/InStage/TileMappingConfig.cs`

### 已修改

- `Assets/Editor/BigMapEditor/BigMapEditorWindow.cs`
- `Assets/Editor/BigMapEditor/NodeInspectorPanel.cs`
- `Assets/Scripts/InStage/System/EntitySystem.cs`
- `Assets/Scripts/InStage/TilemapSyncManager.cs`
- `Assets/Scripts/OutStage/LevelMap/LevelBakerWindow.cs`
- `Assets/Scripts/OutStage/BigMap/BigMapSaveData.cs`

### 已存在并被正式使用

- `Assets/Scenes/TilemapLevelEditor.unity`
- `Assets/Settings/TileMappingConfig.asset`

---

## 当前工作流

### 关卡编辑

1. 打开 BigMap 编辑器
2. 读取 BigMap JSON
3. 选中一个节点，确保它的 `StageID` 正确
4. 点击“编辑关卡”
5. 进入 `TilemapLevelEditor`
6. 若 `Assets/Resources/Levels/{StageID}.json` 已存在，会自动回填到 Tilemap
7. 在 `GroundTilemap` 上继续编辑
8. 点击“保存”或“保存并返回 BigMap”

### 返回 BigMap

点击“保存并返回 BigMap”后：

1. 保存当前 `StageID.json`
2. 标记返回状态
3. 恢复之前缓存的 scene setup
4. 恢复原 active scene
5. BigMap 编辑器窗口若仍存在，则尝试聚焦
6. BigMap 编辑器重新读取 JSON 并刷新当前节点状态

---

## 当前前提条件

以下前提必须满足，否则“已有关卡自动回填到 Tilemap”不会正常工作：

- `TilemapLevelEditor` 组件已显式引用正确的 `TileMappingConfig`
- `TilemapSyncManager` 已显式引用同一份 `TileMappingConfig`
- `LevelBakerWindow` 已显式引用同一份 `TileMappingConfig`
- `Level_Test.json` 中使用的 `tileID` 与该 `TileMappingConfig` 中的配置一致

当前仓库里已有一份可用配置：

- `Assets/Settings/TileMappingConfig.asset`

其中已写入旧 `SampleScene` 中的 6 条映射：

- `1`
- `2`
- `100`
- `101`
- `102`
- `103`

---

## 当前实际验证结果

已确认以下行为成立：

- BigMap 中 `StageID = Level_Test` 的节点，可以正确定位到 `Assets/Resources/Levels/Level_Test.json`
- `Level_Test.json` 本身有有效内容，不是空文件
- JSON 中使用的 `tileID` 与当前 `TileMappingConfig` 能对上
- BigMap 打开 Tilemap 编辑器时，已改为主动注入当前 `StageID`，不再依赖场景里残留的旧序列化值
- “保存并返回 BigMap” 已切换为恢复完整 scene setup，而不是简单 reopen 场景
- 当前主链路已被确认“走通”

---

## 已放弃的旧方案

以下方案已明确不采用：

- `LevelTemplateRef`
- 固定路径自动查找 `TileMappingConfig`
- 自动创建默认 `TileMappingConfig`
- 一次性 YAML 迁移菜单长期保留在工具栏中

原因都是一样的：当前项目阶段更需要简单、显式、稳定的工作流，而不是额外的抽象层。

---

## 后续可选优化

这些不是当前主链路必需项，但后续可以考虑：

- 返回 BigMap 后，自动重新高亮之前正在编辑的节点，而不只是刷新右侧 Inspector
- 给 `TileMappingConfig` 做重复 ID / 缺失 Tile 的校验器
- 给 `TilemapLevelEditor` 做更明确的缺失映射诊断信息
- 后续如果有多层地图需求，再扩展 `gridMap` / `effectMap` 的编辑能力

---

## 一句话总结

当前版本的真实产品形态是：

**BigMap 负责选关卡节点，TilemapLevelEditor 负责编辑 `StageID.json`，TileMappingConfig 负责统一 Tile ↔ ID 映射，返回时恢复原多场景排布。**
