using UnityEngine;
using SpaceTUI;

/// <summary>
/// vfs.msg 的最小 console session。
/// 先承担最基本的消息阅读体验，后续再逐步补充选择、跳转和图驱动会话。
/// </summary>
public sealed class VFSMsgSession : ConsoleSessionBase
{
    private readonly VFSMsgQueryPayload _payload;
    private ConsoleManager _console;

    public VFSMsgSession(VFSMsgQueryPayload payload)
    {
        _payload = payload;
    }

    public override string SessionId => _payload?.Message?.MessageId ?? "vfs.msg";
    public override string SessionName => "VFS Message";
    public override bool ShouldRenderInputLine => false;

    public override void OnSessionEnter(ConsoleManager console)
    {
        _console = console;
        if (console == null || _payload?.Message == null)
            return;

        console.ClearConsole();
        console.ScrollConsoleToTop();

        var msg = _payload.Message;
        console.Log("╔════════ MESSAGE ════════", Color.gray);
        console.Log($"From   : {msg.Sender}", Color.white);
        console.Log($"Title  : {msg.Title}", Color.cyan);

        if (msg.Timestamp > 0)
            console.Log($"Time   : {msg.Timestamp}", Color.gray);

        console.Log(" ", Color.clear);

        if (!string.IsNullOrWhiteSpace(msg.Preview))
            console.Log(msg.Preview, new Color(0.7f, 0.7f, 0.7f));

        if (!string.IsNullOrWhiteSpace(msg.Body))
        {
            console.Log(" ", Color.clear);
            console.Log(msg.Body, Color.white);
        }

        console.Log(" ", Color.clear);
        console.Log("Enter / Esc 关闭消息", new Color(0.55f, 0.55f, 0.55f));
    }

    public override void OnSessionExit(ConsoleManager console)
    {
        _console = null;
    }

    public override bool HandleKey(KeyInfo key)
    {
        if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter)
            return HandleConfirm();

        if (key.keyCode == KeyCode.Escape)
            return HandleCancel();

        return false;
    }

    public override bool HandleSubmit(string input)
    {
        return HandleConfirm();
    }

    public override bool HandleNavigation(ConsoleNavKey key)
    {
        return false;
    }

    public override bool HandleConfirm()
    {
        return HandleCancel();
    }

    public override bool HandleCancel()
    {
        _console?.EndSession(this);
        return true;
    }
}
