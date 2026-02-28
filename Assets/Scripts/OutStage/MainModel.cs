using UnityEngine;

/// <summary>
/// 【数据模型】内存中唯一的用户数据持有者
/// 使用 SingletonData<T> 模式，不需要 Unity 生命周期管理
/// 职责：持有当前 UserModel 实例和当前活跃关卡状态
/// </summary>
public class MainModel : SingletonData<MainModel>
{
    // 当前内存中的用户数据
    public UserModel CurrentUser { get; private set; }

    // 当前正在活跃的关卡 ID（用于判断 SaveNow 该存哪一关）
    public string CurrentActiveStageID { get; private set; }

    /// <summary>
    /// 设置当前用户（通常在加载存档或创建新存档时调用）
    /// </summary>
    public void SetCurrentUser(UserModel user)
    {
        CurrentUser = user;
        Debug.Log($"<color=cyan>[MainModel]</color> 当前用户已更新: {user?.Metadata?.PlayerName ?? "null"}");
    }

    /// <summary>
    /// 设置当前活跃关卡（进入关卡时调用）
    /// </summary>
    public void SetCurrentStage(string stageID)
    {
        CurrentActiveStageID = stageID;
        Debug.Log($"<color=yellow>[MainModel]</color> 当前活跃关卡: {stageID}");
    }

    /// <summary>
    /// 清空当前关卡状态（退出关卡时调用）
    /// </summary>
    public void ClearCurrentStage()
    {
        string lastStage = CurrentActiveStageID;
        CurrentActiveStageID = null;
        Debug.Log($"<color=orange>[MainModel]</color> 已清空关卡状态，撤离 {lastStage}");
    }

    /// <summary>
    /// 清空当前用户数据（返回主菜单或切换存档时调用）
    /// </summary>
    public void ClearCurrentUser()
    {
        CurrentUser = null;
        CurrentActiveStageID = null;
        Debug.Log("<color=red>[MainModel]</color> 当前用户数据已清空");
    }

    /// <summary>
    /// 检查是否有用户数据加载
    /// </summary>
    public bool HasUser => CurrentUser != null;

    /// <summary>
    /// 检查是否在关卡中
    /// </summary>
    public bool IsInStage => !string.IsNullOrEmpty(CurrentActiveStageID);
}