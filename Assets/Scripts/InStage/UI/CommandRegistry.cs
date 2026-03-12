using System;
using System.Collections.Generic;
using AIBrain;
using UnityEngine;

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
public static partial class CommandRegistry
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

    private static void Log(DeveloperConsole console, string message, Color color)
    {
        console?.Log(message, color);
    }

    // =========================================================
    // 🏗️ Entity 实体相关命令
    // =========================================================

    [CommandInfo("spawn", "🏗️ 召唤单位", "Entity", new[] { "BlueprintID", "Position (x,y)", "Team" },
        Tooltip = "在指定位置召唤单个单位喵~\n示例：spawn x_dog 0,0 1",
        Color = "0.2,0.6,0.2")]
    public static CommandResult Spawn(DeveloperConsole console, string[] args)
    {
        if (args.Length < 3)
        {
            Log(console, "Usage: spawn <key> <x,y> <team> [aiType] [dir_x,y]", Color.red);
            return CommandResult.Failed;
        }

        string key = args[0].ToLower();
        Vector2Int pos = ParseGridPos(args[1]);
        int team = int.Parse(args[2]);

        EntityBlueprint bp = BlueprintRegistry.Get(key);
        if (string.IsNullOrEmpty(bp.Name))
        {
            Log(console, $"Unknown blueprint: {key}", Color.red);
            return CommandResult.Failed;
        }

        if (!GridSystem.Instance.IsAreaClear(pos, bp.LogicSize))
        {
            Log(console, $"Area {pos} is blocked!", Color.yellow);
            return CommandResult.Failed;
        }

        EntityHandle handle = EntitySystem.Instance.CreateEntityFromBlueprint(key, pos, team);
        int idx = EntitySystem.Instance.GetIndex(handle);

        if (idx != -1 && args.Length >= 4)
        {
            EntitySystem.Instance.wholeComponent.coreComponent[idx].Rotation = ParseGridPos(args[4]);
        }

        Log(console, $"Successfully spawned {bp.Name} (Team {team})", Color.green);
        return CommandResult.Success;
    }

    [CommandInfo("army", "🏗️ 方阵召唤", "Entity", new[] { "BlueprintID", "Center (x,y)", "Width", "Height", "Team" },
        Tooltip = "以方阵形式召唤多个单位喵~\n示例：army x_dog 0,0 3,3 1",
        Color = "0.2,0.5,0.2")]
    public static CommandResult Army(DeveloperConsole console, string[] args)
    {
        if (args.Length < 4)
        {
            Log(console, "Usage: army <key> <x,y> <w,h> <team>", Color.red);
            return CommandResult.Failed;
        }

        string key = args[0];
        Vector2Int center = ParseGridPos(args[1]);
        Vector2Int groupSize = ParseGridPos(args[2]);
        int team = int.Parse(args[3]);

        EntityBlueprint bp = BlueprintRegistry.Get(key);
        int startX = center.x - groupSize.x / 2;
        int startY = center.y - groupSize.y / 2;
        int count = 0;

        for (int x = 0; x < groupSize.x; x++)
        {
            for (int y = 0; y < groupSize.y; y++)
            {
                Vector2Int current = new Vector2Int(startX + x, startY + y);
                if (GridSystem.Instance.IsAreaClear(current, bp.LogicSize))
                {
                    EntitySystem.Instance.CreateEntityFromBlueprint(key, current, team);
                    count++;
                }
            }
        }

        Log(console, $"Army: {count} {bp.Name}s deployed for Team {team}", Color.green);
        return CommandResult.Success;
    }

    [CommandInfo("ai_wave", "🏗️ AI 波次绑定", "Entity", new[] { "Team", "BrainID", "TargetPos (x,y)" },
        Tooltip = "将单位绑定到 AI 波次逻辑喵~\n示例：ai_wave 1 Red_Dot_Wave 0,0",
        Color = "0.2,0.4,0.2")]
    public static CommandResult AiWave(DeveloperConsole console, string[] args)
    {
        if (args.Length < 3) return CommandResult.Failed;

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
            Log(console, $"[Manual AI] {brainId} grabbed {recruits.Count} wild units.", Color.magenta);
        }

        return CommandResult.Success;
    }

    [CommandInfo("hero", "🏗️ 召唤英雄", "Entity", new[] { "HeroID", "Position (x,y)", "Team" },
        Tooltip = "召唤英雄单位喵~\n示例：hero commander 0,0 1",
        Color = "0.3,0.6,0.3")]
    public static CommandResult Hero(DeveloperConsole console, string[] args)
    {
        int slot = (args.Length >= 1) ? int.Parse(args[0]) : 1;
        var whole = EntitySystem.Instance.wholeComponent;

        if (whole.entityCount > 0)
        {
            int lastIndex = whole.entityCount - 1;
            whole.userControlComponent[lastIndex].HeroSlot = slot;
            whole.coreComponent[lastIndex].Type |= UnitType.Hero;
            whole.coreComponent[lastIndex].Team = 1;

            Log(console, $"Unit {lastIndex} is now your Hero (Slot {slot})", Color.cyan);
        }

        return CommandResult.Success;
    }

    // =========================================================
    // 🔧 System 系统相关命令
    // =========================================================

    [CommandInfo("clear", "🔧 清空单位", "Debug", new[] { "Team (optional)" },
        Tooltip = "清空所有单位或指定阵营单位喵~\n示例：clear 1",
        Color = "0.6,0.3,0.3")]
    public static CommandResult Clear(DeveloperConsole console, string[] args)
    {
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
        Log(console, "System reset: All entities and gridMap cleared.", Color.yellow);

        return CommandResult.Success;
    }

    [CommandInfo("map_load", "🗺️ 加载地图", "Scene", new[] { "MapID" },
        Tooltip = "加载指定地图喵~\n示例：map_load Level_01",
        Color = "0.2,0.4,0.6")]
    public static CommandResult MapLoad(DeveloperConsole console, string[] args)
    {
        if (TilemapSyncManager.Instance != null)
        {
            TilemapSyncManager.Instance.SyncFromTilemap();
            Log(console, "Map Data loaded from Tilemap to ECS groundMap.", Color.green);
        }
        else
        {
            Log(console, "Error: TilemapSyncManager instance not found!", Color.red);
        }
        return CommandResult.Success;
    }

    [CommandInfo("map_apply", "🗺️ 应用地图", "Scene", new[] { "MapID" },
        Tooltip = "应用地图配置喵~\n示例：map_apply Level_01",
        Color = "0.2,0.4,0.5")]
    public static CommandResult MapApply(DeveloperConsole console, string[] args)
    {
        if (TilemapSyncManager.Instance != null)
        {
            TilemapSyncManager.Instance.SyncToTilemap();
            Log(console, "ECS groundMap applied to Tilemap visual renderer.", Color.cyan);
        }
        else
        {
            Log(console, "Error: TilemapSyncManager instance not found!", Color.red);
        }
        return CommandResult.Success;
    }

    [CommandInfo("save_new", "🗺️ 新建存档", "Scene", new[] { "SaveName" },
        Tooltip = "创建新存档喵~\n示例：save_new my_save",
        Color = "0.2,0.5,0.7")]
    public static CommandResult SaveNew(DeveloperConsole console, string[] args)
    {
        string name = (args.Length > 0) ? args[0] : "slot_1";
        SaveManager.Instance.CreateNewSave(name);
        Log(console, $"Created new save profile: {name}", Color.green);
        return CommandResult.Success;
    }

    [CommandInfo("save_load", "🗺️ 加载存档", "Scene", new[] { "SaveName" },
        Tooltip = "加载指定存档喵~\n示例：save_load my_save",
        Color = "0.2,0.5,0.6")]
    public static CommandResult SaveLoad(DeveloperConsole console, string[] args)
    {
        string name = (args.Length > 0) ? args[0] : "slot_1";
        SaveManager.Instance.LoadSave(name);
        Log(console, $"Loaded save profile: {name}. Ready to enter stage.", Color.green);
        return CommandResult.Success;
    }

    [CommandInfo("save_ram", "🗺️ 内存存档", "Scene", null,
        Tooltip = "将当前状态保存到内存喵~",
        Color = "0.3,0.6,0.7")]
    public static CommandResult SaveRam(DeveloperConsole console, string[] args)
    {
        if (MainModel.Instance.IsInStage)
        {
            GameFlowController.Instance.SaveCurrentStageFromSystem();
            Log(console, "Stage data saved to RAM (UserModel). Not written to disk yet.", Color.yellow);
        }
        else
        {
            Log(console, "Not in stage, nothing to save to RAM.", Color.red);
        }
        return CommandResult.Success;
    }

    [CommandInfo("save_now", "🗺️ 立即存档", "Scene", null,
        Tooltip = "立即保存到磁盘喵~",
        Color = "0.3,0.6,0.6")]
    public static CommandResult SaveNow(DeveloperConsole console, string[] args)
    {
        if (MainModel.Instance.IsInStage)
        {
            GameFlowController.Instance.SaveCurrentStageFromSystem();
        }
        SaveManager.Instance.SaveGameToDisk();
        Log(console, "Game Saved to Disk.", Color.cyan);
        return CommandResult.Success;
    }

    [CommandInfo("enter", "🗺️ 进入关卡", "Scene", new[] { "StageID" },
        Tooltip = "进入指定关卡喵~\n示例：enter Level_01",
        Color = "0.3,0.5,0.7")]
    public static CommandResult Enter(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1)
        {
            Log(console, "Usage: enter <stage_id> (e.g., Level_01)", Color.red);
            return CommandResult.Failed;
        }

        string stageID = args[0];
        EntitySystem.Instance.LoadStage(stageID);
        Log(console, $"Entering stage: {stageID}", Color.yellow);
        return CommandResult.Success;
    }

    [CommandInfo("leave", "🗺️ 离开关卡", "Scene", null,
        Tooltip = "离开当前关卡喵~",
        Color = "0.3,0.5,0.6")]
    public static CommandResult Leave(DeveloperConsole console, string[] args)
    {
        GameFlowController.Instance.ReturnToMap(true);
        Log(console, "Exited stage and returned to Map state.", Color.green);
        return CommandResult.Success;
    }

    [CommandInfo("leave_force", "🗺️ 强制离开", "Scene", null,
        Tooltip = "强制离开当前关卡（不保存）喵~",
        Color = "0.4,0.5,0.6")]
    public static CommandResult LeaveForce(DeveloperConsole console, string[] args)
    {
        GameFlowController.Instance.ReturnToMap(false);
        Log(console, "Exited stage WITHOUT saving.", Color.red);
        return CommandResult.Success;
    }

    [CommandInfo("reset_stage", "🗺️ 重置关卡", "Scene", new[] { "StageID" },
        Tooltip = "重置关卡到初始状态喵~",
        Color = "0.5,0.3,0.3")]
    public static CommandResult ResetStage(DeveloperConsole console, string[] args)
    {
        string currentStage = MainModel.Instance.CurrentActiveStageID;
        if (string.IsNullOrEmpty(currentStage))
        {
            Log(console, "Not in any stage!", Color.red);
            return CommandResult.Failed;
        }

        GameFlowController.Instance.ResetStage(currentStage);
        Log(console, $"Stage {currentStage} has been reset to default state.", Color.red);
        return CommandResult.Success;
    }

    [CommandInfo("net_rebuild", "🔧 重建网络", "Debug", null,
        Tooltip = "重建物流网络喵~",
        Color = "0.4,0.2,0.2")]
    public static CommandResult NetRebuild(DeveloperConsole console, string[] args)
    {
        var whole = EntitySystem.Instance.wholeComponent;
        TransportSystem.Instance.RebuildNetwork(whole);
        Log(console, "Transport Network Rebuilt manually.", Color.cyan);
        return CommandResult.Success;
    }

    [CommandInfo("net_info", "🔧 网络信息", "Debug", null,
        Tooltip = "显示物流网络信息喵~",
        Color = "0.4,0.2,0.3")]
    public static CommandResult NetInfo(DeveloperConsole console, string[] args)
    {
        string stats = TransportSystem.Instance.GetNetworkDebugInfo();
        Log(console, "--- Transport Network Status ---", Color.magenta);
        Log(console, stats, Color.white);
        return CommandResult.Success;
    }

    // =========================================================
    // 🎮 Mission 任务相关命令
    // =========================================================

    /*[CommandInfo("mission_load", "🎮 加载任务", "Mission", new[] { "MissionPackID" },
        Tooltip = "加载任务包喵~\n示例：mission_load tutorial_missions",
        Color = "0.6,0.4,0.2")]
    public static CommandResult MissionLoad(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1) return CommandResult.Failed;

        string path = args[0];
        // bool append = (args.Length > 1 && args[1] == "1");  // 旧系统参数，暂不支持
        
        // 使用新的 GraphRunner 系统加载任务包喵~
        var pack = GraphLoader.LoadPackFromResources(path);
        if (pack != null)
        {
            var instance = GraphLoader.LoadFromPack(pack, null, "Mission");
            if (instance != null && GraphRunner.Instance != null)
            {
                GraphRunner.Instance.RegisterInstance(instance);
                Log(console, $"[新系统] 任务包已加载：{path}", Color.green);
                return CommandResult.Success;
            }
        }
        
        Log(console, $"[新系统] 任务包加载失败：{path}", Color.red);
        return CommandResult.Failed;
    }

    [CommandInfo("mission_skip", "🎮 跳过任务", "Mission", new[] { "MissionID" },
        Tooltip = "跳过指定任务喵~\n示例：mission_skip mission_01",
        Color = "0.5,0.4,0.2")]
    public static CommandResult MissionSkip(DeveloperConsole console, string[] args)
    {
        // TODO: 新任务系统的跳过逻辑待实现
        Log(console, "[新系统] 任务跳过功能暂不支持喵~", Color.yellow);
        return CommandResult.Failed;
    }

    [CommandInfo("mission_info", "🎮 任务信息", "Mission", new[] { "MissionID" },
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

    [CommandInfo("help", "⚡ 帮助", "System", new[] { "Command (optional)" },
        Tooltip = "显示帮助信息或指定命令的用法喵~\n示例：help spawn",
        Color = "0.5,0.5,0.5")]
    public static CommandResult Help(DeveloperConsole console, string[] args)
    {
        Log(console, "RTS Commands:", Color.magenta);
        foreach (var command in console.GetCommandKeys())
            Log(console, $"- {command}", Color.white);
        return CommandResult.Success;
    }

    [CommandInfo("cheat_gold", "🔧 金币作弊", "Debug", new[] { "Amount" },
        Tooltip = "获得指定数量金币喵~\n示例：cheat_gold 1000",
        Color = "0.6,0.2,0.2")]
    public static CommandResult CheatGold(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1) return CommandResult.Failed;

        int amount = int.Parse(args[0]);
        IndustrialSystem.Instance.AddGold(amount);
        Log(console, $"Gold added: {amount}. Mission should react!", Color.green);
        return CommandResult.Success;
    }

    [CommandInfo("cheat_power", "⚡ 无限电力", "Debug", new[] { "Enable (0/1)" },
        Tooltip = "开启/关闭无限电力喵~\n示例：cheat_power 1",
        Color = "0.6,0.2,0.3")]
    public static CommandResult CheatPower(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1)
        {
            bool current = IndustrialSystem.Instance.GlobalPowerOverride;
            Log(console, $"Global Power Override is: {(current ? "<color=green>ON</color>" : "<color=red>OFF</color>")}", Color.white);
            return CommandResult.Success;
        }

        bool enable = args[0] == "1" || args[0].ToLower() == "true";
        IndustrialSystem.Instance.GlobalPowerOverride = enable;

        if (enable)
        {
            Log(console, "⚡ UNLIMITED POWER! All buildings are now active without electricity.", Color.green);
        }
        else
        {
            Log(console, "⚡ Power restrictions restored. Build more generators!", Color.yellow);
        }
        return CommandResult.Success;
    }

    [CommandInfo("global_power", "⚡ 全局电力", "System", new[] { "Enable (0/1)" },
        Tooltip = "全局电力覆盖开关喵~\n示例：global_power 1",
        Color = "0.4,0.4,0.4")]
    public static CommandResult GlobalPower(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1)
        {
            Log(console, "Usage: global_power <0|1>", Color.red);
            return CommandResult.Failed;
        }

        bool enable = args[0] == "1" || args[0].ToLower() == "true";
        if (IndustrialSystem.Instance != null)
        {
            IndustrialSystem.Instance.GlobalPowerOverride = enable;
            Log(console, $"[CommandExecutor] 全局电力 {(enable ? "开启" : "关闭")} 喵~", Color.white);
        }
        return CommandResult.Success;
    }

    // =========================================================
    // ⏰ Time 时间相关命令
    // =========================================================

    [CommandInfo("timer_pause", "⏰ 暂停时间", "Time", null,
        Tooltip = "暂停游戏时间喵~",
        Color = "0.6,0.6,0.2")]
    public static CommandResult TimerPause(DeveloperConsole console, string[] args)
    {
        TimeSystem.Instance.SetPaused(true);
        Log(console, "Time System: Paused.", Color.yellow);
        return CommandResult.Success;
    }

    [CommandInfo("timer_resume", "⏰ 恢复时间", "Time", null,
        Tooltip = "恢复游戏时间喵~",
        Color = "0.5,0.5,0.2")]
    public static CommandResult TimerResume(DeveloperConsole console, string[] args)
    {
        TimeSystem.Instance.SetPaused(false);
        Log(console, "Time System: Resumed.", Color.green);
        return CommandResult.Success;
    }

    [CommandInfo("timer_reset", "⏰ 重置时间", "Time", null,
        Tooltip = "重置计时器为 0 喵~",
        Color = "0.6,0.5,0.2")]
    public static CommandResult TimerReset(DeveloperConsole console, string[] args)
    {
        TimeSystem.Instance.ResetTimer();
        Log(console, "Time System: Timer Reset to 0.", Color.cyan);
        return CommandResult.Success;
    }

    [CommandInfo("timer_skip", "⏰ 时间快进", "Time", new[] { "Seconds" },
        Tooltip = "跳过指定秒数喵~\n示例：timer_skip 60",
        Color = "0.5,0.6,0.2")]
    public static CommandResult TimerSkip(DeveloperConsole console, string[] args)
    {
        if (args.Length > 0 && int.TryParse(args[0], out int seconds))
        {
            var missionArgs = MissionArgs.Get();
            missionArgs.Amount = seconds;
            missionArgs.StringKey = "Seconds";
            PostSystem.Instance.Send("生存时间增加", missionArgs);
            MissionArgs.Release(missionArgs);

            Log(console, $"Time System: Skipped {seconds}s for mission goals.", Color.magenta);
        }
        return CommandResult.Success;
    }

    [CommandInfo("nav_info", "🔧 导航信息", "Debug", null,
        Tooltip = "显示 NavMesh 调试信息喵~",
        Color = "0.5,0.2,0.2")]
    public static CommandResult NavInfo(DeveloperConsole console, string[] args)
    {
        string stats = GridSystem.Instance.GetNavMeshDebugInfo();
        Log(console, "--- NavMesh & Portal Topology Status ---", Color.cyan);
        Log(console, stats, Color.white);
        return CommandResult.Success;
    }

    // =========================================================
    // 🎬 Story 剧情相关命令
    // =========================================================

    [CommandInfo("PlayCG", "🎬 播放 CG", "Story", new[] { "CGName" },
        Tooltip = "播放过场动画/CG 喵~\n示例：PlayCG ending_01",
        Color = "0.6,0.3,0.6")]
    public static CommandResult PlayCG(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1)
        {
            Log(console, "PlayCG 命令需要 1 个参数：CGName", Color.yellow);
            return CommandResult.Failed;
        }

        string cgName = args[0];
        Log(console, $"[CommandExecutor] 播放 CG: {cgName}", Color.white);
        return CommandResult.Success;
    }

    [CommandInfo("ShowDialogue", "🎬 显示对话", "Story", new[] { "DialogueID", "Speaker", "Text" },
        Tooltip = "显示剧情对话喵~\n示例：ShowDialogue intro_01 指挥官 这里是……哪里？",
        Color = "0.5,0.3,0.5")]
    public static CommandResult ShowDialogue(DeveloperConsole console, string[] args)
    {
        if (args.Length < 3)
        {
            Log(console, "ShowDialogue 命令需要 3 个参数：DialogueID, Speaker, Text", Color.yellow);
            return CommandResult.Failed;
        }

        string dialogueId = args[0];
        string speaker = args[1];
        string text = args[2];

        Log(console, $"[CommandExecutor] 显示对话 [{speaker}]: {text}", Color.white);
        return CommandResult.Success;
    }

    [CommandInfo("UnlockStage", "🎬 解锁章节", "Story", new[] { "StageID" },
        Tooltip = "解锁新的剧情章节喵~\n示例：UnlockStage chapter_02",
        Color = "0.6,0.4,0.6")]
    public static CommandResult UnlockStage(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1)
        {
            Log(console, "UnlockStage 命令需要 1 个参数：StageID", Color.yellow);
            return CommandResult.Failed;
        }

        string stageId = args[0];
        Log(console, $"[CommandExecutor] 解锁章节：{stageId}", Color.white);
        return CommandResult.Success;
    }

    // =========================================================
    // 🔧 Camera 相机相关命令
    // =========================================================

    [CommandInfo("cam_home", "🔧 相机归位", "Debug", null,
        Tooltip = "相机回到地图中心喵~",
        Color = "0.5,0.2,0.3")]
    public static CommandResult CamHome(DeveloperConsole console, string[] args)
    {
        CameraController.Instance.GoToOrigin();
        Log(console, "Camera returned to map center.", Color.cyan);
        return CommandResult.Success;
    }

    [CommandInfo("cam_goto", "🔧 相机移动", "Debug", new[] { "Position (x,y)" },
        Tooltip = "相机移动到指定位置喵~\n示例：cam_goto 50,50",
        Color = "0.5,0.2,0.4")]
    public static CommandResult CamGoto(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1)
        {
            Log(console, "Usage: cam_goto <x,y>", Color.red);
            return CommandResult.Failed;
        }

        Vector2Int gridPos = ParseGridPos(args[0]);
        Vector2 worldPos = GridSystem.Instance.GridToWorld(gridPos, Vector2Int.one);
        CameraController.Instance.FocusOn(worldPos);
        Log(console, $"Camera focused on Grid {gridPos}", Color.green);
        return CommandResult.Success;
    }

    [CommandInfo("cam_sync", "🔧 相机同步", "Debug", null,
        Tooltip = "同步相机边界喵~",
        Color = "0.5,0.3,0.3")]
    public static CommandResult CamSync(DeveloperConsole console, string[] args)
    {
        CameraController.Instance.SyncBounds();
        Log(console, "Camera bounds re-synchronized with WholeComponent.", Color.yellow);
        return CommandResult.Success;
    }

    [CommandInfo("cam_reset", "🔧 相机重置", "Debug", null,
        Tooltip = "重置相机设置喵~",
        Color = "0.5,0.3,0.4")]
    public static CommandResult CamReset(DeveloperConsole console, string[] args)
    {
        CameraController.Instance.ResetZoom();
        Log(console, "Camera zoom reset to default.", Color.cyan);
        return CommandResult.Success;
    }

    [CommandInfo("cam_speed", "🔧 相机速度", "Debug", new[] { "Speed" },
        Tooltip = "设置相机移动速度喵~\n示例：cam_speed 5",
        Color = "0.5,0.3,0.5")]
    public static CommandResult CamSpeed(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1) return CommandResult.Success;

        float speed = float.Parse(args[0]);
        CameraController.Instance.moveSpeed = speed;
        Log(console, $"Camera move speed set to {speed}", Color.white);
        return CommandResult.Success;
    }

    [CommandInfo("cam_scroll", "🔧 相机滚动", "Debug", new[] { "Enable (0/1)" },
        Tooltip = "开启/关闭相机滚动喵~\n示例：cam_scroll 1",
        Color = "0.5,0.3,0.6")]
    public static CommandResult CamScroll(DeveloperConsole console, string[] args)
    {
        if (args.Length < 1) return CommandResult.Success;

        bool enable = args[0] == "1";
        CameraController.Instance.useEdgeScrolling = enable;
        Log(console, $"Edge scrolling: {(enable ? "Enabled" : "Disabled")}", Color.white);
        return CommandResult.Success;
    }
}
