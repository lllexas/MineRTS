using UnityEngine;

/// <summary>
/// 2D 正交相机控制器 (CameraController)
/// 仿 SC1 操作逻辑：支持鼠标推屏、方向键推屏、中键抓屏、滚轮缩放
/// </summary>
public class CameraController : SingletonMono<CameraController>
{
    [Header("移动参数")]
    public float moveSpeed = 25f;           // 推屏和按键的移动速度
    [Tooltip("中键抓屏的移动比例（1=1:1 跟随，>1 更快，<1 更慢）")]
    public float dragSpeed = 1.0f;          // 中键抓屏的灵敏度
    public float edgeSize = 10f;            // 判定推屏的边缘宽度（像素）
    public bool useEdgeScrolling = true;    // 是否开启推屏

    [Header("缩放参数")]
    public float defaultZoom = 8f;          // 默认正交尺寸
    public float minZoom = 2f;              // 最小缩放
    public float maxZoom = 18f;             // 最大缩放
    public float zoomSensitivity = 10f;     // 缩放灵敏度

    [Header("平滑与边界")]
    public float lerpSpeed = 100f;           // 平滑跟随速度

    [Header("是否暂停")]
    public bool isPaused = false;           // 暂停状态，暂停时不处理输入

    [Header("操作模式")]
    // 直接使用 GameFlowController.GameState，不重复定义枚举
    private GameFlowController.GameState _currentGameState = GameFlowController.GameState.InStage;

    //------------------ 修改 ------------------------
    // 不再手动填写 mapBounds，改为由 SyncBounds 从 WholeComponent 计算得出
    private Rect _currentMovementBounds;    // 当前缩放级别下，相机中心允许活动的范围
    private Rect _worldRect;               // 地图实际的物理矩形
    //-----------------------------------------------

    private Camera _cam;
    private Vector3 _targetPos;
    private float _targetZoom;
    private Vector3 _lastMouseScreenPos;   // 用于中键拖拽时记录上一帧鼠标屏幕位置
    private bool _isInitialized = false;

    /// <summary>
    /// 获取相机控制器是否已初始化
    /// </summary>
    public bool IsInitialized => _isInitialized;

    protected override void Awake()
    {
        base.Awake();
        _cam = GetComponent<Camera>();

        // 检查相机组件
        if (_cam == null)
        {
            Debug.LogError("<color=red>[CameraController]</color> 需要Camera组件！控制器将被禁用");
            enabled = false;
            return;
        }

        // 初始化目标值为当前状态喵
        _targetPos = transform.position;
        _targetZoom = _cam.orthographicSize = defaultZoom;

        Debug.Log("<color=cyan>[CameraController]</color> Awake完成，相机组件已获取");
    }

    private void Start()
    {
        // 检查相机组件
        if (_cam == null)
        {
            Debug.LogError("<color=red>[CameraController]</color> 相机组件未找到，控制器将被禁用");
            enabled = false;
            return;
        }

        // 自动初始化：使用BigMap边界，确保相机立即可用
        SyncBigMap();
        _isInitialized = true;

        Debug.Log("<color=cyan>[CameraController]</color> 已通过Start自动初始化，使用BigMap边界");
    }

    private void Update()
    {
        HandlePause();
        if (isPaused) return; // 如果游戏暂停了，就不处理输入喵
        HandleInput();
        ApplyTransform();
    }

    private void HandlePause()
    {
        // 监听暂停键（F10键）切换暂停状态
        if (Input.GetKeyDown(KeyCode.F10))
        {
            isPaused = !isPaused;
            Debug.Log($"<color=cyan>[CameraController]</color> 暂停状态切换: {(isPaused ? "已暂停" : "已恢复")}");
        }
    }

    private void HandleInput()
    {
        // MainMenu 模式下完全禁用所有输入
        if (_currentGameState == GameFlowController.GameState.MainMenu) return;

        Vector3 moveInput = Vector3.zero;

        // --- 1. 小箭头/方向键推屏 (Arrow Keys) ---
        // 改为只使用方向键，避免与A键冲突
        if (Input.GetKey(KeyCode.UpArrow)) moveInput.y += 1;
        if (Input.GetKey(KeyCode.DownArrow)) moveInput.y -= 1;
        if (Input.GetKey(KeyCode.LeftArrow)) moveInput.x -= 1;
        if (Input.GetKey(KeyCode.RightArrow)) moveInput.x += 1;

        // --- 2. 鼠标推屏 (Edge Scrolling) ---
        // BigMap 模式下，如果正在使用左键或中键拖拽，则禁用推屏防止冲突
        bool isDragging = (_currentGameState == GameFlowController.GameState.BigMap && (Input.GetMouseButton(0) || Input.GetMouseButton(2)))
                        || Input.GetMouseButton(2); // InStage 模式下只检查中键
        if (useEdgeScrolling && !isDragging)
        {
            Vector3 mousePos = Input.mousePosition;
            if (mousePos.x <= edgeSize) moveInput.x = -1;
            else if (mousePos.x >= Screen.width - edgeSize) moveInput.x = 1;

            if (mousePos.y <= edgeSize) moveInput.y = -1;
            else if (mousePos.y >= Screen.height - edgeSize) moveInput.y = 1;
        }

        // 处理移动累加
        if (moveInput != Vector3.zero)
        {
            // 基于缩放程度调整移动速度：镜头拉远时动快点，拉近时动慢点
            float speedMultiplier = _cam.orthographicSize / defaultZoom;
            _targetPos += moveInput.normalized * moveSpeed * speedMultiplier * Time.unscaledDeltaTime;
        }

        // --- 3. 左键抓屏 (BigMap 模式专属) ---
        if (_currentGameState == GameFlowController.GameState.BigMap)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _lastMouseScreenPos = Input.mousePosition;
            }

