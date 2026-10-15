using System;
using System.Collections;
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
        string runId;
        JourneyArchive archive;
        Coroutine appreciationEntrance;
        public bool IsAppreciating { get; private set; }
        public bool IsPreviewing { get; private set; }
        public bool IsBrowsingPanorama => IsPreviewing || IsAppreciating;
        public bool IsEnteringAppreciation { get; private set; }
        public bool HasAppreciated { get; private set; }
        public bool IsFinalized { get; private set; }
        public string SaveError { get; private set; }
        public int LevelIndex { get; private set; } = 1;
        public string RunId => runId;
        public bool IsDebugMode { get; private set; }
        public int DebugStepCount { get; private set; }
        public Station CurrentStation => Map != null ? Map.stations[Session.StationIndex] : null;

        [Header("存档")]
        [Tooltip("自动保存间隔（秒）。对应策划的『定期保存，间隔作为可配置参数』。")]
        [Min(5f)] public float autosaveIntervalSec = 60f;
        [Tooltip("离线推进上限（小时）。超过按上限结算，避免离线一周后开游戏卡死。已定为 2 小时。")]
        [Min(0f)] public float offlineAdvanceCapHours = 2f;
        [Tooltip("单次读档最多推进的模拟步数预算。注意它和上面是「谁小谁生效」的关系：" +
            "2 小时离线 = 7200 步，所以这个值必须不小于 7200，否则上限会被预算悄悄截短。" +
            "它同时是读档卡顿的保险丝——每步要更新 72 格地块与植物，步数越大读档冻得越久。")]
        [Min(0)] public int offlineStepBudget = 7200;
        [Tooltip("离线结算时每帧最多花多少毫秒。太大掉帧，太小结算变慢。8 毫秒约等于 120fps 的余量。")]
        [Min(.5f)] public float offlineStepBudgetMsPerFrame = 8f;
        // 读档时离线结算是分帧跑的：玩家看得见进度，画面也不会假死
        public bool IsSettlingOffline { get; private set; }
        public float OfflineProgress01 { get; private set; }
        string offlineSummary;
        float autosaveSeconds;
        void Start()
        {
            Application.runInBackground = true;
            Placement.Initialize(this); Camera.Initialize(this); UI.Initialize(this);
            ReturnToStart();
            try { archive = JourneyArchiveStore.Load(); }
            catch (Exception error) { SaveError = error.Message; Debug.LogException(error); }
            DiscardSettledSave();
        }
        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float delta = (float)(now - lastTime); lastTime = now;
            // 离线结算期间不推进旅途、不结算行动点：这一段时间属于"读档"，不属于游戏
            if (IsSettlingOffline) return;
            if (IsPreviewing || IsDebugMode || Activity == JourneyActivity.Complete) return;
            Session.Tick(delta);
            TickAutosave(delta);
            if (Map == null) return;
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
            if (Map == null || IsDebugMode || IsSettlingOffline || IsFinalized ||
                Activity == JourneyActivity.Complete || Session.Phase == JourneyPhase.Start) return false;
            var data = SaveCapture.Capture(this);
            return data != null && SaveSystem.Write(data);
        }

        void DiscardSettledSave()
        {
            if (archive == null || !SaveSystem.Exists) return;
            var saved = SaveSystem.Read();
            if (saved != null && saved.levelIndex > 0 && saved.levelIndex < archive.nextLevelIndex)
                SaveSystem.Delete();
        }

        public bool LoadGame()
        {
            var data = SaveSystem.Read();
            if (data == null) { UI.Notify("没有可继续的存档"); return false; }
            if (data.schemaVersion == 1 && archive != null && archive.nextLevelIndex == 1 &&
                IsPlayableSave(data))
            {
                data.levelIndex = archive.nextLevelIndex;
                data.runId = Guid.NewGuid().ToString("N");
            }
            if (archive == null || data.levelIndex != archive.nextLevelIndex ||
                string.IsNullOrEmpty(data.runId) || !IsPlayableSave(data))
            {
                UI.Notify("进度档已过期或无法与当前关卡对应");
                return false;
            }
            return RestoreFrom(data);
        }

        // 读档编排：建地图 → 覆盖数据 → 离线推进 → 按退出状态表恢复交互。
        // 期间先把 generation 推到新值，让上一局遗留的移动回调失效（和 StartJourney 一样）。
        public bool RestoreFrom(SaveData data)
        {
            if (data == null || Map != null || IsDebugMode || archive == null ||
                data.levelIndex != archive.nextLevelIndex || string.IsNullOrEmpty(data.runId) ||
                !IsPlayableSave(data) || data.map == null || data.weather == null ||
                data.stations == null || data.stations.Length == 0 ||
                data.session.stationIndex < 0 || data.session.stationIndex >= data.stations.Length ||
                data.session.stationCount != data.stations.Length) return false;
            try
            {
                // 用存档里的站点主题建图：主题决定远景贴图、路面颜色和地块初始化，
                // 事后改主题要追三处，不如一开始就建对。
                var themes = new StationTheme[data.stations == null ? 0 : data.stations.Length];
                for (int i = 0; i < themes.Length; i++) themes[i] = (StationTheme)data.stations[i].theme;

                Map = MapProvider.CreateMap(WorldRoot, themes);
                Placement.InitializeSlots();
                if (!Map.Validate(out string error)) throw new InvalidOperationException(error);

                generation++;
                Mode = (JourneyMode)data.player.mode;
                LevelIndex = data.levelIndex;
                runId = data.runId;
                Session.Begin(Map.stations.Length, .1, Config.materials);
                Traveler.gameObject.SetActive(true);
                Traveler.RestorePosition(Map, data.player.progress01);

                if (!SaveRestore.Apply(this, data)) throw new InvalidOperationException("存档与当前地图结构不匹配。");

                CurrentTileIndex = Mathf.Clamp(data.player.currentTileIndex, 0, Mathf.Max(0, CurrentStation.tileCount - 1));
                Activity = (JourneyActivity)data.player.activity;
                environmentSeconds = 0; autosaveSeconds = 0;
                lastTime = Time.realtimeSinceStartupAsDouble;
                Camera.ShowStation(CurrentStation);

                ComputeOfflineSteps(data, out int offlineSteps, out double offlineSeconds);
                if (offlineSteps <= 0)
                {
                    ResumeInteraction();
                }
                else
                {
                    StartOfflineSettle(offlineSteps, offlineSeconds);
                }
                return true;
            }
            catch (Exception error)
            {
                ReturnToStart(); UI.Notify("读档失败：" + error.Message); Debug.LogException(error);
                return false;
            }
        }

        static bool IsPlayableSave(SaveData data)
        {
            if (data?.session == null || data.player == null ||
                data.player.activity == (int)JourneyActivity.Complete) return false;
            var phase = (JourneyPhase)data.session.phase;
            return phase == JourneyPhase.AtStation || phase == JourneyPhase.Moving ||
                phase == JourneyPhase.AtSpawn;
        }

        // 离线推进沿用在线语义：只推进环境，不动狐狸、不结算采集、不替玩家做决定。
        // 狐狸仍能接雨——CollectRainAtCurrentTile 只认它当前所在的格子。
        // 这里只算「要推进多少步」，实际推进交给 SettleOffline 分帧做。
        void ComputeOfflineSteps(SaveData data, out int steps, out double seconds)
        {
            steps = 0;
            seconds = 0;
            if (Map == null) return;
            seconds = (DateTime.UtcNow - SaveSystem.SavedAtUtc(data)).TotalSeconds;
            if (seconds <= 0) return;
            double cap = Math.Max(0f, offlineAdvanceCapHours) * 3600.0;
            steps = (int)Math.Min(Math.Min(seconds, cap), Math.Max(0, offlineStepBudget));
        }

        void StartOfflineSettle(int steps, double seconds)
        {
            IsSettlingOffline = true;
            OfflineProgress01 = 0f;
            offlineSummary = "离线 " + DescribeSpan(seconds) + " · 生态已继续" + (seconds > steps ? "（按上限结算）" : "");
            StartCoroutine(SettleOffline(steps));
        }

        // 每帧只花 offlineStepBudgetMsPerFrame 毫秒推进，剩下的时间留给画面刷新。
        // 之前是一次性跑完 7200 步约 3 秒，窗口看起来像卡死；现在同样是这些步数，
        // 但玩家看到的是进度在走。
        IEnumerator SettleOffline(int total)
        {
            var watch = new System.Diagnostics.Stopwatch();
            float budgetMs = Mathf.Max(.5f, offlineStepBudgetMsPerFrame);
            int done = 0;
            while (done < total && Map != null)
            {
                watch.Restart();
                while (done < total && watch.Elapsed.TotalMilliseconds < budgetMs)
                {
                    Map.StepEnvironment(CollectRainAtCurrentTile);
                    done++;
                }
                OfflineProgress01 = total <= 0 ? 1f : (float)done / total;
                yield return null;
            }
            environmentSeconds = 0;
            OfflineProgress01 = 1f;
            IsSettlingOffline = false;
            Map?.CompletePlantFades();
            UI.Notify(offlineSummary);
            ResumeInteraction();
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
                    if (Session.Phase == JourneyPhase.AtStation) Advance();
                    break;
            }
        }

        void ResumeBetweenStations()
        {
            int target = Session.StationIndex + 1;
            if (Map == null || target >= Map.stations.Length) { Activity = JourneyActivity.None; return; }
            int currentGeneration = generation;
            float targetProgress = Map.stations[target].stopProgress;
            float duration = RemainingMoveSeconds(targetProgress,
                CurrentStation.TileCenter(CurrentStation.tileCount - 1), StationTransitionSeconds);
            Traveler.MoveToProgress(targetProgress, duration, () =>
            {
                if (generation != currentGeneration || !Session.Arrive()) return;
                CurrentTileIndex = 0; Camera.ShowStation(CurrentStation);
                Activity = JourneyActivity.Walking;
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
            if (archive == null) { UI.Notify("无法读取旅途存档：" + SaveError); return; }
            StartLevel(mode);
        }
        void StartLevel(JourneyMode mode)
        {
            JourneyMap next = null;
            try
            {
                next = MapProvider.CreateMap(WorldRoot);
                if (!next.Validate(out string error)) throw new InvalidOperationException(error);
                var previous = Map;
                Traveler.transform.SetParent(null, true);
                Session.Begin(next.stations.Length, .1, Config.materials,
                    carried: archive.nextLevelIndex > 1 ? archive.carryInventory : null);
                Map = next; next = null;
                Placement.InitializeSlots();
                if (previous != null) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
                generation++; Mode = mode; LevelIndex = archive.nextLevelIndex;
                runId = Guid.NewGuid().ToString("N");
                CurrentTileIndex = 0; Activity = JourneyActivity.Walking;
                environmentSeconds = 0; autosaveSeconds = 0;
                IsAppreciating = false; IsEnteringAppreciation = false; IsPreviewing = false;
                IsFinalized = false; HasAppreciated = false; SaveError = null;
                Camera.ResetCamera();
                Traveler.ResetFinalJump(); Traveler.gameObject.SetActive(true); Traveler.PlaceOnRoute(Map);
                if (Traveler.spriteRenderer != null) Traveler.spriteRenderer.enabled = true;
                Camera.ShowStation(CurrentStation);
                lastTime = Time.realtimeSinceStartupAsDouble;
                Placement.ClearSelection();
                // Spawn at the first tile and start the first timed step immediately.
                CollectOnTileEntry();
                Advance();
            }
            catch (Exception error)
            {
                if (next != null) Destroy(next.gameObject);
                UI.Notify("无法开始旅途：" + error.Message); Debug.LogException(error);
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
            float targetProgress = Map.ProgressAtDistance(CurrentStation.TileCenter(nextTile));
            float duration = RemainingMoveSeconds(targetProgress, CurrentStation.TileCenter(CurrentTileIndex), MoveSeconds);
            Traveler.MoveToProgress(targetProgress, duration, () =>
            {
                if (generation != currentGeneration) return;
                CurrentTileIndex = nextTile;
                bool awaitingPlacement = Placement.CurrentSlot != null;
                Activity = awaitingPlacement ? JourneyActivity.AwaitingPlacement : JourneyActivity.Walking;
                CollectOnTileEntry();
                if (awaitingPlacement)
                {
                    Placement.OnTravelerArrived();
                }
                else Advance();
            });
        }
        float RemainingMoveSeconds(float targetProgress, float startDistance, float fullDuration)
        {
            float targetDistance = Map.RouteDistanceAtProgress(targetProgress);
            float remaining = Mathf.Max(0f, targetDistance - Map.RouteDistanceAtProgress(Traveler.Progress01));
            float total = Mathf.Max(.001f, targetDistance - startDistance);
            return Mathf.Max(.001f, fullDuration * Mathf.Clamp01(remaining / total));
        }
        public void CompletePlacementVisit()
        {
            if (Activity != JourneyActivity.AwaitingPlacement || IsPreviewing) return;
            UI.ClosePlacementPrompt(false);
            Activity = JourneyActivity.Walking;
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
                TryFinalize();
                return;
            }
            Activity = JourneyActivity.BetweenStations;
            Traveler.MoveToProgress(Map.stations[Session.StationIndex + 1].stopProgress, StationTransitionSeconds, () =>
            {
                if (generation != currentGeneration || !Session.Arrive()) return;
                CurrentTileIndex = 0; Camera.ShowStation(CurrentStation);
                Activity = JourneyActivity.Walking;
                CollectOnTileEntry(); Advance();
            });
        }
        public void TryFinalize()
        {
            if (Activity != JourneyActivity.Complete || IsFinalized || Map == null || archive == null) return;
            try
            {
                archive = JourneyArchiveStore.SaveCompleted(archive, runId, LevelIndex,
                    Mode, Session, Map, Config.materials);
                IsFinalized = true; SaveError = null;
                Session.CompleteFinalization();
                Camera.ShowStation(CurrentStation);
                try { SaveSystem.Delete(); }
                catch (Exception error) { Debug.LogWarning("已保存完成作品，但清理旧进度档失败：" + error.Message); }
            }
            catch (Exception error)
            {
                SaveError = error.Message;
                Debug.LogException(error);
            }
        }
        public void EnterAppreciation()
        {
            if (!IsFinalized || IsAppreciating || IsEnteringAppreciation || Map == null) return;
            Map.CompletePlantFades();
            appreciationEntrance = StartCoroutine(EnterAppreciationRoutine());
        }
        public void PreviewPanorama()
        {
            if (Map == null || IsDebugMode || IsBrowsingPanorama || IsEnteringAppreciation ||
                Activity == JourneyActivity.Complete || UI.IsModalOpen) return;
            if (Placement.EditingSlot != null) Placement.CancelEditor();
            Map.CompletePlantFades();
            IsPreviewing = true;
            Traveler.Paused = true;
            if (Traveler.spriteRenderer != null) Traveler.spriteRenderer.enabled = false;
            Camera.ShowScrollAtStart();
        }
        public void ExitPreview()
        {
            if (!IsPreviewing || Map == null) return;
            IsPreviewing = false;
            Traveler.Paused = false;
            if (Traveler.spriteRenderer != null) Traveler.spriteRenderer.enabled = true;
            Camera.ShowStation(CurrentStation);
            lastTime = Time.realtimeSinceStartupAsDouble;
        }
        IEnumerator EnterAppreciationRoutine()
        {
            IsEnteringAppreciation = true;
            float elapsed = 0f;
            float height = Camera.WorldCamera.orthographicSize * 2.5f;
            while (elapsed < .55f)
            {
                elapsed += Time.unscaledDeltaTime;
                Traveler.SetFinalJump(Mathf.Sin(Mathf.Clamp01(elapsed / .55f) * Mathf.PI * .5f) * height);
                yield return null;
            }
            Traveler.gameObject.SetActive(false);
            Camera.ShowScrollAtStart();
            IsAppreciating = true; IsEnteringAppreciation = false; HasAppreciated = true;
            appreciationEntrance = null;
        }
        public void ExitAppreciation()
        {
            if (!IsAppreciating || Map == null) return;
            IsAppreciating = false;
            Traveler.ResetFinalJump(); Traveler.gameObject.SetActive(true);
            Camera.ShowStation(CurrentStation);
        }
        public void ContinueJourney()
        {
            if (!IsFinalized || IsAppreciating || IsEnteringAppreciation ||
                Session.Phase != JourneyPhase.Completed) return;
            StartLevel(Mode);
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
            if (appreciationEntrance != null) StopCoroutine(appreciationEntrance);
            appreciationEntrance = null; IsAppreciating = false; IsEnteringAppreciation = false; IsPreviewing = false;
            IsFinalized = false; HasAppreciated = false; runId = null;
            IsDebugMode = false; DebugStepCount = 0; environmentSeconds = 0;
            Activity = JourneyActivity.None; CurrentTileIndex = 0;
            Traveler.CancelMovement(); Camera.ResetCamera(); Placement.ClearSelection();
            if (Traveler.spriteRenderer != null) Traveler.spriteRenderer.enabled = true;
            Traveler.ResetFinalJump();
            Traveler.transform.SetParent(null, true); Traveler.gameObject.SetActive(false);
            if (Map != null) { Map.gameObject.SetActive(false); Destroy(Map.gameObject); Map = null; }
            Session.Reset(); lastTime = Time.realtimeSinceStartupAsDouble; UI.Notify("");
        }
        void OnDestroy() { if (Traveler != null) Traveler.CancelMovement(); }
    }
}
