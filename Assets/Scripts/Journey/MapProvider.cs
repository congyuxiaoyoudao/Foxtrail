using UnityEngine;

namespace Hiking.Journey
{
    public class MapProvider : MonoBehaviour
    {
        [Tooltip("直线路线与地块环境的配置")]
        public RingMapSettings ringSettings;
        
        public virtual JourneyMap CreateMap(Transform parent) 
        {
            if (ringSettings == null)
            {
                throw new System.InvalidOperationException("未配置 RingMapSettings，无法创建地图。");
            }
            return RingMapAssembler.Build(ringSettings, parent);
        }
    }
}
