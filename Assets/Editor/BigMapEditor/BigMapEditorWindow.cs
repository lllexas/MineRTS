using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using MineRTS.BigMap;

/// <summary>
/// 大地图拓扑编辑器窗口
/// 纯 UI Toolkit 实现，不使用 GraphView
/// </summary>
public class BigMapEditorWindow : EditorWindow
{
    private const string DefaultBigMapDirectory = "Assets/Resources";
    private const string DefaultBigMapFileName = "BigMapData.json";
    private const string TilemapEditorScenePath = "Assets/Scenes/TilemapLevelEditor.unity";

    private BigMapSaveData _saveData = new BigMapSaveData();

    private MapCanvasElement _mapCanvas;
    private NodeInspectorPanel _inspectorPanel;
    private ToolbarButton _saveButton;
    private ToolbarButton _loadButton;

    private BigMapNodeData _selectedNode;
    private NodeVisualElement _selectedNodeVisual;

    private string _currentFilePath;

    [MenuItem("Tools/猫娘助手/BigMapNet拓扑编辑器")]
    public static void OpenWindow()
    {
        var window = GetWindow<BigMapEditorWindow>();
        window.titleContent = new GUIContent("BigMapNet");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }

    private void OnEnable()
    {
        _saveData = new BigMapSaveData();

        ConstructRootLayout();
        GenerateToolbar();

        if (_mapCanvas != null)
        {
            _mapCanvas.Initialize(_saveData);
            _mapCanvas.OnNodeSelected += OnNodeSelected;
            _mapCanvas.OnNodeDeselected += OnNodeDeselected;
        }

        TryHandleReturnFromTilemapEditor();
    }

    private void OnDisable()
    {
        if (_mapCanvas != null)
        {
            _mapCanvas.OnNodeSelected -= OnNodeSelected;
            _mapCanvas.OnNodeDeselected -= OnNodeDeselected;
        }
    }

    private void OnFocus()
    {
        TryHandleReturnFromTilemapEditor();
    }

    private void ConstructRootLayout()
    {
        rootVisualElement.Clear();

        var mainContainer = new VisualElement();
        mainContainer.style.flexDirection = FlexDirection.Column;
        mainContainer.style.flexGrow = 1;

        var contentContainer = new VisualElement();
        contentContainer.style.flexDirection = FlexDirection.Row;
        contentContainer.style.flexGrow = 1;

        var canvasContainer = new VisualElement();
        canvasContainer.name = "canvas-container";
        canvasContainer.style.flexGrow = 0.7f;
        canvasContainer.style.flexShrink = 0;
        canvasContainer.style.flexBasis = new StyleLength(new Length(70, LengthUnit.Percent));
        canvasContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1.0f);

        _mapCanvas = new MapCanvasElement();
        _mapCanvas.name = "map-canvas";
        _mapCanvas.style.flexGrow = 1;
        canvasContainer.Add(_mapCanvas);

