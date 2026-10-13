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

        public void Configure(JourneyMap owner, RingMapSettings source, Station home,
            SpriteRenderer renderer, bool isGround)
        {
            map = owner;
            settings = source;
            station = home;
            image = renderer;
            ground = isGround;
            RefreshPosition();
        }

        public void RefreshPosition()
        {
            if (map == null || settings == null || station == null || image == null || image.sprite == null)
                return;
            var layers = settings.sceneThemes != null ? settings.sceneThemes.For(station.theme) : null;
            if (layers == null) return;
            image.color = ground ? layers.groundFillColor : layers.skyFillColor;
            float top = settings.ringThickness * .5f + settings.landscapeBaseHeight +
                layers.landscapeYOffset + (layers.HasLandscape ? layers.middleOffset.y : layers.fallbackOffset.y);
            // Start the solid ground just below the source art edge. The sprite's
            // transparent top pixels otherwise expose a thin ground-colored stripe.
            float centerY = ground ? top - 10.2f : 0f;
            map.UpdateRouteObject(transform, new Vector3(
                station.startDistance + station.length * .5f, centerY,
                Mathf.Max(0, settings.landscapeDepth) + .1f));
            transform.localScale = new Vector3(
                station.length / Mathf.Max(.001f, image.sprite.bounds.size.x),
                20f / Mathf.Max(.001f, image.sprite.bounds.size.y), 1);
        }

        void LateUpdate() => RefreshPosition();
    }
}
