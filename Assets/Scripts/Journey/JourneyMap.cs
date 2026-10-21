using System.Collections.Generic;
using UnityEngine;

namespace Hiking.Journey
{
    public class JourneyMap : MonoBehaviour
    {
        public const float FoxHeightAboveRoad = .5f;
        public Station[] stations;
        public Transform finishPoint;
        public RingMapSettings sourceSettings;
        [System.NonSerialized] public Mesh[] generatedMeshes;
        public float RouteLength { get; set; }
        public int SimulationStep { get; private set; }
        // 读档：步数决定下一次天气检查与植物成熟计时的相位，必须原样恢复。
        public void RestoreSimulationStep(int step) => SimulationStep = Mathf.Max(0, step);
        public RainWeatherSystem Weather { get; } = new RainWeatherSystem();
        public float ViewWidth => RouteLength / stations.Length;
        readonly List<MeshRenderer> routeRenderers = new List<MeshRenderer>();
        readonly List<Mesh> routeMeshes = new List<Mesh>();
        readonly List<Transform> routeObjects = new List<Transform>();
        readonly List<Vector3> objectCoordinates = new List<Vector3>();
        readonly HashSet<Transform> nearRouteObjects = new HashSet<Transform>();
        readonly List<Renderer> landscapes = new List<Renderer>();
        readonly List<Station> landscapeStations = new List<Station>();
        readonly List<Material> landscapeMaterials = new List<Material>();
        readonly List<Mesh> landscapeMeshes = new List<Mesh>();
        RainStreakVisual[] rainVisuals;

        public void InitializeRainVisual()
        {
            rainVisuals = new RainStreakVisual[stations.Length];
            for (int station = 0; station < stations.Length; station++)
            {
                var rain = new GameObject("RainStreaks_Station_" + station);
                rain.transform.SetParent(transform, false);
                var visual = rain.AddComponent<RainStreakVisual>();
                visual.Configure(this, sourceSettings, station);
                RegisterRouteObject(rain.transform, new Vector3(0, 0, 1f));
                rain.SetActive(false);
                rainVisuals[station] = visual;
            }
        }

        public void SyncRainVisuals()
        {
            if (rainVisuals == null) return;
            foreach (var visual in rainVisuals)
                if (visual != null) visual.SyncWeather(Weather);
        }

        public void RegisterLandscape(Renderer renderer, Material material = null, Mesh mesh = null)
        {
            landscapes.Add(renderer);
            landscapeStations.Add(renderer.GetComponentInParent<Station>());
            landscapeMaterials.Add(material);
            landscapeMeshes.Add(mesh);
        }
        public void FrameLandscapes(Camera camera, int blendFromStation = -1,
            float blendProgress = 0f, float blendCenter = 0f,
            int currentStationIndex = -1, bool allowRainFilter = true)
        {
            bool visible = camera != null && camera.orthographic;
            bool blending = visible && stations != null && blendFromStation >= 0 &&
                blendFromStation < stations.Length - 1;
            Station outgoing = blending ? stations[blendFromStation] : null;
            Station incoming = blending ? stations[blendFromStation + 1] : null;
            if (visible)
            {
                RefreshRouteMeshPositions();
                RefreshNearRouteObjects();
            }
            for (int index = 0; index < landscapes.Count; index++)
            {
                var landscape = landscapes[index];
                if (landscape == null) continue;
                var station = landscapeStations[index];
                bool isIncoming = blending && station == incoming;
                bool participates = blending && (station == outgoing || isIncoming);
                if (visible) landscape.transform.rotation = camera.transform.rotation;
                var layer = landscape.GetComponent<SceneLandscapeLayer>();
                if (layer != null) layer.SetTransition(participates, blendCenter,
                    isIncoming ? blendProgress : 1f, isIncoming ? 200 : 0);
                var fill = landscape.GetComponent<SceneThemeFill>();
                if (fill != null) fill.SetTransition(participates, blendCenter,
                    isIncoming ? blendProgress : 1f, isIncoming ? 200 : 0);
                landscape.enabled = visible && (!blending || participates);
            }
            if (visible && stations != null)
                foreach (var station in stations)
                    if (station != null) station.FramePlants(camera);
            if (rainVisuals != null)
                foreach (var visual in rainVisuals)
                    if (visual != null) visual.FaceCamera(camera, visible, currentStationIndex, allowRainFilter);
        }

