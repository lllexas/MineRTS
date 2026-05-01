using UnityEngine;
using SpaceTUI;
using NekoGraph;
using System.Collections.Generic;

namespace GAL
{
    /// <summary>
    /// GAL 前端总控 - 兜底管理所有子面板的隐藏
    /// </summary>
    public class GALFrontend : MonoBehaviour
    {
        public static GALFrontend Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
        
        /// <summary>
        /// 兜底：隐藏所有子面板
        /// 遍历所有子物体的 SpaceUIAnimator，发送隐藏事件
        /// </summary>
        public void HideAllGalPanels()
        {
            var uiids = CollectAllUIIDs();
            
            foreach (string uiid in uiids)
            {
                PostSystem.Instance.Send("期望隐藏面板", uiid);
            }
            
            Debug.Log($"[GALFrontend] Sequence结束，兜底隐藏 {uiids.Count} 个面板");
        }
        
        /// <summary>
        /// 收集所有子物体的 SpaceUIAnimator 的 UIID
        /// </summary>
        List<string> CollectAllUIIDs()
        {
            var result = new List<string>();
            var animators = GetComponentsInChildren<SpaceUIAnimator>(true);
            
            foreach (var animator in animators)
            {
                string uiid = animator.GetUIID();
                if (!string.IsNullOrEmpty(uiid) && !result.Contains(uiid))
                {
                    result.Add(uiid);
                }
            }
            
            return result;
        }
    }
}
