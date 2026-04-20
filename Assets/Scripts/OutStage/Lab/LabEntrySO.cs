using UnityEngine;

[CreateAssetMenu(fileName = "NewLabEntry", menuName = "MineRTS/Lab/Lab Entry")]
public class LabEntrySO : ScriptableObject
{
    [Header("Identity")]
    public string EntryId;

    [Header("Entity Reward")]
    public EntityBlueprintSO EntityBlueprint;

    [Header("Description")]
    [TextArea(3, 6)]
    public string Description;

    [Header("Unlock Costs")]
    [Tooltip("解锁代价占位。经济系统落地后填充。")]
    public ResearchCost[] UnlockCosts = System.Array.Empty<ResearchCost>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EntryId != name)
            EntryId = name;
    }
#endif
}

/// <summary>
/// 研究/解锁代价占位结构。
/// 等经济系统落地后扩展字段。
/// </summary>
[System.Serializable]
public struct ResearchCost
{
    [Tooltip("资源类型 ID。等经济系统落地后统一枚举。")]
    public int ResourceType;

    [Tooltip("所需数量。")]
    public int Amount;
}