        // Mesh vertices are authored in route coordinates. Runtime meshes are owned by this map.
        public void RegisterRouteMesh(Mesh mesh)
        {
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i].x -= RouteLength * .5f;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            routeMeshes.Add(mesh);
            generatedMeshes = routeMeshes.ToArray();
            var original = System.Array.Find(GetComponentsInChildren<MeshFilter>(), f => f.sharedMesh == mesh);
            var renderer = original.GetComponent<MeshRenderer>();
            float groundY = (sourceSettings?.sceneLayout?.nearSharedY ?? 0f) +
                (sourceSettings?.sceneLayout?.groundY ?? 0f);
            renderer.transform.localPosition = new Vector3(0f, groundY, 0f);
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_Color", Color.white);
            renderer.SetPropertyBlock(tint);
            routeRenderers.Add(renderer);
        }
        void RefreshRouteMeshPositions()
        {
            float groundY = (sourceSettings?.sceneLayout?.nearSharedY ?? 0f) +
                (sourceSettings?.sceneLayout?.groundY ?? 0f);
            foreach (var renderer in routeRenderers)
            {
                if (renderer == null) continue;
                var position = renderer.transform.localPosition;
                position.y = groundY;
                renderer.transform.localPosition = position;
            }
        }
        public void SetRouteMeshColor(Mesh mesh, Color color)
        {
            int index = routeMeshes.IndexOf(mesh);
            if (index < 0) return;
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            mesh.colors = colors;
        }
        public void SetRouteMeshCovered(Mesh mesh)
        {
            int index = routeMeshes.IndexOf(mesh);
            if (index < 0) return;
            routeRenderers[index].enabled = false;
        }
        Vector3 RoutePoint(Vector3 route) => new Vector3(route.x - RouteLength * .5f, route.y, route.z);
        public void RegisterRouteObject(Transform target, Vector3 routeCoordinates)
        {
            routeObjects.Add(target); objectCoordinates.Add(routeCoordinates);
            target.position = transform.TransformPoint(RoutePoint(routeCoordinates));
        }
        public void MarkNearRouteObject(Transform target)
        {
            int index = routeObjects.IndexOf(target);
            if (index < 0) return;
            nearRouteObjects.Add(target);
            UpdateRouteObject(target, objectCoordinates[index]);
        }
        void RefreshNearRouteObjects()
        {
            foreach (var target in nearRouteObjects)
            {
                int index = routeObjects.IndexOf(target);
                if (index >= 0) UpdateRouteObject(target, objectCoordinates[index]);
            }
        }
        Vector3 VisibleObjectRoute(int index)
        {
            Vector3 route = objectCoordinates[index];
            if (nearRouteObjects.Contains(routeObjects[index]))
            {
                route.y += NearOffsetAtDistance(route.x);
                if (routeObjects[index].GetComponent<PlacementSignVisual>() != null)
                    route.y += sourceSettings?.sceneLayout?.placementSignY ?? 0f;
            }
            return route;
        }
        public float NearOffsetAtDistance(float distance) => sourceSettings?.sceneLayout?.nearSharedY ?? 0f;
        public bool IsPondAt(int globalTile)
        {
            if (stations == null || globalTile < 0 ||
                globalTile >= stations.Length * RingMapSettings.TilesPerStation) return false;
            var station = stations[globalTile / RingMapSettings.TilesPerStation];
            int tile = globalTile % RingMapSettings.TilesPerStation;
            return station != null && station.Tiles != null && tile < station.Tiles.Length &&
                station.Tiles[tile].IsPuddle;
        }
        public void SyncPondVisuals()
        {
            if (stations == null) return;
            for (int i = 0; i < stations.Length; i++)
                if (stations[i] != null) stations[i].SyncPondVisuals(this, i);
        }
        public void UpdateRouteObject(Transform target, Vector3 routeCoordinates)
        {
            int index = routeObjects.IndexOf(target);
            if (index < 0) return;
            objectCoordinates[index] = routeCoordinates;
            Vector3 visibleRoute = VisibleObjectRoute(index);
            target.position = transform.TransformPoint(RoutePoint(visibleRoute));
        }
        public void UnregisterRouteObject(Transform target)
        {
            int index = routeObjects.IndexOf(target);
            if (index < 0) return;
            routeObjects.RemoveAt(index);
            objectCoordinates.RemoveAt(index);
            nearRouteObjects.Remove(target);
        }
        // One step represents one simulated second in both the journey and debug map.
        public void StepEnvironment()
        {
            SimulationStep++;
            int total = stations.Length * RingMapSettings.TilesPerStation;
            var tiles = new TileState[total];
            var beforeFlow = new TileSnapshot[total];
            for (int index = 0; index < total; index++)
            {
                tiles[index] = TileAt(index);
                beforeFlow[index] = tiles[index].Snapshot();
            }
            TileNeighborFlow.Exchange(tiles, sourceSettings.temperatureFlowPerStep,
                sourceSettings.soilWaterFlowPerStep);
            for (int index = 0; index < total; index++) tiles[index].Infiltrate(sourceSettings.infiltrationFactor);
            TileNeighborFlow.ExchangeSurface(tiles, sourceSettings.surfaceWaterFlowPerStep,
                sourceSettings.waterUnitsPerHeight);
            for (int station = 0; station < stations.Length; station++)
                stations[station].StepTiles(SimulationStep, this,
                    beforeFlow, station * RingMapSettings.TilesPerStation);
            var emissions = new List<(int tile, string plantId)>();
            for (int index = 0; index < total; index++)
            {
                var tile = stations[index / RingMapSettings.TilesPerStation].Tiles[index % RingMapSettings.TilesPerStation];
                foreach (var plant in tile.NaturalPlants)
                {
                    var definition = sourceSettings.plantCatalog?.Find(plant.plantId);
                    bool mature = definition != null && definition.StageCount > 0 &&
                        plant.stageIndex >= definition.StageCount - 1 &&
                        plant.life > definition.stages[definition.StageCount - 1].minLife;
                    if (!mature || SimulationStep < plant.nextSeedStep) continue;
                    plant.nextSeedStep = SimulationStep + Mathf.Max(1, sourceSettings.matureSeedInterval);
                    emissions.Add((index, plant.plantId));
                }
            }
            // Deliver after all tiles have updated, so new seeds cannot germinate on the emission step.
            foreach (var emission in emissions)
            {
                int left = emission.tile - 1, right = emission.tile + 1;
                foreach (int target in new[] { left, right })
                    if (target >= 0 && target < total &&
                        CanNaturallySpread(target, emission.plantId, total) &&
                        TileAt(target).TryAddSeed(emission.plantId, SimulationStep,
                            sourceSettings.plantCatalog))
                        stations[target / RingMapSettings.TilesPerStation].RefreshTile(target % RingMapSettings.TilesPerStation, false);
            }
            Weather.Advance(tiles, sourceSettings, SimulationStep);
            SyncRainVisuals();
            for (int index = 0; index < total; index++)
            {
                tiles[index].RecordStepChange(beforeFlow[index]);
                stations[index / RingMapSettings.TilesPerStation].RefreshTile(index % RingMapSettings.TilesPerStation, false);
            }
            SyncPondVisuals();
            for (int station = 0; station < stations.Length; station++)
                if (Weather.RainAppliedAtStation(station)) stations[station].SyncPlantMarkers(this, true);
        }

