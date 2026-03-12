#if false   // 【OLD】已废弃，保留参考喵~
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 奖励面板 - 已休眠喵~
    /// 注：MissionReward 系统已重构，此面板暂时停用
    /// TODO: 等待新的奖励系统实现后再启用
    /// </summary>
    public class RewardPanel : AnimatedPanelBase
    {
        [Header("文本组件")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _rewardText;

        [Header("奖励图标容器")]
        [SerializeField] private Transform _rewardIconsContainer;

        [Header("按钮")]
        [SerializeField] private Button _confirmButton;

        // 当前奖励数据 - 已注释，等待新的奖励系统
        // private MissionReward _currentReward;

        private void Awake()
        {
            base.Awake();

            // 绑定按钮事件
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);

            // 休眠日志
            Debug.Log("<color=cyan>[RewardPanel]</color> 面板已加载（休眠状态，等待新奖励系统）");
        }

        /// <summary>
        /// 设置奖励数据 - 已停用喵~
        /// </summary>
        public void Setup(object rewardData)
        {
            // TODO: 等待新的奖励系统实现
            Debug.LogWarning("<color=orange>[RewardPanel]</color> Setup() 已停用，等待新奖励系统实现");

            // 设置标题
            if (_titleText != null)
                _titleText.text = "奖励系统";

            // 设置提示文本
            if (_rewardText != null)
            {
                _rewardText.text = "<color=gray>奖励系统重构中...</color>";
            }
        }

        /// <summary>
        /// 构建奖励文本 - 已停用喵~
        /// </summary>
        private string BuildRewardText(object reward)
        {
            return "<color=gray>奖励系统重构中...</color>";
        }

        /// <summary>
        /// 创建奖励图标 - 已停用喵~
        /// </summary>
        private void CreateRewardIcons(object reward)
        {
            if (_rewardIconsContainer == null) return;

            // 清空现有图标
            foreach (Transform child in _rewardIconsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // =========================================================
        //  按钮事件处理
        // =========================================================

        /// <summary>
        /// 确认按钮点击
        /// </summary>
        private void OnConfirmClicked()
        {
            Debug.Log("<color=cyan>[RewardPanel]</color> 奖励面板已关闭（休眠状态）");
            Close();
        }
    }
}
#endif
