using Hiking.Journey;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneThemeCatalog))]
public sealed class SceneThemeCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("播放时可调整远、中景及近景共享位移，画面立即更新。前景与背景植物的独立位移在 RingMapSettings 中调整。", MessageType.Info);
        var themes = serializedObject.FindProperty("themes");
        for (int i = 0; i < themes.arraySize; i++)
        {
            var entry = themes.GetArrayElementAtIndex(i);
            var theme = (StationTheme)entry.FindPropertyRelative("theme").enumValueIndex;
            string label = theme == StationTheme.Snow ? "雪地" :
                theme == StationTheme.Grass ? "平原" : "沙漠";
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("theme"), new GUIContent("主题"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("farSky"), new GUIContent("远景 · 天空"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("farClouds"), new GUIContent("远景 · 云"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("farMountains"), new GUIContent("远景 · 山"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("middle"), new GUIContent("中景"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("farSkyOffset"), new GUIContent("天空偏移 X/Y"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("farCloudsOffset"), new GUIContent("云偏移 X/Y"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("farMountainsOffset"), new GUIContent("山偏移 X/Y"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("middleOffset"), new GUIContent("中景偏移 X/Y"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("nearGround"), new GUIContent("近景地面"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("nearOffset"), new GUIContent("近景共享偏移 X/Y"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("fallbackLandscape"), new GUIContent("整张占位画"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("skyFillColor"), new GUIContent("天空延展色"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("groundFillColor"), new GUIContent("地面延展色"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("fallbackOffset"), new GUIContent("占位画偏移 X/Y"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("landscapeYOffset"), new GUIContent("远中景上下偏移"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("farHeightMultiplier"), new GUIContent("远景高度倍率"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("middleHeightMultiplier"), new GUIContent("中景高度倍率"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("nearHeightMultiplier"), new GUIContent("近景高度倍率"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("nearGroundTopOffset"), new GUIContent("近景顶端偏移"));
            EditorGUILayout.EndVertical();
        }
        serializedObject.ApplyModifiedProperties();
    }
}
