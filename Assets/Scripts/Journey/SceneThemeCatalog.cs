using System;
using UnityEngine;

namespace Hiking.Journey
{
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
        [Header("运行时图层位置（X 沿路线，Y 向上）")]
        public Vector2 farSkyOffset;
        public Vector2 farCloudsOffset;
        public Vector2 farMountainsOffset;
        public Vector2 middleOffset;
        public Vector2 fallbackOffset;
        [Header("近景：按地块重复铺设")]
        public Sprite nearGround;
        [Tooltip("有近景地块贴图时，地块、狐狸、前景植物和标记共享此位置偏移")]
        public Vector2 nearOffset = new Vector2(-1.1f, -1.1f);
        [Tooltip("尚无分层素材时使用的整张场景占位画")]
        public Sprite fallbackLandscape;
        [Header("画幅外延展颜色")]
        public Color skyFillColor = new Color(.66f, .89f, .95f);
        public Color groundFillColor = new Color(.77f, .86f, .38f);
        [Tooltip("整组远中景相对地图远景基线的上下偏移")]
        public float landscapeYOffset = .1f;
        [Header("高度倍率（1 保持素材原始比例）")]
        [Min(.1f)] public float farHeightMultiplier = 1f;
        [Min(.1f)] public float middleHeightMultiplier = 1f;
        [Min(.1f)] public float nearHeightMultiplier = 1f;
        [Tooltip("近景地面顶端相对道路的高度")]
        public float nearGroundTopOffset = -.24f;

        public bool HasLandscape => farSky != null || farClouds != null ||
            farMountains != null || middle != null;

        public Vector2 OffsetFor(int index) => index == 0 ? farSkyOffset :
            index == 1 ? farCloudsOffset : index == 2 ? farMountainsOffset : middleOffset;
    }

    [CreateAssetMenu(menuName = "Hiking/Scene Theme Catalog", fileName = "SceneThemeCatalog")]
    public sealed class SceneThemeCatalog : ScriptableObject
    {
        public SceneThemeLayers[] themes = Array.Empty<SceneThemeLayers>();

        public SceneThemeLayers For(StationTheme theme) => themes == null ? null :
            Array.Find(themes, entry => entry != null && entry.theme == theme);
    }
}
