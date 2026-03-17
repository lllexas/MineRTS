using System;

namespace NekoGraph
{
    /// <summary>
    /// 节点菜单项标签 - 用于在创建菜单中注册节点类型喵~
    /// 将此标签添加到节点类上，即可在右键菜单中显示该节点喵
    /// </summary>
    /// <example>
    /// <code>
    /// [NodeMenuItem("🎮 任务/任务节点", typeof(MissionData))]
    /// public class MissionNode : BaseNode { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeMenuItemAttribute : Attribute
    {
        /// <summary>
        /// 菜单显示路径喵~
        /// 例如："🎮 任务/任务节点 (Mission)"
        /// </summary>
        public string MenuPath;

        /// <summary>
        /// 绑定的数据类型喵~
        /// 例如：typeof(MissionData)
        /// </summary>
        public Type DataType;

        /// <summary>
        /// 构造函数喵~
        /// </summary>
        /// <param name="menuPath">菜单路径喵~</param>
        /// <param name="dataType">数据类型喵~</param>
        public NodeMenuItemAttribute(string menuPath, Type dataType)
        {
            MenuPath = menuPath;
            DataType = dataType;
        }
    }
}
