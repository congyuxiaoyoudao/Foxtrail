using Hiking.Journey;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneThemeCatalog))]
public sealed class SceneThemeCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("景色的 Y 轴位置统一在 SceneLayoutSettings 中调整。池塘积水按相邻地块拼接，退水后每格使用单格干涸贴图并渐隐。", MessageType.Info);
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
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("nearGround"), new GUIContent("近景地面"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("pond"), new GUIContent("池塘 · 积水与干涸"), true);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("fallbackLandscape"), new GUIContent("整张占位画"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("skyFillColor"), new GUIContent("天空延展色"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("groundFillColor"), new GUIContent("地面延展色"));
            EditorGUILayout.EndVertical();
        }
        serializedObject.ApplyModifiedProperties();
    }
}
