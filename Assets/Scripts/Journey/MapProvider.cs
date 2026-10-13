using UnityEngine;

namespace Hiking.Journey
{
    public class MapProvider : MonoBehaviour
    {
        [Tooltip("直线路线与地块环境的配置")]
        public RingMapSettings ringSettings;
        
        // themes 为空时按原有规则随机；读档时必须传入存档里的主题，
        // 否则站点主题会重新随机，雪山可能变成沙漠。
        public virtual JourneyMap CreateMap(Transform parent, StationTheme[] themes = null) 
        {
            if (ringSettings == null)
            {
                throw new System.InvalidOperationException("未配置 RingMapSettings，无法创建地图。");
            }
            return RingMapAssembler.Build(ringSettings, parent, themes);
        }
    }
}
