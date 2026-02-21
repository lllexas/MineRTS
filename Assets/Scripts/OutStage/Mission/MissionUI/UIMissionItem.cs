using UnityEngine;
using UnityEngine.UI;
using System.Text;
using TMPro;

public class UIMissionItem : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text descText;
    public TMP_Text goalsText; // 这里可以用一个 Text 拼出所有目标，也可以用多个 Prefab

    private StringBuilder _sb = new StringBuilder();

    public void Setup(MissionData data)
    {
        titleText.text = data.Title;

        // 这里的描述如果太长可以做截断
        if (descText != null) descText.text = data.Description;

        // 拼接目标进度喵
        _sb.Length = 0;
        foreach (var goal in data.Goals)
        {
            // 根据 GoalType 转换成亲切的中文喵
            string goalTypeName = GetGoalTypeChinese(goal.Type);

            // 格式: ● 建造钻头: 2/5
            _sb.Append(goal.IsReached ? "<color=green>✔ " : "<color=orange>● ");
            _sb.Append(goalTypeName);
            if (!string.IsNullOrEmpty(goal.TargetKey)) _sb.Append($"[{goal.TargetKey}]");
            _sb.Append($": {goal.CurrentAmount}/{goal.RequiredAmount}</color>\n");
        }

        goalsText.text = _sb.ToString();
    }

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