using UnityEngine;

namespace Hiking.Journey
{
    [CreateAssetMenu(menuName = "Hiking/Scene Layout Settings", fileName = "SceneLayoutSettings")]
    public sealed class SceneLayoutSettings : ScriptableObject
    {
        [Tooltip("远景素材下边缘相对道路顶面的 Y 位置；各主题图层在此基础上叠加偏移")]
        public float landscapeBaseY = -1.83f;
        [Tooltip("画面高度比例。正值使地图在屏幕中下移；旅途与全景共用")]
        [Range(-.25f, .25f)] public float cameraFrameHeightRatio = .023f;
        [Header("画框内取景")]
        [Tooltip("游戏画面距离屏幕左右边缘的比例；画框留在取景范围之外")]
        [InspectorName("画面左右内缩比例")]
        [Range(0f, .2f)] public float framedViewportHorizontalInset = .065f;
        [Tooltip("游戏画面距离屏幕上下边缘的比例")]
        [InspectorName("画面上下内缩比例")]
        [Range(0f, .2f)] public float framedViewportVerticalInset = .085f;
        [Tooltip("旅途镜头可见宽度，以站为单位。1 表示一站；增大可见相邻地块，减小会裁切本站两端。播放时可直接调整。")]
        [InspectorName("旅途取景宽度（站数）")]
        [Range(.85f, 1.5f)] public float journeyViewWidthInStations = 1f;
        [Tooltip("全景浏览镜头可见宽度，以站为单位；与旅途取景独立")]
        [InspectorName("全景取景宽度（站数）")]
        [Range(1f, 1.5f)] public float framedViewWidthMultiplier = 1.12f;
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
    }
}
