using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSLoader - VFS 加载器喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 设计哲学：
/// - 专注于数据加载和反序列化
/// - 与 GraphAnalyser 配合：Loader 负责"沉睡数据"，Analyser 负责"觉醒"
///
/// 职责：
/// - 从 Resources 加载 BasePackData（JSON 反序列化）
/// - 从文件加载 BasePackData（存档读取）
/// - 序列化 BasePackData 到 JSON（存档写入）
///
/// 与 GraphAnalyser 的分工：
/// - VFSLoader: 数据加载（沉睡状态）
/// - GraphAnalyser: 拓扑构建、路径注入、索引生成（觉醒状态）
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public static class VFSLoader
{
    /// <summary>
    /// Newtonsoft.Json 序列化设置 - 自动读取类型信息喵~
    /// </summary>
    private static readonly JsonSerializerSettings VfsJsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore
    };

    // =========================================================
    //  从 Resources 加载喵~
    // =========================================================

    /// <summary>
    /// 从 Resources 加载 BasePackData 喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// </summary>
    /// <param name="path">资源路径（相对于 Resources 目录，不含扩展名）</param>
    /// <returns>BasePackData，如果加载失败则返回 null</returns>
    public static BasePackData LoadPackFromResources(string path)
    {
        return LoadPackFromResources<BasePackData>(path);
    }

    /// <summary>
    /// 从 Resources 加载 BasePackData（泛型版本）喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// </summary>
    public static T LoadPackFromResources<T>(string path) where T : BasePackData
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>(path);
        if (jsonAsset == null)
        {
            Debug.LogError($"[VFSLoader] 找不到资源：{path}");
            return null;
        }

        T pack = JsonConvert.DeserializeObject<T>(jsonAsset.text, VfsJsonSettings);
        if (pack == null)
        {
            Debug.LogError($"[VFSLoader] 反序列化失败：{path}");
            return null;
        }

        Debug.Log($"[VFSLoader] 资源加载成功：{path}, 节点总数：{pack.Nodes?.Count ?? 0}");

        return pack;
    }

    // =========================================================
    //  从文件加载喵~（用于存档读取）
    // =========================================================

    /// <summary>
    /// 从文件加载 BasePackData 喵~（用于存档读取）
    /// </summary>
    /// <param name="filePath">文件路径（绝对路径或相对路径）</param>
    /// <returns>BasePackData，如果加载失败则返回 null</returns>
    public static BasePackData LoadPackFromFile(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogError($"[VFSLoader] 文件不存在：{filePath}");
                return null;
            }

            string json = System.IO.File.ReadAllText(filePath);
            BasePackData pack = JsonConvert.DeserializeObject<BasePackData>(json, VfsJsonSettings);

            if (pack == null)
            {
                Debug.LogError($"[VFSLoader] 反序列化失败：{filePath}");
                return null;
            }

            Debug.Log($"[VFSLoader] 文件加载成功：{filePath}, 节点总数：{pack.Nodes?.Count ?? 0}");

            return pack;
        }
        catch (Exception e)
        {
            Debug.LogError($"[VFSLoader] 文件加载失败：{filePath}\n{e}");
            return null;
        }
    }

    // =========================================================
    //  保存到文件喵~（用于存档写入）
    // =========================================================

    /// <summary>
    /// 保存 BasePackData 到文件喵~（用于存档写入）
    /// </summary>
    /// <param name="pack">VFS 数据包</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否保存成功</returns>
    public static bool SavePackToFile(BasePackData pack, string filePath)
    {
        if (pack == null)
        {
            Debug.LogError("[VFSLoader] Pack 为 null，无法保存喵~");
            return false;
        }

        try
        {
            // 确保目录存在
            string directory = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(pack, Formatting.Indented, VfsJsonSettings);
            System.IO.File.WriteAllText(filePath, json);

            Debug.Log($"[VFSLoader] 文件保存成功：{filePath}");

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[VFSLoader] 文件保存失败：{filePath}\n{e}");
            return false;
        }
    }

    // =========================================================
    //  从字符串反序列化喵~（用于网络传输等场景）
    // =========================================================

    /// <summary>
    /// 从 JSON 字符串反序列化 BasePackData 喵~
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>BasePackData</returns>
    public static BasePackData LoadPackFromString(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[VFSLoader] JSON 字符串为空喵~");
            return null;
        }

        try
        {
            BasePackData pack = JsonConvert.DeserializeObject<BasePackData>(json, VfsJsonSettings);

            if (pack == null)
            {
                Debug.LogError("[VFSLoader] 反序列化失败喵~");
                return null;
            }

            Debug.Log($"[VFSLoader] 字符串反序列化成功，节点总数：{pack.Nodes?.Count ?? 0}");

            return pack;
        }
        catch (Exception e)
        {
            Debug.LogError($"[VFSLoader] 反序列化失败：\n{e}");
            return null;
        }
    }

    /// <summary>
    /// 序列化 BasePackData 到 JSON 字符串喵~
    /// </summary>
    /// <param name="pack">VFS 数据包</param>
    /// <returns>JSON 字符串</returns>
    public static string SavePackToString(BasePackData pack)
    {
        if (pack == null)
        {
            Debug.LogError("[VFSLoader] Pack 为 null，无法序列化喵~");
            return null;
        }

        try
        {
            string json = JsonConvert.SerializeObject(pack, Formatting.Indented, VfsJsonSettings);
            Debug.Log($"[VFSLoader] 字符串序列化成功，节点总数：{pack.Nodes?.Count ?? 0}");
            return json;
        }
        catch (Exception e)
        {
            Debug.LogError($"[VFSLoader] 序列化失败：\n{e}");
            return null;
        }
    }

    // =========================================================
    //  辅助方法喵~
    // =========================================================

    /// <summary>
    /// 验证 BasePackData 是否有效喵~
    /// </summary>
    public static bool ValidatePack(BasePackData pack)
    {
        if (pack == null) return false;
        if (string.IsNullOrEmpty(pack.PackID)) return false;
        if (pack.Nodes == null || pack.Nodes.Count == 0) return false;
        if (string.IsNullOrEmpty(pack.RootNodeId)) return false;

        // 验证根节点是否存在
        if (!pack.Nodes.ContainsKey(pack.RootNodeId))
        {
            Debug.LogError($"[VFSLoader] 根节点 {pack.RootNodeId} 不存在于 Nodes 字典中喵~");
            return false;
        }

        return true;
    }
}
