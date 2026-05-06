# Clear 命令彻底重置修复报告

## 问题描述
在游戏运行一段时间后，使用 `clear` 命令清空系统，然后重新执行测试指令序列，生成的单位无法被玩家指挥。

## 根本原因分析
根据代码分析，`clear` 命令不彻底是主要原因：
1. **UserControlSystem.playerTeam** 可能被修改且未重置
2. **其他系统全局状态** 未清理（工业、电力、物流、时间等系统）
3. **EntitySystem 重新初始化** 但某些组件未正确初始化

## 修复内容

### 1. 修改文件
**`Assets/Scripts/InStage/UI/CommandRegistry.cs`**

### 2. 具体修改

#### 2.1 添加必要的命名空间引用
```csharp
using System.Reflection;  // 用于反射检查Clear方法
```

#### 2.2 增强 clear 命令（第129-160行区域）
```csharp
console.AddCommand("clear", (args) =>
{
    // 先清理其他系统的状态，防止残留句柄引用
    if (UserControlSystem.Instance != null)
    {
        UserControlSystem.Instance.ClearAllSelection();
        UserControlSystem.Instance.playerTeam = 1; // 重置玩家队伍为默认值
    }

    if (AIBrainServer.Instance != null)
    {
        AIBrainServer.Instance.ClearAll();
    }

    // 清理GridSystem的NavMesh和占据状态
    if (GridSystem.Instance != null)
    {
        GridSystem.Instance.ClearAll();
    }

    // 清理其他工业与物流系统状态
    if (IndustrialSystem.Instance != null)
    {
        IndustrialSystem.Instance.GlobalPowerOverride = false;
        // 可以添加其他工业状态重置
    }

    if (PowerSystem.Instance != null)
    {
        // PowerSystem 可能没有Clear方法，但可以重置网络状态
        // 如果有Clear方法则调用
        var method = PowerSystem.Instance.GetType().GetMethod("Clear");
        if (method != null) method.Invoke(PowerSystem.Instance, null);
    }

    if (TransportSystem.Instance != null)
    {
        // TransportSystem 可能没有Clear方法
        var method = TransportSystem.Instance.GetType().GetMethod("Clear");
        if (method != null) method.Invoke(TransportSystem.Instance, null);
    }

    if (TimeSystem.Instance != null)
    {
        TimeSystem.Instance.SetPaused(false);
        TimeSystem.Instance.ResetTimer();
    }

    // 重新初始化，保持原来的地图规格和坐标偏移
    var whole = EntitySystem.Instance.wholeComponent;
    int w = (whole != null && whole.mapWidth > 0) ? whole.mapWidth : 128;
    int h = (whole != null && whole.mapHeight > 0) ? whole.mapHeight : 128;
    int minX = (whole != null) ? whole.minX : -64;
    int minY = (whole != null) ? whole.minY : -64;

    // 获取当前cellSize，如果GridSystem已初始化
    float cellSize = (GridSystem.Instance != null) ? GridSystem.Instance.CellSize : 1.0f;

    EntitySystem.Instance.Initialize(EntitySystem.Instance.maxEntityCount, w, h, minX, minY, cellSize);
    console.Log("System reset: All entities and gridMap cleared.", Color.yellow);
});
```

### 3. 修复原理

#### 3.1 玩家队伍重置
- **问题**: `UserControlSystem.playerTeam` 可能在游戏过程中被修改
- **修复**: `clear` 时显式重置为默认值 `1`
- **影响**: 确保玩家能正确选择队伍1的单位

#### 3.2 工业系统状态重置
- **问题**: `IndustrialSystem.GlobalPowerOverride` 可能被 `cheat_power` 命令启用
- **修复**: 重置为 `false`，恢复电力限制
- **影响**: 清除作弊状态，恢复正常游戏规则

#### 3.3 电力与物流系统清理
- **方法**: 通过反射检查并调用 `Clear()` 方法（如果存在）
- **原理**: 这些系统可能持有网络连接、缓存状态等
- **备选**: 如果 `Clear()` 方法不存在，系统可能依赖 EntitySystem 的重新初始化

#### 3.4 时间系统重置
- **问题**: 时间可能被暂停或偏移
- **修复**: 恢复时间流动并重置计时器
- **影响**: 确保游戏时间从干净状态开始

