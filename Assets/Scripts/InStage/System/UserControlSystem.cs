using System.Collections.Generic;
using UnityEngine;

public class UserControlSystem : SingletonMono<UserControlSystem>
{
    [Header("设置")]
    public LayerMask groundLayer; // 地面图层（如果有点选碰撞体的话）
    public int playerTeam = 1; // 可供玩家选择的队伍，默认为1
    private bool _isAttackMoving = false; // 是否处于 A 键瞄准模式
    public Texture2D cursorAttack; // 请在 Inspector 里拖一个红色瞄准光标图片！

    // 当前选中的单位句柄列表
    private List<EntityHandle> _selectedHandles = new List<EntityHandle>();

    // 框选相关
    private Vector2 _boxStartPos;
    private bool _isDragging = false;
    private Camera _mainCam;

    // 防止攻击模式左键点击后误触发选择清除
    private bool _skipSelectionThisFrame = false;

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
        // 如果标记为跳过选择（攻击模式左键点击后）
        if (_skipSelectionThisFrame)
        {
            // 只有在抬起鼠标后，才彻底关闭这个拦截开关喵
            if (Input.GetMouseButtonUp(0)) _skipSelectionThisFrame = false;
            _isDragging = false;
            return;
        }

        // 按下左键：记录起始点
        if (Input.GetMouseButtonDown(0))
        {
            // 只有非攻击模式下，按下左键才开启“拖拽合法状态”
            _isDragging = true;
            _boxStartPos = Input.mousePosition;
        }

        // 释放左键：执行选择
        if (Input.GetMouseButtonUp(0))
        {
            // 🔥 核心修正：如果不是从 Down 状态过来的合法拖拽，不许执行选择逻辑
            if (!_isDragging) return;

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
                // 检查单位队伍是否匹配玩家队伍
                int idx = EntitySystem.Instance.GetIndex(handle);
                var whole = EntitySystem.Instance.wholeComponent;
                if (whole.coreComponent[idx].Team == playerTeam)
                {
                    AddToSelection(handle);
                }
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
            if (!core.Active || core.Team != playerTeam || (core.Type & UnitType.Projectile) != 0) continue;

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
        // 使用AutoAISystem的统一API
        AutoAISystem.Instance.RequestMove(handle, targetGrid);
    }

    private void HandleHotkeys()
    {
        // 按下 S 键：全部停止
        if (Input.GetKeyDown(KeyCode.S))
        {
            foreach (var handle in _selectedHandles)
            {
                AutoAISystem.Instance.RequestStop(handle);
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

    /// <summary>
    /// 清除所有选择状态（供系统重置时调用）
    /// </summary>
    public void ClearAllSelection()
    {
        ClearSelection();
    }

    private void SetHighlight(EntityHandle handle, bool highlight)
    {
        int index = EntitySystem.Instance.GetIndex(handle);
        if (index != -1)
            EntitySystem.Instance.wholeComponent.drawComponent[index].IsSelected = highlight;
    }
    public void IssueAttackCommand(EntityHandle handle, Vector2Int targetGrid, EntityHandle targetEntity)
    {
        // 使用AutoAISystem的统一API
        if (EntitySystem.Instance.IsValid(targetEntity))
        {
            // 攻击特定目标
            AutoAISystem.Instance.RequestAttackTarget(handle, targetGrid, targetEntity);
        }
        else
        {
            // 攻击移动（A地板）
            AutoAISystem.Instance.RequestAttackMove(handle, targetGrid);
        }
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

            // 标记跳过下一帧的选择处理，防止攻击模式左键点击误触发选择清除
            _skipSelectionThisFrame = true;

            // 无论有没有点中敌人，都把指令发下去
            // 如果点中了敌人， targetHandle 有效，就是 AttackTarget
            // 如果点中了地板， targetHandle 无效，就是 AttackMove
            foreach (var handle in _selectedHandles)
            {
                IssueAttackCommand(handle, gridPos, targetHandle);
            }

            // 发完指令退出模式，但不清除跳过标志，因为需要在鼠标抬起时拦截选择逻辑
            _isAttackMoving = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            // 注意：_skipSelectionThisFrame 保持为 true，将在 HandleSelection 中鼠标抬起后清除
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
        // 清除攻击模式相关的跳过标志
        _skipSelectionThisFrame = false;
        // 注意：_skipLeftButtonRelease 在左键释放处理中清除
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