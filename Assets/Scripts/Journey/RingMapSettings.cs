using UnityEngine;

namespace Hiking.Journey
{
    // Asset type/name retained so existing scene and asset GUIDs remain valid.
    [CreateAssetMenu(menuName = "Hiking/Station Map Settings")]
    public class RingMapSettings : ScriptableObject
    {
        [Header("直线站点地图（新旅途生效）")]
        [InspectorName("站点数量"), Min(1)] public int blockCount = 6;
        [InspectorName("每站路线长度"), Min(2)] public float stationLength = 12;
        public const int TilesPerStation = 12;
        [HideInInspector] public int patchesPerStation = TilesPerStation;
        [InspectorName("过渡地块颜色")] public Color transitionTileColor = new Color(.4f, .47f, .58f);
        public Material pathMaterial;
        [Header("随机站点主题颜色")]
        [InspectorName("雪地")] public Color snowColor = Color.white;
        [InspectorName("平原")] public Color grassColor = new Color(.3f, .72f, .3f);
        [InspectorName("沙漠")] public Color desertColor = new Color(1f, .82f, .25f);
        public Color ThemeColor(StationTheme theme) => theme == StationTheme.Snow ? snowColor : theme == StationTheme.Grass ? grassColor : desertColor;
        [Header("自然植物")]
        [Tooltip("按地块主题选择自然生长植物；没有适宜植物时该地块不生成自然植物")]
        public PlantCatalog plantCatalog;
        [Tooltip("植物阶段未配置 Sprite 时使用的白色方块，按阶段占位色着色")]
        public Sprite fallbackPlantSprite;
        [Tooltip("背景植物未配置 Sprite 时使用的树形占位图，按阶段占位色着色")]
        public Sprite fallbackBackgroundPlantSprite;
        [Header("狐狸相对地块位置（运行时生效）")]
        [Tooltip("在主题近景共享位移之外，单独调整狐狸的 X/Y 位置；不移动地块或植物")]
        [InspectorName("狐狸相对地块位移 X/Y")] public Vector2 foxPositionOffset;
        [Header("植物前后景位置（运行时生效）")]
        [Tooltip("前景植物相对道路顶面的 Y 轴高度；背景植物以此为基线")]
        [InspectorName("前景基线高度（Y）")] public float foregroundPlantY = .13f;
        [Tooltip("背景植物相对前景基线沿 Y 轴向上移动的距离")]
        [InspectorName("背景上移量"), Min(0)] public float backgroundPlantOffset = .96f;
        [Tooltip("前景植物在基线之外的独立 X/Y 位移；与近景共享位移叠加")]
        [InspectorName("前景植物附加位移 X/Y")] public Vector2 foregroundPlantPositionOffset;
        [Tooltip("背景植物在基线之外的独立 X/Y 位移")]
        [InspectorName("背景植物附加位移 X/Y")] public Vector2 backgroundPlantPositionOffset;
        [InspectorName("背景植物占位高度"), Min(.1f)] public float backgroundPlantHeight = 2f;
        [Header("场景主题图层（新地图生效）")]
        public SceneThemeCatalog sceneThemes;
        [Header("旧版单图远景（缺少主题图层时备用）")]
        public Sprite snowLandscape;
        public Sprite grassLandscape;
        public Sprite desertLandscape;
        [Tooltip("远景专用 URP Unlit 材质")]
        public Material landscapeMaterial;
        [Tooltip("远景图片下边缘的路线 Y 偏移；镜头俯角会让远处画面在屏幕中上移")]
        public float landscapeBaseHeight = -2.1f;
        [Tooltip("远景离道路的纵深距离；数值越大越远")]
        public float landscapeDepth = 1.6f;
        [Tooltip("保持远景宽度不变，向上延展画幅以填满天空")]
        [Min(.1f)] public float landscapeHeightScale = 1f;
        public Sprite LandscapeFor(StationTheme theme) => theme == StationTheme.Snow ? snowLandscape :
            theme == StationTheme.Grass ? grassLandscape : desertLandscape;
        [Header("地块随机初值（湿度以 0～1 填写，新地图生效）")]
        [HideInInspector] public Vector2 soilContentRange = new Vector2(1, 100);
        [HideInInspector] public Vector2Int plantCountRange = new Vector2Int(0, 5);
        [HideInInspector] public Vector2 soilHumidityRange = new Vector2(0, 1);
        [Tooltip("基础蒸发量 / 秒；实际蒸发量还受泥土含量和植物数量影响")]
        public Vector2 evaporationRateRange = new Vector2(.01f, .03f);
        public Vector2Int heightRange = new Vector2Int(0, 1);
        public Vector2 surfaceWaterRange = new Vector2(0, 3);
        [Header("相邻地块流动（每步，先于渗水和蒸发）")]
        [InspectorName("温度流动（°C）"), Min(0)] public float temperatureFlowPerStep = .002f;
        [InspectorName("土壤水流动（水量）"), Min(0)] public float soilWaterFlowPerStep = .02f;
        [InspectorName("地表水流动上限（单位/秒）"), Min(0)] public float surfaceWaterFlowPerStep = 1f;
        [Tooltip("每增加多少地表水，水面高度增加 1")]
        [Min(.01f)] public float waterUnitsPerHeight = 35f;
        [Header("单步水分模拟")]
        [Tooltip("每秒渗水量 = 泥土含量 × 3 / 100，受地表水和土壤剩余容量限制")]
        [Min(0)] public float infiltrationFactor = .03f;
        [Tooltip("实际蒸发量 = 基础蒸发量 × max(最低蒸发比例, 1 - a×泥土含量 - b×植物数量/5)")]
        [Min(0)] public float evaporationSoilFactor = .004f;
        [Min(0)] public float evaporationPlantFactor = .3f;
        [Range(0, 1)] public float minimumEvaporationRatio = .15f;
        [Header("天气与降雨（一步为一秒）")]
        [Tooltip("每隔多少秒抽取一段连续 15 格；未达到门槛时等待下一周期")]
        [Min(1)] public int rainCheckInterval = 40;
        [Tooltip("所选 15 格的待回落水汽量总和达到此值才下雨")]
        [Min(0)] public float rainVaporThreshold = 30f;
        [Tooltip("达到门槛时，水汽回落为雨水的比例")]
        [Range(0, 1)] public float rainReturnRatio = .9f;
        [Tooltip("单场降雨总量上限，均分给连续 15 格")]
        [Min(0)] public float maxRainPerEvent = 80f;
        [Tooltip("单场降雨持续秒数")]
        [Min(1)] public int rainDuration = 40;
        [Header("雨丝占位效果")]
        [Tooltip("每格显示的雨丝数；雨丝在主题远景之前、背景植物之后的层级绘制")]
        [Range(1, 16)] public int rainStreaksPerTile = 6;
        [Tooltip("雨丝可见高度（世界单位）")]
        [Min(.5f)] public float rainVisualHeight = 3.2f;
        [Tooltip("雨丝下落速度（世界单位/秒）")]
        [Min(.1f)] public float rainStreakSpeed = 3.6f;
        [Tooltip("雨丝透明度")]
        [Range(0, 1)] public float rainStreakOpacity = .42f;
        public Font tileLabelFont;
        [Header("生态初值（按主题）")]
        public Vector2 snowSoilRange = new Vector2(30,65), grassSoilRange = new Vector2(40,80), desertSoilRange = new Vector2(20,50);
        public Vector2 snowHumidityRange = new Vector2(.45f,.75f), grassHumidityRange = new Vector2(.45f,.7f), desertHumidityRange = new Vector2(.2f,.4f);
        public Vector2 snowTemperatureRange = new Vector2(1,4), grassTemperatureRange = new Vector2(7,10), desertTemperatureRange = new Vector2(14,18);
        [Range(0,1)] public float initialPlantProbability = .3f;
        public bool createStationBasin = true;
        public Vector2 basinWaterRange = new Vector2(26,32);
        [Header("水温影响与干土蒸发（每秒）")]
        [Min(.001f)] public float drySoilMoistureScale = .4f;
        [Range(0,1)] public float minimumDrySoilMultiplier = .05f;
        [Min(0)] public float extraWaterDeadband = 1;
        [Min(.001f)] public float fullInfluenceWater = 8;
        [Range(5,11.99f)] public float waterTargetTemperature = 10;
        [Range(0,1)] public float thermalApproachRate = .06f, thermalRecoveryRate = .002f;
        [Min(0)] public float thermalApproachCap = .15f, thermalRecoveryCap = .01f;
        [Min(1)] public int matureSeedInterval = 30;
        public TileState CreateTile(StationTheme theme, bool basin = false, bool basinEdge = false)
        {
            Vector2 temperature = theme == StationTheme.Snow ? snowTemperatureRange : theme == StationTheme.Grass ? grassTemperatureRange : desertTemperatureRange;
            Vector2 soil = theme == StationTheme.Snow ? snowSoilRange : theme == StationTheme.Grass ? grassSoilRange : desertSoilRange;
            Vector2 wetness = theme == StationTheme.Snow ? snowHumidityRange : theme == StationTheme.Grass ? grassHumidityRange : desertHumidityRange;
            int count = !basin && Random.Range(0f,1f) < initialPlantProbability ? 1 : 0;
            float soilContent = Sample(soil, 1, 100), humidity = basin ? 1 : Sample(wetness, 0, 1);
            var naturalPlants = plantCatalog != null ? plantCatalog.SpawnNaturalPlants(theme, count, soilContent, humidity * 100f) : System.Array.Empty<PlantInstance>();
            var tile = new TileState(theme, soilContent, naturalPlants.Length, humidity,
                Sample(evaporationRateRange, 0, float.MaxValue), basin ? 0 : basinEdge ? 1 : Sample(heightRange, 0, 6),
                Sample(basin ? basinWaterRange : surfaceWaterRange, 0, 35), Sample(temperature, 0, 20), naturalPlants);
            tile.RefreshEvaporation(evaporationSoilFactor, evaporationPlantFactor, minimumEvaporationRatio);
            return tile;
        }
        static float Sample(Vector2 range, float min, float max) => Random.Range(
            Mathf.Clamp(Mathf.Min(range.x, range.y), min, max), Mathf.Clamp(Mathf.Max(range.x, range.y), min, max));
        static int Sample(Vector2Int range, int min, int max) => Random.Range(
            Mathf.Clamp(Mathf.Min(range.x, range.y), min, max), Mathf.Clamp(Mathf.Max(range.x, range.y), min, max) + 1);
        [Header("道路与完成全景")]
        [InspectorName("全景圆环尺寸倍率"), Min(1)] public float panoramaScale = 1.5f;
        [InspectorName("道路厚度"), Range(.1f, 2f)] public float ringThickness = .5f;
        void OnValidate()
        {
            blockCount = Mathf.Max(1, blockCount);
            patchesPerStation = TilesPerStation;
            stationLength = Mathf.Max(2, stationLength);
            rainCheckInterval = Mathf.Max(1, rainCheckInterval);
            rainDuration = Mathf.Max(1, rainDuration);
        }
    }
}

