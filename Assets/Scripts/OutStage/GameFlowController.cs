using UnityEngine;
using MineRTS.BigMap;
using MineRTS.BigMap.UI;

/// <summary>
/// 【状态控制器】全局流程控制器，协调 UI 面板切换与游戏状态流转
/// 职责：管理游戏状态（主菜单、大地图、战斗界面），协调数据层与表现层
/// </summary>
public class GameFlowController : SingletonMono<GameFlowController>
{
    /// <summary>
    /// 游戏状态定义
    /// </summary>
    public enum GameState
    {
        MainMenu,      // 主菜单界面
        BigMap,        // 大地图界面
        InStage        // 战斗中界面
    }

    [Header("状态管理")]
    [SerializeField] private GameState _currentState = GameState.MainMenu;
    public GameState CurrentState
    {
        get => _currentState;
        private set => _currentState = value;
    }

    protected override void Awake()
    {
        base.Awake();
        Debug.Log("<color=cyan>[GameFlowController]</color> 初始化，当前状态：" + _currentState);

        // 注意：不在 Awake 中注册事件总线，留给 Start 做
    }

    private void Start()
    {
        // 1. 注册事件总线（确保所有单例已初始化）
        PostSystem.Instance.Register(this);

        // 2. 初始化游戏状态
        InitializeGameState();
    }

    private void OnDestroy()
    {
        // 注销事件总线监听
        if (PostSystem.Instance != null)
            PostSystem.Instance.Unregister(this);
    }

    /// <summary>
    /// 初始化游戏状态：尝试退出所有其他状态，然后进入当前状态
    /// 确保所有状态的清理逻辑都被执行，防止残留数据影响
    /// </summary>
    private void InitializeGameState()
    {
        Debug.Log("<color=cyan>[GameFlow]</color> 初始化游戏状态...");

        // 1. 尝试退出所有其他状态（确保清理）
        foreach (GameState state in System.Enum.GetValues(typeof(GameState)))
        {
            if (state != _currentState)
            {
                HandleStateExit(state);
            }
        }

        // 2. 进入当前状态
        HandleStateEnter(_currentState);

        Debug.Log("<color=cyan>[GameFlow]</color> 游戏状态初始化完成，当前状态：" + _currentState);
    }

    /// <summary>
    /// 监听大地图节点点击事件
    /// 现在不直接进入关卡，而是显示节点信息面板
    /// </summary>
    [Subscribe("BigMap.NodeClicked")]
    void OnNodeClicked(object data)
    {
        var e = data as NodeClickEvent;
        if (e != null && !string.IsNullOrEmpty(e.StageId))
        {
            Debug.Log($"<color=yellow>[GameFlow]</color> 收到节点点击事件：{e.DisplayName} ({e.StageId})");

            // 显示节点信息面板（由 WorldSpaceUIManager 处理）
            // 不再直接进入关卡
        }
    }

    /// <summary>
    /// 切换到指定状态，并更新 UI 面板显示
    /// </summary>
    public void SwitchToState(GameState newState)
    {
        if (_currentState == newState) return;

        Debug.Log($"<color=orange>[GameFlow]</color> 状态切换：{_currentState} → {newState}");

        // 退出当前状态
        HandleStateExit(_currentState);

        // 更新状态
        _currentState = newState;

        // 进入新状态
        HandleStateEnter(newState);
    }

    /// <summary>
    /// 处理状态退出逻辑
    /// </summary>
    private void HandleStateExit(GameState oldState)
    {
        switch (oldState)
        {
            case GameState.MainMenu:
                // 主菜单状态退出：关闭主菜单面板
                if (MainMenuManager.Instance != null)
                {
                    MainMenuManager.Instance.Close();
                }
                break;
            case GameState.BigMap:
                // 大地图状态退出：关闭大地图面板
                if (BigMapManager.Instance != null)
                {
                    BigMapManager.Instance.Close();
                    Debug.Log("<color=orange>[GameFlow]</color> 大地图面板已关闭");
                }
                else
                {
                    Debug.LogWarning("<color=orange>[GameFlow]</color> BigMapManager 实例未找到，无法关闭大地图面板");
                }

                // 关闭大地图 UI 管理器
                if (BigMapUIManager.Instance != null)
                {
                    BigMapUIManager.Instance.Close();
                    Debug.Log("<color=orange>[GameFlow]</color> 大地图 UI 管理器已关闭");
                }
                break;
            case GameState.InStage:

                // 1. UI 层面：
                if (InStageUIManager.Instance != null)
                {
                    InStageUIManager.Instance.Close();
                }
                else
                {
                    Debug.LogWarning("<color=orange>[GameFlow]</color> InStageUIManager 实例未找到，无法关闭战斗界面 UI");
                }

                Debug.Log("<color=orange>[GameFlow]</color> 退出关卡状态");
                break;
        }
    }

