using UnityEngine;
using UnityEngine.EventSystems;
using SpaceTUI;
using System;

namespace GAL
{
    /// <summary>
    /// 单个选项条 - SpaceUIAnimator
    ///
    /// 职责：
    /// 1. 有固定的 optionIndex（0-4 等，在编辑器中配置）
    /// 2. 通过 [Subscribe] 反射式订阅 "显示选项" 事件
    /// 3. 事件回调中判断 eventData.OptionIndex 是否匹配自己
    /// 4. 显示选项文本，等待玩家点击
    /// 5. 点击时回调给 ChoicePlayer
    ///
    /// 完全解耦：不引用任何 NekoGraph 类型
    ///
    /// UIID 格式：ChoiceBar{optionIndex}  (例: ChoiceBar0, ChoiceBar1, ...)
    /// 参见 Doc/UIID.md 中的命名规范
    /// </summary>
    public class ChoiceBar : SpaceUIAnimator
    {
        [Header("选项配置")]
        [SerializeField] private int optionIndex = 0;
        [SerializeField] private TMPro.TextMeshProUGUI label;

        protected override string UIID => $"ChoiceBar{optionIndex}";

        private Action<int> _onSelect;

        protected override void Awake()
        {
            base.Awake();  // 必须先调用基类初始化（包括 _canvasGroup）

            // 动态订阅，根据 optionIndex 设置 priority
            // Bar0=priority:50, Bar1=priority:49, Bar2=priority:48...
            int priority = 50 - optionIndex;
            PostSystem.Instance.On("设置选项", OnSetChoiceOption, priority);
        }

        void Start()
        {
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标点击 += OnMouseClick;
            Hide();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            鼠标点击 -= OnMouseClick;
        }

        /// <summary>
        /// 接收 "设置选项" 事件（【设置阶段】）
        /// 根据 optionIndex 自动计算 priority，确保 Bar0 最后收到、Bar4 最早收到
        /// </summary>
        [Subscribe("设置选项")]
        private void OnSetChoiceOption(object data)
        {
            if (data is not ChoiceBarEventData eventData)
            {
                return;
            }

            // 只处理发给自己的选项
            if (eventData.OptionIndex != optionIndex)
            {
                return;
            }

            _onSelect = eventData.OnSelect;
            if (label != null)
                label.text = eventData.OptionText;

            Debug.Log($"[ChoiceBar[{optionIndex}]] 已被设置为选项：{eventData.OptionText}");
        }

        private void OnMouseClick(PointerEventData eventData)
        {
            Debug.Log($"[ChoiceBar[{optionIndex}]] 用户点击");
            _onSelect?.Invoke(optionIndex);
            _onSelect = null;
        }

        /// <summary>
        /// 清空状态（一重保险）
        /// </summary>
        [Subscribe("清空选项")]
        private void OnClearChoiceOption(object data)
        {
            _onSelect = null;
            if (label != null)
                label.text = "";
            Debug.Log($"[ChoiceBar[{optionIndex}]] 已清空");
        }

        private void OnShowPanel(object data)
        {
            if (_onSelect != null)  // 只有在有待选事件时才显示
                FadeIn();
        }

        private void OnHidePanel(object data)
        {
            FadeOut();
        }

        protected override void CloseAction()
        {
            FadeOut();
        }
    }
}
