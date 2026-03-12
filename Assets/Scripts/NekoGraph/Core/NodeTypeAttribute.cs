using System;

namespace NekoGraph
{
    /// <summary>
    /// 节点系统类型枚举喵~
    /// </summary>
    public enum NodeSystem
    {
        Common,     // 公用节点（Story 和 Mission 系统共用）
        Story,      // Story 系统专用
        Mission,    // Mission 系统专用
    }

    /// <summary>
    /// 节点类型标签 Attribute - 标记节点属于哪个系统喵~
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeTypeAttribute : Attribute
    {
        public NodeSystem System { get; }

        public NodeTypeAttribute(NodeSystem system)
        {
            System = system;
        }
    }
}
