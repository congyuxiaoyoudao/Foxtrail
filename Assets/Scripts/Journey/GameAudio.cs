using UnityEngine;

namespace Hiking.Journey
{
    public sealed class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        public AudioCatalog Catalog;
        [Min(1)] public int voiceCount = 8;

        AudioSource[] voices;
        AudioSource ambienceSource;
        int nextVoice;
        float sfxVolume = 1f;
        float ambienceGain = 1f;
        float ambienceTarget;
        AudioCue ambienceCue;

        public float MusicTrim => Catalog != null ? Catalog.musicTrim : 1f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
            BuildSources();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildSources()
        {
            voices = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < voices.Length; i++)
                voices[i] = CreateSource();
            ambienceSource = CreateSource();
            ambienceSource.loop = true;
        }

        AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            RefreshAmbienceTarget();
        }

        public void Play(string id, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (Catalog == null) return;
            Play(Catalog.Find(id), volumeScale, pitchScale);
        }

        public void Play(AudioCue cue, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (cue == null || cue.clip == null || Catalog == null) return;
            var source = NextVoice();
            source.Stop();
            source.clip = cue.clip;
            source.loop = cue.loop;
            source.spatialBlend = cue.spatialBlend;
            source.pitch = Mathf.Clamp(cue.pitch * pitchScale *
                Random.Range(1f - cue.pitchJitter, 1f + cue.pitchJitter), .25f, 3f);
            source.volume = Mathf.Clamp01(cue.volume * Mathf.Max(0f, volumeScale) *
                Catalog.sfxTrim * sfxVolume);
            source.Play();
        }

        public void PlayAmbience(string id, float volumeScale = 1f)
        {
            if (Catalog == null) return;
            var cue = Catalog.Find(id);
            if (cue == null || cue.clip == null) return;
            bool changed = ambienceCue != cue || ambienceSource.clip != cue.clip;
            ambienceCue = cue;
            ambienceGain = Mathf.Max(0f, volumeScale);
            if (changed)
            {
                ambienceSource.Stop();
                ambienceSource.clip = cue.clip;
                ambienceSource.spatialBlend = cue.spatialBlend;
                ambienceSource.pitch = cue.pitch;
                ambienceSource.volume = 0f;
                ambienceSource.Play();
            }
            RefreshAmbienceTarget();
        }

        public void StopAmbience()
        {
            ambienceTarget = 0f;
        }

        void RefreshAmbienceTarget()
        {
            ambienceTarget = ambienceCue == null || Catalog == null ? 0f :
                Mathf.Clamp01(ambienceCue.volume * ambienceGain * Catalog.sfxTrim * sfxVolume);
        }

        void Update()
        {
            if (ambienceSource == null) return;
            float fade = Catalog != null ? Mathf.Max(.05f, Catalog.ambienceFadeSeconds) : .5f;
            ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, ambienceTarget,
                Time.unscaledDeltaTime / fade);
            if (ambienceTarget <= 0f && ambienceSource.volume <= .0001f && ambienceSource.isPlaying)
            {
                ambienceSource.Stop();
                ambienceSource.clip = null;
                ambienceCue = null;
            }
        }

        AudioSource NextVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                int index = (nextVoice + i) % voices.Length;
                if (!voices[index].isPlaying)
                {
                    nextVoice = (index + 1) % voices.Length;
                    return voices[index];
                }
            }
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            return voice;
        }
    }
}
