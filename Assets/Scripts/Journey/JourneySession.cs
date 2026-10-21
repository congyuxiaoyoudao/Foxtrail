using System;
using System.Collections.Generic;

namespace Hiking.Journey
{
    public readonly struct PlacementRequest
    {
        public readonly int StationIndex;
        public readonly string SlotId;
        public readonly string MaterialId;
        public readonly int Quantity;
        public PlacementRequest(int stationIndex, string slotId, string materialId, int quantity = 1)
        { StationIndex = stationIndex; SlotId = slotId; MaterialId = materialId; Quantity = quantity; }
    }

    // Journey rules are independent of scene objects, animation and UI.
    public sealed class JourneySession
    {
        public JourneyPhase Phase { get; private set; } = JourneyPhase.Start;
        public int StationIndex { get; private set; }
        public int StationCount { get; private set; }
        public double RemainingSeconds { get; private set; }
        public double WaitSeconds { get; private set; }
        public double ResidenceSeconds { get; private set; }
        public bool Ready => Phase == JourneyPhase.AtSpawn || Phase == JourneyPhase.AtStation && RemainingSeconds <= 0;
        public bool ApproachingFirstStation => approachingFirstStation;
        public IReadOnlyList<PlacementRequest> Placements => placements;
        // 存档需要读取下面这些私有状态；只加只读入口，不改任何现有逻辑。
        public bool ApproachingFirstStationFlag => approachingFirstStation;
        public int RainEventId => rainEventId;
        public float RainCollected => rainCollected;
        public IReadOnlyDictionary<string, float> Inventory => inventory;
        public IReadOnlyCollection<int> SeedCollectedTiles => seedCollectedTiles;
        public const int CarryCapacity = 20;
        public const float PuddleCollectionLimit = 3f;
        readonly Dictionary<string, float> inventory = new Dictionary<string, float>();
        readonly List<PlacementRequest> placements = new List<PlacementRequest>();
        readonly HashSet<int> seedCollectedTiles = new HashSet<int>();
        int rainEventId = -1;
        float rainCollected;
        bool approachingFirstStation;

        public void Begin(int stationCount, double waitSeconds, IEnumerable<MaterialDefinition> materials,
            bool approachFirstStation = false)
        {
            if (stationCount < 1 || waitSeconds <= 0) throw new ArgumentOutOfRangeException();
            inventory.Clear(); placements.Clear(); seedCollectedTiles.Clear();
            rainEventId = -1; rainCollected = 0;
            foreach (var material in materials)
                inventory.Add(material.id, Math.Min(CarryCapacity, Math.Max(0, material.startingCount)));
            StationCount = stationCount;
            WaitSeconds = waitSeconds;
            ResidenceSeconds = 0;
            StationIndex = 0;
            approachingFirstStation = approachFirstStation;
            RemainingSeconds = approachFirstStation ? 0 : WaitSeconds;
            Phase = approachFirstStation ? JourneyPhase.AtSpawn : JourneyPhase.AtStation;
        }

