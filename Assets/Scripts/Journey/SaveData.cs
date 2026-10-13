using System;

namespace Hiking.Journey
{
    // 进度档的数据结构（需求：游戏存档/读档/关卡）。
    //
    // 三条约定，写在这里免得以后忘：
    // 1. 只存"原始状态"。派生值（最大含水量、土壤湿度、蒸发率、水深枚举、植物数量）一律不存，
    //    否则读档后会有两套真相，迟早对不上。
    // 2. JsonUtility 不支持 Dictionary / HashSet，所以背包、投放记录、已领取格一律用数组。
    // 3. 枚举存成 int，不存枚举本身。以后加值不影响老档。
    //
    // 关于随机：主程已确认「读档后允许未来随机重掷」，所以这里不放随机数发生器的状态，
    // 也不为此把 UnityEngine.Random 换成种子随机。注意区分：
    // 进行中的那场雨仍然要完整保存（覆盖范围、剩余雨量、剩余时间、玩家已接量），
    // 那是已发生的事实，不是随机状态。
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 3;

        public int schemaVersion = CurrentVersion;
        public long savedAtUtcTicks;
        public string runId = "";
        public int levelIndex;
        public string sceneLabel = "";

        public SessionData session = new SessionData();
        public MapData map = new MapData();
        public PlayerData player = new PlayerData();
        public WeatherData weather = new WeatherData();
        public StationData[] stations = Array.Empty<StationData>();
    }

    // 玩家与流程：对应 JourneySession / GameFlowController / TravelerController。
    [Serializable]
    public sealed class SessionData
    {
        public int phase;
        public int stationIndex;
        public int stationCount;
        public double remainingSeconds;
        public double waitSeconds;
        public double residenceSeconds;
        public bool approachingFirstStation;
        public int rainEventId = -1;
        public float rainCollected;
        public InventoryEntry[] inventory = Array.Empty<InventoryEntry>();
        public int[] seedCollectedTiles = Array.Empty<int>();
        public PlacementEntry[] placements = Array.Empty<PlacementEntry>();
    }

    [Serializable]
    public sealed class InventoryEntry
    {
        public string materialId;
        public float amount;
    }

    [Serializable]
    public sealed class PlacementEntry
    {
        public int stationIndex;
        public string slotId;
        public string materialId;
        public int quantity;
    }

    // 狐狸的位置与"是否正在两格之间行走"，对应 TravelerController。
    // moveFraction 是这一格走了多少（0~1），读档后从保存位置继续走。
    [Serializable]
    public sealed class PlayerData
    {
        public int mode;
        public int activity;
        public int currentTileIndex;
        public float progress01;
        public bool isMoving;
        public float moveFraction;
        public float moveDirection;
        public bool debugMode;
        public int debugStepCount;
    }

    [Serializable]
    public sealed class MapData
    {
        public int simulationStep;
    }

    // 进行中的降雨：覆盖范围、剩余雨量、剩余时间都要存，
    // 读档时不能重新抽取这场雨，也不能重新发放完整降水。
    [Serializable]
    public sealed class WeatherData
    {
        public int rainStartTile = -1;
        public int remainingSeconds;
        public float remainingRain;
        public float eventRain;
        public int eventId;
        public int lastCheckStep;
        public float lastCheckVapor;
        public bool lastCheckSucceeded;
        public bool rainAppliedThisStep;
    }

    [Serializable]
    public sealed class StationData
    {
        public string stationId = "";
        public int theme;
        public int tileCount;
        public TileData[] tiles = Array.Empty<TileData>();
    }

    [Serializable]
    public sealed class TileData
    {
        public bool hasExtendedEcologyState;
        public int category;
        public float soilContent;
        public float soilWater;
        public float baseEvaporationRate;
        public float surfaceWater;
        public float pondFadeRemainingSeconds;
        public float temperature;
        public float climateTemperature;
        public float referenceWater;
        public float pendingVapor;
        public float drainageLoss;
        public int height;
        public PlantData[] plants = Array.Empty<PlantData>();
        public SeedData[] seeds = Array.Empty<SeedData>();
    }

    [Serializable]
    public sealed class PlantData
    {
        public string plantId = "";
        public int stageIndex;
        public int life = 1;
        public int lastLifeChange;
        public int matureSteps;
        public int nextSeedStep;
        public float positionOffset;
    }

    [Serializable]
    public sealed class SeedData
    {
        public string plantId = "";
        public int remainingSteps = 30;
        public int createdStep;
    }
}
