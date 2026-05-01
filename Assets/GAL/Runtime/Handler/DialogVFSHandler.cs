using NekoGraph;
using UnityEngine;

namespace GAL
{
/// <summary>
/// Dialog VFS 处理器 - 处理 .dialog 后缀的 VFS 文件节点
///
/// 当前策略：
/// 1. 校验并解析 DialogSequenceSO
/// 2. 输出条目摘要，便于排查数据问题
/// 3. 若存在可用的 DialogPlayer，则交由其异步接管并返回 Nope
/// 4. 若当前还没有真正的运行时播放器，则安全透传后续节点
/// </summary>
public static class DialogVFSHandler
{
    [EXEHandler(".dialog", typeof(DialogSequenceSO))]
    public static HandleResult Handle(
        VFSResolvedContent content,
        SignalContext context,
        BasePackData pack,
        GraphRunner runner,
        string packInstanceID,
        System.Action continueAction)
    {
        // 1. 获取并校验 DialogSequenceSO
        var sequence = content.GetUnityObject<DialogSequenceSO>();
        if (sequence == null)
        {
            Debug.LogError("[DialogVFSHandler] 校验失败：DialogSequenceSO 为 null");
            return HandleResult.Error;
        }
            
        if (sequence.Entries == null)
        {
            Debug.LogError($"[DialogVFSHandler] 校验失败：'{sequence.name}' 的 Entries 为 null");
            return HandleResult.Error;
        }
            
        // 2. 输出所有 Entry（调试用）
        Debug.Log($"[DialogVFSHandler] 播放序列: {sequence.name} (ID: {sequence.SequenceId}, Entries: {sequence.Entries.Count})");
        for (int i = 0; i < sequence.Entries.Count; i++)
        {
            var entry = sequence.Entries[i];
            if (entry == null)
            {
                Debug.LogWarning($"[DialogVFSHandler] Entry[{i}] 为 null");
                continue;
            }
            switch (entry)
            {
                case DialogEntry dialog:
                    Debug.Log($"[{i}] Dialog: {dialog.Speaker} - {dialog.Content}");
                    break;
                case AvatarEffectEntry avatar:
                    Debug.Log($"[{i}] Avatar: {avatar.Profile?.name ?? "null"} / {avatar.EmotionKey}");
                    break;
                case BackgroundEffectEntry background:
                    Debug.Log($"[{i}] Background: {background.Preset?.name ?? "null"}");
                    break;
                case VoiceEffectEntry voice:
                    var clipName = voice.VoiceClip != null ? voice.VoiceClip.name : "null";
                    Debug.Log($"[{i}] Voice: {clipName}");
                    break;
                case CameraEffectEntry camera:
                    Debug.Log($"[{i}] Camera: {camera.EffectKey}");
                    break;
                case ScreenFlashEntry flash:
                    Debug.Log($"[{i}] Flash: {flash.FlashType} ({flash.Duration}s)");
                    break;
                default:
                    Debug.LogWarning($"[DialogVFSHandler] Entry[{i}] 类型未识别: {entry.GetType().FullName}");
                    break;
            }
        }

            // 3. 交给 DialogPlayer 异步播放，完成后显式恢复挂起的 signal
            bool handledAsync = DialogPlayer.Instance.TryPlay(sequence, onComplete: () =>
            {
                // Wait 语义已改为挂起当前 signal 本体，continueAction 已为空操作。
                // 需通过 ResumeSuspendedSignalToTarget 显式决定下一跳。
                if (!pack.Nodes.TryGetValue(context.CurrentNodeId, out var node)
                    || node is not VFSNodeData vfsNode)
                {
                    Debug.LogError($"[DialogVFSHandler] 对话完成后找不到当前 VFS 节点: {context.CurrentNodeId}");
                    return;
                }

                if (vfsNode.ChildNodeIDs.Count == 0)
                {
                    // 没有后续节点，仅标记当前节点已处理
                    node.IsChecked = true;
                    pack.SuspendedSignals.Remove(context.SignalId);
                    Debug.Log($"[DialogVFSHandler] '{sequence.name}' 播放完毕，无后续节点");
                    return;
                }

                string targetId = vfsNode.ChildNodeIDs[0];
                bool resumed = runner.ResumeSuspendedSignalToTarget(
                    packInstanceID,
                    context.SignalId,
                    context.CurrentNodeId,
                    targetId);

                if (!resumed)
                    Debug.LogError($"[DialogVFSHandler] 恢复 signal 失败: packID={packInstanceID}, signalId={context.SignalId}");
                else
                    Debug.Log($"[DialogVFSHandler] '{sequence.name}' 播放完毕，路由到节点: {targetId}");
            });

            if (handledAsync)
                return HandleResult.Wait; // 已接管，Wait 模式下通过显式 Resume 恢复

            Debug.LogWarning($"[DialogVFSHandler] '{sequence.name}' 当前走透传模式，未阻塞图流程");
            return HandleResult.Push;
        }
}
}
