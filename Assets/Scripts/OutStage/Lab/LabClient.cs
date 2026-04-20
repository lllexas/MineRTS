using System.Collections.Generic;
using System.Text;
using NekoGraph;
using SpaceTUI;
using UnityEngine;

/// <summary>
/// Lab 前端客户端仲裁器。
/// 注册 lab.inspect Presenter，把 Query 结果转化为 LabWindow 详情展示。
/// </summary>
public sealed class LabClient : SingletonMono<LabClient>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterConsolePresenters()
    {
        ConsoleClientRuntime.RegisterPresenter("lab.inspect", PresentInspectRequest);
    }

    protected override void Awake()
    {
        base.Awake();
        PostSystem.Instance?.Register(this);
    }

    private void OnDestroy()
    {
        if (!PostSystem.IsApplicationQuitting)
            PostSystem.Instance?.Unregister(this);
    }

    private static void PresentInspectRequest(ConsoleManager console, VFSQueryResult result)
    {
        if (result?.Payload is not VFSLabEntryQueryPayload labPayload || labPayload.Entry == null)
            return;

        var entry = labPayload.Entry;
        var blueprint = entry.EntityBlueprint;
        var lines = new List<string>();

        // 描述
        if (!string.IsNullOrWhiteSpace(entry.Description))
        {
            lines.Add(entry.Description);
            lines.Add(string.Empty);
        }

        // 引用实体信息
        if (blueprint != null)
        {
            lines.Add($"> 解锁实体: {blueprint.DisplayName ?? blueprint.BlueprintId}");
            lines.Add($"> 派系: {FormatFaction(blueprint.Faction)}");
            lines.Add(string.Empty);
        }
        else
        {
            lines.Add("> 无关联实体");
            lines.Add(string.Empty);
        }

        // 解锁代价
        if (entry.UnlockCosts != null && entry.UnlockCosts.Length > 0)
        {
            lines.Add("解锁代价:");
            foreach (var cost in entry.UnlockCosts)
            {
                lines.Add($"  - 资源 {cost.ResourceType}: {cost.Amount}");
            }
        }
        else
        {
            lines.Add("解锁代价: 免费");
        }

        // Footer: 解锁状态 + 操作提示
        string footer = BuildFooter(labPayload);

        PostSystem.Instance.Send("LabWindow.Refresh", new LabGUI.DisplayData
        {
            Title = $"LAB / {entry.EntryId}",
            Lines = lines.ToArray(),
            Footer = footer
        });

        // 确保面板处于显示状态
        PostSystem.Instance.Send("期望显示面板", "LabWindowPanel");
    }

    private static string BuildFooter(VFSLabEntryQueryPayload payload)
    {
        var facade = GraphHub.Instance?.GetFacade<LabFacade>();
        if (facade == null || payload.Node == null)
            return "无法读取解锁状态";

        if (facade.IsUnlocked(payload.Node))
            return "[已解锁] 该实体已加入仓库";

        return "输入 unlock 解锁该条目";
    }

    private static string FormatFaction(int faction)
    {
        return faction switch
        {
            0 => "Protocol",
            1 => "SunCity",
            2 => "Gaia",
            _ => $"F:{faction}"
        };
    }
}
