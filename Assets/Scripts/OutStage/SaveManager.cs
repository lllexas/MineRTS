using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MineRTS.BigMap;
using Newtonsoft.Json;

public class SaveManager : SingletonMono<SaveManager>
{
    // 【已迁移到 MainModel】当前内存中的用户数据 - 请使用 MainModel.Instance.CurrentUser
    // 【已迁移到 MainModel】当前正在活跃的据点 ID - 请使用 MainModel.Instance.CurrentActiveStageID

    // 当前正在使用的存档文件名（不含扩展名），例如 "AutoSave", "Slot_1"
    public string CurrentSaveFileName { get; private set; } = "default_save";

    // 存档存放目录
    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

    // Newtonsoft.Json 序列化设置
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.None, // 不存储类型信息，保证安全
        NullValueHandling = NullValueHandling.Ignore
    };

    protected override void Awake()
    {
        base.Awake();
        // 确保文件夹存在
        if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
    }

    // ==========================================
    //   核心公共方法 (Console API)
    // ==========================================

    /// <summary>
    /// 【指令：NewGame】创建一个全新的存档，并立即切换过去
    /// </summary>
    public void CreateNewSave(string saveName)
    {
        Debug.Log($"<color=cyan>[SaveManager]</color> 正在创建新存档：{saveName}...");

        // 1. 强制清理当前的运行时状态
        UnloadCurrentWorld();

        // 2. new 一个全新的 User Model 并设置到 MainModel
        UserModel newUser = new UserModel();
        newUser.Metadata.PlayerName = "指挥官-" + saveName;

        // 3. 初始化大地图数据
        // 从 BigMapManager 获取当前的大地图地理信息（如果没有，则使用默认地图）
        if (MineRTS.BigMap.BigMapManager.Instance != null)
        {
            // 尝试从 RuntimeRenderer 获取当前加载的地图数据
            var runtimeRenderer = MineRTS.BigMap.BigMapManager.Instance.GetRuntimeRenderer();
            if (runtimeRenderer != null)
            {
                var currentMapData = runtimeRenderer.GetCurrentMapData();
                if (currentMapData != null)
                {
                    // 深拷贝一份到存档中
                    newUser.BigMapData = CloneBigMapData(currentMapData);
                }
                else
                {
                    // 如果没有当前地图数据，使用默认地图
                    LoadDefaultBigMap(newUser);
                }
            }
            else
            {
                LoadDefaultBigMap(newUser);
            }
        }
        else
        {
            // BigMapManager 不存在，使用默认地图
            LoadDefaultBigMap(newUser);
        }

        // 4. 初始化经济数据字典（从大地图节点数据转换）
        Dictionary<string, BigMapEconomyData> economyDict = newUser.BigMapData.CreateEconomyDictFromNodes();
        foreach (var kvp in economyDict)
        {
            newUser.SetEconomyData(kvp.Key, kvp.Value);
        }
        Debug.Log($"<color=yellow>[SaveManager]</color> 已初始化 {economyDict.Count} 个关卡的经济数据");

        // 5. 设置到 MainModel
        MainModel.Instance.SetCurrentUser(newUser);

        // 6. 标记当前文件名
        CurrentSaveFileName = saveName;

        // 7. 立即落地到磁盘
        SaveGameToDisk();

        Debug.Log($"<color=green>[SaveManager]</color> 新存档 {saveName} 已创建并就绪！");
    }

    /// <summary>
    /// 从默认地图加载 BigMapData
    /// </summary>
    private void LoadDefaultBigMap(UserModel user)
    {
        // 尝试从 BigMapManager 获取默认地图配置
        var bigMapManager = MineRTS.BigMap.BigMapManager.Instance;
        if (bigMapManager != null)
        {
            var defaultMapJson = bigMapManager.GetDefaultMapJson();
            if (defaultMapJson != null)
            {
                user.BigMapData = UnityEngine.JsonUtility.FromJson<MineRTS.BigMap.BigMapSaveData>(defaultMapJson.text);
                Debug.Log("<color=cyan>[SaveManager]</color> 已从默认 JSON 加载大地图数据");
                return;
            }
        }

        // 如果都没有，就创建一个空的 BigMapData
        user.BigMapData = new MineRTS.BigMap.BigMapSaveData();
        Debug.LogWarning("<color=orange>[SaveManager]</color> 使用空的大地图数据（未找到默认地图配置）");
    }

    /// <summary>
    /// 深拷贝 BigMapSaveData（防止引用共享导致的问题）
    /// </summary>
    private MineRTS.BigMap.BigMapSaveData CloneBigMapData(MineRTS.BigMap.BigMapSaveData source)
    {
        if (source == null) return new MineRTS.BigMap.BigMapSaveData();

        // 使用 Unity 的 JsonUtility 处理 Unity 类型（Vector2 等）
        var json = UnityEngine.JsonUtility.ToJson(source);
        return UnityEngine.JsonUtility.FromJson<MineRTS.BigMap.BigMapSaveData>(json);
    }

    /// <summary>
    /// 【指令：LoadGame】读取指定存档到内存，并替换当前游戏状态
    /// </summary>
    public void LoadSave(string saveName)
    {
        string fullPath = Path.Combine(SaveDirectory, saveName + ".json");
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"<color=red>[SaveManager]</color> 找不到存档文件：{saveName}");
            return;
        }

        Debug.Log($"<color=yellow>[SaveManager]</color> 正在读取存档：{saveName}...");

        // 1. 清理现场
        UnloadCurrentWorld();

        try
        {
            // 2. 读取文本
            string json = File.ReadAllText(fullPath);

            // 3. 使用 Newtonsoft.Json 反序列化
            UserModel loadedUser = JsonConvert.DeserializeObject<UserModel>(json, JsonSettings);

            if (loadedUser == null)
            {
                throw new System.Exception("反序列化返回 null");
            }

            // 4. 设置到 MainModel
            MainModel.Instance.SetCurrentUser(loadedUser);

            // 5. 更新当前文件名引用
            CurrentSaveFileName = saveName;

            Debug.Log($"<color=green>[SaveManager]</color> 存档 {saveName} 加载完毕喵！");

            // 6. 通知 BigMapManager 重新加载地图数据
            LoadBigMapForCurrentUser();

            // 7. 进入大地图
            GameFlowController.Instance.SwitchToState(GameFlowController.GameState.BigMap);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 坏档了喵！原因：{e.Message}");
            Debug.LogException(e);
            // 坏档处理：可以选择回退或者创建一个新的空档
        }
    }

    /// <summary>
    /// 为当前用户加载大地图数据到渲染器
    /// </summary>
    private void LoadBigMapForCurrentUser()
    {
        var user = MainModel.Instance.CurrentUser;
        if (user?.BigMapData == null)
        {
            Debug.LogWarning("<color=orange>[SaveManager]</color> 当前用户没有大地图数据，跳过渲染");
            return;
        }

        if (MineRTS.BigMap.BigMapManager.Instance == null)
        {
            Debug.LogWarning("<color=orange>[SaveManager]</color> BigMapManager 不存在，跳过渲染");
            return;
        }

        // 将 BigMapData 转换为 JSON 并传递给渲染器
        string mapJson = UnityEngine.JsonUtility.ToJson(user.BigMapData);
        MineRTS.BigMap.BigMapManager.Instance.LoadMapFromSaveData(mapJson);

        Debug.Log("<color=cyan>[SaveManager]</color> 大地图渲染数据已加载");
    }

    /// <summary>
    /// 【指令：SaveGame】保存当前状态到磁盘
    /// </summary>
    public void SaveGameToDisk()
    {
        // 从 MainModel 获取当前用户数据
        var currentUser = MainModel.Instance.CurrentUser;
        if (currentUser == null) return;

        // 1. 如果还在关卡里，先把关卡热数据写回 UserModel
        if (MainModel.Instance.IsInStage)
        {
            // 调用 GameFlowController 进行数据同步
            GameFlowController.Instance.SaveCurrentStageFromSystem();
        }

        // 2. 使用 Newtonsoft.Json 序列化
        string json = JsonConvert.SerializeObject(currentUser, JsonSettings);
        string fullPath = Path.Combine(SaveDirectory, CurrentSaveFileName + ".json");

        File.WriteAllText(fullPath, json);
        Debug.Log($"<color=cyan>[SaveManager]</color> 磁盘写入完毕：{fullPath}");
    }

    /// <summary>
    /// 【指令：DeleteSave】删除指定存档
    /// </summary>
    public void DeleteSave(string saveName)
    {
        string fullPath = Path.Combine(SaveDirectory, saveName + ".json");
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            Debug.Log($"<color=red>[SaveManager]</color> 存档 {saveName} 已被销毁。");
        }
    }

    /// <summary>
    /// 【新增】重命名指定存档
    /// </summary>
    /// <param name="oldName">旧存档名</param>
    /// <param name="newName">新存档名</param>
    /// <returns>是否重命名成功</returns>
    public bool RenameSave(string oldName, string newName)
    {
        // 1. 基本参数检查
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning($"<color=orange>[SaveManager]</color> 重命名失败：存档名称不能为空");
            return false;
        }

        if (oldName == newName)
        {
            Debug.Log($"<color=yellow>[SaveManager]</color> 新旧名称相同，无需重命名");
            return true; // 视为成功
        }

        // 2. 检查旧文件是否存在
        string oldPath = Path.Combine(SaveDirectory, oldName + ".json");
        if (!File.Exists(oldPath))
        {
            Debug.LogError($"<color=red>[SaveManager]</color> 重命名失败：找不到存档 '{oldName}'");
            return false;
        }

        // 3. 检查新名称是否已存在
        string newPath = Path.Combine(SaveDirectory, newName + ".json");
        if (File.Exists(newPath))
        {
            Debug.LogWarning($"<color=orange>[SaveManager]</color> 重命名失败：存档 '{newName}' 已存在");
            return false;
        }

        try
        {
            // 4. 重命名文件
            File.Move(oldPath, newPath);
            Debug.Log($"<color=green>[SaveManager]</color> 存档 '{oldName}' 已重命名为 '{newName}'");

            // 5. 如果重命名的是当前活跃存档，更新当前文件名
            if (CurrentSaveFileName == oldName)
            {
                CurrentSaveFileName = newName;
                Debug.Log($"<color=cyan>[SaveManager]</color> 当前活跃存档名称已更新为 '{newName}'");
            }

            // 6. 如果该存档已加载到内存中，更新 UserModel 的元数据
            if (MainModel.Instance.CurrentUser != null && CurrentSaveFileName == newName)
            {
                // 更新 PlayerName 以反映新存档名
                MainModel.Instance.CurrentUser.Metadata.PlayerName = "指挥官-" + newName;
                Debug.Log($"<color=cyan>[SaveManager]</color> 已更新内存中 UserModel 的元数据");
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"<color=red>[SaveManager]</color> 重命名失败：{e.Message}");
            return false;
        }
    }

    public List<string> GetAllSaveFiles()
    {
        if (!Directory.Exists(SaveDirectory)) return new List<string>();

        return Directory.GetFiles(SaveDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();
    }

    // ==========================================
    //   私有辅助
    // ==========================================

    /// <summary>
    /// 卸载当前世界的运行时数据，防止串台
    /// </summary>
    public void UnloadCurrentWorld()
    {
        // 1. 清空 ECS 系统
        if (EntitySystem.Instance != null)
        {
            EntitySystem.Instance.ClearWorld();
        }

        // 2. 【已迁移到 MainModel】重置当前关卡指针
        MainModel.Instance.ClearCurrentStage();

        // 3. 【已迁移到 MainModel】销毁当前的 UserModel 引用
        MainModel.Instance.ClearCurrentUser();

        System.GC.Collect();
    }
}