        bool CanNaturallySpread(int target, string plantId, int total)
        {
            if (TileAt(target).HasPlantOrSeed(plantId)) return false;
            int stationStart = target / RingMapSettings.TilesPerStation * RingMapSettings.TilesPerStation;
            int occupied = 0;
            for (int tile = stationStart; tile < stationStart + RingMapSettings.TilesPerStation; tile++)
                if (TileAt(tile).HasPlantOrSeed(plantId)) occupied++;
            if (occupied >= Mathf.Clamp(sourceSettings.maxNaturalSpeciesTilesPerStation,
                    1, RingMapSettings.TilesPerStation)) return false;

            int run = 1;
            for (int tile = target - 1; tile >= 0 && TileAt(tile).HasPlantOrSeed(plantId); tile--) run++;
            for (int tile = target + 1; tile < total && TileAt(tile).HasPlantOrSeed(plantId); tile++) run++;
            return run <= Mathf.Clamp(sourceSettings.maxNaturalSpeciesRun,
                1, RingMapSettings.TilesPerStation);
        }
        public void CompletePlantFades()
        {
            if (stations == null) return;
            foreach (var station in stations)
                if (station != null) station.CompletePlantFades(this);
        }
        TileState TileAt(int index) => stations[index / RingMapSettings.TilesPerStation].Tiles[index % RingMapSettings.TilesPerStation];
        public float TileWidth => stations[0].TileWidth;
        public float ViewStartDistance(float progress) => Mathf.Clamp(RouteDistanceAtProgress(progress) - ViewWidth * .5f, 0, RouteLength - ViewWidth);
        public float RouteDistanceAtProgress(float progress) => Mathf.Lerp(TileWidth * .5f, RouteLength - TileWidth * .5f, Mathf.Clamp01(progress));
        public float ProgressAtDistance(float distance) => Mathf.InverseLerp(TileWidth * .5f, RouteLength - TileWidth * .5f, distance);