        var inspectorContainer = new VisualElement();
        inspectorContainer.name = "inspector-container";
        inspectorContainer.style.flexGrow = 0.3f;
        inspectorContainer.style.flexShrink = 0;
        inspectorContainer.style.flexBasis = new StyleLength(new Length(30, LengthUnit.Percent));
        inspectorContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
        inspectorContainer.style.borderLeftWidth = 1;
        inspectorContainer.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);

        _inspectorPanel = new NodeInspectorPanel();
        _inspectorPanel.name = "node-inspector";
        _inspectorPanel.style.flexGrow = 1;
        inspectorContainer.Add(_inspectorPanel);

        contentContainer.Add(canvasContainer);
        contentContainer.Add(inspectorContainer);

        mainContainer.Add(contentContainer);
        rootVisualElement.Add(mainContainer);
    }

    private void GenerateToolbar()
    {
        var toolbar = new Toolbar();

        _saveButton = new ToolbarButton(SaveData)
        {
            text = "保存 (JSON)",
            tooltip = "保存当前地图拓扑"
        };
        toolbar.Add(_saveButton);

        _loadButton = new ToolbarButton(LoadData)
        {
            text = "读取 (JSON)",
            tooltip = "从 JSON 文件加载地图拓扑"
        };
        toolbar.Add(_loadButton);

        var separator = new VisualElement();
        separator.style.width = 1;
        separator.style.marginTop = 2;
        separator.style.marginBottom = 2;
        separator.style.marginLeft = 5;
        separator.style.marginRight = 5;
        separator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        toolbar.Add(separator);

        var clearButton = new ToolbarButton(() =>
        {
            if (!EditorUtility.DisplayDialog("清空画布", "确定要清空所有节点和连线吗？此操作不可撤销。", "确定", "取消"))
            {
                return;
            }

            _saveData = new BigMapSaveData();
            _mapCanvas?.Initialize(_saveData);
            _inspectorPanel?.ClearPanel();
            _selectedNode = null;
            _selectedNodeVisual = null;
        })
        {
            text = "清空画布",
            tooltip = "清空所有节点和连线"
        };
        toolbar.Add(clearButton);

        rootVisualElement.Insert(0, toolbar);
    }

    private void SaveData()
    {
        SaveDataInternal(promptForPath: string.IsNullOrEmpty(_currentFilePath));
    }

    private void LoadData()
    {
        string absolutePath = EditorUtility.OpenFilePanel("加载大地图数据", DefaultBigMapDirectory, "json");
        if (string.IsNullOrEmpty(absolutePath))
        {
            return;
        }

        if (!absolutePath.StartsWith(Application.dataPath))
        {
            EditorUtility.DisplayDialog("错误", "请从 Assets 目录下选择文件。", "确定");
            return;
        }

        string assetPath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
        if (!LoadDataFromAssetPath(assetPath))
        {
            EditorUtility.DisplayDialog("加载失败", "无法解析 JSON 文件。", "确定");
        }
    }

    public void OpenTilemapEditor(BigMapNodeData node)
    {
        if (node == null)
        {
            return;
        }

        if (!TrySaveCurrentFile())
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var originScene = EditorSceneManager.GetActiveScene();
        if (!originScene.IsValid() || string.IsNullOrEmpty(originScene.path))
        {
            EditorUtility.DisplayDialog("错误", "当前活动场景无效，无法进入关卡编辑器。", "确定");
            return;
        }

        EditorSessionBridge.SetSession(node.StageID, originScene.path, _currentFilePath, sceneSetup);

        string scenePath = TilemapLevelEditorSceneUtility.EnsureSceneExists(TilemapEditorScenePath);
        var editorScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        if (!editorScene.IsValid())
        {
            EditorUtility.DisplayDialog("错误", "无法加载 Tilemap 编辑场景。", "确定");
            return;
        }

        EditorSceneManager.SetActiveScene(editorScene);
        InitializeTilemapEditorScene(editorScene, node.StageID);
        EditorSceneManager.CloseScene(originScene, false);
    }

    private static void InitializeTilemapEditorScene(Scene scene, string stageId)
    {
        if (!scene.IsValid())
        {
            return;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            var editor = root.GetComponentInChildren<TilemapLevelEditor>(true);
            if (editor == null)
            {
                continue;
            }

            editor.LoadSessionIfNeeded();
            if (editor.StageID != stageId)
            {
                editor.OverrideStage(stageId);
            }

            EditorUtility.SetDirty(editor);
            return;
        }

        Debug.LogWarning($"<color=orange>[BigMapEditor]</color> 在场景 {scene.path} 中未找到 TilemapLevelEditor。");
    }

    public void RequestRepaint()
    {
        _mapCanvas?.MarkDirtyRepaint();
    }

    public BigMapSaveData GetSaveData()
    {
        return _saveData;
    }

    public void SetSaveData(BigMapSaveData data)
    {
        _saveData = data;
        _mapCanvas?.Initialize(_saveData);
    }

    public void DeleteNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return;
        }

        _mapCanvas?.DeleteNode(nodeId);
        _inspectorPanel?.ClearPanel();
        _selectedNode = null;
        _selectedNodeVisual = null;
    }

    private void OnGUI()
    {
        HandleKeyboardShortcuts();
    }

    private void HandleKeyboardShortcuts()
    {
        var currentEvent = Event.current;
        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Delete && _selectedNode != null)
        {
            DeleteNode(_selectedNode.StageID);
            currentEvent.Use();
        }
    }

    public void UpdateNodeID(string oldID, string newID)
    {
        _mapCanvas?.UpdateNodeID(oldID, newID);
    }

    private void OnNodeSelected(BigMapNodeData nodeData, NodeVisualElement nodeVisual)
    {
        if (_selectedNode != nodeData)
        {
            _selectedNode = nodeData;
            _selectedNodeVisual = nodeVisual;
            _inspectorPanel?.BindNode(nodeData);
        }
        else
        {
            _inspectorPanel?.Refresh();
        }
    }

    private void OnNodeDeselected()
    {
        _selectedNode = null;
        _selectedNodeVisual = null;
        _inspectorPanel?.ClearPanel();
    }

    private bool TrySaveCurrentFile()
    {
        return SaveDataInternal(promptForPath: true);
    }

    private bool SaveDataInternal(bool promptForPath)
    {
        if (_mapCanvas != null)
        {
            _saveData.CanvasOffset = _mapCanvas.CanvasOffset;
            _saveData.CanvasZoom = _mapCanvas.CanvasZoom;
        }

        string assetPath = _currentFilePath;
        if (string.IsNullOrEmpty(assetPath))
        {
            if (!promptForPath)
            {
                return false;
            }

            string absolutePath = EditorUtility.SaveFilePanel("保存大地图数据", DefaultBigMapDirectory, DefaultBigMapFileName, "json");
            if (string.IsNullOrEmpty(absolutePath))
            {
                return false;
            }

            if (!absolutePath.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog("错误", "请将文件保存在 Assets 目录下。", "确定");
                return false;
            }

            assetPath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
        }

        return SaveDataToAssetPath(assetPath);
    }

    private bool SaveDataToAssetPath(string assetPath)
    {
        string absolutePath = ToAbsolutePath(assetPath);
        if (string.IsNullOrEmpty(absolutePath))
        {
            return false;
        }

        try
        {
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(_saveData, true);
            File.WriteAllText(absolutePath, json);

            _currentFilePath = assetPath;
            AssetDatabase.Refresh();

            Debug.Log($"大地图数据已保存到: {_currentFilePath}");
            ShowNotification(new GUIContent("保存成功！"));
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"保存失败: {ex.Message}");
            EditorUtility.DisplayDialog("保存失败", $"保存过程中发生错误:\n{ex.Message}", "确定");
            return false;
        }
    }

    private bool LoadDataFromAssetPath(string assetPath)
    {
        string absolutePath = ToAbsolutePath(assetPath);
        if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(absolutePath);
            var loadedData = JsonUtility.FromJson<BigMapSaveData>(json);
            if (loadedData == null)
            {
                return false;
            }

            _saveData = loadedData;
            _currentFilePath = assetPath;

            if (_mapCanvas != null)
            {
                _mapCanvas.Initialize(_saveData);
                _mapCanvas.CanvasOffset = _saveData.CanvasOffset;
                _mapCanvas.CanvasZoom = _saveData.CanvasZoom;
            }

            _inspectorPanel?.ClearPanel();
            _selectedNode = null;
            _selectedNodeVisual = null;

            Debug.Log($"大地图数据已从 {_currentFilePath} 加载");
            ShowNotification(new GUIContent("加载成功！"));
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"加载失败: {ex.Message}");
            return false;
        }
    }

    private void TryHandleReturnFromTilemapEditor()
    {
        if (!EditorSessionBridge.IsReturning)
        {
            return;
        }

        if (!EditorSessionBridge.TryGetSession(out var session))
        {
            EditorSessionBridge.ClearSession();
            return;
        }

        EditorSessionBridge.ClearReturning();

        if (!string.IsNullOrEmpty(session.BigMapPath))
        {
            LoadDataFromAssetPath(session.BigMapPath);
        }

        if (_saveData?.Nodes != null)
        {
            _selectedNode = _saveData.Nodes.Find(n => n.StageID == session.StageID);
            if (_selectedNode != null)
            {
                _inspectorPanel?.BindNode(_selectedNode);
                _inspectorPanel?.RefreshTemplateStatus();
            }
            else
            {
                _inspectorPanel?.ClearPanel();
            }
        }

        ShowNotification(new GUIContent("已从关卡编辑器返回"));
        EditorSessionBridge.ClearSession();
        FocusExistingWindow();
    }

    private string ToAbsolutePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets"))
        {
            return null;
        }

        return Path.Combine(Application.dataPath, assetPath.Substring("Assets".Length).TrimStart('/', '\\'));
    }

    public static void FocusExistingWindow()
    {
        var windows = Resources.FindObjectsOfTypeAll<BigMapEditorWindow>();
        if (windows == null || windows.Length == 0)
        {
            return;
        }

        windows[0].Focus();
        windows[0].Repaint();
    }
}

