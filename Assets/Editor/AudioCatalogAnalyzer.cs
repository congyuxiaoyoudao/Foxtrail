using System;
using Hiking.Journey;
using UnityEditor;
using UnityEngine;

public static class AudioCatalogAnalyzer
{
    const float SilenceDb = -60f;
    const float AnalysisSeconds = 30f;

    public static void AnalyzeAndRecommend(AudioCatalog catalog, bool apply)
    {
        if (catalog == null) return;
        if (catalog.cues != null)
        {
            foreach (var cue in catalog.cues)
            {
                if (cue == null || cue.clip == null) continue;
                if (!Measure(cue.clip, out float overall, out float active, out float peak)) continue;
                cue.measuredRmsDb = overall;
                cue.measuredActiveRmsDb = active;
                cue.measuredPeakDb = peak;
                float measured = cue.loop || cue.category == AudioCueCategory.Ambience ? overall : active;
                cue.suggestedVolume = Mathf.Clamp01(Mathf.Pow(10f, (cue.targetRmsDb - measured) / 20f));
                if (apply) cue.volume = cue.suggestedVolume;
                Debug.Log($"[AudioCatalog] {cue.id}: 整体 {overall:0.0} dBFS · 活跃 {active:0.0} dBFS · 峰值 {peak:0.0} dBFS · 目标 {cue.targetRmsDb:0.0} · 建议音量 {cue.suggestedVolume:0.00}");
            }
        }

        if (TryAverageMusicRms(out float musicRms, out float musicPeak))
        {
            catalog.musicMeasuredRmsDb = musicRms;
            catalog.musicMeasuredPeakDb = musicPeak;
            float trim = Mathf.Clamp01(Mathf.Pow(10f, (catalog.musicTargetRmsDb - musicRms) / 20f));
            if (apply) catalog.musicTrim = trim;
            Debug.Log($"[AudioCatalog] BGM 平均 {musicRms:0.0} dBFS · 峰值 {musicPeak:0.0} dBFS · 目标 {catalog.musicTargetRmsDb:0.0} · 建议 musicTrim {trim:0.00}");
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    static bool TryAverageMusicRms(out float averageRms, out float averagePeak)
    {
        averageRms = averagePeak = 0f;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audios/BGM" });
        double sumRms = 0;
        double sumPeak = 0;
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || !Measure(clip, out float overall, out _, out float peak)) continue;
            sumRms += overall;
            sumPeak += peak;
            count++;
        }
        if (count == 0) return false;
        averageRms = (float)(sumRms / count);
        averagePeak = (float)(sumPeak / count);
        return true;
    }

    static bool Measure(AudioClip clip, out float overallDb, out float activeDb, out float peakDb)
    {
        overallDb = activeDb = peakDb = -120f;
        if (clip == null || clip.channels <= 0) return false;
        int availableFrames = clip.samples;
        int frames = Mathf.Min(availableFrames, Mathf.Max(1, Mathf.RoundToInt(clip.frequency * AnalysisSeconds)));
        int channels = clip.channels;
        var samples = new float[frames * channels];
        if (!clip.GetData(samples, 0))
        {
            Debug.LogWarning($"[AudioCatalog] 无法读取 {clip.name} 的采样数据（可能是 Streaming 导入）。请把导入设置的 Load Type 改为 Decompress On Load 后再分析。", clip);
            return false;
        }

        int windowFrames = Mathf.Max(1, clip.frequency / 20);
        double totalPower = 0;
        double activePower = 0;
        int activeWindows = 0;
        float peak = 0f;
        for (int start = 0; start < frames; start += windowFrames)
        {
            int end = Mathf.Min(frames, start + windowFrames);
            double windowPower = 0;
            int values = 0;
            for (int frame = start; frame < end; frame++)
                for (int channel = 0; channel < channels; channel++)
                {
                    float value = samples[frame * channels + channel];
                    float magnitude = Mathf.Abs(value);
                    if (magnitude > peak) peak = magnitude;
                    windowPower += value * value;
                    values++;
                }
            totalPower += windowPower;
            double windowRms = Math.Sqrt(windowPower / Mathf.Max(1, values));
            if (ToDb(windowRms) > SilenceDb)
            {
                activePower += windowPower;
                activeWindows++;
            }
        }

        double sampleCount = (double)frames * channels;
        overallDb = ToDb(Math.Sqrt(totalPower / Math.Max(1.0, sampleCount)));
        activeDb = activeWindows == 0 ? overallDb :
            ToDb(Math.Sqrt(activePower / Math.Max(1.0, (double)activeWindows * windowFrames * channels)));
        peakDb = ToDb(peak);
        return true;
    }

    static float ToDb(double linear) => linear <= .000001 ? -120f : 20f * Mathf.Log10((float)linear);
}
