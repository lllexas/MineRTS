using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 存档视图面板 - 对应 StartUI 的 SavesView
/// 职责：实现 IMenuPanel 接口，管理 SaveSlotItem 列表，处理面板级操作，提供请求转发方法
/// 设计原则：作为中间人协调 SaveSlotItem 与 SaveManager 的交互，保持小零件架构
/// </summary>
public class SavesView : MonoBehaviour, IMenuPanel
{
    [Header("面板根节点")]
    [SerializeField] private GameObject _panelRoot;

    [Header("存档列表容器")]
    [SerializeField] private Transform _savesListContainer;

    [Header("存档项预制件")]
    [SerializeField] private GameObject _saveSlotItemPrefab;

    [Header("操作按钮")]
    [SerializeField] private Button _newGameButton;
    [SerializeField] private Button _closeButton;

    [Header("删除确认弹窗")]
    [SerializeField] private GameObject _deleteConfirmPanel;
    [SerializeField] private TMP_Text _deleteConfirmText;
    [SerializeField] private Button _deleteConfirmButton;
    [SerializeField] private Button _deleteCancelButton;

    private string _pendingDeleteSaveName;
    private bool _isOpen = false;

    /// <summary>
    /// IMenuPanel 接口实现：面板根节点
    /// 如果未设置_panelRoot，则返回当前 GameObject
    /// </summary>
    public GameObject PanelRoot => _panelRoot != null ? _panelRoot : gameObject;

    /// <summary>
    /// IMenuPanel 接口实现：面板是否打开
    /// </summary>
    public bool IsOpen => _isOpen;

    private void Awake()
    {
        // 初始化面板根节点引用
        if (_panelRoot == null)
        {
            _panelRoot = gameObject;
            Debug.Log("<color=yellow>[SavesView]</color> PanelRoot 未设置，已自动指向当前 GameObject");
        }

        // 初始化按钮事件
        if (_newGameButton != null)
            _newGameButton.onClick.AddListener(OnNewGameClicked);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(Close);

        // 初始化删除确认弹窗事件
        if (_deleteConfirmButton != null)
            _deleteConfirmButton.onClick.AddListener(OnDeleteConfirmed);

        if (_deleteCancelButton != null)
            _deleteCancelButton.onClick.AddListener(HideDeleteConfirm);

        // 初始隐藏删除确认弹窗
        if (_deleteConfirmPanel != null)
            _deleteConfirmPanel.SetActive(false);
    }

    private void Start()
    {
        // 如果面板初始为激活状态，则标记为打开
        if (PanelRoot.activeSelf)
        {
            _isOpen = true;
            RefreshSavesList();
        }
    }

    /// <summary>
    /// IMenuPanel 接口实现：打开面板
    /// </summary>
    public void Open()
    {
        _isOpen = true;
        PanelRoot.SetActive(true);
        RefreshSavesList();
        Debug.Log("<color=cyan>[SavesView]</color> 存档面板已打开并刷新");
    }

    /// <summary>
    /// IMenuPanel 接口实现：关闭面板
    /// </summary>
    public void Close()
    {
        _isOpen = false;
        PanelRoot.SetActive(false);
        Debug.Log("<color=cyan>[SavesView]</color> 存档面板已关闭");
    }

    /// <summary>
    /// 当面板被激活时调用
    /// </summary>
    private void OnEnable()
    {
        // 当面板被外部激活时（如 GameFlowController），更新状态标志
        _isOpen = true;
    }

    /// <summary>
    /// 当面板被停用时调用
    /// </summary>
    private void OnDisable()
    {
        // 当面板被外部停用时，更新状态标志
        _isOpen = false;
    }

    /// <summary>
    /// 刷新存档列表（从 SaveManager 获取存档列表并创建/更新 SaveSlotItem）
    /// </summary>
    public void RefreshSavesList()
    {
        if (_savesListContainer == null || _saveSlotItemPrefab == null)
        {
            Debug.LogWarning("SavesView: 列表容器或预制件未设置");
            return;
        }

        // 清除现有项
        foreach (Transform child in _savesListContainer)
        {
            Destroy(child.gameObject);
        }

        // 获取存档列表
        List<string> saveFiles = SaveManager.Instance.GetAllSaveFiles();
        if (saveFiles == null || saveFiles.Count == 0)
        {
            Debug.Log("<color=yellow>[SavesView]</color> 未找到任何存档");
            return;
        }

        // 创建存档项
        foreach (string saveName in saveFiles)
        {
            GameObject slotObj = Instantiate(_saveSlotItemPrefab, _savesListContainer);
            SaveSlotItem slotItem = slotObj.GetComponent<SaveSlotItem>();
            if (slotItem != null)
            {
                slotItem.Initialize(saveName, this);
            }
            else
            {
                Debug.LogWarning($"SavesView: 存档项预制件缺少 SaveSlotItem 组件：{saveName}");
            }
        }

        Debug.Log($"<color=cyan>[SavesView]</color> 已刷新存档列表，共 {saveFiles.Count} 个存档");
    }

    /// <summary>
    /// 新建游戏按钮点击事件
    /// </summary>
    private void OnNewGameClicked()
    {
        // 生成默认存档名
        string defaultName = $"存档_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        RequestNewGame(defaultName);
    }

    /// <summary>
    /// 请求新建游戏（公共 API，供外部调用）
    /// </summary>
    public void RequestNewGame(string saveName)
    {
        if (string.IsNullOrEmpty(saveName))
        {
            Debug.LogWarning("SavesView: 存档名称不能为空");
            return;
        }

        Debug.Log($"<color=yellow>[SavesView]</color> 正在创建新存档：{saveName}");
        SaveManager.Instance.CreateNewSave(saveName);

        // 刷新列表并让新存档项进入编辑模式
        RefreshSavesListAndEnterEditMode(saveName);
    }

