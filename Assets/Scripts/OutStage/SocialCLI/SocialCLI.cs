using UnityEngine;
using CatStrategies;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// SocialCLI - 社交终端喵~ (GraphVSF 集成版)
/// ═══════════════════════════════════════════════════════════════
///
/// 继承自 DeveloperConsole，是"开发终端"在社交领域的特化形态喵！
///
/// 职责：
/// 1. 输出：重写 Log() 方法，输出到社交面板而不是主控制台
/// 2. 路径管理：使用 GraphAnalyser 管理文件树路径（CurrentPath）
///
/// 权限控制：
/// - 游戏/调试命令统一要求 subjectLevel >= SystemMin (1000)，由命令本身拒绝
/// - IO 命令（ls/cd/cat/echo/mount）Player 级别可用，VFS 层负责数据访问控制
///
/// GraphVSF 集成：
/// - 使用 GraphAnalyser 单例管理 VFS 实例
/// - CurrentPath 从 GraphAnalyser 查询实时路径
/// - cd 命令通过 GraphAnalyser.GetNode() 验证路径
///
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class SocialCLI : DeveloperConsole
{
    // 策略系统方法已上移至 DeveloperConsole 基类喵~

    // =========================================================
    //  VFS 实例配置
    // =========================================================

    // CurrentPath / SetCurrentPath 已上移至基类喵~

    /// <summary>社交终端首选的 VFS 盘符喵~ 默认 social_tree_default，Inspector 可覆盖</summary>
    [Tooltip("首选 VFS 包 ID（盘符），不存在时自动回退到第一个可用盘")]
    [SerializeField] private string _preferredVFSPackID = "social_tree_default";

    protected override string GetPreferredPackID() => _preferredVFSPackID;


    // =========================================================
    //  重写 Log 方法（输出到社交面板）喵~
    // =========================================================

    /// <summary>
    /// 重写基类的 Log，输出到社交面板而不是主控制台喵~
    /// 注意：不调用 base.Log()，直接发送 SocialCLI 专属事件
    /// </summary>
    public override void Log(string message, Color color)
    {
        Debug.Log(message);
        // 发送 SocialCLI 专属事件，与 DeveloperConsole 隔离（只发送一个事件，避免重复）
        PostSystem.Instance.Send("SocialCLI.Output", new DeveloperConsole.ConsoleOutputEvent { message = message, color = color });
    }

    /// <summary>请求面板将视口滚动到顶部喵~</summary>
    public override void ScrollConsoleToTop()
    {
        PostSystem.Instance.Send("SocialCLI.ScrollToTop", null);
    }

    // ==================== Unity 生命周期 ====================

    protected override void Awake()
    {
        base.Awake(); // 基类负责 PostSystem.Register 和 VFS 兜底初始化喵~
        SetSubjectLevel(PackAccessSubjects.Player); // 社交终端降权到玩家等级喵~
        Debug.Log("[SocialCLI] 终端就绪，正在等待 VFS 系统供电信号喵~");
    }

    // OnDestroy 已上移至基类（负责 PostSystem.Unregister）喵~
    // OnVFSSystemReady 已上移至基类（Subscribe VFS.IO_Ready）喵~
    // VFS 初始化由 SaveManager 在挂盘时统一完成喵~

    // =========================================================
    //  调试方法喵~
    // =========================================================

    /// <summary>
    /// 获取 VFS 调试信息喵~
    /// </summary>
    public string GetVFSDebugInfo()
    {
        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return "GraphAnalyser 实例不存在";

        if (analyser.GetPack(CurrentVFSPackID, PackAccessSubjects.Player) == null) return $"VFS Pack 不存在：{CurrentVFSPackID}";

        return analyser.GetDebugInfo();
    }
}
