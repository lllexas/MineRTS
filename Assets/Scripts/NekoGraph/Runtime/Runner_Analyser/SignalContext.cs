using System;
using System.Collections.Generic;

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

    /// <summary>
    /// 信号已走过的路径（分叉时新建列表，独立记录）喵~
    /// </summary>
    public List<ConnectionData> TraveledPath;

    /// <summary>
    /// 信号传播深度（防止死循环）喵~
    /// 每次克隆/传播时 +1，超过 MaxSignalDepth 会被强制丢弃
    /// </summary>
    public int Depth;

    /// <summary>
    /// 构造信号上下文喵~
    /// </summary>
    public SignalContext(string currentNodeId = null, object args = null, List<ConnectionData> traveledPath = null, int depth = 0)
    {
        CurrentNodeId = currentNodeId;
        Args = args;
        TraveledPath = traveledPath ?? new List<ConnectionData>();
        Depth = depth;
    }

    /// <summary>
    /// 创建副本喵~
    /// 注意：分叉时需要新建路径列表，所以提供 copyPath 参数控制是否复制路径
    /// 深度会 +1（表示传播了一层）喵~
    /// </summary>
    public SignalContext Clone(bool copyPath = false)
    {
        var newPath = copyPath && TraveledPath != null
            ? new List<ConnectionData>(TraveledPath)
            : TraveledPath;
        return new SignalContext(CurrentNodeId, Args, newPath, Depth + 1);
    }

    /// <summary>
    /// 记录信号经过的连线喵~
    /// </summary>
    public void RecordConnection(ConnectionData conn)
    {
        if (TraveledPath == null)
        {
            TraveledPath = new List<ConnectionData>();
        }
        TraveledPath.Add(conn);
    }
}