        public Vector3 ProgressPoint(float progress)
        {
            var route = new Vector3(RouteDistanceAtProgress(progress),
                sourceSettings.ringThickness * .5f + FoxHeightAboveRoad, 0);
            var layout = sourceSettings?.sceneLayout;
            route.y += (layout?.nearSharedY ?? 0f) + (layout?.foxY ?? 0f);
            return transform.TransformPoint(RoutePoint(route));
        }

        void OnDestroy()
        {
            foreach (var material in landscapeMaterials)
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            foreach (var mesh in landscapeMeshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (generatedMeshes == null) return;
            foreach (var mesh in generatedMeshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }

        public bool Validate(out string error)
        {
            error = null;
            if (stations == null || stations.Length < 1 || finishPoint == null)
                error = "地图需要站点和一个旅途终点。";
            else
            {
                var ids = new HashSet<string>();
                var stationIds = new HashSet<string>();
                foreach (var station in stations)
                {
                    if (station == null || station.stopPoint == null || station.slots == null ||
                        station.slots.Length != 3 || string.IsNullOrEmpty(station.stationId) ||
                        !stationIds.Add(station.stationId) || station.cameraRange.y <= station.cameraRange.x)
                    { error = "站点需要唯一 ID、驻足点、三个投放点和有效镜头范围。"; break; }
                    foreach (var slot in station.slots)
                        if (slot == null || string.IsNullOrEmpty(slot.slotId) || !ids.Add(slot.slotId))
                        { error = "投放点缺失或 ID 重复。"; break; }
                    if (station.outgoingWaypoints != null)
                        foreach (var point in station.outgoingWaypoints)
                            if (point == null) error = "行走路径存在空引用。";
                    if (error != null) break;
                }
            }
            return error == null;
        }

        public Vector3[] RouteFrom(int index)
        {
            var points = new List<Vector3>();
            var via = stations[index].outgoingWaypoints;
            if (via != null) foreach (var point in via) points.Add(point.position);
            points.Add(index + 1 < stations.Length ? stations[index + 1].stopPoint.position : finishPoint.position);
            return points.ToArray();
        }
    }

    // A camera-facing curtain of falling translucent streaks. It is one route object so a
    // 15-tile rain event remains continuous when its segment crosses a station boundary.
    public sealed class RainStreakVisual : MonoBehaviour
    {
        JourneyMap map;
        RingMapSettings settings;
        Transform[] streaks;
        SpriteRenderer[] streakRenderers;
        float[] streakOpacityVariation;
        float[] speeds;
        float width;
        float height;
        bool raining;
        int stationIndex;
        int displayedStart = -1;
        Sprite ownedSprite;
        Texture2D ownedTexture;
        Transform overcastTransform;
        SpriteRenderer overcastRenderer;
        float overcastOpacity;
        Color lastStreakColor;
        float lastStreakOpacity;
        float lastStreakThickness;

        public void Configure(JourneyMap owner, RingMapSettings configuration, int station)
        {
            map = owner; settings = configuration; stationIndex = station;
            width = map.TileWidth * RainWeatherSystem.RainTileCount;
            height = Mathf.Max(.5f, settings.rainVisualHeight);
            int count = RainWeatherSystem.RainTileCount * Mathf.Clamp(settings.rainStreaksPerTile, 1, 16);
            streaks = new Transform[count];
            streakRenderers = new SpriteRenderer[count];
            streakOpacityVariation = new float[count];
            speeds = new float[count];
            Sprite sprite = settings.fallbackPlantSprite;
            if (sprite == null)
            {
                ownedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                ownedTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                ownedTexture.Apply();
                ownedSprite = Sprite.Create(ownedTexture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2);
                sprite = ownedSprite;
            }
            var random = new System.Random(1515);
            float spriteWidth = Mathf.Max(.001f, sprite.bounds.size.x);
            float spriteHeight = Mathf.Max(.001f, sprite.bounds.size.y);
            // This screen-sized world sprite shades the scene while the Canvas stays readable.
            var overcast = new GameObject("RainOvercastFilter");
            overcast.transform.SetParent(map.transform, false);
            overcastTransform = overcast.transform;
            overcastRenderer = overcast.AddComponent<SpriteRenderer>();
            overcastRenderer.sprite = sprite;
            overcastRenderer.sortingOrder = 1000;
            overcastRenderer.enabled = false;
            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject("RainStreak_" + i.ToString("000"));
                obj.transform.SetParent(transform, false);
                float x = ((float)random.NextDouble() - .5f) * width;
                float y = ((float)random.NextDouble() - .5f) * height;
                obj.transform.localPosition = new Vector3(x, y, 0);
                obj.transform.localRotation = Quaternion.Euler(0, 0, -12);
                float length = Mathf.Lerp(.2f, .38f, (float)random.NextDouble());
                obj.transform.localScale = new Vector3(
                    Mathf.Max(.001f, settings.rainStreakThickness) / spriteWidth,
                    length / spriteHeight, 1);
                var renderer = obj.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                streakOpacityVariation[i] = Mathf.Lerp(.65f, 1f, (float)random.NextDouble());
                Color streakColor = settings.rainStreakColor;
                renderer.color = new Color(streakColor.r, streakColor.g, streakColor.b,
                    Mathf.Clamp01(settings.rainStreakOpacity) * streakOpacityVariation[i]);
                renderer.sortingOrder = -500;
                streaks[i] = obj.transform;
                streakRenderers[i] = renderer;
                speeds[i] = Mathf.Max(.1f, settings.rainStreakSpeed) *
                    Mathf.Lerp(.75f, 1.25f, (float)random.NextDouble());
            }
            lastStreakColor = settings.rainStreakColor;
            lastStreakOpacity = settings.rainStreakOpacity;
            lastStreakThickness = settings.rainStreakThickness;
        }

        public void SyncWeather(RainWeatherSystem weather)
        {
            raining = weather != null && (weather.IsRainingAtStation(stationIndex) ||
                weather.RainAppliedAtStation(stationIndex));
            if (!raining) { gameObject.SetActive(false); return; }
            int start = stationIndex * RingMapSettings.TilesPerStation;
            if (displayedStart != start)
            {
                displayedStart = start;
                PositionRain();
            }
        }

        void PositionRain()
        {
            if (displayedStart < 0 || map.stations == null) return;
            int lastTile = displayedStart + RainWeatherSystem.RainTileCount - 1;
            int firstStation = displayedStart / RingMapSettings.TilesPerStation;
            int lastStation = lastTile / RingMapSettings.TilesPerStation;
            float first = map.stations[firstStation].TileCenter(displayedStart % RingMapSettings.TilesPerStation);
            float last = map.stations[lastStation].TileCenter(lastTile % RingMapSettings.TilesPerStation);
            map.UpdateRouteObject(transform, new Vector3((first + last) * .5f,
                settings.ringThickness * .5f + height * .42f, 1f));
        }

        void RefreshVisualHeight()
        {
            float nextHeight = Mathf.Max(.5f, settings.rainVisualHeight);
            if (Mathf.Approximately(height, nextHeight)) return;
            float ratio = nextHeight / height;
            foreach (var streak in streaks)
            {
                Vector3 position = streak.localPosition;
                position.y *= ratio;
                streak.localPosition = position;
            }
            height = nextHeight;
            PositionRain();
        }

        public void FaceCamera(Camera camera, bool visible, int currentStationIndex,
            bool allowRainFilter)
        {
            bool show = raining && visible && camera != null;
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            if (show)
            {
                RefreshVisualHeight();
                transform.rotation = camera.transform.rotation;
                RefreshStreakAppearance();
            }
            if (overcastRenderer == null) return;
            bool canRenderFilter = visible && camera != null && allowRainFilter;
            bool showFilter = show && canRenderFilter && stationIndex == currentStationIndex;
            float target = showFilter ? Mathf.Clamp(settings.rainOvercastOpacity, 0, .8f) : 0f;
            overcastOpacity = canRenderFilter ? Mathf.MoveTowards(overcastOpacity, target,
                Mathf.Max(Time.unscaledDeltaTime, 1f / 60f) * .55f) : 0f;
            overcastRenderer.enabled = canRenderFilter && overcastOpacity > .001f;
            if (!overcastRenderer.enabled) return;
            overcastTransform.SetPositionAndRotation(camera.transform.position +
                camera.transform.forward * (camera.nearClipPlane + .5f), camera.transform.rotation);
            Vector2 spriteSize = overcastRenderer.sprite.bounds.size;
            float viewHeight = camera.orthographicSize * 2f;
            overcastTransform.localScale = new Vector3(
                viewHeight * camera.aspect / Mathf.Max(.001f, spriteSize.x),
                viewHeight / Mathf.Max(.001f, spriteSize.y), 1f);
            Color overcastColor = settings.rainOvercastColor;
            overcastRenderer.color = new Color(overcastColor.r, overcastColor.g,
                overcastColor.b, overcastOpacity);
        }

        void RefreshStreakAppearance()
        {
            Color color = settings.rainStreakColor;
            float opacity = settings.rainStreakOpacity;
            float thickness = settings.rainStreakThickness;
            bool colorChanged = color != lastStreakColor || opacity != lastStreakOpacity;
            bool thicknessChanged = thickness != lastStreakThickness;
            if (!colorChanged && !thicknessChanged) return;
            lastStreakColor = color;
            lastStreakOpacity = opacity;
            lastStreakThickness = thickness;
            for (int i = 0; i < streakRenderers.Length; i++)
            {
                if (colorChanged)
                    streakRenderers[i].color = new Color(color.r, color.g, color.b,
                        Mathf.Clamp01(opacity) * streakOpacityVariation[i]);
                if (thicknessChanged)
                {
                    Vector3 scale = streaks[i].localScale;
                    scale.x = Mathf.Max(.001f, thickness) /
                        Mathf.Max(.001f, streakRenderers[i].sprite.bounds.size.x);
                    streaks[i].localScale = scale;
                }
            }
        }

        void Update()
        {
            if (streaks == null) return;
            float delta = Time.unscaledDeltaTime;
            for (int i = 0; i < streaks.Length; i++)
            {
                Vector3 position = streaks[i].localPosition;
                position.y -= speeds[i] * delta;
                position.x -= speeds[i] * delta * .21f;
                if (position.y < -height * .5f - .25f)
                {
                    position.y += height + .5f;
                    position.x += width * .63f;
                }
                if (position.x < -width * .5f - .25f) position.x += width + .5f;
                if (position.x > width * .5f + .25f) position.x -= width + .5f;
                streaks[i].localPosition = position;
            }
        }

        void OnDestroy()
        {
            if (overcastTransform != null)
            {
                if (Application.isPlaying) Destroy(overcastTransform.gameObject);
                else DestroyImmediate(overcastTransform.gameObject);
            }
            if (ownedSprite != null)
            {
                if (Application.isPlaying) Destroy(ownedSprite); else DestroyImmediate(ownedSprite);
            }
            if (ownedTexture != null)
            {
                if (Application.isPlaying) Destroy(ownedTexture); else DestroyImmediate(ownedTexture);
            }
        }
    }

