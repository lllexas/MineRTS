using UnityEngine;
using UnityEngine.UI;

public class UI_BufferWindow : UI_BaseEntityWindow
{
    [Header("控件")]
    // 在 Inspector 里把 3 个输入 UI 和 3 个输出 UI 拖进去
    public UI_ItemSlot[] inputSlots;  // Size = 3
    public UI_ItemSlot[] outputSlots; // Size = 3

    protected override void OnRefresh(WholeComponent whole)
    {
        int idx = EntitySystem.Instance.GetIndex(targetHandle);
        ref var inv = ref whole.inventoryComponent[idx];

        // 刷新输入槽 (0-2)
        for (int i = 0; i < inputSlots.Length; i++)
        {
            if (i < inv.InputSlotCount)
            {
                ref var slot = ref inv.GetInput(i);
                inputSlots[i].gameObject.SetActive(true);
                inputSlots[i].Refresh(slot.ItemType, slot.Count);
            }
            else
            {
                // 如果蓝图只定义了2个槽，隐藏第3个 UI
                inputSlots[i].gameObject.SetActive(false);
            }
        }

        // 刷新输出槽 (0-2)
        for (int i = 0; i < outputSlots.Length; i++)
        {
            if (i < inv.OutputSlotCount)
            {
                ref var slot = ref inv.GetOutput(i);
                outputSlots[i].gameObject.SetActive(true);
                outputSlots[i].Refresh(slot.ItemType, slot.Count);
            }
            else
            {
                outputSlots[i].gameObject.SetActive(false);
            }
        }
    }
}