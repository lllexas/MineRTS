using UnityEngine;
using System.Collections.Generic;

public class UIMissionTracker : MonoBehaviour
{
    [Header("配置")]
    public GameObject missionItemPrefab;
    public Transform container;

    // 缓存已经生成的任务 UI 实例
    private Dictionary<string, UIMissionItem> _uiCache = new Dictionary<string, UIMissionItem>();

    private void Start()
    {
        // 注册到事件系统
        PostSystem.Instance.Register(this);
        // 初始化刷新一次
        RefreshAll();
    }

    // --- 监听：任务进度变了，或者有新任务加进来了喵 ---
    [Subscribe("UI_MISSION_REFRESH")]
    public void OnMissionRefresh(object data)
    {
        RefreshAll();
    }

    // --- 监听：任务完成了喵 ---
    [Subscribe("UI_MISSION_COMPLETE")]
    public void OnMissionComplete(object data)
    {
        if (data is MissionData mission)
        {
            // 播放一个亮晶晶的音效或者特效喵！
            Debug.Log($"UI 播报：任务 {mission.Title} 彻底完成啦！");
            RefreshAll();
        }
    }

    private void RefreshAll()
    {
        var activeMissions = MissionManager.Instance.ActiveMissions;

        // 1. 简单暴力：先全部回收（如果是为了极致性能可以做更细的 Diff）
        // 但由于任务数量通常不多，直接清空再生成是最稳妥的
        foreach (var item in _uiCache.Values)
        {
            item.gameObject.SetActive(false);
        }

        foreach (var m in activeMissions)
        {
            // 只显示“已激活”且“未完成”的任务喵
            if (!m.IsActive || m.IsCompleted) continue;

            if (!_uiCache.ContainsKey(m.MissionID))
            {
                var go = Instantiate(missionItemPrefab, container);
                _uiCache[m.MissionID] = go.GetComponent<UIMissionItem>();
            }

            var uiItem = _uiCache[m.MissionID];
            uiItem.gameObject.SetActive(true);
            uiItem.Setup(m); // 填充数据
        }
    }
}