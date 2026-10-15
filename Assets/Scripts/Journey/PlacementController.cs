using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hiking.Journey
{
    public class PlacementController : MonoBehaviour
    {
        GameFlowController flow;
        float remainingSeconds = -1f;
        float restoredRemaining = -1f;
        public PlacementSlot PendingSlot { get; private set; }
        public PlacementSlot EditingSlot { get; private set; }
        public int EditedWater { get; private set; }
        public int EditedSeeds { get; private set; }
        public float RemainingSeconds => PendingSlot == null ? -1f : remainingSeconds;
        public event Action<PlacementRequest> PlacementCommitted;

        public void Initialize(GameFlowController owner) => flow = owner;
        public void InitializeSlots()
        {
            if (flow.Map == null) return;
            foreach (var station in flow.Map.stations)
                foreach (var slot in station.slots)
                    slot.InitializePlan(flow.Config, flow.Map.sourceSettings);
        }

        public bool IsAtSlot(PlacementSlot slot) => flow.Map != null && !flow.IsDebugMode &&
            flow.Session.Phase == JourneyPhase.AtStation && !flow.Traveler.IsMoving && slot != null &&
            slot.station == flow.CurrentStation && Array.IndexOf(flow.CurrentStation.slots, slot) >= 0 &&
            Mathf.Abs(flow.Map.RouteDistanceAtProgress(flow.Traveler.Progress01) -
                slot.station.TileCenter(slot.tileIndex)) < .001f;
        public PlacementSlot CurrentSlot => flow.Map == null ? null :
            Array.Find(flow.CurrentStation.slots, slot => !slot.Committed && IsAtSlot(slot));
        public void RestoreRemaining(float value) => restoredRemaining = value;

        public void OnTravelerArrived()
        {
            if (flow.Activity != JourneyActivity.AwaitingPlacement || CurrentSlot == null) return;
            PendingSlot = CurrentSlot;
            flow.Traveler.SetPlacementIdle(true);
            bool shortage = PendingSlot.WarningAtArrival ||
                flow.Session.Stock("water") < PendingSlot.PlannedWater ||
                flow.Session.Stock("seed") < PendingSlot.PlannedSeeds;
            PendingSlot.SetWarningAtArrival(shortage);
            float duration = Mathf.Max(.1f, flow.Config.placementStopSeconds);
            if (shortage) duration = Mathf.Max(duration, flow.Config.placementWarningSeconds);
            remainingSeconds = restoredRemaining >= 0f ? Mathf.Min(restoredRemaining, duration) : duration;
            restoredRemaining = -1f;
        }

        int AllowedWater(PlacementSlot slot)
        {
            if (slot == null || flow.Material("water") == null) return 0;
            float free = TileState.SurfaceCapacity - slot.station.Tiles[slot.tileIndex].SurfaceWater;
            return Mathf.Max(0, Mathf.Min(flow.Session.Stock("water"), Mathf.FloorToInt(free + .0001f)));
        }

        int AllowedSeeds(PlacementSlot slot)
        {
            if (slot == null || flow.Material("seed") == null ||
                !slot.station.CanReceiveSeeds(slot.tileIndex, 1)) return 0;
            return Mathf.Max(0, Mathf.Min(flow.Session.Stock("seed"),
                slot.station.Tiles[slot.tileIndex].FreePlantCapacity));
        }

        public void AdjustPlan(string materialId, int delta)
        {
            if (EditingSlot == null) return;
            if (materialId == "water") EditedWater = Mathf.Clamp(EditedWater + delta, 0, 3);
            if (materialId == "seed") EditedSeeds = Mathf.Clamp(EditedSeeds + delta, 0, 3);
        }

        public void ConfirmPlan()
        {
            if (EditingSlot == null) return;
            EditingSlot.SetPlan(EditedWater, EditedSeeds);
            CancelEditor();
            flow.SaveNow();
        }

        public void CancelEditor()
        {
            EditingSlot = null;
            flow.UI.HideSignEditor();
        }

        public void OpenEditor(PlacementSlot slot)
        {
            if (slot == null || slot.Committed || slot.Sign == null || flow.Map == null ||
                Array.IndexOf(flow.Map.stations, slot.station) < 0 ||
                Array.IndexOf(slot.station.slots, slot) < 0) return;
            EditingSlot = slot;
            EditedWater = slot.PlannedWater;
            EditedSeeds = slot.PlannedSeeds;
            flow.UI.ShowSignEditor(slot);
        }

        public void ClearSelection()
        {
            PendingSlot = null;
            remainingSeconds = -1f;
            EditingSlot = null;
            if (flow != null && flow.Traveler != null) flow.Traveler.SetPlacementIdle(false);
            if (flow != null && flow.UI != null) flow.UI.HideSignEditor();
        }

        void Update()
        {
            if (flow == null || flow.Map == null || flow.IsDebugMode || flow.IsPreviewing ||
                flow.IsSettlingOffline || flow.Activity == JourneyActivity.Complete) return;
            var mouse = Mouse.current;
            if (mouse != null && Application.isFocused)
            {
                Vector2 position = mouse.position.ReadValue();
                bool onScreen = position.x >= 0 && position.y >= 0 &&
                    position.x < Screen.width && position.y < Screen.height;
                bool clicked = onScreen && mouse.leftButton.wasPressedThisFrame &&
                    !PointerUtility.OverUI(position);
                PlacementSlot clickedSlot = null;
                foreach (var station in flow.Map.stations)
                    foreach (var slot in station.slots)
                    {
                        if (slot.Committed || slot.Sign == null) continue;
                        slot.Sign.Frame(flow.Camera.WorldCamera, slot, false);
                        bool hovered = onScreen && slot.Sign.Contains(position);
                        slot.Sign.Frame(flow.Camera.WorldCamera, slot, hovered);
                        if (clicked && (hovered || slot.WarningAtArrival &&
                            slot.Sign.Contains(position, true))) clickedSlot = slot;
                    }
                if (clickedSlot != null) OpenEditor(clickedSlot);
            }
            else
                foreach (var station in flow.Map.stations)
                    foreach (var slot in station.slots)
                        if (!slot.Committed && slot.Sign != null)
                            slot.Sign.Frame(flow.Camera.WorldCamera, slot, false);

            if (EditingSlot != null && Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame) CancelEditor();
            if (PendingSlot == null || EditingSlot == PendingSlot) return;
            remainingSeconds -= Time.unscaledDeltaTime;
            if (remainingSeconds <= 0f) CommitPending();
        }

        void CommitPending()
        {
            var slot = PendingSlot;
            if (slot == null || slot.Committed || flow.Activity != JourneyActivity.AwaitingPlacement) return;
            int water = Mathf.Min(slot.PlannedWater, AllowedWater(slot));
            int seeds = Mathf.Min(slot.PlannedSeeds, AllowedSeeds(slot));
            if (water > 0) Place(slot, "water", water);
            if (seeds > 0) Place(slot, "seed", seeds);
            slot.MarkCommitted();
            PendingSlot = null;
            remainingSeconds = -1f;
            flow.Traveler.SetPlacementIdle(false);
            flow.CompletePlacementVisit();
        }

        void Place(PlacementSlot slot, string materialId, int quantity)
        {
            var request = new PlacementRequest(flow.Session.StationIndex, slot.slotId, materialId, quantity);
            if (!flow.Session.TryPlace(request))
                throw new InvalidOperationException("投放背包扣除失败：" + materialId);
            slot.ShowPlacement(flow.Material(materialId), quantity, flow.Map.SimulationStep);
            PlacementCommitted?.Invoke(request);
        }
    }
}
