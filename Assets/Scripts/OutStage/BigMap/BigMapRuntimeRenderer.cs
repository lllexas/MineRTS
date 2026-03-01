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

        [Header("摄像机参数")]
        [Tooltip("正交摄像机视野半高（世界单位）")]
        [SerializeField] private float _orthographicSize = 10f;

        [Header("交互参数")]
        [SerializeField] private float _panSpeed = 1.0f;
        [SerializeField] private float _zoomSpeed = 0.1f;
        [SerializeField] private float _minZoom = 0.1f;
        [SerializeField] private float _maxZoom = 5.0f;

        // UI引用
        private UIDocument _uiDocument;
        private VisualElement _viewport;
        private RuntimeMapContainer _mapContainer;

        // 摄像机状态
        private Vector2 _cameraWorldPos = Vector2.zero;  // 虚拟摄像机世界坐标
        private float _zoomLevel = 1.0f;                 // 缩放级别

        // 计算状态
        private float _currentPPU = 1.0f;                // 当前像素每单位比例
        private Vector2 _lastMousePosition;              // 用于拖拽计算

        // 输入状态
        private bool _isPanning = false;

        // 地图数据
        private BigMapSaveData _mapData;

        // 初始化状态
        private bool _isInitialized = false;

        /// <summary>
        /// 当前摄像机世界位置（只读）
        /// </summary>
        public Vector2 CameraWorldPos => _cameraWorldPos;

        /// <summary>
        /// 当前缩放级别（只读）
        /// </summary>
        public float ZoomLevel => _zoomLevel;

        /// <summary>
        /// 当前PPU（像素每单位，只读）
        /// </summary>
        public float CurrentPPU => _currentPPU;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                Debug.LogError("BigMapRuntimeRenderer: 需要UIDocument组件");
                return;
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
                        _mapContainer.RenderMap(_mapData, _currentPPU);
                    }
                }
            }

            // 应用当前变换
            UpdateContainerTransform();
        }

        private void Start()
        {
            // 应用初始变换
            UpdateContainerTransform();
        }

        private void LateUpdate()
        {
            // 只有在激活状态且已初始化时才更新
            if (!_isInitialized || !isActiveAndEnabled) return;

            // 动态计算PPU（适应屏幕分辨率变化）
            UpdatePPU();

            // 应用摄像机变换到容器
            UpdateContainerTransform();
        }

        /// <summary>
        /// 动态计算PPU（像素每单位）
        /// 公式：PPU = Screen.height / (OrthographicSize * 2)
        /// </summary>
        private void UpdatePPU()
        {
            float newPPU = Screen.height / (_orthographicSize * 2f);

            // 如果PPU发生变化，更新地图容器
            if (Mathf.Abs(_currentPPU - newPPU) > 0.001f)
            {
                _currentPPU = newPPU;
                _mapContainer?.UpdatePPU(_currentPPU);
            }
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

            // 创建Viewport（全屏，用于拦截输入事件）
            _viewport = new VisualElement();
            _viewport.name = "BigMapViewport";
            _viewport.style.width = Length.Percent(100);
            _viewport.style.height = Length.Percent(100);
            _viewport.style.position = Position.Relative;
            _viewport.style.overflow = Overflow.Hidden;
            _viewport.style.backgroundColor = Color.clear;

            // 注册输入事件
            _viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _viewport.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);

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

                // 渲染地图（传递当前PPU）
                if (_mapContainer != null)
                {
                    _mapContainer.RenderMap(_mapData, _currentPPU);
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
        private void OnPointerDown(PointerDownEvent evt)
        {
            // 只响应中键（按钮2）和右键（按钮1）
            if (evt.button == 1 || evt.button == 2) // 1=右键，2=中键
            {
                _isPanning = true;
                _lastMousePosition = (Vector2)evt.position;
                _viewport.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// 指针移动事件（拖拽中）
        /// </summary>
        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isPanning) return;

            Vector2 delta = (Vector2)evt.position - _lastMousePosition;
            _lastMousePosition = (Vector2)evt.position;

            // 拖拽逻辑：鼠标向右拖，摄像机向左移
            // deltaX / PPU / Zoom
            Vector2 worldDelta = new Vector2(
                delta.x / (_currentPPU * _zoomLevel),
                -delta.y / (_currentPPU * _zoomLevel)  // Y轴方向反转
            );

            _cameraWorldPos -= worldDelta * _panSpeed;

            evt.StopPropagation();
        }

        /// <summary>
        /// 指针抬起事件（结束拖拽）
        /// </summary>
        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_isPanning && (evt.button == 1 || evt.button == 2))
            {
                _isPanning = false;
                _viewport.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// 滚轮事件（缩放）
        /// </summary>
        private void OnWheel(WheelEvent evt)
        {
            // 计算缩放增量（滚轮向下为负，缩小；向上为正，放大）
            float zoomDelta = -evt.delta.y * _zoomSpeed;
            float newZoom = Mathf.Clamp(_zoomLevel * (1 + zoomDelta), _minZoom, _maxZoom);

            // 计算以鼠标位置为中心的缩放
            Vector2 mouseScreenPos = (Vector2)evt.localMousePosition;
            Vector2 viewportCenter = new Vector2(_viewport.layout.width / 2, _viewport.layout.height / 2);
            Vector2 mouseOffset = mouseScreenPos - viewportCenter;

            // 调整摄像机位置以保持鼠标指向的世界位置不变
            float zoomRatio = newZoom / _zoomLevel;
            Vector2 mouseWorldPosBefore = ScreenToWorld(mouseScreenPos);
            _zoomLevel = newZoom;
            Vector2 mouseWorldPosAfter = ScreenToWorld(mouseScreenPos);
            _cameraWorldPos += mouseWorldPosAfter - mouseWorldPosBefore;

            evt.StopPropagation();
        }

        /// <summary>
        /// 更新地图容器的变换（基于摄像机状态）
        /// 公式：Translate_X = (Screen.width / 2) - (CameraPos.x * PPU * Zoom)
        ///        Translate_Y = (Screen.height / 2) + (CameraPos.y * PPU * Zoom)
        /// </summary>
        private void UpdateContainerTransform()
        {
            if (_mapContainer == null) return;

            // 计算平移量
            float translateX = (Screen.width / 2f) - (_cameraWorldPos.x * _currentPPU * _zoomLevel);
            float translateY = (Screen.height / 2f) + (_cameraWorldPos.y * _currentPPU * _zoomLevel);

            // 设置平移和缩放
            _mapContainer.style.translate = new Translate(translateX, translateY);
            _mapContainer.style.scale = new Scale(new Vector3(_zoomLevel, _zoomLevel, 1));

            // 强制重绘（触发连线绘制）
            _mapContainer.MarkDirtyRepaint();
        }

        /// <summary>
        /// 屏幕坐标 → 世界坐标转换
        /// </summary>
        private Vector2 ScreenToWorld(Vector2 screenPos)
        {
            // 屏幕中心偏移
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 offsetFromCenter = screenPos - screenCenter;

            // 逆变换：去除缩放和平移
            return _cameraWorldPos + new Vector2(
                offsetFromCenter.x / (_currentPPU * _zoomLevel),
                -offsetFromCenter.y / (_currentPPU * _zoomLevel)  // Y轴方向反转
            );
        }

        /// <summary>
        /// 世界坐标 → 屏幕坐标转换
        /// </summary>
        private Vector2 WorldToScreen(Vector2 worldPos)
        {
            // 相对于摄像机的位置
            Vector2 relativeToCamera = worldPos - _cameraWorldPos;

            // 应用缩放和PPU，并转换到屏幕空间
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            return screenCenter + new Vector2(
                relativeToCamera.x * _currentPPU * _zoomLevel,
                -relativeToCamera.y * _currentPPU * _zoomLevel  // Y轴方向反转
            );
        }

        /// <summary>
        /// 重置视图到初始状态
        /// </summary>
        public void ResetView()
        {
            _cameraWorldPos = Vector2.zero;
            _zoomLevel = 1.0f;
            UpdateContainerTransform();
        }

        /// <summary>
        /// 设置地图数据（运行时切换地图）
        /// </summary>
        public void SetMapData(BigMapSaveData data)
        {
            _mapData = data;
            if (_mapContainer != null)
            {
                _mapContainer.RenderMap(_mapData, _currentPPU);
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