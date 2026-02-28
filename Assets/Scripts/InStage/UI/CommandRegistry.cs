using System;
using System.Collections.Generic;
using System.Reflection;
using AIBrain;
using UnityEngine;

public static class CommandRegistry
{
    public static void RegisterEntityCommands(DeveloperConsole console)
    {
        // 1. 核心召唤指令：spawn <key> <x,y> <team> [aiType] [dir_x,y]
        // 例子: spawn x_dog 0,0 1 1 (召唤一只属于队1、AI为Moving的机械狗)
        // 例子: spawn conveyor 5,5 1 -1 0,-1 (召唤一个属于队1、朝下的传送带)
        console.AddCommand("spawn", (args) =>
        {
            if (args.Length < 3)
            {
                console.Log("Usage: spawn <key> <x,y> <team> [aiType] [dir_x,y]", Color.red);
                return;
            }

            string key = args[0].ToLower();
            Vector2Int pos = ParseGridPos(args[1]);
            int team = int.Parse(args[2]);

            EntityBlueprint bp = BlueprintRegistry.Get(key);
            if (string.IsNullOrEmpty(bp.Name)) return;

            if (!GridSystem.Instance.IsAreaClear(pos, bp.LogicSize))
            {
                console.Log($"Area {pos} is blocked!", Color.yellow);
                return;
            }

            // 统一走蓝图创建
            EntityHandle handle = EntitySystem.Instance.CreateEntityFromBlueprint(key, pos, team);
            int idx = EntitySystem.Instance.GetIndex(handle);

            if (idx != -1)
            {

                // 可选：覆盖朝向 (参数3)
                if (args.Length >= 4)
                    EntitySystem.Instance.wholeComponent.coreComponent[idx].Rotation = ParseGridPos(args[4]);
            }
            console.Log($"Successfully spawned {bp.Name} (Team {team})", Color.green);
        });

        // 2. 方阵召唤指令：army <key> <center_x,y> <w,h> <team>
        console.AddCommand("army", (args) =>
        {
            if (args.Length < 4)
            {
                console.Log("Usage: army <key> <x,y> <w,h> <team>", Color.red);
                return;
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
            console.Log($"Army: {count} {bp.Name}s deployed for Team {team}", Color.green);
        });

        console.AddCommand("ai_wave", (args) =>
        {
            if (args.Length < 3) return;
            int team = int.Parse(args[0]);
            string brainId = args[1];
            Vector2Int targetPos = ParseGridPos(args[2]);

            // 手动扫描全图“野生”单位的逻辑喵
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
                console.Log($"[Manual AI] {brainId} grabbed {recruits.Count} wild units.", Color.magenta);
            }
        });

        // 3. 英雄绑定指令：hero [slot_id]
        console.AddCommand("hero", (args) =>
        {
            int slot = (args.Length >= 1) ? int.Parse(args[0]) : 1;
            var whole = EntitySystem.Instance.wholeComponent;

            if (whole.entityCount > 0)
            {
                int lastIndex = whole.entityCount - 1; // 默认绑定刚刷出来的那个
                whole.userControlComponent[lastIndex].HeroSlot = slot;
                whole.coreComponent[lastIndex].Type |= UnitType.Hero;
                whole.coreComponent[lastIndex].Team = 1; // 英雄强制归属玩家队

                console.Log($"Unit {lastIndex} is now your Hero (Slot {slot})", Color.cyan);
            }
        });
    }

