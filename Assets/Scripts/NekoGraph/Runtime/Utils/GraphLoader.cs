using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 图加载器工具 - 将 PackData 转换为 RuntimeGraphInstance 喵~
/// 负责反序列化 JSON 并构建运行时图实例
/// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
/// </summary>
public static class GraphLoader
{
    /// <summary>
    /// Newtonsoft.Json 序列化设置 - 自动读取类型信息喵~
    /// </summary>
    private static readonly JsonSerializerSettings GraphJsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>
    /// 获取用于图序列化的 JsonSettings 喵~
    /// </summary>
    public static JsonSerializerSettings GetJsonSettings() => GraphJsonSettings;
    /// <summary>
    /// 从 PackData 加载图实例喵~
    /// </summary>
    public static RuntimeGraphInstance LoadFromPack(MissionPackData pack, string instanceID = null, string graphType = "Mission")
    {
        return LoadFromPackGeneric(pack, instanceID, graphType);
    }

    /// <summary>
    /// 从 PackData 加载图实例（泛型版本）喵~
    /// </summary>
    /// <param name="pack">Pack 数据</param>
    /// <param name="instanceID">实例 ID（可选，自动生成）</param>
    /// <param name="graphType">图类型</param>
    /// <param name="sourceJsonFileName">源 JSON 文件名（用于读档，可选）</param>
    /// <returns>运行时图实例</returns>
    public static RuntimeGraphInstance LoadFromPackGeneric<T>(T pack, string instanceID = null, string graphType = "Generic", string packID = null) where T : BasePackData
    {
        if (pack == null)
        {
            Debug.LogError("[GraphLoader] PackData 为空，无法加载喵~");
            return null;
        }

        // 生成实例 ID
        if (string.IsNullOrEmpty(instanceID))
        {
            instanceID = $"{graphType}_{pack.PackID}_{DateTime.Now.Ticks}";
        }

        // 尝试从 MetaLib 获取 SourceJsonFileName（用于 RuntimeGraphInstance 内部元数据）
        string sourceJsonFileName = null;
        if (!string.IsNullOrEmpty(packID))
        {
            var meta = MetaLib.GetMeta(packID);
            sourceJsonFileName = meta?.ResourcePath;
        }
        else // 如果没有传入 PackID，则尝试从 Pack 中获取
        {
            sourceJsonFileName = pack.PackID; // 兼容旧逻辑，直接将 PackID 作为文件名
        }

        var instance = new RuntimeGraphInstance(instanceID, graphType, sourceJsonFileName)
        {
            PackID = pack.PackID // 存入 PackID
        };

        // 加载所有节点到 NodeMap
        LoadNodesToMapGeneric(pack, instance);

        // 重建连线关系
        RebuildConnectionsGeneric(pack, instance);

        Debug.Log($"[GraphLoader] 图实例加载成功：{instanceID}, 节点数：{instance.NodeMap.Count}");

        return instance;
    }

    /// <summary>
    /// 加载所有节点到 NodeMap 喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// 所有节点已从 Nodes 列表自动恢复类型，直接提取即可喵~！
    /// </summary>
    private static void LoadNodesToMap(MissionPackData pack, RuntimeGraphInstance instance)
    {
        LoadNodesToMapGeneric(pack, instance);
    }

    /// <summary>
    /// 加载所有节点到 NodeMap（泛型版本）喵~
    /// </summary>
    private static void LoadNodesToMapGeneric<T>(T pack, RuntimeGraphInstance instance) where T : BasePackData
    {
        // 直接从 Nodes 列表加载所有节点喵~
        if (pack.Nodes != null)
        {
            foreach (var node in pack.Nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.NodeID))
                {
                    instance.NodeMap[node.NodeID] = node;
                }
            }
        }
    }

    /// <summary>
    /// 重建节点之间的连线关系喵~
    /// 确保 OutputConnections 和目标节点的 InputConnections 正确关联
    /// </summary>
    private static void RebuildConnections(MissionPackData pack, RuntimeGraphInstance instance)
    {
        RebuildConnectionsGeneric(pack, instance);
    }

    /// <summary>
    /// 重建节点之间的连线关系（泛型版本）喵~
    /// </summary>
    private static void RebuildConnectionsGeneric<T>(T pack, RuntimeGraphInstance instance) where T : BasePackData
    {
        // 遍历所有节点，重建连线
        foreach (var node in instance.NodeMap.Values)
        {
            // 处理 OutputConnections
            foreach (var conn in node.OutputConnections)
            {
                // 验证目标节点是否存在
                if (!instance.NodeMap.ContainsKey(conn.TargetNodeID))
                {
                    Debug.LogWarning($"[GraphLoader] 节点 {node.NodeID} 的连线目标 {conn.TargetNodeID} 不存在喵~");
                }
            }

            // 处理特殊节点的连线恢复
            RestoreSpineConnections(node, instance);
        }
    }

    /// <summary>
    /// 恢复 Spine 节点的一对多连线喵~
    /// </summary>
    private static void RestoreSpineConnections(BaseNodeData node, RuntimeGraphInstance instance)
    {
        if (node is SpineNodeData spineNode)
        {
            // 将 NextSpineNodeIDs 转换为 OutputConnections
            if (spineNode.NextSpineNodeIDs != null)
            {
                foreach (var nextId in spineNode.NextSpineNodeIDs)
                {
                    if (!spineNode.OutputConnections.Any(c => c.TargetNodeID == nextId))
                    {
                        spineNode.OutputConnections.Add(new ConnectionData(0, nextId, 0));
                    }
                }
            }
        }
    }

    /// <summary>
    /// 从 Resources 加载 PackData 喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// </summary>
    public static MissionPackData LoadPackFromResources(string path)
    {
        return LoadPackFromResources<MissionPackData>(path);
    }

    /// <summary>
    /// 从 Resources 加载 PackData（泛型版本）喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// </summary>
    public static T LoadPackFromResources<T>(string path) where T : BasePackData
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>(path);
        if (jsonAsset == null)
        {
            Debug.LogError($"[GraphLoader] 找不到资源：{path}");
            return null;
        }

        T pack = JsonConvert.DeserializeObject<T>(jsonAsset.text, GraphJsonSettings);
        if (pack == null)
        {
            Debug.LogError($"[GraphLoader] 反序列化失败：{path}");
            return null;
        }

        Debug.Log($"[GraphLoader] 资源加载成功：{path}, 节点总数：{pack.Nodes?.Count ?? 0}");

        return pack;
    }

    /// <summary>
    /// 卸载图实例喵~
    /// </summary>
    public static void UnloadInstance(RuntimeGraphInstance instance)
    {
        if (instance != null)
        {
            instance.IsRunning = false;
            instance.ClearSignals();
            instance.NodeMap.Clear();
            instance.PoweredTriggerIds.Clear();
        }
    }
}