### 4. 潜在需要进一步修复的问题

#### 4.1 EntitySystem.CreateEntity() 中的 UserControlComponent 初始化
- **位置**: `Assets/Scripts/InStage/System/EntitySystem.cs` 第122-198行
- **问题**: `userControlComponent[index]` 未显式初始化
- **现状**: 保持默认值（`HeroSlot = 0`），可能不影响基本控制
- **建议**: 如果当前修复不足，可添加初始化代码

#### 4.2 SaveManager 存档状态清理
- **考虑**: `clear` 是否应该清理当前存档状态？
- **现状**: 未处理，`clear` 后可以立即 `save_new` 或 `enter`
- **权衡**: 保持存档系统独立可能更合理

#### 4.3 MissionManager 任务状态清理
- **考虑**: `clear` 是否应该清除已加载的任务？
- **现状**: 未处理，测试序列中 `clear` 后立即 `mission_load`
- **建议**: 当前设计合理，任务在 `mission_load` 时加载

### 5. 测试建议

#### 5.1 基础测试流程
```bash
# 1. 启动游戏，运行一段时间
# 2. 执行 clear 命令
clear

# 3. 执行完整测试序列
save_new TestPlayer
enter Level_Test
cam_sync; cam_reset; cam_home
mission_load Missions/Test_Missions
spawn seller 5,-1 1
spawn hero_jin_proto -3,5 1
army x_dog 5,-5 5,5 1
army x_dog -5,-5 2,2 2

# 4. 验证单位是否可被指挥
#    - 点击选择单个单位
#    - 框选选择多个单位
#    - 右键移动命令
#    - 攻击命令（如果单位有攻击能力）
```

#### 5.2 调试命令
建议添加以下调试命令辅助测试：
```csharp
// 在 CommandRegistry 中添加调试命令
console.AddCommand("debug_control", (args) => {
    console.Log($"playerTeam = {UserControlSystem.Instance.playerTeam}", Color.yellow);
    console.Log($"GlobalPowerOverride = {IndustrialSystem.Instance.GlobalPowerOverride}", Color.yellow);

    var whole = EntitySystem.Instance.wholeComponent;
    console.Log($"Entity count = {whole.entityCount}", Color.yellow);
    for (int i = 0; i < whole.entityCount; i++) {
        ref var core = ref whole.coreComponent[i];
        console.Log($"Entity {i}: Team={core.Team}, Type={core.Type}, Pos={core.Position}", Color.white);
    }
});
```

### 6. 回退方案

如果当前修复引起新问题或不够彻底：

#### 方案A: 移除反射调用
```csharp
// 替换反射调用为直接调用已知方法
if (PowerSystem.Instance != null)
{
    // 调用已知的重置方法（如果存在）
    // PowerSystem.Instance.Reset();
}

if (TransportSystem.Instance != null)
{
    // 调用已知的重置方法（如果存在）
    // TransportSystem.Instance.RebuildNetwork(whole);
}
```

#### 方案B: 仅重置确认存在的状态
```csharp
// 只重置已知的全局状态，不尝试调用未知方法
IndustrialSystem.Instance.GlobalPowerOverride = false;
TimeSystem.Instance.SetPaused(false);
TimeSystem.Instance.ResetTimer();
```

#### 方案C: 增强 EntitySystem.CreateEntity()
```csharp
// 在 EntitySystem.CreateEntity() 中添加
wholeComponent.userControlComponent[index] = default;
if ((unitType & UnitType.Hero) != 0)
{
    wholeComponent.userControlComponent[index].HeroSlot = 1;
}
```

### 7. 总结

本次修复重点在于使 `clear` 命令真正彻底重置所有关键系统状态。主要改进：

1. **重置玩家队伍**，确保选择逻辑正确
2. **清理工业作弊状态**，恢复游戏规则
3. **尝试清理电力、物流系统**，消除残留状态
4. **重置时间系统**，确保时间从干净状态开始

修复后，期望 `clear` 命令能提供一个完全干净的测试环境，解决单位无法被指挥的问题。

**下一步**: 请测试修复后的 `clear` 命令，如果问题仍然存在，可能需要进一步检查 `EntitySystem.CreateEntity()` 中的组件初始化，或添加更多系统的状态清理。