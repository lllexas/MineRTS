using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_BatteryWindow : UI_BaseEntityWindow
{
    [Header("蓄电池控件")]
    public Slider energySlider;     // 电量百分比
    public TMP_Text energyValueText; // 显示 "850 / 1000 J"
    public TMP_Text flowStatusText;  // 显示 "充电中"、"放电中" 或 "停滞"
    public Image flowIcon;           // 可以用一个小箭头图标旋转 180 度表示充放电

    private float _lastEnergy = 0; // 用于计算充放电趋势

    [Header("电网全局控件")]
    public TMP_Text gridInfoText;    // 显示电网 ID, 满足率, 净功率
    public Slider globalStorageSlider; // 全网总蓄电量进度
    public TMP_Text globalStorageText; // "全网储能: 5000 / 10000 J"

    protected override void OnRefresh(WholeComponent whole)
    {
        int idx = EntitySystem.Instance.GetIndex(targetHandle);
        ref var power = ref whole.powerComponent[idx];

        // 1. 电量条和数值
        if (power.Capacity > 0)
        {
            energySlider.value = power.StoredEnergy / power.Capacity;
            energyValueText.text = $"{power.StoredEnergy:F0} / {power.Capacity:F0} J";
        }

        // 2. 充放电状态判定
        // 我们通过比较当前电量和上一帧刷新的电量来判断趋势喵！
        float diff = power.StoredEnergy - _lastEnergy;

        if (diff > 0.01f)
        {
            flowStatusText.text = $"<color=green>↑ 充电中</color>";
            if (flowIcon != null) flowIcon.color = Color.green;
        }
        else if (diff < -0.01f)
        {
            flowStatusText.text = $"<color=yellow>↓ 放电中</color>";
            if (flowIcon != null) flowIcon.color = Color.yellow;
        }
        else
        {
            // 充满或者电网平衡时
            if (power.StoredEnergy >= power.Capacity * 0.99f)
                flowStatusText.text = $"<color=white>● 已充满</color>";
            else
                flowStatusText.text = $"<color=gray>○ 置空</color>";

            if (flowIcon != null) flowIcon.color = Color.gray;
        }

        _lastEnergy = power.StoredEnergy;

        // 3. 【核心新增】电网全局情况
        var net = PowerSystem.Instance.GetNetDetails(power.NetID);
        if (net != null)
        {
            // A. 计算净功率 (产出 - 需求)
            // 如果净功率 > 0，全网电池都在充电；< 0 则在放电。
            float netPower = net.TotalProduction - net.TotalDemand;
            string netPowerSign = netPower > 0 ? "+" : "";
            string netColor = netPower >= 0 ? "green" : "red";

            // B. 电网基本状态文本
            gridInfoText.text = $"所属电网: <color=yellow>#{net.NetID}</color>\n" +
                               $"供需平衡: <color={netColor}>{netPowerSign}{netPower:F0} J/s</color>\n" +
                               $"电网满足率: {(net.Satisfaction * 100):F0}%";

            // C. 全网储能进度条
            if (net.TotalStorage > 0)
            {
                globalStorageSlider.value = net.CurrentStorage / net.TotalStorage;
                globalStorageText.text = $"全网总储备: {net.CurrentStorage:F0} / {net.TotalStorage:F0} J";
            }
            else
            {
                globalStorageSlider.value = 0;
                globalStorageText.text = "全网无储备容量";
            }
        }
        else
        {
            gridInfoText.text = "<color=gray>未接入任何电网喵...</color>";
            globalStorageSlider.value = 0;
            globalStorageText.text = "-";
        }
    }
}