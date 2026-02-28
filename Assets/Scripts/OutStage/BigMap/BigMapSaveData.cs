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
        public Vector2 CanvasOffset;

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
        public Vector2 Position;

        /// <summary>
        /// 节点类型（可选，用于区分不同类型的节点）
        /// </summary>
        public string NodeType;

        /// <summary>
        /// 节点附加数据（可选，用于扩展）
        /// </summary>
        public string ExtraData;

        /// <summary>
        /// 默认构造函数，自动生成GUID
        /// </summary>
        public BigMapNodeData()
        {
            StageID = Guid.NewGuid().ToString();
            DisplayName = "新节点";
            Position = Vector2.zero;
            NodeType = "Default";
            ExtraData = "";
        }

        /// <summary>
        /// 指定位置的构造函数
        /// </summary>
        public BigMapNodeData(Vector2 position, string displayName = "新节点")
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
        /// 起点节点ID
        /// </summary>
        public string FromNodeID;

        /// <summary>
        /// 终点节点ID
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
}