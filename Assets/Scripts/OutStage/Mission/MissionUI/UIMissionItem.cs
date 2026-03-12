using UnityEngine;
using UnityEngine.UI;
using System.Text;
using TMPro;

/// <summary>
/// UI 任务物品 - 已休眠喵~
/// 注：旧的任务目标系统已废弃，MissionNode_A_Data 不再包含 Goals 属性
/// 新架构：任务目标由流程图中的 TriggerNode + CommandNode 定义
/// TODO: 等待新任务系统的 UI 实现
/// </summary>
public class UIMissionItem : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text descText;
    public TMP_Text goalsText; // 这里可以用一个 Text 拼出所有目标，也可以用多个 Prefab

    private StringBuilder _sb = new StringBuilder();

    public void Setup(MissionNode_A_Data data)
    {
        // 已休眠 - 等待新任务系统实现喵~
        Debug.LogWarning("<color=orange>[UIMissionItem]</color> Setup() 已休眠，旧任务系统已废弃喵~");
        
        if (titleText != null)
            titleText.text = data.Title;

        // 这里的描述如果太长可以做截断
        if (descText != null)
            descText.text = data.Description;

        // 旧的目标显示逻辑已废弃喵~
        // 新架构中任务目标由流程图定义，不再由 UI 直接显示
        if (goalsText != null)
            goalsText.text = "<color=gray>任务系统重构中...</color>";
        
        // 旧代码已注释，保留供参考喵~
        /*
        _sb.Length = 0;
        foreach (var goal in data.Goals)
        {
            // 根据 GoalType 转换成亲切的中文喵~
            string goalTypeName = GetGoalTypeChinese(goal.Type);

            // 格式：● 建造钻头：2/5
            _sb.Append(goal.IsReached ? "<color=green>✔ " : "<color=orange>● ");
            _sb.Append(goalTypeName);
            if (!string.IsNullOrEmpty(goal.TargetKey)) _sb.Append($"[{goal.TargetKey}]");
            _sb.Append($": {goal.CurrentAmount}/{goal.RequiredAmount}</color>\n");
        }

        goalsText.text = _sb.ToString();
        */
    }

    /// <summary>
    /// 获取目标类型的中文名称 - 已休眠喵~
    /// </summary>
    private string GetGoalTypeChinese(GoalType type)
    {
        switch (type)
        {
            case GoalType.BuildEntity: return "建造";
            case GoalType.KillEntity: return "击败";
            case GoalType.SellResource: return "出售";
            case GoalType.ReachPosition: return "抵达";
            case GoalType.SurviveTime: return "生存时间";
            case GoalType.EarnMoney: return "赚取金币";
            default: return "未知目标";
        }
    }
}
