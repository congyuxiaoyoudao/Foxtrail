using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hiking.Journey
{
    public enum StationTheme { Snow, Grass, Desert }

    public class Station : MonoBehaviour
    {
        public string stationId;
        public StationTheme theme;
        public int tileCount;
        public static bool IsTransitionTile(int tile) => tile % 4 == 3;
        public TileState[] Tiles { get; private set; }
        // 读档：整批换成存档里的地块。Tiles 是只读属性，只能在类内部赋值。
        public void RestoreTiles(TileState[] tiles) => Tiles = tiles;
        // 读档前必须清掉植物标记：plantMarkers 以植物对象为键，而读档会整批换成新对象，
        // 旧键不会被 SyncPlantMarkers 判成死亡，结果屏幕上会留下"幽灵植物"。
        public void ClearPlantMarkers(JourneyMap map)
        {
            foreach (var visual in plantVisuals.Values) ClearPlantFade(map, visual);
            plantVisuals.Clear();
            foreach (var pair in plantMarkers)
            {
                if (pair.Value == null) continue;
                DestroyPlantRenderer(map, pair.Value);
            }
            plantMarkers.Clear();
        }
        TextMesh[] moistureLabels;
        readonly Dictionary<PlantInstance, SpriteRenderer> plantMarkers = new Dictionary<PlantInstance, SpriteRenderer>();
        sealed class FadingPlantSprite
        {
            public SpriteRenderer renderer;
            public float startAlpha;
            public float startedAt;
        }
        sealed class PlantVisualState
        {
            public int stageIndex;
            public Color targetColor;
            public float fadeStartedAt = -1f;
            public readonly List<FadingPlantSprite> outgoing = new List<FadingPlantSprite>();
        }
        readonly Dictionary<PlantInstance, PlantVisualState> plantVisuals = new Dictionary<PlantInstance, PlantVisualState>();
        readonly Dictionary<Sprite, Vector2> plantVisibleY = new Dictionary<Sprite, Vector2>();
        Mesh[] patchMeshes;
        SpriteRenderer[] patchVisuals;
        SpriteRenderer[] patchWaterOverlays;
        Color[] patchColors;
        SpriteRenderer[] pondMarkers;
        bool[] pondWasWet;
        float[] pondDryStart;
        RingMapSettings mapSettings;
        JourneyMap ownerMap;
        bool debugLabels;
        public void InitializeTiles(RingMapSettings settings)
        {
            mapSettings = settings;
            Tiles = new TileState[tileCount]; moistureLabels = new TextMesh[tileCount];
            patchMeshes = new Mesh[tileCount]; patchColors = new Color[tileCount];
            patchVisuals = new SpriteRenderer[tileCount];
            patchWaterOverlays = new SpriteRenderer[tileCount];
            pondMarkers = new SpriteRenderer[tileCount];
            pondWasWet = new bool[tileCount];
            pondDryStart = new float[tileCount];
            for (int i = 0; i < tileCount; i++) pondDryStart[i] = -1f;
            int basin = settings.createStationBasin ? UnityEngine.Random.Range(0,3)*4+2 : -10;
            for (int i = 0; i < tileCount; i++) Tiles[i] = settings.CreateTile(theme, i == basin, Mathf.Abs(i-basin) == 1);
        }
        public float MoistureAt(int tile) => Tiles[tile].SurfaceWater;
        public void RefreshTile(int tile, bool syncPond = true)
        {
            RefreshMoisture(tile);
            RefreshWaterVisual(tile);
            if (syncPond && ownerMap != null) ownerMap.SyncPondVisuals();
        }
        public void SetPatchMesh(int tile, Mesh mesh, Color baseColor, JourneyMap map)
        {
            patchMeshes[tile] = mesh; patchColors[tile] = baseColor; ownerMap = map;
            RefreshWaterVisual(tile);
        }
        public void SetPatchVisual(int tile, SpriteRenderer visual, SpriteRenderer waterOverlay)
        {
            patchVisuals[tile] = visual;
            patchWaterOverlays[tile] = waterOverlay;
            RefreshWaterVisual(tile);
        }
        void RefreshWaterVisual(int tile)
        {
            if (ownerMap == null || patchMeshes[tile] == null) return;
            var pond = mapSettings.sceneThemes != null ? mapSettings.sceneThemes.For(theme)?.pond : null;
            float blend = pond != null && pond.HasWetSprites ? 0f :
                Tiles[tile].Depth == WaterDepth.Deep ? .72f : Tiles[tile].Depth == WaterDepth.Medium ? .43f : 0f;
            Color baseColor = Tiles[tile].Category == theme ? patchColors[tile] : mapSettings.ThemeColor(Tiles[tile].Category);
            ownerMap.SetRouteMeshColor(patchMeshes[tile], Color.Lerp(baseColor, new Color(.18f, .58f, .92f), blend));
            if (patchVisuals[tile] != null)
            {
                var layers = mapSettings.sceneThemes != null ? mapSettings.sceneThemes.For(Tiles[tile].Category) : null;
                var sprite = layers != null && layers.nearGround != null ?
                    layers.nearGround : mapSettings.fallbackPlantSprite;
                if (sprite != null)
                {
                    var visual = patchVisuals[tile]; visual.sprite = sprite;
                    float sx = TileWidth / Mathf.Max(.001f, sprite.bounds.size.x);
                    float height = layers?.nearGround != null ? sprite.bounds.size.y * sx :
                        mapSettings.GroundPlaceholderHeight(TileWidth);
                    float sy = height / Mathf.Max(.001f, sprite.bounds.size.y);
                    visual.transform.localScale = new Vector3(sx,sy,1);
                    var layout = mapSettings.sceneLayout;
                    ownerMap.UpdateRouteObject(visual.transform,new Vector3(TileCenter(tile),
                        mapSettings.ringThickness*.5f+(layout != null ? layout.groundY : 0f)-height*.5f,-.05f));
                    visual.color = layers?.nearGround != null ? Color.white :
                        Tiles[tile].Category == theme ? patchColors[tile] :
                        mapSettings.ThemeColor(Tiles[tile].Category);
                }
            }
            if (patchWaterOverlays[tile] != null)
                patchWaterOverlays[tile].color = new Color(.18f, .58f, .92f, blend);
            else if (patchVisuals[tile] != null)
                patchVisuals[tile].color = Color.Lerp(Color.white, new Color(.38f, .72f, 1f), blend);
        }
        public void SyncPondVisuals(JourneyMap map, int stationIndex)
        {
            if (Tiles == null || pondMarkers == null || mapSettings == null) return;
            var layers = mapSettings.sceneThemes != null ? mapSettings.sceneThemes.For(theme) : null;
            var pond = layers?.pond;
            if (pond == null || !pond.HasWetSprites) return;
            for (int tile = 0; tile < tileCount; tile++)
            {
                bool wet = Tiles[tile].IsPuddle;
                int globalTile = stationIndex * RingMapSettings.TilesPerStation + tile;
                if (!wet && !pondWasWet[tile]) continue;
                Sprite sprite = wet ? pond.WetFor(map.IsPondAt(globalTile - 1),
                    map.IsPondAt(globalTile + 1), globalTile) : pond.drySingle;
                var marker = pondMarkers[tile];
                if (marker == null)
                {
                    var obj = new GameObject("PondTile_" + tile);
                    obj.transform.SetParent(transform, false);
                    marker = obj.AddComponent<SpriteRenderer>();
                    marker.sortingOrder = -898;
                    pondMarkers[tile] = marker;
                    map.RegisterRouteObject(obj.transform, Vector3.zero);
                    map.MarkNearRouteObject(obj.transform);
                }
                if (!wet && pondWasWet[tile]) pondDryStart[tile] = Time.unscaledTime;
                if (wet) pondDryStart[tile] = -1f;
                pondWasWet[tile] = wet;
                marker.sprite = sprite;
                marker.enabled = sprite != null;
                marker.color = Color.white;
                if (sprite == null) continue;
                float height = TileWidth * Mathf.Max(.05f, pond.tileHeightRatio);
                marker.transform.localScale = new Vector3(
                    TileWidth / Mathf.Max(.001f, sprite.bounds.size.x),
                    height / Mathf.Max(.001f, sprite.bounds.size.y), 1f);
                var layout = mapSettings.sceneLayout;
                map.UpdateRouteObject(marker.transform, new Vector3(TileCenter(tile),
                    mapSettings.ringThickness * .5f + (layout != null ? layout.groundY : 0f) - height * .5f,
                    -.06f));
            }
        }
        public float PondFadeRemaining(int tile)
        {
            if (Tiles == null || tile < 0 || tile >= Tiles.Length || Tiles[tile].IsPuddle ||
                pondDryStart == null || pondDryStart[tile] < 0f) return 0f;
            var pond = mapSettings.sceneThemes != null ? mapSettings.sceneThemes.For(theme)?.pond : null;
            return pond == null ? 0f : Mathf.Max(0f,
                Mathf.Max(.05f, pond.dryFadeSeconds) - (Time.unscaledTime - pondDryStart[tile]));
        }
        public void RestorePondVisuals(JourneyMap map, int stationIndex, StationData saved, float elapsedSeconds)
        {
            if (Tiles == null || pondMarkers == null || saved?.tiles == null) return;
            var pond = mapSettings.sceneThemes != null ? mapSettings.sceneThemes.For(theme)?.pond : null;
            if (pond == null || !pond.HasWetSprites) return;
            float duration = Mathf.Max(.05f, pond.dryFadeSeconds);
            var remaining = new float[tileCount];
            for (int tile = 0; tile < tileCount; tile++)
            {
                remaining[tile] = Tiles[tile].IsPuddle ? 0f : Mathf.Clamp(
                    saved.tiles[tile].pondFadeRemainingSeconds - elapsedSeconds, 0f, duration);
                // Rebuilt maps may have randomly wet tiles. Replace that transient visual history
                // with the saved fade state before syncing sprites.
                pondWasWet[tile] = !Tiles[tile].IsPuddle && remaining[tile] > 0f;
                pondDryStart[tile] = -1f;
                if (!Tiles[tile].IsPuddle && remaining[tile] <= 0f && pondMarkers[tile] != null)
                    pondMarkers[tile].enabled = false;
            }
            SyncPondVisuals(map, stationIndex);
            for (int tile = 0; tile < tileCount; tile++)
            {
                if (remaining[tile] <= 0f) continue;
                pondDryStart[tile] = Time.unscaledTime - (duration - remaining[tile]);
                if (pondMarkers[tile] != null)
                    pondMarkers[tile].color = new Color(1f, 1f, 1f, remaining[tile] / duration);
            }
        }
        void FramePonds(Camera camera)
        {
            if (pondMarkers == null) return;
            var pond = mapSettings.sceneThemes != null ? mapSettings.sceneThemes.For(theme)?.pond : null;
            if (pond == null) return;
            var layout = mapSettings.sceneLayout;
            for (int tile = 0; tile < pondMarkers.Length; tile++)
            {
                var marker = pondMarkers[tile];
                if (marker == null || marker.sprite == null) continue;
                float height = marker.sprite.bounds.size.y * marker.transform.localScale.y;
                ownerMap.UpdateRouteObject(marker.transform, new Vector3(TileCenter(tile),
                    mapSettings.ringThickness * .5f + (layout?.groundY ?? 0f) - height * .5f, -.06f));
                marker.transform.rotation = camera.transform.rotation;
                if (pondDryStart[tile] < 0f) continue;
                float alpha = 1f - (Time.unscaledTime - pondDryStart[tile]) /
                    Mathf.Max(.05f, pond.dryFadeSeconds);
                marker.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                marker.enabled = alpha > 0f;
            }
        }
        public void SetMoistureLabel(int tile, TextMesh label)
        {
            moistureLabels[tile] = label;
            label.gameObject.SetActive(debugLabels);
            RefreshMoisture(tile);
        }
        public void AddWater(int tile, int quantity)
        {
            if (quantity <= 0) return;
            Tiles[tile].AddWater(quantity); RefreshTile(tile);
        }
        public bool CanReceiveSeeds(int tile, int quantity) => quantity > 0 &&
            Tiles[tile].FreePlantCapacity >= quantity && mapSettings.plantCatalog != null &&
            mapSettings.plantCatalog.SuitableFor(Tiles[tile].Category).Length > 0;
        public void AddSeeds(int tile, int quantity, int currentStep)
        {
            if (!CanReceiveSeeds(tile, quantity)) throw new InvalidOperationException("地块没有适宜品种或剩余植物空间。");
            var eligible = mapSettings.plantCatalog.SuitableFor(Tiles[tile].Category);
            for (int i = 0; i < quantity; i++)
                Tiles[tile].TryAddSeed(eligible[UnityEngine.Random.Range(0, eligible.Length)].id, currentStep);
            RefreshMoisture(tile);
        }
        public void SetDebugLabels(bool visible)
        {
            debugLabels = visible;
            for (int i = 0; i < tileCount; i++)
            {
                var label = moistureLabels[i];
                if (label != null)
                {
                    label.gameObject.SetActive(visible);
                    label.anchor = visible ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter;
                    label.characterSize = (visible ? .016f : .028f) * Mathf.Min(1, TileWidth);
                    label.lineSpacing = 1.1f;
                }
                RefreshMoisture(i);
            }
        }
        public void StepTiles(int currentStep, JourneyMap map,
            TileSnapshot[] beforeFlow, int firstTileIndex)
        {
            for (int i = 0; i < tileCount; i++)
            {
                Tiles[i].Step(mapSettings.plantCatalog, currentStep, beforeFlow[firstTileIndex + i],
                    mapSettings.evaporationSoilFactor, mapSettings.evaporationPlantFactor,
                    mapSettings.minimumEvaporationRatio, mapSettings);
                RefreshTile(i, false);
            }
            SyncPlantMarkers(map);
        }
        public void SyncPlantMarkers(JourneyMap map)
        {
            if (mapSettings.plantCatalog == null) return;
            var dead = new List<PlantInstance>();
            foreach (var entry in plantMarkers)
                if (entry.Key.life <= 0 || entry.Value == null)
                {
                    if (entry.Value != null) DestroyPlantRenderer(map, entry.Value);
                    dead.Add(entry.Key);
                }
            foreach (var plant in dead)
            {
                plantMarkers.Remove(plant);
                if (!plantVisuals.TryGetValue(plant, out var visual)) continue;
                ClearPlantFade(map, visual);
                plantVisuals.Remove(plant);
            }
            for (int tile = 0; tile < tileCount; tile++)
                foreach (var plant in Tiles[tile].NaturalPlants)
                {
                    var definition = mapSettings.plantCatalog.Find(plant.plantId);
                    if (definition == null || definition.StageCount == 0) continue;
                    bool created = false;
                    if (!plantMarkers.TryGetValue(plant, out var marker))
                    {
                        var obj = new GameObject("NaturalPlant_" + tile + "_" + plant.plantId);
                        obj.transform.SetParent(transform, false);
                        marker = obj.AddComponent<SpriteRenderer>();
                        marker.sortingOrder = definition.isForeground ? 20 : 0;
                        plantMarkers.Add(plant, marker);
                        map.RegisterRouteObject(obj.transform, Vector3.zero);
                        map.MarkNearRouteObject(obj.transform);
                        created = true;
                    }
                    int stageIndex = Mathf.Clamp(plant.stageIndex, 0, definition.StageCount - 1);
                    var stage = definition.stages[stageIndex];
                    var sprite = stage.sprite != null ? stage.sprite :
                        definition.isForeground ? mapSettings.fallbackPlantSprite : mapSettings.fallbackBackgroundPlantSprite;
                    Color targetColor = stage.sprite == null ? stage.fallbackColor : Color.white;
                    if (!plantVisuals.TryGetValue(plant, out var visual))
                    {
                        visual = new PlantVisualState { stageIndex = stageIndex, targetColor = targetColor };
                        plantVisuals.Add(plant, visual);
                        created = true;
                    }
                    if (sprite == null)
                    {
                        marker.enabled = false;
                        marker.sprite = null;
                        ClearPlantFade(map, visual);
                        visual.stageIndex = stageIndex;
                        continue;
                    }
                    float now = Time.realtimeSinceStartup;
                    if (created || marker.sprite == null)
                    {
                        marker.sprite = sprite;
                        marker.color = targetColor;
                        visual.stageIndex = stageIndex;
                        visual.targetColor = targetColor;
                    }
                    else if (visual.stageIndex != stageIndex || marker.sprite != sprite)
                    {
                        AdvancePlantFade(map, visual, marker, now);
                        if (mapSettings.plantStageFadeSeconds > 0f)
                            StartPlantFade(map, tile, plant, definition, visual, marker, now);
                        else ClearPlantFade(map, visual);
                        marker.sprite = sprite;
                        marker.color = targetColor;
                        if (mapSettings.plantStageFadeSeconds > 0f)
                            marker.color = WithAlpha(targetColor, 0f);
                        visual.stageIndex = stageIndex;
                        visual.targetColor = targetColor;
                        visual.fadeStartedAt = mapSettings.plantStageFadeSeconds > 0f ? now : -1f;
                    }
                    else
                    {
                        visual.targetColor = targetColor;
                        if (visual.fadeStartedAt < 0f) marker.color = targetColor;
                    }
                    marker.enabled = true;
                    float stageHeightRatio = !definition.isForeground && definition.StageCount == 3 ?
                        stageIndex == 0 ? Mathf.Clamp(mapSettings.backgroundSaplingHeightRatio, .1f, 1f) :
                        stageIndex == 1 ? Mathf.Clamp(mapSettings.backgroundMediumHeightRatio, .1f, 1f) : 1f :
                        (stageIndex + 1f) / definition.StageCount;
                    float height = stage.sprite == null ?
                        (definition.isForeground ? Mathf.Min(TileWidth * .16f, .16f) :
                            Mathf.Max(.1f, mapSettings.backgroundPlantHeight) * stageHeightRatio) :
                        Mathf.Max(.05f, definition.visualHeight) * stageHeightRatio;
                    Vector2 visibleY = PlantVisibleY(sprite);
                    marker.transform.localScale = Vector3.one *
                        (height / Mathf.Max(.001f, visibleY.y - visibleY.x));
                    UpdatePlantPosition(map, tile, plant, definition, marker);
                }
        }
        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
        void DestroyPlantRenderer(JourneyMap map, SpriteRenderer renderer)
        {
            if (renderer == null) return;
            if (map != null) map.UnregisterRouteObject(renderer.transform);
            renderer.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(renderer.gameObject);
            else DestroyImmediate(renderer.gameObject);
        }
        void ClearPlantFade(JourneyMap map, PlantVisualState visual)
        {
            foreach (var layer in visual.outgoing) DestroyPlantRenderer(map, layer.renderer);
            visual.outgoing.Clear();
            visual.fadeStartedAt = -1f;
        }
        void StartPlantFade(JourneyMap map, int tile, PlantInstance plant,
            PlantDefinition definition, PlantVisualState visual, SpriteRenderer marker, float now)
        {
            foreach (var layer in visual.outgoing)
            {
                layer.startAlpha = layer.renderer.color.a;
                layer.startedAt = now;
            }
            if (!marker.enabled || marker.sprite == null || marker.color.a <= .001f) return;
            var obj = new GameObject(marker.name + "_FadeOut");
            obj.transform.SetParent(transform, false);
            var previous = obj.AddComponent<SpriteRenderer>();
            previous.sprite = marker.sprite;
            previous.color = marker.color;
            previous.sharedMaterial = marker.sharedMaterial;
            previous.sortingLayerID = marker.sortingLayerID;
            previous.sortingOrder = marker.sortingOrder - 1;
            previous.flipX = marker.flipX;
            previous.flipY = marker.flipY;
            previous.transform.localScale = marker.transform.localScale;
            previous.transform.rotation = marker.transform.rotation;
            map.RegisterRouteObject(previous.transform, Vector3.zero);
            map.MarkNearRouteObject(previous.transform);
            UpdatePlantPosition(map, tile, plant, definition, previous);
            visual.outgoing.Add(new FadingPlantSprite
            {
                renderer = previous, startAlpha = previous.color.a, startedAt = now
            });
        }
        void AdvancePlantFade(JourneyMap map, PlantVisualState visual,
            SpriteRenderer marker, float now)
        {
            float duration = Mathf.Max(0f, mapSettings.plantStageFadeSeconds);
            if (visual.fadeStartedAt >= 0f)
            {
                float progress = duration <= 0f ? 1f :
                    Mathf.Clamp01((now - visual.fadeStartedAt) / duration);
                marker.color = WithAlpha(visual.targetColor, visual.targetColor.a * progress);
                if (progress >= 1f) visual.fadeStartedAt = -1f;
            }
            for (int i = visual.outgoing.Count - 1; i >= 0; i--)
            {
                var layer = visual.outgoing[i];
                if (layer.renderer == null)
                {
                    visual.outgoing.RemoveAt(i);
                    continue;
                }
                float progress = duration <= 0f ? 1f :
                    Mathf.Clamp01((now - layer.startedAt) / duration);
                if (progress >= 1f)
                {
                    DestroyPlantRenderer(map, layer.renderer);
                    visual.outgoing.RemoveAt(i);
                }
                else layer.renderer.color = WithAlpha(layer.renderer.color,
                    layer.startAlpha * (1f - progress));
            }
        }
        Vector2 PlantVisibleY(Sprite sprite)
        {
            if (plantVisibleY.TryGetValue(sprite, out var bounds)) return bounds;
            // Sprite.bounds includes transparent canvas; tight mesh vertices follow the painted plant.
            var vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0)
                return new Vector2(sprite.bounds.min.y, sprite.bounds.max.y);
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector2 vertex in vertices)
            {
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
            }
            bounds = new Vector2(minY, maxY);
            plantVisibleY.Add(sprite, bounds);
            return bounds;
        }
        void UpdatePlantPosition(JourneyMap map, int tile, PlantInstance plant,
            PlantDefinition definition, SpriteRenderer marker)
        {
            var layout = mapSettings.sceneLayout;
            Vector2 scatter = PlantScatter(tile, plant);
            float baseline = mapSettings.ringThickness * .5f + (layout == null ? 0f :
                definition.isForeground ? layout.foregroundPlantY : layout.backgroundPlantY);
            float plantFootY = baseline +
                scatter.y * Mathf.Max(0, mapSettings.plantVerticalScatter);
            if (!definition.isForeground)
            {
                float foxY = mapSettings.ringThickness * .5f + JourneyMap.FoxHeightAboveRoad +
                    (layout?.foxY ?? 0f);
                plantFootY = Mathf.Min(plantFootY, foxY);
            }
            map.UpdateRouteObject(marker.transform,
                new Vector3(TileCenter(tile) + plant.positionOffset * TileWidth +
                    scatter.x * Mathf.Max(0, mapSettings.plantHorizontalScatter) * TileWidth,
                    plantFootY - PlantVisibleY(marker.sprite).x * marker.transform.localScale.y,
                    definition.isForeground ? -.18f : .35f));
        }
        Vector2 PlantScatter(int tile, PlantInstance plant)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char letter in stationId ?? string.Empty) hash = (hash ^ letter) * 16777619u;
                foreach (char letter in plant.plantId ?? string.Empty) hash = (hash ^ letter) * 16777619u;
                hash = (hash ^ (uint)tile) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(plant.positionOffset * 1000f)) * 16777619u;
                return new Vector2(HashToSignedUnit(hash), HashToSignedUnit(hash ^ 0x9e3779b9u));
            }
        }
        static float HashToSignedUnit(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 8388607.5f - 1f;
            }
        }
        void RefreshPlantPositions()
        {
            if (ownerMap == null || mapSettings == null || Tiles == null) return;
            for (int tile = 0; tile < tileCount; tile++)
            {
                foreach (var plant in Tiles[tile].NaturalPlants)
                {
                    if (!plantMarkers.TryGetValue(plant, out var marker) || marker == null || marker.sprite == null)
                        continue;
                    var definition = mapSettings.plantCatalog?.Find(plant.plantId);
                    if (definition == null) continue;
                    if (plantVisuals.TryGetValue(plant, out var visual))
                        AdvancePlantFade(ownerMap, visual, marker, Time.realtimeSinceStartup);
                    UpdatePlantPosition(ownerMap, tile, plant, definition, marker);
                    if (visual == null) continue;
                    foreach (var layer in visual.outgoing)
                        if (layer.renderer != null)
                            UpdatePlantPosition(ownerMap, tile, plant, definition, layer.renderer);
                }
            }
        }
        public void FramePlants(Camera camera)
        {
            if (camera == null || !camera.orthographic) return;
            RefreshNearGroundPositions();
            FramePonds(camera);
            RefreshPlantPositions();
            Quaternion facing = camera.transform.rotation;
            foreach (var marker in plantMarkers.Values)
                if (marker != null) marker.transform.rotation = facing;
            foreach (var visual in plantVisuals.Values)
                foreach (var layer in visual.outgoing)
                    if (layer.renderer != null) layer.renderer.transform.rotation = facing;
        }
        void RefreshNearGroundPositions()
        {
            if (ownerMap == null || patchVisuals == null) return;
            var layout = mapSettings.sceneLayout;
            float top = mapSettings.ringThickness * .5f + (layout?.groundY ?? 0f);
            for (int tile = 0; tile < tileCount; tile++)
            {
                var ground = patchVisuals[tile];
                if (ground != null && ground.sprite != null)
                {
                    float height = ground.sprite.bounds.size.y * ground.transform.localScale.y;
                    ownerMap.UpdateRouteObject(ground.transform,
                        new Vector3(TileCenter(tile), top - height * .5f, -.05f));
                }
                var water = patchWaterOverlays[tile];
                if (water == null || water.sprite == null) continue;
                float waterHeight = water.sprite.bounds.size.y * water.transform.localScale.y;
                ownerMap.UpdateRouteObject(water.transform,
                    new Vector3(TileCenter(tile), top - waterHeight * .5f, -.055f));
            }
        }
        static string Delta(float value, string suffix = "") => $"({value:+0.0;-0.0;0}{suffix})";
        static string SmallDelta(float value) => $"({value:+0.0000;-0.0000;0.0000})";
        void RefreshMoisture(int tile)
        {
            if (moistureLabels[tile] == null) return;
            var t = Tiles[tile]; var d = t.LastChange;
            moistureLabels[tile].text = debugLabels ?
                $"{stationId.Replace("station-", "")}-{tile + 1} {t.CategoryName}\n" +
                $"泥土 {t.SoilContent:0.0} {Delta(d.SoilContent)}\n" +
                $"容量 {t.MaxWater:0.0} {Delta(d.MaxWater)}\n" +
                $"土壤水 {t.SoilWater:0.0} {Delta(d.SoilWater)}\n" +
                $"自然植物 {t.PlantCount} {Delta(d.PlantCount)}\n" +
                $"待萌发 {t.SeedCount} {Delta(d.SeedCount)}\n" +
                $"植 {PlantDetails(t)}\n" +
                $"湿度 {t.SoilHumidity * 100:0.0}% {Delta(d.SoilHumidity * 100)}\n" +
                $"蒸发 {t.EvaporationRate:0.000}/步 {Delta(d.EvaporationRate)}\n" +
                $"高度 {t.Height}+{t.EffectiveHeight - t.Height}={t.EffectiveHeight} {Delta(d.EffectiveHeight)}\n" +
                $"地表水 {t.SurfaceWater:0.000} {SmallDelta(d.SurfaceWater)}\n" +
                $"水汽 {t.PendingVapor:0.000} {SmallDelta(d.PendingVapor)} · 雨 +{t.RainfallThisStep:0.0000}\n" +
                $"排水 {t.DrainageLoss:0.0} {Delta(d.DrainageLoss)}\n" +
                $"温度 {t.Temperature:0.0000}°C ({d.Temperature:+0.0000;-0.0000;0.0000})" : string.Empty;
        }
        static string PlantDetails(TileState tile) => tile.PlantCount == 0 ? "无" :
            string.Join(" ", Array.ConvertAll(tile.NaturalPlants, plant =>
                $"{plant.life}/{plant.stageIndex + 1}{(plant.lastLifeChange > 0 ? "+" : plant.lastLifeChange < 0 ? "-" : "=")}"));
        public float startDistance, length;
        public float TileWidth => length / tileCount;
        public float TileCenter(int index) => startDistance + (Mathf.Clamp(index, 0, tileCount - 1) + .5f) * TileWidth;
        public string ThemeName => theme == StationTheme.Snow ? "雪地" : theme == StationTheme.Grass ? "平原" : "沙漠";
        public Transform stopPoint;
        public PlacementSlot[] slots;
        [Range(0, 1)] public float stopProgress;
        [Tooltip("按顺序经过这些点，再抵达下一站；最后一站通向终点")]
        public Transform[] outgoingWaypoints;
        [Tooltip("本站左右边界（路线距离）")]
        public Vector2 cameraRange;
    }
}
