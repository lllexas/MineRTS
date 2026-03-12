using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NekoGraph;

// =========================================================
// 基础数据结构（运行时 + 编辑器通用）
// =========================================================

/// <summary>
/// 单句台词数据结构喵~
/// </summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("说话人名字")]
    public string Speaker;

    [Tooltip("台词内容")]
    public string Text;

    [Tooltip("显示时长（秒），0=自动根据字数计算")]
    public float DisplayTime;

    [Tooltip("说话人头像 Sprite ID（可选）")]
    public int PortraitSpriteId;
}

/// <summary>
/// 一段完整的对话序列（一场戏/一个片段）喵~
/// </summary>
[Serializable]
public class DialogueSequence
{
    [Tooltip("对话序列 ID")]
    public string SequenceID;

    [Tooltip("对话标题/名称")]
    public string Title;

    [Tooltip("台词列表")]
    public List<DialogueLine> Lines = new List<DialogueLine>();

    [Tooltip("播放完成后触发的事件 ID（可选，用于连锁播放）")]
    public string OnCompleteEvent;
}

/// <summary>
/// CSV 导入用的临时数据结构喵~
/// </summary>
[Serializable]
public class DialogueLineCSV
{
    public string SequenceID;
    public string Title;
    public int LineIndex;
    public string Speaker;
    public string Text;
    public float DisplayTime;
    public int PortraitId;
}

// =========================================================
// 引用公共触发器数据结构（Common/Trigger/）
// =========================================================
// TriggerData 定义在：
// Assets/Scripts/Common/Trigger/TriggerData.cs
// 触发器类型注册表在 TriggerRegistry.cs 中统一管理喵~

// =========================================================
// 命令数据喵~
// =========================================================

/// <summary>
/// 命令数据喵~
/// 统一结构：命令名 + 参数列表
/// </summary>
[Serializable]
public class CommandData
{
    [Tooltip("命令名（对应 CommandRegistryInfo 中的注册名）")]
    public string CommandName = "";  // 如 "spawn", "PlayCG"

    [Tooltip("命令主参数（快捷访问第一个参数）")]
    public string Parameter = "";  // 快捷访问 Parameters[0]

    [Tooltip("命令参数列表，数量和含义由 CommandName 决定")]
    public List<string> Parameters = new List<string>();  // 灵活支持多个参数

    /// <summary>
    /// 获取参数值（安全访问）喵~
    /// </summary>
    public string GetParam(int index, string defaultValue = "")
    {
        if (Parameters == null || index < 0 || index >= Parameters.Count)
            return defaultValue;
        return Parameters[index] ?? defaultValue;
    }

    /// <summary>
    /// 设置参数值（自动扩展列表）喵~
    /// </summary>
    public void SetParam(int index, string value)
    {
        if (Parameters == null)
            Parameters = new List<string>();

        while (Parameters.Count <= index)
            Parameters.Add("");

        Parameters[index] = value;
    }

    /// <summary>
    /// 同步 Parameter 和 Parameters[0] 喵~
    /// </summary>
    public void SyncParameters()
    {
        if (Parameters == null) Parameters = new List<string>();
        if (Parameters.Count == 0) Parameters.Add("");
        Parameters[0] = Parameter;
    }
}

// =========================================================
// Story 系统专用节点数据
// 使用 Common 中的通用流程节点喵~
// =========================================================

/// <summary>
/// 剧情数据包：包含一个完整剧情章节的所有内容喵~
/// </summary>
[Serializable]
public class StoryPackData : BasePackData
{
    [Tooltip("剧情包 ID（已废弃，使用 PackID）")]
    public string StoryID;

    [Tooltip("对话序列列表（CSV 导入生成）")]
    public List<DialogueSequence> Sequences = new List<DialogueSequence>();

    // 使用通用流程节点（已移动到 Common/ProcessFlowNodeData.cs）
    [Tooltip("剧情根节点（全图唯一）")]
    public RootNodeData Root;

    [Tooltip("树 ID 节点列表（章节/阶段）")]
    public List<SpineNodeData> SpineNodes = new List<SpineNodeData>();

    [Tooltip("叶 ID 节点列表（演出）")]
    public List<LeafNode_A_Data> LeafNodes = new List<LeafNode_A_Data>();

    [Tooltip("触发器节点列表")]
    public List<TriggerNodeData> Triggers = new List<TriggerNodeData>();

    [Tooltip("命令节点列表")]
    public List<CommandNodeData> Commands = new List<CommandNodeData>();
}
