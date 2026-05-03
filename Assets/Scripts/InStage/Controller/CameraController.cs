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
    public float edgeSafetyMargin = 0.5f;    // 视锥贴边时向内收缩的安全余量

    [Header("是否暂停")]
    public bool isPaused = false;           // 暂停状态，暂停时不处理输入

    [Header("操作模式")]
    // 直接使用 GameFlowController.GameState，不重复定义枚举
    private GameFlowController.GameState _currentGameState = GameFlowController.GameState.InStage;
    [SerializeField] private CameraControlMode _controlMode = CameraControlMode.Flat2D;

    //------------------ 修改 ------------------------
    // 不再手动填写 mapBounds，改为由 SyncBounds 从 WholeComponent 计算得出
    private Rect _currentMovementBounds;    // 当前缩放级别下，相机中心允许活动的范围
    private Rect _worldRect;               // 地图实际的物理矩形
    //-----------------------------------------------

    private Camera _cam;
    private Vector3 _targetPos;
    private float _targetZoom;
    private float _targetDistance;
    private float _perspectiveDepthSign = -1f;
    private Vector3 _lastMouseScreenPos;   // 用于中键拖拽时记录上一帧鼠标屏幕位置
    private bool _isInitialized = false;

    /// <summary>
    /// 获取相机控制器是否已初始化
    /// </summary>
    public bool IsInitialized => _isInitialized;
    public CameraControlMode ControlMode => _controlMode;
    public Camera TargetCamera
    {
        get
        {
            EnsureCameraReference();
            return _cam;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        EnsureCameraReference();

        // 检查相机组件
        if (_cam == null)
        {
            Debug.LogError("<color=red>[CameraController]</color> 需要Camera组件！控制器将被禁用");
            enabled = false;
            return;
        }

        // 初始化目标值为当前状态喵
        _targetPos = transform.position;
        _targetZoom = _cam.orthographic ? _cam.orthographicSize : _cam.fieldOfView;
        _targetDistance = Mathf.Abs(transform.position.z);
        _perspectiveDepthSign = ResolveDepthSign(transform.position.z);

        Debug.Log("<color=cyan>[CameraController]</color> Awake完成，相机组件已获取");
    }

    private void Start()
    {
        // 检查相机组件
        EnsureCameraReference();
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
            float speedMultiplier = GetZoomSpeedMultiplier();
            RouteMovementInput(moveInput.normalized, speedMultiplier);
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
                ApplyDragDelta(screenDelta);
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
            ApplyDragDelta(screenDelta);

            // 这一步至关重要：更新上一帧位置，确保位移是增量的
            _lastMouseScreenPos = currentMousePos;
        }

        // --- 5. 滚轮缩放 (Zoom) ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            if (_cam.orthographic)
            {
                _targetZoom -= scroll * zoomSensitivity;
                _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
            }
            else
            {
                _targetDistance -= scroll * zoomSensitivity;
                _targetDistance = Mathf.Clamp(_targetDistance, minZoom, maxZoom);
                _targetPos.z = _perspectiveDepthSign * _targetDistance;
            }
            UpdateMovementLimits();
        }

        // --- 6. 限制边界 ---
        if (_isInitialized)
        {
            _targetPos = ClampTargetPosition(_targetPos);
        }
    }

    private void ApplyTransform()
    {
        // 平滑插值，让移动和缩放看起来像有惯性一样舒服
        transform.position = Vector3.Lerp(transform.position, _targetPos, Time.unscaledDeltaTime * lerpSpeed);

        if (_cam.orthographic)
        {
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetZoom, Time.unscaledDeltaTime * lerpSpeed);
        }
        else
        {
            Vector3 currentPos = transform.position;
            currentPos.z = Mathf.Lerp(currentPos.z, _perspectiveDepthSign * _targetDistance, Time.unscaledDeltaTime * lerpSpeed);
            transform.position = currentPos;
        }
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
        EnsureCameraReference();
        if (_cam == null) return;
        Rect safeWorldRect = GetSafeWorldRect();

        if (!_cam.orthographic)
        {
            if (!TryBuildPerspectiveMovementBounds(safeWorldRect, out _currentMovementBounds))
            {
                _currentMovementBounds = safeWorldRect;
            }
            return;
        }

        // 防止除零错误
        if (Screen.height <= 0)
        {
            Debug.LogWarning("<color=orange>[CameraController]</color> Screen.height为0或负数，使用默认宽高比16:9");
            // 使用默认宽高比16:9
            float camHalfHeight2 = _targetZoom;
            float camHalfWidth2 = camHalfHeight2 * (16f / 9f);

            // 计算边界（使用当前_worldRect）
            float minX2 = safeWorldRect.xMin + camHalfWidth2;
            float maxX2 = safeWorldRect.xMax - camHalfWidth2;
            float minY2 = safeWorldRect.yMin + camHalfHeight2;
            float maxY2 = safeWorldRect.yMax - camHalfHeight2;

            if (minX2 > maxX2) minX2 = maxX2 = safeWorldRect.center.x;
            if (minY2 > maxY2) minY2 = maxY2 = safeWorldRect.center.y;

            _currentMovementBounds = Rect.MinMaxRect(minX2, minY2, maxX2, maxY2);
            return;
        }

        // 计算相机视口的一半高度 (orthographicSize)
        float camHalfHeight = _targetZoom;
        // 计算相机视口的一半宽度 (由宽高比决定)
        float camHalfWidth = camHalfHeight * ((float)Screen.width / Screen.height);

        // 计算中心点允许移动的 X 轴和 Y 轴范围
        float minX = safeWorldRect.xMin + camHalfWidth;
        float maxX = safeWorldRect.xMax - camHalfWidth;
        float minY = safeWorldRect.yMin + camHalfHeight;
        float maxY = safeWorldRect.yMax - camHalfHeight;

        // 如果地图比相机的视野还小，就强制锁定在地图中心
        if (minX > maxX) minX = maxX = safeWorldRect.center.x;
        if (minY > maxY) minY = maxY = safeWorldRect.center.y;

        _currentMovementBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// 根据当前游戏状态同步相机上下文。
    /// 外部只表达“切到了什么状态”，具体边界和输入模式路由都收在这里。
    /// </summary>
    public void ConfigureForState(GameFlowController.GameState gameState)
    {
        _currentGameState = gameState;

        switch (gameState)
        {
            case GameFlowController.GameState.MainMenu:
                SyncMainMenu();
                break;

            case GameFlowController.GameState.BigMap:
                SyncBigMap();
                break;

            case GameFlowController.GameState.InStage:
                SyncBounds();
                break;
        }

        Debug.Log($"<color=cyan>[CameraController]</color> 状态上下文已同步：{_currentGameState}");
    }

    /// <summary>
    /// 回到世界零点
    /// </summary>
    public void GoToOrigin()
    {
        _targetPos = new Vector3(0f, 0f, GetCurrentTargetDepth());
    }

    /// <summary>
    /// 回到默认尺寸
    /// </summary>
    public void ResetZoom()
    {
        if (_cam != null && !_cam.orthographic)
        {
            _targetDistance = defaultZoom;
            _targetPos.z = _perspectiveDepthSign * _targetDistance;
        }
        else
        {
            _targetZoom = defaultZoom;
        }
        UpdateMovementLimits();
    }

    /// <summary>
    /// 强制聚焦到某个世界坐标点
    /// </summary>
    public void FocusOn(Vector2 worldPos)
    {
        _targetPos = new Vector3(worldPos.x, worldPos.y, GetCurrentTargetDepth());
    }

    /// <summary>
    /// 自动初始化摄像机：同步边界、重置缩放、回到地图中心
    /// 替代手动控制台命令：cam_sync; cam_reset; cam_home
    /// </summary>
    public void InitializeCamera()
    {
        Debug.Log("<color=cyan>[CameraController]</color> 自动初始化摄像机...");

        // 1. 同步地图边界
        ConfigureForState(_currentGameState);

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
        ConfigureForState(gameState);
    }

    public void ApplyProfile(CameraProfileSO profile)
    {
        EnsureCameraReference();
        if (profile == null || _cam == null)
        {
            return;
        }

        moveSpeed = profile.MoveSpeed;
        dragSpeed = profile.DragSpeed;
        edgeSize = profile.EdgeSize;
        useEdgeScrolling = profile.UseEdgeScrolling;
        defaultZoom = profile.DefaultZoom;
        minZoom = profile.MinZoom;
        maxZoom = profile.MaxZoom;
        zoomSensitivity = profile.ZoomSensitivity;
        lerpSpeed = profile.LerpSpeed;
        _controlMode = profile.ControlMode;

        _cam.orthographic = profile.ProjectionMode == CameraProjectionMode.Orthographic;
        _cam.nearClipPlane = profile.NearClipPlane;
        _cam.farClipPlane = profile.FarClipPlane;
        _cam.orthographicSize = profile.OrthographicSize;
        _cam.fieldOfView = profile.FieldOfView;

        transform.position = profile.Position;
        transform.rotation = Quaternion.Euler(profile.RotationEuler);
        _targetPos = profile.Position;
        _targetZoom = _cam.orthographic ? profile.OrthographicSize : profile.FieldOfView;
        _targetDistance = Mathf.Abs(profile.Position.z);
        _perspectiveDepthSign = ResolveDepthSign(profile.Position.z);

        InStageRenderSpace.LayoutMode = _controlMode == CameraControlMode.Billboard3D
            ? InStageRenderLayoutMode.Billboard3D
            : InStageRenderLayoutMode.Flat2D;

        UpdateMovementLimits();
    }

    private void EnsureCameraReference()
    {
        if (_cam == null)
        {
            _cam = GetComponent<Camera>();
        }

        if (_cam == null)
        {
            _cam = GetComponentInParent<Camera>();
        }
    }

    private float GetZoomSpeedMultiplier()
    {
        if (Mathf.Abs(defaultZoom) <= 0.001f)
        {
            return 1f;
        }

        float currentZoom = _cam.orthographic ? _cam.orthographicSize : _targetDistance;
        return currentZoom / defaultZoom;
    }

    private void RouteMovementInput(Vector3 moveInput, float speedMultiplier)
    {
        if (_cam.orthographic)
        {
            Vector3 delta = moveInput * moveSpeed * speedMultiplier * Time.unscaledDeltaTime;
            AddMovement(delta);
            return;
        }

        Vector3 rightOnPlane = GetPlaneProjectedAxis(transform.right, Vector3.right);
        Vector3 upOnPlane = GetPlaneProjectedAxis(transform.up, Vector3.up);
        Vector3 planarDirection = (rightOnPlane * moveInput.x) + (upOnPlane * moveInput.y);
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 deltaPerspective = planarDirection.normalized * moveSpeed * speedMultiplier * Time.unscaledDeltaTime;
        AddMovement(new Vector3(deltaPerspective.x, deltaPerspective.y, 0f));
    }

    private void AddMovement(Vector3 delta)
    {
        _targetPos += delta;
    }

    private void ApplyDragDelta(Vector3 screenDelta)
    {
        if (_cam.orthographic)
        {
            float unitsPerPixel = (_cam.orthographicSize * 2f) / Mathf.Max(Screen.height, 1f);
            Vector2 planarDelta = new Vector2(screenDelta.x * unitsPerPixel, screenDelta.y * unitsPerPixel) * dragSpeed;
            _targetPos -= new Vector3(planarDelta.x, planarDelta.y, 0f);
            return;
        }

        float planeZ = 0f;
        Vector3 prevScreen = _lastMouseScreenPos;
        Vector3 currScreen = prevScreen + screenDelta;
        Vector3 prevWorld = ScreenToPlane(prevScreen, planeZ);
        Vector3 currWorld = ScreenToPlane(currScreen, planeZ);
        Vector3 worldDelta = (currWorld - prevWorld) * dragSpeed;
        _targetPos -= new Vector3(worldDelta.x, worldDelta.y, 0f);
    }

    private Vector3 ClampTargetPosition(Vector3 targetPos)
    {
        targetPos.x = Mathf.Clamp(targetPos.x, _currentMovementBounds.xMin, _currentMovementBounds.xMax);
        targetPos.y = Mathf.Clamp(targetPos.y, _currentMovementBounds.yMin, _currentMovementBounds.yMax);
        return targetPos;
    }

    private Vector3 ScreenToPlane(Vector3 screenPos, float planeZ)
    {
        Ray ray = _cam.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return transform.position;
    }

    private bool TryBuildPerspectiveMovementBounds(Rect safeWorldRect, out Rect bounds)
    {
        bounds = safeWorldRect;
        Vector3 targetCameraPos = GetPerspectiveTargetPosition();

        Vector2[] viewportCorners =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f)
        };

        bool hasOffset = false;
        float minOffsetX = 0f;
        float maxOffsetX = 0f;
        float minOffsetY = 0f;
        float maxOffsetY = 0f;

        for (int i = 0; i < viewportCorners.Length; i++)
        {
            Vector2 corner = viewportCorners[i];
            Ray viewportRay = _cam.ViewportPointToRay(corner);
            Vector3 worldDirection = viewportRay.direction;
            if (Mathf.Abs(worldDirection.z) <= 0.0001f)
            {
                return false;
            }

            float enter = -targetCameraPos.z / worldDirection.z;
            if (enter <= 0f)
            {
                return false;
            }

            Vector3 worldPoint = targetCameraPos + worldDirection * enter;
            Vector2 offset = new Vector2(worldPoint.x - targetCameraPos.x, worldPoint.y - targetCameraPos.y);
            if (!hasOffset)
            {
                minOffsetX = maxOffsetX = offset.x;
                minOffsetY = maxOffsetY = offset.y;
                hasOffset = true;
                continue;
            }

            minOffsetX = Mathf.Min(minOffsetX, offset.x);
            maxOffsetX = Mathf.Max(maxOffsetX, offset.x);
            minOffsetY = Mathf.Min(minOffsetY, offset.y);
            maxOffsetY = Mathf.Max(maxOffsetY, offset.y);
        }

        float minX = safeWorldRect.xMin - minOffsetX;
        float maxX = safeWorldRect.xMax - maxOffsetX;
        float minY = safeWorldRect.yMin - minOffsetY;
        float maxY = safeWorldRect.yMax - maxOffsetY;

        if (minX > maxX)
        {
            minX = maxX = safeWorldRect.center.x;
        }

        if (minY > maxY)
        {
            minY = maxY = safeWorldRect.center.y;
        }

        bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private Vector3 GetPlaneProjectedAxis(Vector3 sourceAxis, Vector3 fallbackAxis)
    {
        Vector3 projected = Vector3.ProjectOnPlane(sourceAxis, Vector3.forward);
        if (projected.sqrMagnitude <= 0.0001f)
        {
            return fallbackAxis;
        }

        return projected.normalized;
    }

    private float ResolveDepthSign(float zValue)
    {
        if (Mathf.Abs(zValue) <= 0.001f)
        {
            return -1f;
        }

        return Mathf.Sign(zValue);
    }

    private float GetCurrentTargetDepth()
    {
        return _cam != null && !_cam.orthographic
            ? _perspectiveDepthSign * _targetDistance
            : transform.position.z;
    }

    private Vector3 GetPerspectiveTargetPosition()
    {
        return new Vector3(_targetPos.x, _targetPos.y, _perspectiveDepthSign * _targetDistance);
    }

    private Rect GetSafeWorldRect()
    {
        float margin = Mathf.Max(0f, edgeSafetyMargin);
        if (margin <= 0.0001f)
        {
            return _worldRect;
        }

        float width = Mathf.Max(0f, _worldRect.width - margin * 2f);
        float height = Mathf.Max(0f, _worldRect.height - margin * 2f);

        return new Rect(
            _worldRect.xMin + margin,
            _worldRect.yMin + margin,
            width,
            height);
    }
}
