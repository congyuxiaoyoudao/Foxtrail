using System;
using UnityEngine;

namespace Hiking.Journey
{
    [Serializable]
    public sealed class PondThemeSettings
    {
        [Header("积水贴图")]
        public Sprite wetSingle;
        public Sprite wetLeft;
        public Sprite wetMiddle;
        public Sprite wetRight;
        public Sprite wetFree01;
        public Sprite wetFree02;
        [Header("干涸贴图")]
        public Sprite drySingle;
        [Header("尺寸与过渡")]
        [Tooltip("每段池塘的统一显示高度，相对于一个地块的宽度")]
        [InspectorName("每格池塘统一高度比例")]
        [Min(.05f)] public float tileHeightRatio = .55f;
        [Min(.05f)] public float dryFadeSeconds = 4f;

        public bool HasWetSprites => wetSingle != null || wetLeft != null || wetMiddle != null ||
            wetRight != null || wetFree01 != null || wetFree02 != null;

        public Sprite WetFor(bool joinsLeft, bool joinsRight, int tileIndex)
        {
            if (!joinsLeft && !joinsRight) return wetSingle != null ? wetSingle : wetMiddle;
            if (!joinsLeft) return wetLeft != null ? wetLeft : wetSingle;
            if (!joinsRight) return wetRight != null ? wetRight : wetSingle;
            return tileIndex % 3 == 0 ? wetMiddle : tileIndex % 3 == 1 ?
                (wetFree01 != null ? wetFree01 : wetMiddle) :
                (wetFree02 != null ? wetFree02 : wetMiddle);
        }
    }

    [Serializable]
    public sealed class SceneThemeLayers
    {
        public StationTheme theme;
        [Header("远景：按天空、云、山的顺序叠放")]
        public Sprite farSky;
        public Sprite farClouds;
        public Sprite farMountains;
        [Header("中景")]
        public Sprite middle;
        [Header("近景：按地块重复铺设")]
        public Sprite nearGround;
        [Header("池塘：积水与干涸可按主题独立配置")]
        public PondThemeSettings pond = new PondThemeSettings();
        [Tooltip("尚无分层素材时使用的整张场景占位画")]
        public Sprite fallbackLandscape;
        [Header("画幅外延展颜色")]
        public Color skyFillColor = new Color(.66f, .89f, .95f);
        public Color groundFillColor = new Color(.77f, .86f, .38f);
        public bool HasLandscape => farSky != null || farClouds != null ||
            farMountains != null || middle != null;
    }

    [CreateAssetMenu(menuName = "Hiking/Scene Theme Catalog", fileName = "SceneThemeCatalog")]
    public sealed class SceneThemeCatalog : ScriptableObject
    {
        public SceneThemeLayers[] themes = Array.Empty<SceneThemeLayers>();

        public SceneThemeLayers For(StationTheme theme) => themes == null ? null :
            Array.Find(themes, entry => entry != null && entry.theme == theme);
    }
}
