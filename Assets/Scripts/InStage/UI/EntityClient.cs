using System;
using System.Collections.Generic;
using NekoGraph;
using SpaceTUI;
using UnityEngine;

/// <summary>
/// Entity 前端客户端仲裁器。
/// 后端 Query 只提供原始数据包；真正如何展示，由 EntityClient 按 ViewKey 再分发给前端。
/// </summary>
public sealed class EntityClient : SingletonMono<EntityClient>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterConsolePresenters()
    {
        ConsoleClientRuntime.RegisterPresenter("entity", PresentRequest);
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
        if (result?.Payload is not VFSEntityQueryPayload entityPayload || entityPayload.Blueprint == null)
            return;

        PostSystem.Instance.Send(EntityClientEvents.PresentRequested, new EntityClientPresentRequest
        {
            ViewKey = string.IsNullOrWhiteSpace(result.RequestName) ? EntityClientViewKeys.Inspect : result.RequestName,
            Source = entityPayload,
            FrontendContext = console ?? entityPayload.FrontendContext,
            Actions = new List<EntityClientAction>()
        });
    }

    [Subscribe(EntityClientEvents.PresentRequested)]
    private void OnPresentRequested(object data)
    {
        if (data is not EntityClientPresentRequest request || request.Source?.Blueprint == null)
            return;

        string viewKey = string.IsNullOrWhiteSpace(request.ViewKey)
            ? EntityClientViewKeys.Inspect
            : request.ViewKey;

        PostSystem.Instance.Send(EntityClientEvents.GetViewRequestedEvent(viewKey), request);
    }
}

public static class EntityClientViewKeys
{
    public const string Inspect = "inspect";
    public const string Summary = "summary";
}

public static class EntityClientEvents
{
    public const string PresentRequested = "EntityClient.PresentRequested";
    public const string ViewRequestedPrefix = "EntityClient.ViewRequested.";

    public static string GetViewRequestedEvent(string viewKey)
    {
        return ViewRequestedPrefix + (string.IsNullOrWhiteSpace(viewKey) ? EntityClientViewKeys.Inspect : viewKey);
    }
}

public sealed class EntityClientPresentRequest
{
    public string ViewKey;
    public VFSEntityQueryPayload Source;
    public object FrontendContext;
    public List<EntityClientAction> Actions;
}

public sealed class EntityClientAction
{
    public string ActionKey;
    public string DisplayName;
    public Action Callback;
}
