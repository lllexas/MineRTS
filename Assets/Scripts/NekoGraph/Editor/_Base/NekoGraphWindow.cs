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
/// 万能集约化 NekoGraph 窗口喵~
/// 支持多实例、多系统切换，是 NekoGraph 的终极编辑器喵！
/// </summary>
public class NekoGraphWindow : EditorWindow
{
    [SerializeField]
    private NodeSystem _currentNodeSystem = NodeSystem.Common;

    private INekoGraphView _graphView;
    private VisualElement _viewContainer;
    private ToolbarMenu _systemMenu;

    /// <summary>
    /// 打开一个新的万能窗口喵~
    /// </summary>
    [MenuItem("NekoGraph/✨ 打开新编辑器窗口")]
    public static void OpenNewWindow()
    {
        var window = CreateWindow<NekoGraphWindow>();
        window.titleContent = new GUIContent("NekoGraph Editor");
        window.Show();
    }

    private void OnEnable()
    {
        ConstructWindow();
        UpdateTitle();
    }

    private void ConstructWindow()
    {
        rootVisualElement.Clear();
        
        // 关键修复：利用 Flexbox 布局，让 Toolbar 和 ViewContainer 上下排列喵！
        // 这样 Toolbar 就不会被遮挡了~
        
        // 1. 生成工具栏喵~
        GenerateToolbar();

        // 2. 生成画布容器喵~
        _viewContainer = new VisualElement { name = "ViewContainer" };
        // _viewContainer.StretchToParentSize(); // ❌ 绝对定位会覆盖 Toolbar，千万别用喵！
        _viewContainer.style.flexGrow = 1;       // ✅ 使用 FlexGrow 占据剩余空间喵！
        rootVisualElement.Add(_viewContainer);

        // 3. 构造初始画布喵~
        SwitchSystem(_currentNodeSystem);
    }

    private void GenerateToolbar()
    {
        var toolbar = new Toolbar();

        // --- 系统切换菜单 ---
        _systemMenu = new ToolbarMenu { text = $"系统: {_currentNodeSystem}" };
        
        foreach (NodeSystem system in Enum.GetValues(typeof(NodeSystem)))
        {
            // 【关键修复】创建循环变量的本地副本，防止闭包捕获导致所有菜单项指向同一个值喵！
            var targetSystem = system; 

            _systemMenu.menu.AppendAction(
                targetSystem.ToString(), 
                (a) => SwitchSystem(targetSystem), // 点击回调
                (a) => targetSystem == _currentNodeSystem ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal // 状态回调（打勾）
            );
        }
        toolbar.Add(_systemMenu);

        toolbar.Add(new ToolbarSpacer());

        // --- 基础文件操作 ---
        toolbar.Add(new Button(SaveData) { text = "💾 保存" });
        toolbar.Add(new Button(LoadData) { text = "📂 读取" });

        toolbar.Add(new ToolbarSpacer());

        // --- 扩展按钮区域 ---
        var customContainer = new VisualElement { name = "CustomButtons", style = { flexDirection = FlexDirection.Row } };
        toolbar.Add(customContainer);

        rootVisualElement.Add(toolbar);
    }

    /// <summary>
    /// 切换系统喵~
    /// </summary>
    public void SwitchSystem(NodeSystem system)
    {
        _currentNodeSystem = system;
        _systemMenu.text = $"系统: {system}";
        UpdateTitle();

        // 清理旧画布喵~
        _viewContainer.Clear();

        // 创建新画布喵~
        _graphView = CreateGraphView(system);
        if (_graphView is GraphView gv)
        {
            gv.StretchToParentSize();
            _viewContainer.Add(gv);

            // 绑定 SearchWindow 喵~
            SetupSearchWindow(gv, system);
        }
    }

    private void UpdateTitle()
    {
        titleContent = new GUIContent($"NekoGraph [{_currentNodeSystem}]");
    }

    #region Factory Logic

    /// <summary>
    /// 画布工厂：根据系统类型创建对应的 GraphView 喵~
    /// </summary>
    private INekoGraphView CreateGraphView(NodeSystem system)
    {
        // 查找所有继承自 BaseGraphView<T> 的非抽象类喵~
        var viewType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => !t.IsAbstract && typeof(INekoGraphView).IsAssignableFrom(t) 
                                && GetSystemFromViewType(t) == system);

        if (viewType != null)
        {
            return Activator.CreateInstance(viewType) as INekoGraphView;
        }

