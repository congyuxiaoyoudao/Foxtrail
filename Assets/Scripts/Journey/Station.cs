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
        // 过渡格：每站最左与最右两块，落在站边界上，装配时用灰蓝色标出。
        public static bool IsTransitionTile(int tile, int tileCount) =>
            tile <= 0 || tile >= tileCount - 1;
        // 地块转变：单块按自身温度改变类别（TileState.UpdateWaterTemperature），
        // 单块变化只换该格的地面，不动整站远景与中景；整站再按已转变块数切换，
        // 远景先变（farThemeSwitchTiles，默认 7 块），中景后变（middleThemeSwitchTiles，默认 10 块）。
        readonly List<SceneLandscapeLayer> themeLayers = new List<SceneLandscapeLayer>();
        readonly List<SceneThemeFill> themeFills = new List<SceneThemeFill>();
        public StationTheme FarTheme { get; private set; }
        public StationTheme MiddleTheme { get; private set; }
        public Station NextStation { get; private set; }
        Station previousStation;
        // 逐块过渡：温度进入分界附近的地块，地面提前换成预期类别的土地素材；
        // 整站远景与中景不随单块变化。
        bool[] transitionActive;
        int[] transitionBand;
        StationTheme[] transitionFrom;
        StationTheme[] transitionTo;
        public void RegisterThemeLayer(SceneLandscapeLayer layer)
        {
            if (layer != null && !themeLayers.Contains(layer)) themeLayers.Add(layer);
        }
        public void RegisterThemeFill(SceneThemeFill fill)
        {
            if (fill != null && !themeFills.Contains(fill)) themeFills.Add(fill);
        }
        public void SetNextStation(Station next)
        {
            NextStation = next;
            if (next != null) next.previousStation = this;
            foreach (var layer in themeLayers) layer.RefreshPosition();
            if (tileCount > 0) RefreshTile(tileCount - 1, false);
        }
        // 每步结算后调用。块数没有跨过阈值时不会改动整站景素材引用。
        public void RefreshThemeConversion(bool refreshTileTransitions = true)
        {
            if (Tiles == null || Tiles.Length == 0) return;
            int snow = 0, grass = 0, desert = 0;
            foreach (var tile in Tiles)
            {
                if (tile.Category == StationTheme.Snow) snow++;
                else if (tile.Category == StationTheme.Grass) grass++;
                else desert++;
            }
            // 转变后整站通常收敛到同一种类别，数量最多的新类别决定整站背景。
            var dominant = theme;
            int dominantCount = 0;
            if (theme != StationTheme.Snow && snow > dominantCount) { dominant = StationTheme.Snow; dominantCount = snow; }
            if (theme != StationTheme.Grass && grass > dominantCount) { dominant = StationTheme.Grass; dominantCount = grass; }
            if (theme != StationTheme.Desert && desert > dominantCount) { dominant = StationTheme.Desert; dominantCount = desert; }
            int farTiles = mapSettings != null ? Mathf.Clamp(mapSettings.farThemeSwitchTiles, 1, Tiles.Length) : 7;
            int middleTiles = mapSettings != null ? Mathf.Clamp(mapSettings.middleThemeSwitchTiles, 1, Tiles.Length) : 10;
            FarTheme = dominantCount >= farTiles ? dominant : theme;
            MiddleTheme = dominantCount >= middleTiles ? dominant : theme;
            foreach (var layer in themeLayers)
                layer.SetArtTheme(layer.LayerIndex < 3 ? FarTheme : MiddleTheme);
            foreach (var fill in themeFills)
                fill.SetFillTheme(fill.IsGround ? MiddleTheme : FarTheme);
            if (refreshTileTransitions) RefreshTileTransitions();
        }
        // 每步结算后逐块判断过渡温度带：from 在进入温度带时记录一次并保持不变，
        // to 取温度正在靠拢的那一侧，所以升温与降温显示的方向不同。
        void RefreshTileTransitions()
        {
            if (Tiles == null || transitionActive == null) return;
            float band = mapSettings != null ? Mathf.Max(.1f, mapSettings.transitionBandWidth) : 1f;
            for (int i = 0; i < tileCount; i++)
            {
                var tile = Tiles[i];
                float temperature = tile.Temperature;
                float boundary = Mathf.Abs(temperature - 5f) < band ? 5f :
                    Mathf.Abs(temperature - 12f) < band ? 12f : 0f;
                int segment = boundary <= 0f ? 0 : boundary < 10f ? 1 : 2;
                if (segment == 0)
                {
                    if (!transitionActive[i]) continue;
                    transitionActive[i] = false;
                }
                else
                {
                    bool enteringBand = !transitionActive[i] || transitionBand[i] != segment;
                    if (enteringBand)
                    {
                        transitionBand[i] = segment;
                        transitionFrom[i] = tile.Category;
                    }
                    var low = segment == 1 ? StationTheme.Snow : StationTheme.Grass;
                    var high = segment == 1 ? StationTheme.Grass : StationTheme.Desert;
                    bool warming = tile.LastChange.Temperature > 0f, cooling = tile.LastChange.Temperature < 0f;
                    // 没有本步温差时维持原表现；旧存档缺少方向时按当前类别显示。
                    transitionTo[i] = warming ? high : cooling ? low :
                        enteringBand ? tile.Category : transitionTo[i];
                    transitionActive[i] = true;
                }
                // 过渡期间地面要跟着预期类别重画，所以这一格的状态变化后立即刷新。
                RefreshTile(i, false);
            }
        }
