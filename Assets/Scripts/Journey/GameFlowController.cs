using System;
using UnityEngine;

namespace Hiking.Journey
{
    public enum JourneyActivity { None, Walking, Resting, AwaitingPlacement, BetweenStations, Complete }
    public class GameFlowController : MonoBehaviour
    {
        public JourneyConfig Config;
        public MapProvider MapProvider;
        public TravelerController Traveler;
        public PlacementController Placement;
        public CameraController Camera;
        public GameUIController UI;
        public Transform WorldRoot;
        public JourneySession Session { get; } = new JourneySession();
        public JourneyMap Map { get; private set; }
        public JourneyMode Mode { get; private set; }
        public float MoveSeconds => Config.MoveSeconds(Mode);
        public float StationTransitionSeconds => Config.TransitionSeconds(Mode);
        public JourneyActivity Activity { get; private set; }
        public int CurrentTileIndex { get; private set; }
        double lastTime;
        float environmentSeconds;
        int generation;
        public bool IsPreviewing { get; private set; }
        public bool IsDebugMode { get; private set; }
        public int DebugStepCount { get; private set; }
        public Station CurrentStation => Map != null ? Map.stations[Session.StationIndex] : null;

        [Header("存档")]
        [Tooltip("自动保存间隔（秒）。对应策划的『定期保存，间隔作为可配置参数』。")]
        [Min(5f)] public float autosaveIntervalSec = 60f;
        [Tooltip("离线推进上限（小时）。超过按上限结算，避免离线一周后开游戏卡死。")]
        [Min(0f)] public float offlineAdvanceCapHours = 8f;
        [Tooltip("单次读档最多推进的模拟步数预算。")]
        [Min(0)] public int offlineStepBudget = 3600;
        float autosaveSeconds;
        void Start()
        {
            Application.runInBackground = true;
            Placement.Initialize(this); Camera.Initialize(this); UI.Initialize(this);
            ReturnToStart();
        }
        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float delta = (float)(now - lastTime); lastTime = now;
            if (IsPreviewing || IsDebugMode) return;
            Session.Tick(delta);
            TickAutosave(delta);
            if (Map == null || UI.IsModalOpen || Activity == JourneyActivity.Complete) return;
            environmentSeconds += Mathf.Max(0, delta);
            while (environmentSeconds >= 1f)
            {
                Map.StepEnvironment(CollectRainAtCurrentTile);
                environmentSeconds -= 1f;
            }
        }

        // 定期自动保存（关键操作后的保存见 CollectOnTileEntry / CompletePlacementVisit）
        void TickAutosave(float delta)
        {
            if (Map == null || Session.Phase == JourneyPhase.Start) return;
            autosaveSeconds += Mathf.Max(0, delta);
            if (autosaveSeconds < Mathf.Max(5f, autosaveIntervalSec)) return;
            autosaveSeconds = 0;
            SaveNow();
        }

        // ---- 存档 ----

        public bool HasSave() => SaveSystem.Exists;

        public bool SaveNow()
        {
            if (Map == null || IsDebugMode || Session.Phase == JourneyPhase.Start) return false;
            var data = SaveCapture.Capture(this);
            return data != null && SaveSystem.Write(data);
        }

        public bool LoadGame()
        {
            var data = SaveSystem.Read();
            if (data == null) { UI.Notify("没有可继续的存档"); return false; }
            return RestoreFrom(data);
        }

        // 读档编排：建地图 → 覆盖数据 → 离线推进 → 按退出状态表恢复交互。
        // 期间先把 generation 推到新值，让上一局遗留的移动回调失效（和 StartJourney 一样）。
        public bool RestoreFrom(SaveData data)
        {
            if (data == null || Map != null || IsDebugMode) return false;
            try
            {
                // 用存档里的站点主题建图：主题决定远景贴图、路面颜色和地块初始化，
                // 事后改主题要追三处，不如一开始就建对。
                var themes = new StationTheme[data.stations == null ? 0 : data.stations.Length];
                for (int i = 0; i < themes.Length; i++) themes[i] = (StationTheme)data.stations[i].theme;

                Map = MapProvider.CreateMap(WorldRoot, themes);
                if (!Map.Validate(out string error)) throw new InvalidOperationException(error);

                generation++;
                Mode = (JourneyMode)data.player.mode;
                Session.Begin(Map.stations.Length, .1, Config.materials);
                Traveler.gameObject.SetActive(true);
                Traveler.RestorePosition(Map, data.player.progress01);

                if (!SaveRestore.Apply(this, data)) throw new InvalidOperationException("存档与当前地图结构不匹配。");

                CurrentTileIndex = Mathf.Clamp(data.player.currentTileIndex, 0, Mathf.Max(0, CurrentStation.tileCount - 1));
                Activity = (JourneyActivity)data.player.activity;
                environmentSeconds = 0; autosaveSeconds = 0;
                lastTime = Time.realtimeSinceStartupAsDouble;
                Camera.ShowStation(CurrentStation);

                AdvanceOffline(data);
                ResumeInteraction();
                return true;
            }
            catch (Exception error)
            {
                ReturnToStart(); UI.Notify("读档失败：" + error.Message); Debug.LogException(error);
                return false;
            }
        }

