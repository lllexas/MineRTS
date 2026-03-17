using UnityEngine;

/// <summary>
/// 社交终端硬件抽象接口 - 只负责 IO 喵~
/// </summary>
public interface ISocialTerminal
{
    /// <summary>
    /// 清空屏幕喵~
    /// </summary>
    void ClearScreen();

    /// <summary>
    /// 在屏幕上输出一行文字喵~
    /// </summary>
    void OutputLine(string message, Color color);

    /// <summary>
    /// 移交输入权限给拦截器喵~
    /// </summary>
    void SetInputInterceptor(IInputInterceptor interceptor);

    /// <summary>
    /// 收回输入权限，恢复正常的 Shell 命令模式喵~
    /// </summary>
    void RestoreCommandMode();
}

/// <summary>
/// 输入拦截器接口 - 即插即用的指令策略喵~
/// </summary>
public interface IInputInterceptor
{
    /// <summary>
    /// 当策略会话开始时（U 盘插上喵）
    /// </summary>
    void OnSessionStart(ISocialTerminal terminal);

    /// <summary>
    /// 处理拦截到的键盘输入喵~
    /// </summary>
    void OnInput(string rawInput);

    /// <summary>
    /// 当策略会话结束时（U 盘拔掉喵）
    /// </summary>
    void OnSessionEnd();
}