    // Each station owns its vapor check and rain event. Event IDs remain unique for rain collection.
    public sealed class RainWeatherSystem
    {
        public const int RainTileCount = RingMapSettings.TilesPerStation;
        StationRainData[] states = System.Array.Empty<StationRainData>();
        int nextEventId;
        public int RainStartTile { get { for (int i = 0; i < states.Length; i++)
            if (IsRainingAtStation(i)) return i * RainTileCount; return -1; } }
        public int RainEndTile => RainStartTile < 0 ? -1 : RainStartTile + RainTileCount - 1;
        public int RemainingSeconds => RainStartTile < 0 ? 0 : states[RainStartTile / RainTileCount].remainingSeconds;
        public float RemainingRain => RainStartTile < 0 ? 0 : states[RainStartTile / RainTileCount].remainingRain;
        public float EventRain => RainStartTile < 0 ? 0 : states[RainStartTile / RainTileCount].eventRain;
        public int EventId => nextEventId;
        public int LastCheckStep { get; private set; }
        public float LastCheckVapor { get; private set; }
        public bool LastCheckSucceeded { get; private set; }
        public bool IsRaining => RainStartTile >= 0;
        public bool RainAppliedThisStep { get; private set; }
        public bool CoversStation(int stationIndex) => IsRainingAtStation(stationIndex);
        public bool IsRainingAtStation(int stationIndex) => stationIndex >= 0 &&
            stationIndex < states.Length && states[stationIndex].remainingSeconds > 0;
        public bool IsRainingAtTile(int globalTile) =>
            globalTile >= 0 && IsRainingAtStation(globalTile / RainTileCount);
        public bool RainAppliedAtStation(int stationIndex) => stationIndex >= 0 &&
            stationIndex < states.Length && states[stationIndex].rainAppliedThisStep;
        public int EventIdAtStation(int stationIndex) => stationIndex >= 0 &&
            stationIndex < states.Length ? states[stationIndex].eventId : -1;
        public StationRainData[] CaptureStations()
        {
            var result = new StationRainData[states.Length];
            for (int i = 0; i < result.Length; i++) result[i] = Copy(states[i]);
            return result;
        }
        static StationRainData Copy(StationRainData s) => new StationRainData
        {
            remainingSeconds = s.remainingSeconds, remainingRain = s.remainingRain,
            eventRain = s.eventRain, eventId = s.eventId, lastCheckStep = s.lastCheckStep,
            lastCheckVapor = s.lastCheckVapor, lastCheckSucceeded = s.lastCheckSucceeded,
            rainAppliedThisStep = s.rainAppliedThisStep
        };
        void EnsureStations(int count)
        {
            if (states.Length == count) return;
            var next = new StationRainData[count];
            for (int i = 0; i < count; i++)
                next[i] = i < states.Length && states[i] != null ? states[i] : new StationRainData();
            states = next;
        }