public static class EditorSessionBridge
{
    private const string Prefix = "MineRTS_TilemapEditor_";

    public static bool IsReturning => EditorPrefs.GetBool(Prefix + "IsReturning", false);

    public static void SetSession(string stageId, string returnScenePath, string bigMapPath, SceneSetup[] sceneSetup)
    {
        EditorPrefs.SetString(Prefix + "StageID", stageId ?? string.Empty);
        EditorPrefs.SetString(Prefix + "ReturnScenePath", returnScenePath ?? string.Empty);
        EditorPrefs.SetString(Prefix + "BigMapPath", bigMapPath ?? string.Empty);
        EditorPrefs.SetString(Prefix + "SceneSetupJson", SceneSetupStateCollection.Serialize(sceneSetup));
        EditorPrefs.SetBool(Prefix + "IsReturning", false);
    }

    public static void MarkReturning()
    {
        EditorPrefs.SetBool(Prefix + "IsReturning", true);
    }

    public static void ClearReturning()
    {
        EditorPrefs.SetBool(Prefix + "IsReturning", false);
    }

    public static bool TryGetSession(out EditorTilemapSession session)
    {
        session = new EditorTilemapSession
        {
            StageID = EditorPrefs.GetString(Prefix + "StageID", string.Empty),
            ReturnScenePath = EditorPrefs.GetString(Prefix + "ReturnScenePath", string.Empty),
            BigMapPath = EditorPrefs.GetString(Prefix + "BigMapPath", string.Empty),
            SceneSetup = SceneSetupStateCollection.Deserialize(EditorPrefs.GetString(Prefix + "SceneSetupJson", string.Empty))
        };

        return !string.IsNullOrEmpty(session.StageID);
    }

