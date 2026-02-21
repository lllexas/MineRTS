using System.Collections.Generic;
using UnityEngine;

public class UserControlSystem : SingletonMono<UserControlSystem>
{
    [Header("设置")]
    public LayerMask groundLayer; // 地面图层（如果有点选碰撞体的话）
    private bool _isAttackMoving = false; // 是否处于 A 键瞄准模式
    public Texture2D cursorAttack; // 请在 Inspector 里拖一个红色瞄准光标图片！

    // 当前选中的单位句柄列表
    private List<EntityHandle> _selectedHandles = new List<EntityHandle>();

    // 框选相关
    private Vector2 _boxStartPos;
    private bool _isDragging = false;
    private Camera _mainCam;

    // 获取当前选中的所有单位
    public List<EntityHandle> SelectedEntities => _selectedHandles;

    protected override void Awake()
    {
        base.Awake();
        _mainCam = Camera.main;
    }

    void Update()
    {
        if (_isAttackMoving)
        {
            HandleAttackModeInput(); // 专门处理 A 键模式下的点击
        }
        else
        {
            HandleSelection();
            HandleRightClick();
        }

        // 3. 快捷键（S键停止，A键攻击移动等可以后续加喵）
        HandleHotkeys();
    }

    private void HandleSelection()
    {
        // 按下左键：记录起始点
        if (Input.GetMouseButtonDown(0))
        {
            _isDragging = true;
            _boxStartPos = Input.mousePosition;
        }

        // 释放左键：执行选择
        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;

            // 如果拖拽距离极小，视为“点选”
            if (Vector2.Distance(_boxStartPos, Input.mousePosition) < 5f)
            {
                DoPointSelection(Input.mousePosition);
            }
            else
            {
                DoBoxSelection(_boxStartPos, Input.mousePosition);
            }
        }
    }

    private void DoPointSelection(Vector2 mousePos)
    {
        ClearSelection();
        Vector2Int gridPos = GridSystem.GetMouseGridPos(new Vector2Int(1, 1));

        int occupantId = GridSystem.Instance.GetOccupantId(gridPos);
        if (occupantId != -1)
        {
            EntityHandle handle = EntitySystem.Instance.GetHandleFromId(occupantId);
            if (EntitySystem.Instance.IsValid(handle))
            {
                AddToSelection(handle);
            }
        }
    }

    private void DoBoxSelection(Vector2 start, Vector2 end)
    {
        ClearSelection();

        // 1. 获取鼠标拖出来的世界空间矩形
        Vector2 vMin = _mainCam.ScreenToWorldPoint(Vector3.Min(start, end));
        Vector2 vMax = _mainCam.ScreenToWorldPoint(Vector3.Max(start, end));
        Rect selectionRect = new Rect(vMin.x, vMin.y, vMax.x - vMin.x, vMax.y - vMin.y);

        var whole = EntitySystem.Instance.wholeComponent;
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            if (!core.Active || core.Team != 1 || (core.Type & UnitType.Projectile) != 0) continue;

            // --- 【核心修改：计算单位的世界占用矩形】 ---
            Vector2Int size = core.LogicSize;
            // 注意：我们的 GridToWorld 已经考虑了 1x1 和 2x2 的中心点对齐
            // 所以单位的矩形应该是：中心点坐标 +/- 尺寸的一半
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;
            Rect unitRect = new Rect(core.Position.x - halfW, core.Position.y - halfH, size.x, size.y);

            // --- 【判断逻辑：矩形相交就算选中！】 ---
            // 哪怕你的框只蹭到了坦克的一个履带边缘，也能选上喵！
            if (selectionRect.Overlaps(unitRect))
            {
                AddToSelection(core.SelfHandle);
            }
        }
    }

    private void HandleRightClick()
    {
        if (Input.GetMouseButtonDown(1) && _selectedHandles.Count > 0)
        {
            Vector2Int gridPos = GridSystem.GetMouseGridPos(Vector2Int.one);

            // 检查右键点击了什么
            int targetId = GridSystem.Instance.GetOccupantId(gridPos);
            EntityHandle targetHandle = EntityHandle.None;
            if (targetId != -1) targetHandle = EntitySystem.Instance.GetHandleFromId(targetId);

            // 分发指令给所有选中的单位
            foreach (var handle in _selectedHandles)
            {
                IssueCommand(handle, gridPos, targetHandle);
            }
        }
    }

    private void IssueCommand(EntityHandle handle, Vector2Int targetGrid, EntityHandle targetEntity)
    {
        if (!EntitySystem.Instance.IsValid(handle)) return;
        int idx = EntitySystem.Instance.GetIndex(handle);
        var whole = EntitySystem.Instance.wholeComponent;

        if ((whole.coreComponent[idx].Type & UnitType.Building) != 0) return;

        // Debug.Log($"<color=orange>[Command]</color> 玩家右键指令 -> 单位ID: {idx}, 目标格: {targetGrid}");
        ref var ai = ref whole.aiComponent[idx];
        ref var move = ref whole.moveComponent[idx];
        ref var atk = ref whole.attackComponent[idx];

        // 1. 状态重置
        atk.TargetEntityId = -1;
        ai.TargetEntity = EntityHandle.None;
        ai.CurrentState = AIState.Moving;

        // 2. 🔥【废除旧优化】无论目的地是否相同，只要玩家点了，就执行完整的重置
        move.TargetGridPosition = targetGrid;

        // 彻底洗脑：让 Boids 认为单位是刚出生的，没有任何移动惯性和折返阻尼
        move.PreviousLogicalPosition = move.LogicalPosition;
        move.NextStepTile = move.LogicalPosition;
        move.HasNextStep = false;

        // 3. 寻路重置与强制请求
        move.Waypoints = null;
        move.WaypointIndex = 0;
        move.IsBlocked = false;
        move.TargetPortalExit = null;
        move.StuckTimerTicks = 0; // 顺便把卡死计时也清了

        // 🔥 即使 IsPathPending 是 true，我们也应该允许重新覆盖请求
        // 或者至少确保如果 Waypoints 是空的，就一定要请求
        move.IsPathPending = false; // 强制解锁，确保 RequestPath 必成
        PathfindingSystem.Instance.RequestPath(idx);
    }

    private void HandleHotkeys()
    {
        // 按下 S 键：全部停止
        if (Input.GetKeyDown(KeyCode.S))
        {
            foreach (var handle in _selectedHandles)
            {
                if (!EntitySystem.Instance.IsValid(handle)) continue;
                int idx = EntitySystem.Instance.GetIndex(handle);
                var whole = EntitySystem.Instance.wholeComponent;

                ref var ai = ref whole.aiComponent[idx];
                ref var move = ref whole.moveComponent[idx];

                ai.CurrentState = AIState.Idle;
                ai.TargetEntity = EntityHandle.None;

                // 🔥【核心修正：停止即洗脑】
                // 目标设为当前位置
                move.TargetGridPosition = move.LogicalPosition;
                // 清除折返记忆
                move.PreviousLogicalPosition = move.LogicalPosition;
                // 取消待执行指令
                move.HasNextStep = false;

                // 路径也清掉
                move.Waypoints = null;
            }
        }
        // >>>>>>>>>>> [新增：A键触发] >>>>>>>>>>>
        if (Input.GetKeyDown(KeyCode.A))
        {
            if (_selectedHandles.Count > 0)
            {
                EnterAttackMode();
            }
        }
    }
    // --- 辅助方法 ---

    private void AddToSelection(EntityHandle handle)
    {
        if (!_selectedHandles.Contains(handle))
        {
            _selectedHandles.Add(handle);
            SetHighlight(handle, true);
        }
    }

    private void ClearSelection()
    {
        foreach (var handle in _selectedHandles)
        {
            SetHighlight(handle, false);
        }
        _selectedHandles.Clear();
    }

    private void SetHighlight(EntityHandle handle, bool highlight)
    {
        int index = EntitySystem.Instance.GetIndex(handle);
        if (index != -1)
            EntitySystem.Instance.wholeComponent.drawComponent[index].IsSelected = highlight;
    }
    private void IssueAttackCommand(EntityHandle handle, Vector2Int targetGrid, EntityHandle targetEntity)
    {
        if (!EntitySystem.Instance.IsValid(handle)) return;
        int idx = EntitySystem.Instance.GetIndex(handle);
        var whole = EntitySystem.Instance.wholeComponent;

        // 只有战斗单位能攻击
        if ((whole.coreComponent[idx].Type & (UnitType.Minion | UnitType.Hero)) == 0) return;

        ref var ai = ref whole.aiComponent[idx];
        ref var move = ref whole.moveComponent[idx];
        ref var atk = ref whole.attackComponent[idx];

        // 1. 判定是 A地板 还是 A人
        if (EntitySystem.Instance.IsValid(targetEntity))
        {
            // --- 情况 A: A人 (AttackTarget) ---
            // 只有当目标是敌军时才锁定攻击
            int tIdx = EntitySystem.Instance.GetIndex(targetEntity);
            if (whole.coreComponent[tIdx].Team != whole.coreComponent[idx].Team)
            {
                ai.CurrentCommand = UnitCommand.AttackTarget;
                ai.TargetEntity = targetEntity;
                Debug.Log($"<color=red>[Command]</color> 锁定攻击 -> 目标ID: {targetEntity.Id}");
            }
            else
            {
                // 如果A了友军，变成跟随或者普通的移动
                ai.CurrentCommand = UnitCommand.Move;
                move.TargetGridPosition = targetGrid;
            }
        }
        else
        {
            // --- 情况 B: A地板 (AttackMove) ---
            ai.CurrentCommand = UnitCommand.AttackMove;
            ai.CommandPos = targetGrid;

            // 下达移动相关的重置逻辑 (复用 IssueCommand 的逻辑)
            move.TargetGridPosition = targetGrid;
            move.PreviousLogicalPosition = move.LogicalPosition;
            move.NextStepTile = move.LogicalPosition;
            move.HasNextStep = false;
            move.Waypoints = null;
            move.WaypointIndex = 0;
            move.IsBlocked = false;
            move.IsPathPending = false;
            PathfindingSystem.Instance.RequestPath(idx);

            Debug.Log($"<color=red>[Command]</color> 攻击移动 -> 目标格: {targetGrid}");
        }

        // 无论哪种情况，都先把状态设为 Moving，让 AutoAI 去接管
        ai.CurrentState = AIState.Moving;
    }
    // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
    // >>>>>>>>>>> [新增：A键模式处理逻辑] >>>>>>>>>>>
    private void HandleAttackModeInput()
    {
        // 右键取消 A 模式
        if (Input.GetMouseButtonDown(1))
        {
            ExitAttackMode();
            return;
        }

        // 左键确认攻击
        if (Input.GetMouseButtonDown(0))
        {
            Vector2Int gridPos = GridSystem.GetMouseGridPos(Vector2Int.one);
            int targetId = GridSystem.Instance.GetOccupantId(gridPos);
            EntityHandle targetHandle = EntityHandle.None;
            if (targetId != -1) targetHandle = EntitySystem.Instance.GetHandleFromId(targetId);

            // 无论有没有点中敌人，都把指令发下去
            // 如果点中了敌人， targetHandle 有效，就是 AttackTarget
            // 如果点中了地板， targetHandle 无效，就是 AttackMove
            foreach (var handle in _selectedHandles)
            {
                IssueAttackCommand(handle, gridPos, targetHandle);
            }

            // 发完指令退出模式
            ExitAttackMode();
        }
    }

    private void EnterAttackMode()
    {
        _isAttackMoving = true;
        // 切换鼠标图标 (热点设在中心)
        Cursor.SetCursor(cursorAttack, new Vector2(16, 16), CursorMode.Auto);
    }

    private void ExitAttackMode()
    {
        _isAttackMoving = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
    // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<

    // 可以在这里画一个简单的 GUI 框选矩形
    void OnGUI()
    {
        if (_isDragging)
        {
            var rect = Utils.GetScreenRect(_boxStartPos, Input.mousePosition);
            Utils.DrawScreenRect(rect, new Color(0.8f, 0.8f, 0.95f, 0.25f));
            Utils.DrawScreenRectBorder(rect, 2, new Color(0.8f, 0.8f, 0.95f));
        }
    }
}

