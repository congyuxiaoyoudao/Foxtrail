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
            landscapeStations.Add(renderer.GetComponentInParent<Station>());
            landscapeMaterials.Add(material);
            landscapeMeshes.Add(mesh);
        }
        public void FrameLandscapes(Camera camera, int blendFromStation = -1,
            float blendProgress = 0f, float blendCenter = 0f)
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
            if (rainVisual != null) rainVisual.FaceCamera(camera, visible);
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
                    bool mature = definition != null && definition.StageCount >= 3 && plant.stageIndex >= 2 && plant.life > definition.stages[2].minLife;
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
                    if (target >= 0 && target < total && TileAt(target).TryAddSeed(emission.plantId, SimulationStep))
                        stations[target / RingMapSettings.TilesPerStation].RefreshTile(target % RingMapSettings.TilesPerStation, false);
            }
            Weather.Advance(tiles, sourceSettings, SimulationStep, collectRain);
            if (rainVisual != null) rainVisual.SyncWeather(Weather);
            for (int index = 0; index < total; index++)
            {
                tiles[index].RecordStepChange(beforeFlow[index]);
                stations[index / RingMapSettings.TilesPerStation].RefreshTile(index % RingMapSettings.TilesPerStation, false);
            }
            SyncPondVisuals();
            if (Weather.RainAppliedThisStep)
                for (int station = 0; station < stations.Length; station++) stations[station].SyncPlantMarkers(this, true);
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
        float[] speeds;
        float width;
        float height;
        bool raining;
        int displayedStart = -1;
        Sprite ownedSprite;
        Texture2D ownedTexture;
        Transform overcastTransform;
        SpriteRenderer overcastRenderer;
        float overcastOpacity;

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
            if (overcastRenderer == null) return;
            float target = show ? Mathf.Clamp(settings.rainOvercastOpacity, 0, .4f) : 0f;
            overcastOpacity = visible ? Mathf.MoveTowards(overcastOpacity, target,
                Mathf.Max(Time.unscaledDeltaTime, 1f / 60f) * .55f) : 0f;
            overcastRenderer.enabled = visible && camera != null && overcastOpacity > .001f;
            if (!overcastRenderer.enabled) return;
            overcastTransform.SetPositionAndRotation(camera.transform.position +
                camera.transform.forward * (camera.nearClipPlane + .5f), camera.transform.rotation);
            Vector2 spriteSize = overcastRenderer.sprite.bounds.size;
            float viewHeight = camera.orthographicSize * 2f;
            overcastTransform.localScale = new Vector3(
                viewHeight * camera.aspect / Mathf.Max(.001f, spriteSize.x),
                viewHeight / Mathf.Max(.001f, spriteSize.y), 1f);
            overcastRenderer.color = new Color(.18f, .23f, .29f, overcastOpacity);
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

        // 读档：进行中的这场雨要原样恢复（覆盖范围、剩余雨量、剩余时间、事件 id），
        // 读档后不能重新抽取这场雨，也不能重新发放完整降水。
        public void Restore(WeatherData data)
        {
            if (data == null) return;
            RainStartTile = data.rainStartTile;
            RemainingSeconds = Mathf.Max(0, data.remainingSeconds);
            RemainingRain = Mathf.Max(0, data.remainingRain);
            EventRain = Mathf.Max(0, data.eventRain);
            EventId = data.eventId;
            LastCheckStep = data.lastCheckStep;
            LastCheckVapor = Mathf.Max(0, data.lastCheckVapor);
            LastCheckSucceeded = data.lastCheckSucceeded;
            RainAppliedThisStep = data.rainAppliedThisStep;
        }

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
