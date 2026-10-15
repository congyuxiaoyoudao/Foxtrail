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
