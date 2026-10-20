using UnityEngine;

namespace Hiking.Journey
{
    public class PlacementSlot : MonoBehaviour
    {
        public string slotId;
        public Station station;
        public int tileIndex;
        public int SeedCount { get; private set; }
        public int PlannedWater { get; private set; }
        public int PlannedSeeds { get; private set; }
        public bool HasCustomPlan { get; private set; }
        public bool Committed { get; private set; }
        public bool WarningAtArrival { get; private set; }
        public PlacementSignVisual Sign { get; private set; }

        public void InitializePlan(JourneyConfig config, RingMapSettings settings)
        {
            PlannedWater = Mathf.Clamp(config.defaultPlacementWater, 0, JourneyConfig.PlacementPresetMaximum);
            PlannedSeeds = Mathf.Clamp(config.defaultPlacementSeeds, 0, JourneyConfig.PlacementPresetMaximum);
            Sign = gameObject.AddComponent<PlacementSignVisual>();
            Sign.Configure(settings);
            Sign.SetPlan(this);
        }

        public void SetPlan(int water, int seeds)
        {
            if (Committed) return;
            PlannedWater = Mathf.Clamp(water, 0, 3);
            PlannedSeeds = Mathf.Clamp(seeds, 0, TileState.MaxPlantsPerTile);
            HasCustomPlan = true;
            if (Sign != null) Sign.SetPlan(this);
        }

        public void ApplyPreset(int water, int seeds)
        {
            if (Committed || HasCustomPlan) return;
            PlannedWater = Mathf.Clamp(water, 0, JourneyConfig.PlacementPresetMaximum);
            PlannedSeeds = Mathf.Clamp(seeds, 0, JourneyConfig.PlacementPresetMaximum);
            if (Sign != null) Sign.SetPlan(this);
        }

        public void SetWarningAtArrival(bool value) => WarningAtArrival = value;

        public void MarkCommitted()
        {
            Committed = true;
            WarningAtArrival = false;
            if (Sign != null) Sign.Hide();
        }

        public void RestorePlan(int water, int seeds, bool custom, bool committed,
            bool warningAtArrival = false)
        {
            PlannedWater = Mathf.Clamp(water, 0,
                custom ? 3 : JourneyConfig.PlacementPresetMaximum);
            PlannedSeeds = Mathf.Clamp(seeds, 0, TileState.MaxPlantsPerTile);
            HasCustomPlan = custom;
            Committed = committed;
            WarningAtArrival = warningAtArrival && !committed;
            if (Sign != null) Sign.SetPlan(this);
            if (Sign != null && committed) Sign.Hide();
        }
        public void ShowPlacement(MaterialDefinition material, int quantity, int currentStep)
        {
            if (material.id == "water") station.AddWater(tileIndex, quantity);
            else if (material.id == "seed")
            {
                station.AddSeeds(tileIndex, quantity, currentStep);
                SeedCount += quantity;
            }
        }
    }
}
