using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hiking.Journey
{
    [Flags]
    public enum PlantHabitat { None = 0, Snow = 1, Grass = 2, Desert = 4 }

    [Serializable]
    public sealed class PlantStage
    {
        [Range(0, 100)] public int minHumidityPercent;
        [Range(0, 100)] public int minSoilPercent;
        [Min(0)] public int minLife;
        public Sprite sprite;
        public Color fallbackColor = new Color(.35f, .75f, .35f);
    }

    [Serializable]
    public sealed class PlantInstance
    {
        public string plantId;
        public int stageIndex;
        public int life = 1;
        public int lastLifeChange;
        public int matureSteps;
        public int nextSeedStep;
        public float positionOffset;
        public PlantInstance(string plantId, int stageIndex = 0)
        {
            this.plantId = plantId;
            this.stageIndex = stageIndex;
        }
    }

    [Serializable]
    public sealed class SeedInstance
    {
        public string plantId;
        public int remainingSteps = 30;
        public int createdStep;
        public SeedInstance(string plantId, int createdStep)
        {
            this.plantId = plantId;
            this.createdStep = createdStep;
        }
    }

    public struct PlantRange
    {
        public int Minimum { get; }
        public int Maximum { get; }
        public bool IncludesMaximum { get; }
        public PlantRange(int minimum, int maximum, bool includesMaximum)
        {
            Minimum = minimum; Maximum = maximum; IncludesMaximum = includesMaximum;
        }
        public bool Contains(float value) => value >= Minimum &&
            (value < Maximum || IncludesMaximum && value <= Maximum);
    }

    [Serializable]
    public sealed class PlantDefinition
    {
        public string id = "flower";
        public string displayName = "小花";
        public PlantHabitat suitableHabitats = PlantHabitat.Grass;
        public bool enabledForGeneration = true;
        public PlantHabitat initialHabitats = PlantHabitat.Grass;
        [Min(0)] public float growthWaterPerLife = .03f;
        public bool isForeground = true;
        [Tooltip("该植物成熟阶段的画面高度；所有阶段按生命阶段比例缩放")]
        [Min(.05f)] public float visualHeight = .35f;
        [Tooltip("中等积水时仍可正常生长；深水会伤害所有陆地植物")]
        public bool floodTolerant;
        [Range(0, 100)] public int maxHumidityPercent = 100;
        [Range(0, 100)] public int maxSoilPercent = 100;
        [Min(0)] public int maxLife = 90;
        public PlantStage[] stages =
        {
            new PlantStage(),
            new PlantStage { minHumidityPercent = 30, minSoilPercent = 30, minLife = 30 },
            new PlantStage { minHumidityPercent = 60, minSoilPercent = 60, minLife = 60 }
        };

        public int StageCount => stages == null ? 0 : stages.Length;
        public bool Suits(StationTheme theme) =>
            (suitableHabitats & (theme == StationTheme.Snow ? PlantHabitat.Snow :
                theme == StationTheme.Grass ? PlantHabitat.Grass : PlantHabitat.Desert)) != 0;

        public PlantRange HumidityRange(int stage) => Range(stage, s => s.minHumidityPercent, maxHumidityPercent);
        public PlantRange SoilRange(int stage) => Range(stage, s => s.minSoilPercent, maxSoilPercent);
        public PlantRange LifeRange(int stage) => Range(stage, s => s.minLife, maxLife);
        public int HighestSuitableStage(float soilContent, float humidityPercent)
        {
            int soilStage = -1, humidityStage = -1;
            for (int i = 0; i < StageCount; i++)
            {
                if (SoilRange(i).Contains(soilContent)) soilStage = i;
                if (HumidityRange(i).Contains(humidityPercent)) humidityStage = i;
            }
            return Mathf.Min(soilStage, humidityStage);
        }
        public int LifeCeiling(int stage) => stage + 1 < StageCount ? stages[stage + 1].minLife : maxLife;
        public int StageAtLife(int life, int highestStage)
        {
            int stage = 0;
            for (int i = 1; i <= Mathf.Min(highestStage, StageCount - 1); i++)
                if (life > stages[i].minLife) stage = i;
            return stage;
        }
        PlantRange Range(int stage, Func<PlantStage, int> minimum, int maximum)
        {
            if (stages == null || stage < 0 || stage >= stages.Length)
                throw new ArgumentOutOfRangeException(nameof(stage));
            bool last = stage == stages.Length - 1;
            return new PlantRange(minimum(stages[stage]), last ? maximum : minimum(stages[stage + 1]), last);
        }

        // Called when the asset changes and before it is used at runtime.
        public void Normalize()
        {
            maxHumidityPercent = Mathf.Clamp(maxHumidityPercent, 0, 100);
            maxSoilPercent = Mathf.Clamp(maxSoilPercent, 0, 100);
            maxLife = Mathf.Max(0, maxLife);
            growthWaterPerLife = Mathf.Max(0, growthWaterPerLife);
            initialHabitats &= PlantHabitat.Snow | PlantHabitat.Grass | PlantHabitat.Desert;
            visualHeight = Mathf.Max(.05f, visualHeight);
            suitableHabitats &= PlantHabitat.Snow | PlantHabitat.Grass | PlantHabitat.Desert;
            if (stages == null || stages.Length == 0) stages = new[] { new PlantStage() };
            int lastHumidity = 0, lastSoil = 0, lastLife = 0;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] == null) stages[i] = new PlantStage();
                var stage = stages[i];
                stage.minHumidityPercent = Mathf.Clamp(stage.minHumidityPercent, lastHumidity, maxHumidityPercent);
                stage.minSoilPercent = Mathf.Clamp(stage.minSoilPercent, lastSoil, maxSoilPercent);
                stage.minLife = Mathf.Clamp(stage.minLife, lastLife, maxLife);
                lastHumidity = stage.minHumidityPercent;
                lastSoil = stage.minSoilPercent;
                lastLife = stage.minLife;
            }
        }
    }

    [CreateAssetMenu(menuName = "Hiking/Plant Catalog", fileName = "PlantCatalog")]
    public sealed class PlantCatalog : ScriptableObject
    {
        public PlantDefinition[] plants = Array.Empty<PlantDefinition>();
        public PlantDefinition Find(string id) => plants == null ? null :
            Array.Find(plants, p => p != null && p.id == id);
        public PlantDefinition[] SuitableFor(StationTheme theme)
        {
            var eligible = new List<PlantDefinition>();
            if (plants != null)
                foreach (var plant in plants)
                    if (plant != null && !string.IsNullOrWhiteSpace(plant.id) && plant.enabledForGeneration && (plant.initialHabitats & (theme == StationTheme.Snow ? PlantHabitat.Snow : theme == StationTheme.Grass ? PlantHabitat.Grass : PlantHabitat.Desert)) != 0 && plant.Suits(theme))
                        eligible.Add(plant);
            return eligible.ToArray();
        }
        public PlantInstance[] SpawnNaturalPlants(StationTheme theme, int count,
            float soilContent, float humidityPercent)
        {
            var eligible = SuitableFor(theme);
            if (eligible.Length == 0 || count <= 0) return Array.Empty<PlantInstance>();
            var result = new PlantInstance[count];
            for (int i = 0; i < count; i++)
            {
                var definition = eligible[UnityEngine.Random.Range(0, eligible.Length)];
                int allowedStage = definition.HighestSuitableStage(soilContent, humidityPercent);
                // Existing plants on unsuitable land begin declining instead of vanishing on step one.
                int ceiling = Mathf.Max(1, allowedStage < 0 ? definition.maxLife : definition.LifeCeiling(allowedStage));
                int life = UnityEngine.Random.Range(ceiling >= 2 ? 2 : 1, ceiling + 1);
                int stage = definition.StageAtLife(life, allowedStage < 0 ? definition.StageCount - 1 : allowedStage);
                result[i] = new PlantInstance(definition.id, stage) { life = life };
            }
            return result;
        }
        void OnValidate()
        {
            if (plants == null) plants = Array.Empty<PlantDefinition>();
            foreach (var plant in plants) plant?.Normalize();
        }
    }
}
