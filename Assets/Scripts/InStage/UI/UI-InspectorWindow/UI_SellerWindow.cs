using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_SellerWindow : UI_BaseEntityWindow
{
    [Header("控件")]
    public TMP_Text totalGoldText;
    public UI_ItemSlot inputSlot;

    protected override void OnRefresh(WholeComponent whole)
    {
        int idx = EntitySystem.Instance.GetIndex(targetHandle);
        ref var inv = ref whole.inventoryComponent[idx];

        // 1. 显示全局金钱 (因为 Seller 只是个通道，它自己不存私房钱)
        // 使用 N0 格式化 (1,000,000)
        totalGoldText.text = $"公司资产: ${IndustrialSystem.Instance.Gold:N0}";

        // 2. 显示正在吞噬的物品 (Input0)
        ref var inData = ref inv.GetInput(0);
        inputSlot.Refresh(inData.ItemType, inData.Count);
    }
}