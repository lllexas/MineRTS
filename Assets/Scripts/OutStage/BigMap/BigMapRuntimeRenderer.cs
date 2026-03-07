using UnityEngine;
using System;
using System.Collections.Generic;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 大地图运行时渲染器 - GameObject 版本
    /// 架构：实例化节点 Prefab，连线由 BigMapEdgeRenderer 使用 GPU 实例化渲染
    /// 核心：JSON 坐标直接对应 Unity 世界坐标，无需 PPU 转换
    /// </summary>
    public class BigMapRuntimeRenderer : MonoBehaviour
    {
        [Header("地图数据")]
        [SerializeField] private TextAsset _mapJsonFile;

        [Header("预制件引用")]
        [Tooltip("节点预制件（需挂载 NodeController）")]
        [SerializeField] private GameObject _nodePrefab;

        [Header("容器")]
        [Tooltip("所有节点的父容器")]
        [SerializeField] private Transform _mapRoot;

        [Header("Z 轴配置")]
        [Tooltip("节点 Z 轴位置（世界坐标）")]
        [SerializeField] private float _nodeZPosition = -2f;

        // 地图数据
        private BigMapSaveData _mapData;

        // 节点映射表：StageID -> NodeController
        private Dictionary<string, NodeController> _nodes = new Dictionary<string, NodeController>();

        // 节点点击回调（可选，用于兼容旧代码）
        private Action<string, string> _nodeClickCallback;

        // 单例引用（用于 NodeController 和 BigMapManager 访问）
        public static BigMapRuntimeRenderer Instance { get; private set; }

        // 初始化状态
        private bool _isInitialized = false;

        private void Awake()
        {
            // 设置单例
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("BigMapRuntimeRenderer: 已存在实例，销毁重复对象");
                Destroy(gameObject);
                return;
            }

            // 自动创建容器（如果未设置）
            if (_mapRoot == null)
            {
                var containerObj = new GameObject("MapRoot");
                _mapRoot = containerObj.transform;
                _mapRoot.SetParent(transform);
            }

            // 解析地图数据
            if (_mapJsonFile != null)
            {
                LoadMapData(_mapJsonFile.text);
            }
            else
            {
                Debug.LogWarning("BigMapRuntimeRenderer: 未指定地图 JSON 文件");
            }

            _isInitialized = true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 加载地图数据并实例化节点
        /// </summary>
        public void LoadMapData(string jsonText)
        {
            try
            {
                // 清理现有内容
                ClearMap();

                _mapData = JsonUtility.FromJson<BigMapSaveData>(jsonText);
                if (_mapData == null)
                {
                    Debug.LogError("BigMapRuntimeRenderer: JSON 解析失败");
                    return;
                }

                Debug.Log($"BigMapRuntimeRenderer: 地图数据加载成功 - {_mapData.Nodes.Count}个节点，{_mapData.Edges.Count}条连线");

                // 实例化节点
                foreach (var nodeData in _mapData.Nodes)
                {
                    InstantiateNode(nodeData);
                }

                // 设置连线数据到 BigMapEdgeRenderer
                SetupEdgeRenderer();

                Debug.Log("BigMapRuntimeRenderer: 地图渲染完成");
            }
            catch (Exception e)
            {
                Debug.LogError($"BigMapRuntimeRenderer: 加载地图数据时发生错误：{e.Message}");
            }
        }

        /// <summary>
        /// 实例化单个节点
        /// </summary>
        private void InstantiateNode(BigMapNodeData nodeData)
        {
            if (_nodePrefab == null)
            {
                Debug.LogError("BigMapRuntimeRenderer: 节点预制件未设置");
                return;
            }

            // 实例化
            GameObject nodeObj = Instantiate(_nodePrefab, _mapRoot);
            NodeController nodeController = nodeObj.GetComponent<NodeController>();

            if (nodeController == null)
            {
                Debug.LogError($"BigMapRuntimeRenderer: 预制件 {nodeObj.name} 没有 NodeController 组件");
                Destroy(nodeObj);
                return;
            }

            // 初始化（传入 Z 轴位置）
            nodeController.Init(nodeData, _nodeZPosition);

            // 添加到映射表
            _nodes[nodeData.StageID] = nodeController;
        }

        /// <summary>
        /// 设置连线渲染器
        /// </summary>
        private void SetupEdgeRenderer()
        {
            // 获取或创建 BigMapEdgeRenderer
            var edgeRenderer = BigMapEdgeRenderer.Instance;
            if (edgeRenderer == null)
            {
                Debug.LogWarning("BigMapRuntimeRenderer: BigMapEdgeRenderer 实例未找到");
                return;
            }

            // 设置连线数据
            edgeRenderer.SetEdges(_mapData.Edges);

            // 设置节点位置
            edgeRenderer.SetNodePositions(GetNodePositions());
        }

        /// <summary>
        /// 获取所有节点位置映射表
        /// </summary>
        private Dictionary<string, Vector3> GetNodePositions()
        {
            var positions = new Dictionary<string, Vector3>();
            foreach (var kvp in _nodes)
            {
                positions[kvp.Key] = kvp.Value.GetTransform().position;
            }
            return positions;
        }

        /// <summary>
        /// 清理地图内容
        /// </summary>
        private void ClearMap()
        {
            // 销毁所有节点
            foreach (var node in _nodes.Values)
            {
                if (node != null)
                {
                    Destroy(node.gameObject);
                }
            }
            _nodes.Clear();

            // 清空连线渲染器
            var edgeRenderer = BigMapEdgeRenderer.Instance;
            if (edgeRenderer != null)
            {
                edgeRenderer.ClearEdges();
            }
        }

        /// <summary>
        /// 设置节点点击回调
        /// </summary>
        public void SetNodeClickCallback(Action<string, string> callback)
        {
            _nodeClickCallback = callback;
        }

        /// <summary>
        /// 节点点击事件（由 NodeController 通过事件总线触发）
        /// 此方法保留仅为兼容旧代码，新代码请使用 PostSystem
        /// </summary>
        public void OnNodeClicked(string stageId, string displayName)
        {
            Debug.Log($"BigMapRuntimeRenderer: 节点被点击 - 关卡 ID: {stageId}, 名称：{displayName}");

            _nodeClickCallback?.Invoke(stageId, displayName);
        }

        /// <summary>
        /// 获取节点控制器
        /// </summary>
        public NodeController GetNode(string nodeId)
        {
            _nodes.TryGetValue(nodeId, out NodeController node);
            return node;
        }

        /// <summary>
        /// 设置节点选中状态
        /// </summary>
        public void SetNodeSelected(string nodeId, bool selected)
        {
            if (_nodes.TryGetValue(nodeId, out NodeController node))
            {
                node.SetSelected(selected);
            }
        }

        /// <summary>
        /// 重置视图
        /// </summary>
        public void ResetView()
        {
            Debug.Log("BigMapRuntimeRenderer: 视图重置功能需要摄像机控制器配合实现");
        }

        /// <summary>
        /// 获取当前地图数据
        /// </summary>
        public BigMapSaveData GetCurrentMapData()
        {
            return _mapData;
        }
    }
}
