using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_WindowManager : SingletonMono<UI_WindowManager>
{
    [Header("预制件配置")]
    public List<WindowMap> windowPrefabs;
    public Transform canvasRoot; // 所有的窗口都生在这里喵

    private Dictionary<WorkType, UI_BaseEntityWindow> _activeWindows = new Dictionary<WorkType, UI_BaseEntityWindow>();

    [System.Serializable]
    public struct WindowMap
    {
        public WorkType type;
        public GameObject prefab;
    }

    private void Update()
    {
        // 1. 点击检测 (保持不变)
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            DetectWorldClick();
        }

        // 2. 【核心修改】驱动所有打开的窗口刷新
        // 喵！这里不能直接遍历 Values，要先复制一份引用列表
        // 这样遍历的是 List，字典被 Remove 不会影响当前的 foreach 迭代
        var windowsSnapshot = new List<UI_BaseEntityWindow>(_activeWindows.Values);

        foreach (var window in windowsSnapshot)
        {
            // 增加判空，防止在这一帧前面已经被销毁了
            if (window != null)
            {
                window.TickUpdate();
            }
        }
    }

    private void DetectWorldClick()
    {
        Vector2 mousePos = GridSystem.GetMouseGridPos(Vector2Int.one);
        Vector2Int gridPos = GridSystem.Instance.WorldToGrid(mousePos);
        int occupantId = GridSystem.Instance.GetOccupantId(gridPos);

        if (occupantId != -1)
        {
            OpenWindow(EntitySystem.Instance.GetHandleFromId(occupantId));
        }
    }

    public void OpenWindow(EntityHandle handle)
    {
        int idx = EntitySystem.Instance.GetIndex(handle);
        WorkType type = EntitySystem.Instance.wholeComponent.workComponent[idx].WorkType;
        if (type == WorkType.None) return;

        // --- 核心逻辑：同一类型窗口唯一性 ---
        if (_activeWindows.TryGetValue(type, out var existingWindow))
        {
            // 如果窗口已经开了，换个目标，并提到最前
            existingWindow.Init(handle);
            existingWindow.transform.SetAsLastSibling();
        }
        else
        {
            // 如果没开，实例化新的
            GameObject prefab = windowPrefabs.Find(m => m.type == type).prefab;
            if (prefab != null)
            {
                GameObject go = Instantiate(prefab, canvasRoot);
                var window = go.GetComponent<UI_BaseEntityWindow>();
                window.Init(handle);
                _activeWindows.Add(type, window);
            }
        }
    }

    public void UnregisterWindow(WorkType type)
    {
        if (_activeWindows.ContainsKey(type)) _activeWindows.Remove(type);
    }
}