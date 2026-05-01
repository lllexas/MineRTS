using System;

namespace GAL
{
    /// <summary>
    /// 单个 ChoiceBar 接收的事件数据
    /// </summary>
    public class ChoiceBarEventData
    {
        public int OptionIndex;
        public string OptionText;
        public int TotalOptions;      // 总选项数（用于 ChoicePanel 布局）
        public Action<int> OnSelect;  // 点击时调用 OnSelect(OptionIndex)
    }
}
