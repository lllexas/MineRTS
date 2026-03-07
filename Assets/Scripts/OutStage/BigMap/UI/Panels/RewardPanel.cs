using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 奖励面板
    /// 显示任务完成后的奖励内容
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

        // 当前奖励数据
        private MissionReward _currentReward;

        private void Awake()
        {
            base.Awake();

            // 绑定按钮事件
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);

            Debug.Log("<color=cyan>[RewardPanel]</color> 初始化完成");
        }

        /// <summary>
        /// 设置奖励数据
        /// </summary>
        public void Setup(MissionReward rewardData)
        {
            _currentReward = rewardData;

            // 设置标题
            if (_titleText != null)
                _titleText.text = "任务完成！";

            // 构建奖励文本
            if (_rewardText != null)
            {
                string rewardText = BuildRewardText(rewardData);
                _rewardText.text = rewardText;
            }

            // 创建奖励图标（可选）
            CreateRewardIcons(rewardData);
        }

        /// <summary>
        /// 构建奖励文本
        /// </summary>
        private string BuildRewardText(MissionReward reward)
        {
            List<string> rewardLines = new List<string>();

            if (reward.Money > 0)
            {
                rewardLines.Add($"<color=gold>💰 金币 +{reward.Money}</color>");
            }

            if (reward.TechPoints > 0)
            {
                rewardLines.Add($"<color=cyan>🔬 科技点 +{reward.TechPoints}</color>");
            }

            if (reward.Blueprints != null && reward.Blueprints.Count > 0)
            {
                rewardLines.Add($"<color=green>📐 解锁图纸 x{reward.Blueprints.Count}</color>");
            }

            return string.Join("\n", rewardLines.ToArray());
        }

        /// <summary>
        /// 创建奖励图标
        /// </summary>
        private void CreateRewardIcons(MissionReward reward)
        {
            if (_rewardIconsContainer == null) return;

            // 清空现有图标
            foreach (Transform child in _rewardIconsContainer)
            {
                Destroy(child.gameObject);
            }

            // 这里可以添加图标生成逻辑
            // 目前先用文本显示
        }

        // =========================================================
        //  按钮事件处理
        // =========================================================

        /// <summary>
        /// 确认按钮点击
        /// </summary>
        private void OnConfirmClicked()
        {
            Debug.Log("<color=cyan>[RewardPanel]</color> 奖励已确认");
            Close();
        }
    }
}
