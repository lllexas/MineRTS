using NekoGraph;
using UnityEngine;
using System;

namespace GAL
{
/// <summary>
/// 选项播放器 - 中间层仲裁
///
/// 职责：
/// 1. 接收 Handler 整包的 ChoicePackage
/// 2. 拆包，为每个选项 index 发独立事件给对应的 ChoiceBar
/// 3. 收集 ChoiceBar 的点击回调
/// 4. 调用 RouteSignal 路由信号
///
/// 前端完全解耦：不引用任何 NekoGraph 类型
///
/// ChoiceVFSHandler ──[整包]──► ChoicePlayer ──[拆包分发]──► ChoiceBar[i]
/// </summary>
public class ChoicePlayer : SingletonMono<ChoicePlayer>
{
    private ChoicePackage _pendingPackage;

    /// <summary>
    /// VFSHandler 调用入口 - 接收整包
    /// </summary>
    public bool TryPresent(ChoicePackage package)
    {
        if (package == null || package.Options == null || package.Options.Length == 0)
        {
            Debug.LogError("[ChoicePlayer] 收到空的 ChoicePackage");
            return false;
        }

        _pendingPackage = package;

        // 0. 清空所有 Bar（一重保险）
        PostSystem.Instance.Send("清空选项", null);

        // 1. 拆包分发【设置选项】- Panel 先设置位置（知道总数），Bar 再接收文本
        for (int i = 0; i < package.Options.Length; i++)
        {
            var option = package.Options[i];
            PostSystem.Instance.Send("设置选项", new ChoiceBarEventData
            {
                OptionIndex = i,
                OptionText = option.Text,
                TotalOptions = package.Options.Length,
                OnSelect = OnChoiceSelected
            });
        }

        // 2. 唤起面板显示
        PostSystem.Instance.Send("期望显示面板", "ChoicePanel");

        // 3. 逐个唤起 Bar 显示（UIID 匹配，二重保险）- 只有收到匹配的 UIID 才会 FadeIn
        for (int i = 0; i < package.Options.Length; i++)
        {
            PostSystem.Instance.Send("期望显示面板", $"ChoiceBar{i}");
        }

        Debug.Log($"[ChoicePlayer] 已设置 {package.Options.Length} 个选项，并唤起显示");
        return true;
    }

    private void OnChoiceSelected(int selectedIndex)
    {
        if (_pendingPackage == null)
        {
            Debug.LogError("[ChoicePlayer] 收到选择回调，但没有待处理的包");
            return;
        }

        var package = _pendingPackage;
        _pendingPackage = null;

        // 隐藏所有选项条
        for (int i = 0; i < package.Options.Length; i++)
        {
            PostSystem.Instance.Send("期望隐藏面板", $"ChoiceBar{i}");
        }

        // 调用路由闭包完成信号入队
        Debug.Log($"[ChoicePlayer] 用户选择了选项 [{selectedIndex}]，执行路由");
        package.RouteSignal.Invoke(selectedIndex);
    }
}
}
