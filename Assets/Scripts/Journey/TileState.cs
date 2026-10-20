using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hiking.Journey
{
    public enum WaterDepth { Dry, Medium, Deep }
    [Serializable]
    public sealed class TileState
    {
        public const int MaxPlantsPerTile = 2;
        public StationTheme Category { get; private set; }
        public float SoilContent { get; private set; }
        public const float SurfaceCapacity = 35f;
        public float MaxWater => SoilContent + 10f;
        public int PlantCount { get; private set; }
        public PlantInstance[] NaturalPlants { get; private set; }
        public List<SeedInstance> Seeds { get; } = new List<SeedInstance>();
        public int SeedCount => Seeds.Count;
        public int FreePlantCapacity => Mathf.Max(0, MaxPlantsPerTile - PlantCount - SeedCount);
        public float SoilWater { get; private set; }
        public float SoilHumidity => SoilWater / MaxWater;
        public float BaseEvaporationRate { get; private set; }
        public float EvaporationRate { get; private set; }
        public int Height { get; private set; }
        public int EffectiveHeight => Height + (Depth == WaterDepth.Deep ? 2 : Depth == WaterDepth.Medium ? 1 : 0);
        public float SurfaceWater { get; private set; }
        public WaterDepth Depth => SurfaceWater >= 25 ? WaterDepth.Deep : SurfaceWater > 10 ? WaterDepth.Medium : WaterDepth.Dry;
        public bool IsPuddle => Depth != WaterDepth.Dry;
        public float DrainageLoss { get; private set; }
        public float PendingVapor { get; private set; }
        public float EvaporatedThisStep { get; private set; }
        public float RainfallThisStep { get; private set; }
        public float Temperature { get; private set; }
        [field: SerializeField] public float ClimateTemperature { get; private set; }
        [field: SerializeField] public float ReferenceWater { get; private set; }
        public TileSnapshot LastChange { get; private set; }
        public string CategoryName => (Category == StationTheme.Snow ? "雪地" : Category == StationTheme.Grass ? "平原" : "沙漠") +
            (IsPuddle ? "·水洼" : "");

        public TileState(StationTheme category, float soilContent, int plants, float humidity,
            float evaporationRate, int height, float surfaceWater, float temperature, PlantInstance[] naturalPlants = null)
        {
            Category = category;
            SoilContent = Mathf.Clamp(soilContent, 1, 100);
            NaturalPlants = naturalPlants ?? Array.Empty<PlantInstance>();
            PlantCount = naturalPlants == null ? Mathf.Clamp(plants, 0, MaxPlantsPerTile) : naturalPlants.Length;
            for (int i = 0; i < NaturalPlants.Length; i++)
                NaturalPlants[i].positionOffset = (i - (NaturalPlants.Length - 1) * .5f) * .16f;
            SoilWater = Mathf.Clamp01(humidity) * MaxWater;
            BaseEvaporationRate = Mathf.Max(0, evaporationRate);
            EvaporationRate = BaseEvaporationRate;
            Height = Mathf.Clamp(height, 0, 6);
            SurfaceWater = Mathf.Clamp(surfaceWater, 0, SurfaceCapacity);
            DrainageLoss = Mathf.Max(0, surfaceWater - SurfaceCapacity);
            Temperature = Mathf.Clamp(temperature, 0, 20);
            ClimateTemperature = Temperature; ReferenceWater = SoilWater + SurfaceWater;
        }

        // 读档恢复。构造函数覆盖不了种子队列、待回落水汽和排水损失——它只接受"初始状态"。
        // 这些字段都是私有 set，只能在类内部赋值，所以恢复入口必须放在这里。
        public static TileState Restore(TileData data, PlantCatalog plantCatalog = null)
        {
            if (data == null) return null;

            int plantCount = data.plants == null ? 0 : data.plants.Length;
            var plants = new PlantInstance[plantCount];
            for (int i = 0; i < plantCount; i++)
            {
                var source = data.plants[i];
                plants[i] = new PlantInstance(source.plantId, source.stageIndex)
                {
                    life = source.life,
                    lastLifeChange = source.lastLifeChange,
                    matureSteps = source.matureSteps,
                    nextSeedStep = data.hasExtendedEcologyState ? source.nextSeedStep : 0
                };
            }

            var tile = new TileState((StationTheme)data.category, data.soilContent, plantCount, 0f,
                data.baseEvaporationRate, data.height, data.surfaceWater, data.temperature, plants);

            // positionOffset 必须在构造之后再写：构造函数会按索引重算一遍，
            // 而那是"初始一排植物"的排布；后来萌发出来的植物用的是自由位置，
            // 直接覆盖掉会让它们读档后叠在一起。
            for (int i = 0; i < tile.NaturalPlants.Length && i < data.plants.Length; i++)
                tile.NaturalPlants[i].positionOffset = data.plants[i].positionOffset;
            tile.KeepOneBackgroundPlant(plantCatalog);

            // 覆盖构造函数算出来的近似值，用存档里的真实值
            tile.SoilWater = Mathf.Clamp(data.soilWater, 0, tile.MaxWater);
            if (data.hasExtendedEcologyState)
            {
                tile.ClimateTemperature = Mathf.Clamp(data.climateTemperature, 0, 20);
                tile.ReferenceWater = Mathf.Max(0, data.referenceWater);
            }
            tile.PendingVapor = Mathf.Max(0, data.pendingVapor);
            tile.DrainageLoss = Mathf.Max(0, data.drainageLoss);

            tile.Seeds.Clear();
            if (data.seeds != null)
                foreach (var seed in data.seeds)
                    tile.Seeds.Add(new SeedInstance(seed.plantId, seed.createdStep)
                    {
                        remainingSteps = seed.remainingSteps
                    });

            tile.RefreshEvaporation();
            if (data.hasVisualState)
            {
                tile.EvaporationRate = Mathf.Max(0, data.evaporationRate);
                tile.EvaporatedThisStep = Mathf.Max(0, data.evaporatedThisStep);
                tile.RainfallThisStep = Mathf.Max(0, data.rainfallThisStep);
                tile.LastChange = data.lastChange;
            }
            return tile;
        }

        public TileSnapshot Snapshot() => new TileSnapshot(this);

        public void RefreshEvaporation(float soilFactor = .004f, float plantFactor = .3f,
            float minimumRatio = .15f)
        {
            EvaporationRate = BaseEvaporationRate * Mathf.Max(Mathf.Clamp01(minimumRatio),
                1f - Mathf.Max(0, soilFactor) * SoilContent - Mathf.Max(0, plantFactor) * PlantCount / 5f);
        }

        public bool HasBackgroundOccupant(PlantCatalog catalog)
        {
            if (catalog == null) return false;
            foreach (var plant in NaturalPlants)
                if (catalog.Find(plant.plantId)?.isForeground == false) return true;
            foreach (var seed in Seeds)
                if (catalog.Find(seed.plantId)?.isForeground == false) return true;
            return false;
        }

        public bool HasPlantOrSeed(string plantId)
        {
            foreach (var plant in NaturalPlants)
                if (plant.plantId == plantId) return true;
            foreach (var seed in Seeds)
                if (seed.plantId == plantId) return true;
            return false;
        }

        public bool CanAcceptSeed(PlantDefinition definition, PlantCatalog catalog) =>
            definition != null && FreePlantCapacity > 0 &&
            (definition.isForeground || !HasBackgroundOccupant(catalog));

        public bool TryAddSeed(string plantId, int currentStep, PlantCatalog catalog = null)
        {
            if (string.IsNullOrWhiteSpace(plantId) || FreePlantCapacity <= 0) return false;
            if (catalog != null && !CanAcceptSeed(catalog.Find(plantId), catalog)) return false;
            Seeds.Add(new SeedInstance(plantId, currentStep));
            return true;
        }

        void KeepOneBackgroundPlant(PlantCatalog catalog)
        {
            if (catalog == null || NaturalPlants.Length < 2) return;
            PlantInstance strongest = null;
            int backgroundCount = 0;
            foreach (var plant in NaturalPlants)
                if (catalog.Find(plant.plantId)?.isForeground == false)
                {
                    backgroundCount++;
                    if (strongest == null || plant.life > strongest.life) strongest = plant;
                }
            if (backgroundCount < 2) return;
            NaturalPlants = Array.FindAll(NaturalPlants, plant =>
                plant == strongest || catalog.Find(plant.plantId)?.isForeground != false);
            PlantCount = NaturalPlants.Length;
        }

        float FreePlantPosition()
        {
            float[] positions = { 0, -.16f, .16f, -.32f, .32f };
            foreach (float position in positions)
                if (Array.TrueForAll(NaturalPlants, plant => Mathf.Abs(plant.positionOffset - position) > .03f))
                    return position;
            return 0;
        }

        public void AddWater(float quantity)
        {
            if (quantity <= 0) return;
            // Placement fills surface storage only; overflow runs off this tile.
            float next = SurfaceWater + quantity;
            DrainageLoss += Mathf.Max(0, next - SurfaceCapacity);
            SurfaceWater = Mathf.Min(SurfaceCapacity, next);
        }

        public float TakeSurfaceWater(float quantity)
        {
            float taken = Mathf.Min(SurfaceWater, Mathf.Max(0, quantity));
            SurfaceWater -= taken;
            return taken;
        }

        public void AddRain(float quantity)
        {
            if (quantity <= 0) return;
            RainfallThisStep += quantity;
            AddWater(quantity);
        }

        public float ConsumeVapor(float quantity)
        {
            float consumed = Mathf.Min(PendingVapor, Mathf.Max(0, quantity));
            PendingVapor -= consumed;
            return consumed;
        }

        internal void ApplyNeighborFlow(float soilWater, float temperature)
        {
            Temperature = Mathf.Clamp(temperature, 0, 20);
            SoilWater = Mathf.Clamp(soilWater, 0, MaxWater);
        }

        public void Infiltrate(float factor = .03f)
        {
            float amount = Mathf.Min(SurfaceWater, Mathf.Min(MaxWater - SoilWater, SoilContent * Mathf.Max(0, factor)));
            SurfaceWater -= amount;
            SoilWater += amount;
        }

        internal void ApplySurfaceFlow(float change)
        {
            float next = SurfaceWater + change;
            DrainageLoss += Mathf.Max(0, next - SurfaceCapacity);
            SurfaceWater = Mathf.Clamp(next, 0, SurfaceCapacity);
        }

        // One step is one simulated second. Neighbor exchange and infiltration have already run.
        public void Step(PlantCatalog plantCatalog = null, int currentStep = 0,
            TileSnapshot? beforeNeighborFlow = null, float soilFactor = .004f,
            float plantFactor = .3f, float minimumRatio = .15f, RingMapSettings settings = null)
        {
            var before = beforeNeighborFlow ?? Snapshot();
            RainfallThisStep = 0;
            KeepOneBackgroundPlant(plantCatalog);
            RefreshEvaporation(soilFactor, plantFactor, minimumRatio);
            if (SurfaceWater <= 0) EvaporationRate *= Mathf.Clamp(SoilHumidity / Mathf.Max(.001f, settings != null ? settings.drySoilMoistureScale : .4f), settings != null ? settings.minimumDrySoilMultiplier : .05f, 1);
            float surfaceLoss = Mathf.Min(SurfaceWater, EvaporationRate);
            SurfaceWater -= surfaceLoss;
            float soilLoss = Mathf.Min(SoilWater, Mathf.Max(0, EvaporationRate - surfaceLoss));
            SoilWater -= soilLoss;
            EvaporatedThisStep = surfaceLoss + soilLoss;
            PendingVapor += EvaporatedThisStep;
            UpdateWaterTemperature(settings);
            if (plantCatalog != null && NaturalPlants.Length > 0)
            {
                foreach (var plant in NaturalPlants)
                {
                    var definition = plantCatalog.Find(plant.plantId);
                    int allowedStage = definition == null || !definition.Suits(Category) ? -1 :
                        definition.HighestSuitableStage(SoilContent, SoilHumidity * 100f);
                    int ceiling = allowedStage < 0 ? 0 : definition.LifeCeiling(allowedStage);
                    int previousLife = plant.life;
                    float growthCost = definition != null ? Mathf.Max(0, definition.growthWaterPerLife) : .03f;
                    bool flooded = Depth == WaterDepth.Deep ||
                        Depth == WaterDepth.Medium && (definition == null || !definition.floodTolerant);
                    if (flooded || SoilWater < growthCost || plant.life > ceiling)
                        plant.life = Mathf.Max(0, plant.life - 1);
                    else if (plant.life < ceiling)
                    {
                        SoilWater -= growthCost;
                        plant.life++;
                        while (plant.stageIndex + 1 <= allowedStage &&
                            plant.life >= definition.stages[plant.stageIndex + 1].minLife)
                            plant.stageIndex++;
                    }
                    plant.stageIndex = definition == null ? 0 : definition.StageAtLife(plant.life, definition.StageCount - 1);
                    plant.lastLifeChange = plant.life - previousLife;
                }
                NaturalPlants = Array.FindAll(NaturalPlants, plant => plant.life > 0);
                PlantCount = NaturalPlants.Length;
            }
            if (plantCatalog != null)
            {
                for (int i = Seeds.Count - 1; i >= 0; i--)
                {
                    var seed = Seeds[i];
                    if (seed.createdStep >= currentStep) continue;
                    var definition = plantCatalog.Find(seed.plantId);
                    bool toleratesWater = Depth == WaterDepth.Dry ||
                        Depth == WaterDepth.Medium && definition != null && definition.floodTolerant;
                    bool canGerminate = definition != null && definition.Suits(Category) && toleratesWater &&
                        definition.HighestSuitableStage(SoilContent, SoilHumidity * 100f) >= 0 &&
                        SoilWater >= .1f;
                    bool backgroundOccupied = definition != null && !definition.isForeground &&
                        Array.Exists(NaturalPlants, plant =>
                            plantCatalog.Find(plant.plantId)?.isForeground == false);
                    if (canGerminate && PlantCount < MaxPlantsPerTile && !backgroundOccupied)
                    {
                        SoilWater = Mathf.Max(0, SoilWater - .1f);
                        var plant = new PlantInstance(seed.plantId) { positionOffset = FreePlantPosition() };
                        var grown = new PlantInstance[NaturalPlants.Length + 1];
                        Array.Copy(NaturalPlants, grown, NaturalPlants.Length);
                        grown[grown.Length - 1] = plant;
                        NaturalPlants = grown;
                        PlantCount++;
                        Seeds.RemoveAt(i);
                    }
                    else if (--seed.remainingSteps <= 0) Seeds.RemoveAt(i);
                }
            }
            LastChange = Snapshot() - before;
        }

        void UpdateWaterTemperature(RingMapSettings settings)
        {
            float neutral = settings != null ? settings.waterTargetTemperature : 10;
            float extra = Mathf.Max(0, SoilWater + SurfaceWater - ReferenceWater - (settings != null ? settings.extraWaterDeadband : 1));
            float influence = Mathf.Clamp01(extra / Mathf.Max(.001f, settings != null ? settings.fullInfluenceWater : 8));
            float target = ClimateTemperature + (neutral - ClimateTemperature) * influence;
            bool approaching = Mathf.Abs(target-neutral) < Mathf.Abs(Temperature-neutral);
            float rate = settings != null ? (approaching ? settings.thermalApproachRate : settings.thermalRecoveryRate) : (approaching ? .06f : .002f);
            float cap = settings != null ? (approaching ? settings.thermalApproachCap : settings.thermalRecoveryCap) : (approaching ? .15f : .01f);
            Temperature = Mathf.Clamp(Temperature + Mathf.Clamp((target-Temperature)*rate, -cap, cap), 0, 20);
            Category = Temperature < 5 ? StationTheme.Snow : Temperature < 12 ? StationTheme.Grass : StationTheme.Desert;
        }

        public void RecordStepChange(TileSnapshot before) => LastChange = Snapshot() - before;

    }

    public static class TileNeighborFlow
    {
        // All edge transfers use the same pre-step values, including station boundaries.
        public static void Exchange(TileState[] tiles, float temperatureRate, float soilWaterRate)
        {
            int count = tiles.Length;
            var water = new float[count];
            var temperature = new float[count];
            var soilMax = new float[count];
            var temperatureMax = new float[count];
            for (int i = 0; i < count; i++)
            {
                water[i] = tiles[i].SoilWater;
                temperature[i] = tiles[i].Temperature;
                soilMax[i] = tiles[i].MaxWater;
                temperatureMax[i] = 20;
            }
            // Direction follows relative humidity; the transferred quantity remains water units.
            var waterChange = Changes(water, soilMax, soilWaterRate,
                i => water[i] / soilMax[i], 1f, null, true);
            var temperatureChange = Changes(temperature, temperatureMax, temperatureRate, i => temperature[i]);
            for (int i = 0; i < count; i++)
                tiles[i].ApplyNeighborFlow(water[i] + waterChange[i],
                    temperature[i] + temperatureChange[i]);
        }

        public static void ExchangeSurface(TileState[] tiles, float rate, float waterUnitsPerHeight)
        {
            int count = tiles.Length;
            var water = new float[count]; var capacity = new float[count];
            float unit = Mathf.Max(.01f, waterUnitsPerHeight);
            for (int i = 0; i < count; i++)
            {
                water[i] = tiles[i].SurfaceWater;
                capacity[i] = TileState.SurfaceCapacity;
            }
            var change = Changes(water, capacity, rate,
                i => tiles[i].Height + water[i] / unit,  unit,
                i => tiles[i].SoilWater >= tiles[i].MaxWater - .0001f);
            for (int i = 0; i < count; i++) tiles[i].ApplySurfaceFlow(change[i]);
        }

        static float[] Changes(float[] values, float[] maximums, float rate,
            Func<int, float> level, float levelToAmount = 1f, Func<int, bool> maySend = null,
            bool scaleByCapacity = false)
        {
            int count = values.Length;
            var edges = new float[Mathf.Max(0, count - 1)];
            var outgoing = new float[count];
            var incoming = new float[count];
            for (int i = 0; i < edges.Length; i++)
            {
                float difference = level(i) - level(i + 1);
                int source = difference > 0 ? i : i + 1;
                if (difference == 0 || maySend != null && !maySend(source)) continue;
                // One third of a small difference prevents two adjacent edges from overshooting.
                float scale = scaleByCapacity ? Mathf.Min(maximums[i], maximums[i + 1]) : levelToAmount;
                float amount = Mathf.Min(Mathf.Max(0, rate), Mathf.Abs(difference) * scale / 3f);
                edges[i] = difference > 0 ? amount : -amount;
                if (edges[i] > 0) { outgoing[i] += amount; incoming[i + 1] += amount; }
                else { outgoing[i + 1] += amount; incoming[i] += amount; }
            }
            var outgoingScale = new float[count];
            var incomingScale = new float[count];
            for (int i = 0; i < count; i++)
            {
                outgoingScale[i] = outgoing[i] > 0 ? Mathf.Min(1, Mathf.Max(0, values[i]) / outgoing[i]) : 1;
                incomingScale[i] = incoming[i] > 0 ?
                    Mathf.Min(1, Mathf.Max(0, maximums[i] - values[i]) / incoming[i]) : 1;
            }
            var change = new float[count];
            for (int i = 0; i < edges.Length; i++)
            {
                float amount = edges[i] > 0 ? edges[i] * Mathf.Min(outgoingScale[i], incomingScale[i + 1]) :
                    edges[i] * Mathf.Min(outgoingScale[i + 1], incomingScale[i]);
                change[i] -= amount;
                change[i + 1] += amount;
            }
            return change;
        }
    }

    [Serializable]
    public struct TileSnapshot
    {
        public float SoilContent, MaxWater, SoilWater, SoilHumidity, EvaporationRate, SurfaceWater, Temperature, DrainageLoss, PendingVapor;
        public int PlantCount, SeedCount, Height, EffectiveHeight;
        public TileSnapshot(TileState tile)
        {
            SoilContent = tile.SoilContent; MaxWater = tile.MaxWater; PlantCount = tile.PlantCount; SeedCount = tile.SeedCount;
            SoilWater = tile.SoilWater; SoilHumidity = tile.SoilHumidity; EvaporationRate = tile.EvaporationRate;
            Height = tile.Height; EffectiveHeight = tile.EffectiveHeight;
            SurfaceWater = tile.SurfaceWater; Temperature = tile.Temperature; DrainageLoss = tile.DrainageLoss;
            PendingVapor = tile.PendingVapor;
        }
        public static TileSnapshot operator -(TileSnapshot a, TileSnapshot b) => new TileSnapshot
        {
            SoilContent = a.SoilContent - b.SoilContent, MaxWater = a.MaxWater - b.MaxWater,
            PlantCount = a.PlantCount - b.PlantCount, SeedCount = a.SeedCount - b.SeedCount,
            SoilWater = a.SoilWater - b.SoilWater, SoilHumidity = a.SoilHumidity - b.SoilHumidity,
            EvaporationRate = a.EvaporationRate - b.EvaporationRate, Height = a.Height - b.Height,
            EffectiveHeight = a.EffectiveHeight - b.EffectiveHeight,
            SurfaceWater = a.SurfaceWater - b.SurfaceWater, Temperature = a.Temperature - b.Temperature,
            DrainageLoss = a.DrainageLoss - b.DrainageLoss, PendingVapor = a.PendingVapor - b.PendingVapor
        };
    }
}