    public static void ClearSession()
    {
        EditorPrefs.DeleteKey(Prefix + "StageID");
        EditorPrefs.DeleteKey(Prefix + "ReturnScenePath");
        EditorPrefs.DeleteKey(Prefix + "BigMapPath");
        EditorPrefs.DeleteKey(Prefix + "SceneSetupJson");
        EditorPrefs.DeleteKey(Prefix + "IsReturning");
    }
}

public struct EditorTilemapSession
{
    public string StageID;
    public string ReturnScenePath;
    public string BigMapPath;
    public SceneSetup[] SceneSetup;
}

[System.Serializable]
public struct SceneSetupState
{
    public string path;
    public bool isLoaded;
    public bool isActive;
}

[System.Serializable]
public class SceneSetupStateCollection
{
    public List<SceneSetupState> scenes = new List<SceneSetupState>();

    public static string Serialize(SceneSetup[] setup)
    {
        var collection = new SceneSetupStateCollection();
        if (setup != null)
        {
            foreach (var scene in setup)
            {
                collection.scenes.Add(new SceneSetupState
                {
                    path = scene.path,
                    isLoaded = scene.isLoaded,
                    isActive = scene.isActive
                });
            }
        }

        return JsonUtility.ToJson(collection);
    }

    public static SceneSetup[] Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        var collection = JsonUtility.FromJson<SceneSetupStateCollection>(json);
        if (collection?.scenes == null || collection.scenes.Count == 0)
        {
            return null;
        }

