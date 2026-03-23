using UnityEngine;

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
/// 按键信息结构体 - 打包原始按键状态喵~
/// </summary>
public struct KeyInfo
{
    public KeyCode keyCode;
    public bool isShiftDown;
    public bool isCtrlDown;
    public bool isAltDown;
}

/// <summary>
/// 控制台输入处理器接口。
/// 挂载后，DeveloperConsole 会优先将输入事件导流给处理器。
/// 返回 true 表示该输入已被消费，控制台默认行为不再继续。
/// </summary>
public interface IConsoleInputHandler
{
    /// <summary>
    /// 处理原始按键输入喵~
    /// </summary>
    bool HandleKey(KeyInfo key);

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

/// <summary>
/// 可选的输入行显示策略。
/// 由挂载到控制台的输入处理器决定是否继续显示默认输入提示，
/// 以及是否替换为当前交互态专属的提示文本。
/// </summary>
public interface IConsoleInputLineState
{
    /// <summary>
    /// 是否继续显示底部默认输入行。
    /// 返回 false 时，控制台会把底部空间完全交给输入处理器自己的渲染内容。
    /// </summary>
    bool ShouldRenderInputLine { get; }

    /// <summary>
    /// 返回当前交互态要使用的提示文本。
    /// 返回 null 或空字符串时，沿用控制台自己的默认提示。
    /// </summary>
    string GetInputPrompt(string fallbackPrompt);
}
