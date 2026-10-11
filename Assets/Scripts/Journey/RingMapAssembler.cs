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
                    station.theme = themes != null && i < themes.Length
                        ? themes[i]
                        : ChooseTheme(i == 0 ? (StationTheme?)null : map.stations[i - 1].theme);
                    station.cameraRange = new Vector2(i * section, (i + 1) * section);
                    map.stations[i] = station;
                    station.InitializeTiles(settings);
                    var landscape = settings.LandscapeFor(station.theme);
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
                        map.RegisterLandscape(renderer, material, mesh);
                    }
                    for (int tile = 0; tile < patches; tile++)
                    {
                        var anchor = Anchor(map, station.transform, "Moisture_" + tile,
                            new Vector3(station.TileCenter(tile), -.15f, -.65f));
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
                    station.slots = new PlacementSlot[3];
                    for (int j = 0; j < 3; j++)
                    {
                        int tile = j * 4 + 1;
                        var slot = Anchor(map, station.transform, "Slot_" + j,
                            new Vector3(station.TileCenter(tile), settings.ringThickness * .5f + .025f, 0));
                        station.slots[j] = slot.gameObject.AddComponent<PlacementSlot>();
                        station.slots[j].station = station; station.slots[j].tileIndex = tile;
                        station.slots[j].slotId = station.stationId + "/slot-" + j;
                        station.slots[j].CreateMarker(settings.pathMaterial, station.TileWidth * .75f);
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

