using System.Collections.Generic;
using MineRTS.BigMap.UI.Panels;
using NekoGraph;
using UnityEngine;
using UnityEngine.EventSystems;
using SpaceTUI;

public static class LabEntryViewerEvents
{
    public const string Refresh = "LabEntryViewer.Refresh";
    public const string PanelID = "LabEntryViewerPanel";
}

namespace MineRTS.BigMap.UI.Panels
{
    public class LabEntryViewerPanel : SpaceUIAnimator
    {
        [Header("Lab Entry Viewer")]
        [SerializeField] private LabGUI labGUI;
        private VFSLabEntryQueryPayload _currentPayload;

        protected override string UIID => LabEntryViewerEvents.PanelID;

        protected override void Awake()
        {
            base.Awake();
            if (labGUI == null)
            {
                labGUI = GetComponent<LabGUI>();
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

        [Subscribe(LabEntryViewerEvents.Refresh)]
        private void HandleRefresh(object data)
        {
            if (labGUI == null || data is not VFSLabEntryQueryPayload payload || payload.Entry == null)
            {
                return;
            }

            _currentPayload = payload;
            labGUI.Render(BuildDisplayData(payload));
        }

        [Subscribe(LabFacade.LabChangedEvent)]
        private void HandleLabChanged(object data)
        {
            if (_currentPayload?.Entry == null || labGUI == null)
                return;

            labGUI.Render(BuildDisplayData(_currentPayload));
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

        private static LabGUI.DisplayData BuildDisplayData(VFSLabEntryQueryPayload payload)
        {
            var entry = payload.Entry;
            var blueprint = entry.EntityBlueprint;
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                lines.Add(entry.Description);
                lines.Add(string.Empty);
            }

            lines.Add($"Entry ID : {entry.EntryId}");
            if (payload.Node != null)
            {
                lines.Add($"Node     : {payload.Node.NodeID}");
            }
            lines.Add(string.Empty);

            if (blueprint != null)
            {
                lines.Add($"Entity   : {blueprint.DisplayName ?? blueprint.BlueprintId}");
                lines.Add($"Faction  : {FormatFaction(blueprint.Faction)}");
                lines.Add($"Type     : {BuildUnitTypeLine(blueprint.UnitType)}");
                lines.Add($"HP       : {blueprint.MaxHealth:0}");
                lines.Add($"Size     : {blueprint.LogicSize.x}x{blueprint.LogicSize.y}");
                lines.Add(string.Empty);
            }
            else
            {
                lines.Add("Entity   : (none)");
                lines.Add(string.Empty);
            }

            if (entry.UnlockCosts != null && entry.UnlockCosts.Length > 0)
            {
                lines.Add("Unlock Costs:");
                foreach (var cost in entry.UnlockCosts)
                {
                    lines.Add($"  - Resource {cost.ResourceType}: {cost.Amount}");
                }
            }
            else
            {
                lines.Add("Unlock Costs: Free");
            }

            return new LabGUI.DisplayData
            {
                Title = $"LAB ENTRY / {entry.EntryId}",
                Lines = lines.ToArray(),
                Footer = BuildFooter(payload),
                PrimaryActionText = "解锁",
                PrimaryActionVisible = true,
                PrimaryActionInteractable = !IsUnlocked(payload),
                PrimaryAction = payload.UnlockAction == null
                    ? null
                    : () =>
                    {
                        if (payload.UnlockAction.Invoke())
                        {
                            PostSystem.Instance?.Send(LabFacade.LabChangedEvent, payload.Node?.NodeID);
                        }
                    }
            };
        }

        private static string BuildFooter(VFSLabEntryQueryPayload payload)
        {
            var facade = GraphHub.Instance?.GetFacade<LabFacade>();
            if (facade == null || payload.Node == null)
            {
                return "无法读取解锁状态";
            }

            return facade.IsUnlocked(payload.Node)
                ? "[已解锁] 该实体已加入仓库"
                : "点击【解锁】将实体投递到仓库";
        }

        private static bool IsUnlocked(VFSLabEntryQueryPayload payload)
        {
            var facade = GraphHub.Instance?.GetFacade<LabFacade>();
            return facade != null && payload?.Node != null && facade.IsUnlocked(payload.Node);
        }

        private static string FormatFaction(int faction)
        {
            return faction switch
            {
                0 => "Protocol",
                1 => "SunCity",
                2 => "Gaia",
                _ => $"F:{faction}"
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
