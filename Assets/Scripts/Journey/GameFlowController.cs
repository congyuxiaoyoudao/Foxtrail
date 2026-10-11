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
            if (Map == null || UI.IsModalOpen || Activity == JourneyActivity.Complete) return;
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
            Camera.RestoreJourneyView();
        }
        void OnDestroy() { if (Traveler != null) Traveler.CancelMovement(); }
    }
}
