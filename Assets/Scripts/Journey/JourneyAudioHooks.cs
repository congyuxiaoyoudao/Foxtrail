using UnityEngine;

namespace Hiking.Journey
{
    public sealed class JourneyAudioHooks : MonoBehaviour
    {
        public GameFlowController Flow;
        float lastPlacementSoundTime = -10f;
        bool rainPlaying;
        string footstepCueId;

        void Awake()
        {
            if (Flow == null) Flow = GetComponent<GameFlowController>();
        }

        void Start()
        {
            if (Flow != null && Flow.Placement != null)
                Flow.Placement.PlacementCommitted += OnPlacementCommitted;
        }

        void OnDestroy()
        {
            if (Flow != null && Flow.Placement != null)
                Flow.Placement.PlacementCommitted -= OnPlacementCommitted;
            GameAudio.Instance?.StopFootstep();
        }

        void Update()
        {
            if (Flow == null) return;
            TickFootsteps();
            TickRain();
        }

        void TickFootsteps()
        {
            var audio = GameAudio.Instance;
            var traveler = Flow.Traveler;
            var map = Flow.Map;
            bool walking = audio != null && audio.Catalog != null && traveler != null && map != null &&
                traveler.IsMoving && !traveler.Paused && !Flow.IsPreviewing &&
                !Flow.IsSettlingOffline && Flow.Activity != JourneyActivity.Complete;
            if (!walking)
            {
                if (footstepCueId != null)
                {
                    audio?.StopFootstep();
                    footstepCueId = null;
                }
                return;
            }

            int totalTiles = Mathf.Max(1, map.stations.Length * RingMapSettings.TilesPerStation);
            int globalTile = Mathf.Clamp(Mathf.FloorToInt(
                map.RouteDistanceAtProgress(traveler.Progress01) / Mathf.Max(.001f, map.TileWidth)),
                0, totalTiles - 1);
            int stationIndex = Mathf.Clamp(globalTile / RingMapSettings.TilesPerStation,
                0, map.stations.Length - 1);
            var station = map.stations[stationIndex];
            int tileIndex = Mathf.Clamp(globalTile % RingMapSettings.TilesPerStation,
                0, station.Tiles.Length - 1);
            var tile = station.Tiles[tileIndex];
            var weather = map.Weather;
            bool wetGround = tile.IsPuddle || weather.IsRaining &&
                globalTile >= weather.RainStartTile && globalTile <= weather.RainEndTile;

            string cueId = wetGround ? "footstep_wet" :
                station.MiddleTheme == StationTheme.Snow ? "footstep_snow" :
                station.MiddleTheme == StationTheme.Desert ? "footstep_desert" :
                "footstep_plain";
            if (cueId == footstepCueId) return;
            footstepCueId = cueId;
            audio.StartFootstep(cueId);
        }

        void TickRain()
        {
            bool shouldPlay = Flow.Map != null && Flow.Map.Weather.IsRaining &&
                Flow.Activity != JourneyActivity.Complete;
            if (shouldPlay == rainPlaying) return;
            rainPlaying = shouldPlay;
            if (shouldPlay) GameAudio.Instance?.PlayAmbience("rain_loop");
            else GameAudio.Instance?.StopAmbience();
        }

        void OnPlacementCommitted(PlacementRequest request)
        {
            if (Time.unscaledTime - lastPlacementSoundTime < .2f) return;
            lastPlacementSoundTime = Time.unscaledTime;
            GameAudio.Instance?.Play("putdown");
        }
    }
}
