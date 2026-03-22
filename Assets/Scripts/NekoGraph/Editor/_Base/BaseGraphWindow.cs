#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// 统一 Pack 编辑器窗口 - 非泛型，文件驱动喵~
/// 打开任意 Pack JSON，自动调用对应 SearchWindow，无需子类喵~
/// </summary>
public class PackWindow : EditorWindow
{
    private BaseGraphView _graphView;
    private VisualElement _viewContainer;
    private TextField _packIDField;
    private EnumField _accessLevelField;
    private BasePackData _currentPack;
    private string _currentFilePath;

    [MenuItem("NekoGraph/✨ 打开 Pack 编辑器")]
    public static void Open()
    {
        var w = CreateWindow<PackWindow>();
        w.titleContent = new GUIContent("Pack Editor");
        w.Show();
    }

    private void OnEnable() => BuildLayout();

    private void OnDisable()
    {
        if (_graphView != null)
            _viewContainer?.Remove(_graphView);
    }

    private void BuildLayout()
    {
        rootVisualElement.Clear();
        GenerateToolbar();

        _viewContainer = new VisualElement { name = "ViewContainer" };
        _viewContainer.style.flexGrow = 1;
        rootVisualElement.Add(_viewContainer);

        _graphView = new BaseGraphView { name = "NekoGraph" };
        _graphView.StretchToParentSize();
        _viewContainer.Add(_graphView);
    }

    private void GenerateToolbar()
    {
        var toolbar = new Toolbar();

        _packIDField = new TextField("PackID:")
        {
            name = "PackIDField",
            tooltip = "Pack 的唯一 ID 喵~",
            maxLength = 64,
            value = ""
        };
        _packIDField.style.width = 200;
        _packIDField.style.marginLeft = 5;
        _packIDField.style.marginRight = 5;
        if (_packIDField.labelElement != null)
            _packIDField.labelElement.style.marginRight = -8;
        _packIDField.RegisterValueChangedCallback(evt => _graphView?.SetPackID(evt.newValue));
        toolbar.Add(_packIDField);

        _accessLevelField = new EnumField(PackAccessLevel.ReadOnly)
        {
            tooltip = "玩家访问权限喵~",
        };
        _accessLevelField.style.width = 110;
        _accessLevelField.style.marginRight = 5;
        _accessLevelField.RegisterValueChangedCallback(evt =>
        {
            if (_currentPack != null)
                _currentPack.AccessLevel = (PackAccessLevel)evt.newValue;
        });
        toolbar.Add(_accessLevelField);

        toolbar.Add(new ToolbarSpacer());
        toolbar.Add(new Button(SaveData) { text = "💾 保存" });
        toolbar.Add(new Button(LoadData) { text = "📂 读取" });

        rootVisualElement.Add(toolbar);
    }

    #region Load / Save

    private void LoadData()
    {
        string path = EditorUtility.OpenFilePanel("读取 Pack", "Assets/Resources", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception e) { EditorUtility.DisplayDialog("读取失败", e.Message, "确定"); return; }

        BasePackData pack;
        try { pack = BasePackData.FromJson(json); }
        catch (Exception e) { EditorUtility.DisplayDialog("读取失败", $"JSON 格式错误：{e.Message}", "确定"); return; }

        if (pack == null) { EditorUtility.DisplayDialog("读取失败", "文件内容为空喵~", "确定"); return; }

        _currentPack = pack;
        _currentFilePath = path;

        _graphView.PopulateFromPack(pack);

        string id = !string.IsNullOrEmpty(pack.PackID) ? pack.PackID : Path.GetFileNameWithoutExtension(path);
        _packIDField.SetValueWithoutNotify(id);
        _graphView.SetPackID(id);
        _accessLevelField.SetValueWithoutNotify(pack.AccessLevel);

        SetupSearchWindow(pack);

        titleContent = new GUIContent($"Pack [{id}]");
        Debug.Log($"[PackWindow] 已加载: {path}");
    }

