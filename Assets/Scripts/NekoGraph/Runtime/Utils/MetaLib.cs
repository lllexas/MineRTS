using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// MetaLib - NekoGraph 智能仓库喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 职责：
/// 1. 维护 PackID -> MetaEntry 的全局注册表
/// 2. 【核心】作为资产供应者，根据 PackID 提供加载好的 PackData 对象
/// 3. 抽象不同存储位置（Resources, StreamingAssets等）的加载细节
///
/// </summary>
public static class MetaLib
{
    public enum StorageType { Resources, StreamingAssets }

    private static Dictionary<string, MetaEntry> _metadata;
    private static bool _isInitialized = false;
    private const string METALIB_PATH = "NekoGraph/MetaLib";

    [Serializable]
    public class MetaEntry
    {
        public string PackID;
        public StorageType Storage;
        public string ResourcePath; // 根据 StorageType，这里可能是 Resources 路径或 StreamingAssets 的相对路径
        public string GraphType;
        public string DisplayName;
        public string Author;
        public string Version = "1.0.0";
        public string Description;
        public Dictionary<string, string> CustomFields = new Dictionary<string, string>();
    }

    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        if (_isInitialized) return;
        LoadFromResources();
        _isInitialized = true;
    }

    private static void LoadFromResources()
    {
        var jsonAsset = Resources.Load<TextAsset>(METALIB_PATH);
        if (jsonAsset == null)
        {
            _metadata = new Dictionary<string, MetaEntry>();
            return;
        }
        try
        {
            _metadata = JsonConvert.DeserializeObject<Dictionary<string, MetaEntry>>(jsonAsset.text) ?? new Dictionary<string, MetaEntry>();
        }
        catch (Exception e)
        {
            Debug.LogError($"[MetaLib] 反序列化失败：{e.Message}");
            _metadata = new Dictionary<string, MetaEntry>();
        }
    }
    
    public static void Reload()
    {
        _isInitialized = false;
        Initialize();
    }

    /// <summary>
    /// 【核心】根据 PackID 获取已加载的 PackData 对象喵~
    /// </summary>
    public static T GetPack<T>(string packID) where T : BasePackData
    {
        if (!_isInitialized) Initialize();

        if (!_metadata.TryGetValue(packID, out var meta))
        {
            Debug.LogError($"[MetaLib] 找不到 PackID 为 '{packID}' 的元数据喵~");
            return null;
        }

        string jsonContent = "";
        switch (meta.Storage)
        {
            case StorageType.Resources:
                var textAsset = Resources.Load<TextAsset>(meta.ResourcePath);
                if (textAsset != null)
                {
                    jsonContent = textAsset.text;
                }
                else
                {
                    Debug.LogError($"[MetaLib] 在 Resources 中找不到资源：{meta.ResourcePath}");
                    return null;
                }
                break;
            
            case StorageType.StreamingAssets:
                try
                {
                    string fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, meta.ResourcePath);
                    jsonContent = System.IO.File.ReadAllText(fullPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MetaLib] 从 StreamingAssets 读取文件失败：{meta.ResourcePath} | {e.Message}");
                    return null;
                }
                break;
        }

        if (string.IsNullOrEmpty(jsonContent)) return null;

        try
        {
            // JSON 序列化设置喵~
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore
            };
            
            return JsonConvert.DeserializeObject<T>(jsonContent, jsonSettings);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MetaLib] 反序列化 PackData 失败 (ID: {packID}): {e.Message}");
            return null;
        }
    }

    public static MetaEntry GetMeta(string packID)
    {
        if (!_isInitialized) Initialize();
        _metadata.TryGetValue(packID, out var entry);
        return entry;
    }

    /// <summary>
    /// 【兼容旧存档】通过资源路径反向查找元数据喵~
    /// </summary>
    public static MetaEntry GetMetaByPath(string resourcePath)
    {
        if (!_isInitialized) Initialize();
        foreach (var entry in _metadata.Values)
        {
            if (entry.ResourcePath == resourcePath)
            {
                return entry;
            }
        }
        return null;
    }
    
    public static IEnumerable<MetaEntry> GetAllMetas()
    {
        if (!_isInitialized) Initialize();
        return _metadata.Values;
    }

    public static bool HasMeta(string packID)
    {
        if (!_isInitialized) Initialize();
        return _metadata.ContainsKey(packID);
    }

    public static void Register(string packID, MetaEntry entry)
    {
        if (!_isInitialized) Initialize();

        if (string.IsNullOrEmpty(packID)) return;
        if (entry == null) return;

        entry.PackID = packID;
        _metadata[packID] = entry;
    }

    public static void Unregister(string packID)
    {
        if (!_isInitialized) Initialize();
        _metadata.Remove(packID);
    }
    
    public static void Clear()
    {
        if (!_isInitialized) Initialize();
        _metadata.Clear();
    }

#if UNITY_EDITOR
    public static void Save()
    {
        var json = JsonConvert.SerializeObject(_metadata, Formatting.Indented);
        string directory = System.IO.Path.GetDirectoryName(Application.dataPath + "/Resources/" + METALIB_PATH + ".json");
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        string fullPath = Application.dataPath + "/Resources/" + METALIB_PATH + ".json";
        System.IO.File.WriteAllText(fullPath, json);
        UnityEditor.AssetDatabase.Refresh();
    }
#else
    public static void Save() { }
#endif

    public static string GetDebugInfo()
    {
        if (!_isInitialized) Initialize();
        var info = new System.Text.StringBuilder();
        info.AppendLine($"[MetaLib] 元数据总数：{_metadata.Count}");
        foreach (var kvp in _metadata)
        {
            info.AppendLine($"  - {kvp.Key}: {kvp.Value?.DisplayName} (Path: {kvp.Value?.ResourcePath})");
        }
        return info.ToString();
    }
}
