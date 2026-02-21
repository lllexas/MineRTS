/*using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【Controller】全局流程控制器。
/// 只有它有权力指挥 EntitySystem 初始化或销毁。
/// </summary>
public class GameFlowManager : SingletonMono<GameFlowManager>
{
    // 状态机：当前是在大地图，还是在关卡里？
    public enum GameState { BigMap, InStage }
    public GameState CurrentState = GameState.BigMap;

    [Header("UI 容器引用")]
    public GameObject BigMapPanel; // 大地图 UI 根节点
    public GameObject StageHUDPanel; // 战斗界面 UI 根节点

    /// <summary>
    /// 1. 响应 View 层的请求：进入据点
    /// </summary>
    public void RequestEnterStage(string stageID)
    {
        if (CurrentState == GameState.InStage) return;

        Debug.Log($"<color=orange>[Flow]</color> 正在前往据点 {stageID}...");

        // A. 切换数据
        // 调用我们之前写的 UserModelManager 逻辑
        UserModelManager.Instance.LoadStageIntoSystem(stageID);

        // B. 切换表现
        CurrentState = GameState.InStage;
        ToggleUI(false); // 关掉大地图，打开 HUD

        // C. (可选) 如果你用 Unity Scene 管理，这里可以 LoadScene("GameScene")
        // 但既然是纯 ECS 数据驱动，我们甚至不需要切 Scene，直接重置摄像机位置就行！
        Camera.main.transform.position = new Vector3(0, 0, -10); // 归位
    }

    /// <summary>
    /// 2. 响应 View 层的请求：返回大地图
    /// </summary>
    public void RequestReturnToMap()
    {
        if (CurrentState == GameState.BigMap) return;

        Debug.Log($"<color=orange>[Flow]</color> 正在撤离据点...");

        // A. 保存当前战况
        // 把 EntitySystem 里的热数据写回 UserModel
        UserModelManager.Instance.SaveCurrentStageFromSystem();

        // B. 持久化到磁盘 (可选，防止崩溃丢档)
        UserModelManager.Instance.SaveToDisk();

        // C. 切换表现
        CurrentState = GameState.BigMap;

        // D. 清理战场 (重要！防止上次的兵还在)
        // 我们需要给 EntitySystem 加一个 Reset 接口
        EntitySystem.Instance.ClearWorld();

        ToggleUI(true); // 打开大地图
    }

    private void ToggleUI(bool showMap)
    {
        if (BigMapPanel) BigMapPanel.SetActive(showMap);
        if (StageHUDPanel) StageHUDPanel.SetActive(!showMap);
    }
}*/