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
/// GraphView 画布逻辑 - 社交消息系统专用喵~
/// 【msgPack】专用画布
/// </summary>
[GraphViewType(NodeSystem.Social)]
public class MsgGraphView : BaseGraphView<MsgPackData>
{
    public override NodeSystem CurrentSystem => NodeSystem.Social;

    /// <summary>
    /// 获取根节点（从 NodeMap 中查找）喵~
    /// </summary>
    public RootNode RootNode => NodeMap.Values.OfType<RootNode>().FirstOrDefault();

    public override MsgPackData SerializeToPack()
    {
        return base.SerializeToPack();
    }

    public override void PopulateFromPack(MsgPackData pack)
    {
        base.PopulateFromPack(pack);
    }
}
#endif
