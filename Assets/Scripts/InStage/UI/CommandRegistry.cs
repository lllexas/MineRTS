using System;
using System.Collections.Generic;
using AIBrain;
using UnityEngine;
using SpaceTUI;
using NekoGraph;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// CommandRegistry - 命令执行注册表（重构版喵~）
/// ═══════════════════════════════════════════════════════════════
///
/// 所有命令执行逻辑都在这里定义，每个命令是一个静态方法 + [CommandInfo] 特性。
/// 使用反射自动注册，无需手动维护注册表喵~
///
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public static class GameCommandBricks
{
    // =========================================================
    // 辅助方法喵~
    // =========================================================

    private static Vector2Int ParseGridPos(string input)
    {
        string[] parts = input.Split(',');
        if (parts.Length < 2) return Vector2Int.zero;
        return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    private static void Log(IConsoleController console, string message, Color color)
    {
        console?.Log(message, color);
    }

    // =========================================================
    // 🏗️ Entity 实体相关命令
    // =========================================================

    [CommandInfo("spawn", "🏗️ 召唤单位", "Entity", new[] { "BlueprintID", "Position (x,y)", "Team" },
        Tooltip = "在指定位置召唤单个单位喵~\n示例：spawn x_dog 0,0 1",
        Color = "0.2,0.6,0.2")]
    public static CommandOutput Spawn(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 3)
        {
            return CommandOutput.Fail("Usage: spawn <key> <x,y> <team> [aiType] [dir_x,y]");
        }

        string key = args[0].ToLower();
        Vector2Int pos = ParseGridPos(args[1]);
        int team = int.Parse(args[2]);

        EntityBlueprint bp = BlueprintRegistry.Get(key);
        if (string.IsNullOrEmpty(bp.Name))
        {
            return CommandOutput.Fail($"Unknown blueprint: {key}");
        }

        if (!GridSystem.Instance.IsAreaClear(pos, bp.LogicSize))
        {
            return CommandOutput.Fail($"Area {pos} is blocked!");
        }

        EntityHandle handle = EntitySystem.Instance.CreateEntityFromBlueprint(key, pos, team);
        int idx = EntitySystem.Instance.GetIndex(handle);

        if (idx != -1 && args.Length >= 4)
        {
            EntitySystem.Instance.wholeComponent.coreComponent[idx].Rotation = ParseGridPos(args[4]);
        }

        return CommandOutput.Success($"Successfully spawned {bp.Name} (Team {team})", handle);
    }

    [CommandInfo("army", "🏗️ 方阵召唤", "Entity", new[] { "BlueprintID", "Center (x,y)", "Width", "Height", "Team" },
        Tooltip = "以方阵形式召唤多个单位喵~\n示例：army x_dog 0,0 3,3 1",
        Color = "0.2,0.5,0.2")]
    public static CommandOutput Army(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 4)
        {
            return CommandOutput.Fail("Usage: army <key> <x,y> <w,h> <team>");
        }

        string key = args[0];
        Vector2Int center = ParseGridPos(args[1]);
        Vector2Int groupSize = ParseGridPos(args[2]);
        int team = int.Parse(args[3]);

        EntityBlueprint bp = BlueprintRegistry.Get(key);
        int startX = center.x - groupSize.x / 2;
        int startY = center.y - groupSize.y / 2;
        int count = 0;
        List<EntityHandle> handles = new List<EntityHandle>();

        for (int x = 0; x < groupSize.x; x++)
        {
            for (int y = 0; y < groupSize.y; y++)
            {
                Vector2Int current = new Vector2Int(startX + x, startY + y);
                if (GridSystem.Instance.IsAreaClear(current, bp.LogicSize))
                {
                    var handle = EntitySystem.Instance.CreateEntityFromBlueprint(key, current, team);
                    handles.Add(handle);
                    count++;
                }
            }
        }

        return CommandOutput.Success($"Army: {count} {bp.Name}s deployed for Team {team}", handles);
    }

    [CommandInfo("ai_wave", "🏗️ AI 波次绑定", "Entity", new[] { "Team", "BrainID", "TargetPos (x,y)" },
        Tooltip = "将单位绑定到 AI 波次逻辑喵~\n示例：ai_wave 1 Red_Dot_Wave 0,0",
        Color = "0.2,0.4,0.2")]
    public static CommandOutput AiWave(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 3) return CommandOutput.Fail("Usage: ai_wave <team> <brainId> <x,y>");

        int team = int.Parse(args[0]);
        string brainId = args[1];
        Vector2Int targetPos = ParseGridPos(args[2]);

        var whole = EntitySystem.Instance.wholeComponent;
        List<EntityHandle> recruits = new List<EntityHandle>();

        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            if (core.Active && core.Team == team && (core.Type & UnitType.Building) == 0)
            {
                if (!AIBrainServer.Instance.IsUnitControlled(core.SelfHandle))
                    recruits.Add(core.SelfHandle);
            }
        }

        if (recruits.Count > 0)
        {
            AIBrainServer.Instance.ApplyWaveAI(team, brainId, targetPos, recruits);
            return CommandOutput.Success($"[Manual AI] {brainId} grabbed {recruits.Count} wild units.", recruits);
        }

        return CommandOutput.Success("No wild units found");
    }

    [CommandInfo("hero", "🏗️ 召唤英雄", "Entity", new[] { "HeroID", "Position (x,y)", "Team" },
        Tooltip = "召唤英雄单位喵~\n示例：hero commander 0,0 1",
        Color = "0.3,0.6,0.3")]
    public static CommandOutput Hero(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        int slot = (args.Length >= 1) ? int.Parse(args[0]) : 1;
        var whole = EntitySystem.Instance.wholeComponent;

        if (whole.entityCount > 0)
        {
            int lastIndex = whole.entityCount - 1;
            whole.userControlComponent[lastIndex].HeroSlot = slot;
            whole.coreComponent[lastIndex].Type |= UnitType.Hero;
            whole.coreComponent[lastIndex].Team = 1;

            return CommandOutput.Success($"Unit {lastIndex} is now your Hero (Slot {slot})", whole.coreComponent[lastIndex].SelfHandle);
        }

        return CommandOutput.Fail("No entity found");
    }

    // =========================================================
    // 🔧 System 系统相关命令
    // =========================================================

    [CommandInfo("clear", "🔧 清空单位", "Debug", new[] { "Team (optional)" },
        Tooltip = "清空所有单位或指定阵营单位喵~\n示例：clear 1",
        Color = "0.6,0.3,0.3")]
    public static CommandOutput Clear(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (UserControlSystem.Instance != null)
        {
            UserControlSystem.Instance.ClearAllSelection();
            UserControlSystem.Instance.playerTeam = 1;
        }

        if (AIBrainServer.Instance != null)
        {
            AIBrainServer.Instance.ClearAll();
        }

        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.ClearAll();
        }

        if (IndustrialSystem.Instance != null)
        {
            IndustrialSystem.Instance.GlobalPowerOverride = false;
        }

        if (PowerSystem.Instance != null)
        {
            var method = PowerSystem.Instance.GetType().GetMethod("Clear");
            if (method != null) method.Invoke(PowerSystem.Instance, null);
        }

        if (TransportSystem.Instance != null)
        {
            var method = TransportSystem.Instance.GetType().GetMethod("Clear");
            if (method != null) method.Invoke(TransportSystem.Instance, null);
        }

        if (TimeSystem.Instance != null)
        {
            TimeSystem.Instance.SetPaused(false);
            TimeSystem.Instance.ResetTimer();
        }

        if (PathfindingSystem.Instance != null)
            PathfindingSystem.Instance.Clear();

        if (SelectionOverlaySystem.Instance != null)
            SelectionOverlaySystem.Instance.HideAllSelectionBoxes();

        var whole = EntitySystem.Instance.wholeComponent;
        int w = (whole != null && whole.mapWidth > 0) ? whole.mapWidth : 128;
        int h = (whole != null && whole.mapHeight > 0) ? whole.mapHeight : 128;
        int minX = (whole != null) ? whole.minX : -64;
        int minY = (whole != null) ? whole.minY : -64;
        float cellSize = (GridSystem.Instance != null) ? GridSystem.Instance.CellSize : 1.0f;

        EntitySystem.Instance.Initialize(EntitySystem.Instance.maxEntityCount, w, h, minX, minY, cellSize);

        return CommandOutput.Success("System reset: All entities and gridMap cleared.");
    }

    [CommandInfo("map_load", "🗺️ 加载地图", "Scene", new[] { "MapID" },
        Tooltip = "加载指定地图喵~\n示例：map_load Level_01",
        Color = "0.2,0.4,0.6")]
    public static CommandOutput MapLoad(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (TilemapSyncManager.Instance != null)
        {
            TilemapSyncManager.Instance.SyncFromTilemap();
            return CommandOutput.Success("Map Data loaded from Tilemap to ECS groundMap.");
        }
        else
        {
            return CommandOutput.Fail("Error: TilemapSyncManager instance not found!");
        }
    }

    [CommandInfo("map_apply", "🗺️ 应用地图", "Scene", new[] { "MapID" },
        Tooltip = "应用地图配置喵~\n示例：map_apply Level_01",
        Color = "0.2,0.4,0.5")]
    public static CommandOutput MapApply(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (TilemapSyncManager.Instance != null)
        {
            TilemapSyncManager.Instance.SyncToTilemap();
            return CommandOutput.Success("ECS groundMap applied to Tilemap visual renderer.");
        }
        else
        {
            return CommandOutput.Fail("Error: TilemapSyncManager instance not found!");
        }
    }

    [CommandInfo("save_new", "🗺️ 新建存档", "Scene", new[] { "SaveName" },
        Tooltip = "创建新存档喵~\n示例：save_new my_save",
        Color = "0.2,0.5,0.7")]
    public static CommandOutput SaveNew(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        string name = (args.Length > 0) ? args[0] : "slot_1";
        SaveManager.Instance.CreateNewSave(name);
        return CommandOutput.Success($"Created new save profile: {name}");
    }

    [CommandInfo("save_load", "🗺️ 加载存档", "Scene", new[] { "SaveName" },
        Tooltip = "加载指定存档喵~\n示例：save_load my_save",
        Color = "0.2,0.5,0.6")]
    public static CommandOutput SaveLoad(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        string name = (args.Length > 0) ? args[0] : "slot_1";
        SaveManager.Instance.LoadSave(name);
        return CommandOutput.Success($"Loaded save profile: {name}. Ready to enter stage.");
    }

    [CommandInfo("save_ram", "🗺️ 内存存档", "Scene", null,
        Tooltip = "将当前状态保存到内存喵~",
        Color = "0.3,0.6,0.7")]
    public static CommandOutput SaveRam(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (MainModel.Instance.IsInStage)
        {
            GameFlowController.Instance.SaveCurrentStageFromSystem();
            return CommandOutput.Success("Stage data saved to RAM (UserModel). Not written to disk yet.");
        }
        else
        {
            return CommandOutput.Fail("Not in stage, nothing to save to RAM.");
        }
    }

    [CommandInfo("save_now", "🗺️ 立即存档", "Scene", null,
        Tooltip = "立即保存到磁盘喵~",
        Color = "0.3,0.6,0.6")]
    public static CommandOutput SaveNow(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (MainModel.Instance.IsInStage)
        {
            GameFlowController.Instance.SaveCurrentStageFromSystem();
        }
        SaveManager.Instance.SaveGameToDisk();
        return CommandOutput.Success("Game Saved to Disk.");
    }

    [CommandInfo("enter", "🗺️ 进入关卡", "Scene", new[] { "StageID" },
        Tooltip = "进入指定关卡喵~\n示例：enter Level_01",
        Color = "0.3,0.5,0.7")]
    public static CommandOutput Enter(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            return CommandOutput.Fail("Usage: enter <stage_id> (e.g., Level_01)");
        }

        string stageID = args[0];
        EntitySystem.Instance.LoadStage(stageID);
        return CommandOutput.Success($"Entering stage: {stageID}");
    }

    [CommandInfo("leave", "🗺️ 离开关卡", "Scene", null,
        Tooltip = "离开当前关卡喵~",
        Color = "0.3,0.5,0.6")]
    public static CommandOutput Leave(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        GameFlowController.Instance.ReturnToMap(true);
        return CommandOutput.Success("Exited stage and returned to Map state.");
    }

    [CommandInfo("leave_force", "🗺️ 强制离开", "Scene", null,
        Tooltip = "强制离开当前关卡（不保存）喵~",
        Color = "0.4,0.5,0.6")]
    public static CommandOutput LeaveForce(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        GameFlowController.Instance.ReturnToMap(false);
        return CommandOutput.Success("Exited stage WITHOUT saving.");
    }

    [CommandInfo("reset_stage", "🗺️ 重置关卡", "Scene", new[] { "StageID" },
        Tooltip = "重置关卡到初始状态喵~",
        Color = "0.5,0.3,0.3")]
    public static CommandOutput ResetStage(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        string currentStage = MainModel.Instance.CurrentActiveStageID;
        if (string.IsNullOrEmpty(currentStage))
        {
            return CommandOutput.Fail("Not in any stage!");
        }

        GameFlowController.Instance.ResetStage(currentStage);
        return CommandOutput.Success($"Stage {currentStage} has been reset to default state.");
    }

    [CommandInfo("net_rebuild", "🔧 重建网络", "Debug", null,
        Tooltip = "重建物流网络喵~",
        Color = "0.4,0.2,0.2")]
    public static CommandOutput NetRebuild(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        var whole = EntitySystem.Instance.wholeComponent;
        TransportSystem.Instance.RebuildNetwork(whole);
        return CommandOutput.Success("Transport Network Rebuilt manually.");
    }

    [CommandInfo("net_info", "🔧 网络信息", "Debug", null,
        Tooltip = "显示物流网络信息喵~",
        Color = "0.4,0.2,0.3")]
    public static CommandOutput NetInfo(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        string stats = TransportSystem.Instance.GetNetworkDebugInfo();
        return CommandOutput.Success($"--- Transport Network Status ---\n{stats}");
    }

    // =========================================================
    // 📂 VFS 相关命令
    // =========================================================

    [CommandInfo("vfs_mount", "📂 挂载 VFS", "System", new[] { "PackID" },
        Tooltip = "从 MetaLib 强制挂载 VFS 包（覆盖同名盘）喵~\n示例：vfs_mount social_tree_default",
        Color = "0.3,0.5,0.7")]
    public static CommandOutput VFSMount(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1) return CommandOutput.Fail("Usage: vfs_mount <PackID>");

        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return CommandOutput.Fail("GraphAnalyser 未初始化喵~");

        var template = MetaLib.GetPack<BasePackData>(args[0]);
        if (template == null) return CommandOutput.Fail($"MetaLib 找不到包：{args[0]}");

        var instance = analyser.LoadVFSFromPack(template);
        if (instance == null) return CommandOutput.Fail($"挂载失败：{args[0]}");

        (console as DeveloperConsole)?.SetCurrentPath("/");
        return CommandOutput.Success($"VFS 已挂载：{args[0]}（节点数：{instance.Nodes.Count}）");
    }

    [CommandInfo("vfs_unmount", "🗑️ 卸载 VFS", "System", new[] { "PackID" },
        Tooltip = "从内存卸载指定 VFS 盘喵~\n示例：vfs_unmount social_tree_default",
        Color = "0.5,0.3,0.3")]
    public static CommandOutput VFSUnmount(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1) return CommandOutput.Fail("Usage: vfs_unmount <PackID>");

        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return CommandOutput.Fail("GraphAnalyser 未初始化喵~");

        analyser.UnregisterPack(args[0]);
        return CommandOutput.Success($"已卸载：{args[0]}");
    }

    [CommandInfo("vfs_unmount_all", "🗑️ 卸载所有 VFS", "System", null,
        Tooltip = "从内存卸载全部 VFS 盘喵~",
        Color = "0.5,0.2,0.2")]
    public static CommandOutput VFSUnmountAll(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return CommandOutput.Fail("GraphAnalyser 未初始化喵~");

        foreach (var id in analyser.GetAllPackIds(subjectLevel))
            analyser.UnregisterPack(id);
        return CommandOutput.Success("已卸载全部 VFS 实例");
    }

    [CommandInfo("vfs_save", "💾 保存 VFS 到磁盘", "System", new[] { "PackID" },
        Tooltip = "将 VFS 实例序列化到 StreamingAssets 并注册到 MetaLib 喵~\n示例：vfs_save social_tree_default",
        Color = "0.3,0.6,0.4")]
    public static CommandOutput VFSSave(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1) return CommandOutput.Fail("Usage: vfs_save <PackID>");

        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return CommandOutput.Fail("GraphAnalyser 未初始化喵~");

        var pack = analyser.GetPack(args[0], subjectLevel);
        if (pack == null) return CommandOutput.Fail($"找不到 Pack：{args[0]}");

        string relativePath = $"NekoGraph/{args[0]}.json";
        string fullPath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, relativePath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
        VFSLoader.SavePackToFile(pack, fullPath);

        MetaLib.Register(args[0], new MetaLib.MetaEntry
        {
            PackID = args[0],
            Storage = MetaLib.StorageType.StreamingAssets,
            ResourcePath = relativePath,
            DisplayName = pack.DisplayName ?? args[0]
        });
