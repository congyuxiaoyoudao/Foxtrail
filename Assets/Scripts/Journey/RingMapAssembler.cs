using System;
using UnityEngine;
namespace Hiking.Journey
{
    // Saved provider entry point retained; generation now uses straight route coordinates.
    public static class RingMapAssembler
    {
        // Snow and desert can meet only after a grass station has separated them.
        public static StationTheme ChooseTheme(StationTheme? previous)
        {
            if (previous == StationTheme.Snow)
                return UnityEngine.Random.Range(0, 2) == 0 ? StationTheme.Snow : StationTheme.Grass;
            if (previous == StationTheme.Desert)
                return UnityEngine.Random.Range(0, 2) == 0 ? StationTheme.Desert : StationTheme.Grass;
            return (StationTheme)UnityEngine.Random.Range(0, 3);
        }

        // themes 为空时按相邻规则随机；读档时传入存档里的主题，
        // 让远景贴图、路面颜色、地块初始化三处一次性就对。
        public static JourneyMap Build(RingMapSettings settings, Transform parent, StationTheme[] themes = null)
        {
            if (settings == null || settings.pathMaterial == null || settings.blockCount < 1)
                throw new InvalidOperationException("请配置路线设置与路径材质。");
            var root = new GameObject("JourneyRoute");
            root.transform.SetParent(parent, false);
            var map = root.AddComponent<JourneyMap>();
            try
            {
                map.sourceSettings = settings;
                map.RouteLength = Mathf.Max(2, settings.stationLength) * settings.blockCount;
                map.stations = new Station[settings.blockCount];
                float section = map.RouteLength / settings.blockCount;
                int patches = RingMapSettings.TilesPerStation;
                for (int i = 0; i < map.stations.Length; i++)
                {
                    var station = new GameObject("Station_" + (i + 1).ToString("00")).AddComponent<Station>();
                    station.transform.SetParent(root.transform, false);
                    station.stationId = "station-" + (i + 1);
                    station.startDistance = i * section; station.length = section; station.tileCount = patches;
                    station.theme = themes != null && i < themes.Length
                        ? themes[i]
                        : ChooseTheme(i == 0 ? (StationTheme?)null : map.stations[i - 1].theme);
                    station.cameraRange = new Vector2(i * section, (i + 1) * section);
                    map.stations[i] = station;
                    station.InitializeTiles(settings);
                    var themeLayers = settings.sceneThemes != null ? settings.sceneThemes.For(station.theme) : null;
                    if (themeLayers != null && themeLayers.HasLandscape)
                        AddThemeLayers(map, station, settings, themeLayers);
                    else
                    {
                        var landscape = themeLayers != null && themeLayers.fallbackLandscape != null ?
                            themeLayers.fallbackLandscape : settings.LandscapeFor(station.theme);
                        if (landscape != null)
                        {
                            if (settings.landscapeMaterial == null)
                                throw new InvalidOperationException("请为主题远景配置材质。");
                            var backdrop = Anchor(map, station.transform, "Landscape_" + station.theme,
                                new Vector3(station.startDistance + station.length * .5f,
                                    settings.ringThickness * .5f +
                                        (settings.sceneLayout != null ? settings.sceneLayout.landscapeBaseY : 0f),
                                    Mathf.Max(0, settings.landscapeDepth)));
                            var mesh = new Mesh { name = "LandscapeQuad" };
                            mesh.vertices = new[] { new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0),
                                new Vector3(.5f, 1, 0), new Vector3(-.5f, 1, 0) };
                            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                            mesh.RecalculateNormals(); mesh.RecalculateBounds();
                            backdrop.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                            var material = new Material(settings.landscapeMaterial) { name = "LandscapeBackdrop" };
                            material.SetTexture("_BaseMap", landscape.texture);
                            material.SetColor("_BaseColor", Color.white);
                            material.SetFloat("_Cull", 0);
                            var renderer = backdrop.gameObject.AddComponent<MeshRenderer>();
                            renderer.sharedMaterial = material;
                            renderer.sortingOrder = -1000;
                            backdrop.localScale = new Vector3(station.length,
                                station.length * landscape.bounds.size.y / Mathf.Max(.01f, landscape.bounds.size.x) *
                                Mathf.Max(.1f, settings.landscapeHeightScale), 1);
                            if (themeLayers != null)
                                backdrop.gameObject.AddComponent<SceneLandscapeLayer>().ConfigureFallback(
                                    map, settings, station, landscape);
                            map.RegisterLandscape(renderer, material, mesh);
                        }
                    }
                    if (themeLayers != null && settings.fallbackPlantSprite != null)
                        AddThemeFills(map, station, settings);
                    float stopDistance = station.TileCenter(0);
                    station.stopProgress = map.ProgressAtDistance(stopDistance);
                    station.stopPoint = Anchor(map, station.transform, "StopPoint",
                        new Vector3(stopDistance, settings.ringThickness * .5f + .5f, 0));
                    map.MarkNearRouteObject(station.stopPoint);
                    station.slots = new PlacementSlot[3];
                    for (int j = 0; j < 3; j++)
                    {
                        int tile = j * 4 + 1;
                        var slot = Anchor(map, station.transform, "Slot_" + j,
                            new Vector3(station.TileCenter(tile), settings.ringThickness * .5f + .025f, 0));
                        map.MarkNearRouteObject(slot);
                        station.slots[j] = slot.gameObject.AddComponent<PlacementSlot>();
                        station.slots[j].station = station; station.slots[j].tileIndex = tile;
                        station.slots[j].slotId = station.stationId + "/slot-" + j;
                    }
                    AddPatches(map, station, i * section, section,
                        patches, settings, settings.ThemeColor(station.theme));
                    station.SyncPlantMarkers(map, false);
                }
                for (int i = 0; i < map.stations.Length - 1; i++)
                    map.stations[i].SetNextStation(map.stations[i + 1]);
                map.SyncPondVisuals();
                map.finishPoint = Anchor(map, root.transform, "FinishPoint_100Percent",
                    new Vector3(map.RouteLength - map.TileWidth * .5f, settings.ringThickness * .5f + .5f, 0));
                map.InitializeRainVisual();
                if (!map.Validate(out string error)) throw new InvalidOperationException(error);
                return map;
            }
            catch { UnityEngine.Object.Destroy(root); throw; }
        }
        static void AddThemeLayers(JourneyMap map, Station station, RingMapSettings settings,
            SceneThemeLayers layers)
        {
            var sprites = new[] { layers.farSky, layers.farClouds, layers.farMountains, layers.middle };
            for (int index = 0; index < sprites.Length; index++)
            {
                // 素材可能缺项：图层照建，等整站转变到别的类别时再把贴图补上。
                var sprite = sprites[index];
                var anchor = Anchor(map, station.transform, "Landscape_" + station.theme + "_" + index,
                    new Vector3(station.startDistance + station.length * .5f, 0, 0));
                var renderer = anchor.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = -1200 + index * 10;
                var layer = anchor.gameObject.AddComponent<SceneLandscapeLayer>();
                layer.Configure(map, settings, station, renderer, index);
                station.RegisterThemeLayer(layer);
                map.RegisterLandscape(renderer);
            }
        }
        static void AddThemeFills(JourneyMap map, Station station, RingMapSettings settings)
        {
            for (int index = 0; index < 2; index++)
            {
                bool ground = index == 1;
                var anchor = Anchor(map, station.transform, ground ? "GroundExtension" : "SkyExtension",
                    new Vector3(station.startDistance + station.length * .5f, 0,
                        Mathf.Max(0, settings.landscapeDepth) + .1f));
                var renderer = anchor.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = settings.fallbackPlantSprite;
                renderer.sortingOrder = ground ? -1290 : -1300;
                var fill = anchor.gameObject.AddComponent<SceneThemeFill>();
                fill.Configure(map, settings, station, renderer, ground);
                station.RegisterThemeFill(fill);
                map.RegisterLandscape(renderer);
            }
        }
        static Transform Anchor(JourneyMap map, Transform parent, string name, Vector3 route)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            map.RegisterRouteObject(point, route);
            return point;
        }
        static void AddPatches(JourneyMap map, Station station, float start, float length,
            int count, RingMapSettings settings, Color color)
        {
            for (int p = 0; p < count; p++)
            {
                var obj = new GameObject("Patch_" + (p + 1).ToString("00"));
                obj.transform.SetParent(station.transform, false);
                var baseColor = Station.IsTransitionTile(p, count) ? settings.transitionTileColor : color;
                var mesh = CreateStrip(start + p * length / count + length / count * .015f, length / count * .97f,
                    settings.ringThickness, baseColor, obj.name);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterial = settings.pathMaterial;
                map.RegisterRouteMesh(mesh);
                station.SetPatchMesh(p, mesh, baseColor, map);
                var layers = settings.sceneThemes != null ? settings.sceneThemes.For(station.theme) : null;
                var sprite = layers != null && layers.nearGround != null ?
                    layers.nearGround : settings.fallbackPlantSprite;
                // 素材可能缺项：图层照建，等整站转变到别的类别时再把贴图补上。
if (sprite == null) continue;
                float xScale = station.TileWidth / Mathf.Max(.001f, sprite.bounds.size.x);
                float height = layers?.nearGround != null ? sprite.bounds.size.y * xScale :
                    settings.GroundPlaceholderHeight(station.TileWidth);
                float yScale = height / Mathf.Max(.001f, sprite.bounds.size.y);
                var layout = settings.sceneLayout;
                float groundY = layout != null ? layout.groundY : 0f;
                var image = Anchor(map, station.transform, "GroundTile_" + p,
                    new Vector3(station.TileCenter(p),
                        settings.ringThickness * .5f + groundY - height * .5f,
                        -.05f));
                var visual = image.gameObject.AddComponent<SpriteRenderer>();
                visual.sprite = sprite;
                visual.color = layers?.nearGround != null ? Color.white : baseColor;
                visual.sortingOrder = -900;
                image.localScale = new Vector3(xScale, yScale, 1);
                map.RegisterLandscape(visual);
                map.MarkNearRouteObject(image);
                map.SetRouteMeshCovered(mesh);
                SpriteRenderer waterOverlay = null;
                if (settings.fallbackPlantSprite != null)
                {
                    float waterHeight = Mathf.Min(.55f, height);
                    var water = Anchor(map, station.transform, "GroundWaterTint_" + p,
                        new Vector3(station.TileCenter(p),
                            settings.ringThickness * .5f + groundY - waterHeight * .5f,
                            -.055f));
                    waterOverlay = water.gameObject.AddComponent<SpriteRenderer>();
                    waterOverlay.sprite = settings.fallbackPlantSprite;
                    waterOverlay.sortingOrder = -899;
                    water.localScale = new Vector3(
                        station.TileWidth / Mathf.Max(.001f, waterOverlay.sprite.bounds.size.x),
                        waterHeight / Mathf.Max(.001f, waterOverlay.sprite.bounds.size.y), 1);
                    map.RegisterLandscape(waterOverlay);
                    map.MarkNearRouteObject(water);
                }
                station.SetPatchVisual(p, visual, waterOverlay);
            }
        }
        static Mesh CreateStrip(float start, float length, float thickness, Color color, string name)
        {
            const int segments = 4;
            var vertices = new Vector3[4 * (segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[4 * segments * 6];
            for (int face = 0; face < 4; face++)
            {
                Vector2 a, b;
                if (face == 0) { a = new Vector2(thickness / 2, -.5f); b = new Vector2(thickness / 2, .5f); }
                else if (face == 1) { a = new Vector2(-thickness / 2, .5f); b = new Vector2(-thickness / 2, -.5f); }
                else if (face == 2) { a = new Vector2(-thickness / 2, -.5f); b = new Vector2(thickness / 2, -.5f); }
                else { a = new Vector2(thickness / 2, .5f); b = new Vector2(-thickness / 2, .5f); }
                for (int n = 0; n <= segments; n++)
                {
                    int v = face * (segments + 1) * 2 + n * 2;
                    float x = start + length * n / segments;
                    vertices[v] = new Vector3(x, a.x, a.y); vertices[v+1] = new Vector3(x, b.x, b.y);
                    uv[v] = new Vector2(x, 0); uv[v+1] = new Vector2(x, 1);
                    if (n == segments) continue;
                    int t = (face * segments + n) * 6;
                    triangles[t] = v; triangles[t+1] = v+1; triangles[t+2] = v+2;
                    triangles[t+3] = v+1; triangles[t+4] = v+3; triangles[t+5] = v+2;
                }
            }
            var colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            var mesh = new Mesh { name = name, vertices = vertices, colors = colors, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}

