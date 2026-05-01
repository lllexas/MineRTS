using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAL
{
    /// <summary>
    /// 镜头效果库 - 通过 EffectKey 查找默认镜头效果配置
    /// </summary>
    [CreateAssetMenu(fileName = "NewCameraEffectLibrary", menuName = "GAL01/Dialog/Camera Effect Library")]
    public class CameraEffectLibrarySO : ScriptableObject
    {
        [Serializable]
        public class NamedEffect
        {
            [Tooltip("效果标识名，如 '强烈震动'、'温柔缩放'")]
            public string Key;

            [Tooltip("效果配置")]
            public CameraEffectConfig Config;
        }

        [Tooltip("预定义效果列表")]
        public List<NamedEffect> Effects = new();

        /// <summary>
        /// 根据 Key 获取效果配置
        /// </summary>
        public bool TryGetEffect(string key, out CameraEffectConfig config)
        {
            foreach (var effect in Effects)
            {
                if (effect.Key == key)
                {
                    config = effect.Config;
                    return true;
                }
            }
            config = default;
            return false;
        }

        /// <summary>
        /// 添加或更新效果
        /// </summary>
        public void SetEffect(string key, CameraEffectConfig config)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].Key == key)
                {
                    Effects[i] = new NamedEffect { Key = key, Config = config };
                    return;
                }
            }
            Effects.Add(new NamedEffect { Key = key, Config = config });
        }

        /// <summary>
        /// 获取所有效果名称
        /// </summary>
        public List<string> GetAllKeys()
        {
            var keys = new List<string>();
            foreach (var effect in Effects)
            {
                if (!string.IsNullOrEmpty(effect.Key))
                    keys.Add(effect.Key);
            }
            return keys;
        }

        /// <summary>
        /// 编辑器辅助：初始化常用效果
        /// </summary>
        [ContextMenu("初始化常用效果")]
        private void InitCommonEffects()
        {
            Effects.Clear();
            
            Effects.Add(new NamedEffect 
            { 
                Key = "轻微震动", 
                Config = new CameraEffectConfig 
                { 
                    EffectType = CameraEffectType.ShakeRandom, 
                    Duration = 0.2f, 
                    Intensity = 0.2f,
                    Vibrato = 5,
                    Randomness = 0.3f
                } 
            });
            
            Effects.Add(new NamedEffect 
            { 
                Key = "强烈震动", 
                Config = new CameraEffectConfig 
                { 
                    EffectType = CameraEffectType.ShakeRandom, 
                    Duration = 0.5f, 
                    Intensity = 0.8f,
                    Vibrato = 20,
                    Randomness = 0.9f
                } 
            });
            
            Effects.Add(new NamedEffect 
            { 
                Key = "拉近聚焦", 
                Config = new CameraEffectConfig 
                { 
                    EffectType = CameraEffectType.ZoomIn, 
                    Duration = 0.8f, 
                    TargetZoom = 1.3f,
                    EaseType = DG.Tweening.Ease.InOutCubic
                } 
            });
            
            Effects.Add(new NamedEffect 
            { 
                Key = "拉远恢复", 
                Config = new CameraEffectConfig 
                { 
                    EffectType = CameraEffectType.ZoomOut, 
                    Duration = 0.8f, 
                    TargetZoom = 1.0f,
                    EaseType = DG.Tweening.Ease.InOutCubic
                } 
            });
            
            Effects.Add(new NamedEffect 
            { 
                Key = "闪白", 
                Config = new CameraEffectConfig 
                { 
                    EffectType = CameraEffectType.FlashWhite, 
                    Duration = 0.15f 
                } 
            });
            
            Effects.Add(new NamedEffect 
            { 
                Key = "闪黑", 
                Config = new CameraEffectConfig 
                { 
                    EffectType = CameraEffectType.FlashBlack, 
                    Duration = 0.3f 
                } 
            });

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