        // 离线推进：沿用在线语义，只推进环境，不动狐狸、不结算采集、不替玩家做决定。
        // 狐狸仍能接雨——CollectRainAtCurrentTile 只认它当前所在的格子。
        void AdvanceOffline(SaveData data)
        {
            if (Map == null) return;
            double seconds = (DateTime.UtcNow - SaveSystem.SavedAtUtc(data)).TotalSeconds;
            if (seconds <= 0) return;

            double cap = Math.Max(0f, offlineAdvanceCapHours) * 3600.0;
            double budget = Math.Max(0, offlineStepBudget);
            int steps = (int)Math.Min(Math.Min(seconds, cap), budget);
            if (steps <= 0) return;

            for (int i = 0; i < steps; i++) Map.StepEnvironment(CollectRainAtCurrentTile);
            environmentSeconds = 0;
            UI.Notify("离线 " + DescribeSpan(seconds) + " · 生态已继续" + (seconds > steps ? "（按上限结算）" : ""));
        }

        static string DescribeSpan(double seconds)
        {
            if (seconds >= 3600) return (seconds / 3600.0).ToString("0.#") + " 小时";
            if (seconds >= 60) return (seconds / 60.0).ToString("0.#") + " 分钟";
            return Math.Max(1, (int)seconds) + " 秒";
        }

        // 按策划的「退出前状态 / 读档后行为」对照表恢复交互。
        void ResumeInteraction()
        {
            Placement.ClearSelection();   // 「只选了材料，尚未确认」→ 取消，回到等待状态
            switch (Activity)
            {
                case JourneyActivity.Walking:
                    // 「正在两格之间行走」→ 从保存位置继续走到下一格
                    Advance();
                    break;
                case JourneyActivity.AwaitingPlacement:
                    // 「在投放格等待选择」→ 继续等待玩家操作
                    Placement.OnTravelerArrived();
                    break;
                case JourneyActivity.BetweenStations:
                    ResumeBetweenStations();
                    break;
                default:
                    // 「已确认投放」的记录与扣除后的库存都已随地块一起恢复，这里不重复执行
                    break;
            }
        }

        void ResumeBetweenStations()
        {
            int target = Session.StationIndex + 1;
            if (Map == null || target >= Map.stations.Length) { Activity = JourneyActivity.None; return; }
            int currentGeneration = generation;
            Traveler.MoveToProgress(Map.stations[target].stopProgress, StationTransitionSeconds, () =>
            {
                if (generation != currentGeneration || !Session.Arrive()) return;
                CurrentTileIndex = 0; Camera.ShowStation(CurrentStation);
                CollectOnTileEntry(); Advance();
            });
        }