        var setup = new SceneSetup[collection.scenes.Count];
        for (int i = 0; i < collection.scenes.Count; i++)
        {
            var state = collection.scenes[i];
            setup[i] = new SceneSetup
            {
                path = state.path,
                isLoaded = state.isLoaded,
                isActive = state.isActive
            };
        }

        return setup;
    }
}

[ExecuteAlways]
public class TilemapLevelEditor : MonoBehaviour
{
    private const string DefaultScenePath = "Assets/Scenes/TilemapLevelEditor.unity";
    private const string LevelDirectory = "Assets/Resources/Levels";

    [SerializeField] private Grid _grid;
    [SerializeField] private Tilemap _groundTilemap;
    [SerializeField] private TileMappingConfig _tileMappingConfig;

    [SerializeField] private string _stageId;
    [SerializeField] private bool _loadedExistingTemplate;

    [SerializeField] private int _width = 64;
    [SerializeField] private int _height = 64;
    [SerializeField] private int _originX = -32;
    [SerializeField] private int _originY = -32;

    private bool _sessionLoaded;

    public string StageID => _stageId;
    public bool LoadedExistingTemplate => _loadedExistingTemplate;
    public bool HasTileMappingConfig => _tileMappingConfig != null;

    [MenuItem("Tools/猫娘助手/Tilemap关卡编辑器")]
    public static void OpenEditorScene()
    {
        string scenePath = TilemapLevelEditorSceneUtility.EnsureSceneExists(DefaultScenePath);
        EditorSceneManager.OpenScene(scenePath);
    }

    private void OnEnable()
    {
        EnsureReferences();
        LoadSessionIfNeeded();
    }

    public bool SaveLevel()
    {
        EnsureReferences();

        if (_groundTilemap == null)
        {
            Debug.LogError("<color=red>[TilemapLevelEditor]</color> GroundTilemap 未配置。");
            return false;
        }

        string stageId = _stageId;
        if (string.IsNullOrEmpty(stageId))
        {
            Debug.LogError("<color=red>[TilemapLevelEditor]</color> StageID 为空，无法保存。");
            return false;
        }

        int safeWidth = Mathf.Max(1, _width);
        int safeHeight = Mathf.Max(1, _height);

        var data = new LevelMapData
        {
            levelId = stageId,
            width = safeWidth,
            height = safeHeight,
            originX = _originX,
            originY = _originY,
            groundMap = BakeGroundLayer(safeWidth, safeHeight),
            gridMap = new int[safeWidth * safeHeight],
            effectMap = new int[safeWidth * safeHeight]
        };

        Directory.CreateDirectory(LevelDirectory);
        string assetPath = GetLevelAssetPath(stageId);
        File.WriteAllText(assetPath, JsonUtility.ToJson(data, true));

        _loadedExistingTemplate = true;
        EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
        {
            EditorSceneManager.SaveScene(gameObject.scene);
        }

        AssetDatabase.Refresh();
        Debug.Log($"<color=green>[TilemapLevelEditor]</color> 已保存关卡模板: {assetPath}");
        return true;
    }

