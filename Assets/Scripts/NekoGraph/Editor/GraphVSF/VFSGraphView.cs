#if UNITY_EDITOR
using System.Linq;
using NekoGraph;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSGraphView - VFS 画布喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 继承自 BaseGraphView<VFSPackData>
/// 重写 OnAfterSerialize 确保 RootNodeIds 自动填充喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[GraphViewType(NodeSystem.VFS)]
public class VFSGraphView : BaseGraphView<VFSPackData>
{
    public override NodeSystem CurrentSystem => NodeSystem.VFS;

    /// <summary>
    /// 序列化后处理喵~
    /// 重写此方法确保 RootNodeIds 自动填充喵！
    /// 这样无论从哪里调用保存，返回的数据包都是完整的喵~
    /// </summary>
    protected override void OnAfterSerialize(VFSPackData pack)
    {
        base.OnAfterSerialize(pack);

        // 顺手牵羊：在 Nodes 里把 RootNode 找出来，填进列表喵！
        if (pack.Nodes != null)
        {
            pack.RootNodeIds = pack.Nodes
                .Where(n => n is RootNodeData)
                .Select(n => n.NodeID)
                .ToList();
        }
    }

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
