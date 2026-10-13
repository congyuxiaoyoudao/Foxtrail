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
        // 水草标记是复用加开关的（SyncWaterGrassMarkers），不用清。
        public void ClearPlantMarkers(JourneyMap map)
        {
            foreach (var pair in plantMarkers)
            {
                if (pair.Value == null) continue;
                if (map != null) map.UnregisterRouteObject(pair.Value.transform);
                if (Application.isPlaying) Destroy(pair.Value.gameObject);
                else DestroyImmediate(pair.Value.gameObject);
            }
            plantMarkers.Clear();
        }
        TextMesh[] moistureLabels;
        readonly Dictionary<PlantInstance, SpriteRenderer> plantMarkers = new Dictionary<PlantInstance, SpriteRenderer>();
        Mesh[] patchMeshes;
        SpriteRenderer[] patchVisuals;
        SpriteRenderer[] patchWaterOverlays;
        Color[] patchColors;
        SpriteRenderer[] waterGrassMarkers;
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
            waterGrassMarkers = new SpriteRenderer[tileCount];
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
                if (layers != null && layers.nearGround != null)
                {
                    var visual = patchVisuals[tile]; visual.sprite = layers.nearGround;
                    float sx = TileWidth / Mathf.Max(.001f, visual.sprite.bounds.size.x);
                    float sy = sx * Mathf.Max(.1f, layers.nearHeightMultiplier);
                    visual.transform.localScale = new Vector3(sx,sy,1);
                    ownerMap.UpdateRouteObject(visual.transform,new Vector3(TileCenter(tile),mapSettings.ringThickness*.5f+layers.nearGroundTopOffset-visual.sprite.bounds.size.y*sy*.5f,-.05f));
                    visual.color = Color.white;
                }
                else patchVisuals[tile].color = mapSettings.ThemeColor(Tiles[tile].Category);
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
                map.UpdateRouteObject(marker.transform, new Vector3(TileCenter(tile),
                    mapSettings.ringThickness * .5f + layers.nearGroundTopOffset - height * .5f,
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
            for (int tile = 0; tile < pondMarkers.Length; tile++)
            {
                var marker = pondMarkers[tile];
                if (marker == null || marker.sprite == null) continue;
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
            if (ownerMap != null) SyncWaterGrassMarkers(ownerMap);
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
            SyncWaterGrassMarkers(map);
            if (mapSettings.plantCatalog == null) return;
            var dead = new List<PlantInstance>();
            foreach (var entry in plantMarkers)
                if (entry.Key.life <= 0 || entry.Value == null)
                {
                    if (entry.Value != null)
                    {
                        map.UnregisterRouteObject(entry.Value.transform);
                        entry.Value.gameObject.SetActive(false);
                        Destroy(entry.Value.gameObject);
                    }
                    dead.Add(entry.Key);
                }
            foreach (var plant in dead) plantMarkers.Remove(plant);
            for (int tile = 0; tile < tileCount; tile++)
                foreach (var plant in Tiles[tile].NaturalPlants)
                {
                    var definition = mapSettings.plantCatalog.Find(plant.plantId);
                    if (definition == null || definition.StageCount == 0) continue;
                    if (!plantMarkers.TryGetValue(plant, out var marker))
                    {
                        var obj = new GameObject("NaturalPlant_" + tile + "_" + plant.plantId);
                        obj.transform.SetParent(transform, false);
                        marker = obj.AddComponent<SpriteRenderer>();
                        marker.sortingOrder = definition.isForeground ? 20 : 0;
                        plantMarkers.Add(plant, marker);
                        map.RegisterRouteObject(obj.transform, Vector3.zero);
                        if (definition.isForeground) map.MarkNearRouteObject(obj.transform);
                    }
                    var stage = definition.stages[Mathf.Clamp(plant.stageIndex, 0, definition.StageCount - 1)];
                    var sprite = stage.sprite != null ? stage.sprite :
                        definition.isForeground ? mapSettings.fallbackPlantSprite : mapSettings.fallbackBackgroundPlantSprite;
                    marker.enabled = sprite != null;
                    if (sprite == null) continue;
                    marker.sprite = sprite;
                    marker.color = stage.sprite == null ? stage.fallbackColor : Color.white;
                    float height = stage.sprite == null ?
                        (definition.isForeground ? Mathf.Min(TileWidth * .16f, .16f) :
                            Mathf.Max(.1f, mapSettings.backgroundPlantHeight)) :
                        Mathf.Max(.05f, definition.visualHeight) *
                            (Mathf.Clamp(plant.stageIndex, 0, definition.StageCount - 1) + 1f) /
                            definition.StageCount;
                    marker.transform.localScale = Vector3.one *
                        (height / Mathf.Max(.001f, sprite.bounds.size.y));
                    UpdatePlantPosition(map, tile, plant, definition, marker, height);
                }
        }
        void UpdatePlantPosition(JourneyMap map, int tile, PlantInstance plant,
            PlantDefinition definition, SpriteRenderer marker, float height)
        {
            Vector2 offset = definition.isForeground ? mapSettings.foregroundPlantPositionOffset :
                mapSettings.backgroundPlantPositionOffset;
            Vector2 scatter = PlantScatter(tile, plant);
            float baseline = mapSettings.ringThickness * .5f + mapSettings.foregroundPlantY +
                (definition.isForeground ? 0f : Mathf.Max(0, mapSettings.backgroundPlantOffset));
            map.UpdateRouteObject(marker.transform,
                new Vector3(TileCenter(tile) + plant.positionOffset * TileWidth + offset.x +
                    scatter.x * Mathf.Max(0, mapSettings.plantHorizontalScatter) * TileWidth,
                    baseline + height * .5f + offset.y +
                    scatter.y * Mathf.Max(0, mapSettings.plantVerticalScatter),
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
                    float height = marker.sprite.bounds.size.y * marker.transform.localScale.y;
                    UpdatePlantPosition(ownerMap, tile, plant, definition, marker, height);
                }
                if (waterGrassMarkers == null || waterGrassMarkers[tile] == null) continue;
                Vector2 offset = mapSettings.foregroundPlantPositionOffset;
                ownerMap.UpdateRouteObject(waterGrassMarkers[tile].transform,
                    new Vector3(TileCenter(tile) + offset.x,
                        mapSettings.ringThickness * .5f + mapSettings.foregroundPlantY + offset.y, -.04f));
            }
        }
        public void FramePlants(Camera camera)
        {
            if (camera == null || !camera.orthographic) return;
            FramePonds(camera);
            RefreshPlantPositions();
            Quaternion facing = camera.transform.rotation;
            foreach (var marker in plantMarkers.Values)
                if (marker != null) marker.transform.rotation = facing;
            if (waterGrassMarkers != null)
                foreach (var marker in waterGrassMarkers)
                    if (marker != null) marker.transform.rotation = facing;
        }
        void SyncWaterGrassMarkers(JourneyMap map)
        {
            for (int tile = 0; tile < tileCount; tile++)
            {
                var stage = Tiles[tile].WaterGrass;
                var marker = waterGrassMarkers[tile];
                if (stage == WaterGrassStage.None)
                {
                    if (marker != null) marker.enabled = false;
                    continue;
                }
                if (marker == null)
                {
                    var obj = new GameObject("WaterGrass_" + tile);
                    obj.transform.SetParent(transform, false);
                    marker = obj.AddComponent<SpriteRenderer>();
                    marker.sortingOrder = 20;
                    marker.sprite = mapSettings.fallbackPlantSprite;
                    waterGrassMarkers[tile] = marker;
                    map.RegisterRouteObject(obj.transform, new Vector3(TileCenter(tile),
                        mapSettings.ringThickness * .5f + mapSettings.foregroundPlantY, -.04f));
                    map.MarkNearRouteObject(obj.transform);
                }
                Vector2 offset = mapSettings.foregroundPlantPositionOffset;
                map.UpdateRouteObject(marker.transform, new Vector3(TileCenter(tile) + offset.x,
                    mapSettings.ringThickness * .5f + mapSettings.foregroundPlantY + offset.y, -.04f));
                marker.enabled = marker.sprite != null;
                if (marker.sprite == null) continue;
                marker.color = stage == WaterGrassStage.Large ? new Color(.04f, .48f, .36f) :
                    new Color(.2f, .76f, .55f);
                float size = stage == WaterGrassStage.Large ? .22f : .11f;
                marker.transform.localScale = Vector3.one * (size /
                    Mathf.Max(.001f, Mathf.Max(marker.sprite.bounds.size.x, marker.sprite.bounds.size.y)));
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
                $"水草 {t.WaterGrass} ({t.WaterGrassAge}s) · 排水 {t.DrainageLoss:0.0} {Delta(d.DrainageLoss)}\n" +
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
