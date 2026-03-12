#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// GraphView 画布逻辑 - Story 系统专用喵~
/// 【彻底拥抱混沌·主语驱动重构版】
/// 删除所有死板的端口校验，把自由还给开发者喵！
/// </summary>
[GraphViewType(NodeSystem.Story)]
public class StoryGraphView : BaseGraphView<StoryPackData>
{
    // CSV 导入后暂存的 Sequences 数据喵~
    public List<DialogueSequence> Sequences = new List<DialogueSequence>();

    /// <summary>
    /// 获取根节点（从 NodeMap 中查找）喵~
    /// </summary>
    public RootNode RootNode => NodeMap.Values.OfType<RootNode>().FirstOrDefault();

    /// <summary>
    /// 序列化到数据包喵~
    /// 基类反射自动填充所有节点列表，只需处理 Sequences 喵~
    /// </summary>
    public override StoryPackData SerializeToPack()
    {
        // 调用基类的自动序列化喵~
        var pack = base.SerializeToPack();

        // 保存 Sequences 数据喵~
        pack.Sequences = Sequences;

        return pack;
    }

    /// <summary>
    /// 从数据包填充画布喵~
    /// 基类反射自动填充所有节点，只需恢复 Sequences 数据喵~
    /// </summary>
    public override void PopulateFromPack(StoryPackData pack)
    {
        // 调用基类的自动反序列化喵~
        base.PopulateFromPack(pack);

        // 恢复 Sequences 数据喵~
        if (pack.Sequences != null)
        {
            Sequences = pack.Sequences;
        }
    }
}
#endif
