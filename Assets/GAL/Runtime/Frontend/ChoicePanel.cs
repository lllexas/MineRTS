using UnityEngine;
using System.Collections.Generic;

namespace GAL
{
    /// <summary>
    /// 选项面板 - 纯布局管理（园丁）
    ///
    /// 职责：
    /// 1. 持有若干个预制化的 ChoiceBar 子物体（0-4 个等）
    /// 2. 在【设置选项】阶段，计算各 Bar 的最终位置
    /// 3. 不知道、不关心 VFS.choice 的存在
    ///
    /// 工作流：
    /// - PostSystem 发 "设置选项" 事件
    ///   → ChoicePanel [priority: 100] 先收到，计算位置，设置子物体 anchoredPosition
    ///   → ChoiceBar[i] [priority: 50-i] 后收到，获取文本和回调
    /// - PostSystem 发 "期望显示面板"
    ///   → ChoicePanel 和各 ChoiceBar 都 FadeIn
    /// </summary>
    public class ChoicePanel : MonoBehaviour
    {
        [Header("布局配置")]
        [SerializeField] private float barSpacing = 80f;  // Bar 之间的距离

        private List<ChoiceBar> _childBars = new List<ChoiceBar>();

        void Awake()
        {
            // 扫描子物体，找到所有 ChoiceBar
            _childBars.Clear();
            foreach (Transform child in transform)
            {
                var bar = child.GetComponent<ChoiceBar>();
                if (bar != null)
                    _childBars.Add(bar);
            }

            // 注册到 PostSystem（反射式）
            PostSystem.Instance.Register(this);

            Debug.Log($"[ChoicePanel] 已扫描 {_childBars.Count} 个子 ChoiceBar");
        }

        void OnDestroy()
        {
            if (PostSystem.Instance != null)
                PostSystem.Instance.Unregister(this);
        }

        /// <summary>
        /// 【设置选项】阶段 - 计算位置（priority 最高，最先执行）
        /// </summary>
        /// <summary>
        /// 【设置选项】阶段 - 计算位置（priority 最高，最先执行）
        ///
        /// 职责：
        /// 1. 根据 TotalOptions 隐藏多余的 ChoiceBar
        /// 2. 为每个有效的 ChoiceBar 计算并设置位置
        /// 3. 调整整体容器高度
        /// </summary>
        [Subscribe("设置选项", priority: 100)]
        public void OnSetChoiceOption(object data)
        {
            if (data is not ChoiceBarEventData eventData)
                return;

            int totalOptions = eventData.TotalOptions;

            // 【只在第一个选项时】进行全局布局设置
            if (eventData.OptionIndex == 0)
            {
                LayoutAllBars(totalOptions);
            }
        }

        private void LayoutAllBars(int optionCount)
        {
            // 根据实际选项数隐藏多余的 Bar
            for (int i = 0; i < _childBars.Count; i++)
            {
                var bar = _childBars[i];

                if (i < optionCount)
                {
                    bar.TeleportTo(new Vector2(0, -i * barSpacing));
                }
                else
                {
                    bar.Hide();
                }
            }

            // 调整面板自身高度
            var panelRect = GetComponent<RectTransform>();
            if (panelRect != null)
            {
                float totalHeight = optionCount * barSpacing;
                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, totalHeight);
            }

            Debug.Log($"[ChoicePanel] 已布局 {optionCount} 个选项，总高度: {optionCount * barSpacing}");
        }
    }
}
