using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System;

public class BuildingController : SingletonMono<BuildingController>
{
    [Header("设置")]
    public Color canBuildColor = new Color(0, 1, 0, 0.5f);
    public Color cannotBuildColor = new Color(1, 0, 0, 0.5f);

    [Header("状态")]
    [SerializeField] private string _currentBlueprintKey = "";
    [SerializeField] private Vector2Int _currentRotation = Vector2Int.up;
    [Header("手感设置")]
    // 主人，这个开关设为 true，就是你想要的工业建造手感喵！
    public bool defaultContinuousBuild = true;

    [Header("拆除设置")]
    public Color demolishColor = new Color(1, 0, 0, 0.6f); // 拆除时的红光
    [SerializeField] private bool _isDemolishing = false; // 是否处于拆除模式

    // 框选拆除相关
    private bool _isDemolishDragging = false;
    private Vector2Int _demolishStartGrid;

    // --- 【长条建造模式变量】 ---
    private bool _isLineMode = false;         // 当前蓝图是否支持长条建造（传送带）
    private bool _isDragging = false;         // 是否已经点了第一下
    private Vector2Int _startGridPos;         // 起始点
    private List<Vector2Int> _previewPath = new List<Vector2Int>(); // 缓存当前路径

    // --- 【对象池：预览虚影】 ---
    private List<GameObject> _ghostPool = new List<GameObject>();
    private Transform _ghostRoot;

    //--- 轨迹锁定变量 ---
    private bool _lineXFirst = true; // 缓存当前的弯折方向
    private bool _firstMoveLocked = false; // 是否已经锁定了初始方向
    // 增加一个回调，用于部署成功后的逻辑处理
    private Action<Vector2Int, Vector2Int> _onExternalDeployConfirm;

    protected override void Awake()
    {
        base.Awake();
        _ghostRoot = new GameObject("--- BuildingGhosts ---").transform;
    }

    private void Update()
    {
        if (EntitySystem.Instance == null || !EntitySystem.Instance.IsInitialized) return;

        // 1. 全局模式切换与退出
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (_isDemolishing) CancelDemolishMode();
            else EnterDemolishMode();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelBuilding();
            CancelDemolishMode();
        }