    public static void RegisterSystemCommands(DeveloperConsole console)
    {
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

            // 清理寻路系统
            if (PathfindingSystem.Instance != null)
                PathfindingSystem.Instance.Clear();

            // 清理选择框覆盖层
            if (SelectionOverlaySystem.Instance != null)
                SelectionOverlaySystem.Instance.HideAllSelectionBoxes();

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

        // 2. 【新增】从 Tilemap 加载数据到 ECS
        console.AddCommand("map_load", (args) =>
        {
            if (TilemapSyncManager.Instance != null)
            {
                TilemapSyncManager.Instance.SyncFromTilemap();
                console.Log("Map Data loaded from Tilemap to ECS groundMap.", Color.green);
            }
            else
            {
                console.Log("Error: TilemapSyncManager instance not found!", Color.red);
            }
        });

        // 3. 【新增】将 ECS 数据回显到 Tilemap 渲染
        console.AddCommand("map_apply", (args) =>
        {
            if (TilemapSyncManager.Instance != null)
            {
                TilemapSyncManager.Instance.SyncToTilemap();
                console.Log("ECS groundMap applied to Tilemap visual renderer.", Color.cyan);
            }
            else
            {
                console.Log("Error: TilemapSyncManager instance not found!", Color.red);
            }
        });
        // ==========================================
        //  存档与关卡流指令
        // ==========================================

        // 1. 新建存档: save_new <slot_name>
        console.AddCommand("save_new", (args) =>
        {
            string name = (args.Length > 0) ? args[0] : "slot_1";
            SaveManager.Instance.CreateNewSave(name);
            console.Log($"Created new save profile: {name}", Color.green);
        });

        // 2. 读取存档: save_load <slot_name>
        console.AddCommand("save_load", (args) =>
        {
            string name = (args.Length > 0) ? args[0] : "slot_1";
            SaveManager.Instance.LoadSave(name);
            console.Log($"Loaded save profile: {name}. Ready to enter stage.", Color.green);
        });
        // 3.1 【优化】仅保存到内存 (RAM Save)
        // 场景：在关卡里测试，想临时存一下，但不写硬盘，速度快
        console.AddCommand("save_ram", (args) =>
        {
            if (MainModel.Instance.IsInStage)
            {
                GameFlowController.Instance.SaveCurrentStageFromSystem();
                console.Log("Stage data saved to RAM (UserModel). Not written to disk yet.", Color.yellow);
            }
            else
            {
                console.Log("Not in stage, nothing to save to RAM.", Color.red);
            }
        });

        // 3. 保存当前进度: save_now
        console.AddCommand("save_now", (args) =>
        {
            // 先把 ECS 数据写回 User Model
            if (MainModel.Instance.IsInStage)
            {
                GameFlowController.Instance.SaveCurrentStageFromSystem();
            }
            // 然后落盘
            SaveManager.Instance.SaveGameToDisk();
            console.Log("Game Saved to Disk.", Color.cyan);
        });

        // 4. 进入关卡: enter <stage_id>
        // 这是最常用的指令！
        console.AddCommand("enter", (args) =>
        {
            if (args.Length < 1)
            {
                console.Log("Usage: enter <stage_id> (e.g., Level_01)", Color.red);
                return;
            }
            string stageID = args[0];

            // 这一句会触发 EntitySystem 去 SaveManager 查数据，没有就读 JSON
            EntitySystem.Instance.LoadStage(stageID);
            console.Log($"Entering stage: {stageID}", Color.yellow);
        });

        // 5. 重置关卡: reset_stage
        // 如果玩坏了，想重开这一关
        console.AddCommand("reset_stage", (args) =>
        {
            string currentStage = MainModel.Instance.CurrentActiveStageID;
            if (string.IsNullOrEmpty(currentStage))
            {
                console.Log("Not in any stage!", Color.red);
                return;
            }

            GameFlowController.Instance.ResetStage(currentStage);
            console.Log($"Stage {currentStage} has been reset to default state.", Color.red);
        });

        // 6. 【新增】撤离/返回大地图: leave
        console.AddCommand("leave", (args) =>
        {
            // 默认离开时自动保存
            GameFlowController.Instance.ReturnToMap(true);
            console.Log("Exited stage and returned to Map state.", Color.green);
        });

        // 7. 【新增】不保存直接撤离: leave_force
        // 场景：玩坏了，想直接退出去，不覆盖存档
        console.AddCommand("leave_force", (args) =>
        {
            GameFlowController.Instance.ReturnToMap(false); // false = 不保存
            console.Log("Exited stage WITHOUT saving.", Color.red);
        });

        // 1. 【新增】手动强制重建物流网络
        console.AddCommand("net_rebuild", (args) =>
        {
            var whole = EntitySystem.Instance.wholeComponent;
            TransportSystem.Instance.RebuildNetwork(whole);
            console.Log("Transport Network Rebuilt manually.", Color.cyan);
        });

        // 2. 【新增】查看物流网络状态
        console.AddCommand("net_info", (args) =>
        {
            // 这里我们需要在 TransportSystem 里加一个获取信息的接口喵
            string stats = TransportSystem.Instance.GetNetworkDebugInfo();
            console.Log("--- Transport Network Status ---", Color.magenta);
            console.Log(stats, Color.white);
        });
        // ==========================================
        //  剧本任务指令
        // ==========================================

        // 1. 加载任务包: mission_load <stage_id>
        // 例子: mission_load Level_Test
        console.AddCommand("mission_load", (args) => {
            if (args.Length < 1) return;
            string path = args[0]; // 现在这里可以直接输入完整路径了喵
                                   // 支持可选参数 append
            bool append = (args.Length > 1 && args[1] == "1");
            MissionManager.Instance.LoadMissionPack(path, append);
        });

        // 2. 强行完成当前任务: mission_skip
        // 作用：直接炸开当前的卡关点，进入下一阶段
        console.AddCommand("mission_skip", (args) =>
        {
            MissionManager.Instance.ForceCompleteActiveMissions();
            console.Log("Mission Cheated: Current active missions forced to complete!", Color.yellow);
        });

        // 3. 查看当前任务状态: mission_info
        console.AddCommand("mission_info", (args) =>
        {
            if (MissionManager.Instance.ActiveMissions.Count == 0)
            {
                console.Log("No mission pack loaded.", Color.gray);
                return;
            }

            foreach (var m in MissionManager.Instance.ActiveMissions)
            {
                string state = m.IsCompleted ? "<color=green>[DONE]</color>" :
                               (m.IsActive ? "<color=yellow>[ACTIVE]</color>" : "<color=gray>[LOCKED]</color>");
                console.Log($"{state} {m.Title} (ID: {m.MissionID})", Color.white);

                if (m.IsActive && !m.IsCompleted)
                {
                    foreach (var g in m.Goals)
                        console.Log($"   >> {g.Type}: {g.CurrentAmount}/{g.RequiredAmount}", Color.cyan);
                }
            }
        });
        console.AddCommand("help", (args) =>
        {
            console.Log("RTS Commands:", Color.magenta);
            foreach (var command in console.GetCommandKeys())
                console.Log($"- {command}", Color.white);
        });

        console.AddCommand("cheat_gold", (args) => {
            if (args.Length < 1) return;
            int amount = int.Parse(args[0]);
            IndustrialSystem.Instance.AddGold(amount); // 这会触发“金币更变”广播喵！
            console.Log($"Gold added: {amount}. Mission should react!", Color.green);
        });

        // 开启/关闭全局无限电力
        // 用法: cheat_power 1 (开启), cheat_power 0 (关闭)
        console.AddCommand("cheat_power", (args) =>
        {
            if (args.Length < 1)
            {
                // 如果不输参数，就打印当前状态
                bool current = IndustrialSystem.Instance.GlobalPowerOverride;
                console.Log($"Global Power Override is: {(current ? "<color=green>ON</color>" : "<color=red>OFF</color>")}", Color.white);
                return;
            }

            bool enable = args[0] == "1" || args[0].ToLower() == "true";
            IndustrialSystem.Instance.GlobalPowerOverride = enable;

            if (enable)
            {
                console.Log("⚡ UNLIMITED POWER! All buildings are now active without electricity.", Color.green);
            }
            else
            {
                console.Log("⚡ Power restrictions restored. Build more generators!", Color.yellow);
            }
        });
    }

