using UnityEngine;
using System.Collections.Generic;
using System.Linq; // --- [NEW] 用于查询任务链 ---

public class MissionManager : SingletonMono<MissionManager>
{
    // 当前内存中所有活跃的任务
    public List<MissionData> ActiveMissions = new List<MissionData>();

    // 记录当前加载的所有包名（如果是多包并存的话）
    public HashSet<string> LoadedPackNames = new HashSet<string>();


    private void Start()
    {
        PostSystem.Instance.Register(this);
    }

    /// <summary>
    /// 【通用加载入口】从 Resources 加载任意路径的任务剧本
    /// </summary>
    /// <param name="path">Resources 下的路径，例如 "Missions/Tutorial" 或 "Events/NewYear"</param>
    /// <param name="append">如果是 true，则保留现有任务；如果是 false，则清空后再加</param>
    public void LoadMissionPack(string path, bool append = false)
    {
        if (!append)
        {
            ClearCurrentMissions();
        }

        TextAsset jsonAsset = Resources.Load<TextAsset>(path);
        if (jsonAsset == null)
        {
            Debug.LogError($"<color=red>[Mission]</color> 找不到剧本资源: {path}");
            return;
        }

        // 1. 反序列化
        MissionPackData pack = JsonUtility.FromJson<MissionPackData>(jsonAsset.text);
        if (pack == null) return;

        // 2. 核心：缝合奖励数据 (Stitching)
        InitializeMissionPack(pack);

        // 3. 注入活跃列表
        ActiveMissions.AddRange(pack.Missions);
        LoadedPackNames.Add(path);
        PostSystem.Instance.Send("UI_MISSION_REFRESH", null);

        Debug.Log($"<color=magenta>[Mission]</color> 剧本「{path}」装载成功！当前总任务数: {ActiveMissions.Count}");

        DirectorSystem.Instance.LoadScenario(pack);
    }

    /// <summary>
    /// 根据关卡ID自动加载绑定的任务包
    /// 如果找不到绑定的任务包，则保持当前任务状态不变
    /// </summary>
    public void LoadMissionPackForStage(string stageID)
    {
        if (string.IsNullOrEmpty(stageID))
        {
            Debug.LogWarning("[Mission] 关卡ID为空，无法加载绑定的任务包");
            return;
        }

        // 搜索所有任务包资源，找到绑定到该关卡的任务包
        TextAsset[] allPacks = Resources.LoadAll<TextAsset>("Missions");
        foreach (TextAsset asset in allPacks)
        {
            try
            {
                MissionPackData pack = JsonUtility.FromJson<MissionPackData>(asset.text);
                if (pack != null && !string.IsNullOrEmpty(pack.BoundStageID) && pack.BoundStageID == stageID)
                {
                    Debug.Log($"<color=magenta>[Mission]</color> 找到绑定到关卡 {stageID} 的任务包: {asset.name}");
                    // 加载这个任务包（替换现有任务）
                    LoadMissionPack(GetResourcePath(asset), false);
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Mission] 解析任务包 {asset.name} 时出错: {e.Message}");
            }
        }

        Debug.Log($"<color=yellow>[Mission]</color> 没有找到绑定到关卡 {stageID} 的任务包");
    }

    /// <summary>
    /// 获取TextAsset在Resources下的路径
    /// </summary>
    private string GetResourcePath(TextAsset asset)
    {
        // Resources.LoadAll返回的路径不包含"Resources/"前缀和扩展名
        // 但我们需要相对Resources的路径
        // 简单实现：假设所有任务包都在"Missions"文件夹下
        return "Missions/" + asset.name;
    }

    /// <summary>
    /// 【内部核心】缝合逻辑：将独立存放的 Rewards 映射回 Missions
    /// </summary>
    private void InitializeMissionPack(MissionPackData pack)
    {
        if (pack.Rewards == null || pack.Rewards.Count == 0) return;

        // 构建 ID 查找字典
        var rewardLookup = pack.Rewards.ToDictionary(r => r.RewardID, r => r.Data);

        foreach (var mission in pack.Missions)
        {
            // 如果任务有引用的 RewardID，则从奖励池中取回数据
            if (!string.IsNullOrEmpty(mission.RewardID) && rewardLookup.TryGetValue(mission.RewardID, out var rewardData))
            {
                mission.Reward = rewardData;
            }
        }
    }

    public void ClearCurrentMissions()
    {
        ActiveMissions.Clear();
        LoadedPackNames.Clear();
    }

