#if UNITY_EDITOR
using NekoGraph;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSGraphView - VFS 画布喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 继承自 BaseGraphView<VFSPackData>
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[GraphViewType(NodeSystem.VFS)]
public class VFSGraphView : BaseGraphView<VFSPackData>
{
    public override NodeSystem CurrentSystem => NodeSystem.VFS;

    /// <summary>
    /// 节点粘贴回调喵~
    /// 粘贴后生成新的 GUID，避免重复喵！
    /// </summary>
    protected override void OnNodePasted(BaseNode node)
    {
        if (node.Data != null)
        {
            node.Data.NodeID = System.Guid.NewGuid().ToString();
            node.SyncGUID(node.Data.NodeID);
        }
    }
}
#endif
