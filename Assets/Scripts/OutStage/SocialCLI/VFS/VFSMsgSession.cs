using UnityEngine;
using SpaceTUI;
using NekoGraph;

/// <summary>
/// vfs.msg 的最小 console session。
/// 先承担最基本的消息阅读体验，后续再逐步补充选择、跳转和图驱动会话。
/// </summary>
public sealed class VFSMsgSession : ConsoleSessionBase
{
    private readonly VFSMsgQueryPayload _payload;
    private ConsoleManager _console;
    private int _selectedChoiceIndex;

    public VFSMsgSession(VFSMsgQueryPayload payload)
    {
        _payload = payload;
    }

    public override string SessionId => _payload?.Message?.MessageTag ?? "vfs.msg";
    public override string SessionName => "VFS Message";
    public override bool ShouldRenderInputLine => false;

    public override void OnSessionEnter(ConsoleManager console)
    {
        _console = console;
        if (console == null || _payload?.Message == null)
            return;

        _selectedChoiceIndex = 0;
        Render();
    }

    public override void OnSessionExit(ConsoleManager console)
    {
        _console = null;
    }

    public override bool HandleKey(KeyInfo key)
    {
        if (TryHandleDigitChoice(key.keyCode))
            return true;

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
        var choices = _payload?.Message?.Choices;
        if (choices == null || choices.Count == 0)
            return false;

        if (key == ConsoleNavKey.Up)
        {
            _selectedChoiceIndex = (_selectedChoiceIndex - 1 + choices.Count) % choices.Count;
            Render();
            return true;
        }

        if (key == ConsoleNavKey.Down)
        {
            _selectedChoiceIndex = (_selectedChoiceIndex + 1) % choices.Count;
            Render();
            return true;
        }

        return false;
    }

    public override bool HandleConfirm()
    {
        var choices = _payload?.Message?.Choices;
        if (choices == null || choices.Count == 0)
            return HandleCancel();

        return ResumeSelectedChoice() || HandleCancel();
    }

    public override bool HandleCancel()
    {
        _console?.EndSession(this);
        return true;
    }

    private void Render()
    {
        if (_console == null || _payload?.Message == null)
            return;

        _console.ClearConsole();
        _console.ScrollConsoleToTop();

        var msg = _payload.Message;
        _console.Log("╔════════ MESSAGE ════════", Color.gray);
        _console.Log($"From   : {msg.Sender}", Color.white);
        _console.Log($"Title  : {msg.Title}", Color.cyan);
        _console.Log(" ", Color.clear);

        if (!string.IsNullOrWhiteSpace(msg.Body))
            _console.Log(msg.Body, Color.white);

        if (msg.Choices != null && msg.Choices.Count > 0)
        {
            _console.Log(" ", Color.clear);
            _console.Log("Choices:", new Color(0.75f, 0.75f, 0.75f));
            for (int i = 0; i < msg.Choices.Count; i++)
            {
                var choice = msg.Choices[i];
                string text = string.IsNullOrWhiteSpace(choice?.Text) ? "(empty)" : choice.Text;
                bool isSelected = i == _selectedChoiceIndex;
                _console.Log($"{(isSelected ? ">" : " ")} [{i + 1}] {text}", isSelected ? Color.yellow : new Color(0.85f, 0.85f, 0.85f));
            }

            _console.Log(" ", Color.clear);
            _console.Log("Up/Down or 1-9 to choose, Enter confirm, Esc close", new Color(0.55f, 0.55f, 0.55f));
            return;
        }

        _console.Log(" ", Color.clear);
        _console.Log("Enter / Esc 关闭消息", new Color(0.55f, 0.55f, 0.55f));
    }

    private bool ResumeSelectedChoice()
    {
        var replicaMeta = _payload?.ReplicaMeta;
        if (replicaMeta == null || replicaMeta.IsResolved)
            return false;

        if (replicaMeta.ChoiceTargetNodeIDs == null ||
            _selectedChoiceIndex < 0 ||
            _selectedChoiceIndex >= replicaMeta.ChoiceTargetNodeIDs.Count)
        {
            return false;
        }

        string targetNodeId = replicaMeta.ChoiceTargetNodeIDs[_selectedChoiceIndex];
        if (string.IsNullOrWhiteSpace(targetNodeId))
            return false;

        var runner = GraphHub.Instance?.DefaultRunner;
        if (runner == null)
            return false;

        bool resumed = runner.ResumeSuspendedSignalToTarget(
            replicaMeta.BackendPackID,
            replicaMeta.SignalId,
            replicaMeta.BackendNodeID,
            targetNodeId);

        if (!resumed)
            return false;

        replicaMeta.IsResolved = true;
        PersistReplicaMeta(replicaMeta);
        return HandleCancel();
    }

    private void PersistReplicaMeta(VFSMsgReplicaMeta replicaMeta)
    {
        if (replicaMeta == null || string.IsNullOrWhiteSpace(_payload?.PackID) || string.IsNullOrWhiteSpace(_payload?.VfsPath))
            return;

        var analyser = GraphHub.Instance?.DefaultAnalyser;
        if (analyser?.GetNode(_payload.PackID, _payload.VfsPath, PackAccessSubjects.SystemMin) is VFSNodeData node)
        {
            node.InlineText = VFSMsgReplicaMeta.Serialize(replicaMeta);
        }
    }

    private bool TryHandleDigitChoice(KeyCode keyCode)
    {
        int index = keyCode switch
        {
            KeyCode.Alpha1 or KeyCode.Keypad1 => 0,
            KeyCode.Alpha2 or KeyCode.Keypad2 => 1,
            KeyCode.Alpha3 or KeyCode.Keypad3 => 2,
            KeyCode.Alpha4 or KeyCode.Keypad4 => 3,
            KeyCode.Alpha5 or KeyCode.Keypad5 => 4,
            KeyCode.Alpha6 or KeyCode.Keypad6 => 5,
            KeyCode.Alpha7 or KeyCode.Keypad7 => 6,
            KeyCode.Alpha8 or KeyCode.Keypad8 => 7,
            KeyCode.Alpha9 or KeyCode.Keypad9 => 8,
            _ => -1
        };

        var choices = _payload?.Message?.Choices;
        if (index < 0 || choices == null || index >= choices.Count)
            return false;

        _selectedChoiceIndex = index;
        Render();
        return true;
    }
}
