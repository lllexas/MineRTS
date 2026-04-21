using NekoGraph;
using SpaceTUI;
using UnityEngine;

/// <summary>
/// Msg 前端客户端仲裁器。
/// 后端 .msg Query 只提供原始数据包；真正如何展示，由 MsgClient 按 ViewKey 再分发给前端。
///
/// 具名请求与分发模型：
/// 1. ConsoleClientRuntime 回调 PresentRequest（presentationType = "msg"）
/// 2. MsgClient 提取 result.RequestName 作为 ViewKey（默认 inspect）
/// 3. 发送 PresentRequested 事件，携带 MsgClientPresentRequest
/// 4. OnPresentRequested 根据 ViewKey 分发到具体事件（如 ViewRequestedInspect / ViewRequestedResolved）
/// 5. 各视图处理器决定打开交互 session 或只读 viewer
/// </summary>
public sealed class MsgClient : SingletonMono<MsgClient>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterConsolePresenters()
    {
        ConsoleClientRuntime.RegisterPresenter("msg", PresentRequest);
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
        _ = MsgClient.Instance;

        if (result?.Payload is not VFSMsgQueryPayload msgPayload || msgPayload.Message == null)
            return;

        PostSystem.Instance.Send(MsgClientEvents.PresentRequested, new MsgClientPresentRequest
        {
            ViewKey = string.IsNullOrWhiteSpace(result.RequestName) ? MsgClientViewKeys.Inspect : result.RequestName,
            Source = msgPayload,
            FrontendContext = console ?? msgPayload.FrontendContext
        });
    }

    [Subscribe(MsgClientEvents.PresentRequested)]
    private void OnPresentRequested(object data)
    {
        if (data is not MsgClientPresentRequest request || request.Source?.Message == null)
            return;

        string viewKey = string.IsNullOrWhiteSpace(request.ViewKey)
            ? MsgClientViewKeys.Inspect
            : request.ViewKey;

        // 已处理态强制路由到 resolved 视图（即使请求的是 inspect）
        // 这是 client 层的仲裁：session 只消费模式，client 决定模式
        if (request.Source.ReplicaMeta?.IsResolved == true && viewKey == MsgClientViewKeys.Inspect)
        {
            viewKey = MsgClientViewKeys.Resolved;
        }

        PostSystem.Instance.Send(MsgClientEvents.GetViewRequestedEvent(viewKey), request);
    }

    [Subscribe(MsgClientEvents.ViewRequestedInspect)]
    private void OnInspectRequested(object data)
    {
        if (data is not MsgClientPresentRequest request || request.Source?.Message == null)
            return;

        // inspect 模式：打开交互式 VFSMsgSession
        // 由 MsgClient 明确决定这是 inspect 模式，session 只负责呈现
        var session = new VFSMsgSession(request.Source);
        if (request.FrontendContext is ConsoleManager console)
        {
            console.BeginSession(session);
        }

        PostSystem.Instance.Send(VFSMsgSessionEvents.SessionOpened, new VFSMsgSessionOpenedEvent
        {
            Session = session,
            Mode = MsgClientViewKeys.Inspect,
            Source = request.Source
        });
    }

    [Subscribe(MsgClientEvents.ViewRequestedResolved)]
    private void OnResolvedRequested(object data)
    {
        if (data is not MsgClientPresentRequest request || request.Source?.Message == null)
            return;

        // resolved 模式：当前复用 VFSMsgSession，但由 MsgClient 明确标记为 resolved
        // 后续如需拆分，可在此替换为 MsgViewerPanel
        var session = new VFSMsgSession(request.Source);
        if (request.FrontendContext is ConsoleManager console)
        {
            console.BeginSession(session);
        }

        PostSystem.Instance.Send(VFSMsgSessionEvents.SessionOpened, new VFSMsgSessionOpenedEvent
        {
            Session = session,
            Mode = MsgClientViewKeys.Resolved,
            Source = request.Source
        });
    }
}

/// <summary>
/// MsgClient 支持的具名视图键。
/// </summary>
public static class MsgClientViewKeys
{
    public const string Inspect = "inspect";
    public const string Resolved = "resolved";
    public const string Summary = "summary";
}

/// <summary>
/// MsgClient 事件常量。
/// </summary>
public static class MsgClientEvents
{
    public const string PresentRequested = "MsgClient.PresentRequested";
    public const string ViewRequestedPrefix = "MsgClient.ViewRequested.";
    public const string ViewRequestedInspect = ViewRequestedPrefix + MsgClientViewKeys.Inspect;
    public const string ViewRequestedResolved = ViewRequestedPrefix + MsgClientViewKeys.Resolved;

    public static string GetViewRequestedEvent(string viewKey)
    {
        return ViewRequestedPrefix + (string.IsNullOrWhiteSpace(viewKey) ? MsgClientViewKeys.Inspect : viewKey);
    }
}

/// <summary>
/// MsgClient 展示请求上下文。
/// </summary>
public sealed class MsgClientPresentRequest
{
    public string ViewKey;
    public VFSMsgQueryPayload Source;
    public object FrontendContext;
}

/// <summary>
/// VFSMsgSession 生命周期事件。
/// </summary>
public static class VFSMsgSessionEvents
{
    public const string SessionOpened = "VFSMsgSession.Opened";
}

/// <summary>
/// VFSMsgSession 打开事件数据。
/// </summary>
public sealed class VFSMsgSessionOpenedEvent
{
    public VFSMsgSession Session;
    public string Mode;
    public VFSMsgQueryPayload Source;
}
