using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【Controller】主菜单控制器 - 按钮协调版
/// 职责：管理主菜单四个核心按钮的引用和事件绑定
///  - 新游戏按钮（功能待实现）
///  - 加载游戏按钮（打开SavesView面板）
///  - 设置按钮（功能待实现）
///  - 退出按钮（功能待实现）
/// 设计原则：遵循小零件架构，MainMenuManager作为协调者而非上帝对象
/// 实现IMenuPanel接口，支持GameFlowController的状态机管理
/// </summary>
public class MainMenuManager : SingletonMono<MainMenuManager>, IMenuPanel
{
    [Header("UI引用")]
    [SerializeField] private GameObject _mainMenuRoot; // 主界面可视部分根节点
    [SerializeField] private SavesView _savesView;
    [SerializeField] private Button _newGameButton;
    [SerializeField] private Button _loadGameButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;

    // IMenuPanel接口实现
    private bool _isOpen = false;
    public GameObject PanelRoot => _mainMenuRoot != null ? _mainMenuRoot : gameObject;
    public bool IsOpen => _isOpen;

    protected override void Awake()
    {
        base.Awake();
        Debug.Log("<color=cyan>[MainMenuManager]</color> 初始化完成（小零件架构）");

        // 设置按钮事件监听器
        if (_loadGameButton != null)
        {
            _loadGameButton.onClick.AddListener(OnLoadGameClicked);
            Debug.Log("<color=cyan>[MainMenuManager]</color> 加载游戏按钮事件已绑定");
        }

        // 新游戏按钮
        if (_newGameButton != null)
        {
            _newGameButton.onClick.AddListener(OnNewGameClicked);
            Debug.Log("<color=cyan>[MainMenuManager]</color> 新游戏按钮事件已绑定");
        }

        // 设置按钮
        if (_settingsButton != null)
        {
            _settingsButton.onClick.AddListener(OnSettingsClicked);
            Debug.Log("<color=yellow>[MainMenuManager]</color> 设置按钮引用已设置，功能待实现");
        }

        // 退出按钮
        if (_quitButton != null)
        {
            _quitButton.onClick.AddListener(OnQuitClicked);
            Debug.Log("<color=cyan>[MainMenuManager]</color> 退出按钮事件已绑定");
        }
    }

    private void Start()
    {
        // 确保初始状态正确
        if (_savesView != null && _savesView.IsOpen)
        {
            _savesView.Close();
        }
    }

    /// <summary>
    /// 【公共API】打开存档选择界面
    /// 主菜单的"继续游戏"按钮应调用此方法
    /// 直接打开SavesView面板，GameFlowController保持在主菜单状态
    /// </summary>
    public void OpenSaveSelection()
    {
        if (_savesView == null)
        {
            Debug.LogError("<color=red>[MainMenuManager]</color> SavesView引用未设置");
            return;
        }

        Debug.Log("<color=cyan>[MainMenuManager]</color> 打开存档选择界面");
        _savesView.Open();
        // 注意：GameFlowController保持在MainMenu状态，不切换状态
    }

    /// <summary>
    /// 【公共API】显示主菜单界面
    /// 供其他系统调用（如从游戏返回主菜单）
    /// 注意：现在主菜单可能由多个小零件组成，此方法协调显示
    /// </summary>
    public void ShowMainMenu()
    {
        Debug.Log("<color=cyan>[MainMenuManager]</color> 显示主菜单");

        // 打开主菜单面板
        Open();

        // 确保SavesView已关闭（返回主菜单时清理子面板）
        if (_savesView != null && _savesView.IsOpen)
        {
            _savesView.Close();
        }
    }

    /// <summary>
    /// 【公共API】开始新游戏
    /// 主菜单的"新游戏"按钮可调用此方法
    /// 生成默认存档名，打开SavesView并创建新存档
    /// </summary>
    public void StartNewGame()
    {
        if (_savesView == null)
        {
            Debug.LogError("<color=red>[MainMenuManager]</color> SavesView引用未设置");
            return;
        }

        // 生成默认存档名
        string defaultName = $"新存档_{System.DateTime.Now:yyyyMMdd_HHmmss}";

        Debug.Log($"<color=cyan>[MainMenuManager]</color> 开始新游戏: {defaultName}");

        // 打开存档面板
        _savesView.Open();

        // 创建新存档
        _savesView.RequestNewGame(defaultName);

        // 注意：GameFlowController保持在MainMenu状态，不切换状态
    }

    /// <summary>
    /// 加载游戏按钮点击事件处理
    /// </summary>
    private void OnLoadGameClicked()
    {
        Debug.Log("<color=cyan>[MainMenuManager]</color> 加载游戏按钮被点击");
        OpenSaveSelection();
    }

    /// <summary>
    /// 新游戏按钮点击事件处理
    /// </summary>
    private void OnNewGameClicked()
    {
        Debug.Log("<color=cyan>[MainMenuManager]</color> 新游戏按钮被点击");
        StartNewGame();
    }

    /// <summary>
    /// 设置按钮点击事件处理
    /// </summary>
    private void OnSettingsClicked()
    {
        Debug.Log("<color=yellow>[MainMenuManager]</color> 设置按钮被点击（功能待实现）");
        // TODO: 打开设置面板
    }

    /// <summary>
    /// 退出按钮点击事件处理
    /// </summary>
    private void OnQuitClicked()
    {
        Debug.Log("<color=cyan>[MainMenuManager]</color> 退出按钮被点击");
        QuitGame();
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("<color=yellow>[MainMenuManager]</color> 正在退出游戏...");

#if UNITY_EDITOR
        // 编辑器模式下停止播放
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 构建版本中退出应用程序
        Application.Quit();
#endif
    }

    /// <summary>
    /// IMenuPanel接口实现：打开主菜单面板
    /// </summary>
    public void Open()
    {
        if (_isOpen) return;

        _isOpen = true;
        PanelRoot.SetActive(true);
        Debug.Log("<color=cyan>[MainMenuManager]</color> 主菜单面板已打开");
    }

    /// <summary>
    /// IMenuPanel接口实现：关闭主菜单面板
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        PanelRoot.SetActive(false);

        // 确保SavesView也关闭
        if (_savesView != null && _savesView.IsOpen)
        {
            _savesView.Close();
        }

        Debug.Log("<color=cyan>[MainMenuManager]</color> 主菜单面板已关闭");
    }
}