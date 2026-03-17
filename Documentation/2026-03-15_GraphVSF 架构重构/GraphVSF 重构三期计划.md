# GraphVSF 架构重构三期计划

**日期**: 2026 年 3 月 15 日  
**状态**: 计划制定完成  
**作者**: NekoTeam

---

## 目录

1. [总体概述](#总体概述)
2. [第一期：基础架构重构](#第一期基础架构重构)
3. [第二期：运行时系统](#第二期运行时系统)
4. [第三期：集成与编辑器](#第三期集成与编辑器)
5. [文件结构](#文件结构)
6. [进度追踪](#进度追踪)

---

## 总体概述

### 重构目标

基于《GraphVSF 架构设计报告》，对现有 VFS 系统进行三期重构：

1. **统一节点类型** - 只用 `VFSNodeData` 一种类型，通过 `Extension` 区分文件/目录
2. **复用原有设施** - 使用 `RootNodeData`、`BasePackData`、`BaseNodeData`
3. **清晰的职责分离** - Runtime（`Runtime/GraphVSF/`）vs Editor（`Editor/GraphVSF/`）
4. **Unix 风格路径** - 使用扩展名区分文件/目录，而不是类型枚举

### 三期划分

| 期数 | 名称 | 目标 | 预计工作量 |
|------|------|------|-----------|
| 第一期 | 基础架构重构 | 清理冗余代码，建立统一的 VFS 核心类型 | 中等 |
| 第二期 | 运行时系统 | 实现 VFS 解释器和静态图分析器 | 大 |
| 第三期 | 集成与编辑器 | 完成 SocialCLI 集成和编辑器工具 | 大 |

---

## 第一期：基础架构重构

**目标**：清理冗余代码，建立统一的 VFS 核心类型

### 任务清单

- [ ] 删除冗余类型
  - [ ] `VSFRootNodeData`
  - [ ] `VSFFolderNodeData`
  - [ ] `VSFFileNodeData`
  - [ ] `VSFNodeType` 枚举
- [ ] 将 `VFSNodeData` 改为非抽象类
  - [ ] 简化字段设计（使用 `Name` + `Extension`）
  - [ ] 添加 `IsDirectory`、`IsFile` 只读属性
  - [ ] 添加 `FullPath`、`ParentNodeID` 路径信息字段
  - [ ] 添加 `DataJson` 数据内容字段
- [ ] 创建 `VFSPathResolver` 路径解析器静态工具类
  - [ ] 实现 `Normalize` - 规范化路径
  - [ ] 实现 `Combine` - 合并路径
  - [ ] 实现 `GetParentPath` - 获取父路径
  - [ ] 实现 `GetFileName` - 获取文件名
  - [ ] 实现 `SplitToSegments` - 分割路径段
  - [ ] 实现 `FromSegments` - 从路径段构建路径
  - [ ] 实现 `Resolve` - 解析路径（支持相对路径）
- [ ] 更新 `VFSPackData`
  - [ ] 继承 `BasePackData`
  - [ ] 添加 `GraphType` 字段（固定为 "VFS"）
  - [ ] 添加 `RootNodeIds` 字段
- [ ] 编写路径解析器单元测试

### 交付物

| 文件 | 说明 | 状态 |
|------|------|------|
| `VFSNodeData.cs` | 统一节点类型 | 待创建 |
| `VFSPackData.cs` | 统一数据包类型 | 待创建 |
| `VFSPathResolver.cs` | 路径解析工具类 | 待创建 |

### 验收标准

1. 冗余类型全部删除
2. `VFSNodeData` 可正确区分文件/目录
3. `VFSPathResolver` 所有方法通过单元测试
4. 代码符合项目规范（命名、注释、格式）

---

## 第二期：运行时系统

**目标**：实现 VFS 解释器和静态图分析器

### 任务清单

- [ ] 创建 `VFSInterpreter` 单例类
  - [ ] 继承 `SingletonMono<VFSInterpreter>`
  - [ ] 实现实例字典管理
  - [ ] 实现 `RegisterInstance`、`UnregisterInstance`、`GetInstance`
  - [ ] 实现 `GetDefaultInstance`、`SetDefaultInstanceId`
- [ ] 创建 `VFSInstance` 运行时实例类
  - [ ] 实现 `NodeMap` 节点字典
  - [ ] 实现 `PathIndex` 路径索引
  - [ ] 实现 `RootNodeIds` 根节点列表
  - [ ] 实现 `AddNode`、`GetNodeByPath`、`GetChildren`、`Clear`
- [ ] 实现节点查询方法
  - [ ] `GetNode(string path)` - 根据路径获取节点
  - [ ] `ListChildren(string path)` - 列出目录的子节点
  - [ ] `PathExists(string path)` - 检查路径是否存在
  - [ ] `IsDirectory(string path)` - 检查是否是目录
  - [ ] `IsFile(string path)` - 检查是否是文件
- [ ] 实现文件读写方法
  - [ ] `ReadFile<T>(string path)` - 读取文件内容
  - [ ] `WriteFile<T>(string path, T value)` - 写入文件内容
- [ ] 创建 `GraphAnalyser` 通用静态图分析器
  - [ ] 设计为泛型静态类
  - [ ] 实现 `BuildTreeStructure<T>` - 构建树形结构
  - [ ] 实现 `BuildPathIndex<T>` - 建立路径索引
  - [ ] 实现 `BuildNameIndex<T>` - 建立名称索引
  - [ ] 实现 `GetChildren<T>` - 获取子节点列表
  - [ ] 实现 `GetNodeByPath<T>` - 根据路径获取节点
  - [ ] 实现 `ValidateTree<T>` - 验证树结构
- [ ] 实现树构建方法
  - [ ] `BuildTreeStructure` - 设置 ParentNodeID 和 FullPath
  - [ ] `BuildSubTree` - 递归构建子树
- [ ] 编写集成测试

### 交付物

| 文件 | 说明 | 状态 |
|------|------|------|
| `VFSInterpreter.cs` | VFS 解释器单例 | 待创建 |
| `VFSInstance.cs` | 运行时实例 | 待创建 |
| `GraphAnalyser.cs` | 通用静态图分析器 | 待创建 |

### 验收标准

1. `VFSInterpreter` 可正确管理 VFS 实例
2. 路径查询时间复杂度为 O(1)
3. 文件读写支持泛型 JSON 序列化/反序列化
4. `GraphAnalyser` 可复用于其他图类型（Config、SkillTree 等）
5. 所有方法通过集成测试

---

## 第三期：集成与编辑器

**目标**：完成 SocialCLI 集成和编辑器工具

### 任务清单

- [ ] 修改 `SocialCLI` 类
  - [ ] 添加 `CurrentNode` 属性（当前目录节点）
  - [ ] 添加 `CurrentPath` 只读属性（从 `CurrentNode.FullPath` 计算）
  - [ ] 添加 `InitializeVFS()` 初始化方法
  - [ ] 从 Resources 加载 `social_tree.json`
  - [ ] 创建默认社交文件树（如果 JSON 不存在）
- [ ] 重构 `cd` 命令
  - [ ] 使用 `VFSInterpreter.GetNode` 查询目标节点
  - [ ] 支持相对路径（`..`、`.`）
  - [ ] 支持绝对路径（`/social/`）
  - [ ] 验证目标是否是目录
- [ ] 重构 `ls` 命令
  - [ ] 使用 `VFSInterpreter.ListChildren` 获取子节点
  - [ ] 显示目录图标（`[DIR]`、`[FILE]`）
  - [ ] 跳过被禁用的节点
- [ ] 创建 `pwd` 命令
  - [ ] 显示当前目录路径
- [ ] 创建编辑器工具
  - [ ] `VFSGraphView` - VFS 画布
    - [ ] 继承 `BaseGraphView<VFSPackData>`
    - [ ] 实现连接验证（文件节点不能有输出连接）
    - [ ] 实现节点标题更新
  - [ ] `VFSGraphWindow` - VFS 编辑器窗口
    - [ ] 继承 `BaseGraphWindow<VFSGraphView, VFSPackData>`
    - [ ] 实现菜单项 `GraphVSF/VFS Editor`
    - [ ] 实现保存/加载功能
  - [ ] `VFSNodeView` - VFS 节点视图
    - [ ] 继承 `BaseNode<VFSNodeData>`
    - [ ] 实现 UI 编辑（名称、扩展名、描述、启用状态）
    - [ ] 实现标题自动更新
  - [ ] `VFSNodeSearchWindow` - VFS 节点搜索窗口
    - [ ] 继承 `BaseNodeSearchWindow`
    - [ ] 实现节点创建菜单
- [ ] 添加属性标签
  - [ ] `[NodeMenuItem("VFS/VFS 节点", typeof(VFSNodeData))]`
  - [ ] `[NodeType(NodeSystem.Common)]`
- [ ] 创建默认测试数据
  - [ ] `social_tree.json` - 社交文件树
    - [ ] 根节点 `/social/`
    - [ ] 子目录：`friends/`、`requests/`、`blocks/`、`groups/`、`messages/`
- [ ] 整体测试与验证
  - [ ] 编辑器创建节点测试
  - [ ] 保存/加载测试
  - [ ] SocialCLI 命令测试
  - [ ] 路径解析测试

### 交付物

| 文件 | 说明 | 状态 |
|------|------|------|
| `SocialCLI.cs` | 集成 VFS 的版本 | 待修改 |
| `CommandRegistry.Social.cs` | 重构后的命令 | 待修改 |
| `VFSGraphView.cs` | VFS 画布 | 待创建 |
| `VFSGraphWindow.cs` | VFS 编辑器窗口 | 待创建 |
| `VFSNodeView.cs` | VFS 节点视图 | 待创建 |
| `VFSNodeSearchWindow.cs` | VFS 节点搜索窗口 | 待创建 |
| `social_tree.json` | 测试数据 | 待创建 |

### 验收标准

1. SocialCLI 可正确初始化 VFS
2. `cd`、`ls`、`pwd` 命令正常工作
3. 编辑器可创建、编辑、保存 VFS 节点
4. 加载 JSON 后树结构正确构建
5. 所有功能通过手动测试

---

## 文件结构

```
Assets/Scripts/
├── NekoGraph/
│   ├── Runtime/
│   │   ├── Runner_Analyser/
│   │   │   ├── GraphRunner.cs          (原有)
│   │   │   ├── RuntimeGraphInstance.cs (原有)
│   │   │   ├── SignalContext.cs        (原有)
│   │   │   ├── INodeStrategy.cs        (原有)
│   │   │   ├── GraphAnalyser.cs        (第二期 ✨)
│   │   │   └── VFSInstance.cs          (第二期 ✨)
│   │   │
│   │   ├── GraphVSF/                   (新目录 ✨)
│   │   │   ├── VFSNodeData.cs          (第一期 ✨)
│   │   │   ├── VFSPackData.cs          (第一期 ✨)
│   │   │   ├── VFSPathResolver.cs      (第一期 ✨)
│   │   │   └── VFSInterpreter.cs       (第二期 ✨)
│   │   │
│   │   ├── Base/                       (原有)
│   │   │   ├── BasePackData.cs
│   │   │   ├── BaseNodeData.cs
│   │   │   └── ConnectionData.cs
│   │   │
│   │   ├── Data/                       (原有)
│   │   │   └── ProcessFlowNodeData.cs  (含 RootNodeData)
│   │   │
│   │   └── Attributes/                 (原有)
│   │       ├── NodeTypeAttribute.cs
│   │       └── NodeMenuItemAttribute.cs
│   │
│   └── Editor/
│       ├── GraphVSF/                   (新目录 ✨ 第三期)
│       │   ├── VFSGraphView.cs
│       │   ├── VFSGraphWindow.cs
│       │   ├── VFSNodeView.cs
│       │   └── VFSNodeSearchWindow.cs
│       │
│       └── _Base/                      (原有)
│           ├── BaseGraphView.cs
│           ├── BaseGraphWindow.cs
│           ├── BaseNode.cs
│           └── BaseNodeSearchWindow.cs
│
├── OutStage/
│   └── SocialCLI/
│       ├── SocialCLI.cs                (第三期 重构)
│       └── CommandRegistry.Social.cs   (第三期 重构)
│
└── Resources/
    └── NekoGraph/
        └── GraphVSF/
            └── Packs/                  (新目录 ✨ 第三期)
                └── social_tree.json    (第三期 ✨)
```

---

## 进度追踪

### 第一期进度

| 任务 | 状态 | 完成日期 | 备注 |
|------|------|----------|------|
| 删除冗余类型 | ⬜ 未开始 | - | - |
| 创建 VFSNodeData.cs | ⬜ 未开始 | - | - |
| 创建 VFSPackData.cs | ⬜ 未开始 | - | - |
| 创建 VFSPathResolver.cs | ⬜ 未开始 | - | - |
| 单元测试 | ⬜ 未开始 | - | - |

### 第二期进度

| 任务 | 状态 | 完成日期 | 备注 |
|------|------|----------|------|
| 创建 VFSInterpreter.cs | ⬜ 未开始 | - | - |
| 创建 VFSInstance.cs | ⬜ 未开始 | - | - |
| 创建 GraphAnalyser.cs | ⬜ 未开始 | - | - |
| 集成测试 | ⬜ 未开始 | - | - |

### 第三期进度

| 任务 | 状态 | 完成日期 | 备注 |
|------|------|----------|------|
| 修改 SocialCLI.cs | ⬜ 未开始 | - | - |
| 重构 cd/ls/pwd 命令 | ⬜ 未开始 | - | - |
| 创建编辑器工具 | ⬜ 未开始 | - | - |
| 创建 social_tree.json | ⬜ 未开始 | - | - |
| 整体测试 | ⬜ 未开始 | - | - |

---

**文档结束**