    public void SaveAndReturnToBigMap()
    {
        if (!SaveLevel())
        {
            return;
        }

        if (!EditorSessionBridge.TryGetSession(out var session))
        {
            Debug.LogWarning("<color=orange>[TilemapLevelEditor]</color> 未找到返回会话，仅执行保存。");
            return;
        }

        EditorSessionBridge.MarkReturning();
        if (session.SceneSetup != null && session.SceneSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(session.SceneSetup);
            EditorApplication.delayCall += BigMapEditorWindow.FocusExistingWindow;
            return;
        }

        if (string.IsNullOrEmpty(session.ReturnScenePath))
        {
            Debug.LogWarning("<color=orange>[TilemapLevelEditor]</color> ReturnScenePath 为空，无法自动返回 BigMap。");
            return;
        }

        var returnScene = EditorSceneManager.OpenScene(session.ReturnScenePath, OpenSceneMode.Additive);
        if (returnScene.IsValid())
        {
            EditorSceneManager.SetActiveScene(returnScene);
        }

        var currentScene = gameObject.scene;
        if (currentScene.IsValid())
        {
            EditorSceneManager.CloseScene(currentScene, true);
        }

        EditorApplication.delayCall += BigMapEditorWindow.FocusExistingWindow;
    }

    public void LoadSessionIfNeeded()
    {
        if (!EditorSessionBridge.TryGetSession(out var session))
        {
            return;
        }

        bool shouldReload = !_sessionLoaded || _stageId != session.StageID;
        if (!shouldReload)
        {
            return;
        }

        _sessionLoaded = true;
        _stageId = session.StageID;

        LoadLevelTemplate();
        EditorUtility.SetDirty(this);
    }

    public void OverrideStage(string stageId)
    {
        if (string.IsNullOrEmpty(stageId))
        {
            return;
        }

        _sessionLoaded = true;
        _stageId = stageId;
        LoadLevelTemplate();
        EditorUtility.SetDirty(this);
    }

    public void LoadLevelTemplate()
    {
        EnsureReferences();

        string assetPath = GetLevelAssetPath(_stageId);
        if (!File.Exists(assetPath))
        {
            _loadedExistingTemplate = false;
            ResetToBlankTemplate();
            Debug.Log($"<color=yellow>[TilemapLevelEditor]</color> 未找到模板，初始化为空白地图: {assetPath}");
            return;
        }

        string json = File.ReadAllText(assetPath);
        var levelData = JsonUtility.FromJson<LevelMapData>(json);
        if (levelData == null)
        {
            Debug.LogError($"<color=red>[TilemapLevelEditor]</color> 关卡 JSON 解析失败: {assetPath}");
            return;
        }

        _width = Mathf.Max(1, levelData.width);
        _height = Mathf.Max(1, levelData.height);
        _originX = levelData.originX;
        _originY = levelData.originY;
        _loadedExistingTemplate = true;

        ApplyLevelDataToTilemap(levelData);
        Debug.Log($"<color=cyan>[TilemapLevelEditor]</color> 已加载模板: {assetPath}");
    }

    private void ResetToBlankTemplate()
    {
        _width = Mathf.Max(1, _width);
        _height = Mathf.Max(1, _height);
        _groundTilemap?.ClearAllTiles();
    }

    private void EnsureReferences()
    {
        if (_grid == null)
        {
            _grid = GetComponent<Grid>();
        }

        if (_groundTilemap == null)
        {
            _groundTilemap = GetComponentInChildren<Tilemap>();
        }
    }

    private string GetLevelAssetPath(string stageId)
    {
        return Path.Combine(LevelDirectory, $"{stageId}.json");
    }

    private int[] BakeGroundLayer(int safeWidth, int safeHeight)
    {
        int[] result = new int[safeWidth * safeHeight];

        for (int y = 0; y < safeHeight; y++)
        {
            for (int x = 0; x < safeWidth; x++)
            {
                Vector3Int cell = new Vector3Int(_originX + x, _originY + y, 0);
                TileBase tile = _groundTilemap.GetTile(cell);
                int id = ResolveTileId(tile, cell);
                result[y * safeWidth + x] = id;
            }
        }

        return result;
    }

    private int ResolveTileId(TileBase tile, Vector3Int cell)
    {
        if (tile == null)
        {
            return 0;
        }

        if (_tileMappingConfig == null)
        {
            Debug.LogError("<color=red>[TilemapLevelEditor]</color> TileMappingConfig 未配置。");
            return 0;
        }

        int id = _tileMappingConfig.GetTileID(tile);
        if (id != 0)
        {
            return id;
        }

        Debug.LogError($"<color=red>[TilemapLevelEditor]</color> Tile 未注册 ID: {tile.name} @ {cell}");
        return 0;
    }