    public static void RegisterTimeCommands(DeveloperConsole console)
    {
        // 1. 暂停/恢复时间
        console.AddCommand("timer_pause", (args) =>
        {
            TimeSystem.Instance.SetPaused(true);
            console.Log("Time System: Paused.", Color.yellow);
        });

        console.AddCommand("timer_resume", (args) =>
        {
            TimeSystem.Instance.SetPaused(false);
            console.Log("Time System: Resumed.", Color.green);
        });

        // 2. 重置秒表
        console.AddCommand("timer_reset", (args) =>
        {
            TimeSystem.Instance.ResetTimer();
            console.Log("Time System: Timer Reset to 0.", Color.cyan);
        });

        // 3. 时间快进 (调试神器喵！)
        // 用法示例: timer_skip 60
        console.AddCommand("timer_skip", (args) =>
        {
            if (args.Length > 0 && int.TryParse(args[0], out int seconds))
            {
                // 直接手动触发一次大额的时间增加广播
                var missionArgs = MissionArgs.Get();
                missionArgs.Amount = seconds;
                missionArgs.StringKey = "Seconds";
                PostSystem.Instance.Send("生存时间增加", missionArgs);
                MissionArgs.Release(missionArgs);

                console.Log($"Time System: Skipped {seconds}s for mission goals.", Color.magenta);
            }
        });
        console.AddCommand("nav_info", (args) =>
        {
            // 调用刚才写的接口
            string stats = GridSystem.Instance.GetNavMeshDebugInfo();

            console.Log("--- NavMesh & Portal Topology Status ---", Color.cyan);
            // 直接打印到控制台，如果太长建议分段或者打印到文件
            console.Log(stats, Color.white);
        });
    }
    public static void RegisterCameraCommands(DeveloperConsole console)
    {
        // 1. 回到地图中心点
        // 例子: cam_home
        console.AddCommand("cam_home", (args) =>
        {
            CameraController.Instance.GoToOrigin();
            console.Log("Camera returned to map center.", Color.cyan);
        });

        // 2. 聚焦到指定网格坐标
        // 例子: cam_goto 50,50
        console.AddCommand("cam_goto", (args) =>
        {
            if (args.Length < 1)
            {
                console.Log("Usage: cam_goto <x,y>", Color.red);
                return;
            }
            Vector2Int gridPos = ParseGridPos(args[0]);
            // 将网格坐标转换为世界坐标 (假设 logicSize 为 1x1 时的转换)
            Vector2 worldPos = GridSystem.Instance.GridToWorld(gridPos, Vector2Int.one);
            CameraController.Instance.FocusOn(worldPos);
            console.Log($"Camera focused on Grid {gridPos}", Color.green);
        });

        // 3. 手动同步边界 (当主人在控制台修改了 mapWidth/Height 后很有用)
        // 例子: cam_sync
        console.AddCommand("cam_sync", (args) =>
        {
            CameraController.Instance.SyncBounds();
            console.Log("Camera bounds re-synchronized with WholeComponent.", Color.yellow);
        });

        // 4. 重置缩放
        // 例子: cam_reset
        console.AddCommand("cam_reset", (args) =>
        {
            CameraController.Instance.ResetZoom();
            console.Log("Camera zoom reset to default.", Color.cyan);
        });

        // 5. 设置移动速度 (调试推屏手感)
        // 例子: cam_speed 50
        console.AddCommand("cam_speed", (args) =>
        {
            if (args.Length < 1) return;
            float speed = float.Parse(args[0]);
            CameraController.Instance.moveSpeed = speed;
            console.Log($"Camera move speed set to {speed}", Color.white);
        });

        // 6. 开启/关闭推屏
        // 例子: cam_scroll 0 (关闭), cam_scroll 1 (开启)
        console.AddCommand("cam_scroll", (args) =>
        {
            if (args.Length < 1) return;
            bool enable = args[0] == "1";
            CameraController.Instance.useEdgeScrolling = enable;
            console.Log($"Edge scrolling: {(enable ? "Enabled" : "Disabled")}", Color.white);
        });
    }
    private static Vector2Int ParseGridPos(string input)
    {
        string[] parts = input.Split(',');
        if (parts.Length < 2) return Vector2Int.zero;
        return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
    }
}