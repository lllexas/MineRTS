namespace MineRTS.BigMap
{
    /// <summary>
    /// 大地图节点点击事件数据
    /// 通过 PostSystem 事件总线传递
    /// </summary>
    public class NodeClickEvent
    {
        public string StageId;      // 关卡 ID
        public string DisplayName;  // 节点显示名称
    }

    /// <summary>
    /// 大地图事件名称定义
    /// </summary>
    public static class BigMapEvents
    {
        public const string NodeClicked = "BigMap.NodeClicked";
    }
}