#if UNITY_EDITOR
        MetaLib.Save();
#endif
        return CommandOutput.Success($"已保存：{args[0]}");
    }


    [CommandInfo("vfs_info", "🔍 VFS 信息", "System", null,
        Tooltip = "显示 VFS 系统信息喵~",
        Color = "0.5,0.5,0.5")]
    public static CommandOutput VFSInfo(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        var analyser = GraphAnalyser.Instance;
        if (analyser == null)
        {
            return CommandOutput.Fail("GraphAnalyser 未初始化喵~\n请使用 vfs_mount 命令加载 VFS 数据包");
        }

        if (analyser.GetAllPackIds(subjectLevel).Count == 0)
        {
            return CommandOutput.Fail("未加载任何 VFS 实例喵~\n请使用 vfs_mount 命令加载 VFS 数据包");
        }

        return CommandOutput.Success((GameObject.FindFirstObjectByType<SocialCLI>()).GetVFSDebugInfo());
    }

    // =========================================================
    // 💬 Social 社交相关命令
    // =========================================================

    [CommandInfo("social_send", "💬 发送社交消息", "Social", new[] { "PackID", "VFSPath (optional)", "Sender (optional)" },
        Tooltip = "发送一条社交消息给玩家喵~\n示例：social_send event_01 /social/inbox/msg01.msg 指挥官",
        Color = "0.3,0.5,0.8")]
    public static CommandOutput SocialSend(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            return CommandOutput.Fail("Usage: social_send <PackID> [VFSPath] [Sender]");
        }

        string packID = args[0];
        string vfsPath = (args.Length > 1) ? args[1] : null;
        string sender = (args.Length > 2) ? args[2] : "系统";

        if (SocialManager.Instance == null)
        {
            return CommandOutput.Fail("SocialManager 实例不存在喵~");
        }

        SocialManager.Instance.SendMessage(packID, sender, vfsPath);
        return CommandOutput.Success($"社交消息已发送：PackID={packID}, Sender={sender}, Path={(vfsPath ?? "默认")} 喵~");
    }

    [CommandInfo("social_markread", "💬 标记消息已读", "Social", new[] { "VFSPath" },
        Tooltip = "标记某条社交消息为已读喵~\n示例：social_markread /social/messages/event_01.msg",
        Color = "0.3,0.6,0.7")]
    public static CommandOutput SocialMarkRead(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            return CommandOutput.Fail("Usage: social_markread <VFSPath>");
        }

        string vfsPath = args[0];

        if (SocialManager.Instance == null)
        {
            return CommandOutput.Fail("SocialManager 实例不存在喵~");
        }

        SocialManager.Instance.MarkAsRead(vfsPath);
        return CommandOutput.Success($"消息已标记为已读：{vfsPath} 喵~");
    }

    // =========================================================
    // 🎮 Mission 任务相关命令
    // =========================================================

    [CommandInfo("pack_run", "📦 运行 Pack", "Mission", new[] { "PackID" },
        Tooltip = "加载并运行 Pack 到 GraphRunner 喵~\n示例：pack_run tutorial_missions",
        Color = "0.6,0.4,0.2")]
    public static CommandOutput PackRun(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1) return CommandOutput.Fail("Usage: pack_run <PackID>");

        string packID = args[0];

        // 使用新的 GraphRunner 系统加载并运行 Pack 喵~
        var pack = MetaLib.GetPack<BasePackData>(packID);
        if (pack != null)
        {
            if (GraphRunner.Instance != null)
            {
                GraphRunner.Instance.SetPackDataDict(MainModel.Instance.CurrentUser.PackDataDict);
                var instanceID = GraphRunner.Instance.LoadPack(pack);
                GraphRunner.Instance.InjectSignalFromRoot(instanceID);
                return CommandOutput.Success($"Pack 已加载并运行：{packID} → InstanceID: {instanceID}");
            }
        }

        return CommandOutput.Fail($"Pack 加载失败：{packID}");
    }

    [CommandInfo("mission_skip", "🎮 跳过任务", "Mission", new[] { "MissionID" },
        Tooltip = "跳过指定任务喵~\n示例：mission_skip mission_01",
        Color = "0.5,0.4,0.2")]
    public static CommandOutput MissionSkip(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        // TODO: 新任务系统的跳过逻辑待实现
        Log(console, "[新系统] 任务跳过功能暂不支持喵~", Color.yellow);
        return CommandOutput.Fail("[新系统] 任务跳过功能暂不支持");
    }

    /*[CommandInfo("mission_info", "🎮 任务信息", "Mission", new[] { "MissionID" },
        Tooltip = "显示任务详细信息喵~\n示例：mission_info mission_01",
        Color = "0.6,0.5,0.2")]
    public static CommandResult MissionInfo(DeveloperConsole console, string[] args)
    {
        if (MissionManager.Instance.ActiveMissions.Count == 0)
        {
            Log(console, "No mission pack loaded.", Color.gray);
            return CommandResult.Success;
        }

        foreach (var m in MissionManager.Instance.ActiveMissions)
        {
            string state = m.IsCompleted ? "<color=green>[DONE]</color>" :
                           (m.IsActive ? "<color=yellow>[ACTIVE]</color>" : "<color=gray>[LOCKED]</color>");
            Log(console, $"{state} {m.Title} (ID: {m.MissionID})", Color.white);

            if (m.IsActive && !m.IsCompleted)
            {
                foreach (var g in m.Goals)
                    Log(console, $"   >> {g.Type}: {g.CurrentAmount}/{g.RequiredAmount}", Color.cyan);
            }
        }
        return CommandResult.Success;
    }*/

    [CommandInfo("system_help", "⚡ 系统帮助", "System", new[] { "Command (optional)" },
        Tooltip = "显示帮助信息或指定命令的用法喵~\n示例：help spawn",
        Color = "0.5,0.5,0.5")]
    public static CommandOutput SystemHelp(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        string helpText = "RTS Commands:\n";
        var devConsole = console as DeveloperConsole;
        var keys = devConsole?.GetCommandKeys() ?? CommandRegistry.GetAllCommandNames();
        foreach (var command in keys)
            helpText += $"- {command}\n";
        return CommandOutput.Success(helpText);
    }

    [CommandInfo("cheat_gold", "🔧 金币作弊", "Debug", new[] { "Amount" },
        Tooltip = "获得指定数量金币喵~\n示例：cheat_gold 1000",
        Color = "0.6,0.2,0.2")]
    public static CommandOutput CheatGold(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1) return CommandOutput.Fail("Usage: cheat_gold <amount>");

        int amount = int.Parse(args[0]);
        IndustrialSystem.Instance.AddGold(amount);
        return CommandOutput.Success($"Gold added: {amount}. Mission should react!");
    }

    [CommandInfo("cheat_power", "⚡ 无限电力", "Debug", new[] { "Enable (0/1)" },
        Tooltip = "开启/关闭无限电力喵~\n示例：cheat_power 1",
        Color = "0.6,0.2,0.3")]
    public static CommandOutput CheatPower(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            bool current = IndustrialSystem.Instance.GlobalPowerOverride;
            string status = current ? "ON" : "OFF";
            return CommandOutput.Success($"Global Power Override is: {status}");
        }

        bool enable = args[0] == "1" || args[0].ToLower() == "true";
        IndustrialSystem.Instance.GlobalPowerOverride = enable;

        if (enable)
        {
            return CommandOutput.Success("⚡ UNLIMITED POWER! All buildings are now active without electricity.");
        }
        else
        {
            return CommandOutput.Success("⚡ Power restrictions restored. Build more generators!");
        }
    }

    [CommandInfo("global_power", "⚡ 全局电力", "System", new[] { "Enable (0/1)" },
        Tooltip = "全局电力覆盖开关喵~\n示例：global_power 1",
        Color = "0.4,0.4,0.4")]
    public static CommandOutput GlobalPower(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            return CommandOutput.Fail("Usage: global_power <0|1>");
        }

        bool enable = args[0] == "1" || args[0].ToLower() == "true";
        if (IndustrialSystem.Instance != null)
        {
            IndustrialSystem.Instance.GlobalPowerOverride = enable;
            return CommandOutput.Success($"[CommandExecutor] 全局电力 {(enable ? "开启" : "关闭")} 喵~");
        }
        return CommandOutput.Fail("IndustrialSystem.Instance is null");
    }

    // =========================================================
    // ⏰ Time 时间相关命令
    // =========================================================

    [CommandInfo("timer_pause", "⏰ 暂停时间", "Time", null,
        Tooltip = "暂停游戏时间喵~",
        Color = "0.6,0.6,0.2")]
    public static CommandOutput TimerPause(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        TimeSystem.Instance.SetPaused(true);
        return CommandOutput.Success("Time System: Paused.");
    }

    [CommandInfo("timer_resume", "⏰ 恢复时间", "Time", null,
        Tooltip = "恢复游戏时间喵~",
        Color = "0.5,0.5,0.2")]
    public static CommandOutput TimerResume(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        TimeSystem.Instance.SetPaused(false);
        return CommandOutput.Success("Time System: Resumed.");
    }

    [CommandInfo("timer_reset", "⏰ 重置时间", "Time", null,
        Tooltip = "重置计时器为 0 喵~",
        Color = "0.6,0.5,0.2")]
    public static CommandOutput TimerReset(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        TimeSystem.Instance.ResetTimer();
        return CommandOutput.Success("Time System: Timer Reset to 0.");
    }

    [CommandInfo("timer_skip", "⏰ 时间快进", "Time", new[] { "Seconds" },
        Tooltip = "跳过指定秒数喵~\n示例：timer_skip 60",
        Color = "0.5,0.6,0.2")]
    public static CommandOutput TimerSkip(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length > 0 && int.TryParse(args[0], out int seconds))
        {
            var missionArgs = MissionArgs.Get();
            missionArgs.Amount = seconds;
            missionArgs.StringKey = "Seconds";
            PostSystem.Instance.Send("生存时间增加", missionArgs);
            MissionArgs.Release(missionArgs);

            return CommandOutput.Success($"Time System: Skipped {seconds}s for mission goals.");
        }
        return CommandOutput.Fail("Invalid seconds value");
    }

    [CommandInfo("nav_info", "🔧 导航信息", "Debug", null,
        Tooltip = "显示 NavMesh 调试信息喵~",
        Color = "0.5,0.2,0.2")]
    public static CommandOutput NavInfo(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        string stats = GridSystem.Instance.GetNavMeshDebugInfo();
        return CommandOutput.Success($"--- NavMesh & Portal Topology Status ---\n{stats}");
    }

    // =========================================================
    // 🎬 Story 剧情相关命令
    // =========================================================

    [CommandInfo("PlayCG", "🎬 播放 CG", "Story", new[] { "CGName" },
        Tooltip = "播放过场动画/CG 喵~\n示例：PlayCG ending_01",
        Color = "0.6,0.3,0.6")]
    public static CommandOutput PlayCG(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            return CommandOutput.Fail("PlayCG 命令需要 1 个参数：CGName");
        }

        string cgName = args[0];
        return CommandOutput.Success($"[CommandExecutor] 播放 CG: {cgName}");
    }

    [CommandInfo("ShowDialogue", "🎬 显示对话", "Story", new[] { "DialogueID", "Speaker", "Text" },
        Tooltip = "显示剧情对话喵~\n示例：ShowDialogue intro_01 指挥官 这里是……哪里？",
        Color = "0.5,0.3,0.5")]
    public static CommandOutput ShowDialogue(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 3)
        {
            return CommandOutput.Fail("ShowDialogue 命令需要 3 个参数：DialogueID, Speaker, Text");
        }

        string dialogueId = args[0];
        string speaker = args[1];
        string text = args[2];

        return CommandOutput.Success($"[CommandExecutor] 显示对话 [{speaker}]: {text}");
    }

    [CommandInfo("UnlockStage", "🎬 解锁章节", "Story", new[] { "StageID" },
        Tooltip = "解锁新的剧情章节喵~\n示例：UnlockStage chapter_02",
        Color = "0.6,0.4,0.6")]
    public static CommandOutput UnlockStage(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            return CommandOutput.Fail("UnlockStage 命令需要 1 个参数：StageID");
        }

        string stageId = args[0];
        return CommandOutput.Success($"[CommandExecutor] 解锁章节：{stageId}");
    }

    // =========================================================
    // 🔧 Camera 相机相关命令
    // =========================================================

    [CommandInfo("cam_home", "🔧 相机归位", "Debug", null,
        Tooltip = "相机回到地图中心喵~",
        Color = "0.5,0.2,0.3")]
    public static CommandOutput CamHome(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        CameraController.Instance.GoToOrigin();
        return CommandOutput.Success("Camera returned to map center.");
    }

    [CommandInfo("cam_goto", "🔧 相机移动", "Debug", new[] { "Position (x,y)" },
        Tooltip = "相机移动到指定位置喵~\n示例：cam_goto 50,50",
        Color = "0.5,0.2,0.4")]
    public static CommandOutput CamGoto(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            return CommandOutput.Fail("Usage: cam_goto <x,y>");
        }

        Vector2Int gridPos = ParseGridPos(args[0]);
        Vector2 worldPos = GridSystem.Instance.GridToWorld(gridPos, Vector2Int.one);
        CameraController.Instance.FocusOn(worldPos);
        return CommandOutput.Success($"Camera focused on Grid {gridPos}");
    }

    [CommandInfo("cam_sync", "🔧 相机同步", "Debug", null,
        Tooltip = "同步相机边界喵~",
        Color = "0.5,0.3,0.3")]
    public static CommandOutput CamSync(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        CameraController.Instance.SyncBounds();
        return CommandOutput.Success("Camera bounds re-synchronized with WholeComponent.");
    }

    [CommandInfo("cam_reset", "🔧 相机重置", "Debug", null,
        Tooltip = "重置相机设置喵~",
        Color = "0.5,0.3,0.4")]
    public static CommandOutput CamReset(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        CameraController.Instance.ResetZoom();
        return CommandOutput.Success("Camera zoom reset to default.");
    }

    [CommandInfo("cam_speed", "🔧 相机速度", "Debug", new[] { "Speed" },
        Tooltip = "设置相机移动速度喵~\n示例：cam_speed 5",
        Color = "0.5,0.3,0.5")]
    public static CommandOutput CamSpeed(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1) return CommandOutput.Success("Usage: cam_speed <speed>");

        float speed = float.Parse(args[0]);
        CameraController.Instance.moveSpeed = speed;
        return CommandOutput.Success($"Camera move speed set to {speed}");
    }

    [CommandInfo("cam_scroll", "🔧 相机滚动", "Debug", new[] { "Enable (0/1)" },
        Tooltip = "开启/关闭相机滚动喵~\n示例：cam_scroll 1",
        Color = "0.5,0.3,0.6")]
    public static CommandOutput CamScroll(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1) return CommandOutput.Success("Usage: cam_scroll <0|1>");

        bool enable = args[0] == "1";
        CameraController.Instance.useEdgeScrolling = enable;
        return CommandOutput.Success($"Edge scrolling: {(enable ? "Enabled" : "Disabled")}");
    }

    // =========================================================
    // 🖼️ UI 界面相关命令
    // =========================================================

    [CommandInfo("ui_root", "🖼️ 进入根界面", "UI", null,
        Tooltip = "发送\"进入根界面\"事件，测试大地图 Canvas 淡入喵~",
        Color = "0.3,0.5,0.7")]
    public static CommandOutput UIRoot(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        PostSystem.Instance.Send("进入根界面", null);
        return CommandOutput.Success("已发送 [进入根界面] 事件喵~");
    }

    [CommandInfo("ui_hide_all", "🖼️ 隐藏所有面板", "UI", null,
        Tooltip = "发送\"期望隐藏所有面板\"事件，测试所有面板淡出喵~",
        Color = "0.5,0.3,0.3")]
    public static CommandOutput UIHideAll(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        PostSystem.Instance.Send("期望隐藏所有面板", null);
        return CommandOutput.Success("已发送 [期望隐藏所有面板] 事件喵~");
    }

    [CommandInfo("ui_show", "🖼️ 显示面板", "UI", new[] { "UI_ID" },
        Tooltip = "发送\"期望显示面板\"事件，测试指定面板淡入喵~\n示例：ui_show NodeInfoPanel",
        Color = "0.3,0.6,0.3")]
    public static CommandOutput UIShow(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            return CommandOutput.Fail("Usage: ui_show <UI_ID> (e.g., NodeInfoPanel)");
        }
        string uiID = args[0];
        PostSystem.Instance.Send("期望显示面板", uiID);
        return CommandOutput.Success($"已发送 [期望显示面板] 事件，ID: {uiID} 喵~");
    }

    [CommandInfo("ui_hide", "🖼️ 隐藏面板", "UI", new[] { "UI_ID" },
        Tooltip = "发送\"期望隐藏面板\"事件，测试指定面板淡出喵~\n示例：ui_hide NodeInfoPanel",
        Color = "0.6,0.3,0.3")]
    public static CommandOutput UIHide(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        if (subjectLevel < PackAccessSubjects.SystemMin)
            return CommandOutput.Fail("权限不足：此命令需要 System 级别权限喵~");
        if (args.Length < 1)
        {
            return CommandOutput.Fail("Usage: ui_hide <UI_ID> (e.g., NodeInfoPanel)");
        }
        string uiID = args[0];
        PostSystem.Instance.Send("期望隐藏面板", uiID);
        return CommandOutput.Success($"已发送 [期望隐藏面板] 事件，ID: {uiID} 喵~");
    }
}
