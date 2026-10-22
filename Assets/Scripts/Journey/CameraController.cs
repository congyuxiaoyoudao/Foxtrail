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

        // UI_Fullview_Background is drawn aspect-fit inside the 1152x720 FrameRoot.
        // Its transparent landscape opening spans x=14.36%-85.62%, y=18.87%-89.37%.
        static Rect FullviewViewport
        {
            get
            {
                const float artHeight = 1152f * 5984f / 10668f;
                float bottomMargin = (720f - artHeight) * .5f;
                return new Rect(.1436f, (bottomMargin + artHeight * .1887f) / 720f,
                    .7126f, artHeight * .705f / 720f);
            }
        }

        float journeyCenter;
        float framedAspect = SceneLayoutSettings.GameplayAspect;
        CameraClearFlags originalClearFlags;
        Color originalBackgroundColor;
        SpriteRenderer panoramaBackdrop;
        Sprite panoramaBackdropSprite;
        Texture2D panoramaBackdropTexture;
        bool blendingStationLandscapes;
        float landscapeBlend;
        float scrollCenter;
        float PanoramaViewWidthMultiplier => Mathf.Max(1f,
            flow.Map.sourceSettings.sceneLayout?.framedViewWidthMultiplier ?? 1.3f);
        // Keep the journey composition and zoom it out uniformly inside the same framed viewport.
        float ScrollViewWidth => Mathf.Min(flow.Map.RouteLength,
            JourneyViewWidth * PanoramaViewWidthMultiplier);
        // Preserve the original starting composition. Only limit the far end so
        // scrolling cannot expose the route's right edge.
        float ScrollRightEdgeInset => Mathf.Min(flow.Map.TileWidth * .25f,
            Mathf.Max(0f, (flow.Map.RouteLength - ScrollViewWidth) * .5f));
        float ScrollMinimum => flow.Map.stations[0].length * .5f;
        float ScrollMaximum => flow.Map.RouteLength - ScrollViewWidth * .5f - ScrollRightEdgeInset;
        public float ScrollProgress01 => flow == null || flow.Map == null ? 0f :
            Mathf.InverseLerp(ScrollMinimum, ScrollMaximum, scrollCenter);
        public bool CanScrollLeft => flow != null && flow.Map != null && scrollCenter > ScrollMinimum + .001f;
        public bool CanScrollRight => flow != null && flow.Map != null && scrollCenter < ScrollMaximum - .001f;
        public void ShowScrollAtStart()
        {
            if (flow.Map == null) return;
            ResetCamera();
            scrollCenter = ScrollMinimum;
            UpdatePanoramaBackdrop();
            FrameScroll();
            SizePanoramaBackdrop();
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
            bool fullviewVisible = flow.IsPreviewing || flow.Activity == JourneyActivity.Complete;
            Rect viewport = fullviewVisible ? FullviewViewport : layout != null ?
                layout.FramedViewport : SceneLayoutSettings.DefaultFramedViewport;
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
            var layout = map.sourceSettings.sceneLayout;
            target += rotation * Vector3.up * (2 * WorldCamera.orthographicSize *
                ((layout?.cameraFrameHeightRatio ?? 0f) +
                    (layout?.panoramaFrameHeightOffsetRatio ?? 0f)));
            WorldCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 30, rotation);
        }
        public float JourneyViewWidth => flow.CurrentStation.length * Mathf.Max(.85f,
            flow.Map.sourceSettings.sceneLayout?.journeyViewWidthInStations ?? 1f);
        public void Initialize(GameFlowController owner)
        {
            flow = owner; WorldCamera.orthographic = true;
            originalClearFlags = WorldCamera.clearFlags;
            originalBackgroundColor = WorldCamera.backgroundColor;
            authoredFrameRoot = owner.UI != null
                ? owner.UI.transform.Find("FrameRoot") as RectTransform : null;
        }
        void UpdatePanoramaBackdrop()
        {
            if (flow.IsBrowsingPanorama)
            {
                WorldCamera.clearFlags = CameraClearFlags.SolidColor;
                WorldCamera.backgroundColor = Color.black;
                if (panoramaBackdrop == null) CreatePanoramaBackdrop();
                panoramaBackdrop.enabled = true;
            }
            else
            {
                WorldCamera.clearFlags = originalClearFlags;
                WorldCamera.backgroundColor = originalBackgroundColor;
                if (panoramaBackdrop != null) panoramaBackdrop.enabled = false;
            }
        }
        void CreatePanoramaBackdrop()
        {
            panoramaBackdropTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            panoramaBackdropTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            panoramaBackdropTexture.Apply();
            panoramaBackdropSprite = Sprite.Create(panoramaBackdropTexture,
                new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2f);
            var backdrop = new GameObject("Opaque Panorama Backdrop");
            backdrop.transform.SetParent(WorldCamera.transform, false);
            panoramaBackdrop = backdrop.AddComponent<SpriteRenderer>();
            panoramaBackdrop.sprite = panoramaBackdropSprite;
            panoramaBackdrop.color = Color.black;
            panoramaBackdrop.sortingOrder = -32000;
        }
        void SizePanoramaBackdrop()
        {
            if (panoramaBackdrop == null || !panoramaBackdrop.enabled) return;
            var backdrop = panoramaBackdrop.transform;
            backdrop.localPosition = new Vector3(0f, 0f, WorldCamera.farClipPlane - 1f);
            float height = WorldCamera.orthographicSize * 2.04f;
            backdrop.localScale = new Vector3(height * WorldCamera.aspect, height, 1f);
        }
        void OnDestroy()
        {
            if (panoramaBackdrop != null) Destroy(panoramaBackdrop.gameObject);
            if (panoramaBackdropSprite != null) Destroy(panoramaBackdropSprite);
            if (panoramaBackdropTexture != null) Destroy(panoramaBackdropTexture);
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
            UpdatePanoramaBackdrop();
            if (flow.IsBrowsingPanorama) FrameScroll();
            else FrameJourney();
            SizePanoramaBackdrop();
            if (flow.Traveler.gameObject.activeSelf) flow.Traveler.FaceCamera(WorldCamera);
            flow.Map.FrameLandscapes(WorldCamera,
                !flow.IsBrowsingPanorama && blendingStationLandscapes ?
                    flow.Session.StationIndex : -1,
                landscapeBlend, journeyCenter,
                flow.Session.StationIndex, !flow.IsBrowsingPanorama);
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
