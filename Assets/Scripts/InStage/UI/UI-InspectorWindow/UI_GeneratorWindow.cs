using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_GeneratorWindow : UI_BaseEntityWindow
{
    [Header("发电机特有控件")]
    public Slider fuelProgressSlider; // 展示当前那份燃料还能烧多久
    public TMP_Text powerStatusText;  // 显示 "正在发电: 50W" 或 "停机"
    public UI_ItemSlot fuelInputSlot; // 燃料输入槽显示

    [Header("电网全局信息")]
    public TMP_Text gridInfoText; // <--- 新增：显示电网概况
    protected override void OnRefresh(WholeComponent whole)
    {
        int idx = EntitySystem.Instance.GetIndex(targetHandle);
        ref var work = ref whole.workComponent[idx];
        ref var inv = ref whole.inventoryComponent[idx];
        ref var power = ref whole.powerComponent[idx];

        // 1. 燃料燃烧进度 (从 1 降到 0)
        fuelProgressSlider.value = work.Progress;

        // 2. 状态文字与颜色
        if (work.Progress > 0)
        {
            powerStatusText.text = $"<color=green>正在发电: {power.Production:F0}W</color>";
        }
        else
        {
            powerStatusText.text = "<color=red>缺少燃料</color>";
        }

        // 3. 刷新燃料槽 (假设燃料在 Input0)
        ref var fuelData = ref inv.GetInput(0);
        fuelInputSlot.Refresh(fuelData.ItemType, fuelData.Count);

        // 4. 电网全局信息展示
        var net = PowerSystem.Instance.GetNetDetails(power.NetID);
        if (net != null)
        {
            string color = net.Satisfaction >= 1.0f ? "white" : "orange";
            gridInfoText.text = $"电网ID: #{net.NetID}\n" +
                               $"总产出: {net.TotalProduction:F0} J/s\n" +
                               $"总需求: {net.TotalDemand:F0} J/s\n" +
                               $"<color={color}>满足率: {(net.Satisfaction * 100):F0}%</color>";
        }
        else
        {
            gridInfoText.text = "<color=gray>未接入电网</color>";
        }
    }
}