    /// <summary>
    /// 刷新存档列表并让指定存档项进入编辑模式（用于新建存档后）
    /// </summary>
    /// <param name="newSaveName">新存档名称</param>
    private void RefreshSavesListAndEnterEditMode(string newSaveName)
    {
        if (_savesListContainer == null || _saveSlotItemPrefab == null)
        {
            Debug.LogWarning("SavesView: 列表容器或预制件未设置");
            return;
        }

        // 清除现有项
        foreach (Transform child in _savesListContainer)
        {
            Destroy(child.gameObject);
        }

        // 获取存档列表
        List<string> saveFiles = SaveManager.Instance.GetAllSaveFiles();
        if (saveFiles == null || saveFiles.Count == 0)
        {
            Debug.LogWarning("SavesView: 未找到任何存档");
            return;
        }

        // 创建存档项，并找到新创建的存档项
        SaveSlotItem newItem = null;
        foreach (string saveName in saveFiles)
        {
            GameObject slotObj = Instantiate(_saveSlotItemPrefab, _savesListContainer);
            SaveSlotItem slotItem = slotObj.GetComponent<SaveSlotItem>();
            if (slotItem != null)
            {
                slotItem.Initialize(saveName, this);

                // 找到新创建的存档项
                if (saveName == newSaveName)
                {
                    newItem = slotItem;
                }
            }
            else
            {
                Debug.LogWarning($"SavesView: 存档项预制件缺少 SaveSlotItem 组件：{saveName}");
            }
        }

        // 让新存档项进入编辑模式
        if (newItem != null)
        {
            Debug.Log($"<color=cyan>[SavesView]</color> 新存档已创建，进入重命名模式：{newSaveName}");
            newItem.EnterEditMode();
        }

        Debug.Log($"<color=cyan>[SavesView]</color> 已刷新存档列表，共 {saveFiles.Count} 个存档");
    }

    /// <summary>
    /// 请求加载游戏（供 SaveSlotItem 调用）
    /// </summary>
    public void RequestLoadGame(string slotName)
    {
        if (string.IsNullOrEmpty(slotName))
        {
            Debug.LogWarning("SavesView: 存档名称不能为空");
            return;
        }

        Debug.Log($"<color=yellow>[SavesView]</color> 正在加载存档：{slotName}");
        SaveManager.Instance.LoadSave(slotName);

        // 加载完成后，切换到大地图状态
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.SwitchToState(GameFlowController.GameState.BigMap);
            Close(); // 关闭存档面板
        }
        else
        {
            Debug.LogError("SavesView: GameFlowController 未初始化");
        }
    }

    /// <summary>
    /// 请求删除游戏（供 SaveSlotItem 调用）
    /// </summary>
    public void RequestDeleteGame(string slotName)
    {
        if (string.IsNullOrEmpty(slotName))
        {
            Debug.LogWarning("SavesView: 存档名称不能为空");
            return;
        }

        _pendingDeleteSaveName = slotName;
        ShowDeleteConfirm(slotName);
    }

    /// <summary>
    /// 请求重命名游戏（供 SaveSlotItem 调用）
    /// </summary>
    public void RequestRenameGame(string oldName, string newName)
    {
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("SavesView: 存档名称不能为空");
            return;
        }

        Debug.Log($"<color=yellow>[SavesView]</color> 正在重命名存档：{oldName} -> {newName}");
        bool success = SaveManager.Instance.RenameSave(oldName, newName);

        if (success)
        {
            // 重命名成功后刷新列表
            RefreshSavesList();
        }
        else
        {
            Debug.LogWarning($"SavesView: 重命名存档失败：{oldName} -> {newName}");
        }
    }

    /// <summary>
    /// 显示删除确认弹窗
    /// </summary>
    private void ShowDeleteConfirm(string slotName)
    {
        if (_deleteConfirmPanel == null || _deleteConfirmText == null)
        {
            // 如果没有确认弹窗，直接删除
            OnDeleteConfirmed();
            return;
        }

        _deleteConfirmText.text = $"确定要删除存档 '{slotName}' 吗？\n此操作不可撤销！";
        _deleteConfirmPanel.SetActive(true);
    }

    /// <summary>
    /// 隐藏删除确认弹窗
    /// </summary>
    private void HideDeleteConfirm()
    {
        if (_deleteConfirmPanel != null)
            _deleteConfirmPanel.SetActive(false);

        _pendingDeleteSaveName = null;
    }

    /// <summary>
    /// 确认删除
    /// </summary>
    private void OnDeleteConfirmed()
    {
        if (string.IsNullOrEmpty(_pendingDeleteSaveName))
        {
            Debug.LogWarning("SavesView: 没有待删除的存档");
            return;
        }

        Debug.Log($"<color=red>[SavesView]</color> 正在删除存档：{_pendingDeleteSaveName}");
        SaveManager.Instance.DeleteSave(_pendingDeleteSaveName);

        // 刷新列表
        RefreshSavesList();

        // 隐藏确认弹窗
        HideDeleteConfirm();
    }

    /// <summary>
    /// 快捷方法：通过 GameFlowController 打开面板
    /// </summary>
    public static void OpenPanel()
    {
        // 查找场景中的 SavesView 实例
        SavesView instance = FindObjectOfType<SavesView>();
        if (instance != null)
        {
            instance.Open();
        }
        else
        {
            Debug.LogError("SavesView: 场景中未找到 SavesView 实例");
        }
    }
}
