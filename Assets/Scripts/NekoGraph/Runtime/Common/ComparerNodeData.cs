using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

/// <summary>
/// 比较器节点数据喵~
/// 职责：持有所选比较器的名称及其参数。
/// </summary>
[Serializable]
public class ComparerNodeData : BaseNodeData
{
    [Tooltip("比较器逻辑名（对应 ComparerRegistry 中的 Name）喵~")]
    public string ComparerName = "";

    [Tooltip("比较参数列表喵~")]
    public List<string> Parameters = new List<string>();

    [InPort(0, "输入", NekoPortCapacity.Multi)]
    public List<string> InputNodeIDs = new List<string>();

    [OutPort(0, "通过 (Pass)", NekoPortCapacity.Multi)]
    public List<string> PassOutputs = new List<string>();

    [OutPort(1, "失败 (Fail)", NekoPortCapacity.Multi)]
    public List<string> FailOutputs = new List<string>();

    public new void CopyFrom(BaseNodeData other)
    {
        base.CopyFrom(other);
        if (other is ComparerNodeData comparerOther)
        {
            ComparerName = comparerOther.ComparerName;
            Parameters = new List<string>(comparerOther.Parameters);
            InputNodeIDs = new List<string>(comparerOther.InputNodeIDs);
            PassOutputs = new List<string>(comparerOther.PassOutputs);
            FailOutputs = new List<string>(comparerOther.FailOutputs);
        }
    }
}
