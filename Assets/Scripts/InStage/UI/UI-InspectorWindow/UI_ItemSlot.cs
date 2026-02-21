using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_ItemSlot : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text countText;

    // 缓存一下上一次的 ItemType，避免每帧重复 SetSprite
    private int _lastItemType = -1;
    private int _lastCount = -1;

    public void Refresh(int itemType, int count)
    {
        // 只有数据变了才更新 UI，节省性能喵
        if (itemType != _lastItemType)
        {
            _lastItemType = itemType;
            if (itemType == 0)
            {
                // 空槽位：透明或显示默认底图
                iconImage.color = Color.clear;
                iconImage.sprite = null;
            }
            else
            {
                // 有物品：显示图标
                iconImage.color = Color.white;
                // 这里假设 SpriteLib 有个方法能拿到物品图标，如果没有请看下面的补充
                // 暂时假设 itemType 1=Iron, 2=Copper
                string bpKey = (itemType == 1) ? "ore_iron" : "ore_copper";
                var bp = BlueprintRegistry.Get(bpKey);
                iconImage.sprite = SpriteLib.Instance.unitSprites[bp.SpriteId];
            }
        }

        if (count != _lastCount)
        {
            _lastCount = count;
            countText.text = count > 0 ? count.ToString() : "";
        }
    }
}