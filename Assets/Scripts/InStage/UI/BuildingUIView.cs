using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class BuildingUIView : SingletonMono<BuildingUIView>
{
    [Header("UI 引用")]
    public Transform contentRoot;      // 按钮生成的父节点 (通常带 HorizontalLayoutGroup)
    public GameObject slotPrefab;      // 建筑槽位的预制件

    [Header("配置")]
    // 想要显示在快捷栏里的蓝图 Key 列表
    public List<string> buildableKeys = new List<string> {
        "miner", "conveyor", "generator", "power_pole", "battery", "buffer", "seller"
    };

    private List<BuildingSlotUI> _activeSlots = new List<BuildingSlotUI>();

    private void Start()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        // 1. 清理旧按钮
        foreach (var slot in _activeSlots) Destroy(slot.gameObject);
        _activeSlots.Clear();

        // 2. 根据蓝图列表生成新按钮
        foreach (var key in buildableKeys)
        {
            var bp = BlueprintRegistry.Get(key);
            if (string.IsNullOrEmpty(bp.Name)) continue;

            GameObject go = Instantiate(slotPrefab, contentRoot);
            BuildingSlotUI slotScript = go.GetComponent<BuildingSlotUI>();

            // 初始化槽位显示
            slotScript.Setup(key, bp);
            _activeSlots.Add(slotScript);
        }
    }

    // 当某个建筑被选中时，高亮它（可选）
    public void OnSlotSelected(string selectedKey)
    {
        foreach (var slot in _activeSlots)
        {
            slot.SetHighlight(slot.blueprintKey == selectedKey);
        }
    }
}