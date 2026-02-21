using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_MinerWindow : UI_BaseEntityWindow
{
    [Header("控件")]
    public Slider progressSlider;
    public TMP_Text statusText;
    public UI_ItemSlot outputSlot; // 拖入刚才做的 Slot 组件

    protected override void OnRefresh(WholeComponent whole)
    {
        int idx = EntitySystem.Instance.GetIndex(targetHandle);
        ref var work = ref whole.workComponent[idx];
        ref var inv = ref whole.inventoryComponent[idx];
        // 拿到电力组件查看满足率喵
        ref var power = ref whole.powerComponent[idx];

        // 1. 进度条
        progressSlider.value = work.Progress;

        // 2. 状态判断逻辑升级
        float satisfaction = power.CurrentSatisfaction;

        if (satisfaction <= 0.01f && work.RequiresPower)
        {
            statusText.text = "<color=red>电力断绝</color>";
        }
        else if (inv.GetOutput(0).IsFull)
        {
            statusText.text = "<color=yellow>出口堵塞</color>";
        }
        else if (satisfaction < 0.99f && work.RequiresPower)
        {
            // 增加低效运转显示
            int percent = Mathf.RoundToInt(satisfaction * 100);
            statusText.text = $"<color=#FFA500>低效运转 ({percent}%)</color>";
        }
        else
        {
            statusText.text = "<color=green>正常运转</color>";
        }

        // 3. 输出槽显示 (保持不变)
        ref var outData = ref inv.GetOutput(0);
        outputSlot.Refresh(outData.ItemType, outData.Count);
    }
}