        public float Amount(string id) => id != null && inventory.TryGetValue(id, out var count) ? count : 0;
        public int Stock(string id) => (int)Math.Floor(Amount(id) + 0.000001f);
        public int CollectSeedsOnFirstEntry(int globalTile, IReadOnlyList<PlantInstance> plants)
        {
            if (globalTile < 0 || !seedCollectedTiles.Add(globalTile)) return 0;
            if (!inventory.ContainsKey("seed")) return 0;
            int remainingCapacity = Math.Max(0, CarryCapacity - Stock("seed"));
            int collected = 0;
            if (plants != null)
                foreach (var plant in plants)
                {
                    if (plant == null || plant.life <= 0) continue;
                    // The first stage yields none; every later stage yields one seed.
                    int seedYield = (int)Math.Min(remainingCapacity - collected,
                        Math.Min(1, Math.Max(0, plant.stageIndex)));
                    collected += seedYield;
                    if (collected == remainingCapacity) break;
                }
            inventory["seed"] += collected;
            return collected;
        }
        public float CollectPuddleWater(float available)
        {
            if (!inventory.ContainsKey("water")) return 0;
            float collected = Math.Min(PuddleCollectionLimit, Math.Min(Math.Max(0, available),
                Math.Max(0, CarryCapacity - Amount("water"))));
            inventory["water"] += collected;
            return collected;
        }
        public float CollectRain(int eventId)
        {
            if (!inventory.ContainsKey("water") || eventId < 0) return 0;
            if (rainEventId != eventId) { rainEventId = eventId; rainCollected = 0; }
            float collected = Math.Min(1f, Math.Max(0, CarryCapacity - Amount("water")));
            inventory["water"] += collected;
            rainCollected += collected;
            return collected;
        }
        public void Tick(double seconds)
        {
            if (Phase == JourneyPhase.AtStation && seconds > 0)
                { ResidenceSeconds += seconds; RemainingSeconds = Math.Max(0, WaitSeconds - ResidenceSeconds); }
        }
        public bool TryDepart(bool automatic = false)
        {
            if (automatic ? Phase != JourneyPhase.AtStation : !Ready) return false;
            RemainingSeconds = 0;
            Phase = JourneyPhase.Moving;
            return true;
        }
        public bool Arrive()
        {
            if (Phase != JourneyPhase.Moving) return false;
            if (approachingFirstStation)
            {
                approachingFirstStation = false;
                RemainingSeconds = WaitSeconds;
                Phase = JourneyPhase.AtStation;
                return true;
            }
            if (StationIndex == StationCount - 1) Phase = JourneyPhase.Finalizing;
            else
            {
                StationIndex++;
                ResidenceSeconds = 0;
                RemainingSeconds = WaitSeconds;
                Phase = JourneyPhase.AtStation;
            }
            return true;
        }
        public bool CompleteFinalization()
        {
            if (Phase != JourneyPhase.Finalizing) return false;
            Phase = JourneyPhase.Completed;
            return true;
        }
        // Slot membership is validated by PlacementController against the live map.
        public bool TryPlace(PlacementRequest request)
        {
            if (Phase != JourneyPhase.AtStation || request.StationIndex != StationIndex ||
                string.IsNullOrEmpty(request.SlotId) || request.Quantity <= 0 ||
                Stock(request.MaterialId) < request.Quantity) return false;
            inventory[request.MaterialId] -= request.Quantity;
            placements.Add(request);
            return true;
        }
        public void Reset()
        {
            Phase = JourneyPhase.Start; StationIndex = 0; StationCount = 0;
            RemainingSeconds = 0; ResidenceSeconds = 0; inventory.Clear(); placements.Clear(); seedCollectedTiles.Clear();
            rainEventId = -1; rainCollected = 0;
            approachingFirstStation = false;
        }

        // 读档恢复。字段全是私有 set，只能在类内部赋值。
        // 背包与"该格已首次领取"必须一起恢复：策划要求两者同时记录，
        // 否则会出现"领了种子但标记没存"或反过来，读档后重复领取。
        public void Restore(SessionData data)
        {
            if (data == null) return;

            Phase = (JourneyPhase)data.phase;
            StationIndex = data.stationIndex;
            StationCount = data.stationCount;
            RemainingSeconds = data.remainingSeconds;
            WaitSeconds = data.waitSeconds;
            ResidenceSeconds = data.residenceSeconds;
            approachingFirstStation = data.approachingFirstStation;
            rainEventId = data.rainEventId;
            rainCollected = data.rainCollected;

            inventory.Clear();
            if (data.inventory != null)
                foreach (var entry in data.inventory)
                    if (!string.IsNullOrEmpty(entry.materialId)) inventory[entry.materialId] = entry.amount;

            placements.Clear();
            if (data.placements != null)
                foreach (var entry in data.placements)
                    placements.Add(new PlacementRequest(entry.stationIndex, entry.slotId, entry.materialId, entry.quantity));

            seedCollectedTiles.Clear();
            if (data.seedCollectedTiles != null)
                foreach (var tile in data.seedCollectedTiles) seedCollectedTiles.Add(tile);
        }
    }
}
