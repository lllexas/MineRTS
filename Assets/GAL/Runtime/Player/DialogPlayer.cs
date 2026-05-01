using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace GAL
{
    /// <summary>
    /// 对话播放器 - 后端推送包 → 前端自治渲染 → 回调通知完成
    /// 
    /// 自相似三层架构：
    /// VFSHandler ──[直接调用]──► DialogPlayer ──[事件+委托]──► DialoguePanel
    /// 结构相同（发包-等待-回调），实现因地制宜
    /// 
    /// 前端完全解耦：不引用任何 NekoGraph 类型
    /// 
    /// 【仲裁机制】
    /// - 同一时刻只有一个 Sequence 在播放
    /// - 新请求进入 FIFO 队列，当前 Sequence 结束后自动消费
    /// - 帧隔离：Sequence 完成回调后延迟一帧再启动下一个，避免时序冲突
    /// </summary>
    public class DialogPlayer : SingletonMono<DialogPlayer>
    {
        private Coroutine _player;                              // 当前播放协程
        private readonly Queue<Request> _queue = new();         // 等待队列
        private int _currentIndex;                              // 当前 Entry 索引
        
        private class Request
        {
            public DialogSequenceSO Sequence;
            public Action OnComplete;
        }
        
        /// <summary>
        /// VFSHandler 调用入口 - 第一层：直接调用
        /// 前端只接收 sequence 和 onComplete 回调，不碰任何 NekoGraph 类型
        /// </summary>
        public bool TryPlay(DialogSequenceSO sequence, Action onComplete)
        {
            if (sequence == null)
            {
                Debug.LogError("[DialogPlayer] sequence 为 null");
                return false;
            }

            var request = new Request { Sequence = sequence, OnComplete = onComplete };

            if (_player == null)
                _player = StartCoroutine(Play(request));
            else
                _queue.Enqueue(request);

            return true; // 已接管，后端挂起等待回调
        }
        
        /// <summary>
        /// 播放协程 - 仲裁入口
        /// 负责单个 Request 的播放，结束后自动消费队列
        /// </summary>
        IEnumerator Play(Request request)
        {
            yield return PlaySequence(request.Sequence);

            request.OnComplete?.Invoke();

            // 帧隔离：延迟一帧，确保当前帧的所有动画/事件处理完毕
            yield return null;

            // 消费队列或清空引用
            _player = _queue.Count > 0 
                ? StartCoroutine(Play(_queue.Dequeue())) 
                : null;
        }

        /// <summary>
        /// 第三层：解包为 RoutedRequest，逐条发事件+委托
        /// </summary>
        IEnumerator PlaySequence(DialogSequenceSO sequence)
        {
            PostSystem.Instance.Send("期望显示面板", "DialoguePanel");
            Debug.Log($"[DialogPlayer] 已唤起台词面板。");
            Debug.Log($"[DialogPlayer] 开始播放序列: {sequence.name} ({sequence.Entries.Count} 条)");
            
            for (_currentIndex = 0; _currentIndex < sequence.Entries.Count; _currentIndex++)
            {
                var entry = sequence.Entries[_currentIndex];
                if (entry == null) continue;
                
                bool stepComplete = false;
                Action onStepComplete = () => stepComplete = true;
                
                switch (entry)
                {
                    case DialogEntry dialog:
                        yield return PlayDialog(dialog, onStepComplete);
                        break;
                        
                    case AvatarEffectEntry avatar:
                        yield return PlayAvatar(avatar, onStepComplete);
                        break;

                    case BackgroundEffectEntry background:
                        yield return PlayBackground(background, onStepComplete);
                        break;
                        
                    case ScreenFlashEntry flash:
                        yield return PlayFlash(flash, onStepComplete);
                        break;
                        
                    case VoiceEffectEntry voice:
                        yield return PlayVoice(voice, onStepComplete);
                        break;
                        
                    case CameraEffectEntry camera:
                        yield return PlayCamera(camera, onStepComplete);
                        break;
                }
                
                yield return new WaitUntil(() => stepComplete);
            }
            
            Debug.Log($"[DialogPlayer] 序列播放完成: {sequence.name}");
            
            // 兜底回收 GAL 面板
            GALFrontend.Instance?.HideAllGalPanels();
        }
        
        // ========== 各类型条目：发事件带委托 ==========
        
        IEnumerator PlayDialog(DialogEntry dialog, Action onStepComplete)
        {
            bool lineComplete = false;
            
            // 发送【播放行】事件，逐行播放
            PostSystem.Instance.Send("播放行", new PlayLineEventData
            {
                Entry = dialog,
                OnComplete = () => lineComplete = true
            });
            
            // 等待该行播放完成
            yield return new WaitUntil(() => lineComplete);
            
            // 通知外层步骤完成
            onStepComplete?.Invoke();
        }
        
        IEnumerator PlayAvatar(AvatarEffectEntry avatar, Action onStepComplete)
        {
            Debug.Log($"[Avatar]正在变换角色槽位：{avatar.SlotIndex}");
            if (avatar == null)
            {
                onStepComplete?.Invoke();
                yield break;
            }

            int slotIndex = Mathf.Clamp(avatar.SlotIndex, 0, 4);
            string uiid = $"CharSlot{slotIndex}";

            if (avatar.Action == AvatarAction.Hide)
            {
                PostSystem.Instance.Send("期望隐藏角色", new RoutedRequest<CharacterHideData>
                {
                    uiid = uiid,
                    data = new CharacterHideData { slotIndex = slotIndex },
                    onComplete = onStepComplete
                });
                yield break;
            }

            if (avatar.Profile == null)
            {
                Debug.LogWarning("[DialogPlayer] 角色 Profile 为空");
                onStepComplete?.Invoke();
                yield break;
            }

            PostSystem.Instance.Send("期望显示角色", new RoutedRequest<CharacterShowData>
            {
                uiid = uiid,
                data = new CharacterShowData
                {
                    slotIndex = slotIndex,
                    id = avatar.Profile.CharacterId,
                    profile = avatar.Profile,
                    emotionKey = avatar.EmotionKey,
                    fromLeft = avatar.FromLeft
                },
                onComplete = onStepComplete
            });
            
            yield return new WaitForSeconds(0.1f);
        }

        IEnumerator PlayBackground(BackgroundEffectEntry background, Action onStepComplete)
        {
            if (background?.Preset == null)
            {
                Debug.LogWarning("[DialogPlayer] 背景预设为空");
                onStepComplete?.Invoke();
                yield break;
            }

            PostSystem.Instance.Send("期望显示面板", "BackgroundAnimator");
            
            PostSystem.Instance.Send("期望切换背景", new RoutedRequest<BackgroundChangeData>
            {
                uiid = "BackgroundAnimator",
                data = new BackgroundChangeData
                {
                    sprite = background.Preset.MainSprite,
                    fadeType = background.Preset.DefaultFade
                },
                onComplete = onStepComplete
            });
            
            // 等待一帧让动画开始
            yield return null;
        }
        
        IEnumerator PlayFlash(ScreenFlashEntry flash, Action onStepComplete)
        {
            var transType = flash.FlashType == ScreenFlashType.White
                ? TransitionType.FlashWhite
                : TransitionType.FadeToBlack;
                
            PostSystem.Instance.Send("期望显示面板", new RoutedRequest<TransitionData>
            {
                uiid = "TransitionManager",
                data = new TransitionData
                {
                    type = transType,
                    duration = flash.Duration
                },
                onComplete = onStepComplete
            });
            
            yield return new WaitForSeconds(flash.Duration);
        }
        
        IEnumerator PlayVoice(VoiceEffectEntry voice, Action onStepComplete)
        {
            // TODO: 接入音频系统
            Debug.Log($"[DialogPlayer] 播放语音: {voice?.VoiceClip?.name}");
            yield return null;
            onStepComplete?.Invoke();
        }
        
        IEnumerator PlayCamera(CameraEffectEntry camera, Action onStepComplete)
        {
            if (CameraDirector.Instance == null)
            {
                Debug.LogWarning("[DialogPlayer] CameraDirector 不存在");
                onStepComplete?.Invoke();
                yield break;
            }

            bool completed = false;
            float? durationOverride = camera.UseDurationOverride ? camera.DurationOverride : null;
            float? intensityOverride = camera.UseIntensityOverride ? camera.IntensityOverride : null;
            CameraDirector.Instance.PlayEffect(camera.EffectKey, durationOverride, intensityOverride, () => completed = true);
            yield return new WaitUntil(() => completed);
            onStepComplete?.Invoke();
        }
    }
}
