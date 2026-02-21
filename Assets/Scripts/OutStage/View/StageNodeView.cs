/*using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【View】大地图上的一个据点节点
/// </summary>
public class StageNodeView : MonoBehaviour
{
    public string StageID; // 据点唯一ID，例如 "Level_1_IronBase"

    [Header("UI 组件")]
    public Button EnterButton;
    public GameObject LockIcon;
    public GameObject ClearIcon;
    public Text StageNameText;

    private void Start()
    {
        EnterButton.onClick.AddListener(OnNodeClicked);
        RefreshVisuals();
    }

    /// <summary>
    /// 从 Model 拉取数据来刷新自己的显示
    /// 这就是 MVVM 中的 "Data Binding" (手动版)
    /// </summary>
    public void RefreshVisuals()
    {
        var user = UserModelManager.Instance.CurrentUser;

        // 1. 检查是否解锁 (这里写死逻辑，实际可以根据前置关卡判断)
        bool isUnlocked = true;
        LockIcon.SetActive(!isUnlocked);
        EnterButton.interactable = isUnlocked;

        // 2. 检查是否通关
        bool isCleared = user.StageClearStatus.ContainsKey(StageID) && user.StageClearStatus[StageID];
        ClearIcon.SetActive(isCleared);

        // 3. 显示名字
        StageNameText.text = StageID;
    }

    private void OnNodeClicked()
    {
        Debug.Log($"<color=cyan>[UI]</color> 指挥官选择了据点: {StageID}");

        // 🔥 呼叫中控层，请求切换世界
        GameFlowManager.Instance.RequestEnterStage(StageID);
    }
}*/