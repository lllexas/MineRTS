/// <summary>
/// 控制台导航键类型。
/// </summary>
public enum ConsoleNavKey
{
    Up,
    Down,
    Left,
    Right,
    Home,
    End
}

/// <summary>
/// 控制台输入处理器接口。
/// 挂载后，DeveloperConsole 会优先将输入事件导流给处理器。
/// 返回 true 表示该输入已被消费，控制台默认行为不再继续。
/// </summary>
public interface IConsoleInputHandler
{
    /// <summary>
    /// 处理一条来自控制台的提交输入。
    /// </summary>
    bool HandleSubmit(string input);

    /// <summary>
    /// 处理导航键。
    /// </summary>
    bool HandleNavigation(ConsoleNavKey key);

    /// <summary>
    /// 处理确认键（Enter）。
    /// </summary>
    bool HandleConfirm();

    /// <summary>
    /// 处理取消键（Esc）。
    /// </summary>
    bool HandleCancel();
}
