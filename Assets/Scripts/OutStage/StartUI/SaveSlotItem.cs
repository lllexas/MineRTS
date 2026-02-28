using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 存档槽位项组件 - 对应StartUI的SaveSlotItem
/// 职责：显示单个存档项，提供加载/删除/重命名按钮，支持显示/编辑双模式切换
/// 设计原则：通过委托将操作请求转发给SavesView，保持小零件架构的松散耦合
/// </summary>
public class SaveSlotItem : MonoBehaviour
{
    [Header("显示控件")]
    [SerializeField] private TMP_Text SaveNameText;
    [SerializeField] private TMP_Text SaveDateText;
    [SerializeField] private TMP_Text PlayTimeText;

    [Header("操作按钮")]
    [SerializeField] private Button LoadButton;
    [SerializeField] private Button DeleteButton;
    [SerializeField] private Button RenameButton;

    [Header("重命名控件")]
    [SerializeField] private GameObject RenamePanel;
    [SerializeField] private TMP_InputField RenameInputField;

    private string _saveName;
    private SavesView _ownerView;
    private bool _isCancelling = false;

    /// <summary>
    /// 初始化存档项
    /// </summary>
    /// <param name="saveName">存档名称</param>
    /// <param name="owner">所属的SavesView实例，用于委托操作</param>
    public void Initialize(string saveName, SavesView owner)
    {
        _saveName = saveName;
        _ownerView = owner;

        // 设置基础信息
        UpdateDisplayInfo();

        // 设置按钮事件
        if (LoadButton != null)
            LoadButton.onClick.AddListener(OnLoadClicked);

        if (DeleteButton != null)
            DeleteButton.onClick.AddListener(OnDeleteClicked);

        if (RenameButton != null)
            RenameButton.onClick.AddListener(EnterEditMode);

        // 设置输入框失去焦点自动保存
        if (RenameInputField != null)
        {
            RenameInputField.onEndEdit.AddListener(OnRenameInputEndEdit);
        }

        // 初始状态：显示模式
        EnterDisplayMode();
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
    /// 进入编辑模式（重命名）
    /// </summary>
    public void EnterEditMode()
    {
        _isCancelling = false;

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
    /// 进入显示模式
    /// </summary>
    public void EnterDisplayMode()
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
        if (RenameInputField == null || _ownerView == null) return;

        string newName = RenameInputField.text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("存档名称不能为空");
            return;
        }

        if (newName == _saveName)
        {
            EnterDisplayMode();
            return;
        }

        // 通过委托请求SavesView处理重命名
        _ownerView.RequestRenameGame(_saveName, newName);

        // 注意：重命名成功后，SavesView会刷新列表，当前项会被销毁并重新创建
        // 所以这里不需要手动更新_saveName
        EnterDisplayMode();
    }

    /// <summary>
    /// 取消重命名
    /// </summary>
    private void CancelRename()
    {
        _isCancelling = true;
        EnterDisplayMode();
        _isCancelling = false;
    }

    /// <summary>
    /// 输入框结束编辑事件处理（失去焦点或按下回车）
    /// </summary>
    private void OnRenameInputEndEdit(string text)
    {
        // 如果正在取消编辑，则忽略失去焦点事件
        if (_isCancelling) return;

        // 触发确认重命名逻辑
        ConfirmRename();
    }

    /// <summary>
    /// 加载存档
    /// </summary>
    private void OnLoadClicked()
    {
        _ownerView?.RequestLoadGame(_saveName);
    }

    /// <summary>
    /// 删除存档
    /// </summary>
    private void OnDeleteClicked()
    {
        _ownerView?.RequestDeleteGame(_saveName);
    }

    /// <summary>
    /// 获取当前存档名
    /// </summary>
    public string GetCurrentName()
    {
        return _saveName;
    }

    private void OnDestroy()
    {
        // 清理事件监听
        if (LoadButton != null)
            LoadButton.onClick.RemoveAllListeners();
        if (DeleteButton != null)
            DeleteButton.onClick.RemoveAllListeners();
        if (RenameButton != null)
            RenameButton.onClick.RemoveAllListeners();
        if (RenameInputField != null)
            RenameInputField.onEndEdit.RemoveListener(OnRenameInputEndEdit);
    }
}