# MineRTS 测试指令链路报告

## 概述

本文档详细描述了MineRTS项目中测试指令系统的完整链路，特别关注加载相关命令（`clear`、`save_new`、`enter`、`mission_load`等）的实现机制和系统集成。

## 测试指令系统架构

### 核心系统文件

1. **`DeveloperConsole.cs`** (`Assets/Scripts/InStage/UI/DeveloperConsole.cs`)
   - 开发者控制台UI界面
   - 命令输入、解析和执行
   - 彩色日志输出和滚动功能

2. **`CommandRegistry.cs`** (`Assets/Scripts/InStage/UI/CommandRegistry.cs`)
   - 命令注册和管理中心
   - 所有测试指令的具体实现

3. **`TestCommand.txt`** (`Assets/Scripts/InStage/UI/TestCommand.txt`)
   - 测试命令示例和标准流程

### 命令分类

| 类别 | 主要命令 | 功能描述 |
|------|----------|----------|
| **系统管理** | `clear`, `save_new`, `save_load`, `enter` | 系统状态管理、存档和关卡加载 |
| **实体操作** | `spawn`, `army`, `kill`, `kill_all` | 实体生成和删除 |
| **相机控制** | `cam_sync`, `cam_reset`, `cam_home`, `cam_focus` | 相机视角操作 |
| **任务管理** | `mission_load`, `mission_start`, `mission_info` | 任务剧本加载和管理 |
| **时间控制** | `timer_pause`, `timer_resume`, `timer_skip` | 游戏时间控制 |
| **调试工具** | `net_rebuild`, `net_info`, `nav_info` | 系统状态查看和调试 |
| **作弊功能** | `cheat_gold`, `cheat_power` | 资源作弊功能 |

## 重点：加载相关命令链路分析

### 1. `clear` - 清除系统状态

**命令格式**: `clear`

**执行链路**:
```
DeveloperConsole.ParseAndExecute()
  → CommandRegistry.ExecuteSystemCommand("clear")
    → EntitySystem.Instance.ClearAllEntities()
      → UserControlSystem.Instance.Clear()
      → AIBrainServer.Instance.Clear()
      → GridSystem.Instance.Clear()
      → EntitySystem.Reinitialize()
```

**关键代码位置**:
- `CommandRegistry.cs:105` - `ExecuteSystemCommand("clear")`
- `EntitySystem.cs` - `ClearAllEntities()`方法

**功能说明**:
- 清除所有实体（单位、建筑、物品等）
- 重置所有系统状态（AI、网格、控制系统）
- 重新初始化ECS组件数组
- 为新的测试场景准备干净的环境

### 2. `save_new` - 创建新存档

**命令格式**: `save_new <slot_name>`

**示例**: `save_new TestPlayer`

**执行链路**:
```
CommandRegistry.ExecuteSystemCommand("save_new")
  → SaveManager.Instance.CreateNewSave(slotName)
    → 创建存档数据结构
    → 序列化游戏状态
    → 保存到磁盘
```

**关键代码位置**:
- `CommandRegistry.cs:125` - `ExecuteSystemCommand("save_new")`
- `SaveManager.cs` - `CreateNewSave()`方法

**功能说明**:
- 创建新的存档槽位
- 保存当前玩家状态和进度
- 为后续的`enter`命令提供存档上下文

### 3. `enter` - 进入关卡

**命令格式**: `enter <stage_id>`

**示例**: `enter Level_Test`

**执行链路**:
```
CommandRegistry.ExecuteSystemCommand("enter")
  → EntitySystem.Instance.LoadStage(stageId)
    → 加载关卡配置数据
    → 初始化地形和静态物体
    → 生成预设实体
    → 设置关卡边界和起始条件
```

**关键代码位置**:
- `CommandRegistry.cs:140` - `ExecuteSystemCommand("enter")`
- `EntitySystem.cs` - `LoadStage()`方法

**功能说明**:
- 加载指定ID的关卡场景
- 初始化关卡特定的游戏状态
- 设置相机边界和地图范围
- 为测试提供标准化的环境

### 4. `mission_load` - 加载任务剧本

**命令格式**: `mission_load <mission_pack_path>`

**示例**: `mission_load Missions/Test_Missions`

**执行链路**:
```
CommandRegistry.ExecuteSystemCommand("mission_load")
  → MissionManager.Instance.LoadMissionPack(missionPackPath)
    → Resources.Load<TextAsset>(missionPackPath)
    → JSON解析任务配置
    → 初始化任务状态机
    → 注册任务事件处理器
```

**关键代码位置**:
- `CommandRegistry.cs` - `ExecuteSystemCommand("mission_load")`
- `MissionManager.cs` - `LoadMissionPack()`方法

**功能说明**:
- 从Resources加载任务剧本JSON文件
- 解析任务目标、条件和奖励
- 初始化任务系统状态
- 启用任务相关的游戏逻辑

### 5. 相机控制命令链

#### `cam_sync` - 同步相机边界
```
CommandRegistry.ExecuteCameraCommand("cam_sync")
  → CameraController.Instance.SyncBounds()
    → 获取当前地图边界
    → 更新相机限制范围
```

