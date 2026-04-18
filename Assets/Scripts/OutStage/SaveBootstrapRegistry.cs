using System;
using System.Collections.Generic;
using NekoGraph;
using UnityEngine;

/// <summary>
/// 【注册表】只负责记录"新存档应该加载哪些Pack"
/// 职责单一：只存静态表，提供简单的注册和应用接口
/// </summary>
public static class SaveBootstrapRegistry
{
    /// <summary>
    /// 【静态表】这就是我们需要的核心
    /// </summary>
    private static readonly List<BootstrapEntry> _bootstrapList = new()
    {
        // 核心系统包
        new BootstrapEntry { PackID = "social_tree_default", IsMetaPack = true, Required = true },
        new BootstrapEntry { PackID = PlayerWarehouseManager.DefaultWarehousePackID, IsMetaPack = false, Required = true },
        
        // 主进程系统包 - 从MetaLib加载的预制包
        // 【架构说明】：
        // - 用途：驱动主进程/主线剧情流程
        // - 节点类型：使用 Root -> Spine -> Leaf 为主干结构
        // - 控制节点：Trigger (监听事件) + Comparer (逻辑判断) + Accumulator (进度累计) + Command (执行操作)
        // - 运行方式：GraphRunner 每帧 Tick 驱动信号流动
        new BootstrapEntry { PackID = "main_process", IsMetaPack = true, Required = false },
        
        // Lab系统进程包 - 从MetaLib加载的预制包
        // 【架构说明】：
        // - 用途：在特定Lab进程中，把实体蓝图和解锁需求包装成科技节点
        // - 节点类型：结构类似 main_story，使用 Root -> Spine -> Leaf 为主干
        // - 节点内容：记录 blueprint_id（实体蓝图）、description（描述）、resource_id（演示视频/图片资源ID）
        // - 输出目标：将包装好的科技节点搬运到 lab_panel 包
        new BootstrapEntry { PackID = "lab_process", IsMetaPack = true, Required = false },
        
        // Lab面板包 - 空包
        // 【架构说明】：
        // - 用途：作为Lab系统的UI数据容器，展示科技节点
        // - 来源：由 lab_process 包在合适时机搬运科技节点到此包
        new BootstrapEntry { PackID = "lab_panel", IsMetaPack = false, Required = false },
        
        // 实体仓库系统包 - 从MetaLib加载的预制包
        // 【架构说明】：
        // - 用途：玩家的持久化实体蓝图仓库
        // - 来源：从 lab_panel 包中解锁或购买
        // - 结构：VFS子文件夹结构，按实体类型分类（预制包已提前创建好）
        // - 特殊节点：背包索引节点，记录玩家携带到局内的实体id
        // - 背包索引节点约定：
        //   - 扩展名：.carry
        //   - 数据类型：CarryData (包含 CarriedEntityIds 列表)
        //   - 位置：通常在根目录 /inventory.carry
        new BootstrapEntry { PackID = "entity_warehouse", IsMetaPack = true, Required = false },
        
        // 新添加的空包
        new BootstrapEntry { PackID = "lab_system", IsMetaPack = false, Required = false },
        new BootstrapEntry { PackID = "player_progress", IsMetaPack = false, Required = false },
        new BootstrapEntry { PackID = "achievements", IsMetaPack = false, Required = false },
        new BootstrapEntry { PackID = "settings", IsMetaPack = false, Required = false }
    };

    /// <summary>
    /// 【表项】单个Pack的配置
    /// </summary>
    private class BootstrapEntry
    {
        public string PackID;
        public bool IsMetaPack;   // true=从MetaLib加载, false=new BasePackData
        public bool Required;     // 加载失败是否报错
    }

    /// <summary>
    /// 【应用】把表中所有Pack应用到用户存档
    /// </summary>
    public static void ApplyDefaultPacks(UserModel user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        user.PackDataDict ??= new Dictionary<string, BasePackData>();

        foreach (var entry in _bootstrapList)
        {
            ApplyEntry(user, entry);
        }
    }

    /// <summary>
    /// 【注册】运行时动态添加新项
    /// </summary>
    public static void Register(string packID, bool isMetaPack, bool required = true)
    {
        _bootstrapList.Add(new BootstrapEntry
        {
            PackID = packID,
            IsMetaPack = isMetaPack,
            Required = required
        });
    }

    /// <summary>
    /// 【清空】清空表（测试用）
    /// </summary>
    public static void Clear() => _bootstrapList.Clear();

    // =========================================================
    //  内部实现
    // =========================================================

    private static void ApplyEntry(UserModel user, BootstrapEntry entry)
    {
        BasePackData pack = null;

        if (entry.IsMetaPack)
        {
            pack = MetaLib.GetPack<BasePackData>(entry.PackID);
        }
        else
        {
            pack = new BasePackData { PackID = entry.PackID };
            pack.Initialize();
        }

        if (pack == null)
        {
            ReportFailure(entry.Required, $"Failed to load pack: {entry.PackID}");
            return;
        }

        PreparePack(pack);
        user.PackDataDict[pack.PackID] = pack;
    }

    private static void PreparePack(BasePackData pack)
    {
        pack.HasStarted = false;
        pack.ActiveSignals ??= new Queue<SignalContext>();
        pack.ActiveSignals.Clear();
        pack.Touch();
    }

    private static void ReportFailure(bool required, string message)
    {
        if (required)
            Debug.LogError($"[SaveBootstrapRegistry] {message}");
        else
            Debug.LogWarning($"[SaveBootstrapRegistry] {message}");
    }
}
