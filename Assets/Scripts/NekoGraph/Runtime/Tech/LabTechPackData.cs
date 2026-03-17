using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

// =========================================================
// Lab 科技树数据包
// =========================================================
//
// 【架构说明】
//
// LabTechPackData 继承 BasePackData，使用基类的 Nodes 字段存储所有节点喵~
// Newtonsoft.Json + TypeNameHandling.Auto 自动保存类型信息喵~！
//
// =========================================================

/// <summary>
/// 科技树数据包 - 继承 BasePackData 喵~
/// 
/// 包含一个完整科技树包的所有节点数据：
/// - TechNode: 科技节点
/// - TechNode_R: UI 刷新节点
/// - TriggerNode: 触发器节点（解锁条件）
/// - CommandNode: 命令节点（解锁奖励）
/// 
/// 【注】所有节点统一存入 Nodes 列表，类型信息自动保存在 JSON 中喵~！
/// </summary>
[Serializable]
public class LabTechPackData : BasePackData
{
    [Tooltip("科技包名称")]
    public string PackName;

    /// <summary>
    /// 验证数据包是否有效喵~
    /// </summary>
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(PackID))
        {
            Debug.LogWarning("[LabTechPack] PackID 不能为空喵~");
            return false;
        }
        return true;
    }
}