    private void SaveData()
    {
        if (_currentPack == null)
        {
            EditorUtility.DisplayDialog("保存失败", "请先读取一个 Pack 喵~", "确定");
            return;
        }
        if (string.IsNullOrWhiteSpace(_packIDField.value))
        {
            EditorUtility.DisplayDialog("保存失败", "PackID 不能为空！", "确定");
            return;
        }

        string defaultDir = string.IsNullOrEmpty(_currentFilePath)
            ? "Assets/Resources"
            : Path.GetDirectoryName(_currentFilePath);
        string path = EditorUtility.SaveFilePanel("保存 Pack", defaultDir, $"{_packIDField.value}.json", "json");
        if (string.IsNullOrEmpty(path)) return;

        _graphView.FlushToPack(_currentPack);
        File.WriteAllText(path, _currentPack.ToJson());
        _currentFilePath = path;

        RegisterToMetaLib(path, _currentPack);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("保存成功", $"已保存至：\n{path}", "确定");
        Debug.Log($"[PackWindow] 已保存: {path}");
    }

    #endregion

    #region SearchWindow

    private void SetupSearchWindow(BasePackData pack)
    {
        var provider = ScriptableObject.CreateInstance<NodeSearchWindow>();
        provider.Initialize(this, _graphView, pack);

        _graphView.nodeCreationRequest = context =>
        {
            var method = typeof(SearchWindow).GetMethod("Open", BindingFlags.Static | BindingFlags.Public);
            method?.MakeGenericMethod(typeof(NodeSearchWindow)).Invoke(null, new object[]
            {
                new SearchWindowContext(context.screenMousePosition), provider
            });
        };
    }

    #endregion

    #region MetaLib

    private void RegisterToMetaLib(string fullPath, BasePackData pack)
    {
        if (string.IsNullOrEmpty(pack.PackID))
        {
            EditorUtility.DisplayDialog("注册失败", "PackID 不能为空！", "好的");
            return;
        }
        string fileName = Path.GetFileNameWithoutExtension(fullPath);
        if (fileName != pack.PackID)
        {
            EditorUtility.DisplayDialog("文件名错误",
                $"文件名必须与 PackID 一致喵~\n\nPackID：'{pack.PackID}'\n文件名：'{fileName}'", "好的");
            return;
        }

        var (storageType, resourcePath) = GetStorageInfo(fullPath);
        if (MetaLib.HasMeta(pack.PackID))
        {
            var existing = MetaLib.GetMeta(pack.PackID);
            if (existing.ResourcePath != resourcePath || existing.Storage != storageType)
            {
                EditorUtility.DisplayDialog("PackID 已被占用",
                    $"PackID '{pack.PackID}' 已被 '{existing.ResourcePath}' 使用喵~", "好的");
                return;
            }
        }

        var meta = new MetaLib.MetaEntry
        {
            PackID = pack.PackID,
            Storage = storageType,
            ResourcePath = resourcePath,
            GraphType = pack.GetType().Name.Replace("PackData", ""),
            DisplayName = !string.IsNullOrEmpty(pack.DisplayName) ? pack.DisplayName : pack.PackID,
            Author = "NekoTeam",
            Version = "1.0.0"
        };
        MetaLib.Register(pack.PackID, meta);
        MetaLib.Save();
        Debug.Log($"[MetaLib] 已注册：{pack.PackID} -> {meta.ResourcePath}");
    }

    private static (MetaLib.StorageType, string) GetStorageInfo(string fullPath)
    {
        string assetsPath = Application.dataPath.Replace('\\', '/');
        fullPath = fullPath.Replace('\\', '/');
        if (fullPath.StartsWith(assetsPath))
        {
            if (fullPath.Contains("/Resources/"))
            {
                int idx = fullPath.IndexOf("/Resources/") + "/Resources/".Length;
                return (MetaLib.StorageType.Resources, Path.ChangeExtension(fullPath[idx..], null));
            }
            if (fullPath.Contains("/StreamingAssets/"))
            {
                int idx = fullPath.IndexOf("/StreamingAssets/") + "/StreamingAssets/".Length;
                return (MetaLib.StorageType.StreamingAssets, fullPath[idx..]);
            }
        }
        Debug.LogWarning($"[PackWindow] 文件不在 Resources 或 StreamingAssets 内：{fullPath}");
        return (MetaLib.StorageType.Resources, Path.GetFileNameWithoutExtension(fullPath));
    }

    #endregion

    public BaseGraphView GetGraphView() => _graphView;

    public Vector2 ScreenToLocal(Vector2 screenPosition)
    {
        var local = rootVisualElement.ChangeCoordinatesTo(rootVisualElement.parent, screenPosition);
        return rootVisualElement.WorldToLocal(local);
    }
}
#endif
