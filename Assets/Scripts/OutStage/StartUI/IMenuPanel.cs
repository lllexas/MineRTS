using UnityEngine;

/// <summary>
/// 菜单面板接口，统一所有面板的开关行为
/// 遵循"小零件"架构设计原则，便于GameFlowController统一管理
/// </summary>
public interface IMenuPanel
{
    /// <summary>
    /// 面板根节点游戏对象
    /// </summary>
    GameObject PanelRoot { get; }

    /// <summary>
    /// 面板是否处于打开状态
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// 打开面板
    /// </summary>
    void Open();

    /// <summary>
    /// 关闭面板
    /// </summary>
    void Close();
}