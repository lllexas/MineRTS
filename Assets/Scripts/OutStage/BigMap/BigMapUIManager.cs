using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【Controller】大地图 UI 管理器
/// 职责：管理大地图界面上除了节点之外的所有 UI 元素
///  - 返回主菜单按钮（带保存和确认）
///  - 设置按钮
///  - 其他大地图功能按钮
/// 设计原则：遵循小零件架构，作为 IMenuPanel 接入 GameFlowController 状态机
/// 设计理念：不提供直接退出程序选项，让玩家欣赏精美的主菜单特效喵！
/// </summary>
public class BigMapUIManager : SingletonMono<BigMapUIManager>, IMenuPanel
{
    [Header("UI 根节点")]
    [SerializeField] private GameObject _uiRoot; // 大地图 UI 面板根节点

    [Header("功能按钮")]
    [SerializeField] private Button _backToMainMenuButton; // 返回主菜单（保存并确认）
    [SerializeField] private Button _settingsButton;    // 设置

    [Header("子面板引用")]
    [SerializeField] private GameObject _settingsPanel; // 设置面板（可选）
    [SerializeField] private GameObject _confirmBackPanel; // 返回主菜单确认弹窗（可选）

    [Header("确认弹窗按钮")]
    [SerializeField] private Button _confirmBackButton; // 确认返回主菜单
    [SerializeField] private Button _cancelBackButton;  // 取消返回

    // IMenuPanel 接口实现
    private bool _isOpen = false;

    public GameObject PanelRoot => _uiRoot != null ? _uiRoot : gameObject;
    public bool IsOpen => _isOpen;

    protected override void Awake()
    {
        base.Awake();
        Debug.Log("<color=cyan>[BigMapUIManager]</color> 初始化完成");

        // 绑定按钮事件
        if (_backToMainMenuButton != null)
        {
            _backToMainMenuButton.onClick.AddListener(OnBackToMainMenuClicked);
            Debug.Log("<color=cyan>[BigMapUIManager]</color> 返回主菜单按钮事件已绑定");
        }

        if (_settingsButton != null)
        {
            _settingsButton.onClick.AddListener(OnSettingsClicked);
            Debug.Log("<color=yellow>[BigMapUIManager]</color> 设置按钮事件已绑定（功能待实现）");
        }

        // 绑定确认弹窗按钮事件
        if (_confirmBackButton != null)
        {
            _confirmBackButton.onClick.AddListener(OnConfirmBackClicked);
            Debug.Log("<color=cyan>[BigMapUIManager]</color> 确认返回按钮事件已绑定");
        }

        if (_cancelBackButton != null)
        {
            _cancelBackButton.onClick.AddListener(OnCancelBackClicked);
            Debug.Log("<color=cyan>[BigMapUIManager]</color> 取消返回按钮事件已绑定");
        }

        // 初始隐藏子面板
        if (_settingsPanel != null)
        {
            _settingsPanel.SetActive(false);
        }

        if (_confirmBackPanel != null)
        {
            _confirmBackPanel.SetActive(false);
        }
    }

    private void Start()
    {
        // 确保初始状态正确
        if (_isOpen != PanelRoot.activeSelf)
        {
            PanelRoot.SetActive(_isOpen);
        }
    }

    // ==========================================
    // IMenuPanel 接口实现
    // ==========================================

    /// <summary>
    /// 打开大地图 UI 面板
    /// </summary>
    public void Open()
    {
        if (_isOpen) return;

        _isOpen = true;
        PanelRoot.SetActive(true);
        Debug.Log("<color=cyan>[BigMapUIManager]</color> 大地图 UI 面板已打开");
    }

    /// <summary>
    /// 关闭大地图 UI 面板
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        PanelRoot.SetActive(false);

        // 关闭子面板
        if (_settingsPanel != null)
        {
            _settingsPanel.SetActive(false);
        }

        if (_confirmBackPanel != null)
        {
            _confirmBackPanel.SetActive(false);
        }

