using System;
using UnityEngine;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 大地图关卡经济数据
    /// 用于存储每个关卡的经济产出、建筑、驻兵等信息
    /// </summary>
    [Serializable]
    public class BigMapEconomyData
    {
        /// <summary>
        /// 关卡唯一标识符（与 BigMapNodeData.StageID 对应）
        /// </summary>
        public string StageID;

        /// <summary>
        /// 每日产值
        /// </summary>
        public long DailyOutput;

        /// <summary>
        /// 每日消耗
        /// </summary>
        public long DailyCost;

        /// <summary>
        /// 每日净产值（= DailyOutput - DailyCost）
        /// </summary>
        public long DailyNetOutput;

        /// <summary>
        /// 建筑数量
        /// </summary>
        public int BuildingCount;

        /// <summary>
        /// 驻兵总价值
        /// </summary>
        public long GarrisonValue;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public BigMapEconomyData()
        {
            StageID = "";
            DailyOutput = 0;
            DailyCost = 0;
            DailyNetOutput = 0;
            BuildingCount = 0;
            GarrisonValue = 0;
        }

        /// <summary>
        /// 指定 StageID 的构造函数
        /// </summary>
        public BigMapEconomyData(string stageID)
        {
            StageID = stageID;
            DailyOutput = 0;
            DailyCost = 0;
            DailyNetOutput = 0;
            BuildingCount = 0;
            GarrisonValue = 0;
        }

        /// <summary>
        /// 更新净产值计算
        /// </summary>
        public void UpdateNetOutput()
        {
            DailyNetOutput = DailyOutput - DailyCost;
        }

        /// <summary>
        /// 从关卡结算数据更新经济信息
        /// </summary>
        /// <param name="dailyOutput">每日产值</param>
        /// <param name="dailyCost">每日消耗</param>
        /// <param name="buildingCount">建筑数量</param>
        /// <param name="garrisonValue">驻兵总价值</param>
        public void UpdateFromStageResult(long dailyOutput, long dailyCost, int buildingCount, long garrisonValue)
        {
            DailyOutput = dailyOutput;
            DailyCost = dailyCost;
            BuildingCount = buildingCount;
            GarrisonValue = garrisonValue;
            UpdateNetOutput();
        }
    }
}
