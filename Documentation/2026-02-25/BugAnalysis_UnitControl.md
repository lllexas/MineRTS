# 单位无法被指挥 Bug 分析报告

## Bug 描述
在游戏运行一段时间后，使用 `clear` 命令清空系统，然后重新执行测试指令序列，生成的单位无法被玩家指挥（无法通过点击或框选选中单位）。

## 测试指令序列
```bash
clear
save_new TestPlayer
enter Level_Test
cam_sync; cam_reset; cam_home
mission_load Missions/Test_Missions
spawn seller 5,-1 1
spawn hero_jin_proto -3,5 1
army x_dog 5,-5 5,5 1
army x_dog -5,-5 2,2 2
```

## 初步分析

### 1. 系统架构回顾
- **EntitySystem**: ECS核心，管理所有实体和组件
- **UserControlSystem**: 玩家单位选择和控制
- **AIBrainServer**: AI控制系统
- **GridSystem**: 空间网格和占据管理
- **CommandRegistry**: 测试指令实现

### 2. 可能的原因分析

#### 2.1 UserControlComponent 初始化问题
- **位置**: `EntitySystem.CreateEntityFromBlueprint()` 和 `CreateEntity()`
- **问题**: 这两个方法都没有初始化 `UserControlComponent` 数组中的对应元素
- **影响**: 新创建的实体 `UserControlComponent` 保持默认值（`HeroSlot = 0`）
- **验证**: `UserControlComponent` 结构仅包含 `HeroSlot` 字段，可能不是控制的关键因素

#### 2.2 实体类型标志检查
- **蓝图配置**:
  - `x_dog`: `UnitType.Minion` ✓
  - `hero_jin_proto`: `UnitType.Hero` ✓
  - `seller`: `UnitType.Building` ✗（建筑可能不可选择）
- **UserControlSystem 选择逻辑**:
  - 点选：检查 `core.Team == playerTeam`（第101行）
  - 框选：检查 `core.Team != playerTeam` 或 `(core.Type & UnitType.Projectile) != 0`（第122行）
  - 建筑可能被排除在可选择单位外

#### 2.3 队伍匹配问题
- **生成参数**: 所有测试单位队伍为 `1`（玩家队伍），除了第二个 `army` 命令生成队伍 `2` 的单位
- **UserControlSystem.playerTeam**: 默认值为 `1`，可能被其他系统修改且在 `clear` 后未重置

#### 2.4 GridSystem 占用注册问题
- **CreateEntity 逻辑**: 第183行调用 `GridSystem.Instance.SetOccupantRect()` 注册占用
- **潜在问题**: 如果 GridSystem 在 `clear` 后未正确重置，可能导致占用注册失败
- **点选依赖**: `DoPointSelection()` 通过 `GridSystem.Instance.GetOccupantId()` 获取单位ID

#### 2.5 AIBrainServer 残留控制
- **clear 命令**: 调用 `AIBrainServer.Instance.ClearAll()`（第139行）
- **ClearAll 实现**: 清除 `_activePipelines`、`_unitOwnership`、`_activeBrains`
- **潜在问题**: 可能还有其他未清理的静态状态或全局标志

#### 2.6 全局状态重置不完整
- **clear 命令重置的内容**:
  - UserControlSystem: `ClearAllSelection()`（仅清除选择状态）
  - AIBrainServer: `ClearAll()`
  - GridSystem: `ClearAll()`
  - EntitySystem: `Initialize()`（重新创建组件数组）
- **未重置的内容**:
  - UserControlSystem.playerTeam
  - 其他系统的静态字段或全局状态

### 3. 关键代码检查点

#### 3.1 CommandRegistry.cs - clear 命令（第129-160行）
```csharp
// 清理其他系统状态
UserControlSystem.Instance.ClearAllSelection();  // 仅清除选择，不重置playerTeam
AIBrainServer.Instance.ClearAll();
GridSystem.Instance.ClearAll();

// 重新初始化EntitySystem
EntitySystem.Instance.Initialize(...);
```

#### 3.2 EntitySystem.cs - CreateEntity 方法（第122-198行）
- 初始化 `coreComponent`、`moveComponent`、`healthComponent` 等
- **未初始化**: `userControlComponent`（数组已分配，但元素未设置）

#### 3.3 UserControlSystem.cs - 选择逻辑
- **点选**: 依赖 `GridSystem.GetOccupantId()` 和队伍匹配
- **框选**: 遍历所有实体，检查 `core.Team == playerTeam` 且非子弹单位

