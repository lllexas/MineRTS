using System;

/// <summary>
/// NekoGraph 运行时类型定义 - 不依赖任何 Unity Editor 命名空间喵~
/// 【跨平台安全·运行时纯净版】
/// </summary>

namespace NekoGraph
{
    /// <summary>
    /// 端口容量类型 - 运行时安全版本喵~
    /// 用于替代 UnityEditor.Experimental.GraphView.Port.Capacity 喵！
    /// </summary>
    public enum NekoPortCapacity
    {
        /// <summary>
        /// 单连接 - 一个端口只能连一条线喵~
        /// </summary>
        Single,

        /// <summary>
        /// 多连接 - 一个端口可以连多条线喵~
        /// </summary>
        Multi
    }
}