        // 策划要求：直接关闭窗口时也尝试保存
        void OnApplicationQuit() { SaveNow(); }
        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        public MaterialDefinition Material(string id) => Array.Find(Config.materials, entry => entry.id == id);
        public void StartJourney(JourneyMode mode)
        {
            if (Session.Phase != JourneyPhase.Start || Map != null) return;
            try
            {
                Map = MapProvider.CreateMap(WorldRoot);
                if (!Map.Validate(out string error)) throw new InvalidOperationException(error);
                Session.Begin(Map.stations.Length, .1, Config.materials);
                generation++; Mode = mode; CurrentTileIndex = 0; environmentSeconds = 0;
                Traveler.gameObject.SetActive(true); Traveler.PlaceOnRoute(Map);
                Camera.ShowStation(CurrentStation);
                lastTime = Time.realtimeSinceStartupAsDouble;
                Placement.ClearSelection();
                // Spawn at the first tile and start the first timed step immediately.
                CollectOnTileEntry();
                Advance();
            }
            catch (Exception error)
            {
                ReturnToStart(); UI.Notify("无法开始旅途：" + error.Message); Debug.LogException(error);
            }
        }
        public void StartDebugMode()
        {
            if (Session.Phase != JourneyPhase.Start || Map != null) return;
            try
            {
                Map = MapProvider.CreateMap(WorldRoot);
                if (!Map.Validate(out string error)) throw new InvalidOperationException(error);
                Session.Begin(Map.stations.Length, .1, Config.materials);
                generation++; IsDebugMode = true; DebugStepCount = 0; CurrentTileIndex = 0; environmentSeconds = 0;
                Activity = JourneyActivity.None;
                Traveler.CancelMovement(); Traveler.gameObject.SetActive(false);
                foreach (var station in Map.stations) station.SetDebugLabels(true);
                Camera.ShowDebugMap();
            }
            catch (Exception error)
            {
                ReturnToStart(); UI.Notify("无法打开调试地图：" + error.Message); Debug.LogException(error);
            }
        }
        public void StepDebugMap()
        {
            if (!IsDebugMode || Map == null) return;
            Map.StepEnvironment();
            DebugStepCount++;
        }
        void Advance()
        {
            if (Map == null || IsDebugMode || IsPreviewing || Traveler.IsMoving || UI.IsModalOpen) return;
            if (CurrentTileIndex == CurrentStation.tileCount - 1) { Depart(); return; }
            Activity = JourneyActivity.Walking;
            int nextTile = CurrentTileIndex + 1, currentGeneration = generation;
            Traveler.MoveToProgress(Map.ProgressAtDistance(CurrentStation.TileCenter(nextTile)), MoveSeconds, () =>
            {
                if (generation != currentGeneration) return;
                CurrentTileIndex = nextTile;
                CollectOnTileEntry();
                if (Placement.CurrentSlot != null)
                {
                    Activity = JourneyActivity.AwaitingPlacement;
                    Placement.OnTravelerArrived();
                }
                else Advance();
            });
        }
        public void CompletePlacementVisit()
        {
            if (Activity != JourneyActivity.AwaitingPlacement || IsPreviewing) return;
            UI.ClosePlacementPrompt(false);
            SaveNow();   // 策划的关键操作：投放确认后保存，记录与扣除后的库存一起落盘
            Advance();
        }
        void Depart()
        {
            if (!Session.TryDepart(true)) return;
            Placement.ClearSelection();
            int currentGeneration = generation;
            if (Session.StationIndex == Map.stations.Length - 1)
            {
                Session.Arrive(); Activity = JourneyActivity.Complete;
                Camera.RevealPanorama(Map.panoramaBounds, () =>
                {
                    if (generation == currentGeneration) Session.FinishReveal();
                });
                return;
            }
            Activity = JourneyActivity.BetweenStations;
            Traveler.MoveToProgress(Map.stations[Session.StationIndex + 1].stopProgress, StationTransitionSeconds, () =>
            {
                if (generation != currentGeneration || !Session.Arrive()) return;
                CurrentTileIndex = 0; Camera.ShowStation(CurrentStation);
                CollectOnTileEntry(); Advance();
            });
        }
        int CurrentGlobalTile => Session.StationIndex * RingMapSettings.TilesPerStation + CurrentTileIndex;
        float CollectRainAtCurrentTile(int eventId, int globalTile, float amount)
        {
            int occupiedTile = Mathf.Clamp(Mathf.FloorToInt(
                Map.RouteDistanceAtProgress(Traveler.Progress01) / Map.TileWidth),
                0, Map.stations.Length * RingMapSettings.TilesPerStation - 1);
            return globalTile == occupiedTile && Activity != JourneyActivity.Complete ?
                Session.CollectRain(eventId, amount) : 0;
        }
        void CollectOnTileEntry()
        {
            if (Map == null || IsDebugMode) return;
            var tile = CurrentStation.Tiles[CurrentTileIndex];
            int seeds = Session.CollectSeedsOnFirstEntry(CurrentGlobalTile, tile.PlantCount);
            float water = tile.IsPuddle ? Session.CollectPuddleWater(tile.SurfaceWater) : 0;
            if (water > 0)
            {
                tile.TakeSurfaceWater(water);
                CurrentStation.RefreshTile(CurrentTileIndex);
            }
            if (seeds > 0 || water > 0)
                UI.Notify($"采集：种子 +{seeds} · 水 +{water:0.##}");
            // 策划的关键操作：进入格子并完成采集后保存。
            // 库存变化和「该格已首次领取」标记必须同一次写入，否则读档后会重复领取。
            SaveNow();
        }
        public void ReturnToStart()
        {
            generation++; UI.CloseExitPrompt(); UI.ClosePlacementPrompt(false);
            IsPreviewing = false; IsDebugMode = false; DebugStepCount = 0; environmentSeconds = 0; Traveler.Paused = false;
            Activity = JourneyActivity.None; CurrentTileIndex = 0;
            Traveler.CancelMovement(); Camera.ResetCamera(); Placement.ClearSelection();
            Traveler.transform.SetParent(null, true); Traveler.gameObject.SetActive(false);
            if (Map != null) { Map.gameObject.SetActive(false); Destroy(Map.gameObject); Map = null; }
            Session.Reset(); lastTime = Time.realtimeSinceStartupAsDouble; UI.Notify("");
        }
        public void PreviewPanorama()
        {
            if (Map == null || IsDebugMode || IsPreviewing || UI.IsModalOpen || Session.Phase == JourneyPhase.Revealing || Session.Phase == JourneyPhase.Panorama) return;
            IsPreviewing = true; Traveler.Paused = true;
            Camera.RevealPanorama(Map.panoramaBounds, () => { });
        }
        public void ExitPreview()
        {
            if (!IsPreviewing || Map == null) return;
            Camera.ResetCamera(); Map.ShowJourney(); Traveler.RefreshPosition();
            IsPreviewing = false; Traveler.Paused = false; lastTime = Time.realtimeSinceStartupAsDouble;
            Camera.RestoreJourneyView(); Placement.RefreshHighlights();
        }
        void OnDestroy() { if (Traveler != null) Traveler.CancelMovement(); }
    }
}
