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
        public bool IsDebugMode { get; private set; }
        public int DebugStepCount { get; private set; }
        public Station CurrentStation => Map != null ? Map.stations[Session.StationIndex] : null;
        void Start()
        {
            Application.runInBackground = true;
            Placement.Initialize(this); Camera.Initialize(this); UI.Initialize(this);
            ReturnToStart();
            try { archive = JourneyArchiveStore.Load(); }
            catch (Exception error) { SaveError = error.Message; Debug.LogException(error); }
        }
        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float delta = (float)(now - lastTime); lastTime = now;
            if (IsDebugMode || IsPreviewing || Activity == JourneyActivity.Complete) return;
            Session.Tick(delta);
            if (Map == null || UI.IsModalOpen) return;
            environmentSeconds += Mathf.Max(0, delta);
            while (environmentSeconds >= 1f)
            {
                Map.StepEnvironment(CollectRainAtCurrentTile);
                environmentSeconds -= 1f;
            }
        }
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
                if (previous != null) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
                generation++; Mode = mode; LevelIndex = archive.nextLevelIndex;
                runId = Guid.NewGuid().ToString("N");
                CurrentTileIndex = 0; environmentSeconds = 0;
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
            if (IsPreviewing || Activity != JourneyActivity.AwaitingPlacement) return;
            UI.ClosePlacementPrompt(false); Advance();
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
            appreciationEntrance = StartCoroutine(EnterAppreciationRoutine());
        }
        public void PreviewPanorama()
        {
            if (Map == null || IsDebugMode || IsBrowsingPanorama || IsEnteringAppreciation ||
                Activity == JourneyActivity.Complete || UI.IsModalOpen) return;
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