#### 3.4 BlueprintRegistry.cs - 单位类型
- `x_dog`: `UnitType.Minion` - 应可选择
- `hero_jin_proto`: `UnitType.Hero` - 应可选择
- `seller`: `UnitType.Building` - 建筑可能不可选择

### 4. 验证假设

#### 假设1: 建筑单位不可选择
- **seller** 是建筑 (`UnitType.Building`)，可能设计为不可选择
- **预期**: seller 无法被选择是正常行为
- **实际**: hero_jin_proto 和 x_dog 也应无法选择 → 假设不成立

#### 假设2: playerTeam 被修改
- **可能**: 游戏运行过程中 playerTeam 被修改为其他值
- **clear 后**: 未重置为默认值 `1`
- **验证**: 检查 UserControlSystem.playerTeam 的修改点

#### 假设3: GridSystem 占用失效
- **clear 调用**: `GridSystem.Instance.ClearAll()`
- **重新初始化**: EntitySystem.Initialize() 传递原有地图尺寸
- **可能问题**: GridSystem 内部状态未完全同步

### 5. 调试建议

#### 5.1 控制台调试命令
```bash
# 检查实体状态
entity_info <entity_id>

# 查看 GridSystem 占用
grid_info <x,y>

# 查看 UserControlSystem 状态
# （需要添加相应命令）

# 查看所有实体列表
list_entities
```

#### 5.2 代码添加调试输出
在以下位置添加日志：

1. **UserControlSystem.DoPointSelection()**:
   - 输出获取到的 occupantId
   - 输出单位队伍和 playerTeam 值

2. **EntitySystem.CreateEntity()**:
   - 输出创建的实体ID、队伍、类型

3. **GridSystem.SetOccupantRect()**:
   - 输出占用注册结果

#### 5.3 临时测试命令
添加临时命令检查系统状态：
```csharp
// 在 CommandRegistry 中添加
console.AddCommand("debug_control", (args) => {
    console.Log($"playerTeam = {UserControlSystem.Instance.playerTeam}", Color.yellow);
    console.Log($"GridSystem cellSize = {GridSystem.Instance.CellSize}", Color.yellow);

    var whole = EntitySystem.Instance.wholeComponent;
    for (int i = 0; i < whole.entityCount; i++) {
        ref var core = ref whole.coreComponent[i];
        console.Log($"Entity {i}: Team={core.Team}, Type={core.Type}, Pos={core.Position}", Color.white);
    }
});
```

### 6. 潜在修复方案

#### 方案1: 完善 clear 命令
```csharp
// 在 CommandRegistry.clear 命令中添加
UserControlSystem.Instance.playerTeam = 1; // 重置玩家队伍
// 其他系统的全局状态重置
```

#### 方案2: 确保 UserControlComponent 初始化
```csharp
// 在 EntitySystem.CreateEntity() 中添加
wholeComponent.userControlComponent[index] = default;
// 或根据单位类型设置 HeroSlot
if ((unitType & UnitType.Hero) != 0) {
    wholeComponent.userControlComponent[index].HeroSlot = 1;
}
```

#### 方案3: 验证 GridSystem 同步
确保 `clear` 后 GridSystem 与 EntitySystem 的地图参数一致。

#### 方案4: 添加实体创建后的控制注册
```csharp
// 在 CreateEntityFromBlueprint 中添加
if ((bp.UnitType & (UnitType.Hero | UnitType.Minion)) != 0) {
    // 注册到控制系统（如果需要）
}
```

### 7. 下一步行动建议

1. **重现问题**: 确认 bug 的稳定重现步骤
2. **添加调试**: 在关键位置添加日志输出
3. **隔离测试**: 简化测试序列，排除 mission_load 等命令的影响
4. **逐步验证**: 分别测试点选和框选功能
5. **修复验证**: 应用修复后重新测试

## 结论

单位无法被指挥的问题可能由多个因素导致，最可能的原因是 `clear` 命令未完全重置所有控制相关系统的全局状态，以及新创建实体的控制组件未正确初始化。建议从完善 `clear` 命令和确保组件初始化入手进行修复。

**优先检查点**:
1. UserControlSystem.playerTeam 的值
2. GridSystem.GetOccupantId() 在单位位置是否返回正确ID
3. 实体创建后 userControlComponent 的状态