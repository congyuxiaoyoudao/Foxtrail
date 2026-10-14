using UnityEngine;

namespace Hiking.Journey
{
    // 过渡地块的一层素材片：把一个类别的图层素材按「一格宽」切片。artLayer 2 = 背景层，3 = 中景层。
    //
    // 导入时 Unity 已经裁掉了素材的透明边（sprite.bounds 就是「有画面」的那一块），
    // 所以这里直接用 sprite.bounds 算：把新素材的高度缩放到整站同层素材的高度、底边对齐，
    // 换素材后的可见范围就与相邻地块严格一致，正好盖住原来的那一段。
    public sealed class SceneTransitionPiece : MonoBehaviour
    {
        JourneyMap map;
        RingMapSettings settings;
        Station station;
        SpriteRenderer image;
        readonly Sprite[] slices = new Sprite[3];
        int tile;
        int artLayer = 2;
        int baseOrder;
        float heightRatio = 1f;
        StationTheme theme;
        bool visible;
        float transitionAlpha = 1f;
        int sortingOffset;

        public bool IsVisible => visible;
        public StationTheme Theme => theme;

        public void Configure(JourneyMap owner, RingMapSettings source, Station home, int tileIndex,
            int layer, float ratio, int sortingOrder)
        {
            map = owner;
            settings = source;
            station = home;
            tile = tileIndex;
            artLayer = layer;
            heightRatio = Mathf.Clamp(ratio, .05f, 1f);
            baseOrder = sortingOrder;
            image = gameObject.AddComponent<SpriteRenderer>();
            image.sortingOrder = sortingOrder;
            if (map != null) map.RegisterRouteObject(transform, Vector3.zero);
            SetTheme(null);
        }

        public void SetTheme(StationTheme? value)
        {
            visible = value.HasValue;
            if (value.HasValue) theme = value.Value;
            Apply();
        }

        // 站间过渡时跟着整站图层一起淡入淡出；位置始终钉在自己那一格上。
        public void SetTransition(bool active, float center, float alpha, int orderOffset)
        {
            transitionAlpha = active ? Mathf.Clamp01(alpha) : 1f;
            sortingOffset = active ? orderOffset : 0;
            Apply();
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (image == null) return;
            var sprite = visible ? SliceFor(theme) : null;
            if (image.sprite != sprite) image.sprite = sprite;
            image.enabled = sprite != null && transitionAlpha > .001f;
            image.sortingOrder = baseOrder + sortingOffset;
            if (sprite == null)
            {
                image.color = new Color(1f, 1f, 1f, 0f);
                return;
            }
            var color = Color.white;
            color.a = transitionAlpha;
            image.color = color;
            if (map == null || settings == null || station == null) return;

            float sliceHeight = sprite.bounds.size.y;
            float band = StationBandHeight(artLayer);
            // 半高后片按宽度比例缩窄，好让旁边露出这一格还没有改变的后景；其余层占满一格宽。
            bool halfPiece = artLayer < 3 && heightRatio < 1f;
            float widthRatio = halfPiece ? Mathf.Clamp(settings.transitionBackdropWidthRatio, .1f, 1f) : 1f;
            float scaleX = station.TileWidth * widthRatio / Mathf.Max(.001f, sprite.bounds.size.x);

            float height;
            float bottom;
            if (band > 0f)
            {
                if (artLayer >= 3)
                {
                    // 中景片：高度取整站中景层的显示高度、底边对齐——正好盖住这一格原来的中景。
                    height = band;
                    bottom = BandBottom(artLayer);
                }
                else
                {
                    // 背景层：整片后片与整站远景等高；半高后片缩到 ratio 倍，并可整体抬高露出后景。
                    float ratio = heightRatio >= 1f ? 1f :
                        Mathf.Clamp(settings.transitionBackdropHeightRatio, .05f, 1f);
                    height = band * ratio;
                    bottom = BandBottom(artLayer) + (heightRatio >= 1f ? 0f :
                        Mathf.Clamp01(settings.transitionBackdropBottomRatio) * band);
                }
            }
            else
            {
                // 整站这一层没有素材（例如雪地没有中景）：按素材自己在一格宽下的自然高度放。
                height = sliceHeight * scaleX;
                bottom = BandBottom(artLayer);
            }
            transform.localScale = new Vector3(scaleX, height / Mathf.Max(.001f, sliceHeight), 1f);
            map.UpdateRouteObject(transform, new Vector3(station.TileCenter(tile),
                bottom + height * .5f, Mathf.Max(0, settings.landscapeDepth) - .05f));
        }

        float BandBottom(int layer)
        {
            var layout = settings.sceneLayout;
            return settings.ringThickness * .5f +
                (layout != null ? layout.landscapeBaseY : 0f) +
                (layout != null ? layout.LandscapeY(layer) : 0f);
        }

        // 整站该层素材的显示层高（按整张素材算），缺素材时返回 0。
        float StationBandHeight(int layer)
        {
            var sprite = ThemeArt(station.theme, layer);
            if (sprite == null) return 0f;
            return sprite.bounds.size.y *
                (station.length / Mathf.Max(.001f, sprite.bounds.size.x)) *
                Mathf.Max(.1f, settings.landscapeHeightScale);
        }

        Sprite ThemeArt(StationTheme value, int layer)
        {
            var layers = settings != null && settings.sceneThemes != null ? settings.sceneThemes.For(value) : null;
            if (layers == null) return null;
            // 背景层用山体，雪地没有独立山体时退回该主题的整张远景占位图；中景层用中景素材。
            return layer >= 3 ? layers.middle :
                layers.farMountains != null ? layers.farMountains : layers.fallbackLandscape;
        }

        Sprite SliceFor(StationTheme value)
        {
            int index = (int)value;
            if (slices[index] != null) return slices[index];
            var source = ThemeArt(value, artLayer);
            if (source == null || source.texture == null) return null;
            var rect = source.textureRect;
            float width = rect.width / RingMapSettings.TilesPerStation;
            var slice = Sprite.Create(source.texture,
                new Rect(rect.x + width * tile, rect.y, width, rect.height),
                new Vector2(.5f, .5f), Mathf.Max(1f, source.pixelsPerUnit));
            slice.name = source.name + "_slice" + tile;
            slices[index] = slice;
            return slice;
        }

        void OnDestroy()
        {
            foreach (var slice in slices)
            {
                if (slice == null) continue;
                if (Application.isPlaying) Destroy(slice);
                else DestroyImmediate(slice);
            }
        }
    }
}
