using System;
using System.Collections.Generic;
using UnityEngine;

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
// TriggerType 和 TriggerData 已移动到：
// Assets/Scripts/Common/Trigger/TriggerType.cs
// Assets/Scripts/Common/Trigger/TriggerData.cs

// =========================================================
// 命令数据喵~
// =========================================================

/// <summary>
/// 命令数据喵~
/// </summary>
[Serializable]
public class CommandData
{
    public string CommandType;       // 命令类型
    public string CommandParam;      // 命令参数
}

// =========================================================
// GraphView 编辑器专用数据 - 5 种核心节点
// =========================================================

/// <summary>
/// 节点基类喵~
/// </summary>
[Serializable]
public abstract class StoryNodeDataBase
{
    public string NodeID;
    public UnityEngine.Vector2 EditorPosition;
}

/// <summary>
/// 剧情根节点 - 整个剧情树的起始锚点（全图唯一）喵~
/// </summary>
[Serializable]
public class StoryRootNodeData : StoryNodeDataBase
{
    // 根节点没有数据，只是锚点
}

/// <summary>
/// 树 ID 节点 (Spine) - 定义剧情的逻辑骨架（章节/阶段）喵~
/// </summary>
[Serializable]
public class SpineIDNodeData : StoryNodeDataBase
{
    [Tooltip("故事进程 ID（与 LeafIDNode 共享）")]
    public string StoryProcessID;
    
    [Tooltip("下一个 SpineID 节点的 ID 列表（用于恢复一对多连线）喵~")]
    public List<string> NextSpineNodeIDs = new List<string>();
    
    [Tooltip("是否连接到根节点（用于恢复连线）")]
    public bool IsConnectedToRoot;
}

/// <summary>
/// 叶 ID 节点 (Leaf) - 处理具体的对话演出喵~
/// </summary>
[Serializable]
public class LeafIDNodeData : StoryNodeDataBase
{
    [Tooltip("故事进程 ID（与 SpineIDNode 共享）")]
    public string StoryProcessID;
    
    [Tooltip("关联的剧情序列 ID（CSV 中的 SequenceID）")]
    public string SequenceID;
    
    [Tooltip("连接的 Command 节点 ID 列表（用于恢复一对多连线）喵~")]
    public List<string> ConnectedCommandNodeIDs = new List<string>();
}

/// <summary>
/// 触发器节点数据 - 监听游戏事件（串并联逻辑）喵~
/// Mission 和 Story 系统共用
/// </summary>
[Serializable]
public class TriggerNodeData
{
    // 基础字段（所有节点共有）
    [Tooltip("节点唯一 ID")]
    public string NodeID;
    
    [Tooltip("编辑器中的位置")]
    public UnityEngine.Vector2 EditorPosition;
    
    // 触发器数据
    [Tooltip("触发器数据")]
    public TriggerData Trigger = new TriggerData();
    
    // 运行时状态（不序列化，运行时使用）
    [NonSerialized]
    public bool HasTriggered;  // 是否已触发
    
    // 下一个节点 ID 列表（用于恢复一对多连线）喵~
    [Tooltip("下一个节点 ID 列表（用于恢复一对多连线）喵~")]
    public List<string> NextNodeIDs = new List<string>();
    
    // Mission 系统专用字段
    [Tooltip("连向的召唤节点 ID（Mission 系统专用）")]
    public string NextSpawnID;
}

/// <summary>
/// 命令节点 - 执行 RTS 动作（串并联逻辑）喵~
/// </summary>
[Serializable]
public class CommandNodeData : StoryNodeDataBase
{
    public CommandData Command = new CommandData();
    
    [Tooltip("下一个 Command 节点 ID 列表（用于恢复一对多连线）喵~")]
    public List<string> NextCommandNodeIDs = new List<string>();
}

/// <summary>
/// 剧情数据包：包含一个完整剧情章节的所有内容喵~
/// </summary>
[Serializable]
public class StoryPackData
{
    [Tooltip("剧情包 ID")]
    public string StoryID;

    [Tooltip("绑定的关卡 ID（可选，为空则通用）")]
    public string BoundStageID;

    [Tooltip("对话序列列表（CSV 导入生成）")]
    public List<DialogueSequence> Sequences = new List<DialogueSequence>();
    
    // 5 种节点数据
    [Tooltip("剧情根节点（全图唯一）")]
    public StoryRootNodeData Root;

    [Tooltip("树 ID 节点列表（章节/阶段）")]
    public List<SpineIDNodeData> SpineNodes = new List<SpineIDNodeData>();

    [Tooltip("叶 ID 节点列表（演出）")]
    public List<LeafIDNodeData> LeafNodes = new List<LeafIDNodeData>();

    [Tooltip("触发器节点列表")]
    public List<TriggerNodeData> Triggers = new List<TriggerNodeData>();

    [Tooltip("命令节点列表")]
    public List<CommandNodeData> Commands = new List<CommandNodeData>();
}