    /// <summary>
    /// 处理状态进入逻辑：同步协调 UI、背景底板、摄像机
    /// </summary>
    private void HandleStateEnter(GameState newState)
    {
        switch (newState)
        {
            case GameState.MainMenu:
                Debug.Log("<color=orange>[GameFlow]</color> 进入主菜单状态");

                // 1. UI 层面：打开主菜单面板
                if (MainMenuManager.Instance != null) MainMenuManager.Instance.Open();

                // 2. 背景层面：切换到主菜单背景（或者用 None 隐藏掉，节约性能）
                if (ViewportBackgroundQuad.Instance != null)
                {
                    // 如果主菜单是全屏纯 UI 遮挡，可以直接传 ViewportMode.None 隐藏 Quad
                    ViewportBackgroundQuad.Instance.ApplyMode(ViewportMode.MainMenu);
                }

                // 3. 摄像机层面：同步主菜单专用的限制边界
                if (CameraController.Instance != null)
                {
                    CameraController.Instance.SyncMainMenu();
                    CameraController.Instance.SetGameMode(GameState.MainMenu);
                }
                break;

            case GameState.BigMap:
                Debug.Log("<color=orange>[GameFlow]</color> 进入大地图状态");

                // 1. UI 层面：打开大地图面板
                if (BigMapManager.Instance != null) BigMapManager.Instance.Open();

                // 1.5 UI 层面：打开大地图 UI 管理器（保存、设置等按钮）
                if (BigMapUIManager.Instance != null) BigMapUIManager.Instance.Open();

                // 1.6 事件层面：发布"进入根界面"事件，通知分布式 UI 浮现
                PostSystem.Instance.Send("进入根界面", null);

                // 2. 背景层面：切换到大地图炫酷底板
                if (ViewportBackgroundQuad.Instance != null)
                {
                    ViewportBackgroundQuad.Instance.ApplyMode(ViewportMode.BigMap);
                }

                // 3. 摄像机层面：同步大地图的边界范围并居中
                if (CameraController.Instance != null)
                {
                    CameraController.Instance.SyncBigMap();
                    CameraController.Instance.GoToOrigin(); // 切到大地图时默认回原点
                    CameraController.Instance.SetGameMode(GameState.BigMap);
                }
                break;

            case GameState.InStage:
                Debug.Log("<color=orange>[GameFlow]</color> 进入战斗关卡状态");

                // 1. UI 层面：
                if (InStageUIManager.Instance != null)
                {
                    InStageUIManager.Instance.Open();
                }
                else
                {
                    Debug.LogWarning("<color=orange>[GameFlow]</color> InStageUIManager 实例未找到，无法打开战斗界面 UI");
                }

                // 2. 背景层面：切换到战斗用的基础正交网格
                if (ViewportBackgroundQuad.Instance != null)
                {
                    ViewportBackgroundQuad.Instance.ApplyMode(ViewportMode.None);
                }

                // 3. 摄像机层面：根据 ECS 当前加载的关卡数据同步边界
                if (CameraController.Instance != null)
                {
                    // 注意：因为 LoadStage 是在 EnterStage 触发的，此时 EntitySystem 里应该已经有数据了
                    CameraController.Instance.SyncBounds();
                    CameraController.Instance.InitializeCamera(); // 自动回原点并重置缩放
                    CameraController.Instance.SetGameMode(GameState.InStage);
                }
                break;
        }
    }

    /// <summary>
    /// 进入指定关卡（从大地图或主菜单调用）
    /// </summary>
    public void EnterStage(string stageID)
    {
        if (string.IsNullOrEmpty(stageID))
        {
            Debug.LogError("<color=red>[GameFlow]</color> 关卡 ID 不能为空");
            return;
        }

        if (!MainModel.Instance.HasUser)
        {
            Debug.LogError("<color=red>[GameFlow]</color> 没有加载用户存档，无法进入关卡");
            return;
        }

        Debug.Log($"<color=yellow>[GameFlow]</color> 正在进入关卡：{stageID}");

        // 1. 更新数据层状态
        MainModel.Instance.SetCurrentStage(stageID);

        // 2. 加载关卡到 ECS 系统
        EntitySystem.Instance.LoadStage(stageID);

        // 3. 切换到战斗界面状态
        SwitchToState(GameState.InStage);
    }