        Debug.LogWarning($"[NekoGraph] 未找到系统 {system} 的专属 GraphView，使用通用方案喵~");
        // 如果没找到，尝试默认创建一个能跑的喵 (这里简化处理，实际可以更复杂)
        return null; 
    }

    private NodeSystem GetSystemFromViewType(Type t)
    {
        // 实例化一个临时的来看看它宣称自己是什么系统喵~
        // 或者通过属性判断，这里简单起见，利用我们刚才加的 CurrentSystem 属性喵
        try {
            var instance = Activator.CreateInstance(t) as INekoGraphView;
            return instance?.CurrentSystem ?? NodeSystem.Common;
        } catch { return NodeSystem.Common; }
    }

    private void SetupSearchWindow(GraphView gv, NodeSystem system)
    {
        // 根据系统寻找对应的 SearchWindow 提供者喵~
        var searchWindowType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.IsSubclassOf(typeof(ScriptableObject)) && t.GetInterfaces().Contains(typeof(ISearchWindowProvider))
                                && GetSystemFromSearchWindowType(t) == system);

        if (searchWindowType != null)
        {
            var provider = ScriptableObject.CreateInstance(searchWindowType);
            
            // 初始化 SearchWindow (假设它们都有 Initialize 方法喵)
            var initMethod = searchWindowType.GetMethod("Initialize");
            if (initMethod != null)
            {
                initMethod.Invoke(provider, new object[] { this, gv });
            }

            gv.nodeCreationRequest = context =>
            {
                // 使用反射调用 SearchWindow.Open，解决泛型约束问题喵~
                // 因为 SearchWindow.Open<T> 要求 T : ScriptableObject, ISearchWindowProvider
                // 而我们这里只有一个 Object (provider)，编译时无法满足约束喵
                var method = typeof(SearchWindow).GetMethod("Open",
                    BindingFlags.Static | BindingFlags.Public);
                
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(provider.GetType());
                    genericMethod.Invoke(null, new object[] {
                        new SearchWindowContext(context.screenMousePosition),
                        provider
                    });
                }
            };
        }
    }

    private NodeSystem GetSystemFromSearchWindowType(Type t)
    {
        // 这里的逻辑可以跟 SearchWindow 的基类约定好喵~
        // 目前先通过名称粗略判断，后续建议在 SearchWindow 基类加个 System 属性喵
        if (t.Name.Contains("Story")) return NodeSystem.Story;
        if (t.Name.Contains("Mission")) return NodeSystem.Mission;
        if (t.Name.Contains("Social")) return NodeSystem.Social;
        if (t.Name.Contains("Lab")) return NodeSystem.Lab;
        return NodeSystem.Common;
    }

    #endregion

    #region Save / Load

    private void SaveData()
    {
        if (_graphView == null) return;

        string path = EditorUtility.SaveFilePanel("保存数据", "Assets/Resources", "New_Graph.json", "json");
        if (string.IsNullOrEmpty(path)) return;

        var pack = _graphView.SerializeToPackGeneric();
        if (pack == null) return;

        string json = JsonConvert.SerializeObject(pack, Formatting.Indented, new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.Auto
        });

        System.IO.File.WriteAllText(path, json);
        _graphView.CurrentFilePath = path;
        
        AssetDatabase.Refresh();
        Debug.Log($"[NekoGraph] 已保存至: {path} 喵~");
    }

    private void LoadData()
    {
        if (_graphView == null) return;

        string path = EditorUtility.OpenFilePanel("读取数据", "Assets/Resources", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = System.IO.File.ReadAllText(path);
        
        // 关键点：我们需要知道反序列化成什么类型喵！
        // 我们可以从 _graphView 宣称的 TPack 类型里拿喵
        Type packType = GetPackTypeFromView(_graphView.GetType());
        
        var pack = JsonConvert.DeserializeObject(json, packType, new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.Auto
        }) as BasePackData;

        if (pack != null)
        {
            _graphView.PopulateFromPackGeneric(pack);
            _graphView.CurrentFilePath = path;
            Debug.Log($"[NekoGraph] 已加载: {path} 喵~");
        }
    }

    private Type GetPackTypeFromView(Type viewType)
    {
        // 溯源：从 BaseGraphView<TPack> 中提取 TPack 喵~
        var baseType = viewType.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(BaseGraphView<>))
            {
                return baseType.GetGenericArguments()[0];
            }
            baseType = baseType.BaseType;
        }
        return typeof(BasePackData);
    }

    #endregion
}
#endif