        public void Restore(WeatherData data)
        {
            if (data == null) return;
            nextEventId = Mathf.Max(nextEventId, data.eventId);
            if (data.stations != null && data.stations.Length > 0)
            {
                states = new StationRainData[data.stations.Length];
                for (int i = 0; i < states.Length; i++)
                {
                    states[i] = data.stations[i] == null ? new StationRainData() : Copy(data.stations[i]);
                    nextEventId = Mathf.Max(nextEventId, states[i].eventId);
                }
            }
            else if (data.rainStartTile >= 0)
            {
                int station = data.rainStartTile / RainTileCount;
                EnsureStations(station + 1);
                states[station] = new StationRainData
                {
                    remainingSeconds = Mathf.Max(0, data.remainingSeconds),
                    remainingRain = Mathf.Max(0, data.remainingRain), eventRain = Mathf.Max(0, data.eventRain),
                    eventId = data.eventId, lastCheckStep = data.lastCheckStep,
                    lastCheckVapor = data.lastCheckVapor,
                    lastCheckSucceeded = data.lastCheckSucceeded,
                    rainAppliedThisStep = data.rainAppliedThisStep
                };
                nextEventId = Mathf.Max(nextEventId, data.eventId);
            }
            LastCheckStep = data.lastCheckStep;
            LastCheckVapor = Mathf.Max(0, data.lastCheckVapor);
            LastCheckSucceeded = data.lastCheckSucceeded;
            RainAppliedThisStep = data.rainAppliedThisStep;
        }

