using UnityEngine;

namespace Hiking.Journey
{
    // Solid extensions fill the screen outside the unscaled landscape artwork.
    public sealed class SceneThemeFill : MonoBehaviour
    {
        JourneyMap map;
        RingMapSettings settings;
        Station station;
        SpriteRenderer image;
        StationTheme fillTheme;
        SceneLandscapeLayer middleLayer;
        bool ground;
        bool transitionActive;
        float transitionCenter;
        float transitionAlpha = 1f;
        int baseSortingOrder;
        public bool IsGround => ground;
        public StationTheme FillTheme => fillTheme;
        // 天空填充跟随远景、地面填充跟随中景，两者可以各自切换。
        public void SetFillTheme(StationTheme theme)
        {
            if (fillTheme == theme) return;
            fillTheme = theme;
            RefreshPosition();
        }
        int sortingOffset;

        public void Configure(JourneyMap owner, RingMapSettings source, Station home,
            SpriteRenderer renderer, bool isGround)
        {
            map = owner;
            settings = source;
            station = home;
            image = renderer;
            fillTheme = home.theme;
            ground = isGround;
            baseSortingOrder = renderer.sortingOrder;
            RefreshPosition();
        }

        public void SetTransition(bool active, float center, float alpha, int orderOffset)
        {
            transitionActive = active;
            transitionCenter = center;
            transitionAlpha = active ? Mathf.Clamp01(alpha) : 1f;
            sortingOffset = active ? orderOffset : 0;
            RefreshPosition();
        }

        public void RefreshPosition()
        {
            if (map == null || settings == null || station == null || image == null || image.sprite == null)
                return;
            var layers = settings.sceneThemes != null ? settings.sceneThemes.For(fillTheme) : null;
            if (layers == null) return;
            var color = ground ? layers.groundFillColor : layers.skyFillColor;
            color.a *= transitionAlpha;
            image.color = color;
            image.sortingOrder = baseSortingOrder + sortingOffset;
            var layout = settings.sceneLayout;
            float top = settings.ringThickness * .5f +
                (settings.sceneLayout != null ? settings.sceneLayout.landscapeBaseY : 0f) +
                (layout != null ? (layers.HasLandscape ? layout.middleY : layout.fallbackY) : 0f);
            map.UpdateRouteObject(transform, new Vector3(
                transitionActive ? transitionCenter : station.startDistance + station.length * .5f,
                ground ? top : 0f,
                Mathf.Max(0, settings.landscapeDepth) + .1f));
            transform.localScale = new Vector3(
                station.length / Mathf.Max(.001f, image.sprite.bounds.size.x),
                20f / Mathf.Max(.001f, image.sprite.bounds.size.y), 1);
            if (ground)
            {
                // Follow the middle sprite's rendered bottom edge, including its transition offset.
                if (middleLayer == null && layers.middle != null)
                    middleLayer = station.transform.Find("Landscape_" + station.theme + "_3")?
                        .GetComponent<SceneLandscapeLayer>();
                Vector3 topEdge = transform.position;
                if (middleLayer != null)
                {
                    middleLayer.RefreshPosition();
                    var middle = middleLayer.GetComponent<SpriteRenderer>();
                    if (middle != null && middle.sprite != null)
                        topEdge = middle.transform.TransformPoint(
                            new Vector3(0f, middle.sprite.bounds.min.y, 0f));
                }
                transform.position = topEdge - transform.TransformVector(
                    new Vector3(0f, image.sprite.bounds.max.y, 0f)) - transform.up * .02f;
            }
        }

        void LateUpdate() => RefreshPosition();
    }
}
