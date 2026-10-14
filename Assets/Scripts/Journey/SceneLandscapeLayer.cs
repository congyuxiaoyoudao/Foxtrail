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
        StationTheme artTheme;
        bool transitionActive;
        float transitionCenter;
        float transitionAlpha = 1f;
        int baseSortingOrder;
        int sortingOffset;

        public void Configure(JourneyMap owner, RingMapSettings source, Station home,
            SpriteRenderer renderer, int index)
        {
            map = owner;
            settings = source;
            station = home;
            image = renderer;
            layerIndex = index;
            baseSortingOrder = renderer.sortingOrder;
            artTheme = home.theme;
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
            baseSortingOrder = GetComponent<MeshRenderer>().sortingOrder;
            artTheme = home.theme;
            RefreshPosition();
        }

        public int LayerIndex => layerIndex;
        public StationTheme ArtTheme => artTheme;
        // 整站地块转变后换用别的主题素材：0～2 是远景，3 是中景。
        public void SetArtTheme(StationTheme theme)
        {
            if (artTheme == theme) return;
            artTheme = theme;
            RefreshPosition();
        }
        static Sprite SpriteFor(SceneThemeLayers layers, int index) => index == 0 ? layers.farSky :
            index == 1 ? layers.farClouds : index == 2 ? layers.farMountains : layers.middle;

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
            if (map == null || settings == null || station == null)
                return;
            var layers = settings.sceneThemes != null ? settings.sceneThemes.For(artTheme) : null;
            if (layers == null) return;
            if (layerIndex >= 0 && image != null)
            {
                var art = SpriteFor(layers, layerIndex);
                if (image.sprite != art) image.sprite = art;
            }
            var sprite = layerIndex < 0 ? fallbackSprite : image != null ? image.sprite : null;
            if (sprite == null) return;
            float widthScale = station.length / Mathf.Max(.001f, sprite.bounds.size.x);
            var layout = settings.sceneLayout;
            float baseY = settings.sceneLayout != null ? settings.sceneLayout.landscapeBaseY : 0f;
            float heightScale = Mathf.Max(.1f, settings.landscapeHeightScale);
            float center = transitionActive ? transitionCenter : station.startDistance + station.length * .5f;
            if (layerIndex < 0)
            {
                map.UpdateRouteObject(transform, new Vector3(
                    center,
                    settings.ringThickness * .5f + baseY + (layout != null ? layout.fallbackY : 0f),
                    Mathf.Max(0, settings.landscapeDepth)));
                transform.localScale = new Vector3(station.length,
                    sprite.bounds.size.y * widthScale * heightScale, 1);
                GetComponent<MeshRenderer>().sortingOrder = baseSortingOrder + sortingOffset;
                return;
            }
            image.sortingOrder = baseSortingOrder + sortingOffset;
            var color = image.color;
            color.a = transitionAlpha;
            image.color = color;
            float height = sprite.bounds.size.y * widthScale * heightScale;
            map.UpdateRouteObject(transform, new Vector3(
                center,
                settings.ringThickness * .5f + baseY + height * .5f +
                    (layout != null ? layout.LandscapeY(layerIndex) : 0f),
                Mathf.Max(0, settings.landscapeDepth) - layerIndex * .015f));
            transform.localScale = new Vector3(widthScale, widthScale * heightScale, 1);
        }

        void LateUpdate() => RefreshPosition();
    }
}
