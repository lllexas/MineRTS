using UnityEngine;
using System;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 【管理器】大地图管理器 - 业务逻辑协调者
    /// 职责：管理大地图的整体状态，协调渲染器、数据、交互
    ///  - 实现 IMenuPanel 接口，支持 GameFlowController 的状态机管理
    ///  - 持有 BigMapRuntimeRenderer 和 BigMapEdgeRenderer 引用，控制其启用/禁用
    ///  - 管理大地图数据的加载和保存
    /// 设计原则：遵循小零件架构，管理器协调业务，渲染器专注表现
    /// </summary>
    public class BigMapManager : SingletonMono<BigMapManager>, IMenuPanel
    {
        [Header("核心引用")]
        [SerializeField] private GameObject _bigMapRoot; // 大地图根节点（UI 面板）
        [SerializeField] private BigMapRuntimeRenderer _runtimeRenderer; // 节点渲染器
        [SerializeField] private BigMapEdgeRenderer _edgeRenderer; // 连线渲染器
        [SerializeField] private TextAsset _defaultMapJson; // 默认大地图 JSON 文件
        [SerializeField] private GameObject _BG; // 大地图背景图（可选）

        // IMenuPanel 接口实现
        private bool _isOpen = false;
        public GameObject PanelRoot => _bigMapRoot != null ? _bigMapRoot : gameObject;
        public bool IsOpen => _isOpen;

        // 地图加载状态跟踪
        private bool _mapLoaded = false;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("<color=cyan>[BigMapManager]</color> 大地图管理器初始化完成");

            // 自动获取组件引用（如果未在 Inspector 中设置）
            if (_bigMapRoot == null)
            {
                _bigMapRoot = gameObject;
                Debug.Log($"<color=yellow>[BigMapManager]</color> 未设置_bigMapRoot，使用自身 GameObject: {gameObject.name}");
            }

            if (_runtimeRenderer == null)
            {
                _runtimeRenderer = GetComponentInChildren<BigMapRuntimeRenderer>();
                if (_runtimeRenderer != null)
                {
                    Debug.Log($"<color=cyan>[BigMapManager]</color> 自动找到 BigMapRuntimeRenderer: {_runtimeRenderer.gameObject.name}");
                }
                else
                {
                    Debug.LogWarning("<color=orange>[BigMapManager]</color> 未找到 BigMapRuntimeRenderer 组件，需要手动设置引用");
                }
            }

            if (_edgeRenderer == null)
            {
                _edgeRenderer = FindObjectOfType<BigMapEdgeRenderer>();
                if (_edgeRenderer != null)
                {
                    Debug.Log($"<color=cyan>[BigMapManager]</color> 自动找到 BigMapEdgeRenderer: {_edgeRenderer.gameObject.name}");
                }
            }

            // 确保 GPU 缓冲区管理器存在
            EnsureGPUBufferManager();

            // 初始状态由 GameFlowController 控制，这里不主动修改激活状态
        }

        private void Start()
        {
            // 确保初始状态正确
            if (_isOpen != PanelRoot.activeSelf)
            {
                PanelRoot.SetActive(_isOpen);
                _BG.SetActive(_isOpen);
            }
        }

        /// <summary>
        /// IMenuPanel 接口实现：打开大地图面板
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            PanelRoot.SetActive(true);
            _BG.SetActive(true);

            // 激活世界空间渲染器
            if (_runtimeRenderer != null)
            {
                _runtimeRenderer.gameObject.SetActive(true);
            }

            if (_edgeRenderer != null)
            {
                _edgeRenderer.gameObject.SetActive(true);
            }

            // 如果当前没有加载地图，加载默认地图
            if (_runtimeRenderer != null && !HasMapLoaded())
            {
                LoadDefaultMap();
            }

            Debug.Log("<color=cyan>[BigMapManager]</color> 大地图面板已打开");
        }

        /// <summary>
        /// IMenuPanel 接口实现：关闭大地图面板
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            PanelRoot.SetActive(false);
            _BG.SetActive(false);

            // 禁用世界空间渲染器（节省性能）
            if (_runtimeRenderer != null)
            {
                _runtimeRenderer.gameObject.SetActive(false);
            }

            if (_edgeRenderer != null)
            {
                _edgeRenderer.gameObject.SetActive(false);
            }

            Debug.Log("<color=cyan>[BigMapManager]</color> 大地图面板已关闭");
        }

        /// <summary>
        /// 加载指定的大地图数据
        /// </summary>
        public void LoadMap(TextAsset mapJson)
        {
            if (_runtimeRenderer == null)
            {
                Debug.LogError("<color=red>[BigMapManager]</color> 无法加载地图：RuntimeRenderer 为空");
                return;
            }

            if (mapJson == null)
            {
                Debug.LogError("<color=red>[BigMapManager]</color> 地图 JSON 文件为空");
                return;
            }

            Debug.Log($"<color=cyan>[BigMapManager]</color> 正在加载大地图：{mapJson.name}");
            _runtimeRenderer.LoadMapData(mapJson.text);
            _mapLoaded = true;

            // 更新 GPU 缓冲区（如果存在）
            UpdateGPUBuffers(mapJson.text);
        }

        /// <summary>
        /// 加载默认大地图（通常用于主菜单进入大地图时）
        /// </summary>
        public void LoadDefaultMap()
        {
            if (_defaultMapJson == null)
            {
                Debug.LogWarning("<color=orange>[BigMapManager]</color> 未设置默认地图 JSON 文件");
                return;
            }

            LoadMap(_defaultMapJson);
        }

        /// <summary>
        /// 检查是否有地图已加载
        /// </summary>
        private bool HasMapLoaded()
        {
            return _mapLoaded;
        }

        /// <summary>
        /// 确保 GPU 缓冲区管理器存在
        /// </summary>
        private void EnsureGPUBufferManager()
        {
            if (BigMapGPUBufferManager.Instance == null)
            {
                // 在同一个 GameObject 上添加组件
                var gpuManager = gameObject.AddComponent<BigMapGPUBufferManager>();
                Debug.Log($"<color=cyan>[BigMapManager]</color> 自动创建 BigMapGPUBufferManager: {gpuManager.GetType().Name}");
            }
            else
            {
                Debug.Log($"<color=cyan>[BigMapManager]</color> BigMapGPUBufferManager 已存在");
            }
        }

        /// <summary>
        /// 更新 GPU 缓冲区数据
        /// </summary>
        private void UpdateGPUBuffers(string jsonText)
        {
            try
            {
                BigMapSaveData mapData = JsonUtility.FromJson<BigMapSaveData>(jsonText);
                if (mapData == null)
                {
                    Debug.LogWarning("<color=orange>[BigMapManager]</color> 无法解析 JSON 数据，跳过 GPU 缓冲区更新");
                    return;
                }

                // 更新 GPU 缓冲区管理器
                if (BigMapGPUBufferManager.Instance != null)
                {
                    BigMapGPUBufferManager.Instance.UpdateMapData(mapData);
                    Debug.Log("<color=cyan>[BigMapManager]</color> GPU 缓冲区数据已更新");
                }
                else
                {
                    Debug.LogWarning("<color=orange>[BigMapManager]</color> BigMapGPUBufferManager 实例未找到，跳过 GPU 缓冲区更新");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>[BigMapManager]</color> 更新 GPU 缓冲区时发生错误：{e.Message}");
            }
        }

        /// <summary>
        /// 重置大地图视图（居中并恢复默认缩放）
        /// </summary>
        public void ResetView()
        {
            if (_runtimeRenderer != null)
            {
                _runtimeRenderer.ResetView();
                Debug.Log("<color=cyan>[BigMapManager]</color> 大地图视图已重置");
            }
        }

        /// <summary>
        /// 设置摄像机位置和缩放（用于恢复游戏状态）
        /// </summary>
        public void SetCameraView(Vector2 position, float zoomLevel)
        {
            // 注意：BigMapRuntimeRenderer 目前没有直接的 setter 方法
            // 需要扩展 RuntimeRenderer 或在这里实现位置/缩放的设置逻辑
            Debug.Log($"<color=yellow>[BigMapManager]</color> 设置摄像机位置和缩放功能待实现 - 位置：{position}, 缩放：{zoomLevel}");
        }
    }
}