        public void Advance(TileState[] tiles, RingMapSettings settings, int currentStep)
        {
            RainAppliedThisStep = false;
            if (tiles == null || tiles.Length < RainTileCount || settings == null) return;
            int count = tiles.Length / RainTileCount;
            EnsureStations(count);
            int interval = Mathf.Max(1, settings.rainCheckInterval);
            for (int station = 0; station < count; station++)
            {
                var state = states[station];
                state.rainAppliedThisStep = false;
                int start = station * RainTileCount;
                if (!HasSimulatedWindow(tiles, start)) continue;
                // Give each station its own starting phase instead of checking all stations together.
                if (currentStep == 1 && state.lastCheckStep == 0)
                    state.lastCheckStep = -UnityEngine.Random.Range(0, interval);
                if (currentStep > 0 && currentStep - state.lastCheckStep >= interval &&
                    state.remainingSeconds <= 0)
                {
                    state.lastCheckStep = currentStep;
                    LastCheckStep = currentStep;
                    TryStartRain(tiles, start, settings);
                }
                if (state.remainingSeconds <= 0) continue;
                float thisSecond = state.remainingRain / state.remainingSeconds;
                float perTile = thisSecond / RainTileCount;
                for (int i = start; i < start + RainTileCount; i++)
                    tiles[i].AddRain(perTile);
                state.rainAppliedThisStep = true;
                RainAppliedThisStep = true;
                state.remainingRain = Mathf.Max(0, state.remainingRain - thisSecond);
                state.remainingSeconds--;
            }
        }

