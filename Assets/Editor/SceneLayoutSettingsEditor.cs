using Hiking.Journey;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneLayoutSettings))]
public sealed class SceneLayoutSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("三个主题共用这组 Y 轴偏移。近景共享偏移会同时移动地面、狐狸、植物和木牌；各自偏移在共享偏移上叠加。播放时修改立即生效。", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("landscapeBaseY"), new GUIContent("远中景基线 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraFrameHeightRatio"), new GUIContent("镜头画面下移比例"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("画框内取景", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("旅途取景宽度设为 1 时恰好显示一站；全景缩小倍率在旅途画面的基础上等比例缩小内容，画框不动。左右、上下内缩比例控制画面在画框内的边界。", MessageType.None);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("framedViewportHorizontalInset"), new GUIContent("画面左右内缩比例"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("framedViewportVerticalInset"), new GUIContent("画面上下内缩比例"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("journeyViewWidthInStations"), new GUIContent("旅途取景宽度（站数）"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("framedViewWidthMultiplier"), new GUIContent("全景画面缩小倍率"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("远景与中景", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skyY"), new GUIContent("天空偏移 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cloudsY"), new GUIContent("云偏移 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mountainsY"), new GUIContent("山偏移 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("middleY"), new GUIContent("中景偏移 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackY"), new GUIContent("整张占位画偏移 Y"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("近景", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nearSharedY"), new GUIContent("近景共享偏移 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("groundY"), new GUIContent("地面单独偏移 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("foxY"), new GUIContent("狐狸单独偏移 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("journeyFoxScale"), new GUIContent("旅途中狐狸大小（倍率）"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("foregroundPlantY"), new GUIContent("前景植物单独偏移 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("backgroundPlantY"), new GUIContent("背景植物单独偏移 Y"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("placementSignY"), new GUIContent("木牌单独偏移 Y"));
        serializedObject.ApplyModifiedProperties();
    }
}
