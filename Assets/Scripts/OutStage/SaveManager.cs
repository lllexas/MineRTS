using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class SaveManager : SingletonMono<SaveManager>
{
    // 当前内存中的用户数据
    public UserModel CurrentUser;

    // 【新增】当前正在活跃的据点 ID（用于判断 SaveNow 该存哪一关）
    public string CurrentActiveStageID;

    // 当前正在使用的存档文件名（不含扩展名），例如 "AutoSave", "Slot_1"
    public string CurrentSaveFileName { get; private set; } = "default_save";

    // 存档存放目录
    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

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
        Debug.Log($"<color=cyan>[SaveManager]</color> 正在创建新存档: {saveName}...");

        // 1. 强制清理当前的运行时状态
        UnloadCurrentWorld();

        // 2. new 一个全新的 User Model
        CurrentUser = new UserModel();
        CurrentUser.Metadata.PlayerName = "指挥官-" + saveName;

        // 3. 标记当前文件名
        CurrentSaveFileName = saveName;

        // 4. 立即落地到磁盘
        SaveGameToDisk();

        Debug.Log($"<color=green>[SaveManager]</color> 新存档 {saveName} 已创建并就绪！");
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

        Debug.Log($"<color=yellow>[SaveManager]</color> 正在读取存档: {saveName}...");

        // 1. 清理现场
        UnloadCurrentWorld();

        try
        {
            // 2. 读取文本
            string json = File.ReadAllText(fullPath);

            // 3. 反序列化
            CurrentUser = JsonUtility.FromJson<UserModel>(json);

            // 4. 重建字典索引 (关键！)
            CurrentUser.RebuildRuntimeCache();

            // 5. 更新当前文件名引用
            CurrentSaveFileName = saveName;

            Debug.Log($"<color=green>[SaveManager]</color> 存档 {saveName} 加载完毕喵！");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 坏档了喵！原因: {e.Message}");
            // 坏档处理：可以选择回退或者创建一个新的空档
        }
    }

    /// <summary>
    /// 【新增】将 EntitySystem 当前运行的关卡数据，回写到 UserData 内存中
    /// </summary>
    public void SaveCurrentStageFromSystem()
    {
        // 如果当前不在任何关卡里，或者 System 还没初始化，就别存了
        if (string.IsNullOrEmpty(CurrentActiveStageID)) return;
        if (EntitySystem.Instance == null || EntitySystem.Instance.wholeComponent == null) return;

        Debug.Log($"<color=orange>[SaveManager]</color> 正在从 ECS 提取关卡数据: {CurrentActiveStageID}...");

        // 1. 从 ECS 获取当前快照 (必须 Clone，防止引用污染)
        WholeComponent snapshot = EntitySystem.Instance.wholeComponent.Clone();

        // 2. 更新到 UserModel
        // (IsCleared 逻辑您可以根据游戏胜利条件来传 true/false，这里暂时传 false)
        CurrentUser.UpdateStage(CurrentActiveStageID, snapshot, false);

        Debug.Log($"<color=cyan>[SaveManager]</color> {CurrentActiveStageID} 内存数据同步完成。");
    }
    /// <summary>
    /// 【新增】从关卡撤退回到“存档大厅”/“大地图”状态
    /// </summary>
    /// <param name="autoSave">撤退前是否自动保存进度？</param>
    public void ExitCurrentStage(bool autoSave = true)
    {
        if (string.IsNullOrEmpty(CurrentActiveStageID))
        {
            Debug.LogWarning("喵？当前并没有在任何关卡里，无法撤退！");
            return;
        }

        Debug.Log($"<color=orange>[SaveManager]</color> 正在从据点 {CurrentActiveStageID} 撤离...");

        // 1. (可选) 撤退前自动保存热数据到 RAM
        if (autoSave)
        {
            SaveCurrentStageFromSystem();
            // 顺便落盘，防止崩溃丢档（视性能需求而定，也可以不写这句）
            SaveGameToDisk();
        }

        // 2. 物理清理 ECS 世界 (视觉消失，内存释放)
        if (EntitySystem.Instance != null)
        {
            EntitySystem.Instance.ClearWorld();
        }

        // 3. 清空状态指针 (这标志着我们回到了“存档外/大地图”状态)
        string lastStage = CurrentActiveStageID;
        CurrentActiveStageID = null;

        Debug.Log($"<color=green>[SaveManager]</color> 已安全撤离 {lastStage}。指挥官现在位于大地图待机。");
    }
    /// <summary>
    /// 【新增】重置指定关卡（删除存档记录，下次进入时会重新读 JSON）
    /// </summary>
    public void ResetStage(string stageID)
    {
        if (CurrentUser == null) return;

        Debug.Log($"<color=red>[SaveManager]</color> 正在重置关卡存档: {stageID}");

        // 1. 从用户数据中移除该关卡的记录
        CurrentUser.RemoveStage(stageID);

        // 2. 如果当前正在玩这一关，立即让 ECS 重新加载
        // (ECS 的 LoadStage 发现 UserData 里没了，就会自动去读 WorldFactory 的 JSON)
        if (CurrentActiveStageID == stageID)
        {
            EntitySystem.Instance.LoadStage(stageID);
        }
    }

    /// <summary>
    /// 【指令：SaveGame】保存当前状态到磁盘
    /// </summary>
    public void SaveGameToDisk()
    {
        if (CurrentUser == null) return;

        // 1. 如果还在关卡里，先把关卡热数据写回 UserModel
        if (!string.IsNullOrEmpty(CurrentActiveStageID))
        {
            SaveCurrentStageFromSystem();
        }

        // 2. 序列化并写入
        string json = JsonUtility.ToJson(CurrentUser, true);
        string fullPath = Path.Combine(SaveDirectory, CurrentSaveFileName + ".json");

        File.WriteAllText(fullPath, json);
        Debug.Log($"<color=cyan>[SaveManager]</color> 磁盘写入完毕: {fullPath}");
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
    private void UnloadCurrentWorld()
    {
        // 1. 清空 ECS 系统
        if (EntitySystem.Instance != null)
        {
            EntitySystem.Instance.ClearWorld();
        }

        // 2. 【新增】重置当前关卡指针
        CurrentActiveStageID = null;

        // 3. 销毁当前的 UserModel 引用
        CurrentUser = null;

        System.GC.Collect();
    }
}