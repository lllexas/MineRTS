using System.Collections.Generic;
using System.IO;
using System.Linq;
using MineRTS.BigMap;
using NekoGraph;
using Newtonsoft.Json;
using UnityEngine;

public class SaveManager : SingletonMono<SaveManager>
{
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Ignore
    };

    public string CurrentSaveFileName { get; private set; } = "default_save";

    [Header("Boot")]
    [SerializeField] private StartBoots _startBoots;

    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

    protected override void Awake()
    {
        base.Awake();

        if (_startBoots == null)
            _startBoots = GetComponent<StartBoots>();

        if (!Directory.Exists(SaveDirectory))
        {
            Directory.CreateDirectory(SaveDirectory);
        }
    }

    public void CreateNewSave(string saveName)
    {
        Debug.Log($"<color=cyan>[SaveManager]</color> Creating new save: {saveName}");

        UnloadCurrentWorld();

        UserModel newUser = new UserModel();
        newUser.Metadata.PlayerName = "Commander-" + saveName;

        if (BigMapManager.Instance != null)
        {
            var runtimeRenderer = BigMapManager.Instance.GetRuntimeRenderer();
            var currentMapData = runtimeRenderer != null ? runtimeRenderer.GetCurrentMapData() : null;

            if (currentMapData != null)
            {
                newUser.BigMapData = CloneBigMapData(currentMapData);
            }
            else
            {
                LoadDefaultBigMap(newUser);
            }
        }
        else
        {
            LoadDefaultBigMap(newUser);
        }

        Dictionary<string, BigMapEconomyData> economyDict = newUser.BigMapData.CreateEconomyDictFromNodes();
        foreach (var pair in economyDict)
        {
            newUser.SetEconomyData(pair.Key, pair.Value);
        }

        if (_startBoots != null && _startBoots.HasEntries)
            _startBoots.ApplyTo(newUser);

        CurrentSaveFileName = saveName;
        WriteUserToDisk(newUser, saveName);

        Debug.Log($"<color=green>[SaveManager]</color> Save created: {saveName}");
        LoadSave(saveName);
    }

    private void LoadDefaultBigMap(UserModel user)
    {
        var bigMapManager = BigMapManager.Instance;
        if (bigMapManager != null)
        {
            var defaultMapJson = bigMapManager.GetDefaultMapJson();
            if (defaultMapJson != null)
            {
                user.BigMapData = JsonUtility.FromJson<BigMapSaveData>(defaultMapJson.text);
                Debug.Log("<color=cyan>[SaveManager]</color> Loaded default big map data.");
                return;
            }
        }

        user.BigMapData = new BigMapSaveData();
        Debug.LogWarning("<color=orange>[SaveManager]</color> Default big map data not found. Using empty map.");
    }

    private BigMapSaveData CloneBigMapData(BigMapSaveData source)
    {
        if (source == null)
        {
            return new BigMapSaveData();
        }

        string json = JsonUtility.ToJson(source);
        return JsonUtility.FromJson<BigMapSaveData>(json);
    }

    public void LoadSave(string saveName)
    {
        string fullPath = Path.Combine(SaveDirectory, saveName + ".json");
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"<color=red>[SaveManager]</color> Save file not found: {saveName}");
            return;
        }

        Debug.Log($"<color=yellow>[SaveManager]</color> Loading save: {saveName}");

        UnloadCurrentWorld();

        try
        {
            string json = File.ReadAllText(fullPath);
            UserModel loadedUser = JsonConvert.DeserializeObject<UserModel>(json, JsonSettings);
            if (loadedUser == null)
            {
                throw new JsonException("Deserialized UserModel is null.");
            }

            MainModel.Instance.SetCurrentUser(loadedUser);
            GraphHub.Instance?.ApplyUser(loadedUser);
            _startBoots?.ApplyHubBindings(GraphHub.Instance);
            PostSystem.Instance.Send("VFS.IO_Ready", loadedUser);

            CurrentSaveFileName = saveName;

            LoadBigMapForCurrentUser();
            GameFlowController.Instance.SwitchToState(GameFlowController.GameState.BigMap);

            Debug.Log($"<color=green>[SaveManager]</color> Save loaded: {saveName}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to load save '{saveName}': {ex.Message}");
            Debug.LogException(ex);
        }
    }

    private void LoadBigMapForCurrentUser()
    {
        var user = MainModel.Instance.CurrentUser;
        if (user?.BigMapData == null)
        {
            Debug.LogWarning("<color=orange>[SaveManager]</color> Current user has no big map data.");
            return;
        }

        if (BigMapManager.Instance == null)
        {
            Debug.LogWarning("<color=orange>[SaveManager]</color> BigMapManager is unavailable.");
            return;
        }

        string mapJson = JsonUtility.ToJson(user.BigMapData);
        BigMapManager.Instance.LoadMapFromSaveData(mapJson);
        Debug.Log("<color=cyan>[SaveManager]</color> Big map data applied.");
    }

    public void SaveGameToDisk()
    {
        var currentUser = MainModel.Instance.CurrentUser;
        if (currentUser == null)
        {
            return;
        }

        if (MainModel.Instance.IsInStage)
        {
            GameFlowController.Instance.SaveCurrentStageFromSystem();
        }

        WriteUserToDisk(currentUser, CurrentSaveFileName);
    }

    private void WriteUserToDisk(UserModel user, string saveName)
    {
        if (user == null || string.IsNullOrWhiteSpace(saveName))
            return;

        string json = JsonConvert.SerializeObject(user, JsonSettings);
        string fullPath = Path.Combine(SaveDirectory, saveName + ".json");
        File.WriteAllText(fullPath, json);

        Debug.Log($"<color=cyan>[SaveManager]</color> Save written: {fullPath}");
    }

    public void DeleteSave(string saveName)
    {
        string fullPath = Path.Combine(SaveDirectory, saveName + ".json");
        if (!File.Exists(fullPath))
        {
            return;
        }

        File.Delete(fullPath);
        Debug.Log($"<color=red>[SaveManager]</color> Save deleted: {saveName}");
    }

    public bool RenameSave(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
        {
            Debug.LogWarning("<color=orange>[SaveManager]</color> Rename failed: save name is empty.");
            return false;
        }

        if (oldName == newName)
        {
            return true;
        }

        string oldPath = Path.Combine(SaveDirectory, oldName + ".json");
        string newPath = Path.Combine(SaveDirectory, newName + ".json");

        if (!File.Exists(oldPath))
        {
            Debug.LogError($"<color=red>[SaveManager]</color> Rename failed: save not found '{oldName}'.");
            return false;
        }

        if (File.Exists(newPath))
        {
            Debug.LogWarning($"<color=orange>[SaveManager]</color> Rename failed: target already exists '{newName}'.");
            return false;
        }

        try
        {
            File.Move(oldPath, newPath);

            if (CurrentSaveFileName == oldName)
            {
                CurrentSaveFileName = newName;
            }

            if (MainModel.Instance.CurrentUser != null && CurrentSaveFileName == newName)
            {
                MainModel.Instance.CurrentUser.Metadata.PlayerName = "Commander-" + newName;
            }

            Debug.Log($"<color=green>[SaveManager]</color> Save renamed: {oldName} -> {newName}");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"<color=red>[SaveManager]</color> Rename failed: {ex.Message}");
            return false;
        }
    }

    public List<string> GetAllSaveFiles()
    {
        if (!Directory.Exists(SaveDirectory))
        {
            return new List<string>();
        }

        return Directory
            .GetFiles(SaveDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();
    }

    public void UnloadCurrentWorld()
    {
        if (EntitySystem.Instance != null)
        {
            EntitySystem.Instance.ClearWorld();
        }

        MainModel.Instance.ClearCurrentStage();
        MainModel.Instance.ClearCurrentUser();
        GraphHub.Instance?.ApplyUser(MainModel.Instance.CurrentUser);
        GraphHub.Instance?.ClearFacadeBindings();

        System.GC.Collect();
    }
}
