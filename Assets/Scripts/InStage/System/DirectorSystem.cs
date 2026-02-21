using System.Collections.Generic;
using System.Linq;
using AIBrain;
using UnityEngine;

public class DirectorSystem : SingletonMono<DirectorSystem>
{
    private List<ScenarioEventData> _activeEvents = new List<ScenarioEventData>();
    private Dictionary<string, SpawnActionData> _spawnDict;
    private Dictionary<string, AIBrainActionData> _aiDict;

    private void Start()
    {
        // 🐾 这里的关键：把自己注册到主人的 PostSystem 里喵！
        PostSystem.Instance.Register(this);
    }

    public void LoadScenario(MissionPackData pack)
    {
        if (pack == null) return;
        _activeEvents = pack.ScenarioEvents ?? new List<ScenarioEventData>();

        _spawnDict = pack.SpawnActions?.ToDictionary(s => s.SpawnID) ?? new Dictionary<string, SpawnActionData>();
        _aiDict = pack.AIBrainActions?.ToDictionary(a => a.BrainNodeID) ?? new Dictionary<string, AIBrainActionData>();

        foreach (var e in _activeEvents) e.HasTriggered = false;

        Debug.Log($"<color=cyan>[Director]</color> 剧本事件加载完毕，共 {_activeEvents.Count} 个事件喵！");
    }

    // =========================================================
    // 1. 基于时间的触发 (Update 仅处理时间流逝)
    // =========================================================
    private void Update()
    {
        if (TimeSystem.Instance == null || TimeSystem.Instance.IsPaused) return;

        // 优化：只筛选出还没触发且类型是 Time 的事件
        var timeEvents = _activeEvents.Where(e => !e.HasTriggered && e.Trigger == TriggerType.Time);

        foreach (var evt in timeEvents)
        {
            if (float.TryParse(evt.TriggerParam, out float targetTime))
            {
                if (TimeSystem.Instance.TotalElapsedSeconds >= targetTime)
                {
                    TriggerEvent(evt);
                }
            }
        }
    }

    // =========================================================
    // 2. 基于事件的触发 (由 PostSystem 自动推送到这里)
    // =========================================================

    /// <summary>
    /// 监听任务完成事件喵！
    /// </summary>
    [Subscribe("UI_MISSION_COMPLETE")]
    private void OnMissionCompleted(object data)
    {
        if (data is MissionData mission)
        {
            // 找到所有等待这个任务完成的导演事件
            var relatedEvents = _activeEvents.Where(e =>
                !e.HasTriggered &&
                e.Trigger == TriggerType.MissionCompleted &&
                e.TriggerParam == mission.MissionID);

            foreach (var evt in relatedEvents)
            {
                Debug.Log($"<color=cyan>[Director]</color> 检测到前置任务 [{mission.Title}] 已完成，触发导演事件！");
                TriggerEvent(evt);
            }
        }
    }

    // =========================================================
    // 3. 核心执行逻辑
    // =========================================================

    private void TriggerEvent(ScenarioEventData evt)
    {
        evt.HasTriggered = true;
        ExecuteAction(evt);
    }

    // 在 DirectorSystem.cs 中
    private void ExecuteAction(ScenarioEventData evt)
    {
        // 1. 顺藤摸瓜找到【召唤节点】数据
        if (string.IsNullOrEmpty(evt.NextSpawnID) || !_spawnDict.TryGetValue(evt.NextSpawnID, out var spawnData))
            return;

        List<EntityHandle> totalSpawnedForThisEvent = new List<EntityHandle>();

        // 2. 执行所有兵种召唤，并收集他们的 Handle
        foreach (var unit in spawnData.Units)
        {
            if (unit.Count <= 0 || string.IsNullOrEmpty(unit.BlueprintId)) continue;

            // 计算方阵宽高
            int w = Mathf.CeilToInt(Mathf.Sqrt(unit.Count));
            int h = Mathf.CeilToInt((float)unit.Count / w);

            // --- 直接调用 C#，拿到这批 Handle ---
            var handles = EntitySystem.Instance.SpawnArmy(unit.BlueprintId, spawnData.SpawnPos, new Vector2Int(w, h), spawnData.Team);
            totalSpawnedForThisEvent.AddRange(handles);

            // 顺便在控制台打印一下，方便主人看戏
            Debug.Log($"[Director] Spawned {handles.Count} {unit.BlueprintId}");
        }

        // 3. 如果召唤节点后连了【AI挂载节点】，直接缝合！
        if (totalSpawnedForThisEvent.Count > 0 && !string.IsNullOrEmpty(spawnData.AttachAIBrainID))
        {
            if (_aiDict.TryGetValue(spawnData.AttachAIBrainID, out var aiData))
            {
                // --- 直接把名单塞给 AI，不用去全图扫描了喵！ ---
                AIBrainServer.Instance.ApplyWaveAI(spawnData.Team, aiData.BrainIdentifier, aiData.TargetPos, totalSpawnedForThisEvent);
                Debug.Log($"[Director] AI Wave '{aiData.BrainIdentifier}' bound to {totalSpawnedForThisEvent.Count} units.");
            }
        }
    }
}