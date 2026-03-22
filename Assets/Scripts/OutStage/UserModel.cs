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

    // ==================== 社交数据 ====================

    /// <summary>
    /// 社交系统数据（好友、消息、群组等）
    /// </summary>
    public SocialData Social = new SocialData();

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

    // ==================== 图数据包数据 ====================

    /// <summary>
    /// 图数据包字典（科技树、剧情图、VFS 等）
    /// Key: GUID
    /// Value: BasePackData 本体（支持多态序列化，System 字段区分类型）
    /// </summary>
    public Dictionary<string, BasePackData> PackDataDict = new Dictionary<string, BasePackData>();

    /// <summary>
    /// 实体持有的图数据包字典
    /// 外层 Key: 实体/AI/进程实体 ID
    /// 内层 Key: GUID
    /// 内层 Value: BasePackData 本体
    /// </summary>
    public Dictionary<string, Dictionary<string, BasePackData>> EntityPackDataDict =
        new Dictionary<string, Dictionary<string, BasePackData>>();

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

    // ==================== 实体 Pack 数据 管理方法 ====================

    /// <summary>
    /// 获取指定实体的 Pack 字典
    /// </summary>
    public Dictionary<string, BasePackData> GetEntityPackDict(string entityID, bool createIfMissing = false)
    {
        if (string.IsNullOrEmpty(entityID))
        {
            return null;
        }

        EntityPackDataDict ??= new Dictionary<string, Dictionary<string, BasePackData>>();

        if (EntityPackDataDict.TryGetValue(entityID, out var packDict))
        {
            return packDict;
        }

        if (!createIfMissing)
        {
            return null;
        }

        packDict = new Dictionary<string, BasePackData>();
        EntityPackDataDict[entityID] = packDict;
        return packDict;
    }

    public Dictionary<string, BasePackData> GetEntityPackDict(GraphInstanceSlot slot, bool createIfMissing = false)
    {
        return GetEntityPackDict(slot.ToString(), createIfMissing);
    }

    /// <summary>
    /// 设置或更新指定实体持有的 Pack
    /// </summary>
    public void SetEntityPack(string entityID, string guid, BasePackData packData)
    {
        if (string.IsNullOrEmpty(entityID) || string.IsNullOrEmpty(guid) || packData == null)
        {
            return;
        }

        var packDict = GetEntityPackDict(entityID, createIfMissing: true);
        packDict[guid] = packData;
    }

    public void SetEntityPack(GraphInstanceSlot slot, string guid, BasePackData packData)
    {
        SetEntityPack(slot.ToString(), guid, packData);
    }

    /// <summary>
    /// 移除指定实体持有的 Pack
    /// </summary>
    public void RemoveEntityPack(string entityID, string guid)
    {
        if (string.IsNullOrEmpty(entityID) || string.IsNullOrEmpty(guid))
        {
            return;
        }

        if (EntityPackDataDict == null)
        {
            return;
        }

        if (EntityPackDataDict.TryGetValue(entityID, out var packDict))
        {
            packDict.Remove(guid);
            if (packDict.Count == 0)
            {
                EntityPackDataDict.Remove(entityID);
            }
        }
    }

    public void RemoveEntityPack(GraphInstanceSlot slot, string guid)
    {
        RemoveEntityPack(slot.ToString(), guid);
    }

    /// <summary>
    /// 移除指定实体的全部 Pack
    /// </summary>
    public void RemoveEntityPackDict(string entityID)
    {
        if (string.IsNullOrEmpty(entityID))
        {
            return;
        }

        if (EntityPackDataDict == null)
        {
            return;
        }

        EntityPackDataDict.Remove(entityID);
    }

    public void RemoveEntityPackDict(GraphInstanceSlot slot)
    {
        RemoveEntityPackDict(slot.ToString());
    }

    /// <summary>
    /// 检查指定实体是否存在 Pack 字典
    /// </summary>
    public bool HasEntityPackDict(string entityID)
    {
        return !string.IsNullOrEmpty(entityID)
            && EntityPackDataDict != null
            && EntityPackDataDict.ContainsKey(entityID);
    }

    public bool HasEntityPackDict(GraphInstanceSlot slot)
    {
        return HasEntityPackDict(slot.ToString());
    }
}
