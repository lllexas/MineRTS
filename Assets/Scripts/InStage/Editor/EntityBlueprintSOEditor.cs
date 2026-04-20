using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EntityBlueprintSO))]
public class EntityBlueprintSOEditor : Editor
{
    private static readonly string[] FactionNames =
    {
        "0 - 协议军",
        "1 - 日之城",
        "2 - 盖亚黎明",
    };

    private static readonly int[] FactionValues = { 0, 1, 2 };

    private static readonly string[] UnitTypeNames =
    {
        "Hero",
        "Minion",
        "Building",
        "ResourceItem",
        "Projectile",
        "Flyer",
    };

    private static readonly int[] UnitTypeValues =
    {
        UnitType.Hero,
        UnitType.Minion,
        UnitType.Building,
        UnitType.ResourceItem,
        UnitType.Projectile,
        UnitType.Flyer,
    };

    private bool _showIdentity = true;
    private bool _showVisual = true;
    private bool _showCore = true;
    private bool _showCombat = true;
    private bool _showProjectile = true;
    private bool _showTraits = true;
    private bool _showIndustrial = true;
    private bool _showSpawn = true;
    private bool _showPower = true;
    private bool _showInventory = true;
    private bool _showPorts = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIdentitySection();
        DrawVisualSection();
        DrawCoreSection();
        DrawFoldout(ref _showCombat, "Combat", "MoveInterval", "AttackRange", "AttackDamage", "AttackCooldown");
        DrawProjectileSection();
        DrawFoldout(ref _showTraits, "Traits", "IsFlyer", "ExplodeOnDeath", "FleeHealthPercent");
        DrawFoldout(ref _showIndustrial, "Industrial", "WorkType", "WorkSpeed", "DrillRange", "RequiresPower");
        DrawFoldout(ref _showSpawn, "Spawn", "SpawnBlueprint", "SpawnInterval");
        DrawFoldout(ref _showPower, "Power", "IsPowerNode", "SupplyRange", "ConnectionRange", "EnergyGeneration", "IdleEnergy", "WorkEnergy", "EnergyCapacity");
        DrawFoldout(ref _showInventory, "Inventory", "InputCount", "OutputCount", "DefaultCapacity");
        DrawFoldout(ref _showPorts, "Ports", "Ports");

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawIdentitySection()
    {
        _showIdentity = EditorGUILayout.BeginFoldoutHeaderGroup(_showIdentity, "Identity");
        if (_showIdentity)
        {
            var blueprintIdProperty = serializedObject.FindProperty("BlueprintId");
            using (new EditorGUI.DisabledScope(true))
            {
                if (blueprintIdProperty != null)
                    EditorGUILayout.TextField("Blueprint Id", blueprintIdProperty.stringValue);
                else
                    EditorGUILayout.TextField("Blueprint Id", "<missing>");
            }

            EditorGUILayout.HelpBox("BlueprintId 与资产名保持一致，不在 Inspector 中手动编辑。", MessageType.None);
            DrawProperty("DisplayName");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    private void DrawVisualSection()
    {
        _showVisual = EditorGUILayout.BeginFoldoutHeaderGroup(_showVisual, "Visual");
        if (_showVisual)
        {
            DrawProperty("SpriteId");
            var spriteIdProperty = serializedObject.FindProperty("SpriteId");
            if (spriteIdProperty != null)
                DrawSpritePreview(spriteIdProperty.intValue, "Main Sprite");
            DrawProperty("LogicSize");
            DrawProperty("VisualScale");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    private void DrawCoreSection()
    {
        _showCore = EditorGUILayout.BeginFoldoutHeaderGroup(_showCore, "Core");
        if (_showCore)
        {
            DrawUnitTypeMask();
            DrawFactionPopup();
            DrawProperty("MaxHealth");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    private void DrawProjectileSection()
    {
        _showProjectile = EditorGUILayout.BeginFoldoutHeaderGroup(_showProjectile, "Projectile");
        if (_showProjectile)
        {
            DrawProperty("ProjectileSpeed");
            DrawProperty("ProjectileSpriteId");
            var projectileSpriteIdProperty = serializedObject.FindProperty("ProjectileSpriteId");
            if (projectileSpriteIdProperty != null)
                DrawSpritePreview(projectileSpriteIdProperty.intValue, "Projectile Sprite");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    private void DrawFoldout(ref bool foldout, string title, params string[] propertyNames)
    {
        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, title);
        if (foldout)
        {
            foreach (var propertyName in propertyNames)
            {
                DrawProperty(propertyName);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    private void DrawUnitTypeMask()
    {
        var property = serializedObject.FindProperty("UnitType");
        if (property == null)
        {
            EditorGUILayout.HelpBox("字段不存在或不可序列化：UnitType", MessageType.Warning);
            return;
        }

        int currentMask = property.intValue;
        int newMask = EditorGUILayout.MaskField(new GUIContent("Unit Type"), currentMask, UnitTypeNames);
        if (newMask != currentMask)
            property.intValue = newMask;
    }

    private void DrawFactionPopup()
    {
        var property = serializedObject.FindProperty("Faction");
        if (property == null)
        {
            EditorGUILayout.HelpBox("字段不存在或不可序列化：Faction", MessageType.Warning);
            return;
        }

        int currentValue = property.intValue;
        int currentIndex = System.Array.IndexOf(FactionValues, currentValue);
        if (currentIndex < 0)
            currentIndex = 0;

        int newIndex = EditorGUILayout.Popup("Faction", currentIndex, FactionNames);
        property.intValue = FactionValues[newIndex];
    }

    private void DrawProperty(string propertyName)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"字段不存在或不可序列化：{propertyName}", MessageType.Warning);
            return;
        }

        EditorGUILayout.PropertyField(property, true);
    }

    private static void DrawSpritePreview(int spriteId, string label)
    {
        if (spriteId < 0)
        {
            EditorGUILayout.HelpBox($"{label}: <none>", MessageType.None);
            return;
        }

        var spriteLib = Object.FindFirstObjectByType<SpriteLib>();
        if (spriteLib == null)
        {
            spriteLib = Resources.FindObjectsOfTypeAll<SpriteLib>().Length > 0
                ? Resources.FindObjectsOfTypeAll<SpriteLib>()[0]
                : null;
        }

        if (spriteLib == null)
        {
            EditorGUILayout.HelpBox($"{label}: 无法预览，当前未找到 SpriteLib。", MessageType.Info);
            return;
        }

        if (spriteId >= spriteLib.unitSprites.Count)
        {
            EditorGUILayout.HelpBox($"{label}: SpriteId {spriteId} 超出 SpriteLib 范围（Count={spriteLib.unitSprites.Count}）。", MessageType.Warning);
            return;
        }

        var sprite = spriteLib.unitSprites[spriteId];
        if (sprite == null)
        {
            EditorGUILayout.HelpBox($"{label}: SpriteId {spriteId} 对应为空。", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"{label}: [{spriteId}] {sprite.name}", EditorStyles.boldLabel);
        var rect = GUILayoutUtility.GetRect(96f, 96f, GUILayout.ExpandWidth(false));
        EditorGUI.DrawPreviewTexture(rect, AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite));
        EditorGUILayout.EndVertical();
    }
}
