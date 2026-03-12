using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 数据包基类 - 所有剧情/任务数据包的抽象基类喵~
/// 运行时和编辑器共用，不能放在 Editor 目录下喵~
/// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
/// 所有节点统一存入 Nodes 列表，类型信息自动保存在 JSON 中喵~！
/// </summary>
[Serializable]
public abstract class BasePackData
{
    [Tooltip("数据包 ID")]
    public string PackID;

    [Tooltip("所有节点的集合（Newtonsoft.Json + TypeNameHandling.Auto 自动保存类型信息）喵~")]
    public List<BaseNodeData> Nodes = new List<BaseNodeData>();

    /// <summary>
    /// 验证数据包是否有效喵~
    /// </summary>
    public virtual bool Validate() => true;
}

/// <summary>
/// 泛型数据包基类 - 保留用于向后兼容喵~
/// </summary>
[Serializable]
public abstract class BasePackData<T> : BasePackData where T : BaseNodeData
{
}
