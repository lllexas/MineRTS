using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 电线杆/电网桩的检视面板
/// 重点展示：连接状态、所属电网拓扑概况
/// </summary>
public class UI_PowerPoleWindow : UI_BaseEntityWindow
{
    [Header("状态显示")]
    public TMP_Text connectionStatusText; // "已联网" 或 "离线"
    public Image statusLight;             // 绿灯或红灯

    [Header("电网统计面板")]
    public TMP_Text gridIDText;           // "电网编号: #12"
    public TMP_Text powerBalanceText;     // "净功率: +250 J/s"
    public TMP_Text satisfactionText;      // "满足率: 100%"
    public Slider satisfactionSlider;     // 满足率进度条（红色到绿色的渐变）

    [Header("全网储能摘要")]
    public Slider globalStorageSlider;
    public TMP_Text globalStorageText;

    protected override void OnRefresh(WholeComponent whole)
    {
        int idx = EntitySystem.Instance.GetIndex(targetHandle);
        if (idx == -1) return;

        ref var power = ref whole.powerComponent[idx];

        // 1. 检查联网状态
        var net = PowerSystem.Instance.GetNetDetails(power.NetID);

        if (net != null)
        {
            // --- 联网状态 ---
            connectionStatusText.text = "<color=green>● 链路已建立</color>";
            if (statusLight != null) statusLight.color = Color.green;

            // --- 电网基本统计 ---
            gridIDText.text = $"电网 ID: #{net.NetID}";

            float netPower = net.TotalProduction - net.TotalDemand;
            string sign = netPower >= 0 ? "+" : "";
            powerBalanceText.text = $"全网产出: {net.TotalProduction:F0} J/s\n" +
                                   $"全网需求: {net.TotalDemand:F0} J/s\n" +
                                   $"净功率: {sign}{netPower:F0} J/s";

            // --- 满足率可视化 ---
            satisfactionText.text = $"供电满足率: {(net.Satisfaction * 100):F0}%";
            satisfactionSlider.value = net.Satisfaction;
            // 满足率低的时候，进度条变红，喵！
            if (satisfactionSlider.fillRect != null)
                satisfactionSlider.fillRect.GetComponent<Image>().color = Color.Lerp(Color.red, Color.green, net.Satisfaction);

            // --- 储能摘要 ---
            if (net.TotalStorage > 0)
            {
                globalStorageSlider.gameObject.SetActive(true);
                globalStorageSlider.value = net.CurrentStorage / net.TotalStorage;
                globalStorageText.text = $"全网储能: {net.CurrentStorage:F0} / {net.TotalStorage:F0} J";
            }
            else
            {
                globalStorageSlider.gameObject.SetActive(false);
                globalStorageText.text = "电网内无储能设备";
            }
        }
        else
        {
            // --- 离线状态 ---
            connectionStatusText.text = "<color=red>○ 链路中断</color>";
            if (statusLight != null) statusLight.color = Color.red;

            gridIDText.text = "电网 ID: 未知";
            powerBalanceText.text = "请检查附近的连接范围喵！";
            satisfactionText.text = "满足率: 0%";
            satisfactionSlider.value = 0;
            globalStorageSlider.value = 0;
            globalStorageText.text = "-";
        }
    }
}