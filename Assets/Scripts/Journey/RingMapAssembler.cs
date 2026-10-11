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

        public static JourneyMap Build(RingMapSettings settings, Transform parent)
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
                map.radius = map.RouteLength / (2 * Mathf.PI);
                map.stations = new Station[settings.blockCount];
                float section = map.RouteLength / settings.blockCount;
                int patches = RingMapSettings.TilesPerStation;
                for (int i = 0; i < map.stations.Length; i++)
                {
                    var station = new GameObject("Station_" + (i + 1).ToString("00")).AddComponent<Station>();
                    station.transform.SetParent(root.transform, false);
                    station.stationId = "station-" + (i + 1);
                    station.startDistance = i * section; station.length = section; station.tileCount = patches;
                    station.theme = ChooseTheme(i == 0 ? (StationTheme?)null : map.stations[i - 1].theme);
                    station.cameraRange = new Vector2(i * section, (i + 1) * section);
                    map.stations[i] = station;
                    station.InitializeTiles(settings);
                    var themeLayers = settings.sceneThemes != null ? settings.sceneThemes.For(station.theme) : null;
                    if (themeLayers != null && settings.fallbackPlantSprite != null)
                        AddThemeFills(map, station, settings);
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
                                    settings.ringThickness * .5f + settings.landscapeBaseHeight,
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
                    for (int tile = 0; tile < patches; tile++)
                    {
                        var anchor = Anchor(map, station.transform, "Moisture_" + tile,
                            new Vector3(station.TileCenter(tile), -.15f, -.65f));
                        map.MarkNearRouteObject(anchor);
                        var label = anchor.gameObject.AddComponent<TextMesh>();
                        label.fontSize = 48; label.characterSize = .028f * Mathf.Min(1, station.TileWidth);
                        label.anchor = TextAnchor.MiddleCenter; label.color = new Color(.65f,.87f,1);
                        if (settings.tileLabelFont != null)
                        {
                            label.font = settings.tileLabelFont;
                            label.GetComponent<MeshRenderer>().sharedMaterial = settings.tileLabelFont.material;
                        }
                        station.SetMoistureLabel(tile, label);
                    }
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
                    station.SyncPlantMarkers(map);
                }
                map.finishPoint = Anchor(map, root.transform, "FinishPoint_100Percent",
                    new Vector3(map.RouteLength - map.TileWidth * .5f, settings.ringThickness * .5f + .5f, 0));
                map.InitializeRainVisual();
                map.SetBend(0);
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
                var sprite = sprites[index];
                if (sprite == null) continue;
                var anchor = Anchor(map, station.transform, "Landscape_" + station.theme + "_" + index,
                    new Vector3(station.startDistance + station.length * .5f, 0, 0));
                var renderer = anchor.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = -1200 + index * 10;
                anchor.gameObject.AddComponent<SceneLandscapeLayer>().Configure(
                    map, settings, station, renderer, index);
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
                anchor.gameObject.AddComponent<SceneThemeFill>().Configure(
                    map, settings, station, renderer, ground);
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
                var baseColor = Station.IsTransitionTile(p) ? settings.transitionTileColor : color;
                var mesh = CreateStrip(start + p * length / count + length / count * .015f, length / count * .97f,
                    settings.ringThickness, baseColor, obj.name);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterial = settings.pathMaterial;
                map.RegisterRouteMesh(mesh);
                station.SetPatchMesh(p, mesh, baseColor, map);
                var layers = settings.sceneThemes != null ? settings.sceneThemes.For(station.theme) : null;
                if (layers == null || layers.nearGround == null) continue;
                var sprite = layers.nearGround;
                float xScale = station.TileWidth / Mathf.Max(.001f, sprite.bounds.size.x);
                float yScale = xScale * Mathf.Max(.1f, layers.nearHeightMultiplier);
                float height = sprite.bounds.size.y * yScale;
                var image = Anchor(map, station.transform, "GroundTile_" + p,
                    new Vector3(station.TileCenter(p),
                        settings.ringThickness * .5f + layers.nearGroundTopOffset - height * .5f,
                        -.05f));
                var visual = image.gameObject.AddComponent<SpriteRenderer>();
                visual.sprite = sprite;
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
                            settings.ringThickness * .5f + layers.nearGroundTopOffset - waterHeight * .5f,
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

