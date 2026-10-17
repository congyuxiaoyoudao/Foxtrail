using UnityEngine;

namespace Hiking.Journey
{
    [CreateAssetMenu(menuName = "Hiking/Scene Layout Settings", fileName = "SceneLayoutSettings")]
    public sealed class SceneLayoutSettings : ScriptableObject
    {
        public const float GameplayAspect = 16f / 10f;
        const float FrameRootAspect = 1152f / 720f;
        public static Rect DefaultFramedViewport => new Rect(.126f, .126f, .756f, .756f);

        [Tooltip("远景素材下边缘相对道路顶面的 Y 位置；各主题图层在此基础上叠加偏移")]
        public float landscapeBaseY = -1.83f;
        [Tooltip("画面高度比例。正值使地图在屏幕中下移；旅途与全景共用")]
        [Range(-.25f, .25f)] public float cameraFrameHeightRatio = .023f;
        [Header("画框内取景")]
        [Tooltip("游戏画面距离 FrameRoot 左右边缘的比例；与木框内口配合")]
        [InspectorName("画面左右内缩比例")]
        [Range(0f, .2f)] public float framedViewportHorizontalInset = .122f;
        [Tooltip("游戏画面距离 FrameRoot 上下边缘的最小比例；取景区始终保持 16:10")]
        [InspectorName("画面上下内缩比例")]
        [Range(0f, .2f)] public float framedViewportVerticalInset = .122f;
        [Tooltip("内口相对 FrameRoot 的水平偏移比例；正值向右")]
        [InspectorName("画面水平偏移比例")]
        [Range(-.05f, .05f)] public float framedViewportHorizontalOffset = .004f;
        [Tooltip("内口相对 FrameRoot 的垂直偏移比例；正值向上")]
        [InspectorName("画面垂直偏移比例")]
        [Range(-.05f, .05f)] public float framedViewportVerticalOffset = .004f;
        [Tooltip("旅途镜头可见宽度，以站为单位。1 表示一站；增大可见相邻地块，减小会裁切本站两端。播放时可直接调整。")]
        [InspectorName("旅途取景宽度（站数）")]
        [Range(.85f, 1.5f)] public float journeyViewWidthInStations = 1f;
        [Tooltip("全景相对旅途画面的缩小倍率。1 保持原尺寸，1.3 表示画面内的物体约缩小到 77%；画框不变")]
        [InspectorName("全景画面缩小倍率")]
        [Range(1f, 2f)] public float framedViewWidthMultiplier = 1.3f;
        [Header("三个主题共用的远中景 Y 偏移")]
        public float skyY = 1.6f;
        public float cloudsY = 1.24f;
        public float mountainsY = -1.69f;
        public float middleY = -2.23f;
        public float fallbackY;
        [Header("三个主题共用的近景 Y 偏移")]
        public float nearSharedY = -1.1f;
        [Tooltip("近景地面、地面水色与池塘的顶边，相对道路顶面的 Y 偏移")]
        public float groundY = -.26f;
        [Tooltip("狐狸相对行走基线的 Y 偏移")]
        public float foxY = -.5f;
        [Tooltip("前景植物相对道路顶面的 Y 偏移")]
        public float foregroundPlantY = -.83f;
        [Tooltip("背景植物落地点相对道路顶面的 Y 偏移；叠加随机偏移后也不会高于狐狸行走位置")]
        public float backgroundPlantY = -.32f;
        [Tooltip("木牌相对近景共享位置的额外 Y 偏移；播放时修改立即生效")]
        public float placementSignY;

        public float LandscapeY(int index) => index == 0 ? skyY :
            index == 1 ? cloudsY : index == 2 ? mountainsY : middleY;

        public Rect FramedViewport
        {
            get
            {
                float x = Mathf.Clamp(framedViewportHorizontalInset, 0f, .2f);
                float y = Mathf.Clamp(framedViewportVerticalInset, 0f, .2f);
                float width = 1f - 2f * x;
                float height = 1f - 2f * y;
                // Fit a 16:10 camera opening inside the configured safe area.
                // The 1152x720 root and camera opening both remain 16:10.
                if (width * FrameRootAspect > height * GameplayAspect)
                    width = height * GameplayAspect / FrameRootAspect;
                else
                    height = width * FrameRootAspect / GameplayAspect;
                float centerX = .5f + Mathf.Clamp(framedViewportHorizontalOffset, -.05f, .05f);
                float centerY = .5f + Mathf.Clamp(framedViewportVerticalOffset, -.05f, .05f);
                return new Rect(centerX - width * .5f, centerY - height * .5f,
                    width, height);
            }
        }
    }
}
