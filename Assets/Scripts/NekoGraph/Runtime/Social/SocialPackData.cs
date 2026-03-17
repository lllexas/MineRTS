using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// 社交消息数据包 - 包含一个完整社交对话图的所有内容喵~
    /// 【msgPack】
    /// </summary>
    [Serializable]
    public class MsgPackData : BasePackData
    {
        public MsgPackData() : base()
        {
            DisplayName = "新社交消息";
            Description = "一个由 NekoGraph 驱动的交互式社交对话图喵~";
            Author = "NekoTeam";
            Version = "1.0.0";
        }

        /// <summary>
        /// 验证数据包是否有效喵~
        /// 社交包至少需要一个 RootNode 喵。
        /// </summary>
        public override bool Validate()
        {
            if (Nodes == null || Nodes.Count == 0) return false;
            return true;
        }
    }
}
