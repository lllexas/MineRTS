using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UI 任务追踪器 - 已休眠喵~
/// 注：旧的 MissionManager 已移除，任务系统已重构为 NekoGraph 电子伏特协议
/// TODO: 等待新任务系统的 UI 追踪实现
/// </summary>
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
        
        // 已休眠 - 等待新任务系统实现
        Debug.Log("<color=orange>[UIMissionTracker]</color> 已休眠，等待新任务系统实现喵~");
        // RefreshAll();
    }

    // --- 监听：任务进度变了，或者有新任务加进来了喵 ---
    [Subscribe("UI_MISSION_REFRESH")]
    public void OnMissionRefresh(object data)
    {
        // RefreshAll();
    }

    // --- 监听：任务完成了喵 ---
    [Subscribe("UI_MISSION_COMPLETE")]
    public void OnMissionComplete(object data)
    {
        if (data is MissionNode_A_Data mission)
        {
            // 播放一个亮晶晶的音效或者特效喵！
            Debug.Log($"UI 播报：任务 {mission.Title} 彻底完成啦！");
            // RefreshAll();
        }
    }

    /// <summary>
    /// 刷新所有任务 UI - 已休眠喵~
    /// </summary>
    private void RefreshAll()
    {
        // 已休眠 - 等待新任务系统实现
        Debug.LogWarning("<color=orange>[UIMissionTracker]</color> RefreshAll() 已休眠，等待新任务系统实现喵~");
        
        // 旧代码已注释，等待新任务系统实现
        // var activeMissions = MissionManager.Instance.ActiveMissions;

        // // 1. 简单暴力：先全部回收（如果是为了极致性能可以做更细的 Diff）
        // // 但由于任务数量通常不多，直接清空再生成是最稳妥的
        // foreach (var item in _uiCache.Values)
        // {
        //     item.gameObject.SetActive(false);
        // }

        // foreach (var m in activeMissions)
        // {
        //     // 只显示"已激活"且"未完成"的任务喵
        //     if (!m.IsActive || m.IsCompleted) continue;

        //     if (!_uiCache.ContainsKey(m.MissionID))
        //     {
        //         var go = Instantiate(missionItemPrefab, container);
        //         _uiCache[m.MissionID] = go.GetComponent<UIMissionItem>();
        //     }

        //     var uiItem = _uiCache[m.MissionID];
        //     uiItem.gameObject.SetActive(true);
        //     uiItem.Setup(m); // 填充数据
        // }
    }
}