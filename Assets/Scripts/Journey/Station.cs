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
        TextMesh[] moistureLabels;
        readonly Dictionary<PlantInstance, SpriteRenderer> plantMarkers = new Dictionary<PlantInstance, SpriteRenderer>();
        Mesh[] patchMeshes;
        SpriteRenderer[] patchVisuals;
        SpriteRenderer[] patchWaterOverlays;
        Color[] patchColors;
        SpriteRenderer[] waterGrassMarkers;
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
            for (int i = 0; i < tileCount; i++) Tiles[i] = settings.CreateTile(theme);
        }
        public float MoistureAt(int tile) => Tiles[tile].SurfaceWater;
        public void RefreshTile(int tile) { RefreshMoisture(tile); RefreshWaterVisual(tile); }
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
            float blend = Tiles[tile].Depth == WaterDepth.Deep ? .72f : Tiles[tile].Depth == WaterDepth.Medium ? .43f : 0f;
            ownerMap.SetRouteMeshColor(patchMeshes[tile], Color.Lerp(patchColors[tile], new Color(.18f, .58f, .92f), blend));
            if (patchWaterOverlays[tile] != null)
                patchWaterOverlays[tile].color = new Color(.18f, .58f, .92f, blend);
            else if (patchVisuals[tile] != null)
                patchVisuals[tile].color = Color.Lerp(Color.white, new Color(.38f, .72f, 1f), blend);
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
            mapSettings.plantCatalog.SuitableFor(theme).Length > 0;
        public void AddSeeds(int tile, int quantity, int currentStep)
        {
            if (!CanReceiveSeeds(tile, quantity)) throw new InvalidOperationException("地块没有适宜品种或剩余植物空间。");
            var eligible = mapSettings.plantCatalog.SuitableFor(theme);
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
                    mapSettings.minimumEvaporationRatio);
                RefreshTile(i);
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
            float baseline = mapSettings.ringThickness * .5f + mapSettings.foregroundPlantY +
                (definition.isForeground ? 0f : Mathf.Max(0, mapSettings.backgroundPlantOffset));
            map.UpdateRouteObject(marker.transform,
                new Vector3(TileCenter(tile) + plant.positionOffset * TileWidth + offset.x,
                    baseline + height * .5f + offset.y, definition.isForeground ? -.18f : .35f));
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
        // StopPoint is the single source of truth, so moving the marker changes the actual destination.
        public float StopAngle
        {
            get
            {
                var map = GetComponentInParent<JourneyMap>();
                Vector3 point = map.transform.InverseTransformPoint(stopPoint.position);
                // XZ平面上的角度，逆时针方向（X使用负号）
                return Mathf.Repeat(Mathf.Atan2(-point.x, point.z) * Mathf.Rad2Deg, 360);
            }
        }
        [Tooltip("按顺序经过这些点，再抵达下一站；最后一站通向终点")]
        public Transform[] outgoingWaypoints;
        [Tooltip("本站左右边界（路线距离）")]
        public Vector2 cameraRange;
    }
}
