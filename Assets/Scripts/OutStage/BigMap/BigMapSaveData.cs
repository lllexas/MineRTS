using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 大地图拓扑编辑器的根数据结构
    /// 包含所有节点和连线的数据
    /// </summary>
    [Serializable]
    public class BigMapSaveData
    {
        /// <summary>
        /// 地图节点列表
        /// </summary>
        public List<BigMapNodeData> Nodes = new List<BigMapNodeData>();

        /// <summary>
        /// 地图连线列表
        /// </summary>
        public List<BigMapEdgeData> Edges = new List<BigMapEdgeData>();

        /// <summary>
        /// 地图底图路径（可选）
        /// </summary>
        public string BackgroundImagePath;

        /// <summary>
        /// 画布偏移量（用于保存时的视口位置）
        /// </summary>
        public SerializableVector2 CanvasOffset;

        /// <summary>
        /// 画布缩放比例
        /// </summary>
        public float CanvasZoom = 1.0f;
    }

    /// <summary>
    /// 大地图节点数据
    /// </summary>
    [Serializable]
    public class BigMapNodeData
    {
        /// <summary>
        /// 节点唯一标识符（GUID）
        /// </summary>
        public string StageID;

        /// <summary>
        /// 节点显示名称
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// 节点在画布上的位置（世界坐标）
        /// </summary>
        public SerializableVector2 Position;

        /// <summary>
        /// 节点类型（可选，用于区分不同类型的节点）
        /// </summary>
        public string NodeType;

        /// <summary>
        /// 节点附加数据（可选，用于扩展）
        /// </summary>
        public string ExtraData;

        /// <summary>
        /// 默认构造函数，自动生成 GUID
        /// </summary>
        public BigMapNodeData()
        {
            StageID = Guid.NewGuid().ToString();
            DisplayName = "新节点";
            Position = SerializableVector2.zero;
            NodeType = "Default";
            ExtraData = "";
        }

        /// <summary>
        /// 指定位置的构造函数
        /// </summary>
        public BigMapNodeData(SerializableVector2 position, string displayName = "新节点")
        {
            StageID = Guid.NewGuid().ToString();
            DisplayName = displayName;
            Position = position;
            NodeType = "Default";
            ExtraData = "";
        }
    }

    /// <summary>
    /// 大地图连线数据
    /// </summary>
    [Serializable]
    public class BigMapEdgeData
    {
        /// <summary>
        /// 起点节点 ID
        /// </summary>
        public string FromNodeID;

        /// <summary>
        /// 终点节点 ID
        /// </summary>
        public string ToNodeID;

        /// <summary>
        /// 连线类型（单向/双向）
        /// </summary>
        public EdgeDirection Direction;

        /// <summary>
        /// 连线附加数据（可选）
        /// </summary>
        public string ExtraData;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public BigMapEdgeData()
        {
            FromNodeID = "";
            ToNodeID = "";
            Direction = EdgeDirection.Bidirectional;
            ExtraData = "";
        }

        /// <summary>
        /// 指定起点终点的构造函数
        /// </summary>
        public BigMapEdgeData(string fromNodeID, string toNodeID, EdgeDirection direction = EdgeDirection.Bidirectional)
        {
            FromNodeID = fromNodeID;
            ToNodeID = toNodeID;
            Direction = direction;
            ExtraData = "";
        }
    }

    /// <summary>
    /// 连线方向枚举
    /// </summary>
    [Serializable]
    public enum EdgeDirection
    {
        /// <summary>
        /// 单向：从起点到终点
        /// </summary>
        Unidirectional,

        /// <summary>
        /// 双向：起点和终点可以互相通行
        /// </summary>
        Bidirectional
    }

    /// <summary>
    /// 扩展方法：将 BigMapSaveData 转换为经济数据字典
    /// </summary>
    public static class BigMapSaveDataExtensions
    {
        /// <summary>
        /// 从大地图节点数据创建经济数据字典
        /// 用于新存档初始化时，将编辑器地理数据转换为游戏经济数据
        /// </summary>
        /// <param name="bigMapData">大地图地理数据</param>
        /// <returns>经济数据字典（Key: StageID）</returns>
        public static Dictionary<string, BigMapEconomyData> CreateEconomyDictFromNodes(this BigMapSaveData bigMapData)
        {
            var economyDict = new Dictionary<string, BigMapEconomyData>();

            if (bigMapData?.Nodes == null)
            {
                return economyDict;
            }

            foreach (var node in bigMapData.Nodes)
            {
                if (!string.IsNullOrEmpty(node.StageID))
                {
                    var economyData = new BigMapEconomyData(node.StageID)
                    {
                        // 初始化时所有经济数据为 0
                        // 实际游玩后通过关卡结算更新
                        DailyOutput = 0,
                        DailyCost = 0,
                        DailyNetOutput = 0,
                        BuildingCount = 0,
                        GarrisonValue = 0
                    };
                    economyDict.Add(node.StageID, economyData);
                }
            }

            return economyDict;
        }

        /// <summary>
        /// 从大地图节点数据创建经济数据列表（用于序列化）
        /// </summary>
        /// <param name="bigMapData">大地图地理数据</param>
        /// <returns>经济数据列表</returns>
        public static List<BigMapEconomyData> CreateEconomyListFromNodes(this BigMapSaveData bigMapData)
        {
            return new List<BigMapEconomyData>(bigMapData.CreateEconomyDictFromNodes().Values);
        }
    }
}
