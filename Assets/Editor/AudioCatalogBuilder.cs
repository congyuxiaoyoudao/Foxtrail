using Hiking.Journey;
using UnityEditor;
using UnityEngine;

public static class AudioCatalogBuilder
{
    const string CatalogPath = "Assets/Settings/AudioCatalog.asset";
    const string SeFolder = "Assets/Audios/SE/";
    const string ElevenLabs = "https://elevenlabs.io/app/sound-effects";
    const string HoverSource = "https://freesound.org/people/GameDevC/sounds/422836/";

    struct CueSpec
    {
        public string id, name, file, license, source;
        public AudioCueCategory category;
        public bool loop;
        public float target;
        public CueSpec(string id, string name, string file, AudioCueCategory category,
            float target, bool loop = false, string license = "付费商用", string source = ElevenLabs)
        {
            this.id = id; this.name = name; this.file = file; this.category = category;
            this.target = target; this.loop = loop; this.license = license; this.source = source;
        }
    }

    static readonly CueSpec[] Specs =
    {
        new CueSpec("footstep_plain", "平原脚步声", "SE_Footsteps_Plain.mp3", AudioCueCategory.Footstep, -46f),
        new CueSpec("footstep_desert", "沙漠脚步声", "SE_Footsteps_Desert.mp3", AudioCueCategory.Footstep, -46f),
        new CueSpec("footstep_wet", "潮湿地面脚步声", "SE_Footsteps_WetGround.mp3", AudioCueCategory.Footstep, -46f),
        new CueSpec("footstep_snow", "雪地脚步声", "SE_Footsteps_Snow.mp3", AudioCueCategory.Footstep, -46f),
        new CueSpec("rain_loop", "下雨背景音", "BG_Rain.mp3", AudioCueCategory.Ambience, -52f, true),
        new CueSpec("ui_hover", "UI，光标选中", "SE_UI_Hover.mp3", AudioCueCategory.Ui, -38f,
            false, "免费开源", HoverSource),
        new CueSpec("ui_click", "UI，点击按钮", "SE_UI_Click.mp3", AudioCueCategory.Ui, -33f),
        new CueSpec("start_game", "开始游戏时播放一次", "SE_StartGame.mp3", AudioCueCategory.Stinger, -33f),
        new CueSpec("putdown", "投放音效", "SE_PutDown.mp3", AudioCueCategory.Placement, -36f)
    };

    [MenuItem("Hiking/Audio/Build Audio Catalog")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
        bool created = catalog == null;
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        foreach (var spec in Specs)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SeFolder + spec.file);
            if (clip == null)
            {
                Debug.LogError("[AudioCatalog] 缺少音频文件：" + SeFolder + spec.file);
                continue;
            }
            var cue = catalog.Find(spec.id);
            if (cue == null)
            {
                cue = new AudioCue { id = spec.id, volume = 1f };
                var list = new System.Collections.Generic.List<AudioCue>(catalog.cues ?? new AudioCue[0]);
                list.Add(cue);
                catalog.cues = list.ToArray();
            }
            cue.displayName = spec.name;
            cue.clip = clip;
            cue.category = spec.category;
            cue.loop = spec.loop;
            cue.targetRmsDb = spec.target;
            cue.license = spec.license;
            cue.source = spec.source;
            cue.usage = 1f;
            cue.Normalize();
        }

        catalog.sfxTrim = .9f;
        catalog.musicTargetRmsDb = -30f;
        catalog.footstepFadeSeconds = .08f;
        catalog.hoverCooldownSeconds = .08f;
        catalog.ambienceFadeSeconds = 1.2f;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AudioCatalogAnalyzer.AnalyzeAndRecommend(catalog, created);
        AssetDatabase.SaveAssets();
        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
        Debug.Log(created
            ? "[AudioCatalog] 已同步 9 条音效并应用首次混音建议。"
            : "[AudioCatalog] 已同步 9 条音效，保留现有音量；需要重算时使用 Inspector 的“应用建议音量”。");
    }
}
