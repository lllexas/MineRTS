using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 大地图GPU缓冲区管理器
    /// 职责：将BigMapData转换为GPU可访问的ComputeBuffer，供Shader使用
    /// 架构：SingletonMono，管理与Shader的数据通信
    /// </summary>
    public class BigMapGPUBufferManager : SingletonMono<BigMapGPUBufferManager>
    {
        [Header("GPU缓冲区设置")]
        [SerializeField] private int _maxNodes = 1024;
        [SerializeField] private int _maxEdges = 2048;

        [Header("Shader属性名称")]
        [SerializeField] private string _nodeBufferName = "_NodeBuffer";
        [SerializeField] private string _nodeCountName = "_NodeCount";
        [SerializeField] private string _edgeBufferName = "_EdgeBuffer";
        [SerializeField] private string _edgeCountName = "_EdgeCount";

        [Header("调试")]
        [SerializeField] private bool _logBufferUpdates = true;

        // GPU缓冲区
        private ComputeBuffer _nodeBuffer;
        private ComputeBuffer _edgeBuffer;

        // 缓冲区数据格式（与Shader中的结构体匹配）
        // 注意：所有结构体必须是16字节对齐
        private struct NodeGPUData
        {
            public Vector4 PositionAndRadius; // x,y:位置, z:半径, w:保留
            public Vector4 ColorAndFlags;     // x,y,z,w:颜色(RGBA), x:标志位存储在另一个字段
            public Vector4 Attributes;        // x:标志位, yzw:保留
        }

        private struct EdgeGPUData
        {
            public Vector4 FromPosAndThickness; // x,y:起点位置, z:厚度, w:保留
            public Vector4 ToPosAndFlags;       // x,y:终点位置, z:保留, w:标志位
            public Vector4 ColorAndAttributes;  // x,y,z,w:颜色(RGBA), 其他属性
        }

        // 当前数据
        private BigMapSaveData _currentMapData;
        private bool _buffersInitialized = false;

        protected override void Awake()
        {
            base.Awake();

            // 初始化GPU缓冲区
            InitializeGPUBuffers();

            Debug.Log($"<color=cyan>[BigMapGPUBufferManager]</color> GPU缓冲区管理器初始化完成 - 最大节点: {_maxNodes}, 最大边: {_maxEdges}");
        }

        private void OnDestroy()
        {
            // 释放GPU缓冲区
            ReleaseGPUBuffers();
        }

        /// <summary>
        /// 初始化GPU缓冲区
        /// </summary>
        private void InitializeGPUBuffers()
        {
            // 创建节点缓冲区
            int nodeStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NodeGPUData));
            _nodeBuffer = new ComputeBuffer(_maxNodes, nodeStride, ComputeBufferType.Structured);

            // 创建边缓冲区
            int edgeStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(EdgeGPUData));
            _edgeBuffer = new ComputeBuffer(_maxEdges, edgeStride, ComputeBufferType.Structured);

            _buffersInitialized = true;

            if (_logBufferUpdates)
            {
                Debug.Log($"<color=cyan>[BigMapGPUBufferManager]</color> GPU缓冲区已创建 - 节点: {_maxNodes}x{nodeStride}字节, 边: {_maxEdges}x{edgeStride}字节");
            }
        }

        /// <summary>
        /// 释放GPU缓冲区
        /// </summary>
        private void ReleaseGPUBuffers()
        {
            if (_nodeBuffer != null)
            {
                _nodeBuffer.Release();
                _nodeBuffer = null;
            }

            if (_edgeBuffer != null)
            {
                _edgeBuffer.Release();
                _edgeBuffer = null;
            }

            _buffersInitialized = false;

            if (_logBufferUpdates)
            {
                Debug.Log($"<color=cyan>[BigMapGPUBufferManager]</color> GPU缓冲区已释放");
            }
        }

        /// <summary>
        /// 更新地图数据到GPU缓冲区
        /// </summary>
        public void UpdateMapData(BigMapSaveData mapData)
        {
            if (!_buffersInitialized)
            {
                Debug.LogWarning("BigMapGPUBufferManager: GPU缓冲区未初始化，无法更新数据");
                return;
            }

            if (mapData == null)
            {
                Debug.LogWarning("BigMapGPUBufferManager: 地图数据为空，清空缓冲区");
                ClearBuffers();
                return;
            }

            _currentMapData = mapData;

            // 转换节点数据
            UpdateNodeBuffer(mapData.Nodes);

            // 转换边数据（需要节点位置查找）
            UpdateEdgeBuffer(mapData.Nodes, mapData.Edges);

            if (_logBufferUpdates)
            {
                Debug.Log($"<color=cyan>[BigMapGPUBufferManager]</color> GPU数据更新完成 - 节点: {mapData.Nodes.Count}, 边: {mapData.Edges.Count}");
            }
        }

        /// <summary>
        /// 更新节点缓冲区
        /// </summary>
        private void UpdateNodeBuffer(List<BigMapNodeData> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                ClearNodeBuffer();
                return;
            }

            // 检查容量
            if (nodes.Count > _maxNodes)
            {
                Debug.LogWarning($"BigMapGPUBufferManager: 节点数量({nodes.Count})超过最大容量({_maxNodes})，将截断");
            }

            // 准备GPU数据
            NodeGPUData[] nodeData = new NodeGPUData[Mathf.Min(nodes.Count, _maxNodes)];

            for (int i = 0; i < nodeData.Length && i < nodes.Count; i++)
            {
                var node = nodes[i];
                Color nodeColor = GetNodeColorByType(node.NodeType);

                nodeData[i] = new NodeGPUData
                {
                    PositionAndRadius = new Vector4(
                        node.Position.x,
                        node.Position.y,
                        0.2f, // 半径
                        0 // 保留
                    ),
                    ColorAndFlags = new Vector4(
                        nodeColor.r,
                        nodeColor.g,
                        nodeColor.b,
                        nodeColor.a
                    ),
                    Attributes = new Vector4(
                        GetNodeFlags(node.NodeType), // 标志位
                        0, 0, 0 // 保留
                    )
                };
            }

            // 上传到GPU
            _nodeBuffer.SetData(nodeData);
        }

        /// <summary>
        /// 更新边缓冲区
        /// </summary>
        private void UpdateEdgeBuffer(List<BigMapNodeData> nodes, List<BigMapEdgeData> edges)
        {
            if (edges == null || edges.Count == 0 || nodes == null || nodes.Count == 0)
            {
                ClearEdgeBuffer();
                return;
            }

            // 创建节点ID到位置的查找表
            Dictionary<string, Vector2> nodePositionLookup = new Dictionary<string, Vector2>();
            foreach (var node in nodes)
            {
                nodePositionLookup[node.StageID] = node.Position;
            }

            // 检查容量
            if (edges.Count > _maxEdges)
            {
                Debug.LogWarning($"BigMapGPUBufferManager: 边数量({edges.Count})超过最大容量({_maxEdges})，将截断");
            }

            // 准备GPU数据
            EdgeGPUData[] edgeData = new EdgeGPUData[Mathf.Min(edges.Count, _maxEdges)];

            int validEdgeCount = 0;
            for (int i = 0; i < edges.Count && validEdgeCount < edgeData.Length; i++)
            {
                var edge = edges[i];

                // 查找起点和终点位置
                if (!nodePositionLookup.TryGetValue(edge.FromNodeID, out Vector2 fromPos) ||
                    !nodePositionLookup.TryGetValue(edge.ToNodeID, out Vector2 toPos))
                {
                    // 跳过无效的边（节点ID不存在）
                    continue;
                }

                Color edgeColor = GetEdgeColorByDirection(edge.Direction);
                edgeData[validEdgeCount] = new EdgeGPUData
                {
                    FromPosAndThickness = new Vector4(
                        fromPos.x,
                        fromPos.y,
                        0.05f, // 厚度
                        0 // 保留
                    ),
                    ToPosAndFlags = new Vector4(
                        toPos.x,
                        toPos.y,
                        0, // 保留
                        (uint)edge.Direction // 标志位
                    ),
                    ColorAndAttributes = new Vector4(
                        edgeColor.r,
                        edgeColor.g,
                        edgeColor.b,
                        edgeColor.a
                    )
                };

                validEdgeCount++;
            }

            // 如果有效边数量少于数组长度，调整数组
            if (validEdgeCount < edgeData.Length)
            {
                System.Array.Resize(ref edgeData, validEdgeCount);
            }

            // 上传到GPU
            if (validEdgeCount > 0)
            {
                _edgeBuffer.SetData(edgeData);
            }
            else
            {
                ClearEdgeBuffer();
            }
        }

        /// <summary>
        /// 清空节点缓冲区
        /// </summary>
        private void ClearNodeBuffer()
        {
            NodeGPUData[] emptyData = new NodeGPUData[1]; // 最小尺寸
            _nodeBuffer.SetData(emptyData);
        }

        /// <summary>
        /// 清空边缓冲区
        /// </summary>
        private void ClearEdgeBuffer()
        {
            EdgeGPUData[] emptyData = new EdgeGPUData[1]; // 最小尺寸
            _edgeBuffer.SetData(emptyData);
        }

        /// <summary>
        /// 清空所有缓冲区
        /// </summary>
        private void ClearBuffers()
        {
            ClearNodeBuffer();
            ClearEdgeBuffer();
        }

        /// <summary>
        /// 获取节点缓冲区（供 DiffusionFieldBridge 使用）
        /// </summary>
        public ComputeBuffer GetNodeBuffer()
        {
            return _nodeBuffer;
        }

        /// <summary>
        /// 获取边缓冲区（可选）
        /// </summary>
        public ComputeBuffer GetEdgeBuffer()
        {
            return _edgeBuffer;
        }

        /// <summary>
        /// 将GPU缓冲区设置到材质
        /// </summary>
        public void ApplyBuffersToMaterial(Material material)
        {
            if (!_buffersInitialized || material == null)
            {
                Debug.LogWarning("BigMapGPUBufferManager: 无法应用缓冲区到材质 - 缓冲区未初始化或材质为空");
                return;
            }

            // 设置节点缓冲区
            material.SetBuffer(_nodeBufferName, _nodeBuffer);
            material.SetInt(_nodeCountName, _currentMapData?.Nodes?.Count ?? 0);

            // 设置边缓冲区
            material.SetBuffer(_edgeBufferName, _edgeBuffer);
            material.SetInt(_edgeCountName, _currentMapData?.Edges?.Count ?? 0);

            if (_logBufferUpdates)
            {
                Debug.Log($"<color=cyan>[BigMapGPUBufferManager]</color> GPU缓冲区已应用到材质: {material.name}");
            }
        }

        /// <summary>
        /// 根据节点类型获取颜色
        /// </summary>
        private Color GetNodeColorByType(string nodeType)
        {
            switch (nodeType?.ToLower())
            {
                case "start":
                    return new Color(0.1f, 0.8f, 0.1f, 0.8f); // 绿色
                case "boss":
                    return new Color(0.8f, 0.1f, 0.1f, 0.8f); // 红色
                case "shop":
                    return new Color(0.9f, 0.7f, 0.1f, 0.8f); // 黄色
                case "event":
                    return new Color(0.6f, 0.2f, 0.8f, 0.8f); // 紫色
                default:
                    return new Color(0.1f, 0.5f, 0.9f, 0.8f); // 默认蓝色
            }
        }

        /// <summary>
        /// 根据连线方向获取颜色
        /// </summary>
        private Color GetEdgeColorByDirection(EdgeDirection direction)
        {
            switch (direction)
            {
                case EdgeDirection.Unidirectional:
                    return new Color(0.8f, 0.3f, 0.1f, 0.6f); // 橙色（单向）
                case EdgeDirection.Bidirectional:
                    return new Color(0.3f, 0.6f, 0.9f, 0.4f); // 蓝色（双向）
                default:
                    return new Color(0.5f, 0.5f, 0.5f, 0.3f); // 灰色
            }
        }

        /// <summary>
        /// 根据节点类型获取标志位
        /// </summary>
        private uint GetNodeFlags(string nodeType)
        {
            uint flags = 0;

            if (nodeType?.ToLower() == "start")
                flags |= 1 << 0; // 起点标志

            if (nodeType?.ToLower() == "boss")
                flags |= 1 << 1; // Boss标志

            if (nodeType?.ToLower() == "shop")
                flags |= 1 << 2; // 商店标志

            return flags;
        }

        /// <summary>
        /// 颜色转换为32位无符号整数（RGBA格式）
        /// </summary>
        private uint ColorToUInt(Color color)
        {
            uint r = (uint)(color.r * 255) & 0xFF;
            uint g = (uint)(color.g * 255) & 0xFF;
            uint b = (uint)(color.b * 255) & 0xFF;
            uint a = (uint)(color.a * 255) & 0xFF;

            return (r << 24) | (g << 16) | (b << 8) | a;
        }

        /// <summary>
        /// 颜色转换为float（用于Shader中的颜色压缩）
        /// 注意：这不是精确转换，但足够用于颜色插值
        /// </summary>
        private float ColorToFloat(Color color)
        {
            // 将RGBA打包为32位整数，然后转换为float
            // 注意：这不是可逆转换，但足够用于颜色比较和插值
            uint packed = ColorToUInt(color);
            return asfloat(packed);
        }

        /// <summary>
        /// 安全的uint到float转换（避免C#编译器警告）
        /// </summary>
        private float asfloat(uint value)
        {
            // 使用不安全代码或BitConverter
            return BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
        }

        /// <summary>
        /// 32位无符号整数转换为颜色
        /// </summary>
        private Color UIntToColor(uint value)
        {
            float r = ((value >> 24) & 0xFF) / 255.0f;
            float g = ((value >> 16) & 0xFF) / 255.0f;
            float b = ((value >> 8) & 0xFF) / 255.0f;
            float a = (value & 0xFF) / 255.0f;

            return new Color(r, g, b, a);
        }

        /// <summary>
        /// 获取当前地图数据
        /// </summary>
        public BigMapSaveData GetCurrentMapData()
        {
            return _currentMapData;
        }

        /// <summary>
        /// 检查GPU缓冲区是否已初始化
        /// </summary>
        public bool AreBuffersInitialized()
        {
            return _buffersInitialized;
        }

        /// <summary>
        /// 检查 GPU 缓冲区是否就绪（已初始化且 Buffer 有效）
        /// </summary>
        public bool IsReady
        {
            get
            {
                if (!_buffersInitialized) return false;
                if (_nodeBuffer == null) return false;
                return true;
            }
        }

        /// <summary>
        /// 设置最大节点数量（需要重新初始化缓冲区）
        /// </summary>
        public void SetMaxNodes(int maxNodes)
        {
            if (maxNodes <= 0)
            {
                Debug.LogError("BigMapGPUBufferManager: 最大节点数量必须大于0");
                return;
            }

            _maxNodes = maxNodes;

            // 重新初始化缓冲区
            ReleaseGPUBuffers();
            InitializeGPUBuffers();

            // 重新应用当前数据
            if (_currentMapData != null)
            {
                UpdateMapData(_currentMapData);
            }
        }

        /// <summary>
        /// 设置最大边数量（需要重新初始化缓冲区）
        /// </summary>
        public void SetMaxEdges(int maxEdges)
        {
            if (maxEdges <= 0)
            {
                Debug.LogError("BigMapGPUBufferManager: 最大边数量必须大于0");
                return;
            }

            _maxEdges = maxEdges;

            // 重新初始化缓冲区
            ReleaseGPUBuffers();
            InitializeGPUBuffers();

            // 重新应用当前数据
            if (_currentMapData != null)
            {
                UpdateMapData(_currentMapData);
            }
        }
    }
}