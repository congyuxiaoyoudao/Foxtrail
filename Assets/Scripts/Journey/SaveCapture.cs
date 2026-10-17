using System.Collections.Generic;

namespace Hiking.Journey
{
    // 把运行时状态抓成 SaveData。只读，不改动任何运行时对象。
    // 恢复是另一回事（SaveRestore），因为状态类的字段都是 private set，
    // 从外部赋不进去，需要在各自类里加恢复入口。
    public static class SaveCapture
    {
        public static SaveData Capture(GameFlowController flow)
        {
            if (flow == null || flow.Map == null || flow.Session == null) return null;

            var data = new SaveData();
            data.runId = flow.RunId;
            data.levelIndex = flow.LevelIndex;
            var session = flow.Session;

            var s = data.session;
            s.phase = (int)session.Phase;
            s.stationIndex = session.StationIndex;
            s.stationCount = session.StationCount;
            s.remainingSeconds = session.RemainingSeconds;
            s.waitSeconds = session.WaitSeconds;
            s.residenceSeconds = session.ResidenceSeconds;
            s.approachingFirstStation = session.ApproachingFirstStationFlag;
            s.rainEventId = session.RainEventId;
            s.rainCollected = session.RainCollected;

            var inventory = new List<InventoryEntry>();
            foreach (var pair in session.Inventory)
                inventory.Add(new InventoryEntry { materialId = pair.Key, amount = pair.Value });
            s.inventory = inventory.ToArray();

            // 已首次领取的格子：排序后写入，档内容稳定，方便肉眼比对
            var collected = new List<int>(session.SeedCollectedTiles);
            collected.Sort();
            s.seedCollectedTiles = collected.ToArray();

            var placements = new List<PlacementEntry>();
            foreach (var request in session.Placements)
                placements.Add(new PlacementEntry
                {
                    stationIndex = request.StationIndex,
                    slotId = request.SlotId,
                    materialId = request.MaterialId,
                    quantity = request.Quantity
                });
            s.placements = placements.ToArray();
            var plans = new List<PlacementPlanEntry>();
            foreach (var station in flow.Map.stations)
                foreach (var slot in station.slots)
                    plans.Add(new PlacementPlanEntry
                    {
                        slotId = slot.slotId,
                        water = slot.PlannedWater,
                        seeds = slot.PlannedSeeds,
                        custom = slot.HasCustomPlan,
                        committed = slot.Committed,
                        warningAtArrival = slot.WarningAtArrival
                    });
            s.placementPlans = plans.ToArray();

            data.map.simulationStep = flow.Map.SimulationStep;

            var weather = flow.Map.Weather;
            var w = data.weather;
            w.rainStartTile = weather.RainStartTile;
            w.remainingSeconds = weather.RemainingSeconds;
            w.remainingRain = weather.RemainingRain;
            w.eventRain = weather.EventRain;
            w.eventId = weather.EventId;
            w.lastCheckStep = weather.LastCheckStep;
            w.lastCheckVapor = weather.LastCheckVapor;
            w.lastCheckSucceeded = weather.LastCheckSucceeded;
            w.rainAppliedThisStep = weather.RainAppliedThisStep;

            var p = data.player;
            p.mode = (int)flow.Mode;
            p.activity = (int)flow.Activity;
            p.currentTileIndex = flow.CurrentTileIndex;
            p.progress01 = flow.Traveler.Progress01;
            p.isMoving = flow.Traveler.IsMoving;
            p.moveFraction = flow.Traveler.MoveFraction;
            p.moveDirection = flow.Traveler.MoveDirection;
            p.placementStopRemaining = flow.Placement.RemainingSeconds;

            var stations = new List<StationData>();
            foreach (var station in flow.Map.stations)
                stations.Add(CaptureStation(station));
            data.stations = stations.ToArray();

            return data;
        }

        static StationData CaptureStation(Station station)
        {
            var data = new StationData
            {
                stationId = station.stationId ?? "",
                theme = (int)station.theme,
                tileCount = station.tileCount
            };
            if (station.Tiles == null) return data;

            var tiles = new List<TileData>(station.Tiles.Length);
            for (int i = 0; i < station.Tiles.Length; i++)
            {
                var tile = CaptureTile(station.Tiles[i]);
                station.CaptureTileTransition(i, tile);
                tile.pondFadeRemainingSeconds = station.PondFadeRemaining(i);
                tiles.Add(tile);
            }
            data.tiles = tiles.ToArray();
            return data;
        }

        static TileData CaptureTile(TileState tile)
        {
            var data = new TileData();
            if (tile == null) return data;

            data.category = (int)tile.Category;
            data.soilContent = tile.SoilContent;
            data.soilWater = tile.SoilWater;
            data.baseEvaporationRate = tile.BaseEvaporationRate;
            data.evaporationRate = tile.EvaporationRate;
            data.evaporatedThisStep = tile.EvaporatedThisStep;
            data.rainfallThisStep = tile.RainfallThisStep;
            data.lastChange = tile.LastChange;
            data.hasVisualState = true;
            data.surfaceWater = tile.SurfaceWater;
            data.temperature = tile.Temperature;
            data.hasExtendedEcologyState = true;
            data.climateTemperature = tile.ClimateTemperature;
            data.referenceWater = tile.ReferenceWater;
            data.pendingVapor = tile.PendingVapor;
            data.drainageLoss = tile.DrainageLoss;
            data.height = tile.Height;

            var plants = new List<PlantData>();
            if (tile.NaturalPlants != null)
                foreach (var plant in tile.NaturalPlants)
                    plants.Add(new PlantData
                    {
                        plantId = plant.plantId ?? "",
                        stageIndex = plant.stageIndex,
                        life = plant.life,
                        lastLifeChange = plant.lastLifeChange,
                        matureSteps = plant.matureSteps,
                        nextSeedStep = plant.nextSeedStep,
                        positionOffset = plant.positionOffset
                    });
            data.plants = plants.ToArray();

            var seeds = new List<SeedData>();
            foreach (var seed in tile.Seeds)
                seeds.Add(new SeedData
                {
                    plantId = seed.plantId ?? "",
                    remainingSteps = seed.remainingSteps,
                    createdStep = seed.createdStep
                });
            data.seeds = seeds.ToArray();

            return data;
        }
    }
}