    /// <summary>
    /// 从关卡返回大地图
    /// </summary>
    /// <param name="autoSave">撤退前是否自动保存进度？</param>
    public void ReturnToMap(bool autoSave = true)
    {
        if (CurrentState != GameState.InStage)
        {
            Debug.LogWarning("<color=orange>[GameFlow]</color> 当前不在关卡中，无法返回大地图");
            return;
        }

        string lastStage = MainModel.Instance.CurrentActiveStageID;
        if (string.IsNullOrEmpty(lastStage))
        {
            Debug.LogWarning("<color=orange>[GameFlow]</color> 当前没有活跃关卡记录");
            return;
        }

        Debug.Log($"<color=orange>[GameFlow]</color> 正在从关卡 {lastStage} 撤离...");

        // 1. (可选) 撤退前自动保存热数据到 RAM
        if (autoSave)
        {
            SaveCurrentStageFromSystem();
            // 可以在此处添加磁盘保存逻辑，但通常由用户手动触发或定时保存
        }

        // 2. 物理清理 ECS 世界 (视觉消失，内存释放)
        if (EntitySystem.Instance != null)
        {
            EntitySystem.Instance.ClearWorld();
        }

        // 3. 清空数据层状态指针
        MainModel.Instance.ClearCurrentStage();

        // 4. 切换回大地图状态
        SwitchToState(GameState.BigMap);

        Debug.Log($"<color=green>[GameFlow]</color> 已安全撤离 {lastStage}，返回大地图待机。");
    }

    /// <summary>
    /// 【从 SaveManager 迁移】将 EntitySystem 当前运行的关卡数据，回写到 UserModel 内存中
    /// </summary>
    public void SaveCurrentStageFromSystem()
    {
        if (!MainModel.Instance.IsInStage)
        {
            Debug.LogWarning("<color=orange>[GameFlow]</color> 当前不在任何关卡里，无法保存关卡数据");
            return;
        }

        string stageID = MainModel.Instance.CurrentActiveStageID;
        if (EntitySystem.Instance == null || EntitySystem.Instance.wholeComponent == null)
        {
            Debug.LogError("<color=red>[GameFlow]</color> EntitySystem 未初始化，无法保存关卡数据");
            return;
        }

        Debug.Log($"<color=orange>[GameFlow]</color> 正在从 ECS 提取关卡数据：{stageID}...");

        // 1. 从 ECS 获取当前快照 (必须 Clone，防止引用污染)
        WholeComponent snapshot = EntitySystem.Instance.wholeComponent.Clone();

        // 2. 更新到 UserModel
        // IsCleared 逻辑根据游戏胜利条件来传 true/false，这里暂时传 false
        UpdateStageData(stageID, snapshot, false);

        Debug.Log($"<color=cyan>[GameFlow]</color> {stageID} 内存数据同步完成。");
    }

    /// <summary>
    /// 【从 SaveManager 迁移】更新指定关卡的数据到 UserModel
    /// </summary>
    public void UpdateStageData(string stageID, WholeComponent data, bool isCleared)
    {
        if (MainModel.Instance.CurrentUser == null)
        {
            Debug.LogError("<color=red>[GameFlow]</color> 当前没有用户数据，无法更新关卡数据");
            return;
        }

        MainModel.Instance.CurrentUser.UpdateStage(stageID, data, isCleared);
        Debug.Log($"<color=cyan>[GameFlow]</color> 关卡 {stageID} 数据已更新 (Cleared: {isCleared})");
    }

    /// <summary>
    /// 【从 SaveManager 迁移】重置指定关卡（删除存档记录，下次进入时会重新读 JSON）
    /// </summary>
    public void ResetStage(string stageID)
    {
        if (MainModel.Instance.CurrentUser == null)
        {
            Debug.LogWarning("<color=orange>[GameFlow]</color> 当前没有用户数据，无法重置关卡");
            return;
        }

        Debug.Log($"<color=red>[GameFlow]</color> 正在重置关卡存档：{stageID}");

        // 1. 从用户数据中移除该关卡的记录
        MainModel.Instance.CurrentUser.RemoveStage(stageID);

        // 2. 如果当前正在玩这一关，立即让 ECS 重新加载
        // (ECS 的 LoadStage 发现 UserData 里没了，就会自动去读 WorldFactory 的 JSON)
        if (MainModel.Instance.CurrentActiveStageID == stageID)
        {
            EntitySystem.Instance.LoadStage(stageID);
        }
    }
}