        // 2. 根据当前模式分流逻辑
        if (_isDemolishing)
        {
            UpdateDemolishLogic();
        }
        else if (!string.IsNullOrEmpty(_currentBlueprintKey))
        {
            // 旋转快捷键（仅在建造模式）
            if (Input.GetKeyDown(KeyCode.R)) RotateGhost();

            // 右键取消建造
            if (Input.GetMouseButtonDown(1))
            {
                CancelBuilding();
                return;
            }

            UpdatePlacementLogic();

            if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                HandleLeftClick();
            }
        }
    }


    public void SetCurrentBlueprint(string bpKey)
    {
        CancelDemolishMode(); // 选定蓝图时，自动退出拆除模式喵！

        _currentBlueprintKey = bpKey;
        var bp = BlueprintRegistry.Get(bpKey);

        _isLineMode = (bp.WorkType == WorkType.Conveyor);
        _isDragging = false;

        // --- 【优化联动 A】：根据需求决定是否开启电网视图 ---
        if (OverlayPowerSystem.Instance != null)
        {
            // 如果是电力节点，或者这建筑本身需要电，就打开视图喵！
            bool needPowerView = bp.IsPowerNode || bp.RequiresPower;
            OverlayPowerSystem.Instance.SetOverlayActive(needPowerView);

            // 如果是耗电建筑但不是节点，我们可以让视图变得稍淡一点（可选表现逻辑）
            // OverlayPowerSystem.Instance.SetDimMode(!bp.IsPowerNode); 
        }

        PrepareGhost();
    }

    public void CancelBuilding()
    {
        // --- 联动 B：退出建造时，强制关闭电网视图喵！ ---
        if (OverlayPowerSystem.Instance != null)
        {
            OverlayPowerSystem.Instance.UpdatePreview(null, null); // 清空预览
            OverlayPowerSystem.Instance.SetOverlayActive(false);
        }
        _onExternalDeployConfirm = null; // 清理回调

        _currentBlueprintKey = "";
        _isDragging = false;
        HideAllGhosts();
    }

    private void UpdatePlacementLogic()
    {
        var bp = BlueprintRegistry.Get(_currentBlueprintKey);
        Vector2Int currentMouseGrid = GridSystem.GetMouseGridPos(bp.LogicSize);
        _previewPath.Clear();

        if (_isLineMode && _isDragging)
        {
            // --- DSP 动态轨迹切换逻辑 ---
            int dx = Math.Abs(currentMouseGrid.x - _startGridPos.x);
            int dy = Math.Abs(currentMouseGrid.y - _startGridPos.y);

            // 1. 如果是刚开始拖（距离都很近），还没锁定
            if (!_firstMoveLocked)
            {
                if (dx >= 2) { _lineXFirst = true; _firstMoveLocked = true; }
                else if (dy >= 2) { _lineXFirst = false; _firstMoveLocked = true; }
                else { _lineXFirst = (dx >= dy); } // 默认逻辑
            }
            else
            {
                // 2. 智能翻转：如果另一条边的距离远超当前锁定的边（比如 2 倍以上），自动翻转
                if (_lineXFirst && dy > dx * 2 && dy >= 3) _lineXFirst = false;
                else if (!_lineXFirst && dx > dy * 2 && dx >= 3) _lineXFirst = true;
            }

            _previewPath = CalculateLPath(_startGridPos, currentMouseGrid, _lineXFirst);
        }
        else
        {
            // --- 模式 B: 单体建造预览 ---
            _previewPath.Add(currentMouseGrid);
            _firstMoveLocked = false; // 不在拖拽时，重置锁定状态
        }

        // --- 【新增联动】电力预览 ---
        if (bp.IsPowerNode && OverlayPowerSystem.Instance != null)
        {
            // 获取虚影的世界坐标（考虑了逻辑尺寸偏移）
            Vector3 ghostWorldPos = GridSystem.Instance.GridToWorld(currentMouseGrid, bp.LogicSize);
            OverlayPowerSystem.Instance.UpdatePreview(ghostWorldPos, bp);
        }
        else if (OverlayPowerSystem.Instance != null)
        {
            // 如果切到了非电力建筑，清空预览
            OverlayPowerSystem.Instance.UpdatePreview(null, null);
        }

        // 根据路径更新虚影显示
        RenderGhosts(_previewPath, bp);
    }

    private void HandleLeftClick()
    {
        var bp = BlueprintRegistry.Get(_currentBlueprintKey);
        Vector2Int gridPos = GridSystem.GetMouseGridPos(bp.LogicSize);

        // --- 如果当前是“战略部署”模式 ---
        if (_onExternalDeployConfirm != null)
        {
            // 只有位置合法时才允许触发回调
            if (GridSystem.Instance.IsAreaClear(gridPos, bp.LogicSize))
            {
                _onExternalDeployConfirm.Invoke(gridPos, _currentRotation);
                // 部署完通常就关闭预览了喵
                CancelBuilding();
                _onExternalDeployConfirm = null;
            }
            return;
        }

        if (_isLineMode)
        {
            if (!_isDragging)
            {
                _startGridPos = gridPos;
                _isDragging = true;
            }
            else
            {
                ExecuteBatchBuild();
            }
        }
        else
        {
            TryPlaceSingle(gridPos);
        }
    }

    // =========================================================
    // 核心算法：L型转角规则
    // =========================================================
    private List<Vector2Int> CalculateLPath(Vector2Int start, Vector2Int end, bool moveXFirst)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        int dx = end.x - start.x;
        int dy = end.y - start.y;

        if (moveXFirst)
        {
            // 1. 先横再纵
            int stepX = Math.Sign(dx);
            for (int x = 0; x != dx + stepX; x += (dx == 0 ? 1 : stepX))
                path.Add(new Vector2Int(start.x + x, start.y));

            int stepY = Math.Sign(dy);
            if (dy != 0) // 只有有纵向位移才补垂直段
                for (int y = stepY; y != dy + stepY; y += stepY)
                    path.Add(new Vector2Int(end.x, start.y + y));
        }
        else
        {
            // 2. 先纵再横
            int stepY = Math.Sign(dy);
            for (int y = 0; y != dy + stepY; y += (dy == 0 ? 1 : stepY))
                path.Add(new Vector2Int(start.x, start.y + y));

            int stepX = Math.Sign(dx);
            if (dx != 0)
                for (int x = stepX; x != dx + stepX; x += stepX)
                    path.Add(new Vector2Int(start.x + x, end.y));
        }
        return path;
    }

    private void ExecuteBatchBuild()
    {
        var bp = BlueprintRegistry.Get(_currentBlueprintKey);
        bool anyChange = false;
        Vector2Int lastPointOfPath = _previewPath[_previewPath.Count - 1]; // 记录下这一段路的终点，为了实现“全自动接力”喵！

        for (int i = 0; i < _previewPath.Count; i++)
        {
            Vector2Int pos = _previewPath[i];

            // 1. 计算这一格的“意图旋转”
            Vector2Int intentRot = _currentRotation;
            if (i < _previewPath.Count - 1)
                intentRot = _previewPath[i + 1] - _previewPath[i];
            else if (i > 0)
                intentRot = _previewPath[i] - _previewPath[i - 1];

            // 2. 检查格子上是否已有东西
            int occupantId = GridSystem.Instance.GetOccupantId(pos);

            if (occupantId == -1)
            {
                // --- 情况 A: 空地，直接造 ---
                if (GridSystem.Instance.IsAreaClear(pos, bp.LogicSize))
                {
                    EntityHandle h = EntitySystem.Instance.CreateEntityFromBlueprint(_currentBlueprintKey, pos, 1);
                    int idx = EntitySystem.Instance.GetIndex(h);
                    if (idx != -1)
                    {
                        EntitySystem.Instance.wholeComponent.coreComponent[idx].Rotation = intentRot;
                        anyChange = true;
                        PostSystem.Instance.Send("建筑完成", _currentBlueprintKey);
                    }
                }
            }
            else
            {
                // --- 情况 B: 已有实体，判断是否需要“意图覆盖” ---
                EntityHandle h = EntitySystem.Instance.GetHandleFromId(occupantId);
                int idx = EntitySystem.Instance.GetIndex(h);

                if (idx != -1 && EntitySystem.Instance.wholeComponent.workComponent[idx].WorkType == WorkType.Conveyor)
                {
                    // 如果已经是传送带，玩家又拉了一次，说明要改方向！
                    // 这就是“注入意图”：直接修改已有实体的 Rotation
                    EntitySystem.Instance.wholeComponent.coreComponent[idx].Rotation = intentRot;
                    anyChange = true;
                    // Debug.Log($"<color=yellow>意图注入：更新了坐标 {pos} 的传送带方向为 {intentRot}</color>");
                }
            }
        }

        // 只要有任何改动，重建网络。
        // 现在的 RebuildNetwork 只需要严格遵守实体的 Rotation (In/Out) 即可，
        // 因为玩家的意图已经在建造时通过 Rotation 表达清楚了喵！
        if (anyChange)
        {
            TransportSystem.Instance.RebuildNetwork(EntitySystem.Instance.wholeComponent);
        }

        // 只有当 (既不是默认连续) 且 (也没按住Shift) 时，才退出
        // 也就是说，默认情况下它会一直留着预览虚影，直到你右键喵！

        // --- 【连续建造核心手感优化】 ---
        bool isContinuous = defaultContinuousBuild || Input.GetKey(KeyCode.LeftShift);

        if (isContinuous)
        {
            // 喵！核心逻辑：把当前的终点设为下一次的起点，并且保持 _isDragging 开启！
            _startGridPos = lastPointOfPath;
            _isDragging = true;
            _firstMoveLocked = false; // 喵！接力时允许重新判定下一段的方向

            // 这样 UpdatePlacementLogic 下一帧就会立刻以刚才的终点为基准拉出新的虚影
        }
        else
        {
            CancelBuilding();
        }
    }

    private void TryPlaceSingle(Vector2Int pos)
    {
        var bp = BlueprintRegistry.Get(_currentBlueprintKey);
        if (GridSystem.Instance.IsAreaClear(pos, bp.LogicSize))
        {
            EntityHandle h = EntitySystem.Instance.CreateEntityFromBlueprint(_currentBlueprintKey, pos, 1);
            int idx = EntitySystem.Instance.GetIndex(h);
            if (idx != -1)
            {
                EntitySystem.Instance.wholeComponent.coreComponent[idx].Rotation = _currentRotation;

                PostSystem.Instance.Send("建筑完成", _currentBlueprintKey);
                // --- 【核心手感修改】 ---
                bool shouldExit = !defaultContinuousBuild && !Input.GetKey(KeyCode.LeftShift);

                if (shouldExit)
                {
                    CancelBuilding();
                }
                else if (bp.WorkType == WorkType.Conveyor)
                {
                    // 如果是连续造传送带，每次点下去都要刷新一下网络
                    TransportSystem.Instance.RebuildNetwork(EntitySystem.Instance.wholeComponent);
                }
            }
        }
    }

    // =========================================================
    // 表现层：虚影渲染
    // =========================================================
    private void RenderGhosts(List<Vector2Int> path, EntityBlueprint bp)
    {
        HideAllGhosts();
        for (int i = 0; i < path.Count; i++)
        {
            GameObject g = GetGhostFromPool(i);
            g.SetActive(true);
            g.transform.position = GridSystem.Instance.GridToWorld(path[i], bp.LogicSize);
            g.transform.localScale = bp.VisualScale;

            var sr = g.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteLib.Instance.unitSprites[bp.SpriteId];
            sr.color = GridSystem.Instance.IsAreaClear(path[i], bp.LogicSize) ? canBuildColor : cannotBuildColor;

            // 处理虚影的旋转
            Vector2Int rot = _currentRotation;
            if (path.Count > 1)
            {
                if (i < path.Count - 1) rot = path[i + 1] - path[i];
                else rot = path[i] - path[i - 1];
            }
            float angle = Mathf.Atan2(rot.y, rot.x) * Mathf.Rad2Deg - 90f;
            g.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private GameObject GetGhostFromPool(int index)
    {
        while (_ghostPool.Count <= index)
        {
            GameObject g = new GameObject("LineGhost_" + _ghostPool.Count);
            g.transform.SetParent(_ghostRoot);
            g.AddComponent<SpriteRenderer>().sortingLayerName = "UI";
            _ghostPool.Add(g);
        }
        return _ghostPool[index];
    }

    private void HideAllGhosts()
    {
        foreach (var g in _ghostPool)
        {

            g.SetActive(false);
            g.transform.localScale = Vector3.one;
        }
    }

    private void RotateGhost()
    {
        _currentRotation = new Vector2Int(_currentRotation.y, -_currentRotation.x);
    }

    private void PrepareGhost()
    {
        // 1. 立即清理掉之前可能残留的所有虚影
        HideAllGhosts();

        // 2. 获取当前蓝图数据
        var bp = BlueprintRegistry.Get(_currentBlueprintKey);
        if (string.IsNullOrEmpty(bp.Name)) return;

        // 3. 核心初始化：重置建造方向为默认向上
        _currentRotation = Vector2Int.up;

        // 4. 预热虚影池：取出第一个虚影，提前换上对应的 Sprite
        // 这样在 UpdatePlacementLogic 运行前，虚影的皮肤就已经准备好了喵！
        GameObject firstGhost = GetGhostFromPool(0);
        var sr = firstGhost.GetComponent<SpriteRenderer>();

        if (SpriteLib.Instance != null && bp.SpriteId >= 0)
        {
            sr.sprite = SpriteLib.Instance.unitSprites[bp.SpriteId];
        }

        // 5. 初始状态设为隐藏，由 RenderGhosts 根据鼠标位置决定在哪里显示
        firstGhost.SetActive(false);
    }
    #region 拆除模式相关

    private void EnterDemolishMode()
    {
        CancelBuilding(); // 进入拆除时先关掉建造模式
        _isDemolishing = true;
        _isDemolishDragging = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // 这里以后可以换成一个红色的小铲子图标喵
    }

    private void CancelDemolishMode()
    {
        _isDemolishing = false;
        _isDemolishDragging = false;
        HideAllGhosts();
    }

    private void UpdateDemolishLogic()
    {
        Vector2Int mouseGrid = GridSystem.GetMouseGridPos(Vector2Int.one);

        // 1. 确定框选范围（格子）
        Vector2Int start = _isDemolishDragging ? _demolishStartGrid : mouseGrid;
        Vector2Int end = mouseGrid;

        // 2. 【核心修复】将“格子范围”转化为“唯一的实体ID集合”
        // 使用 HashSet 自动去重，不管建筑占几格，只要扫到它一部分，就算选中它这一个整体
        HashSet<int> targetEntityIds = GetUniqueEntitiesInRect(start, end);

        // 3. 处理输入
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            _isDemolishDragging = true;
            _demolishStartGrid = mouseGrid;
        }

        if (Input.GetMouseButtonUp(0) && _isDemolishDragging)
        {
            ExecuteDemolish(targetEntityIds); // 直接传 ID 集合，不再传坐标
            _isDemolishDragging = false;
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelDemolishMode();
            return;
        }

        // 4. 渲染虚影 (针对实体渲染，而不是针对格子渲染)
        RenderDemolishGhosts(targetEntityIds);
    }

    // 辅助：从矩形区域获取所有唯一的实体 ID
    private HashSet<int> GetUniqueEntitiesInRect(Vector2Int start, Vector2Int end)
    {
        HashSet<int> ids = new HashSet<int>();
        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);
        int minY = Mathf.Min(start.y, end.y);
        int maxY = Mathf.Max(start.y, end.y);

        var grid = GridSystem.Instance;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int id = grid.GetOccupantId(new Vector2Int(x, y));
                // 过滤 -1 (空地) 和 即使是同一建筑的不同格子，HashSet 也会自动去重
                if (id != -1)
                {
                    ids.Add(id);
                }
            }
        }
        return ids;
    }


    private void RenderDemolishGhosts(HashSet<int> entityIds)
    {
        HideAllGhosts();
        int ghostIndex = 0;

        foreach (int id in entityIds)
        {
            // 获取数据
            EntityHandle handle = EntitySystem.Instance.GetHandleFromId(id);
            int idx = EntitySystem.Instance.GetIndex(handle);
            if (idx == -1) continue;

            ref var core = ref EntitySystem.Instance.wholeComponent.coreComponent[idx];

            // 过滤掉不可拆除的（比如地板、特效、子弹）
            // 这里必须严格判断，否则可能选到不该选的东西导致逻辑混乱
            if ((core.Type & UnitType.Building) == 0) continue;

            GameObject g = GetGhostFromPool(ghostIndex++);
            g.SetActive(true);

            // 【关键】直接使用 Core.Position，这是建筑的物理中心
            // 无论它占 1x1 还是 3x3，X 永远在正中间
            g.transform.position = core.Position;

            // 缩放适配：根据 LogicSize 决定 X 的大小
            float scale = Mathf.Min(core.LogicSize.x, core.LogicSize.y);
            g.transform.localScale = Vector3.one * scale;

            // 设置颜色和图层
            var sr = g.GetComponent<SpriteRenderer>();
            sr.sprite = Utils.WhiteTextureToSprite(); // 或者你的 X 图片
            sr.color = demolishColor;
            sr.sortingOrder = 500; // 确保在最上层
        }
    }


    private void ExecuteDemolish(HashSet<int> entityIds)
    {
        if (entityIds.Count == 0) return;

        bool anyChanged = false;
        foreach (int id in entityIds)
        {
            EntityHandle handle = EntitySystem.Instance.GetHandleFromId(id);

            // 再次校验 Handle 有效性，防止在循环中被删了两次（极少数情况）
            if (!EntitySystem.Instance.IsValid(handle)) continue;

            // 这里调用 EntitySystem 的销毁
            EntitySystem.Instance.DestroyEntity(handle);
            anyChanged = true;
        }

        // 只有真拆了才重建网格，节省性能
        if (anyChanged)
        {
            // 稍后在 System 层面解决重建，这里甚至不需要手动调 Rebuild
        }
        TransportSystem.Instance.RebuildIfDirty(EntitySystem.Instance.wholeComponent);
    }

    #endregion

    /// <summary>
    /// 战略配备呼叫：由 StratagemManager 调用
    /// </summary>
    public void EnterStratagemDeployment(string bpKey, Action<Vector2Int, Vector2Int> onConfirm)
    {
        // 1. 先把现有的建造或拆除模式关掉
        CancelBuilding();
        CancelDemolishMode();

        // 2. 注入回调并开启预览
        _onExternalDeployConfirm = onConfirm;
        SetCurrentBlueprint(bpKey);

        Debug.Log($"<color=yellow>[部署模式]</color> 正在为 {bpKey} 寻找着陆点...");
    }
}