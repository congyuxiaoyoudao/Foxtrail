using System.Collections.Generic;
using UnityEngine;

namespace Hiking.Journey
{
    public class JourneyMap : MonoBehaviour
    {
        public Station[] stations;
        public Transform finishPoint;
        public Bounds panoramaBounds;
        [Header("圆环地图（固定地图配置）")]
        public bool circular;
        [Min(5)] public float radius = 18;
        public float finishAngle = 360;
        public RingMapSettings sourceSettings;
        public float BlockAngle => 360f / (stations.Length + 1);
        [System.NonSerialized] public Mesh[] generatedMeshes;
        public float RouteLength { get; set; }
        public int SimulationStep { get; private set; }
        public RainWeatherSystem Weather { get; } = new RainWeatherSystem();
        public float Bend { get; private set; }
        public int AssembledPatchCount { get; private set; }
        public bool ShowingRing { get; private set; }
        public float ViewWidth => RouteLength / stations.Length;
        public float PanoramaScale => Mathf.Max(1, sourceSettings.panoramaScale);
        public Vector3 RingPoint(Vector3 route) => RouteGeometry.Point(route, RouteLength, 1) * PanoramaScale;
        readonly List<Mesh> ringMeshes = new List<Mesh>();
        readonly List<MeshRenderer> ringRenderers = new List<MeshRenderer>();
        readonly List<MeshRenderer> stripRenderers = new List<MeshRenderer>();
        readonly List<bool> coveredRouteMeshes = new List<bool>();
        readonly List<bool> assembled = new List<bool>();
        readonly List<Mesh> routeMeshes = new List<Mesh>();
        readonly List<Vector3[]> routeVertices = new List<Vector3[]>();
        readonly List<Vector3[]> displayVertices = new List<Vector3[]>();
        readonly List<Transform> routeObjects = new List<Transform>();
        readonly List<Vector3> objectCoordinates = new List<Vector3>();
        readonly HashSet<Transform> nearRouteObjects = new HashSet<Transform>();
        readonly List<Renderer> landscapes = new List<Renderer>();
        readonly List<Material> landscapeMaterials = new List<Material>();
        readonly List<Mesh> landscapeMeshes = new List<Mesh>();
        RainStreakVisual rainVisual;

        public void InitializeRainVisual()
        {
            var rain = new GameObject("RainStreaks_15Tiles");
            rain.transform.SetParent(transform, false);
            rainVisual = rain.AddComponent<RainStreakVisual>();
            rainVisual.Configure(this, sourceSettings);
            RegisterRouteObject(rain.transform, new Vector3(0, 0, 1f));
            rain.SetActive(false);
        }

        public void RegisterLandscape(Renderer renderer, Material material = null, Mesh mesh = null)
        {
            landscapes.Add(renderer);
            landscapeMaterials.Add(material);
            landscapeMeshes.Add(mesh);
        }
        public void FrameLandscapes(Camera camera)
        {
            bool visible = camera != null && camera.orthographic && !ShowingRing;
            if (visible) RefreshNearRouteObjects();
            foreach (var landscape in landscapes)
            {
                if (landscape == null) continue;
                landscape.enabled = visible;
                if (visible) landscape.transform.rotation = camera.transform.rotation;
            }
            if (visible && stations != null)
                foreach (var station in stations)
                    if (station != null) station.FramePlants(camera);
            if (rainVisual != null) rainVisual.FaceCamera(camera, visible);
        }