#### `cam_reset` - 重置相机缩放
```
CommandRegistry.ExecuteCameraCommand("cam_reset")
  → CameraController.Instance.ResetZoom()
    → 重置相机缩放到默认级别
```

#### `cam_home` - 回到地图中心
```
CommandRegistry.ExecuteCameraCommand("cam_home")
  → CameraController.Instance.GoToOrigin()
    → 移动相机到地图中心(0,0)位置
```

### 6. 实体生成命令链

#### `spawn` - 生成单个实体
**命令格式**: `spawn <blueprint_key> <x,y> <team> [aiType] [dir_x,y]`

**示例**: `spawn seller 5,-1 1`

**执行链路**:
```
CommandRegistry.ExecuteEntityCommand("spawn")
  → BlueprintRegistry.Get(blueprintKey)
  → EntitySystem.Instance.CreateEntityFromBlueprint(blueprint, position, team, aiType, direction)
    → 分配实体ID
    → 初始化所有组件
    → 设置位置和队伍
    → 注册到空间网格
```

#### `army` - 生成方阵实体
**命令格式**: `army <blueprint_key> <center_x,y> <width,height> <team>`

**示例**: `army x_dog 5,-5 5,5 1`

**执行链路**:
```
CommandRegistry.ExecuteEntityCommand("army")
  → 循环生成多个实体
  → 在指定区域内均匀分布
  → 调用EntitySystem.CreateEntityFromBlueprint()逐个创建
```

## 测试指令示例分析

### 标准测试流程
```bash
# 1. 清理环境
clear

# 2. 创建存档（提供玩家上下文）
save_new TestPlayer

# 3. 进入测试关卡
enter Level_Test

# 4. 相机初始化（确保正确视角）
cam_sync; cam_reset; cam_home

# 5. 加载任务剧本
mission_load Missions/Test_Missions

# 6. 生成测试实体
spawn seller 5,-1 1          # 生成商人单位
spawn hero_jin_proto -3,5 1  # 生成英雄原型

# 7. 生成部队方阵
army x_dog 5,-5 5,5 1        # 生成5x5机械狗方阵（队1）
army x_dog -5,-5 2,2 2       # 生成2x2机械狗方阵（队2）
```

### 链路时序分析
1. **环境准备阶段** (`clear` → `save_new` → `enter`)
   - 确保每次测试从干净状态开始
   - 提供存档支持的游戏上下文
   - 加载标准测试环境

2. **视角设置阶段** (`cam_sync` → `cam_reset` → `cam_home`)
   - 确保相机正确显示整个测试区域
   - 提供标准化的观察视角

3. **内容加载阶段** (`mission_load`)
   - 加载任务逻辑和游戏规则
   - 启用特定的游戏行为

4. **实体部署阶段** (`spawn` → `army`)
   - 部署测试单位
   - 创建对战场景

## 系统依赖关系

### 核心系统依赖
- **EntitySystem**: 所有实体操作的基础
- **BlueprintRegistry**: 实体蓝图的定义和获取
- **SaveManager**: 存档管理支持
- **MissionManager**: 任务系统支持
- **CameraController**: 相机控制
- **GridSystem**: 空间管理和碰撞检测

### 数据流依赖
1. **蓝图数据** → 实体生成
2. **关卡配置** → 环境初始化
3. **任务剧本** → 游戏逻辑
4. **存档数据** → 玩家状态

## 潜在问题和调试建议

### 常见问题
1. **实体生成失败**
   - 检查蓝图key是否正确
   - 验证位置是否在可放置区域
   - 确认队伍ID有效性

2. **关卡加载异常**
   - 检查关卡ID是否存在
   - 验证关卡配置文件的完整性
   - 查看EntitySystem日志

3. **任务加载失败**
   - 确认Resources路径正确
   - 检查JSON格式有效性
   - 验证任务依赖的实体是否存在

### 调试命令
```bash
# 查看实体状态
entity_info <entity_id>

# 查看网络状态
net_info

# 查看导航网格
nav_info

# 查看系统状态
system_info
```

## 扩展和定制

### 添加新命令
1. 在`CommandRegistry.cs`中添加命令处理方法
2. 注册到相应的命令类别中
3. 在`DeveloperConsole.cs`中更新命令列表显示

### 修改现有命令
1. 定位`CommandRegistry.cs`中的对应方法
2. 修改执行逻辑
3. 更新相关系统调用

### 创建自定义测试流程
1. 参考`TestCommand.txt`格式
2. 组合现有命令创建新流程
3. 保存为新的命令脚本文件

## 总结

MineRTS的测试指令系统提供了完整的调试工具链，通过命令的组合可以快速创建各种测试场景。加载相关命令（`clear`、`save_new`、`enter`、`mission_load`）构成了测试环境的基础框架，确保每次测试都在可控、可重复的环境中进行。

系统设计具有良好扩展性，新的测试命令可以方便地集成到现有框架中，为持续开发和测试提供了有力支持。