using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 大地图运行时渲染器 - 基于正交摄像机投影的视口控制器
    /// 架构：采用图形学投影管线，摒弃传统UI布局思维
    /// 核心：世界空间坐标 → 屏幕像素坐标的正交投影变换
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BigMapRuntimeRenderer : MonoBehaviour
    {
        [Header("地图数据")]
        [SerializeField] private TextAsset _mapJsonFile;

        [Header("目标摄像机")]
        [Tooltip("渲染地图使用的Unity正交摄像机，默认为Camera.main")]
        [SerializeField] private Camera _targetCamera;

        // UI引用
        private UIDocument _uiDocument;
        private VisualElement _viewport;
        private RuntimeMapContainer _mapContainer;

        // 投影参数
        private float _basePPU = -1f;                    // 基础像素每单位比例（仅初始化时计算一次）
        private float _currentPPU = 1.0f;                // 当前像素每单位比例（动态计算）

        // 地图数据
        private BigMapSaveData _mapData;

        // 初始化状态
        private bool _isInitialized = false;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                Debug.LogError("BigMapRuntimeRenderer: 需要UIDocument组件");
                return;
            }

            // 初始化目标摄像机
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
                if (_targetCamera == null)
                {
                    Debug.LogError("BigMapRuntimeRenderer: 未找到主摄像机，请手动指定目标摄像机");
                    return;
                }
            }

            // 初始化UI结构
            InitializeUIStructure();

            // 解析地图数据
            if (_mapJsonFile != null)
            {
                LoadMapData(_mapJsonFile.text);
            }
            else
            {
                Debug.LogWarning("BigMapRuntimeRenderer: 未指定地图JSON文件");
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized) return;

            // 检查UI结构是否仍然有效
            if (_uiDocument != null && _uiDocument.rootVisualElement != null)
            {
                // 如果_viewport为空或未附加到root，重新初始化
                if (_viewport == null || !_uiDocument.rootVisualElement.Contains(_viewport))
                {
                    Debug.Log("BigMapRuntimeRenderer: UI结构失效，重新初始化");
                    InitializeUIStructure();

                    // 重新加载地图数据（如果已有数据）
                    if (_mapData != null && _mapContainer != null)
                    {
                        // 使用基础PPU（如果已计算），否则使用当前PPU
                        float ppuToUse = _basePPU > 0 ? _basePPU : _currentPPU;
                        _mapContainer.RenderMap(_mapData, ppuToUse);
                    }
                }
            }
        }

        private void Start()
        {
            // Start方法可以留空，变换在LateUpdate中每帧更新
        }

        private void LateUpdate()
        {
            // 只有在激活状态且已初始化时才更新
            if (!_isInitialized || !isActiveAndEnabled) return;
            if (_targetCamera == null || _mapContainer == null) return;

            // 动态计算当前PPU：基于当前摄像机正交尺寸
            _currentPPU = Screen.height / (_targetCamera.orthographicSize * 2f);

            // 计算缩放比例：CurrentPPU / BasePPU
            if (_basePPU <= 0) return; // 基础PPU未初始化
            float scaleRatio = _currentPPU / _basePPU;

            // 获取摄像机世界位置
            Vector3 cameraWorldPos = _targetCamera.transform.position;

            // 计算平移量（严格遵循正交投影公式）
            // Translate_X = (Screen.width / 2f) - (Camera.position.x * CurrentPPU)
            // Translate_Y = (Screen.height / 2f) - (-Camera.position.y * CurrentPPU) // Y轴反转
            float translateX = (Screen.width / 2f) - (cameraWorldPos.x * _currentPPU);
            float translateY = (Screen.height / 2f) - (-cameraWorldPos.y * _currentPPU);

            // 应用容器级变换（O(1)操作，避免遍历节点）
            _mapContainer.style.translate = new Translate(translateX, translateY);
            _mapContainer.style.scale = new Scale(new Vector3(scaleRatio, scaleRatio, 1));

            // 更新缩放比例（用于连线绘制）
            _mapContainer.SetZoomRatio(scaleRatio);

            // 强制重绘（触发连线绘制）
            _mapContainer.MarkDirtyRepaint();
        }


        /// <summary>
        /// 初始化UI结构：Viewport → MapContainer
        /// </summary>
        private void InitializeUIStructure()
        {
            var root = _uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("BigMapRuntimeRenderer: UIDocument的rootVisualElement为空");
                return;
            }

            // 清除现有内容
            root.Clear();

            // 创建Viewport（全屏背景，允许点击事件穿透）
            _viewport = new VisualElement();
            _viewport.name = "BigMapViewport";
            _viewport.style.width = Length.Percent(100);
            _viewport.style.height = Length.Percent(100);
            _viewport.style.position = Position.Relative;
            _viewport.style.overflow = Overflow.Hidden;
            _viewport.style.backgroundColor = Color.clear;
            _viewport.pickingMode = PickingMode.Ignore;  // 允许点击事件穿透到下层UI

            // 创建地图容器
            _mapContainer = new RuntimeMapContainer();
            _mapContainer.name = "MapContainer";
            _mapContainer.style.position = Position.Absolute;
            _mapContainer.style.width = Length.Percent(100);
            _mapContainer.style.height = Length.Percent(100);

            // 关键：设置变换原点为左上角 (0, 0)
            _mapContainer.style.transformOrigin = new TransformOrigin(new Length(0), new Length(0));

            // 构建层级
            _viewport.Add(_mapContainer);
            root.Add(_viewport);

            Debug.Log("BigMapRuntimeRenderer: UI结构初始化完成");
        }

        /// <summary>
        /// 加载地图数据并渲染
        /// </summary>
        public void LoadMapData(string jsonText)
        {
            try
            {
                _mapData = JsonUtility.FromJson<BigMapSaveData>(jsonText);
                if (_mapData == null)
                {
                    Debug.LogError("BigMapRuntimeRenderer: JSON解析失败");
                    return;
                }

                Debug.Log($"BigMapRuntimeRenderer: 地图数据加载成功 - {_mapData.Nodes.Count}个节点，{_mapData.Edges.Count}条连线");

                // 计算基础PPU：仅在地图加载时计算一次
                if (_targetCamera == null)
                {
                    Debug.LogError("BigMapRuntimeRenderer: 目标摄像机未设置");
                    return;
                }

                _basePPU = Screen.height / (_targetCamera.orthographicSize * 2f);
                Debug.Log($"BigMapRuntimeRenderer: 基础PPU计算完成 - {_basePPU:F2} (屏幕高度: {Screen.height}, 正交尺寸: {_targetCamera.orthographicSize})");

                // 渲染地图（传递基础PPU）
                if (_mapContainer != null)
                {
                    _mapContainer.RenderMap(_mapData, _basePPU);
                }

                // 应用保存的视图状态（如果存在）
                if (_mapData.CanvasOffset != Vector2.zero || Math.Abs(_mapData.CanvasZoom - 1.0f) > 0.001f)
                {
                    // 注意：这里需要将保存的CanvasOffset转换为世界坐标
                    // 暂时简化处理，直接重置到初始状态
                    ResetView();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"BigMapRuntimeRenderer: 加载地图数据时发生错误: {e.Message}");
            }
        }

        /// <summary>
        /// 指针按下事件（中键/右键开始拖拽）
        /// </summary>

        /// <summary>
        /// 指针移动事件（拖拽中）
        /// </summary>

        /// <summary>
        /// 指针抬起事件（结束拖拽）
        /// </summary>

        /// <summary>
        /// 滚轮事件（缩放）
        /// </summary>


        /// <summary>
        /// 屏幕坐标 → 世界坐标转换
        /// </summary>

        /// <summary>
        /// 世界坐标 → 屏幕坐标转换
        /// </summary>

        /// <summary>
        /// 重置视图到初始状态
        /// </summary>
        public void ResetView()
        {
            // 注意：重构后，摄像机控制由CameraController负责
            // 此方法不再生效，保留仅为API兼容性
            Debug.LogWarning("BigMapRuntimeRenderer: ResetView()已废弃，摄像机控制由CameraController负责");
        }

        /// <summary>
        /// 设置地图数据（运行时切换地图）
        /// </summary>
        public void SetMapData(BigMapSaveData data)
        {
            _mapData = data;

            // 计算新的基础PPU
            if (_targetCamera != null)
            {
                _basePPU = Screen.height / (_targetCamera.orthographicSize * 2f);
            }

            if (_mapContainer != null)
            {
                _mapContainer.RenderMap(_mapData, _basePPU > 0 ? _basePPU : _currentPPU);
            }
        }

        /// <summary>
        /// 获取节点元素（根据节点ID）
        /// </summary>
        public RuntimeNodeElement GetNodeElement(string nodeId)
        {
            return _mapContainer?.GetNodeElement(nodeId);
        }

        /// <summary>
        /// 设置节点点击回调
        /// </summary>
        public void SetNodeClickCallback(Action<string, string> callback)
        {
            if (_mapContainer != null)
            {
                _mapContainer.SetNodeClickCallback(callback);
            }
        }
    }
}