public TileState[] Tiles { get; private set; }
        // 读档：整批换成存档里的地块。Tiles 是只读属性，只能在类内部赋值。
        public void RestoreTiles(TileState[] tiles) => Tiles = tiles;
        public void CaptureTileTransition(int index, TileData data)
        {
            if (data == null || transitionActive == null || index < 0 || index >= transitionActive.Length)
                return;
            data.transitionActive = transitionActive[index];
            data.transitionBand = transitionBand[index];
            data.transitionFrom = (int)transitionFrom[index];
            data.transitionTo = (int)transitionTo[index];
        }
        // 过渡方向会影响当前地面贴图，必须在刷新画面前恢复。
        // 老档没有这组字段，调用方会按当前类别和温度重新计算。
        public bool RestoreTileTransitions(TileData[] saved)
        {
            if (saved == null || transitionActive == null || saved.Length != transitionActive.Length)
                return false;
            foreach (var tile in saved)
                if (tile == null || !tile.hasVisualState || tile.transitionBand < 0 || tile.transitionBand > 2 ||
                    tile.transitionFrom < 0 || tile.transitionFrom > 2 ||
                    tile.transitionTo < 0 || tile.transitionTo > 2 ||
                    tile.transitionActive && tile.transitionBand == 0)
                    return false;
            for (int i = 0; i < saved.Length; i++)
            {
                transitionActive[i] = saved[i].transitionActive;
                transitionBand[i] = saved[i].transitionBand;
                transitionFrom[i] = (StationTheme)saved[i].transitionFrom;
                transitionTo[i] = (StationTheme)saved[i].transitionTo;
            }
            return true;
        }
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
        int pondStationIndex;
        bool[] pondWasWet;
        float[] pondDryStart;
        RingMapSettings mapSettings;
        JourneyMap ownerMap;
        public void InitializeTiles(RingMapSettings settings)
        {
            mapSettings = settings;
                        FarTheme = theme; MiddleTheme = theme;
            transitionActive = new bool[tileCount]; transitionBand = new int[tileCount];
            transitionFrom = new StationTheme[tileCount]; transitionTo = new StationTheme[tileCount];
Tiles = new TileState[tileCount];
            patchMeshes = new Mesh[tileCount]; patchColors = new Color[tileCount];
            patchVisuals = new SpriteRenderer[tileCount];
            patchWaterOverlays = new SpriteRenderer[tileCount];
            pondMarkers = new SpriteRenderer[tileCount];
            pondWasWet = new bool[tileCount];
            pondDryStart = new float[tileCount];
            for (int i = 0; i < tileCount; i++) pondDryStart[i] = -1f;
            int basin = settings.createStationBasin ? UnityEngine.Random.Range(0,3)*4+2 : -10;
            for (int i = 0; i < tileCount; i++)
            {
                int tileIndex = i;
                Tiles[i] = settings.CreateTile(theme, i == basin, Mathf.Abs(i - basin) == 1,
                    definition => CanSpawnNaturalPlant(tileIndex, definition, settings));
            }
        }

        bool CanSpawnNaturalPlant(int tileIndex, PlantDefinition definition, RingMapSettings settings)
        {
            int occupied = 0;
            for (int i = 0; i < tileIndex; i++)
                if (Tiles[i].HasPlantOrSeed(definition.id)) occupied++;
            if (occupied >= Mathf.Clamp(settings.maxNaturalSpeciesTilesPerStation, 1, tileCount))
                return false;
            int run = 0;
            for (int i = tileIndex - 1; i >= 0 && Tiles[i].HasPlantOrSeed(definition.id); i--)
                run++;
            return run < Mathf.Clamp(settings.maxNaturalSpeciesRun, 1, tileCount);
        }
        public void RefreshTile(int tile, bool syncPond = true)
        {
            RefreshWaterVisual(tile);
            // Edge tiles display the themes of both neighbors, so redraw them when either changes.
            if (tile == 1) RefreshWaterVisual(0);
            if (tile == tileCount - 2) RefreshWaterVisual(tileCount - 1);
            if (tile == 0 && previousStation != null)
                previousStation.RefreshWaterVisual(previousStation.tileCount - 1);
            if (tile == tileCount - 1 && NextStation != null)
                NextStation.RefreshWaterVisual(0);
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
            var groundTheme = GroundThemeAt(tile);
            var transition = TransitionForTile(tile, out var sharedNeighborTheme);
            if (sharedNeighborTheme.HasValue) groundTheme = sharedNeighborTheme.Value;
            Color baseColor = groundTheme == theme ? patchColors[tile] : mapSettings.ThemeColor(groundTheme);
            ownerMap.SetRouteMeshColor(patchMeshes[tile], Color.Lerp(baseColor, new Color(.18f, .58f, .92f), blend));
            if (patchVisuals[tile] != null)
            {
                var layers = mapSettings.sceneThemes != null ? mapSettings.sceneThemes.For(groundTheme) : null;
                var transitionSprite = transition?.nearGround;
                var sprite = transitionSprite != null ? transitionSprite :
                    layers != null && layers.nearGround != null ?
                    layers.nearGround : mapSettings.fallbackPlantSprite;
                if (sprite != null)
                {
                    var visual = patchVisuals[tile]; visual.sprite = sprite;
                    float sx = TileWidth / Mathf.Max(.001f, sprite.bounds.size.x);
                    bool hasGroundArt = transitionSprite != null || layers?.nearGround != null;
                    float height = hasGroundArt ? sprite.bounds.size.y * sx :
                        mapSettings.GroundPlaceholderHeight(TileWidth);
                    float sy = height / Mathf.Max(.001f, sprite.bounds.size.y);
                    visual.transform.localScale = new Vector3(sx,sy,1);
                    var layout = mapSettings.sceneLayout;
                    ownerMap.UpdateRouteObject(visual.transform,new Vector3(TileCenter(tile),
                        mapSettings.ringThickness*.5f+(layout != null ? layout.groundY : 0f)-height*.5f,-.05f));
                    visual.color = hasGroundArt ? Color.white :
                        groundTheme == theme ? patchColors[tile] :
                        mapSettings.ThemeColor(groundTheme);
                }
            }
            if (patchWaterOverlays[tile] != null)
                patchWaterOverlays[tile].color = new Color(.18f, .58f, .92f, blend);
            else if (patchVisuals[tile] != null)
                patchVisuals[tile].color = Color.Lerp(Color.white, new Color(.38f, .72f, 1f), blend);
        }
        // The first and last tiles bridge the visible ground themes immediately to their left and right.
        SceneThemeTransition TransitionForTile(int tile, out StationTheme? sharedNeighborTheme)
        {
            sharedNeighborTheme = null;
            if (!IsTransitionTile(tile, tileCount)) return null;
            bool hasLeft = tile > 0 || previousStation != null;
            bool hasRight = tile + 1 < tileCount || NextStation != null;
            if (!hasLeft || !hasRight) return null;
            var left = tile > 0 ? GroundThemeAt(tile - 1) :
                previousStation.GroundThemeAt(previousStation.tileCount - 1);
            var right = tile + 1 < tileCount ? GroundThemeAt(tile + 1) :
                NextStation.GroundThemeAt(0);
            if (left == right)
            {
                sharedNeighborTheme = left;
                return null;
            }
            return mapSettings.sceneThemes?.TransitionFor(left, right);
        }
        // 过渡期间地面先换成预期类别的土地素材，其余时候就是地块自己的类别。
        StationTheme GroundThemeAt(int tile) => transitionActive != null && tile >= 0 &&
            tile < transitionActive.Length && transitionActive[tile] ? transitionTo[tile] : Tiles[tile].Category;
        public void SyncPondVisuals(JourneyMap map, int stationIndex)
        {
            if (Tiles == null || pondMarkers == null || mapSettings == null) return;
            pondStationIndex = stationIndex;
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
                float height = UpdatePondScale(marker, pond);
                var layout = mapSettings.sceneLayout;
                map.UpdateRouteObject(marker.transform, new Vector3(PondCenter(tile, map, wet),
                    mapSettings.ringThickness * .5f + (layout != null ? layout.groundY : 0f) - height * .5f,
                    -.06f));
            }
        }
        float UpdatePondScale(SpriteRenderer marker, PondThemeSettings pond)
        {
            float scale = Mathf.Max(.05f, mapSettings.pondVisualScale);
            float height = TileWidth * Mathf.Max(.05f, pond.tileHeightRatio) * scale;
            marker.transform.localScale = new Vector3(
                TileWidth * scale / Mathf.Max(.001f, marker.sprite.bounds.size.x),
                height / Mathf.Max(.001f, marker.sprite.bounds.size.y), 1f);
            return height;
        }
        float PondCenter(int tile, JourneyMap map, bool wet)
        {
            if (!wet) return TileCenter(tile);
            int globalTile = pondStationIndex * RingMapSettings.TilesPerStation + tile;
            int first = globalTile, last = globalTile;
            while (map.IsPondAt(first - 1)) first--;
            while (map.IsPondAt(last + 1)) last++;
            float scale = Mathf.Max(.05f, mapSettings.pondVisualScale);
            return TileCenter(tile) +
                (globalTile - (first + last) * .5f) * TileWidth * (scale - 1f);
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
                float height = UpdatePondScale(marker, pond);
                ownerMap.UpdateRouteObject(marker.transform, new Vector3(
                    PondCenter(tile, ownerMap, Tiles[tile].IsPuddle),
                    mapSettings.ringThickness * .5f + (layout?.groundY ?? 0f) - height * .5f, -.06f));
                marker.transform.rotation = camera.transform.rotation;
                if (pondDryStart[tile] < 0f) continue;
                float alpha = 1f - (Time.unscaledTime - pondDryStart[tile]) /
                    Mathf.Max(.05f, pond.dryFadeSeconds);
                marker.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                marker.enabled = alpha > 0f;
            }
        }
        public void AddWater(int tile, int quantity)
        {
            if (quantity <= 0) return;
            Tiles[tile].AddWater(quantity); RefreshTile(tile);
        }
        public bool CanReceiveSeeds(int tile, int quantity)
        {
            if (quantity <= 0 || Tiles[tile].FreePlantCapacity < quantity ||
                mapSettings.plantCatalog == null) return false;
            var catalog = mapSettings.plantCatalog;
            var eligible = catalog.SuitableFor(Tiles[tile].Category);
            if (Array.Exists(eligible, plant => plant.isForeground)) return true;
            return quantity == 1 && Array.Exists(eligible,
                plant => Tiles[tile].CanAcceptSeed(plant, catalog));
        }
        public void AddSeeds(int tile, int quantity, int currentStep)
        {
            if (!CanReceiveSeeds(tile, quantity)) throw new InvalidOperationException("地块没有适宜品种或剩余植物空间。");
            var catalog = mapSettings.plantCatalog;
            var eligible = catalog.SuitableFor(Tiles[tile].Category);
            for (int i = 0; i < quantity; i++)
            {
                var choices = Array.FindAll(eligible,
                    plant => Tiles[tile].CanAcceptSeed(plant, catalog));
                Tiles[tile].TryAddSeed(choices[UnityEngine.Random.Range(0, choices.Length)].id,
                    currentStep, catalog);
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
            RefreshThemeConversion();
            SyncPlantMarkers(map, true);
        }
        public void SyncPlantMarkers(JourneyMap map, bool fadeNewPlants)
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
                        bool fadeIn = fadeNewPlants && mapSettings.plantStageFadeSeconds > 0f;
                        marker.color = fadeIn ? WithAlpha(targetColor, 0f) : targetColor;
                        visual.stageIndex = stageIndex;
                        visual.targetColor = targetColor;
                        visual.fadeStartedAt = fadeIn ? now : -1f;
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
                    marker.transform.localScale = Vector3.one * Mathf.Max(.01f, stage.visualScale);
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
        public void CompletePlantFades(JourneyMap map)
        {
            foreach (var entry in plantVisuals)
            {
                if (plantMarkers.TryGetValue(entry.Key, out var marker) && marker != null)
                    marker.color = entry.Value.targetColor;
                ClearPlantFade(map, entry.Value);
            }
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
