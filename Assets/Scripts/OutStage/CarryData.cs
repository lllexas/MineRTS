using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineRTS.OutStage
{
    /// <summary>
    /// 背包数据类型：
    /// 存储玩家当前携带到局内的实体id列表
    /// 对应 VFS 节点扩展名为 .carry
    /// </summary>
    [Serializable]
    public class CarryData
    {
        /// <summary>
        /// 携带到局内的实体 id 列表
        /// </summary>
        public List<int> CarriedEntityIds = new List<int>();

        /// <summary>
        /// 最后更新时间戳
        /// </summary>
        public long UpdatedAt;

        /// <summary>
        /// 最后修改原因
        /// </summary>
        public string LastReason;
    }
}