        Debug.Log("<color=cyan>[BigMapUIManager]</color> 大地图 UI 面板已关闭");
    }

    // ==========================================
    // 按钮点击事件处理
    // ==========================================

    /// <summary>
    /// 返回主菜单按钮点击事件
    /// </summary>
    private void OnBackToMainMenuClicked()
    {
        Debug.Log("<color=cyan>[BigMapUIManager]</color> 返回主菜单按钮被点击");
        ShowConfirmBackToMainMenu();
    }

    /// <summary>
    /// 设置按钮点击事件
    /// </summary>
    private void OnSettingsClicked()
    {
        Debug.Log("<color=yellow>[BigMapUIManager]</color> 设置按钮被点击");
        ToggleSettingsPanel();
    }

    /// <summary>
    /// 确认返回主菜单按钮点击事件
    /// </summary>
    private void OnConfirmBackClicked()
    {
        Debug.Log("<color=cyan>[BigMapUIManager]</color> 确认返回主菜单");
        SaveAndBackToMainMenu();
    }

    /// <summary>
    /// 取消返回主菜单按钮点击事件
    /// </summary>
    private void OnCancelBackClicked()
    {
        Debug.Log("<color=cyan>[BigMapUIManager]</color> 取消返回主菜单");
        CloseConfirmPanel();
    }

    // ==========================================
    // PostSystem 事件订阅（支持外部触发）
    // ==========================================

    private void OnEnable()
    {
        // 订阅返回主菜单确认事件（供其他系统触发）
        PostSystem.Instance.On("BigMap.ShowConfirmBack", OnShowConfirmBack);
    }

    private void OnDisable()
    {
        // 取消订阅
        if (PostSystem.Instance != null)
            PostSystem.Instance.Off("BigMap.ShowConfirmBack", OnShowConfirmBack);
    }

    /// <summary>
    /// 外部触发显示确认弹窗
    /// </summary>
    private void OnShowConfirmBack(object data)
    {
        ShowConfirmBackToMainMenu();
    }

    // ==========================================
    // 功能方法
    // ==========================================

    /// <summary>
    /// 切换设置面板显示/隐藏
    /// </summary>
    public void ToggleSettingsPanel()
    {
        if (_settingsPanel == null)
        {
            Debug.LogWarning("<color=orange>[BigMapUIManager]</color> 设置面板未设置");
            return;
        }

        bool isActive = _settingsPanel.activeSelf;
        _settingsPanel.SetActive(!isActive);

        Debug.Log($"<color=cyan>[BigMapUIManager]</color> 设置面板已{(!isActive ? "打开" : "关闭")}");
    }

    /// <summary>
    /// 显示返回主菜单确认弹窗
    /// </summary>
    public void ShowConfirmBackToMainMenu()
    {
        if (_confirmBackPanel != null)
        {
            // 如果有确认弹窗，显示它
            _confirmBackPanel.SetActive(true);
            Debug.Log("<color=yellow>[BigMapUIManager]</color> 显示返回主菜单确认弹窗");
        }
        else
        {
            // 没有确认弹窗，直接返回主菜单（带保存）
            Debug.Log("<color=yellow>[BigMapUIManager]</color> 无确认弹窗，直接返回主菜单");
            SaveAndBackToMainMenu();
        }
    }

    /// <summary>
    /// 保存游戏并返回主菜单
    /// </summary>
    public void SaveAndBackToMainMenu()
    {
        Debug.Log("<color=cyan>[BigMapUIManager]</color> 正在保存并返回主菜单...");

        // 1. 保存游戏
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGameToDisk();
            Debug.Log("<color=green>[BigMapUIManager]</color> 游戏已保存");
        }

        // 2. 清理当前关卡状态
        if (EntitySystem.Instance != null)
        {
            EntitySystem.Instance.ClearWorld();
        }

        // 3. 重置 MainModel 状态
        if (MainModel.Instance != null)
        {
            MainModel.Instance.ClearCurrentStage();
        }

        // 4. 切换回主菜单状态（欣赏特效喵！）
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.SwitchToState(GameFlowController.GameState.MainMenu);
        }

        // 5. 关闭确认弹窗（如果有）
        if (_confirmBackPanel != null)
        {
            _confirmBackPanel.SetActive(false);
        }

        // 6. 关闭当前 UI
        Close();

        Debug.Log("<color=green>[BigMapUIManager]</color> 返回主菜单完成，请欣赏特效喵！");
    }

    /// <summary>
    /// 返回主菜单（不保存）
    /// </summary>
    public void BackToMainMenuWithoutSave()
    {
        Debug.Log("<color=orange>[BigMapUIManager]</color> 正在返回主菜单（未保存）...");

        // 1. 清理当前关卡状态
        if (EntitySystem.Instance != null)
        {
            EntitySystem.Instance.ClearWorld();
        }

        // 2. 重置 MainModel 状态
        if (MainModel.Instance != null)
        {
            MainModel.Instance.ClearCurrentStage();
        }

        // 3. 切换回主菜单状态
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.SwitchToState(GameFlowController.GameState.MainMenu);
        }

        // 4. 关闭确认弹窗
        if (_confirmBackPanel != null)
        {
            _confirmBackPanel.SetActive(false);
        }

        // 5. 关闭当前 UI
        Close();

        Debug.LogWarning("<color=orange>[BigMapUIManager]</color> 返回主菜单完成（未保存进度）");
    }

    /// <summary>
    /// 打开设置面板
    /// </summary>
    public void OpenSettings()
    {
        if (_settingsPanel != null)
        {
            _settingsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 关闭设置面板
    /// </summary>
    public void CloseSettings()
    {
        if (_settingsPanel != null)
        {
            _settingsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 关闭确认弹窗
    /// </summary>
    public void CloseConfirmPanel()
    {
        if (_confirmBackPanel != null)
        {
            _confirmBackPanel.SetActive(false);
        }
    }
}