    private void ApplyLevelDataToTilemap(LevelMapData levelData)
    {
        if (_groundTilemap == null)
        {
            return;
        }

        _groundTilemap.ClearAllTiles();
        if (levelData.groundMap == null)
        {
            return;
        }

        for (int y = 0; y < levelData.height; y++)
        {
            for (int x = 0; x < levelData.width; x++)
            {
                int tileId = levelData.groundMap[y * levelData.width + x];
                if (tileId == 0)
                {
                    continue;
                }

                TileBase tileAsset = ResolveTileAsset(tileId);
                if (tileAsset == null)
                {
                    continue;
                }

                Vector3Int cell = new Vector3Int(levelData.originX + x, levelData.originY + y, 0);
                _groundTilemap.SetTile(cell, tileAsset);
            }
        }
    }

    private TileBase ResolveTileAsset(int tileId)
    {
        if (_tileMappingConfig == null)
        {
            Debug.LogWarning("<color=orange>[TilemapLevelEditor]</color> TileMappingConfig 未配置，无法回填 Tilemap。");
            return null;
        }

        TileBase tileAsset = _tileMappingConfig.GetTileAsset(tileId);
        if (tileAsset == null)
        {
            Debug.LogWarning($"<color=orange>[TilemapLevelEditor]</color> 未找到 TileID={tileId} 对应的 TileAsset。");
        }

        return tileAsset;
    }
}

[CustomEditor(typeof(TilemapLevelEditor))]
public class TilemapLevelEditorInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var levelEditor = (TilemapLevelEditor)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("当前编辑关卡", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("StageID", string.IsNullOrEmpty(levelEditor.StageID) ? "(未绑定)" : levelEditor.StageID);
        EditorGUILayout.LabelField("关卡文件", string.IsNullOrEmpty(levelEditor.StageID) ? "(未绑定)" : $"Resources/Levels/{levelEditor.StageID}.json");
        EditorGUILayout.LabelField("状态", levelEditor.LoadedExistingTemplate ? "已加载现有模板" : "新建空白模板");
        if (!levelEditor.HasTileMappingConfig)
        {
            EditorGUILayout.HelpBox("TileMappingConfig 未配置，已有 JSON 无法正确回填到 Tilemap。", MessageType.Warning);
        }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("保存", GUILayout.Height(28)))
        {
            levelEditor.SaveLevel();
        }

        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.66f, 0.92f, 0.95f);
        if (GUILayout.Button("保存并返回 BigMap", GUILayout.Height(34)))
        {
            levelEditor.SaveAndReturnToBigMap();
        }
        GUI.backgroundColor = previousColor;

        if (GUILayout.Button("重新读取当前模板", GUILayout.Height(24)))
        {
            levelEditor.LoadLevelTemplate();
        }
    }
}

public static class TilemapLevelEditorSceneUtility
{
    public static string EnsureSceneExists(string scenePath)
    {
        if (File.Exists(scenePath))
        {
            return scenePath;
        }

        string directory = Path.GetDirectoryName(scenePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.orthographic = true;
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        var gridGo = new GameObject("LevelGrid");
        var grid = gridGo.AddComponent<Grid>();

        var groundGo = new GameObject("GroundTilemap");
        groundGo.transform.SetParent(gridGo.transform);
        var groundTilemap = groundGo.AddComponent<Tilemap>();
        groundGo.AddComponent<TilemapRenderer>();

        var editorRoot = new GameObject("TilemapLevelEditor");
        var tilemapEditor = editorRoot.AddComponent<TilemapLevelEditor>();
        var serializedObject = new SerializedObject(tilemapEditor);
        serializedObject.FindProperty("_grid").objectReferenceValue = grid;
        serializedObject.FindProperty("_groundTilemap").objectReferenceValue = groundTilemap;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
        return scenePath;
    }
}
