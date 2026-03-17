using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

// =========================================================
// Lab 科技树管理器
// =========================================================
//
// 【职责说明】
//
// TechTreeManager 是科技树系统的中央管理器喵~
// 负责加载/卸载科技树图实例，管理科技状态喵~
//
// 【工作流程】
//
// 1. 读档时：从 UserModel 读取科技树 PackID → 加载 LabTechPackData → 注册到 GraphRunner
// 2. 卸载时：从 GraphRunner 注销图实例 → 清理运行时数据
// 3. 研究中：每帧 Update 更新研究进度 → 进度满时触发 CommandNode 执行解锁
//
// =========================================================

/// <summary>
/// 科技树管理器单例喵~
/// </summary>
public class TechTreeManager : SingletonMono<TechTreeManager>
{
    /// <summary>
    /// 当前加载的科技树图实例
    /// </summary>
    private RuntimeGraphInstance _currentTechTree;

    /// <summary>
    /// 科技运行时数据字典：TechID -> TechRuntimeData
    /// </summary>
    private Dictionary<string, TechRuntimeData> _techRuntimeData = new Dictionary<string, TechRuntimeData>();

    /// <summary>
    /// 科技节点数据字典：TechID -> TechNodeData
    /// </summary>
    private Dictionary<string, TechNodeData> _techNodeMap = new Dictionary<string, TechNodeData>();

    /// <summary>
    /// 当前科技树 PackID
    /// </summary>
    public string CurrentTechTreePackID { get; private set; }

    /// <summary>
    /// 科技树是否已加载
    /// </summary>
    public bool IsTechTreeLoaded => _currentTechTree != null && _currentTechTree.IsRunning;

    // ==========================================
    //  事件定义
    // ==========================================

    /// <summary>
    /// 科技状态改变事件喵~
    /// </summary>
    public event Action<string, TechState> OnTechStateChanged;

    /// <summary>
    /// 科技研究进度更新事件喵~
    /// </summary>
    public event Action<string, float> OnTechProgressUpdated;

    /// <summary>
    /// 科技解锁完成事件喵~
    /// </summary>
    public event Action<string> OnTechUnlocked;

    protected override void Awake()
    {
        base.Awake();
        // 不销毁，跨场景保留
        // DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // 更新研究中的科技进度
        if (IsTechTreeLoaded)
        {
            UpdateResearchProgress();
        }
    }

    // ==========================================
    //  核心公共方法
    // ==========================================

    /// <summary>
    /// 加载科技树喵~
    /// </summary>
    public void LoadTechTree(LabTechPackData pack)
    {
        if (pack == null)
        {
            Debug.LogError("[TechTreeManager] PackData 为空，无法加载喵~");
            return;
        }

        // 卸载旧的科技树
        UnloadTechTree();

        // 生成实例 ID
        string instanceID = $"LabTech_{pack.PackID}_{DateTime.Now.Ticks}";

        // 使用 GraphLoader 加载图实例
        var instance = GraphLoader.LoadFromPackGeneric(pack, instanceID, "LabTech");
        if (instance == null)
        {
            Debug.LogError("[TechTreeManager] 图实例加载失败喵~");
            return;
        }

        // 注册到 GraphRunner
        if (GraphRunner.Instance != null)
        {
            GraphRunner.Instance.RegisterInstance(instance);
        }

        _currentTechTree = instance;
        CurrentTechTreePackID = pack.PackID;

        // 构建科技节点映射
        BuildTechNodeMap();

        // 初始化运行时数据
        InitializeRuntimeData();

        Debug.Log($"[TechTreeManager] 科技树已加载：{pack.PackID}, 节点数：{_techNodeMap.Count}");
    }

    /// <summary>
    /// 卸载科技树喵~
    /// </summary>
    public void UnloadTechTree()
    {
        if (_currentTechTree != null)
        {
            // 从 GraphRunner 注销
            if (GraphRunner.Instance != null)
            {
                GraphRunner.Instance.UnregisterInstance(_currentTechTree.InstanceID);
            }

            // 清理数据
            _currentTechTree = null;
            _techNodeMap.Clear();
            _techRuntimeData.Clear();
            CurrentTechTreePackID = null;

            Debug.Log("[TechTreeManager] 科技树已卸载喵~");
        }
    }

    /// <summary>
    /// 获取科技状态喵~
    /// </summary>
    public TechState GetTechState(string techID)
    {
        if (_techRuntimeData.TryGetValue(techID, out var data))
        {
            return data.State;
        }
        return TechState.Locked;
    }

    /// <summary>
    /// 获取科技节点数据喵~
    /// </summary>
    public TechNodeData GetTechNode(string techID)
    {
        _techNodeMap.TryGetValue(techID, out var node);
        return node;
    }

