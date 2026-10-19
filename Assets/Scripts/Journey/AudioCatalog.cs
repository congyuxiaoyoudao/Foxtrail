using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hiking.Journey
{
    public enum AudioCueCategory { Ui, Footstep, Ambience, Placement, Stinger }

    [Serializable]
    public sealed class AudioCue
    {
        public string id = "ui_click";
        public string displayName = "UI，点击按钮";
        public AudioCueCategory category = AudioCueCategory.Ui;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(.5f, 1.5f)] public float pitch = 1f;
        [Range(0f, .15f)] public float pitchJitter = .02f;
        public bool loop;
        [Range(0f, 1f)] public float spatialBlend;
        [Tooltip("自动混音的目标响度（RMS dBFS）。短音效按活跃段建议，循环音按整体建议。")]
        public float targetRmsDb = -24f;
        [HideInInspector] public float measuredRmsDb;
        [HideInInspector] public float measuredActiveRmsDb;
        [HideInInspector] public float measuredPeakDb;
        [HideInInspector] public float suggestedVolume = 1f;
        public string license = "付费商用";
        public string source = "https://elevenlabs.io/app/sound-effects";
        [Range(0f, 1f)] public float usage = 1f;

        public void Normalize()
        {
            id = string.IsNullOrWhiteSpace(id) ? "audio" : id.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName.Trim();
            volume = Mathf.Clamp01(volume);
            pitch = Mathf.Clamp(pitch, .5f, 1.5f);
            pitchJitter = Mathf.Clamp(pitchJitter, 0f, .15f);
            spatialBlend = Mathf.Clamp01(spatialBlend);
            targetRmsDb = Mathf.Clamp(targetRmsDb, -60f, 0f);
            usage = Mathf.Clamp01(usage);
        }
    }

    [CreateAssetMenu(menuName = "Hiking/Audio Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [Header("总体混音")]
        [Tooltip("背景音乐的额外增益，与设置面板中的音乐滑条相乘。")]
        [Range(0f, 1f)] public float musicTrim = .6f;
        [Tooltip("全部音效的额外增益，与设置面板中的操作音量相乘。")]
        [Range(0f, 1f)] public float sfxTrim = .9f;
        [Tooltip("自动混音时希望音乐达到的整体 RMS 响度。")]
        public float musicTargetRmsDb = -30f;
        [HideInInspector] public float musicMeasuredRmsDb;
        [HideInInspector] public float musicMeasuredPeakDb;

        [Header("节奏")]
        [Tooltip("脚步序列切换或停止时的淡入淡出时间。")]
        [FormerlySerializedAs("footstepIntervalSeconds")]
        [Min(.02f)] public float footstepFadeSeconds = .08f;
        [Min(0f)] public float hoverCooldownSeconds = .08f;
        [Min(.05f)] public float ambienceFadeSeconds = 1.2f;

        [Header("音效表")]
        public AudioCue[] cues = Array.Empty<AudioCue>();

        public AudioCue Find(string id)
        {
            if (cues == null || string.IsNullOrEmpty(id)) return null;
            foreach (var cue in cues)
                if (cue != null && cue.id == id) return cue;
            return null;
        }

        public AudioCue Find(AudioClip clip)
        {
            if (cues == null || clip == null) return null;
            foreach (var cue in cues)
                if (cue != null && cue.clip == clip) return cue;
            return null;
        }

        void OnValidate()
        {
            musicTrim = Mathf.Clamp01(musicTrim);
            sfxTrim = Mathf.Clamp01(sfxTrim);
            musicTargetRmsDb = Mathf.Clamp(musicTargetRmsDb, -60f, 0f);
            footstepFadeSeconds = Mathf.Max(.02f, footstepFadeSeconds);
            hoverCooldownSeconds = Mathf.Max(0f, hoverCooldownSeconds);
            ambienceFadeSeconds = Mathf.Max(.05f, ambienceFadeSeconds);
            if (cues == null) cues = Array.Empty<AudioCue>();
            foreach (var cue in cues) cue?.Normalize();
        }
    }
}