        // Mesh vertices are authored in route coordinates. Runtime meshes are owned by this map.
        public void RegisterRouteMesh(Mesh mesh)
        {
            routeMeshes.Add(mesh); routeVertices.Add(mesh.vertices);
            displayVertices.Add(new Vector3[mesh.vertexCount]);
            generatedMeshes = routeMeshes.ToArray();
            var original = System.Array.Find(GetComponentsInChildren<MeshFilter>(), f => f.sharedMesh == mesh);
            var stripRenderer = original.GetComponent<MeshRenderer>();
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_Color", Color.white);
            stripRenderer.SetPropertyBlock(tint);
            stripRenderers.Add(stripRenderer);
            coveredRouteMeshes.Add(false);
            var obj = new GameObject(original.name + "_ArchivedRing");
            obj.transform.SetParent(transform, false);
            var ring = Instantiate(mesh); ring.name = mesh.name + "_Ring";
            obj.AddComponent<MeshFilter>().sharedMesh = ring;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = original.GetComponent<MeshRenderer>().sharedMaterial;
            renderer.SetPropertyBlock(tint);
            renderer.enabled = false;
            ringMeshes.Add(ring); ringRenderers.Add(renderer); assembled.Add(false);
        }
        public void SetRouteMeshColor(Mesh mesh, Color color)
        {
            int index = routeMeshes.IndexOf(mesh);
            if (index < 0) return;
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            mesh.colors = colors;
            ringMeshes[index].colors = colors;
        }
        public void SetRouteMeshCovered(Mesh mesh)
        {
            int index = routeMeshes.IndexOf(mesh);
            if (index < 0) return;
            coveredRouteMeshes[index] = true;
            stripRenderers[index].enabled = false;
        }
        // Retire only patches that have fully left the local viewport. Their route IDs stay unchanged.
        public void ArchiveBefore(float distance)
        {
            for (int i = 0; i < routeMeshes.Count; i++)
            {
                if (assembled[i]) continue;
                float end = 0;
                foreach (var v in routeVertices[i]) end = Mathf.Max(end, v.x);
                if (end > distance) continue;
                var vertices = new Vector3[routeVertices[i].Length];
                for (int v = 0; v < vertices.Length; v++) vertices[v] = RingPoint(routeVertices[i][v]);
                ringMeshes[i].vertices = vertices; ringMeshes[i].RecalculateNormals(); ringMeshes[i].RecalculateBounds();
                assembled[i] = true; AssembledPatchCount++;
            }
        }
        public void ShowCompletedRing()
        {
            ArchiveBefore(RouteLength + .01f);
            ShowingRing = true; Bend = 1; circular = true;
            for (int i = 0; i < ringRenderers.Count; i++) { stripRenderers[i].enabled = false; ringRenderers[i].enabled = true; }
            for (int i = 0; i < routeObjects.Count; i++)
            {
                Vector3 route = VisibleObjectRoute(i);
                routeObjects[i].position = transform.TransformPoint(RingPoint(route));
                routeObjects[i].rotation = transform.rotation * RouteGeometry.Rotation(route.x, RouteLength, 1);
            }
            panoramaBounds = new Bounds(transform.TransformPoint(new Vector3(0, 0, -radius) * PanoramaScale), new Vector3(2 * radius + 2, 4, 2 * radius + 2) * PanoramaScale);
        }
        public void ShowJourney()
        {
            ShowingRing = false;
            for (int i = 0; i < ringRenderers.Count; i++)
            {
                ringRenderers[i].enabled = false;
                stripRenderers[i].enabled = !coveredRouteMeshes[i];
            }
            SetBend(0);
        }
        public void RegisterRouteObject(Transform target, Vector3 routeCoordinates)
        {
            routeObjects.Add(target); objectCoordinates.Add(routeCoordinates);
            target.position = transform.TransformPoint(RouteGeometry.Point(routeCoordinates, RouteLength, Bend));
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
            if (nearRouteObjects.Contains(routeObjects[index]) && !ShowingRing)
            {
                Vector2 offset = NearOffsetAtDistance(route.x);
                route.x += offset.x;
                route.y += offset.y;
            }
            return route;
        }
        public Vector2 NearOffsetAtDistance(float distance)
        {
            if (stations == null || stations.Length == 0 || sourceSettings == null ||
                sourceSettings.sceneThemes == null) return Vector2.zero;
            int index = Mathf.Clamp(Mathf.FloorToInt(distance / Mathf.Max(.001f, ViewWidth)),
                0, stations.Length - 1);
            var layers = sourceSettings.sceneThemes.For(stations[index].theme);
            return layers != null && layers.nearGround != null ? layers.nearOffset : Vector2.zero;
        }
        public void UpdateRouteObject(Transform target, Vector3 routeCoordinates)
        {
            int index = routeObjects.IndexOf(target);
            if (index < 0) return;
            objectCoordinates[index] = routeCoordinates;
            Vector3 visibleRoute = VisibleObjectRoute(index);
            target.position = transform.TransformPoint(ShowingRing ? RingPoint(routeCoordinates) :
                RouteGeometry.Point(visibleRoute, RouteLength, Bend));
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
        public void StepEnvironment(System.Func<int, int, float, float> collectRain = null)
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
                    bool mature = definition != null && definition.StageCount >= 3 && plant.stageIndex >= 2 &&
                        plant.life >= definition.stages[2].minLife &&
                        definition.HighestSuitableStage(tile.SoilContent, tile.SoilHumidity * 100f) >= 2 &&
                        (tile.Depth == WaterDepth.Dry || tile.Depth == WaterDepth.Medium && definition.floodTolerant);
                    plant.matureSteps = mature ? plant.matureSteps + 1 : 0;
                    if (plant.matureSteps < 30) continue;
                    plant.matureSteps = 0;
                    emissions.Add((index, plant.plantId));
                }
            }
            // Deliver after all tiles have updated, so new seeds cannot germinate on the emission step.
            foreach (var emission in emissions)
            {
                int left = emission.tile - 1, right = emission.tile + 1;
                bool canLeft = left >= 0 && TileAt(left).FreePlantCapacity > 0;
                bool canRight = right < total && TileAt(right).FreePlantCapacity > 0;
                if (!canLeft && !canRight) continue;
                int target = canLeft && canRight ? (UnityEngine.Random.Range(0, 2) == 0 ? left : right) :
                    canLeft ? left : right;
                if (TileAt(target).TryAddSeed(emission.plantId, SimulationStep))
                    stations[target / RingMapSettings.TilesPerStation].RefreshTile(target % RingMapSettings.TilesPerStation);
            }
            Weather.Advance(tiles, sourceSettings, SimulationStep, collectRain);
            if (rainVisual != null) rainVisual.SyncWeather(Weather);
            for (int index = 0; index < total; index++)
            {
                tiles[index].RecordStepChange(beforeFlow[index]);
                stations[index / RingMapSettings.TilesPerStation].RefreshTile(index % RingMapSettings.TilesPerStation);
            }
            if (Weather.RainAppliedThisStep)
                for (int station = 0; station < stations.Length; station++) stations[station].SyncPlantMarkers(this);
        }
        TileState TileAt(int index) => stations[index / RingMapSettings.TilesPerStation].Tiles[index % RingMapSettings.TilesPerStation];
        public float TileWidth => stations[0].TileWidth;
        public float ViewStartDistance(float progress) => Mathf.Clamp(RouteDistanceAtProgress(progress) - ViewWidth * .5f, 0, RouteLength - ViewWidth);
        public float RouteDistanceAtProgress(float progress) => Mathf.Lerp(TileWidth * .5f, RouteLength - TileWidth * .5f, Mathf.Clamp01(progress));
        public float ProgressAtDistance(float distance) => Mathf.InverseLerp(TileWidth * .5f, RouteLength - TileWidth * .5f, distance);

        public Vector3 ProgressPoint(float progress)
        {
            var route = new Vector3(RouteDistanceAtProgress(progress), sourceSettings.ringThickness * .5f + .5f, 0);
            if (!ShowingRing)
            {
                Vector2 near = NearOffsetAtDistance(route.x);
                Vector2 fox = sourceSettings.foxPositionOffset;
                route.x += near.x + fox.x;
                route.y += near.y + fox.y;
            }
            return transform.TransformPoint(ShowingRing ? RingPoint(route) : RouteGeometry.Point(route, RouteLength, Bend));
        }

        public void SetBend(float bend)
        {
            Bend = Mathf.Clamp01(bend); circular = Bend >= .9999f;
            var bounds = new Bounds(); bool first = true;
            for (int m = 0; m < routeMeshes.Count; m++)
            {
                for (int v = 0; v < routeVertices[m].Length; v++)
                {
                    var point = RouteGeometry.Point(routeVertices[m][v], RouteLength, Bend);
                    displayVertices[m][v] = point;
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
                routeMeshes[m].vertices = displayVertices[m];
                routeMeshes[m].RecalculateNormals(); routeMeshes[m].RecalculateBounds();
            }
            for (int i = 0; i < routeObjects.Count; i++)
            {
                if (routeObjects[i] == null) continue;
                Vector3 route = VisibleObjectRoute(i);
                routeObjects[i].position = transform.TransformPoint(RouteGeometry.Point(route, RouteLength, Bend));
                routeObjects[i].rotation = transform.rotation * RouteGeometry.Rotation(route.x, RouteLength, Bend);
            }
            bounds.Expand(new Vector3(1, 4, 1));
            panoramaBounds = new Bounds(transform.TransformPoint(bounds.center), bounds.size);
        }

        void OnDestroy()
        {
            foreach (var material in landscapeMaterials)
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            foreach (var mesh in landscapeMeshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            foreach (var mesh in ringMeshes) if (mesh != null) Destroy(mesh);
            if (generatedMeshes == null) return;
            foreach (var mesh in generatedMeshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }

        public Vector3 LocalPoint(float angle) => RingJourneyMath.Point(angle, radius);

        public bool IsVisible(PlacementSlot slot)
        {
            // 在3D模式下，所有槽位始终可见（由相机视锥体决定）
            return true;
        }

        public bool Validate(out string error)
        {
            error = null;
            if (circular && (radius <= 0 || finishAngle != 360))
            { error = "圆环半径必须为正，旅途终点必须为 360 度。"; return false; }
            if (stations == null || stations.Length < 1 || finishPoint == null)
                error = "地图需要站点和一个观景终点。";
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
                if (error == null && circular)
                {
                    // 跳过驻足点位置验证，在3D模式下由用户自由调整
                }
                if (panoramaBounds.size.x <= 0 || panoramaBounds.size.y <= 0)
                    error = "全景范围无效。";
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
        float[] speeds;
        float width;
        float height;
        bool raining;
        int displayedStart = -1;
        Sprite ownedSprite;
        Texture2D ownedTexture;

        public void Configure(JourneyMap owner, RingMapSettings configuration)
        {
            map = owner; settings = configuration;
            width = map.TileWidth * RainWeatherSystem.RainTileCount;
            height = Mathf.Max(.5f, settings.rainVisualHeight);
            int count = RainWeatherSystem.RainTileCount * Mathf.Clamp(settings.rainStreaksPerTile, 1, 16);
            streaks = new Transform[count]; speeds = new float[count];
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
            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject("RainStreak_" + i.ToString("000"));
                obj.transform.SetParent(transform, false);
                float x = ((float)random.NextDouble() - .5f) * width;
                float y = ((float)random.NextDouble() - .5f) * height;
                obj.transform.localPosition = new Vector3(x, y, 0);
                obj.transform.localRotation = Quaternion.Euler(0, 0, -12);
                float length = Mathf.Lerp(.2f, .38f, (float)random.NextDouble());
                obj.transform.localScale = new Vector3(.018f / spriteWidth, length / spriteHeight, 1);
                var renderer = obj.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(.78f, .92f, 1f,
                    Mathf.Clamp01(settings.rainStreakOpacity) * Mathf.Lerp(.65f, 1f, (float)random.NextDouble()));
                renderer.sortingOrder = -500;
                streaks[i] = obj.transform;
                speeds[i] = Mathf.Max(.1f, settings.rainStreakSpeed) *
                    Mathf.Lerp(.75f, 1.25f, (float)random.NextDouble());
            }
        }

        public void SyncWeather(RainWeatherSystem weather)
        {
            raining = weather != null && (weather.IsRaining || weather.RainAppliedThisStep);
            if (!raining) { gameObject.SetActive(false); return; }
            if (displayedStart != weather.RainStartTile)
            {
                displayedStart = weather.RainStartTile;
                int firstStation = displayedStart / RingMapSettings.TilesPerStation;
                int lastStation = weather.RainEndTile / RingMapSettings.TilesPerStation;
                float first = map.stations[firstStation].TileCenter(displayedStart % RingMapSettings.TilesPerStation);
                float last = map.stations[lastStation].TileCenter(weather.RainEndTile % RingMapSettings.TilesPerStation);
                map.UpdateRouteObject(transform, new Vector3((first + last) * .5f,
                    settings.ringThickness * .5f + height * .42f, 1f));
            }
        }

        public void FaceCamera(Camera camera, bool visible)
        {
            bool show = raining && visible && camera != null;
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            if (show) transform.rotation = camera.transform.rotation;
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

    // Keeps rain scoped to one contiguous route segment, including station boundaries.
    public sealed class RainWeatherSystem
    {
        public const int RainTileCount = 15;
        public int RainStartTile { get; private set; } = -1;
        public int RainEndTile => RainStartTile < 0 ? -1 : RainStartTile + RainTileCount - 1;
        public int RemainingSeconds { get; private set; }
        public float RemainingRain { get; private set; }
        public float EventRain { get; private set; }
        public int EventId { get; private set; }
        public int LastCheckStep { get; private set; }
        public float LastCheckVapor { get; private set; }
        public bool LastCheckSucceeded { get; private set; }
        public bool IsRaining => RemainingSeconds > 0;
        public bool RainAppliedThisStep { get; private set; }

        public void Advance(TileState[] tiles, RingMapSettings settings, int currentStep,
            System.Func<int, int, float, float> collectRain = null)
        {
            RainAppliedThisStep = false;
            if (tiles == null || tiles.Length < RainTileCount || settings == null) return;
            int interval = Mathf.Max(1, settings.rainCheckInterval);
            if (currentStep > 0 && currentStep % interval == 0 && !IsRaining)
            {
                LastCheckStep = currentStep;
                var eligibleStarts = new List<int>();
                for (int start = 0; start + RainTileCount <= tiles.Length; start++)
                    if (HasSimulatedWindow(tiles, start)) eligibleStarts.Add(start);
                LastCheckVapor = 0;
                LastCheckSucceeded = false;
                if (eligibleStarts.Count > 0)
                    TryStartRain(tiles, eligibleStarts[UnityEngine.Random.Range(0, eligibleStarts.Count)], settings);
            }
            if (!IsRaining) return;
            // An unloaded segment must never receive rain without a live tile state.
            if (!HasSimulatedWindow(tiles, RainStartTile)) return;
            float thisSecond = RemainingRain / RemainingSeconds;
            float perTile = thisSecond / RainTileCount;
            for (int i = RainStartTile; i <= RainEndTile; i++)
            {
                float collected = Mathf.Clamp(collectRain?.Invoke(EventId, i, perTile) ?? 0, 0, perTile);
                tiles[i].AddRain(perTile - collected);
            }
            RainAppliedThisStep = true;
            RemainingRain = Mathf.Max(0, RemainingRain - thisSecond);
            RemainingSeconds--;
        }

        public bool TryStartRain(TileState[] tiles, int start, RingMapSettings settings)
        {
            if (settings == null || IsRaining || !HasSimulatedWindow(tiles, start)) return false;
            float vapor = 0;
            for (int i = start; i < start + RainTileCount; i++) vapor += tiles[i].PendingVapor;
            LastCheckVapor = vapor;
            LastCheckSucceeded = false;
            if (vapor < Mathf.Max(0, settings.rainVaporThreshold)) return false;
            float rain = Mathf.Min(Mathf.Max(0, settings.maxRainPerEvent),
                vapor * Mathf.Clamp01(settings.rainReturnRatio));
            if (rain <= 0) return false;
            float deducted = 0;
            for (int i = start; i < start + RainTileCount; i++)
                deducted += tiles[i].ConsumeVapor(rain * tiles[i].PendingVapor / vapor);
            EventRain = deducted;
            RemainingRain = EventRain;
            RemainingSeconds = Mathf.Max(1, settings.rainDuration);
            RainStartTile = start;
            EventId++;
            LastCheckSucceeded = true;
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
