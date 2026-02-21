using UnityEngine;

/// <summary>
/// 2D 正交相机控制器 (CameraController)
/// 仿 SC1 操作逻辑：支持鼠标推屏、方向键推屏、中键抓屏、滚轮缩放
/// </summary>
public class CameraController : SingletonMono<CameraController>
{
    [Header("移动参数")]
    public float moveSpeed = 25f;           // 推屏和按键的移动速度
    public float dragSpeed = 1.5f;          // 中键抓屏的灵敏度
    public float edgeSize = 10f;            // 判定推屏的边缘宽度（像素）
    public bool useEdgeScrolling = true;    // 是否开启推屏

    [Header("缩放参数")]
    public float defaultZoom = 8f;          // 默认正交尺寸
    public float minZoom = 2f;              // 最小缩放
    public float maxZoom = 18f;             // 最大缩放
    public float zoomSensitivity = 10f;     // 缩放灵敏度

    [Header("平滑与边界")]
    public float lerpSpeed = 100f;           // 平滑跟随速度
    //------------------ 修改 ------------------------
    // 不再手动填写 mapBounds，改为由 SyncBounds 从 WholeComponent 计算得出
    private Rect _currentMovementBounds;    // 当前缩放级别下，相机中心允许活动的范围
    private Rect _worldRect;               // 地图实际的物理矩形
    //-----------------------------------------------

    private Camera _cam;
    private Vector3 _targetPos;
    private float _targetZoom;

    protected override void Awake()
    {
        base.Awake();
        _cam = GetComponent<Camera>();

        // 初始化目标值为当前状态喵
        _targetPos = transform.position;
        _targetZoom = _cam.orthographicSize = defaultZoom;
    }

    private void Update()
    {
        HandleInput();
        ApplyTransform();
    }

    private void HandleInput()
    {
        Vector3 moveInput = Vector3.zero;

        // --- 1. 小箭头/方向键推屏 (Arrow Keys) ---
        // 改为只使用方向键，避免与A键冲突
        if (Input.GetKey(KeyCode.UpArrow)) moveInput.y += 1;
        if (Input.GetKey(KeyCode.DownArrow)) moveInput.y -= 1;
        if (Input.GetKey(KeyCode.LeftArrow)) moveInput.x -= 1;
        if (Input.GetKey(KeyCode.RightArrow)) moveInput.x += 1;

        // --- 2. 鼠标推屏 (Edge Scrolling) ---
        if (useEdgeScrolling && !Input.GetMouseButton(2)) // 抓屏时禁用推屏，防止冲突
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

        // --- 3. 中键抓屏 (Middle Mouse Drag) ---
        if (Input.GetMouseButton(2))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            // 抓屏是反向移动：鼠标往左拉，相机往右走喵
            _targetPos -= new Vector3(mouseX, mouseY, 0) * dragSpeed * (_cam.orthographicSize * 0.2f);
        }

        // --- 4. 滚轮缩放 (Zoom) ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            _targetZoom -= scroll * zoomSensitivity;
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
            UpdateMovementLimits();
        }

        // --- 5. 限制边界 ---
        _targetPos.x = Mathf.Clamp(_targetPos.x, _currentMovementBounds.xMin, _currentMovementBounds.xMax);
        _targetPos.y = Mathf.Clamp(_targetPos.y, _currentMovementBounds.yMin, _currentMovementBounds.yMax);
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
        var whole = EntitySystem.Instance.wholeComponent;

        // 1. 根据 WholeComponent 的数据构建地图矩形
        // 假设 minX, minY 是左下角起点，加上宽高
        _worldRect = new Rect(whole.minX, whole.minY, whole.mapWidth, whole.mapHeight);

        // 2. 刷新当前的中心点限制
        UpdateMovementLimits();
    }

    /// <summary>
    /// 核心逻辑：计算相机在当前缩放级别下，不露出地图外边缘的活动矩形
    /// </summary>
    private void UpdateMovementLimits()
    {
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
}