            if (Input.GetMouseButton(0))
            {
                Vector3 currentMousePos = Input.mousePosition;
                Vector3 screenDelta = currentMousePos - _lastMouseScreenPos;

                // 计算当前缩放级别下，1 像素对应多少世界单位
                float unitsPerPixel = (_cam.orthographicSize * 2f) / Screen.height;

                // 计算世界位移（鼠标往右移，相机往左走，所以用负号）
                Vector3 worldMove = new Vector3(
                    screenDelta.x * unitsPerPixel,
                    screenDelta.y * unitsPerPixel,
                    0
                );

                _targetPos -= worldMove;
                _lastMouseScreenPos = currentMousePos;
            }
        }

        // --- 4. 中键抓屏 (Middle Mouse Drag) ---
        if (Input.GetMouseButtonDown(2))
        {
            // 记录按下瞬间的屏幕位置
            _lastMouseScreenPos = Input.mousePosition;
        }

        if (Input.GetMouseButton(2))
        {
            Vector3 currentMousePos = Input.mousePosition;
            Vector3 screenDelta = currentMousePos - _lastMouseScreenPos;

            // 计算当前缩放级别下，1 像素对应多少世界单位
            // 正交相机下：屏幕高度 = orthoSize * 2
            float unitsPerPixel = (_cam.orthographicSize * 2f) / Screen.height;

            // 计算世界位移（鼠标往右移，相机往左走，所以用负号）
            Vector3 worldMove = new Vector3(
                screenDelta.x * unitsPerPixel,
                screenDelta.y * unitsPerPixel,
                0
            );

            // 直接修改目标位置，完全无视当前相机在哪，只针对目标点操作
            _targetPos -= worldMove;

            // 这一步至关重要：更新上一帧位置，确保位移是增量的
            _lastMouseScreenPos = currentMousePos;
        }

        // --- 5. 滚轮缩放 (Zoom) ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            _targetZoom -= scroll * zoomSensitivity;
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
            UpdateMovementLimits();
        }

        // --- 6. 限制边界 ---
        if (_isInitialized)
        {
            _targetPos.x = Mathf.Clamp(_targetPos.x, _currentMovementBounds.xMin, _currentMovementBounds.xMax);
            _targetPos.y = Mathf.Clamp(_targetPos.y, _currentMovementBounds.yMin, _currentMovementBounds.yMax);
        }
    }

    private void ApplyTransform()
    {
        // 平滑插值，让移动和缩放看起来像有惯性一样舒服
        transform.position = Vector3.Lerp(transform.position, _targetPos, Time.unscaledDeltaTime * lerpSpeed);
        _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetZoom, Time.unscaledDeltaTime * lerpSpeed);
    }

    // ==========================================
    // 公共方法 (Public API) 喵！
    // ==========================================

    /// <summary>
    /// 从 EntitySystem 同步地图数据并刷新边界限制
    /// </summary>
    public void SyncBounds()
    {
        try
        {
            // 检查EntitySystem是否可用
            if (EntitySystem.Instance == null)
            {
                Debug.LogWarning("<color=orange>[CameraController]</color> EntitySystem未初始化，使用BigMap边界作为回退");
                SyncBigMap();
                return;
            }

            var whole = EntitySystem.Instance.wholeComponent;

            // 1. 根据 WholeComponent 的数据构建地图矩形
            // 假设 minX, minY 是左下角起点，加上宽高
            _worldRect = new Rect(whole.minX, whole.minY, whole.mapWidth, whole.mapHeight);

            // 2. 刷新当前的中心点限制
            UpdateMovementLimits();
            _isInitialized = true;

            Debug.Log($"<color=cyan>[CameraController]</color> 已同步EntitySystem边界: {_worldRect}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"<color=red>[CameraController]</color> 同步EntitySystem边界失败: {ex.Message}");
            // 回退到安全边界
            SyncBigMap();
        }
    }

    /// <summary>
    /// 同步BigMap边界（临时方法）- 设置固定边界 (±100)
    /// </summary>
    public void SyncBigMap()
    {
        // 设置固定边界：从(-100, -100)到(100, 100)，总大小200x200
        _worldRect = new Rect(-100f, -100f, 200f, 200f);

        // 刷新当前的中心点限制
        UpdateMovementLimits();
        _isInitialized = true;

        Debug.Log($"<color=cyan>[CameraController]</color> 已同步BigMap边界: {_worldRect}");
    }

    /// <summary>
    /// 同步主菜单边界（占位符方法）
    /// </summary>
    public void SyncMainMenu()
    {
        // TODO: 根据主菜单布局设置合适的边界
        // 暂时使用与BigMap相同的边界
        _worldRect = new Rect(-50f, -50f, 100f, 100f);

        UpdateMovementLimits();
        _isInitialized = true;

        Debug.Log($"<color=cyan>[CameraController]</color> 已同步主菜单边界: {_worldRect}");
    }

    /// <summary>
    /// 设置自定义世界边界
    /// </summary>
    /// <param name="center">边界中心点</param>
    /// <param name="width">边界宽度</param>
    /// <param name="height">边界高度</param>
    public void SetCustomBounds(Vector2 center, float width, float height)
    {
        _worldRect = new Rect(center.x - width/2f, center.y - height/2f, width, height);
        UpdateMovementLimits();
        _isInitialized = true;

        Debug.Log($"<color=cyan>[CameraController]</color> 已设置自定义边界: {_worldRect}");
    }

    /// <summary>
    /// 核心逻辑：计算相机在当前缩放级别下，不露出地图外边缘的活动矩形
    /// </summary>
    private void UpdateMovementLimits()
    {
        // 检查是否已初始化
        if (!_isInitialized) return;

        // 防止除零错误
        if (Screen.height <= 0)
        {
            Debug.LogWarning("<color=orange>[CameraController]</color> Screen.height为0或负数，使用默认宽高比16:9");
            // 使用默认宽高比16:9
            float camHalfHeight2 = _targetZoom;
            float camHalfWidth2 = camHalfHeight2 * (16f / 9f);

            // 计算边界（使用当前_worldRect）
            float minX2 = _worldRect.xMin + camHalfWidth2;
            float maxX2 = _worldRect.xMax - camHalfWidth2;
            float minY2 = _worldRect.yMin + camHalfHeight2;
            float maxY2 = _worldRect.yMax - camHalfHeight2;

            if (minX2 > maxX2) minX2 = maxX2 = _worldRect.center.x;
            if (minY2 > maxY2) minY2 = maxY2 = _worldRect.center.y;

            _currentMovementBounds = Rect.MinMaxRect(minX2, minY2, maxX2, maxY2);
            return;
        }

        // 计算相机视口的一半高度 (orthographicSize)
        float camHalfHeight = _targetZoom;
        // 计算相机视口的一半宽度 (由宽高比决定)
        float camHalfWidth = camHalfHeight * ((float)Screen.width / Screen.height);

        // 计算中心点允许移动的 X 轴和 Y 轴范围
        float minX = _worldRect.xMin + camHalfWidth;
        float maxX = _worldRect.xMax - camHalfWidth;
        float minY = _worldRect.yMin + camHalfHeight;
        float maxY = _worldRect.yMax - camHalfHeight;

        // 如果地图比相机的视野还小，就强制锁定在地图中心
        if (minX > maxX) minX = maxX = _worldRect.center.x;
        if (minY > maxY) minY = maxY = _worldRect.center.y;

        _currentMovementBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// 回到世界零点
    /// </summary>
    public void GoToOrigin()
    {
        _targetPos = new Vector3(0, 0, transform.position.z);
    }

    /// <summary>
    /// 回到默认尺寸
    /// </summary>
    public void ResetZoom()
    {
        _targetZoom = defaultZoom;
        UpdateMovementLimits();
    }

    /// <summary>
    /// 强制聚焦到某个世界坐标点
    /// </summary>
    public void FocusOn(Vector2 worldPos)
    {
        _targetPos = new Vector3(worldPos.x, worldPos.y, transform.position.z);
    }

    /// <summary>
    /// 自动初始化摄像机：同步边界、重置缩放、回到地图中心
    /// 替代手动控制台命令：cam_sync; cam_reset; cam_home
    /// </summary>
    public void InitializeCamera()
    {
        Debug.Log("<color=cyan>[CameraController]</color> 自动初始化摄像机...");

        // 1. 同步地图边界
        SyncBounds();

        // 2. 重置缩放级别
        ResetZoom();

        // 3. 回到世界中心
        GoToOrigin();

        Debug.Log("<color=cyan>[CameraController]</color> 摄像机自动化初始化完成");
    }

    /// <summary>
    /// 根据游戏状态设置相机操作模式
    /// </summary>
    public void SetGameMode(GameFlowController.GameState gameState)
    {
        _currentGameState = gameState;
        Debug.Log($"<color=cyan>[CameraController]</color> 操作模式已切换：{_currentGameState}");
    }
}