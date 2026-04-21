using System.Collections.Generic;
using SpaceTUI;
using UnityEngine;
using UnityEngine.EventSystems;

public static class EntityViewerEvents
{
    public const string Refresh = "EntityViewer.Refresh";
    public const string PanelID = "EntityViewerPanel";
}

namespace MineRTS.BigMap.UI.Panels
{
    public class EntityViewerPanel : SpaceUIAnimator
    {
        [Header("Entity Viewer")]
        [SerializeField] private EntityGUI entityGUI;

        protected override string UIID => EntityViewerEvents.PanelID;

        protected override void Awake()
        {
            base.Awake();
            if (entityGUI == null)
            {
                entityGUI = GetComponent<EntityGUI>();
            }
        }

        private void Start()
        {
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标滑入 += OnMouseEnterHandler;
            鼠标滑出 += OnMouseExitHandler;
            鼠标点击 += OnMouseClickHandler;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            期望显示面板 -= OnShowPanel;
            期望隐藏面板 -= OnHidePanel;
            鼠标滑入 -= OnMouseEnterHandler;
            鼠标滑出 -= OnMouseExitHandler;
            鼠标点击 -= OnMouseClickHandler;
        }

        protected override void CloseAction()
        {
            FadeOut();
        }

        [Subscribe(EntityViewerEvents.Refresh)]
        private void HandleRefresh(object data)
        {
            if (entityGUI == null || data is not VFSEntityQueryPayload payload || payload.Blueprint == null)
                return;

            entityGUI.Render(BuildDisplayData(payload));
        }

        private void OnShowPanel(object data)
        {
            FadeIn();
            StartBreathing();
        }

        private void OnHidePanel(object data)
        {
            StopBreathing();
            FadeOut();
        }

        private void OnMouseEnterHandler(PointerEventData eventData)
        {
            SetTargetScale(new Vector3(1.01f, 1.01f, 1.01f));
            PlayScaleAnimation();
        }

        private void OnMouseExitHandler(PointerEventData eventData)
        {
            ResetScale();
            ResetRotation();
        }

        private void OnMouseClickHandler(PointerEventData eventData)
        {
        }

        private static EntityGUI.DisplayData BuildDisplayData(VFSEntityQueryPayload payload)
        {
            var blueprint = payload.Blueprint;
            var lines = new List<string>();

            lines.Add($"Blueprint : {blueprint.BlueprintId}");
            if (payload.Node != null)
            {
                lines.Add($"Node      : {payload.Node.NodeID}");
            }
            if (!string.IsNullOrWhiteSpace(payload.PackID))
            {
                lines.Add($"Pack      : {payload.PackID}");
            }
            if (!string.IsNullOrWhiteSpace(payload.VfsPath))
            {
                lines.Add($"Path      : {payload.VfsPath}");
            }
            lines.Add(string.Empty);

            lines.Add($"Faction   : {FormatFaction(blueprint.Faction)}");
            lines.Add($"Type      : {BuildUnitTypeLine(blueprint.UnitType)}");
            lines.Add($"HP        : {blueprint.MaxHealth:0}");
            lines.Add($"Size      : {blueprint.LogicSize.x}x{blueprint.LogicSize.y}");
            lines.Add($"Move      : {blueprint.MoveInterval:0.##}");
            lines.Add(string.Empty);

            lines.Add($"Attack    : {blueprint.AttackDamage:0.##}");
            lines.Add($"Range     : {blueprint.AttackRange:0.##}");
            lines.Add($"Cooldown  : {blueprint.AttackCooldown:0.##}");
            lines.Add(string.Empty);

            lines.Add($"WorkType  : {blueprint.WorkType}");
            lines.Add($"WorkSpeed : {blueprint.WorkSpeed:0.##}");
            lines.Add($"PowerNeed : {(blueprint.RequiresPower ? "Yes" : "No")}");

            return new EntityGUI.DisplayData
            {
                Title = $"ENTITY / {blueprint.DisplayName ?? blueprint.BlueprintId}",
                Lines = lines.ToArray(),
                Footer = BuildFooter(blueprint)
            };
        }

        private static string BuildFooter(EntityBlueprintSO blueprint)
        {
            return $"Sprite:{blueprint.SpriteId}  Projectile:{blueprint.ProjectileSpriteId}";
        }

        private static string FormatFaction(int faction)
        {
            return faction switch
            {
                0 => "协议军",
                1 => "日之城",
                2 => "盖亚黎明",
                _ => $"Faction:{faction}"
            };
        }

        private static string BuildUnitTypeLine(int unitTypeMask)
        {
            if (unitTypeMask == UnitType.None)
                return "None";

            var parts = new List<string>();
            if ((unitTypeMask & UnitType.Hero) != 0) parts.Add("Hero");
            if ((unitTypeMask & UnitType.Minion) != 0) parts.Add("Minion");
            if ((unitTypeMask & UnitType.Building) != 0) parts.Add("Building");
            if ((unitTypeMask & UnitType.ResourceItem) != 0) parts.Add("Resource");
            if ((unitTypeMask & UnitType.Projectile) != 0) parts.Add("Projectile");
            if ((unitTypeMask & UnitType.Flyer) != 0) parts.Add("Flyer");
            return string.Join("|", parts);
        }
    }
}
