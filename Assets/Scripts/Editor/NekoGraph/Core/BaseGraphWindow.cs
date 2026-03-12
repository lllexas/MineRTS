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
    /// 保存数据喵~
    /// </summary>
    protected virtual void SaveData()
    {
        string path = EditorUtility.SaveFilePanel(
            "保存数据",
            FileDirectory,
            DefaultFileName,
            FileExtension);

        if (string.IsNullOrEmpty(path)) return;

        var pack = GraphView.SerializeToPack();
        if (pack == null)
        {
            EditorUtility.DisplayDialog("保存失败", "数据验证失败，请检查是否符合规则喵~", "确定");
            return;
        }

        SaveToFile(path, pack);
        AssetDatabase.Refresh();

        Debug.Log($"[NekoGraph] 保存成功：{path}");
        EditorUtility.DisplayDialog("保存成功", $"数据已保存至：\n{path}", "确定");
    }

    /// <summary>
    /// 加载数据喵~
    /// </summary>
    protected virtual void LoadData()
    {
        string path = EditorUtility.OpenFilePanel(
            "读取数据",
            FileDirectory,
            FileExtension);

        if (string.IsNullOrEmpty(path)) return;

        var pack = LoadFromFile(path);
        if (pack == null)
        {
            EditorUtility.DisplayDialog("读取失败", "JSON 格式错误或文件损坏喵~", "确定");
            return;
        }

        GraphView.PopulateFromPack(pack);
        CurrentFilePath = path;

        Debug.Log($"[NekoGraph] 读取成功：{path}");
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
