using UnityEngine;

namespace Hiking.Journey
{
    // Reads the theme asset while Play mode is running, so Inspector edits appear immediately.
    public sealed class SceneLandscapeLayer : MonoBehaviour
    {
        JourneyMap map;
        RingMapSettings settings;
        Station station;
        SpriteRenderer image;
        Sprite fallbackSprite;
        int layerIndex;

        public void Configure(JourneyMap owner, RingMapSettings source, Station home,
            SpriteRenderer renderer, int index)
        {
            map = owner;
            settings = source;
            station = home;
            image = renderer;
            layerIndex = index;
            RefreshPosition();
        }

        public void ConfigureFallback(JourneyMap owner, RingMapSettings source, Station home,
            Sprite sprite)
        {
            map = owner;
            settings = source;
            station = home;
            fallbackSprite = sprite;
            layerIndex = -1;
            RefreshPosition();
        }

        public void RefreshPosition()
        {
            if (map == null || settings == null || station == null)
                return;
            var layers = settings.sceneThemes != null ? settings.sceneThemes.For(station.theme) : null;
            if (layers == null) return;
            var sprite = layerIndex < 0 ? fallbackSprite : image != null ? image.sprite : null;
            if (sprite == null) return;
            float widthScale = station.length / Mathf.Max(.001f, sprite.bounds.size.x);
            float layerHeightMultiplier = layerIndex == 3 ? layers.middleHeightMultiplier :
                layers.farHeightMultiplier;
            float heightScale = Mathf.Max(.1f, settings.landscapeHeightScale) *
                Mathf.Max(.1f, layerHeightMultiplier);
            if (layerIndex < 0)
            {
                Vector2 fallbackPosition = layers.fallbackOffset;
                map.UpdateRouteObject(transform, new Vector3(
                    station.startDistance + station.length * .5f + fallbackPosition.x,
                    settings.ringThickness * .5f + settings.landscapeBaseHeight +
                        layers.landscapeYOffset + fallbackPosition.y,
                    Mathf.Max(0, settings.landscapeDepth)));
                transform.localScale = new Vector3(station.length,
                    sprite.bounds.size.y * widthScale * heightScale, 1);
                return;
            }
            float height = sprite.bounds.size.y * widthScale * heightScale;
            Vector2 offset = layers.OffsetFor(layerIndex);
            map.UpdateRouteObject(transform, new Vector3(
                station.startDistance + station.length * .5f + offset.x,
                settings.ringThickness * .5f + settings.landscapeBaseHeight +
                    layers.landscapeYOffset + height * .5f + offset.y,
                Mathf.Max(0, settings.landscapeDepth) - layerIndex * .015f));
            transform.localScale = new Vector3(widthScale, widthScale * heightScale, 1);
        }

        void LateUpdate() => RefreshPosition();
    }
}
