using UnityEngine;
using UnityEngine.InputSystem;
namespace Hiking.Journey
{
    public class CameraController : MonoBehaviour
    {
        public Camera WorldCamera;
        [Header("直线旅途与长卷浏览")]
        [Range(30, 80)] public float overviewPitchAngle = 45;
        GameFlowController flow;
        bool dragging;
        Vector2 lastPointer;
        TextMesh[] labels;

        float journeyCenter;
        bool blendingStationLandscapes;
        float landscapeBlend;
        float debugCenter;
        float scrollCenter;
        float ScrollViewWidth => flow.Map.stations[0].length;
        float ScrollMinimum => ScrollViewWidth * .5f - flow.Map.TileWidth * .5f;
        float ScrollMaximum => flow.Map.RouteLength - ScrollViewWidth * .5f + flow.Map.TileWidth * .5f;
        public float ScrollProgress01 => flow == null || flow.Map == null ? 0f :
            Mathf.InverseLerp(ScrollMinimum, ScrollMaximum, scrollCenter);
        public bool CanScrollLeft => flow != null && flow.Map != null && scrollCenter > ScrollMinimum + .001f;
        public bool CanScrollRight => flow != null && flow.Map != null && scrollCenter < ScrollMaximum - .001f;
        public void ShowScrollAtStart()
        {
            if (flow.Map == null) return;
            ResetCamera();
            scrollCenter = ScrollMinimum;
            FrameScroll();
        }
        public void PanScroll(float distance)
        {
            if (flow == null || !flow.IsBrowsingPanorama || flow.Map == null) return;
            scrollCenter = Mathf.Clamp(scrollCenter + distance, ScrollMinimum, ScrollMaximum);
            FrameScroll();
        }
        public void PanScrollBySection(int direction) => PanScroll(direction * ScrollViewWidth);
        void FrameScroll()
        {
            var map = flow.Map;
            float width = ScrollViewWidth;
            WorldCamera.ResetProjectionMatrix(); WorldCamera.orthographic = true;
            WorldCamera.orthographicSize = width / (2 * Mathf.Max(.1f, WorldCamera.aspect));
            var rotation = Quaternion.Euler(overviewPitchAngle, 0, 0);
            var target = map.transform.position + new Vector3(scrollCenter - map.RouteLength * .5f, .6f, 0);
            target += rotation * Vector3.up * (2 * WorldCamera.orthographicSize *
                (map.sourceSettings.sceneLayout?.cameraFrameHeightRatio ?? 0f));
            WorldCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 30, rotation);
        }
        public float DebugCenter => debugCenter;
        float DebugViewWidth => Mathf.Min(flow.Map.RouteLength + flow.Map.TileWidth,
            Mathf.Max(6 * flow.Map.TileWidth, 3.375f * WorldCamera.aspect));
        public void ShowDebugMap()
        {
            ResetCamera();
            debugCenter = DebugViewWidth * .5f - flow.Map.TileWidth * .5f;
            FrameDebugMap();
        }
        public void PanDebug(float distance)
        {
            if (flow == null || !flow.IsDebugMode || flow.Map == null) return;
            float width = DebugViewWidth;
            float padding = flow.Map.TileWidth * .5f;
            debugCenter = Mathf.Clamp(debugCenter + distance, width * .5f - padding,
                flow.Map.RouteLength - width * .5f + padding);
            FrameDebugMap();
        }
        void FrameDebugMap()
        {
            var map = flow.Map;
            float width = DebugViewWidth;
            debugCenter = Mathf.Clamp(debugCenter, width * .5f - map.TileWidth * .5f,
                map.RouteLength - width * .5f + map.TileWidth * .5f);
            WorldCamera.ResetProjectionMatrix(); WorldCamera.orthographic = true;
            WorldCamera.orthographicSize = width / (2 * Mathf.Max(.1f, WorldCamera.aspect));
            var rotation = Quaternion.Euler(overviewPitchAngle, 0, 0);
            var target = map.transform.position + new Vector3(debugCenter - map.RouteLength * .5f, 0, 0)
                - rotation * Vector3.up * .7f;
            WorldCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 30, rotation);
        }
        void BrowseDebugMap()
        {
            var pointer = Pointer.current;
            if (pointer != null)
            {
                Vector2 position = pointer.position.ReadValue();
                if (!Application.isFocused || !pointer.press.isPressed) dragging = false;
                if (Application.isFocused && pointer.press.wasPressedThisFrame && !PointerUtility.OverUI(position))
                { dragging = true; lastPointer = position; }
                if (dragging)
                {
                    PanDebug(-(position.x - lastPointer.x) * DebugViewWidth / Mathf.Max(1, WorldCamera.pixelWidth));
                    lastPointer = position;
                }
                if (Application.isFocused && !PointerUtility.OverUI(position) && Mouse.current != null)
                {
                    Vector2 scroll = Mouse.current.scroll.ReadValue();
                    PanDebug((scroll.x - scroll.y) / 120f * flow.Map.TileWidth * 2);
                }
            }
            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null)
            {
                float direction = (keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0);
                PanDebug(direction * flow.Map.TileWidth * 8 * Time.unscaledDeltaTime);
            }
            FrameDebugMap();
        }
        public float JourneyViewWidth => flow.CurrentStation.length;
        public void Initialize(GameFlowController owner)
        {
            flow = owner; WorldCamera.orthographic = true;
        }
        public void ResetCamera()
        {
            dragging = false; labels = null;
            WorldCamera.ResetProjectionMatrix();
        }
        public void ShowStation(Station station)
        {
            if (flow.Map == null) return;
            journeyCenter = station.startDistance + JourneyViewWidth * .5f;
            FrameJourney();
        }
        void LateUpdate()
        {
            if (flow == null || flow.Map == null) return;
            if (flow.IsDebugMode) BrowseDebugMap();
            else if (flow.IsBrowsingPanorama) FrameScroll();
            else FrameJourney();
            if (flow.Traveler.gameObject.activeSelf) flow.Traveler.FaceCamera(WorldCamera);
            flow.Map.FrameLandscapes(WorldCamera,
                !flow.IsDebugMode && !flow.IsBrowsingPanorama && blendingStationLandscapes ?
                    flow.Session.StationIndex : -1,
                landscapeBlend, journeyCenter);
            if (labels == null) labels = flow.Map.GetComponentsInChildren<TextMesh>(true);
            foreach (var label in labels) label.transform.rotation = WorldCamera.transform.rotation;
        }
        void FrameJourney()
        {
            var map = flow.Map;
            blendingStationLandscapes = false;
            landscapeBlend = 0f;
            WorldCamera.ResetProjectionMatrix();
            float width = JourneyViewWidth;
            var station = flow.CurrentStation;
            journeyCenter = station.startDistance + width * .5f;
            if (flow.Session.StationIndex < map.stations.Length - 1)
            {
                var nextStation = map.stations[flow.Session.StationIndex + 1];
                float lastTileCenter = station.TileCenter(station.tileCount - 1);
                float foxDistance = map.RouteDistanceAtProgress(flow.Traveler.Progress01);
                if (foxDistance >= lastTileCenter)
                {
                    blendingStationLandscapes = true;
                    landscapeBlend = Mathf.InverseLerp(lastTileCenter,
                        nextStation.TileCenter(0), foxDistance);
                }
                int startTile = station.tileCount - Mathf.Clamp(flow.Config.cameraTransitionStartFromEnd, 1, station.tileCount);
                float walkedTiles = Mathf.Max(0, (foxDistance - station.TileCenter(startTile)) / station.TileWidth);
                int remainingSteps = station.tileCount - 1 - startTile;
                // Weight each segment by its duration so the slower crossing does not change camera speed.
                // Deriving progress from the fox also freezes the camera during decisions.
                float duration = remainingSteps * flow.MoveSeconds + flow.StationTransitionSeconds;
                float elapsed = Mathf.Min(walkedTiles, remainingSteps) * flow.MoveSeconds
                    + Mathf.Clamp01(walkedTiles - remainingSteps) * flow.StationTransitionSeconds;
                float destination = map.stations[flow.Session.StationIndex + 1].startDistance + width * .5f;
                journeyCenter = Mathf.Lerp(journeyCenter, destination, elapsed / duration);
            }
            float cameraX = map.transform.position.x - map.RouteLength * .5f + journeyCenter;
            WorldCamera.orthographic = true;
            WorldCamera.orthographicSize = width / (2 * Mathf.Max(.1f, WorldCamera.aspect));
            var rotation = Quaternion.Euler(overviewPitchAngle, 0, 0);
            var target = new Vector3(cameraX, map.transform.position.y + .6f, map.transform.position.z);
            // Moving the camera up in screen space lowers the entire route without changing gameplay coordinates.
            target += rotation * Vector3.up * (2 * WorldCamera.orthographicSize *
                (map.sourceSettings.sceneLayout?.cameraFrameHeightRatio ?? 0f));
            WorldCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 30, rotation);
        }
    }
}
