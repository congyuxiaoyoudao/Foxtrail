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
        bool ground;
        StationTheme fillTheme;
        bool transitionActive;
        float transitionCenter;
        float transitionAlpha = 1f;
        int baseSortingOrder;
        int sortingOffset;

        public void Configure(JourneyMap owner, RingMapSettings source, Station home,
            SpriteRenderer renderer, bool isGround)
        {
            map = owner;
            settings = source;
            station = home;
            image = renderer;
            ground = isGround;
            baseSortingOrder = renderer.sortingOrder;
            fillTheme = home.theme;
            RefreshPosition();
        }

        public bool IsGround => ground;
        public StationTheme FillTheme => fillTheme;
        // 天空填充跟随远景、地面填充跟随中景，两者可以各自切换。
        public void SetFillTheme(StationTheme theme)
        {
            if (fillTheme == theme) return;
            fillTheme = theme;
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
            // Start the solid ground just below the source art edge. The sprite's
            // transparent top pixels otherwise expose a thin ground-colored stripe.
            float centerY = ground ? top - 10.2f : 0f;
            map.UpdateRouteObject(transform, new Vector3(
                transitionActive ? transitionCenter : station.startDistance + station.length * .5f,
                centerY,
                Mathf.Max(0, settings.landscapeDepth) + .1f));
            transform.localScale = new Vector3(
                station.length / Mathf.Max(.001f, image.sprite.bounds.size.x),
                20f / Mathf.Max(.001f, image.sprite.bounds.size.y), 1);
        }

        void LateUpdate() => RefreshPosition();
    }
}
