#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using NekoGraph;
using Newtonsoft.Json;

/// <summary>
/// 通用 Graph 窗口基类 - 封装 EditorWindow 的通用逻辑喵~
/// 仅编辑器使用喵~
/// </summary>
public abstract class BaseGraphWindow<TView, TPack> : EditorWindow
    where TView : BaseGraphView<TPack>, new()
    where TPack : BasePackData, new()
{
    /// <summary>
    /// GraphView 引用喵~
    /// </summary>
    protected TView GraphView;

    /// <summary>
    /// 公共 GraphView 访问器喵~
    /// </summary>
    public TView GetGraphView() => GraphView;

    /// <summary>
    /// SearchWindow 提供者引用喵~
    /// </summary>
    protected ScriptableObject SearchWindowProvider;

    /// <summary>
    /// 当前文件路径喵~
    /// </summary>
    protected string CurrentFilePath;

    /// <summary>
    /// 窗口标题喵~
    /// </summary>
    protected virtual string WindowTitle => "NekoGraph Editor";

    /// <summary>
    /// 默认文件名喵~
    /// </summary>
    protected virtual string DefaultFileName => "New_Graph.json";

    /// <summary>
    /// 文件扩展名喵~
    /// </summary>
    protected virtual string FileExtension => "json";

    /// <summary>
    /// 文件目录（相对于 Assets）喵~
    /// </summary>
    protected virtual string FileDirectory => "Assets/Resources";

    /// <summary>
    /// 当前系统类型喵~
    /// 子类重写此属性以指定所属系统，用于过滤节点类型喵~
    /// </summary>
    protected virtual NodeSystem CurrentNodeSystem => NodeSystem.Common;

    /// <summary>
    /// PackID 输入框喵~
    /// </summary>
    protected TextField PackIDField;

    /// <summary>
    /// 窗口启用时调用喵~
    /// </summary>
    protected virtual void OnEnable()
    {
        ConstructGraphView();
        GenerateToolbar();
    }

    /// <summary>
    /// 窗口禁用时调用喵~
    /// </summary>
    protected virtual void OnDisable()
    {
        if (GraphView != null)
        {
            rootVisualElement.Remove(GraphView);
        }
    }

    /// <summary>
    /// 构建 GraphView 喵~
    /// </summary>
    protected virtual void ConstructGraphView()
    {
        // 创建 SearchWindow（如果子类提供了的话）
        SearchWindowProvider = CreateSearchWindow();

        // 创建 GraphView
        GraphView = new TView { name = GetGraphViewName() };

        if (SearchWindowProvider != null)
        {
            SetupSearchWindow();
        }

        GraphView.StretchToParentSize();
        rootVisualElement.Add(GraphView);

        // 初始化完成后调用
        OnGraphViewConstructed();
    }

    /// <summary>
    /// 获取 GraphView 名称喵~
    /// </summary>
    protected virtual string GetGraphViewName() => "NekoGraph";

    /// <summary>
    /// 创建 SearchWindow 提供者喵~
    /// </summary>
    protected virtual ScriptableObject CreateSearchWindow() => null;

    /// <summary>
    /// 设置 SearchWindow 提供者喵~
    /// </summary>
    protected virtual void SetupSearchWindow()
    {
        // 默认实现，子类可以重写
        if (SearchWindowProvider is ISearchWindowProvider provider)
        {
            GraphView.nodeCreationRequest = context =>
            {
                // 使用反射调用 SearchWindow.Open，避免泛型约束问题喵~
                var method = typeof(SearchWindow).GetMethod("Open",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(SearchWindowProvider.GetType());
                    genericMethod.Invoke(null, new object[] {
                        new SearchWindowContext(context.screenMousePosition),
                        SearchWindowProvider
                    });
                }
            };
        }
    }

    /// <summary>
    /// GraphView 构建完成回调喵~
    /// </summary>
    protected virtual void OnGraphViewConstructed()
    {
        // 默认实现，子类重写
    }

    /// <summary>
    /// 生成工具栏喵~
    /// </summary>
    protected virtual void GenerateToolbar()
    {
        var toolbar = new Toolbar();

        // 添加 PackID 输入框喵~
        PackIDField = new TextField("PackID:")
        {
            name = "PackIDField",
            tooltip = "输入 Pack 的唯一 ID，用于代码引用喵~",
            maxLength = 64,
            value = ""
        };
        // 设置样式喵~
        PackIDField.style.width = 200;
        PackIDField.style.marginLeft = 5;
        PackIDField.style.marginRight = 5;
        
        // 调整 label 和输入框之间的间距喵~（使用负边距拉近）
        if (PackIDField.labelElement != null)
        {
            PackIDField.labelElement.style.marginRight = -8;
        }

        PackIDField.RegisterValueChangedCallback(evt =>
        {
            // 实时同步 PackID 到 GraphView
            if (GraphView != null)
            {
                GraphView.SetPackID(evt.newValue);
            }
        });
        toolbar.Add(PackIDField);

        // 添加分隔符
        toolbar.Add(new ToolbarSpacer());

        // 添加默认按钮
        AddDefaultToolbarButtons(toolbar);

        // 调用子类的自定义按钮添加（如果有的话）
        AddCustomButtons(toolbar);

        rootVisualElement.Add(toolbar);
    }

    /// <summary>
    /// 添加默认工具栏按钮喵~
    /// </summary>
    protected virtual void AddDefaultToolbarButtons(Toolbar toolbar)
    {
        // 保存按钮
        toolbar.Add(new Button(SaveData) { text = "💾 保存" });
        toolbar.Add(new Button(LoadData) { text = "📂 读取" });
    }

    /// <summary>
    /// 添加自定义工具栏按钮喵~
    /// 子类可以重写此方法添加自己的按钮（用于全局操作，如自动布局、验证等）喵~
    /// </summary>
    protected virtual void AddCustomButtons(Toolbar toolbar)
    {
        // 默认实现，子类重写
    }

    /// <summary>
    /// 获取当前系统可用的节点类型列表喵~
    /// 使用 NodeTypeHelper 静态辅助类获取喵~
    /// </summary>
    protected List<NekoGraph.Editor.NodeTypeInfo> GetNodeTypesForCurrentSystem()
    {
        return NekoGraph.Editor.NodeTypeHelper.GetNodeTypesForSystem(CurrentNodeSystem);
    }

    #region Save / Load

    /// <summary>
    /// 当前保存路径喵~（子类可访问）
    /// </summary>
    protected string CurrentSavePath;

    /// <summary>
    /// 保存数据喵~（模板方法模式）
    /// </summary>
    protected virtual void SaveData()
    {
        // 1. 保存前验证
        if (!OnBeforeSave()) return;

        // 2. 序列化 Pack
        var pack = SerializePack();
        if (pack == null) return;

        // 3. 保存到文件
        SaveToFile(CurrentSavePath, pack);

        // 4. 注册到 MetaLib
        RegisterToMetaLib(CurrentSavePath, pack);

        // 5. 保存完成回调
        OnAfterSave(CurrentSavePath);
    }

    /// <summary>
    /// 保存前验证喵~（子类可重写）
    /// 返回 false 则取消保存
    /// </summary>
    protected virtual bool OnBeforeSave()
    {
        // 验证 PackID 不能为空喵~
        if (string.IsNullOrWhiteSpace(PackIDField.value))
        {
            EditorUtility.DisplayDialog("保存失败", "PackID 不能为空！\n请在工具栏中输入 PackID 喵~", "确定");
            return false;
        }

        // 使用 PackID 作为文件名喵~（方便协作和识别）
        string defaultFileName = $"{PackIDField.value}.{FileExtension}";

        CurrentSavePath = EditorUtility.SaveFilePanel(
            "保存数据",
            FileDirectory,
            defaultFileName,
            FileExtension);

        if (string.IsNullOrEmpty(CurrentSavePath)) return false;

        return true;
    }

    /// <summary>
    /// 序列化 Pack 喵~（子类可重写）
    /// </summary>
    protected virtual TPack SerializePack()
    {
        var pack = GraphView.SerializeToPack();
        if (pack == null)
        {
            EditorUtility.DisplayDialog("保存失败", "数据验证失败，请检查是否符合规则喵~", "确定");
        }
        return pack;
    }

    /// <summary>
    /// 保存完成回调喵~（子类可重写）
    /// </summary>
    protected virtual void OnAfterSave(string path)
    {
        AssetDatabase.Refresh();
        Debug.Log($"[NekoGraph] 保存成功：{path}");
        EditorUtility.DisplayDialog("保存成功", $"数据已保存至：\n{path}", "确定");
    }

    /// <summary>
    /// 自动注册到 MetaLib 喵~
    /// </summary>
    private void RegisterToMetaLib(string path, TPack pack)
    {
        // 计算 Resources 相对路径（去掉 Assets/Resources/ 和扩展名）
        // ================== MetaLib 自动注册 ==================
        if (string.IsNullOrEmpty(pack.PackID))
        {
            UnityEditor.EditorUtility.DisplayDialog("注册失败", "PackID 不能为空！\n请在工具栏中输入 PackID 喵~", "好的");
            return;
        }

        // 1. 检查文件名是否与 PackID 一致喵~（强制要求，不一致直接拒绝）
        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (fileName != pack.PackID)
        {
            UnityEditor.EditorUtility.DisplayDialog("文件名错误",
                $"文件名必须与 PackID 一致喵~\n\n" +
                $"当前 PackID：'{pack.PackID}'\n" +
                $"当前文件名：'{fileName}'\n\n" +
                "请将文件名改为与 PackID 一致后重新保存。",
                "好的");
            return; // 直接拒绝保存
        }

        // 2. PackID 唯一性校验喵~（冲突直接拒绝，避免制造幽灵文件）
        var (storageType, resourcePath) = GetStorageInfo(path);
        if (MetaLib.HasMeta(pack.PackID))
        {
            var existingMeta = MetaLib.GetMeta(pack.PackID);
            if (existingMeta.ResourcePath != resourcePath || existingMeta.Storage != storageType)
            {
                // ID 冲突：另一个文件已经使用了这个 PackID
                // 不能覆盖注册，否则老文件会变成幽灵文件
                UnityEditor.EditorUtility.DisplayDialog("PackID 已被占用",
                    $"PackID '{pack.PackID}' 已被另一个文件使用喵~\n\n" +
                    $"已存在的文件：'{existingMeta.ResourcePath}'\n\n" +
                    "为了避免制造幽灵文件（注册被覆盖但文件还在磁盘上的垃圾文件），\n" +
                    "请使用其他 PackID，或者先删除老文件。",
                    "好的");
                return; // 直接拒绝保存
            }
            // 如果路径相同，说明是同一个文件，直接更新注册信息（正常保存流程）
        }

        // 3. 构造 MetaEntry
        var meta = new MetaLib.MetaEntry
        {
            PackID = pack.PackID,
            Storage = storageType,
            ResourcePath = resourcePath,
            GraphType = GetGraphTypeName(),
            DisplayName = !string.IsNullOrEmpty(pack.DisplayName) ? pack.DisplayName : pack.PackID,
            Author = "NekoTeam",
            Version = "1.0.0"
        };

        // 4. 注册到 MetaLib
        MetaLib.Register(pack.PackID, meta);

        // 5. 保存 MetaLib
        MetaLib.Save();

        Debug.Log($"[MetaLib] 已自动注册/更新：{pack.PackID} -> {meta.ResourcePath} ({meta.Storage})");
    }

        /// <summary>
        /// 根据完整路径解析存储类型和资源路径喵~
        /// </summary>
        private (MetaLib.StorageType, string) GetStorageInfo(string fullPath)
        {
        string assetsPath = Application.dataPath;
        // 规范化路径分隔符
        fullPath = fullPath.Replace('\\', '/');
        assetsPath = assetsPath.Replace('\\', '/');

        if (fullPath.StartsWith(assetsPath))
        {
            // In Resources Folder
            if (fullPath.Contains("/Resources/"))
            {
                int resourcesIndex = fullPath.IndexOf("/Resources/") + "/Resources/".Length;
                string resPath = fullPath.Substring(resourcesIndex);
                resPath = System.IO.Path.ChangeExtension(resPath, null);
                return (MetaLib.StorageType.Resources, resPath);
            }

            // In StreamingAssets Folder
            if (fullPath.Contains("/StreamingAssets/"))
            {
                int streamingAssetsIndex = fullPath.IndexOf("/StreamingAssets/") + "/StreamingAssets/".Length;
                string saPath = fullPath.Substring(streamingAssetsIndex);
                return (MetaLib.StorageType.StreamingAssets, saPath);
            }
        }

        Debug.LogWarning($"[BaseGraphWindow] 文件未保存在 Resources 或 StreamingAssets 文件夹中，路径解析可能不准：{fullPath}");
        // 默认回退行为
        string fallbackPath = System.IO.Path.GetFileNameWithoutExtension(fullPath);
        return (MetaLib.StorageType.Resources, fallbackPath);
        }


    /// <summary>
    /// 获取图类型的显示名称喵~
    /// </summary>
    protected virtual string GetGraphTypeName()
    {
        // 从 TPack 类型推断
        var typeName = typeof(TPack).Name;
        if (typeName.Contains("LabTech")) return "Lab";
        if (typeName.Contains("Mission")) return "Mission";
        if (typeName.Contains("Story")) return "Story";
        return "Generic";
    }

    /// <summary>
    /// 加载数据喵~（模板方法模式）
    /// </summary>
    protected virtual void LoadData()
    {
        // 1. 打开文件对话框
        string path = OnBeforeLoad();
        if (string.IsNullOrEmpty(path)) return;

        // 2. 加载文件
        var pack = LoadFromFile(path);
        if (pack == null)
        {
            EditorUtility.DisplayDialog("读取失败", "JSON 格式错误或文件损坏喵~", "确定");
            return;
        }

        // 3. 填充画布
        GraphView.PopulateFromPack(pack);
        CurrentFilePath = path;

        // 4. 同步 PackID 到输入框喵~
        SyncPackIDToField(pack, path);

        // 5. 完成回调
        OnLoadCompleted(path);
    }

    /// <summary>
    /// 加载前处理喵~（子类可重写）
    /// 返回文件路径，如果返回空则取消加载
    /// </summary>
    protected virtual string OnBeforeLoad()
    {
        return EditorUtility.OpenFilePanel("读取数据", FileDirectory, FileExtension);
    }

    /// <summary>
    /// 同步 PackID 到输入框喵~
    /// </summary>
    protected void SyncPackIDToField(TPack pack, string path)
    {
        if (!string.IsNullOrEmpty(pack.PackID))
        {
            PackIDField.SetValueWithoutNotify(pack.PackID);
            GraphView.SetPackID(pack.PackID);
        }
        else
        {
            // 如果 PackID 为空，尝试从文件名推断喵~
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            PackIDField.SetValueWithoutNotify(fileName);
            GraphView.SetPackID(fileName);
            Debug.LogWarning($"[NekoGraph] 文件 {path} 的 PackID 为空，已使用文件名 '{fileName}' 作为 PackID 喵~");
        }
    }

    /// <summary>
    /// 加载完成回调喵~（子类可重写）
    /// </summary>
    protected virtual void OnLoadCompleted(string path)
    {
        Debug.Log($"[NekoGraph] 读取成功：{path} (PackID: {PackIDField.value})");
    }

    /// <summary>
    /// 保存到文件喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// </summary>
    protected virtual void SaveToFile(string path, TPack pack)
    {
        string json = JsonConvert.SerializeObject(pack, BaseGraphView<TPack>.GraphJsonSettings);
        System.IO.File.WriteAllText(path, json);
        CurrentFilePath = path;
    }

    /// <summary>
    /// 从文件加载喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// </summary>
    protected virtual TPack LoadFromFile(string path)
    {
        string json = System.IO.File.ReadAllText(path);
        return JsonConvert.DeserializeObject<TPack>(json, BaseGraphView<TPack>.GraphJsonSettings);
    }

    #endregion

    #region Search Window Helpers

    /// <summary>
    /// 打开 SearchWindow 喵~
    /// </summary>
    protected void OpenSearchWindow(Vector2 screenPosition)
    {
        if (SearchWindowProvider is ISearchWindowProvider provider)
        {
            // 使用反射调用 SearchWindow.Open，避免泛型约束问题喵~
            var method = typeof(SearchWindow).GetMethod("Open",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (method != null)
            {
                var genericMethod = method.MakeGenericMethod(SearchWindowProvider.GetType());
                genericMethod.Invoke(null, new object[] {
                    new SearchWindowContext(screenPosition),
                    SearchWindowProvider
                });
            }
        }
    }

    /// <summary>
    /// 将屏幕坐标转换为本地坐标喵~
    /// </summary>
    public Vector2 ScreenToLocal(Vector2 screenPosition)
    {
        var windowMousePosition = rootVisualElement.ChangeCoordinatesTo(
            rootVisualElement.parent, screenPosition);
        return rootVisualElement.WorldToLocal(windowMousePosition);
    }

    #endregion
}
#endif
