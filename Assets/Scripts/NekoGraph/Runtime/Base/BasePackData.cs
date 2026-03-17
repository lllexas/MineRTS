using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// BasePackData - 数据包基类喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 所有剧情/任务/VSF 数据包的统一抽象基类
/// 运行时和编辑器共用，不能放在 Editor 目录下喵~
/// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
/// 所有节点统一存入 Nodes 列表，类型信息自动保存在 JSON 中喵~！
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[Serializable]
public abstract class BasePackData
{
    /// <summary>
    /// Pack 唯一 ID（用于代码引用）
    /// </summary>
    [Tooltip("Pack 唯一 ID")]
    public string PackID;

    /// <summary>
    /// 显示名称（用于 UI 展示）
    /// </summary>
    [Tooltip("显示名称")]
    public string DisplayName;

    /// <summary>
    /// 描述信息
    /// </summary>
    [Tooltip("描述")]
    [TextArea(2, 4)]
    public string Description;

    /// <summary>
    /// 作者/创建者
    /// </summary>
    [Tooltip("作者")]
    public string Author;

    /// <summary>
    /// 版本号
    /// </summary>
    [Tooltip("版本号")]
    public string Version = "1.0.0";

    /// <summary>
    /// 创建时间戳
    /// </summary>
    [Tooltip("创建时间")]
    public long CreatedAt;

    /// <summary>
    /// 最后修改时间戳
    /// </summary>
    [Tooltip("最后修改时间")]
    public long ModifiedAt;

    /// <summary>
    /// 所有节点的集合（Newtonsoft.Json + TypeNameHandling.Auto 自动保存类型信息）喵~
    /// </summary>
    [Tooltip("节点列表")]
    public List<BaseNodeData> Nodes = new List<BaseNodeData>();

    protected BasePackData()
    {
        CreatedAt = DateTimeOffset.Now.ToUnixTimeSeconds();
        ModifiedAt = CreatedAt;
    }

    /// <summary>
    /// 更新修改时间戳喵~
    /// </summary>
    public void Touch()
    {
        ModifiedAt = DateTimeOffset.Now.ToUnixTimeSeconds();
    }

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
