using System;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Hiking.Journey
{
    public class PlacementController : MonoBehaviour
    {
        public string SelectedMaterialId { get; private set; }
        public int SelectedQuantity { get; private set; } = 1;
        public PlacementSlot PendingSlot { get; private set; }
        public event Action<PlacementRequest> PlacementCommitted;
        GameFlowController flow;
        public void Initialize(GameFlowController owner) => flow = owner;
        public bool IsAtSlot(PlacementSlot slot) => flow.Map != null && !flow.IsDebugMode && !flow.IsPreviewing &&
            flow.Session.Phase == JourneyPhase.AtStation && !flow.Traveler.IsMoving && slot != null &&
            slot.station == flow.CurrentStation && Array.IndexOf(flow.CurrentStation.slots, slot) >= 0 &&
            Mathf.Abs(flow.Map.RouteDistanceAtProgress(flow.Traveler.Progress01) - slot.station.TileCenter(slot.tileIndex)) < .001f;
        public PlacementSlot CurrentSlot => flow.Map == null ? null : Array.Find(flow.CurrentStation.slots, IsAtSlot);
        public void OnTravelerArrived()
        {
            if (flow.Activity != JourneyActivity.AwaitingPlacement || CurrentSlot == null) return;
            ClearSelection(); PendingSlot = CurrentSlot; flow.UI.ShowPlacementDecision();
        }
        public void ChooseToPlace()
        {
            if (!flow.UI.PlacementDecisionOpen || !IsAtSlot(PendingSlot)) return;
            flow.UI.ShowPlacementPrompt();
            Select(flow.Session.Stock("seed") > 0 && PendingSlot.station.CanReceiveSeeds(PendingSlot.tileIndex, 1) ? "seed" : "water");
        }
        public void SkipPlacement()
        {
            if (flow.Activity != JourneyActivity.AwaitingPlacement || !IsAtSlot(PendingSlot)) return;
            flow.CompletePlacementVisit();
        }
        public void Select(string id)
        {
            if (!flow.UI.PlacementPromptOpen || !IsAtSlot(PendingSlot) || (id != "water" && id != "seed")) return;
            SelectedMaterialId = id; SetQuantity(SelectedQuantity); RefreshHighlights();
        }
        public void SetQuantity(int value) => SelectedQuantity = Mathf.Clamp(value, 1, Mathf.Max(1, flow.Session.Stock(SelectedMaterialId)));
        public void AdjustQuantity(int delta) => SetQuantity(SelectedQuantity + delta);
        public void ClearSelection() { PendingSlot = null; SelectedMaterialId = null; SelectedQuantity = 1; RefreshHighlights(); }
        public void RefreshHighlights()
        {
            if (flow == null || flow.Map == null) return;
            var material = flow.Material(SelectedMaterialId);
            foreach (var station in flow.Map.stations)
                foreach (var slot in station.slots)
                    slot.Highlight(slot == PendingSlot && IsAtSlot(slot) && material != null, material != null ? material.color : Color.white);
        }
        void Update()
        {
            if (flow == null || !flow.UI.IsModalOpen) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame ||
                Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (flow.UI.PlacementPromptOpen) flow.UI.ShowPlacementDecision(); else SkipPlacement();
            }
        }
        public bool CanConfirm => flow.Activity == JourneyActivity.AwaitingPlacement && flow.UI.PlacementPromptOpen && IsAtSlot(PendingSlot) &&
            (SelectedMaterialId == "water" || SelectedMaterialId == "seed") && flow.Material(SelectedMaterialId) != null &&
            SelectedQuantity > 0 && flow.Session.Stock(SelectedMaterialId) >= SelectedQuantity &&
            (SelectedMaterialId != "seed" || PendingSlot.station.CanReceiveSeeds(PendingSlot.tileIndex, SelectedQuantity)) &&
            (SelectedMaterialId != "water" || SelectedQuantity <= Mathf.CeilToInt(TileState.SurfaceCapacity - PendingSlot.station.Tiles[PendingSlot.tileIndex].SurfaceWater));
        public void ConfirmPlacement() => ApplyPlacement(PendingSlot);
        public bool ApplyPlacement(PlacementSlot slot)
        {
            if (!CanConfirm || slot != PendingSlot) return false;
            var material = flow.Material(SelectedMaterialId);
            var request = new PlacementRequest(flow.Session.StationIndex, slot.slotId, material.id, SelectedQuantity);
            if (!flow.Session.TryPlace(request)) return false;
            slot.ShowPlacement(material, request.Quantity, flow.Map.SimulationStep);
            flow.CompletePlacementVisit();
            PlacementCommitted?.Invoke(request);
            flow.UI.Notify("已投放：" + material.displayName + " × " + request.Quantity);
            return true;
        }
    }
}
