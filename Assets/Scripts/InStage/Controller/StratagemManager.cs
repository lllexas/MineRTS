using UnityEngine;
using System.Collections.Generic;
using System;

// 战略配备定义
[Serializable]
public class Stratagem
{
    public string Name;             // 名称
    public string Description;      // 描述
    public List<KeyCode> Code;      // 搓招序列（如：Up, Down, Left, Right）
    public long Cost;               // 费用
    public string BlueprintKey;     // 呼叫成功后部署的蓝图名
}

public class StratagemManager : SingletonMono<StratagemManager>
{
    [Header("配置")]
    public KeyCode CallMenuKey = KeyCode.LeftControl; // 呼叫菜单按键

    private List<Stratagem> _library = new List<Stratagem>();
    private List<KeyCode> _currentSequence = new List<KeyCode>();
    private bool _isMenuOpen = false;

    protected override void Awake()
    {
        base.Awake();
        // 喵！这里以后可以从配置表读，现在先手动加几个默认的
        RegisterDefaultStratagems();
    }

    private void Update()
    {
        // 1. 只有按住呼叫键（如 Ctrl）时才允许搓招
        if (Input.GetKeyDown(CallMenuKey)) OpenMenu();
        if (Input.GetKeyUp(CallMenuKey)) CloseMenu();

        if (_isMenuOpen)
        {
            HandleSequenceInput();
        }
    }

    private void OpenMenu()
    {
        _isMenuOpen = true;
        _currentSequence.Clear();
        // 广播给 UI 喵！
        PostSystem.Instance.Send("战略配备菜单状态", true);
    }

    private void CloseMenu()
    {
        _isMenuOpen = false;
        PostSystem.Instance.Send("战略配备菜单状态", false);
    }

    private void HandleSequenceInput()
    {
        // 监听方向键输入喵
        if (Input.GetKeyDown(KeyCode.UpArrow)) AddToSequence(KeyCode.UpArrow);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) AddToSequence(KeyCode.DownArrow);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) AddToSequence(KeyCode.LeftArrow);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) AddToSequence(KeyCode.RightArrow);
    }

    private void AddToSequence(KeyCode key)
    {
        _currentSequence.Add(key);

        // 每次按键都发广播，让 UI 能同步显示主人的“搓招进度”喵！
        PostSystem.Instance.Send("战略配备按键输入", key);

        // 检查是否有匹配的配备
        CheckForMatch();
    }

    private void CheckForMatch()
    {
        foreach (var strat in _library)
        {
            if (IsSequenceMatch(strat.Code, _currentSequence))
            {
                ExecuteStratagem(strat);
                _currentSequence.Clear(); // 成功后清空
                return;
            }
        }

        // 如果按键太长了还没对上，说明搓错了，清空重来喵
        if (_currentSequence.Count > 10) _currentSequence.Clear();
    }

    private bool IsSequenceMatch(List<KeyCode> pattern, List<KeyCode> input)
    {
        if (pattern.Count != input.Count) return false;
        for (int i = 0; i < pattern.Count; i++)
        {
            if (pattern[i] != input[i]) return false;
        }
        return true;
    }

    private void ExecuteStratagem(Stratagem strat)
    {
        // 喵！现在我们不直接扣钱，而是先打开“建筑预览”
        Debug.Log($"<color=cyan>[搓招成功]</color> 指令正确！{strat.Name} 准备就绪，请选择部署地点...");

        // 告知建筑控制器：我要部署这个，部署好了告诉我坐标和朝向喵！
        BuildingController.Instance.EnterStratagemDeployment(strat.BlueprintKey, (pos, rot) => {

            // 这部分逻辑在玩家点击左键后才会执行：
            if (IndustrialSystem.Instance.SpendGold(strat.Cost))
            {
                // 1. 正式召唤！
                EntityHandle handle = EntitySystem.Instance.CreateEntityFromBlueprint(strat.BlueprintKey, pos, 1);
                int idx = EntitySystem.Instance.GetIndex(handle);
                if (idx != -1)
                {
                    EntitySystem.Instance.wholeComponent.coreComponent[idx].Rotation = rot;
                }

                // 2. 华丽的视觉反馈（以后可以加空投箱掉落动画喵！）
                Debug.Log($"<color=green>[支援到达]</color> {strat.Name} 已部署在 {pos}！");
                PostSystem.Instance.Send("战略配备部署成功", strat.Name);
            }
            else
            {
                Debug.LogWarning("喵？！没钱了！空投服务已取消。");
                PostSystem.Instance.Send("战略配备失败", "资金不足喵");
            }
        });
    }

    private void RegisterDefaultStratagems()
    {
        // 指令：上 下 右
        _library.Add(new Stratagem
        {
            Name = "空降机械狗",
            Code = new List<KeyCode> { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow },
            Cost = 300,
            BlueprintKey = "x_dog"
        });

        // 指令：下 下 上 上
        _library.Add(new Stratagem
        {
            Name = "轨道空投：发电机",
            Code = new List<KeyCode> { KeyCode.DownArrow, KeyCode.DownArrow, KeyCode.UpArrow, KeyCode.UpArrow },
            Cost = 2000,
            BlueprintKey = "generator"
        });
    }
}