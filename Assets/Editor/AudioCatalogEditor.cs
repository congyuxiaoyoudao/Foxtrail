using System;
using Hiking.Journey;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioCatalog))]
public sealed class AudioCatalogEditor : Editor
{
    Vector2 tableScroll;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var catalog = (AudioCatalog)target;
        EditorGUILayout.HelpBox("音效表按工作簿逐条登记。音量是相对素材实测响度的混音增益；目标 RMS 用于自动建议，不是运行时播放音量。", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("musicTrim"), new GUIContent("音乐额外增益"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sfxTrim"), new GUIContent("音效额外增益"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("musicTargetRmsDb"), new GUIContent("音乐目标 RMS"));
        GUILayout.Label($"BGM 实测：RMS {catalog.musicMeasuredRmsDb:0.0} dBFS · 峰值 {catalog.musicMeasuredPeakDb:0.0} dBFS");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("footstepFadeSeconds"), new GUIContent("脚步淡入淡出（秒）"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hoverCooldownSeconds"), new GUIContent("悬停音最短间隔"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ambienceFadeSeconds"), new GUIContent("环境音淡入淡出"));

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("分析响度并更新建议"))
        {
            serializedObject.ApplyModifiedProperties();
            AudioCatalogAnalyzer.AnalyzeAndRecommend(catalog, false);
            serializedObject.Update();
        }
        if (GUILayout.Button("应用建议音量"))
        {
            serializedObject.ApplyModifiedProperties();
            AudioCatalogAnalyzer.AnalyzeAndRecommend(catalog, true);
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();

        var cues = serializedObject.FindProperty("cues");
        tableScroll = EditorGUILayout.BeginScrollView(tableScroll, true, true,
            GUILayout.Height(Mathf.Min(420f, 30f * (cues.arraySize + 2) + 26f)));
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("使用位置", EditorStyles.boldLabel, GUILayout.Width(145));
        GUILayout.Label("ID", EditorStyles.boldLabel, GUILayout.Width(115));
        GUILayout.Label("类别", EditorStyles.boldLabel, GUILayout.Width(82));
        GUILayout.Label("音频", EditorStyles.boldLabel, GUILayout.Width(155));
        GUILayout.Label("音量", EditorStyles.boldLabel, GUILayout.Width(95));
        GUILayout.Label("音高", EditorStyles.boldLabel, GUILayout.Width(48));
        GUILayout.Label("抖动", EditorStyles.boldLabel, GUILayout.Width(48));
        GUILayout.Label("循环", EditorStyles.boldLabel, GUILayout.Width(36));
        GUILayout.Label("目标dB", EditorStyles.boldLabel, GUILayout.Width(58));
        GUILayout.Label("实测dB", EditorStyles.boldLabel, GUILayout.Width(58));
        GUILayout.Label("授权", EditorStyles.boldLabel, GUILayout.Width(85));
        GUILayout.Label("来源", EditorStyles.boldLabel, GUILayout.Width(190));
        GUILayout.Label("使用率", EditorStyles.boldLabel, GUILayout.Width(52));
        GUILayout.Space(26);
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < cues.arraySize; i++)
        {
            var cue = cues.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("displayName"), GUIContent.none, GUILayout.Width(145));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("id"), GUIContent.none, GUILayout.Width(115));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("category"), GUIContent.none, GUILayout.Width(82));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("clip"), GUIContent.none, GUILayout.Width(155));
            EditorGUILayout.Slider(cue.FindPropertyRelative("volume"), 0f, 1f, GUIContent.none, GUILayout.Width(95));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("pitch"), GUIContent.none, GUILayout.Width(48));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("pitchJitter"), GUIContent.none, GUILayout.Width(48));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("loop"), GUIContent.none, GUILayout.Width(36));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("targetRmsDb"), GUIContent.none, GUILayout.Width(58));
            var data = i < catalog.cues.Length ? catalog.cues[i] : null;
            GUILayout.Label(data == null ? "—" : (data.loop ? data.measuredRmsDb : data.measuredActiveRmsDb).ToString("0.0"),
                GUILayout.Width(58));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("license"), GUIContent.none, GUILayout.Width(85));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("source"), GUIContent.none, GUILayout.Width(190));
            EditorGUILayout.Slider(cue.FindPropertyRelative("usage"), 0f, 1f, GUIContent.none, GUILayout.Width(52));
            if (GUILayout.Button("×", GUILayout.Width(22)))
            {
                cues.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("＋ 添加音效"))
        {
            cues.InsertArrayElementAtIndex(cues.arraySize);
            var cue = cues.GetArrayElementAtIndex(cues.arraySize - 1);
            cue.FindPropertyRelative("id").stringValue = "audio_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            cue.FindPropertyRelative("displayName").stringValue = "新音效";
            cue.FindPropertyRelative("category").enumValueIndex = (int)AudioCueCategory.Ui;
            cue.FindPropertyRelative("clip").objectReferenceValue = null;
            cue.FindPropertyRelative("volume").floatValue = 1f;
            cue.FindPropertyRelative("pitch").floatValue = 1f;
            cue.FindPropertyRelative("pitchJitter").floatValue = .02f;
            cue.FindPropertyRelative("loop").boolValue = false;
            cue.FindPropertyRelative("spatialBlend").floatValue = 0f;
            cue.FindPropertyRelative("targetRmsDb").floatValue = -24f;
            cue.FindPropertyRelative("license").stringValue = "未登记";
            cue.FindPropertyRelative("source").stringValue = "";
            cue.FindPropertyRelative("usage").floatValue = 1f;
        }

        if (serializedObject.ApplyModifiedProperties())
            EditorUtility.SetDirty(catalog);
    }
}
