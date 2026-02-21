using System;
using System.Collections.Generic;
using System.Linq; // 方便用 Linq
using UnityEngine;

/// <summary>
/// 存档元数据：用于在UI列表里显示（比如存档名、游玩时间）
/// </summary>
[Serializable]
public class SaveMetadata
{
    public string PlayerName = "指挥官喵";
    public string SaveDate;
    public float TotalPlayTime;
    public int LevelReached;
}

/// <summary>
/// 跨据点的全局进步数据（Meta-Progression）
/// </summary>
[Serializable]
public class GlobalProgression
{
    public long GlobalMoney;      // 局外金钱（用来买升级的）
    public int TechPoint;          // 科技点
    public List<string> UnlockedBlueprints = new List<string>(); // 已解锁的图纸名单
}


// 1. 定义一个包装类，把 Key 和 Value 绑在一起，方便存进 List
[Serializable]
public class StageSaveData
{
    public string StageID;
    public WholeComponent WorldData; // 这里存着那一大堆数组
    public bool IsCleared;
}

[Serializable]
public class UserModel
{
    public SaveMetadata Metadata = new SaveMetadata();
    public GlobalProgression Progression = new GlobalProgression();

    // --- 序列化层 (存盘用这个) ---
    // JsonUtility 可以完美处理 List
    public List<StageSaveData> StageList = new List<StageSaveData>();

    // --- 逻辑层 (运行时用这个) ---
    // Dictionary 查找快，但不参与序列化
    [NonSerialized]
    private Dictionary<string, StageSaveData> _stageDict = new Dictionary<string, StageSaveData>();

    // 在加载存档后，手动调用这个方法重建字典
    public void RebuildRuntimeCache()
    {
        _stageDict.Clear();
        foreach (var stage in StageList)
        {
            if (!_stageDict.ContainsKey(stage.StageID))
            {
                _stageDict.Add(stage.StageID, stage);
            }
        }
    }

    // 辅助方法：获取或创建
    public StageSaveData GetStage(string id)
    {
        if (_stageDict.TryGetValue(id, out var data))
        {
            return data;
        }
        return null;
    }

    // 辅助方法：保存/更新
    public void UpdateStage(string id, WholeComponent data, bool isCleared)
    {
        // 如果已经有了，就更新
        if (_stageDict.TryGetValue(id, out var existing))
        {
            existing.WorldData = data;
            existing.IsCleared = isCleared;
        }
        else
        {
            // 如果没有，创建新的
            var newEntry = new StageSaveData { StageID = id, WorldData = data, IsCleared = isCleared };
            StageList.Add(newEntry);
            _stageDict[id] = newEntry;
        }
    }

    // 辅助方法：删除（用于重置关卡）
    public void RemoveStage(string id)
    {
        if (_stageDict.ContainsKey(id))
        {
            StageList.RemoveAll(x => x.StageID == id);
            _stageDict.Remove(id);
        }
    }
}