using System;
using Hiking.Journey;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlantCatalog))]
public sealed class PlantCatalogEditor : Editor
{
    const int MaxStages = 64;
    Vector2 tableScroll;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var catalog = (PlantCatalog)target;
        var plants = serializedObject.FindProperty("plants");
        EditorGUILayout.HelpBox("每行只填写阶段下限。当前阶段的上限取下一阶段的下限；最后一阶段取植物总上限。前几阶段为左闭右开区间，最后一阶段包含上限。", MessageType.Info);
        EditorGUILayout.HelpBox("地块会从适宜其主题的植物中自然生成。每个阶段可选 Sprite；未设置时使用颜色方块占位。植物前后景偏移量在地图设置中统一配置。", MessageType.Info);
        EditorGUILayout.Space(8);

        for (int i = 0; i < plants.arraySize; i++)
        {
            var plant = plants.GetArrayElementAtIndex(i);
            if (DrawPlant(plants, plant, i)) break;
        }

        if (GUILayout.Button("＋ 添加植物"))
        {
            plants.InsertArrayElementAtIndex(plants.arraySize);
            var plant = plants.GetArrayElementAtIndex(plants.arraySize - 1);
            plant.FindPropertyRelative("id").stringValue = "plant_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            plant.FindPropertyRelative("displayName").stringValue = "新植物";
            plant.FindPropertyRelative("suitableHabitats").intValue = (int)PlantHabitat.Grass;
            plant.FindPropertyRelative("enabledForGeneration").boolValue = true;
            plant.FindPropertyRelative("initialHabitats").intValue = (int)PlantHabitat.Grass;
            plant.FindPropertyRelative("growthWaterPerLife").floatValue = .03f;
            plant.FindPropertyRelative("isForeground").boolValue = true;
            plant.FindPropertyRelative("visualHeight").floatValue = .35f;
            plant.FindPropertyRelative("floodTolerant").boolValue = false;
            plant.FindPropertyRelative("maxHumidityPercent").intValue = 100;
            plant.FindPropertyRelative("maxSoilPercent").intValue = 100;
            plant.FindPropertyRelative("maxLife").intValue = 90;
            var stages = plant.FindPropertyRelative("stages");
            stages.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var stage = stages.GetArrayElementAtIndex(i);
                stage.FindPropertyRelative("minHumidityPercent").intValue = i * 30;
                stage.FindPropertyRelative("minSoilPercent").intValue = i * 30;
                stage.FindPropertyRelative("minLife").intValue = i * 30;
                stage.FindPropertyRelative("sprite").objectReferenceValue = null;
                stage.FindPropertyRelative("fallbackColor").colorValue = new Color(.35f, .75f, .35f);
            }
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            // OnValidate also protects values edited outside this inspector.
            EditorUtility.SetDirty(catalog);
        }
    }

    bool DrawPlant(SerializedProperty plants, SerializedProperty plant, int index)
    {
        var id = plant.FindPropertyRelative("id");
        var name = plant.FindPropertyRelative("displayName");
        var habitats = plant.FindPropertyRelative("suitableHabitats");
        var foreground = plant.FindPropertyRelative("isForeground");
        var floodTolerant = plant.FindPropertyRelative("floodTolerant");
        var humidityMax = plant.FindPropertyRelative("maxHumidityPercent");
        var soilMax = plant.FindPropertyRelative("maxSoilPercent");
        var lifeMax = plant.FindPropertyRelative("maxLife");
        var stages = plant.FindPropertyRelative("stages");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        plant.isExpanded = EditorGUILayout.Foldout(plant.isExpanded, $"{index + 1}. {name.stringValue}  ·  {stages.arraySize} 阶段", true);
        GUI.enabled = index > 0;
        if (GUILayout.Button("↑", GUILayout.Width(27))) { plants.MoveArrayElement(index, index - 1); GUI.enabled = true; EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); return true; }
        GUI.enabled = index < plants.arraySize - 1;
        if (GUILayout.Button("↓", GUILayout.Width(27))) { plants.MoveArrayElement(index, index + 1); GUI.enabled = true; EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); return true; }
        GUI.enabled = true;
        if (GUILayout.Button("删除", GUILayout.Width(46))) { plants.DeleteArrayElementAtIndex(index); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); return true; }
        EditorGUILayout.EndHorizontal();
        if (plant.isExpanded)
        {
            EditorGUILayout.PropertyField(id, new GUIContent("植物 ID"));
            EditorGUILayout.PropertyField(name, new GUIContent("名称"));
            EditorGUILayout.PropertyField(plant.FindPropertyRelative("enabledForGeneration"), new GUIContent("参与初始生成与通用种子池"));
            EditorGUILayout.PropertyField(plant.FindPropertyRelative("initialHabitats"), new GUIContent("初始生成地貌"));
            EditorGUILayout.PropertyField(habitats, new GUIContent("可生存地貌"));
            EditorGUILayout.PropertyField(plant.FindPropertyRelative("growthWaterPerLife"), new GUIContent("每增长1生命耗水"));
            EditorGUILayout.PropertyField(foreground, new GUIContent("前景植物"));
            EditorGUILayout.PropertyField(plant.FindPropertyRelative("visualHeight"), new GUIContent("成熟画面高度"));
            EditorGUILayout.PropertyField(floodTolerant, new GUIContent("耐中等积水"));
            if (habitats.intValue == 0) EditorGUILayout.HelpBox("请至少选择一种适宜地块。", MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("适宜区间总上限");
            GUILayout.Label("湿度 %", GUILayout.Width(48));
            humidityMax.intValue = Mathf.Clamp(EditorGUILayout.IntField(humidityMax.intValue, GUILayout.Width(38)), 0, 100);
            GUILayout.Label("泥土", GUILayout.Width(48));
            soilMax.intValue = Mathf.Clamp(EditorGUILayout.IntField(soilMax.intValue, GUILayout.Width(38)), 0, 100);
            GUILayout.Label("生命", GUILayout.Width(32));
            lifeMax.intValue = Mathf.Max(0, EditorGUILayout.IntField(lifeMax.intValue, GUILayout.Width(48)));
            EditorGUILayout.EndHorizontal();

            int count = Mathf.Clamp(EditorGUILayout.IntField("生长阶段数", stages.arraySize), 1, MaxStages);
            if (count != stages.arraySize) stages.arraySize = count;
            NormalizeStages(stages, humidityMax.intValue, soilMax.intValue, lifeMax.intValue);
            tableScroll = EditorGUILayout.BeginScrollView(tableScroll, true, stages.arraySize > 10,
                GUILayout.Height(Mathf.Min(410, 52 * stages.arraySize + 32)));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("阶段", GUILayout.Width(55));
            GUILayout.Label("适宜湿度 %", GUILayout.Width(128));
            GUILayout.Label("适宜泥土", GUILayout.Width(128));
            GUILayout.Label("生命数值", GUILayout.Width(128));
            EditorGUILayout.EndHorizontal();
            for (int stage = 0; stage < stages.arraySize; stage++)
            {
                var stageProperty = stages.GetArrayElementAtIndex(stage);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((stage + 1).ToString(), GUILayout.Width(55));
                DrawRangeCell(stages, stage, "minHumidityPercent", humidityMax.intValue);
                DrawRangeCell(stages, stage, "minSoilPercent", soilMax.intValue);
                DrawRangeCell(stages, stage, "minLife", lifeMax.intValue);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(55);
                GUILayout.Label("Sprite", GUILayout.Width(44));
                EditorGUILayout.PropertyField(stageProperty.FindPropertyRelative("sprite"), GUIContent.none, GUILayout.Width(145));
                GUILayout.Label("占位色", GUILayout.Width(48));
                EditorGUILayout.PropertyField(stageProperty.FindPropertyRelative("fallbackColor"), GUIContent.none, GUILayout.Width(85));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            if (stages.arraySize == MaxStages) EditorGUILayout.HelpBox("最多可配置 64 个阶段。", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        return false;
    }

    static void DrawRangeCell(SerializedProperty stages, int index, string fieldName, int maximum)
    {
        var field = stages.GetArrayElementAtIndex(index).FindPropertyRelative(fieldName);
        int lowerBound = index == 0 ? 0 : stages.GetArrayElementAtIndex(index - 1).FindPropertyRelative(fieldName).intValue;
        int value = EditorGUILayout.IntField(field.intValue, GUILayout.Width(48));
        if (value != field.intValue)
        {
            field.intValue = Mathf.Clamp(value, lowerBound, maximum);
            for (int i = index + 1; i < stages.arraySize; i++)
            {
                var next = stages.GetArrayElementAtIndex(i).FindPropertyRelative(fieldName);
                next.intValue = Mathf.Max(next.intValue,
                    stages.GetArrayElementAtIndex(i - 1).FindPropertyRelative(fieldName).intValue);
            }
        }
        bool last = index == stages.arraySize - 1;
        int upper = last ? maximum : stages.GetArrayElementAtIndex(index + 1).FindPropertyRelative(fieldName).intValue;
        GUILayout.Label((last ? "～" : "～<") + upper, GUILayout.Width(76));
    }

    static void NormalizeStages(SerializedProperty stages, int humidityMax, int soilMax, int lifeMax)
    {
        int humidity = 0, soil = 0, life = 0;
        for (int i = 0; i < stages.arraySize; i++)
        {
            var stage = stages.GetArrayElementAtIndex(i);
            var h = stage.FindPropertyRelative("minHumidityPercent");
            var s = stage.FindPropertyRelative("minSoilPercent");
            var l = stage.FindPropertyRelative("minLife");
            h.intValue = humidity = Mathf.Clamp(h.intValue, humidity, humidityMax);
            s.intValue = soil = Mathf.Clamp(s.intValue, soil, soilMax);
            l.intValue = life = Mathf.Clamp(l.intValue, life, lifeMax);
        }
    }
}
