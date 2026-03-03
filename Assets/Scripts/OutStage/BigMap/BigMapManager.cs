using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 【管理器】大地图管理器 - 业务逻辑协调者
    /// 职责：管理大地图的整体状态，协调渲染器、数据、交互
    ///  - 实现IMenuPanel接口，支持GameFlowController的状态机管理
    ///  - 持有BigMapRuntimeRenderer引用，控制其启用/禁用
    ///  - 处理节点点击回调，连接到GameFlowController
    ///  - 管理大地图数据的加载和保存
    /// 设计原则：遵循小零件架构，管理器协调业务，渲染器专注表现
    /// </summary>
    public class BigMapManager : SingletonMono<BigMapManager>, IMenuPanel
    {
        [Header("核心引用")]
        [SerializeField] private GameObject _bigMapRoot; // 大地图根节点（通常就是BigMapRuntimeRenderer的GameObject）
        [SerializeField] private BigMapRuntimeRenderer _runtimeRenderer;
        [SerializeField] private TextAsset _defaultMapJson; // 默认大地图JSON文件
        [SerializeField] private GameObject _BG; // 大地图背景图（可选）

        // IMenuPanel接口实现
        private bool _isOpen = false;
        public GameObject PanelRoot => _bigMapRoot != null ? _bigMapRoot : gameObject;
        public bool IsOpen => _isOpen;

        // 地图加载状态跟踪
        private bool _mapLoaded = false;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("<color=cyan>[BigMapManager]</color> 大地图管理器初始化完成");

            // 自动获取组件引用（如果未在Inspector中设置）
            if (_bigMapRoot == null)
            {
                _bigMapRoot = gameObject;
                Debug.Log($"<color=yellow>[BigMapManager]</color> 未设置_bigMapRoot，使用自身GameObject: {gameObject.name}");
            }

            if (_runtimeRenderer == null)
            {
                _runtimeRenderer = GetComponentInChildren<BigMapRuntimeRenderer>();
                if (_runtimeRenderer != null)
                {
                    Debug.Log($"<color=cyan>[BigMapManager]</color> 自动找到BigMapRuntimeRenderer: {_runtimeRenderer.gameObject.name}");
                }
                else
                {
                    Debug.LogWarning("<color=orange>[BigMapManager]</color> 未找到BigMapRuntimeRenderer组件，需要手动设置引用");
                }
            }

            // 确保GPU缓冲区管理器存在
            EnsureGPUBufferManager();

            // 初始状态由GameFlowController控制，这里不主动修改激活状态
        }

        private void Start()
        {
            // 确保初始状态正确
            if (_isOpen != PanelRoot.activeSelf)
            {
                PanelRoot.SetActive(_isOpen);
                _BG.SetActive(_isOpen);
            }

            // 设置节点点击回调
            SetupNodeClickCallback();
        }

        /// <summary>
        /// 设置节点点击回调，连接到GameFlowController
        /// </summary>
        private void SetupNodeClickCallback()
        {
            if (_runtimeRenderer != null)
            {
                _runtimeRenderer.SetNodeClickCallback(OnNodeClicked);
                Debug.Log("<color=cyan>[BigMapManager]</color> 节点点击回调已设置");
            }
            else
            {
                Debug.LogWarning("<color=orange>[BigMapManager]</color> 无法设置节点点击回调：RuntimeRenderer为空");
            }
        }

        /// <summary>
        /// 节点点击事件处理
        /// 当用户点击大地图节点时，触发关卡加载流程
        /// </summary>
        private void OnNodeClicked(string stageId, string displayName)
        {
            Debug.Log($"<color=yellow>[BigMapManager]</color> 节点被点击 - 关卡ID: {stageId}, 名称: {displayName}");

            // 验证当前状态
            if (!_isOpen)
            {
                Debug.LogWarning("<color=orange>[BigMapManager]</color> 大地图未打开，忽略节点点击");
                return;
            }

            // 触发关卡加载（通过GameFlowController）
            if (GameFlowController.Instance != null)
            {
                GameFlowController.Instance.EnterStage(stageId);
            }
            else
            {
                Debug.LogError("<color=red>[BigMapManager]</color> GameFlowController实例未找到，无法进入关卡");
            }
        }

        /// <summary>
        /// 加载指定的大地图数据
        /// </summary>
        public void LoadMap(TextAsset mapJson)
        {
            if (_runtimeRenderer == null)
            {
                Debug.LogError("<color=red>[BigMapManager]</color> 无法加载地图：RuntimeRenderer为空");
                return;
            }

            if (mapJson == null)
            {
                Debug.LogError("<color=red>[BigMapManager]</color> 地图JSON文件为空");
                return;
            }

            Debug.Log($"<color=cyan>[BigMapManager]</color> 正在加载大地图: {mapJson.name}");
            _runtimeRenderer.LoadMapData(mapJson.text);
            _mapLoaded = true;

            // 更新GPU缓冲区（如果存在）
            UpdateGPUBuffers(mapJson.text);
        }

        /// <summary>
        /// 加载默认大地图（通常用于主菜单进入大地图时）
        /// </summary>
        public void LoadDefaultMap()
        {
            if (_defaultMapJson == null)
            {
                Debug.LogWarning("<color=orange>[BigMapManager]</color> 未设置默认地图JSON文件");
                return;
            }

            LoadMap(_defaultMapJson);
        }

        /// <summary>
        /// IMenuPanel接口实现：打开大地图面板
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            PanelRoot.SetActive(true);
            _BG.SetActive(true);

            // 如果当前没有加载地图，加载默认地图
            if (_runtimeRenderer != null && !HasMapLoaded())
            {
                LoadDefaultMap();
            }

            // 自动初始化大地图摄像机（重置视图到默认状态）
            if (_runtimeRenderer != null)
            {
                _runtimeRenderer.ResetView();
                Debug.Log("<color=cyan>[BigMapManager]</color> 大地图摄像机视图已重置");
            }

            // 确保节点点击回调已设置
            SetupNodeClickCallback();

            Debug.Log("<color=cyan>[BigMapManager]</color> 大地图面板已打开");
        }

        /// <summary>
        /// IMenuPanel接口实现：关闭大地图面板
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            PanelRoot.SetActive(false);
            _BG.SetActive(false);
            Debug.Log("<color=cyan>[BigMapManager]</color> 大地图面板已关闭");
        }

        /// <summary>
        /// 检查是否有地图已加载
        /// </summary>
        private bool HasMapLoaded()
        {
            return _mapLoaded;
        }

        /// <summary>
        /// 确保GPU缓冲区管理器存在
        /// </summary>
        private void EnsureGPUBufferManager()
        {
            if (BigMapGPUBufferManager.Instance == null)
            {
                // 在同一个GameObject上添加组件
                var gpuManager = gameObject.AddComponent<BigMapGPUBufferManager>();
                Debug.Log($"<color=cyan>[BigMapManager]</color> 自动创建BigMapGPUBufferManager: {gpuManager.GetType().Name}");
            }
            else
            {
                Debug.Log($"<color=cyan>[BigMapManager]</color> BigMapGPUBufferManager已存在");
            }
        }

        /// <summary>
        /// 更新GPU缓冲区数据
        /// </summary>
        private void UpdateGPUBuffers(string jsonText)
        {
            try
            {
                BigMapSaveData mapData = JsonUtility.FromJson<BigMapSaveData>(jsonText);
                if (mapData == null)
                {
                    Debug.LogWarning("<color=orange>[BigMapManager]</color> 无法解析JSON数据，跳过GPU缓冲区更新");
                    return;
                }

                // 更新GPU缓冲区管理器
                if (BigMapGPUBufferManager.Instance != null)
                {
                    BigMapGPUBufferManager.Instance.UpdateMapData(mapData);
                    Debug.Log("<color=cyan>[BigMapManager]</color> GPU缓冲区数据已更新");
                }
                else
                {
                    Debug.LogWarning("<color=orange>[BigMapManager]</color> BigMapGPUBufferManager实例未找到，跳过GPU缓冲区更新");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>[BigMapManager]</color> 更新GPU缓冲区时发生错误: {e.Message}");
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
            // 注意：BigMapRuntimeRenderer目前没有直接的setter方法
            // 需要扩展RuntimeRenderer或在这里实现位置/缩放的设置逻辑
            Debug.Log($"<color=yellow>[BigMapManager]</color> 设置摄像机位置和缩放功能待实现 - 位置: {position}, 缩放: {zoomLevel}");
        }
    }
}