using System.Collections.Generic;
using UnityEngine;

namespace Hiking.UIUX
{
    // Scene-only simulated data. Never placed in the deliverable TravelUI prefab.
    public sealed class ResourceAllocationDemo : MonoBehaviour
    {
        public ResourceAllocationBinding Binding;
        public UnityEngine.UI.Text Status;
        private readonly Dictionary<string, AllocationResult> points = new Dictionary<string, AllocationResult>();
        private int seedHeld = 12, waterHeld = 8;
        private void OnEnable() { Binding.Submitted += Save; }
        private void OnDisable() { Binding.Submitted -= Save; }
        public void OpenPointA() => OpenPoint("DemoPointA");
        public void OpenPointB() => OpenPoint("DemoPointB");
        private void OpenPoint(string id)
        {
            bool custom = points.TryGetValue(id, out var value);
            Binding.OpenPoint(id, custom ? value.Seed : Binding.PresetSeed,
                custom ? value.Water : Binding.PresetWater, seedHeld, waterHeld);
        }
        public void ToggleStock()
        {
            seedHeld = seedHeld == 12 ? 0 : 12; waterHeld = seedHeld == 0 ? 3 : 8;
            if (Binding.Panel.IsOpen && Binding.Panel.Mode == AllocationMode.Point)
                Binding.Panel.RefreshLimits(seedHeld, waterHeld);
            Status.text = "模拟库存：种子 " + seedHeld + " / 水分 " + waterHeld;
        }
        private void Save(AllocationResult result)
        {
            if (result.Mode == AllocationMode.Preset)
                Binding.SetPresetSnapshot(result.Seed, result.Water, Binding.PresetSeedMaximum, Binding.PresetWaterMaximum);
            else points[result.PointId] = result;
            Status.text = "模拟保存：" + (result.Mode == AllocationMode.Preset ? "预设" : result.PointId) +
                " · " + result.Seed + " / " + result.Water + "（未扣资源）";
        }
    }
}
