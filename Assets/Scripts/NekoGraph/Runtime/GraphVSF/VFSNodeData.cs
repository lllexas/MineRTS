using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using NekoGraph;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSNodeData - VFS 统一节点数据类喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 设计哲学：
/// - 继承 BaseNodeData，复用 NodeID、EditorPosition、OutputConnections、Name
/// - 使用 Name + Extension 区分用途（类似 Linux 文件）
/// - 不定义 Root/Folder/File 枚举，用 Extension 是否为空判断
/// - FullPath 是运行时数据，由 GraphAnalyser 计算，不存储在 JSON 中喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[Serializable]
[NodeType(NodeSystem.VFS)]
public class VFSNodeData : BaseNodeData
{
    // ==================== 连接信息 ====================

    [InPort(0, "父", NekoPortCapacity.Single)]
    public string ParentNodeID;

    [OutPort(0, "子", NekoPortCapacity.Multi)]
    public List<string> ChildNodeIDs = new List<string>();

    // ==================== 基础信息 ====================

    /// <summary>
    /// 扩展名（空=目录，".json"=文件）
    /// 类似 Linux 的设计：目录没有扩展名
    /// </summary>
    [Tooltip("扩展名（空=目录）")]
    [SerializeField, FormerlySerializedAs("Extension")]
    private string _extension = "";

    public string Extension
    {
        get => _extension;
        set => _extension = (!string.IsNullOrEmpty(value) && !value.StartsWith("."))
            ? "." + value
            : value ?? "";
    }

    /// <summary>
    /// 数据内容（JSON 格式）
    /// 目录可为空，文件必须有数据
    /// </summary>
    [Tooltip("数据（JSON 格式）")]
    [TextArea(4, 8)]
    public string DataJson;

    /// <summary>
    /// 是否启用（被禁用的节点在 ls 时会被跳过）
    /// </summary>
    [Tooltip("是否启用")]
    public bool IsEnabled = true;

    /// <summary>
    /// 描述信息
    /// </summary>
    [Tooltip("描述")]
    [TextArea(2, 4)]
    public string Description;

    // ==================== 元数据（可选） ====================

    /// <summary>
    /// MIME 类型（可选，用于更细粒度的用途区分）
    /// 例如："application/json", "text/plain"
    /// </summary>
    [Tooltip("MIME 类型")]
    public string MimeType;

    // ==================== 只读属性 ====================

    /// <summary>
    /// 是否是目录（根据 Extension 计算）
    /// Extension 为空 = 目录
    /// </summary>
    public bool IsDirectory => string.IsNullOrEmpty(Extension);

    /// <summary>
    /// 是否是文件（根据 Extension 计算）
    /// Extension 不为空 = 文件
    /// </summary>
    public bool IsFile => !string.IsNullOrEmpty(Extension);
}