    // --- [MODIFIED] 更新逻辑：增加了 IsActive 的检查 ---
    private void UpdateGoalProgress(GoalType type, string key, long addAmount)
    {
        bool anyProgressChanged = false;

        foreach (var mission in ActiveMissions)
        {
            if (!mission.IsActive || mission.IsCompleted || mission.IsFailed) continue;

            bool missionFinished = true;
            foreach (var goal in mission.Goals)
            {
                if (goal.Type == type && goal.TargetKey == key)
                {
                    goal.CurrentAmount += addAmount;
                    anyProgressChanged = true; // 进度确实变了喵！
                    Debug.Log($"<color=cyan>[Mission]</color> {mission.Title} 进度: {goal.CurrentAmount}/{goal.RequiredAmount}");
                }

                if (!goal.IsReached) missionFinished = false;
            }

            if (missionFinished)
            {
                CompleteMission(mission);
            }
        }

        // --- [NEW] 如果进度有任何变化，通知 UI 刷新界面 ---
        if (anyProgressChanged)
        {
            // 我们可以把整个 ActiveMissions 传出去，或者传具体的 mission
            PostSystem.Instance.Send("UI_MISSION_REFRESH", null);
        }
    }
    /// <summary>
    /// 【调试专用】强行完成当前所有已激活但未完成的任务
    /// </summary>
    public void ForceCompleteActiveMissions()
    {
        // 找到当前正在进行的任务（IsActive 且未完成）
        var targets = ActiveMissions.Where(m => m.IsActive && !m.IsCompleted).ToList();

        if (targets.Count == 0)
        {
            Debug.Log("<color=yellow>[Mission]</color> 当前没有正在进行中的任务可以跳过喵。");
            return;
        }

        foreach (var m in targets)
        {
            // 1. 强行填满所有目标数值
            foreach (var goal in m.Goals)
            {
                goal.CurrentAmount = goal.RequiredAmount;
            }
            // 2. 调用原有的完成逻辑（触发连环任务和奖励）
            CompleteMission(m);
        }
    }
    // --- [MODIFIED] 完成逻辑：增加了奖励发放和连环激活 ---
    private void CompleteMission(MissionData mission)
    {
        mission.IsCompleted = true;
        Debug.Log($"<color=green>[Mission]</color> ★ 任务完成：{mission.Title} ★");

        // --- [NEW] 1. 发放奖励 ---
        if (mission.Reward != null)
        {
            // 发送给 SaveManager 处理金钱、科技点和蓝图解锁
            PostSystem.Instance.Send("EVT_MISSION_REWARD", mission.Reward);
        }

        // --- [NEW] 2. 激活连环任务 (Mission Chain) ---
        if (!string.IsNullOrEmpty(mission.NextMissionID))
        {
            var next = ActiveMissions.Find(m => m.MissionID == mission.NextMissionID);
            if (next != null)
            {
                next.IsActive = true;
                Debug.Log($"<color=yellow>[Mission]</color> 下一阶段已开启: {next.Title}");
            }
        }

        PostSystem.Instance.Send("UI_MISSION_COMPLETE", mission);
        CheckLevelVictory();
    }

    // --- [MODIFIED] 胜利判定：现在只看主线任务 ---
    private void CheckLevelVictory()
    {
        // --- [NEW] 检查所有 Priority == Main 的任务是否全部完成 ---
        bool allMainDone = ActiveMissions
            .Where(m => m.Priority == MissionPriority.Main)
            .All(m => m.IsCompleted);

        if (allMainDone && ActiveMissions.Any(m => m.Priority == MissionPriority.Main))
        {
            Debug.Log("<color=gold>[Mission]</color> 所有的主线任务已达成！据点已收复喵！");
            PostSystem.Instance.Send("UI_LEVEL_VICTORY");
        }
    }

    // =========================================================
    //  事件监听 (带严格类型防御检查版)
    // =========================================================

    // --- [监听] 基础建设 ---
    [Subscribe("建筑完成")]
    private void OnEntityBuilt(object data)
    {
        if (data is string unitKey)
        {
            UpdateGoalProgress(GoalType.BuildEntity, unitKey, 1);
        }
        else
        {
            LogTypeError("建筑完成", "string", data);
        }
    }

    // --- [监听] 战斗行为 ---
    [Subscribe("击败目标")]
    private void OnEntityKilled(object data)
    {
        if (data is string unitKey)
        {
            UpdateGoalProgress(GoalType.KillEntity, unitKey, 1);
        }
        else
        {
            LogTypeError("击败目标", "string", data);
        }
    }

    // --- [监听] 物资交易 ---
    [Subscribe("出售资源")]
    private void OnResourceSold(object data)
    {
        if (data is MissionArgs args)
        {
            // 这里的 IntKey.ToString() 以后可以通过 ItemRegistry 优化喵
            UpdateGoalProgress(GoalType.SellResource, args.IntKey.ToString(), args.Amount);
        }
        else
        {
            LogTypeError("出售资源", "MissionArgs", data);
        }
    }

    // --- [监听] 经济变动 ---
    [Subscribe("金币更变")]
    private void OnGoldGained(object data)
    {
        // 采用更严谨的数字转换判定
        if (data is long lAmount)
        {
            UpdateGoalProgress(GoalType.EarnMoney, "Gold", lAmount);
        }
        else if (data is int iAmount)
        {
            UpdateGoalProgress(GoalType.EarnMoney, "Gold", (long)iAmount);
        }
        else
        {
            LogTypeError("金币更变", "long/int", data);
        }
    }

    // --- [监听] 时间流逝 ---
    [Subscribe("生存时间增加")]
    private void OnSurviveTimeTick(object data)
    {
        if (data is MissionArgs args)
        {
            UpdateGoalProgress(GoalType.SurviveTime, args.StringKey, args.Amount);
        }
        else
        {
            LogTypeError("生存时间增加", "MissionArgs", data);
        }
    }

    // --- 统一的报错小助手喵 ---
    private void LogTypeError(string eventName, string expectedType, object receivedData)
    {
        string receivedType = (receivedData != null) ? receivedData.GetType().Name : "null";
        Debug.LogError($"<color=red>[Mission Error]</color> 事件「{eventName}」参数异常！" +
                       $"期望类型: {expectedType}, 实际收到: {receivedType}。请检查发送源喵！");
    }
}