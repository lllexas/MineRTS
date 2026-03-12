using System;

/// <summary>
/// 信号上下文 - 在节点之间流动的数据载体喵~
/// Signal 永远踩在【点】上，不在线上的喵~
/// </summary>
[Serializable]
public class SignalContext
{
    /// <summary>
    /// 当前节点 ID（信号正在处理的节点）
    /// </summary>
    public string CurrentNodeId;

    /// <summary>
    /// 信号携带的数据（可以是任何东西）
    /// </summary>
    public object Args;

    public SignalContext(string currentNodeId = null, object args = null)
    {
        CurrentNodeId = currentNodeId;
        Args = args;
    }

    /// <summary>
    /// 创建副本喵~
    /// </summary>
    public SignalContext Clone()
    {
        return new SignalContext(CurrentNodeId, Args);
    }
}
