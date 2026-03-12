using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

/// <summary>
/// 命令节点数据 - Mission 和 Story 系统共用喵~
/// 【跨平台安全·运行时纯净版】
/// </summary>
[Serializable]
public class CommandNodeData : BaseNodeData
{
    public CommandData Command = new CommandData();

    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [OutPort(0, "输出", NekoPortCapacity.Multi)]
    public List<string> OutputNodeIDs = new List<string>();
}
