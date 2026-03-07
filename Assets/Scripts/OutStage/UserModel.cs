using System;
using System.Collections.Generic;
using UnityEngine;
using MineRTS.BigMap;

/// <summary>
/// 存档元数据（用于 UI 列表显示，如存档名、存档时间等）
/// </summary>
[Serializable]
public class SaveMetadata
{
    public string PlayerName = "指挥官";
    public string SaveDate;
    public float TotalPlayTime;
    public int LevelReached;
}

/// <summary>
/// 存档的全局进度数据（Meta-Progression）
/// </summary>
[Serializable]
public class GlobalProgression
{
    public long GlobalMoney;      // 全局金钱（跨存档累计的）
    public int TechPoint;          // 科技点
    public List<string> UnlockedBlueprints = new List<string>(); // 已解锁的图纸（跨存档）
}

/// <summary>
/// 关卡存档数据
/// </summary>
[Serializable]
public class StageSaveData
{
    public string StageID;
    public WholeComponent WorldData; // 关卡内的 ECS 数据
    public bool IsCleared;
}

/// <summary>
/// 用户数据模型（存档的核心数据结构）
/// 使用 Newtonsoft.Json 序列化，支持 Dictionary 直接序列化
/// </summary>
[Serializable]
public class UserModel
{
    public SaveMetadata Metadata = new SaveMetadata();
    public GlobalProgression Progression = new GlobalProgression();

    // ==================== 局外数据（大地图相关） ====================

    /// <summary>
    /// 大地图地理信息（节点位置、连线等）
    /// 每个存档独立的一份
    /// </summary>
    public BigMapSaveData BigMapData = new BigMapSaveData();

    /// <summary>
    /// 大地图关卡经济数据字典
    /// Key: StageID, Value: 经济数据
    /// </summary>
    public Dictionary<string, BigMapEconomyData> BigMapEconomyDict = new Dictionary<string, BigMapEconomyData>();

    // ==================== 局内数据（关卡 ECS 相关） ====================

    /// <summary>
    /// 关卡存档数据字典
    /// Key: StageID, Value: 关卡数据
    /// </summary>
    public Dictionary<string, StageSaveData> StageDict = new Dictionary<string, StageSaveData>();

    // ==================== 经济数据 管理方法 ====================

    /// <summary>
    /// 获取指定关卡的经济数据
    /// </summary>
    public BigMapEconomyData GetEconomyData(string stageID)
    {
        if (BigMapEconomyDict.TryGetValue(stageID, out var data))
        {
            return data;
        }
        return null;
    }

    /// <summary>
    /// 设置或更新关卡经济数据
    /// </summary>
    public void SetEconomyData(string stageID, BigMapEconomyData economyData)
    {
        if (economyData == null) return;

        economyData.StageID = stageID;

        if (BigMapEconomyDict.ContainsKey(stageID))
        {
            BigMapEconomyDict[stageID] = economyData;
        }
        else
        {
            BigMapEconomyDict.Add(stageID, economyData);
        }
    }

    /// <summary>
    /// 更新关卡经济数据（从关卡结算数据）
    /// </summary>
    /// <param name="stageID">关卡 ID</param>
    /// <param name="dailyOutput">每日产值</param>
    /// <param name="dailyCost">每日消耗</param>
    /// <param name="buildingCount">建筑数量</param>
    /// <param name="garrisonValue">驻兵总价值</param>
    public void UpdateEconomyData(string stageID, long dailyOutput, long dailyCost, int buildingCount, long garrisonValue)
    {
        if (BigMapEconomyDict.TryGetValue(stageID, out var economy))
        {
            economy.UpdateFromStageResult(dailyOutput, dailyCost, buildingCount, garrisonValue);
        }
        else
        {
            var newEconomy = new BigMapEconomyData(stageID);
            newEconomy.UpdateFromStageResult(dailyOutput, dailyCost, buildingCount, garrisonValue);
            BigMapEconomyDict.Add(stageID, newEconomy);
        }
    }

    /// <summary>
    /// 移除关卡经济数据
    /// </summary>
    public void RemoveEconomyData(string stageID)
    {
        if (BigMapEconomyDict.ContainsKey(stageID))
        {
            BigMapEconomyDict.Remove(stageID);
        }
    }

    /// <summary>
    /// 检查是否存在指定关卡的经济数据
    /// </summary>
    public bool HasEconomyData(string stageID)
    {
        return BigMapEconomyDict.ContainsKey(stageID);
    }

    // ==================== 关卡 ECS 数据 管理方法 ====================

    /// <summary>
    /// 获取指定关卡的 ECS 数据
    /// </summary>
    public StageSaveData GetStage(string stageID)
    {
        if (StageDict.TryGetValue(stageID, out var data))
        {
            return data;
        }
        return null;
    }

    /// <summary>
    /// 更新或创建关卡 ECS 数据
    /// </summary>
    /// <param name="stageID">关卡 ID</param>
    /// <param name="worldData">ECS 世界数据</param>
    /// <param name="isCleared">是否已通关</param>
    public void UpdateStage(string stageID, WholeComponent worldData, bool isCleared)
    {
        if (StageDict.TryGetValue(stageID, out var existing))
        {
            existing.WorldData = worldData;
            existing.IsCleared = isCleared;
        }
        else
        {
            var newEntry = new StageSaveData { StageID = stageID, WorldData = worldData, IsCleared = isCleared };
            StageDict.Add(stageID, newEntry);
        }
    }

    /// <summary>
    /// 移除关卡数据
    /// </summary>
    public void RemoveStage(string stageID)
    {
        if (StageDict.ContainsKey(stageID))
        {
            StageDict.Remove(stageID);
        }
    }

    /// <summary>
    /// 检查是否存在指定关卡的数据
    /// </summary>
    public bool HasStageData(string stageID)
    {
        return StageDict.ContainsKey(stageID);
    }
}
