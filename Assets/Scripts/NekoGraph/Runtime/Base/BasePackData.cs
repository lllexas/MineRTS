using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using NekoGraph;

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
[JsonObject(ItemTypeNameHandling = TypeNameHandling.All)]
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
    /// 节点字典：NodeID → BaseNodeData
    /// Newtonsoft.Json + TypeNameHandling.Auto 自动保存类型信息喵~
    /// </summary>
    [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
    [Tooltip("节点字典")]
    public Dictionary<string, BaseNodeData> Nodes = new Dictionary<string, BaseNodeData>();

    /// <summary>
    /// 根节点 ID（唯一，运行时自动构建）
    /// </summary>
    [Tooltip("根节点 ID")]
    public string RootNodeId;

    /// <summary>
    /// Pack 级快捷元数据 - 序列化时由 SideParaRegistry 自动从节点提取喵~
    /// 消费方：先查此字典，miss 时再扫 Nodes 喵~
    /// </summary>
    [Tooltip("Pack 级快捷元数据（自动生成）")]
    public Dictionary<string, string> SidePara = new Dictionary<string, string>();

    /// <summary>
    /// 构建 RootNodeId（运行时调用，序列化后自动填充）喵~
    /// </summary>
    public void BuildRootNodeId()
    {
        RootNodeId = Nodes.Values
            .FirstOrDefault(n => n is RootNodeData)?.NodeID;
    }

    // =========================================================
    // 运行时状态字段 - "血管里的血液"喵~
    // =========================================================
    // 设计理念：
    // 1. 信号应该在 PackData 内部流动，而不是单独存储
    // 2. GraphRunner 和 GraphAnalyser 共用同一份数据
    // 3. GraphAnalyser 模式下这些字段为空也不影响使用
    // =========================================================

    /// <summary>
    /// 活跃信号队列 - 当前正在图中流动的信号喵~
    /// GraphAnalyser 模式下通常为空
    /// </summary>
    [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
    [Tooltip("活跃信号队列（运行时状态）")]
    public Queue<SignalContext> ActiveSignals = new Queue<SignalContext>();

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Objects,
        NullValueHandling = NullValueHandling.Ignore
    };

    protected BasePackData()
    {
        CreatedAt = DateTimeOffset.Now.ToUnixTimeSeconds();
        ModifiedAt = CreatedAt;
    }

    /// <summary>
    /// 序列化为 JSON 字符串喵~
    /// </summary>
    public string ToJson()
    {
        OnBeforeSerialize();
        Touch();
        return JsonConvert.SerializeObject(this, JsonSettings);
    }

    /// <summary>
    /// 从 JSON 字符串反序列化，自动识别具体类型喵~
    /// </summary>
    public static BasePackData FromJson(string json)
    {
        var pack = JsonConvert.DeserializeObject<BasePackData>(json, JsonSettings);
        pack?.OnAfterDeserialize();
        return pack;
    }

    /// <summary>
    /// 序列化前钩子，子类可重写喵~
    /// 默认行为：从节点提取 [SideParaKey] 标记的字段到 SidePara 喵~
    /// </summary>
    protected virtual void OnBeforeSerialize()
    {
        BuildRootNodeId();
        SidePara = SideParaRegistry.Extract(Nodes.Values);
    }

    /// <summary>
    /// 反序列化后钩子，子类可重写，用于整理节点数据为便捷字段喵~
    /// </summary>
    protected virtual void OnAfterDeserialize() { }

    /// <summary>
    /// 更新修改时间戳喵~
    /// </summary>
    public void Touch()
    {
        ModifiedAt = DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    /// <summary>
    /// 返回本 Pack 所属的节点系统喵~（用于 SearchWindow 过滤）
    /// </summary>
    public abstract NodeSystem GetNodeSystem();

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
    public override NodeSystem GetNodeSystem() => NodeSystem.Common;
}
