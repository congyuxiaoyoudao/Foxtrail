using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hiking.Journey
{
    [Serializable]
    public sealed class SavedInventory
    {
        public string id;
        public float amount;
    }

    [Serializable]
    public sealed class SavedTile
    {
        public float soilContent, soilWater, surfaceWater, temperature, baseEvaporation;
        public float pendingVapor, drainageLoss;
        public int height, waterGrassStage, waterGrassAge, waterGrassRetreat;
        public PlantInstance[] plants;
        public SeedInstance[] seeds;
    }

    [Serializable]
    public sealed class SavedStation
    {
        public StationTheme theme;
        public SavedTile[] tiles;
    }

    [Serializable]
    public sealed class SavedPlacement
    {
        public int stationIndex, quantity;
        public string slotId, materialId;
    }

    [Serializable]
    public sealed class CompletedJourney
    {
        public string runId;
        public int levelIndex, simulationStep;
        public string completedUtc;
        public JourneyMode mode;
        public SavedInventory[] inventory;
        public SavedStation[] stations;
        public SavedPlacement[] placements;
        public int rainStartTile, rainRemainingSeconds;
        public float rainRemainingAmount;
    }

    [Serializable]
    public sealed class JourneyArchive
    {
        public int version = 1;
        public int nextLevelIndex = 1;
        public SavedInventory[] carryInventory;
        public List<CompletedJourney> completed = new List<CompletedJourney>();
    }

    public static class JourneyArchiveStore
    {
        public static string PathName => Path.Combine(Application.persistentDataPath, "journey-archive.json");

        public static JourneyArchive Load(string path = null)
        {
            path = path ?? PathName;
            if (!File.Exists(path)) return new JourneyArchive();
            var archive = JsonUtility.FromJson<JourneyArchive>(File.ReadAllText(path));
            if (archive == null || archive.version != 1)
                throw new InvalidDataException("旅途存档版本不受支持。");
            if (archive.completed == null) archive.completed = new List<CompletedJourney>();
            archive.nextLevelIndex = Math.Max(1, archive.nextLevelIndex);
            return archive;
        }

        public static JourneyArchive SaveCompleted(JourneyArchive archive, string runId,
            int levelIndex, JourneyMode mode, JourneySession session, JourneyMap map,
            MaterialDefinition[] materials, string path = null)
        {
            if (archive == null || string.IsNullOrEmpty(runId) || map == null)
                throw new ArgumentException("完成旅途的存档信息不完整。");
            foreach (var saved in archive.completed)
                if (saved.runId == runId) return archive;
            if (levelIndex != archive.nextLevelIndex)
                throw new InvalidOperationException("本段旅途已结算，不能再次继承资源。");
            var inventory = new SavedInventory[materials.Length];
            for (int i = 0; i < materials.Length; i++)
                inventory[i] = new SavedInventory { id = materials[i].id,
                    amount = session.Amount(materials[i].id) };
            var finished = new CompletedJourney
            {
                runId = runId,
                levelIndex = levelIndex,
                mode = mode,
                simulationStep = map.SimulationStep,
                completedUtc = DateTime.UtcNow.ToString("O"),
                inventory = inventory,
                stations = new SavedStation[map.stations.Length],
                placements = new SavedPlacement[session.Placements.Count],
                rainStartTile = map.Weather.RainStartTile,
                rainRemainingSeconds = map.Weather.RemainingSeconds,
                rainRemainingAmount = map.Weather.RemainingRain
            };
            for (int i = 0; i < session.Placements.Count; i++)
            {
                var placed = session.Placements[i];
                finished.placements[i] = new SavedPlacement { stationIndex = placed.StationIndex,
                    slotId = placed.SlotId, materialId = placed.MaterialId, quantity = placed.Quantity };
            }
            for (int s = 0; s < map.stations.Length; s++)
            {
                var station = map.stations[s];
                var saved = new SavedStation { theme = station.theme,
                    tiles = new SavedTile[station.Tiles.Length] };
                for (int t = 0; t < station.Tiles.Length; t++)
                {
                    var tile = station.Tiles[t];
                    saved.tiles[t] = new SavedTile
                    {
                        soilContent = tile.SoilContent, soilWater = tile.SoilWater,
                        surfaceWater = tile.SurfaceWater, temperature = tile.Temperature,
                        baseEvaporation = tile.BaseEvaporationRate,
                        pendingVapor = tile.PendingVapor, drainageLoss = tile.DrainageLoss,
                        height = tile.Height, waterGrassStage = (int)tile.WaterGrass,
                        waterGrassAge = tile.WaterGrassAge, waterGrassRetreat = tile.WaterGrassRetreat,
                        plants = tile.NaturalPlants, seeds = tile.Seeds.ToArray()
                    };
                }
                finished.stations[s] = saved;
            }
            // Build the next archive before touching the file or the in-memory original.
            var next = new JourneyArchive { nextLevelIndex = levelIndex + 1,
                carryInventory = inventory,
                completed = new List<CompletedJourney>(archive.completed) };
            next.completed.Add(finished);
            path = path ?? PathName;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(next, true));
            try
            {
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return next;
        }
    }
}
