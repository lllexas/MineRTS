using UnityEngine;

[CreateAssetMenu(fileName = "NewEntityBlueprint", menuName = "MineRTS/Entity/Entity Blueprint")]
public class EntityBlueprintSO : ScriptableObject
{
    [Header("Identity")]
    public string BlueprintId;
    public string DisplayName;

    [Header("Visual")]
    public int SpriteId;
    public UnitAtlasAnimationSetSO AnimationSetSO;
    public Vector2Int LogicSize = Vector2Int.one;
    public Vector2 VisualScale = Vector2.one;

    [Header("Core")]
    public int UnitType;
    public int Faction;
    public float MaxHealth = 100f;

    [Header("Combat")]
    public float MoveInterval = 0.5f;
    public float AttackRange = 0f;
    public float AttackDamage = 0f;
    public float AttackCooldown = 1f;

    [Header("Projectile")]
    public float ProjectileSpeed = 10f;
    public int ProjectileSpriteId = -1;

    [Header("Traits")]
    public bool IsFlyer;
    public bool ExplodeOnDeath;
    [Range(0f, 1f)]
    public float FleeHealthPercent = 0f;

    [Header("Industrial")]
    public WorkType WorkType = WorkType.None;
    public float WorkSpeed = 1f;
    public int DrillRange = 0;
    public bool RequiresPower;

    [Header("Spawn")]
    public string SpawnBlueprint;
    public float SpawnInterval = 1f;

    [Header("Power")]
    public bool IsPowerNode;
    public float SupplyRange = 0f;
    public float ConnectionRange = 0f;
    public float EnergyGeneration = 0f;
    public float IdleEnergy = 0f;
    public float WorkEnergy = 0f;
    public float EnergyCapacity = 0f;

    [Header("Inventory")]
    public int InputCount = 0;
    public int OutputCount = 0;
    public int DefaultCapacity = 0;

    [Header("Ports")]
    public BuildingPort[] Ports;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (BlueprintId != name)
            BlueprintId = name;
    }
#endif
}
