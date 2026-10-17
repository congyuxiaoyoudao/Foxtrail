using UnityEngine;
namespace Hiking.Journey
{
    public class CameraController : MonoBehaviour
    {
        public Camera WorldCamera;
        [Header("直线旅途与长卷浏览")]
        [Range(30, 80)] public float overviewPitchAngle = 45;
        GameFlowController flow;
        RectTransform authoredFrameRoot;
        TextMesh[] labels;
        readonly Vector3[] frameCorners = new Vector3[4];

        float journeyCenter;
        float framedAspect = SceneLayoutSettings.GameplayAspect;
        bool blendingStationLandscapes;
        float landscapeBlend;
        float scrollCenter;
        float PanoramaViewWidthMultiplier => Mathf.Max(1f,
            flow.Map.sourceSettings.sceneLayout?.framedViewWidthMultiplier ?? 1.3f);
        // Keep the journey composition and zoom it out uniformly inside the same framed viewport.
        float ScrollViewWidth => JourneyViewWidth * PanoramaViewWidthMultiplier;
        float ScrollMinimum => flow.Map.stations[0].length * .5f;
        float ScrollMaximum => flow.Map.RouteLength - flow.Map.stations[0].length * .5f;
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
        public void PanScrollBySection(int direction) => PanScroll(direction * flow.Map.stations[0].length);
        void ApplyFramedViewport()
        {
            var layout = flow.Map.sourceSettings.sceneLayout;
            Rect viewport = layout != null ? layout.FramedViewport :
                SceneLayoutSettings.DefaultFramedViewport;
            var frameRoot = WindowsTransparentWindow.ActiveFrameRoot != null
                ? WindowsTransparentWindow.ActiveFrameRoot : authoredFrameRoot;
            if (frameRoot == null)
            {
                WorldCamera.rect = viewport;
                framedAspect = Mathf.Max(.1f, Screen.width * viewport.width /
                    (Screen.height * viewport.height));
                WorldCamera.aspect = framedAspect;
                return;
            }

            frameRoot.GetWorldCorners(frameCorners);
            Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(null, frameCorners[0]);
            Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(null, frameCorners[2]);
            float width = upperRight.x - lowerLeft.x;
            float height = upperRight.y - lowerLeft.y;
            framedAspect = Mathf.Max(.1f, width * viewport.width /
                (height * viewport.height));
            WorldCamera.rect = new Rect(
                (lowerLeft.x + width * viewport.x) / Screen.width,
                (lowerLeft.y + height * viewport.y) / Screen.height,
                width * viewport.width / Screen.width,
                height * viewport.height / Screen.height);
            WorldCamera.aspect = framedAspect;
        }
        void FrameScroll()
        {
            var map = flow.Map;
            float width = ScrollViewWidth;
            ApplyFramedViewport();
            WorldCamera.ResetProjectionMatrix(); WorldCamera.orthographic = true;
            WorldCamera.orthographicSize = width / (2 * framedAspect);
            var rotation = Quaternion.Euler(overviewPitchAngle, 0, 0);
            var target = map.transform.position + new Vector3(scrollCenter - map.RouteLength * .5f, .6f, 0);
            target += rotation * Vector3.up * (2 * WorldCamera.orthographicSize *
                (map.sourceSettings.sceneLayout?.cameraFrameHeightRatio ?? 0f));
            WorldCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 30, rotation);
        }
        public float JourneyViewWidth => flow.CurrentStation.length * Mathf.Max(.85f,
            flow.Map.sourceSettings.sceneLayout?.journeyViewWidthInStations ?? 1f);
        public void Initialize(GameFlowController owner)
        {
            flow = owner; WorldCamera.orthographic = true;
            authoredFrameRoot = owner.UI != null
                ? owner.UI.transform.Find("FrameRoot") as RectTransform : null;
        }
        public void ResetCamera()
        {
            labels = null;
            WorldCamera.ResetProjectionMatrix();
        }
        public void ShowStation(Station station)
        {
            if (flow.Map == null) return;
            journeyCenter = station.startDistance + station.length * .5f;
            FrameJourney();
        }
        void LateUpdate()
        {
            if (flow == null || flow.Map == null) return;
            if (flow.IsBrowsingPanorama) FrameScroll();
            else FrameJourney();
            if (flow.Traveler.gameObject.activeSelf) flow.Traveler.FaceCamera(WorldCamera);
            flow.Map.FrameLandscapes(WorldCamera,
                !flow.IsBrowsingPanorama && blendingStationLandscapes ?
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
            journeyCenter = station.startDistance + station.length * .5f;
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
                var destinationStation = map.stations[flow.Session.StationIndex + 1];
                float destination = destinationStation.startDistance + destinationStation.length * .5f;
                journeyCenter = Mathf.Lerp(journeyCenter, destination, elapsed / duration);
            }
            float cameraX = map.transform.position.x - map.RouteLength * .5f + journeyCenter;
            ApplyFramedViewport();
            WorldCamera.orthographic = true;
            WorldCamera.orthographicSize = width / (2 * framedAspect);
            var rotation = Quaternion.Euler(overviewPitchAngle, 0, 0);
            var target = new Vector3(cameraX, map.transform.position.y + .6f, map.transform.position.z);
            // Moving the camera up in screen space lowers the entire route without changing gameplay coordinates.
            target += rotation * Vector3.up * (2 * WorldCamera.orthographicSize *
                (map.sourceSettings.sceneLayout?.cameraFrameHeightRatio ?? 0f));
            WorldCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 30, rotation);
        }
    }
}
