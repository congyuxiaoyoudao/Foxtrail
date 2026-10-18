using System;
using UnityEngine;

namespace Hiking.UIUX
{
    // Input snapshots supplied by the future game-state owner; no save file or inventory access.
    public sealed class ResourceAllocationBinding : MonoBehaviour
    {
        public ResourceAllocationPanel Panel;
        public int PresetSeed = 1;
        public int PresetWater = 1;
        public int PresetSeedMaximum = 3;
        public int PresetWaterMaximum = 3;
        public event Action<AllocationResult> Submitted;
        private void Awake() { Panel.Confirmed += Forward; }
        private void OnDestroy() { if (Panel) Panel.Confirmed -= Forward; }
        private void Forward(AllocationResult result) { Submitted?.Invoke(result); }
        public void OpenPreset()
        {
            Panel.Open(AllocationMode.Preset, null, PresetSeed, PresetWater,
                PresetSeedMaximum, PresetWaterMaximum);
        }
        public void SetPresetSnapshot(int seed, int water, int seedMaximum, int waterMaximum)
        {
            PresetSeed = seed; PresetWater = water;
            PresetSeedMaximum = seedMaximum; PresetWaterMaximum = waterMaximum;
            if (Panel.IsOpen && Panel.Mode == AllocationMode.Preset)
                Panel.RefreshLimits(seedMaximum, waterMaximum);
        }
        public void OpenPoint(string pointId, int seed, int water, int seedHeld, int waterHeld)
        { Panel.Open(AllocationMode.Point, pointId, seed, water, seedHeld, waterHeld); }
    }
}