        public bool TryStartRain(TileState[] tiles, int start, RingMapSettings settings)
        {
            if (settings == null || start < 0 || start % RainTileCount != 0 ||
                !HasSimulatedWindow(tiles, start)) return false;
            int station = start / RainTileCount;
            EnsureStations(tiles.Length / RainTileCount);
            var state = states[station];
            if (state.remainingSeconds > 0) return false;
            float vapor = 0;
            for (int i = start; i < start + RainTileCount; i++) vapor += tiles[i].PendingVapor;
            state.lastCheckVapor = LastCheckVapor = vapor;
            state.lastCheckSucceeded = LastCheckSucceeded = false;
            if (vapor < Mathf.Max(0, settings.rainVaporThreshold)) return false;
            float rain = Mathf.Min(Mathf.Max(0, settings.maxRainPerEvent),
                vapor * Mathf.Clamp01(settings.rainReturnRatio));
            if (rain <= 0) return false;
            float deducted = 0;
            for (int i = start; i < start + RainTileCount; i++)
                deducted += tiles[i].ConsumeVapor(rain * tiles[i].PendingVapor / vapor);
            state.eventRain = deducted;
            state.remainingRain = deducted;
            state.remainingSeconds = Mathf.Max(1, settings.rainDuration);
            state.eventId = ++nextEventId;
            state.lastCheckSucceeded = LastCheckSucceeded = true;
            return true;
        }

        static bool HasSimulatedWindow(TileState[] tiles, int start)
        {
            if (tiles == null || start < 0 || start + RainTileCount > tiles.Length) return false;
            for (int i = start; i < start + RainTileCount; i++)
                if (tiles[i] == null) return false;
            return true;
        }
    }
}