    /// <summary>
    /// 检查科技是否可研究喵~
    /// </summary>
    public bool CanResearch(string techID)
    {
        if (!_techRuntimeData.TryGetValue(techID, out var data))
        {
            return false;
        }

        if (data.State != TechState.Available)
        {
            return false;
        }

        // 检查前置科技是否都已完成
        var techNode = GetTechNode(techID);
        if (techNode == null)
        {
            return false;
        }

        // 注意：InputNodeIDs 存储的是前置科技 ID 喵~
        foreach (var prereqID in techNode.InputNodeIDs)
        {
            if (!_techRuntimeData.TryGetValue(prereqID, out var prereqData) ||
                prereqData.State != TechState.Completed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 开始研究科技喵~
    /// </summary>
    public bool StartResearch(string techID, float duration = 10f)
    {
        if (!CanResearch(techID))
        {
            Debug.LogWarning($"[TechTreeManager] 科技 {techID} 不可研究喵~");
            return false;
        }

        if (_techRuntimeData.TryGetValue(techID, out var data))
        {
            data.StartResearch(duration);
            SetTechState(techID, TechState.Researching);
            Debug.Log($"[TechTreeManager] 开始研究：{techID}, 时长：{duration}秒");
            return true;
        }

        Debug.LogError($"[TechTreeManager] 找不到科技数据：{techID}");
        return false;
    }

    /// <summary>
    /// 取消研究喵~
    /// </summary>
    public void CancelResearch(string techID)
    {
        if (_techRuntimeData.TryGetValue(techID, out var data))
        {
            data.Cancel();
            SetTechState(techID, TechState.Available);
            Debug.Log($"[TechTreeManager] 取消研究：{techID}");
        }
    }

    /// <summary>
    /// 完成科技研究喵~
    /// </summary>
    public void CompleteResearch(string techID)
    {
        if (_techRuntimeData.TryGetValue(techID, out var data))
        {
            data.Complete();
            SetTechState(techID, TechState.Completed);
            OnTechUnlocked?.Invoke(techID);
            Debug.Log($"[TechTreeManager] 科技完成：{techID}");
        }
    }

    /// <summary>
    /// 获取研究进度喵~
    /// </summary>
    public float GetResearchProgress(string techID)
    {
        if (_techRuntimeData.TryGetValue(techID, out var data))
        {
            return data.Progress;
        }
        return 0f;
    }

    // ==========================================
    //  内部方法
    // ==========================================

    /// <summary>
    /// 构建科技节点映射喵~
    /// </summary>
    private void BuildTechNodeMap()
    {
        if (_currentTechTree == null) return;

        _techNodeMap.Clear();

        foreach (var node in _currentTechTree.NodeMap.Values)
        {
            if (node is TechNodeData techNode)
            {
                string key = !string.IsNullOrEmpty(techNode.TechID) ? techNode.TechID : techNode.NodeID;
                _techNodeMap[key] = techNode;
            }
        }

        Debug.Log($"[TechTreeManager] 已构建 { _techNodeMap.Count} 个科技节点映射");
    }

    /// <summary>
    /// 初始化运行时数据喵~
    /// </summary>
    private void InitializeRuntimeData()
    {
        _techRuntimeData.Clear();

        foreach (var kvp in _techNodeMap)
        {
            _techRuntimeData[kvp.Key] = new TechRuntimeData(kvp.Key);
        }

        // 找出没有前置科技的节点，设为 Available
        foreach (var kvp in _techNodeMap)
        {
            var techNode = kvp.Value;
            if (techNode.InputNodeIDs == null || techNode.InputNodeIDs.Count == 0)
            {
                _techRuntimeData[kvp.Key].State = TechState.Available;
            }
        }
    }

    /// <summary>
    /// 更新研究进度喵~
    /// </summary>
    private void UpdateResearchProgress()
    {
        foreach (var kvp in _techRuntimeData)
        {
            var data = kvp.Value;
            if (data.State == TechState.Researching)
            {
                data.UpdateProgress();
                OnTechProgressUpdated?.Invoke(data.TechID, data.Progress);

                if (data.State == TechState.Completed)
                {
                    OnTechUnlocked?.Invoke(data.TechID);
                }
            }
        }
    }

    /// <summary>
    /// 设置科技状态喵~
    /// </summary>
    private void SetTechState(string techID, TechState state)
    {
        if (_techRuntimeData.TryGetValue(techID, out var data))
        {
            data.State = state;
            OnTechStateChanged?.Invoke(techID, state);
        }
    }

    // ==========================================
    //  静态工具方法
    // ==========================================

    /// <summary>
    /// 检查是否存在可用的科技树管理器喵~
    /// </summary>
    public static bool HasInstance()
    {
        return Instance != null;
    }
}
