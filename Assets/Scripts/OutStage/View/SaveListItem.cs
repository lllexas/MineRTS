using UnityEngine;

using UnityEngine.UI;

/// <summary>
/// 存档列表项组件（增强版，支持重命名功能）
/// </summary>
public class SaveListItem : MonoBehaviour
{
    [Header("显示控件")]
    [SerializeField] private Text SaveNameText;
    [SerializeField] private Text SaveDateText;
    [SerializeField] private Text PlayTimeText;

    [Header("操作按钮")]
    [SerializeField] private Button SelectButton;
    [SerializeField] private Button DeleteButton;
    [SerializeField] private Button RenameButton;

    [Header("重命名控件")]
    [SerializeField] private GameObject RenamePanel;
    [SerializeField] private InputField RenameInputField;
    [SerializeField] private Button ConfirmRenameButton;
    [SerializeField] private Button CancelRenameButton;

    private string _saveName;
    private System.Action<string> _onSelect;
    private System.Action<string> _onDelete;
    private System.Action<string, string> _onRename; // oldName, newName

    /// <summary>
    /// 初始化存档项（兼容旧版）
    /// </summary>
    public void Initialize(string saveName, System.Action<string> onSelect, System.Action<string> onDelete)
    {
        Initialize(saveName, onSelect, onDelete, null);
    }

    /// <summary>
    /// 初始化存档项（增强版，支持重命名）
    /// </summary>
    public void Initialize(string saveName, System.Action<string> onSelect, System.Action<string> onDelete, System.Action<string, string> onRename)
    {
        _saveName = saveName;
        _onSelect = onSelect;
        _onDelete = onDelete;
        _onRename = onRename;

        // 设置基础信息
        UpdateDisplayInfo();

        // 设置按钮事件
        if (SelectButton != null)
            SelectButton.onClick.AddListener(OnSelectClicked);

        if (DeleteButton != null)
            DeleteButton.onClick.AddListener(OnDeleteClicked);

        if (RenameButton != null)
            RenameButton.onClick.AddListener(EnterRenameMode);

        if (ConfirmRenameButton != null)
            ConfirmRenameButton.onClick.AddListener(ConfirmRename);

        if (CancelRenameButton != null)
            CancelRenameButton.onClick.AddListener(CancelRename);

        // 初始状态：显示模式
        ExitRenameMode();
    }

    /// <summary>
    /// 更新显示信息（存档名、时间等）
    /// </summary>
    private void UpdateDisplayInfo()
    {
        if (SaveNameText != null)
            SaveNameText.text = _saveName;

        // TODO: 从存档文件读取更多元数据（保存时间、游戏时长等）
        // 暂时显示占位符
        if (SaveDateText != null)
            SaveDateText.text = "最近保存: 未知";
        if (PlayTimeText != null)
            PlayTimeText.text = "游戏时长: 未知";
    }

    /// <summary>
    /// 进入重命名模式
    /// </summary>
    private void EnterRenameMode()
    {
        if (RenamePanel != null)
            RenamePanel.SetActive(true);

        if (SaveNameText != null)
            SaveNameText.gameObject.SetActive(false);

        if (RenameInputField != null)
        {
            RenameInputField.text = _saveName;
            RenameInputField.Select();
            RenameInputField.ActivateInputField();
        }
    }

    /// <summary>
    /// 退出重命名模式
    /// </summary>
    private void ExitRenameMode()
    {
        if (RenamePanel != null)
            RenamePanel.SetActive(false);

        if (SaveNameText != null)
            SaveNameText.gameObject.SetActive(true);
    }

    /// <summary>
    /// 确认重命名
    /// </summary>
    private void ConfirmRename()
    {
        if (RenameInputField == null || _onRename == null) return;

        string newName = RenameInputField.text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("存档名称不能为空");
            return;
        }

        if (newName == _saveName)
        {
            ExitRenameMode();
            return;
        }

        // 调用重命名回调
        _onRename?.Invoke(_saveName, newName);

        // 注意：重命名成功后，SaveManager会刷新列表，当前项会被销毁并重新创建
        // 所以这里不需要手动更新_saveName
        ExitRenameMode();
    }

    /// <summary>
    /// 取消重命名
    /// </summary>
    private void CancelRename()
    {
        ExitRenameMode();
    }

    /// <summary>
    /// 选择存档
    /// </summary>
    private void OnSelectClicked()
    {
        _onSelect?.Invoke(_saveName);
    }

    /// <summary>
    /// 删除存档
    /// </summary>
    private void OnDeleteClicked()
    {
        _onDelete?.Invoke(_saveName);
    }

    /// <summary>
    /// 获取当前存档名
    /// </summary>
    public string GetCurrentName()
    {
        return _saveName;
    }

    /// <summary>
    /// 手动进入编辑模式（供外部调用，例如创建新存档后自动进入重命名）
    /// </summary>
    public void EnterEditMode()
    {
        EnterRenameMode();
    }

    private void OnDestroy()
    {
        // 清理事件监听
        if (SelectButton != null)
            SelectButton.onClick.RemoveAllListeners();
        if (DeleteButton != null)
            DeleteButton.onClick.RemoveAllListeners();
        if (RenameButton != null)
            RenameButton.onClick.RemoveAllListeners();
        if (ConfirmRenameButton != null)
            ConfirmRenameButton.onClick.RemoveAllListeners();
        if (CancelRenameButton != null)
            CancelRenameButton.onClick.RemoveAllListeners();
    }
}