using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingSlotUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public Image highlightFrame;
    public string blueprintKey;

    public void Setup(string key, EntityBlueprint bp)
    {
        blueprintKey = key;
        nameText.text = bp.Name;
        // 从 SpriteLib 获取图标
        iconImage.sprite = SpriteLib.Instance.unitSprites[bp.SpriteId];
        if (highlightFrame) highlightFrame.enabled = false;

        // 绑定点击事件
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        BuildingController.Instance.SetCurrentBlueprint(blueprintKey);
        BuildingUIView.Instance.OnSlotSelected(blueprintKey);
    }

    public void SetHighlight(bool active)
    {
        if (highlightFrame) highlightFrame.enabled = active;
    }
}