// 辅助绘图类 (需要新建一个 Utils.cs 或者放在下面)
public static class Utils
{
    static Texture2D _whiteTexture;
    public static Texture2D WhiteTexture
    {
        get
        {
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(1, 1);
                _whiteTexture.SetPixel(0, 0, Color.white);
                _whiteTexture.Apply();
            }
            return _whiteTexture;
        }
    }

    public static void DrawScreenRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, WhiteTexture);
        GUI.color = Color.white;
    }

    public static void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        DrawScreenRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawScreenRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        DrawScreenRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
    }

    public static Rect GetScreenRect(Vector3 screenPos1, Vector3 screenPos2)
    {
        screenPos1.y = Screen.height - screenPos1.y;
        screenPos2.y = Screen.height - screenPos2.y;
        var topLeft = Vector3.Min(screenPos1, screenPos2);
        var bottomRight = Vector3.Max(screenPos1, screenPos2);
        return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
    }

    private static Sprite _whiteSprite;

    /// <summary>
    /// 将 1x1 的纯白纹理转换成 Sprite，用于 UI 遮罩或拆除红框
    /// </summary>
    public static Sprite WhiteTextureToSprite()
    {
        if (_whiteSprite == null)
        {
            // 使用我们之前定义的 WhiteTexture (1x1)
            Texture2D tex = WhiteTexture;

            // 创建一个 Sprite
            // 参数：纹理, 矩形区域, 轴心(中心), 像素单位(1表示1像素对应1个Unity单位)
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
        }
        return _whiteSprite;
    }
}