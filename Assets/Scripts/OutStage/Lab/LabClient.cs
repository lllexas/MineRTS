using NekoGraph;
using SpaceTUI;
using UnityEngine;

/// <summary>
/// Lab 前端客户端仲裁器。
/// 后端 Query 只提供原始数据包；真正如何展示，由 LabClient 按 ViewKey 再分发给前端。
/// </summary>
public sealed class LabClient : SingletonMono<LabClient>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterConsolePresenters()
    {
        ConsoleClientRuntime.RegisterPresenter("lab", PresentRequest);
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

    private static void PresentRequest(ConsoleManager console, VFSQueryResult result)
    {
        _ = LabClient.Instance;

        if (result?.Payload is not VFSLabEntryQueryPayload labPayload || labPayload.Entry == null)
            return;

        PostSystem.Instance.Send(LabClientEvents.PresentRequested, new LabClientPresentRequest
        {
            ViewKey = string.IsNullOrWhiteSpace(result.RequestName) ? LabClientViewKeys.Inspect : result.RequestName,
            Source = labPayload,
            FrontendContext = console ?? labPayload.FrontendContext,
            Actions = null
        });
    }

    [Subscribe(LabClientEvents.PresentRequested)]
    private void OnPresentRequested(object data)
    {
        if (data is not LabClientPresentRequest request || request.Source?.Entry == null)
            return;

        string viewKey = string.IsNullOrWhiteSpace(request.ViewKey)
            ? LabClientViewKeys.Inspect
            : request.ViewKey;

        PostSystem.Instance.Send(LabClientEvents.GetViewRequestedEvent(viewKey), request);
    }

    [Subscribe(LabClientEvents.ViewRequestedInspect)]
    private void OnInspectRequested(object data)
    {
        if (data is not LabClientPresentRequest request || request.Source?.Entry == null)
            return;

        PostSystem.Instance.Send(LabEntryViewerEvents.Refresh, request.Source);
        PostSystem.Instance.Send("期望显示面板", LabEntryViewerEvents.PanelID);
    }
}

public static class LabClientViewKeys
{
    public const string Inspect = "inspect";
}

public static class LabClientEvents
{
    public const string PresentRequested = "LabClient.PresentRequested";
    public const string ViewRequestedPrefix = "LabClient.ViewRequested.";
    public const string ViewRequestedInspect = ViewRequestedPrefix + LabClientViewKeys.Inspect;

    public static string GetViewRequestedEvent(string viewKey)
    {
        return ViewRequestedPrefix + (string.IsNullOrWhiteSpace(viewKey) ? LabClientViewKeys.Inspect : viewKey);
    }
}

public sealed class LabClientPresentRequest
{
    public string ViewKey;
    public VFSLabEntryQueryPayload Source;
    public object FrontendContext;
    public object Actions;
}
