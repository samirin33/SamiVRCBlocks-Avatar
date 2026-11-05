using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(SnappedParameter))]
    [CanEditMultipleObjects]
    public class SnappedParameterEditor : SamirinMABaseEditor
    {
        private SerializedProperty _snapSettings;
        private SerializedProperty _writeDefault;
        private SerializedProperty _matchAvatarWriteDefaults;
        private SerializedProperty _replaceWithSnapSmoothedInAnimator;
        private float _bulkSmoothWeight = 0.2f;

        private void OnEnable()
        {
            _snapSettings = serializedObject.FindProperty("snapSettings");
            _writeDefault = serializedObject.FindProperty("writeDefault");
            _matchAvatarWriteDefaults = serializedObject.FindProperty("matchAvatarWriteDefaults");
            _replaceWithSnapSmoothedInAnimator = serializedObject.FindProperty("replaceWithSnapSmoothedInAnimator");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "指定した Float パラメーターを任意範囲で n 分割し、スナップ値とスムージング値を出力します。同期はしません。",
                    MessageType.Info);

                EditorGUILayout.LabelField("スナップ設定");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (_snapSettings != null)
                {
                    DrawBulkSmoothWeightControls();

                    var canEditArray = CanEditArrayStructure(serializedObject);
                    var showSummary = !serializedObject.isEditingMultipleObjects;
                    var pendingIndex = -1;
                    var pendingAction = ArrayRowAction.None;

                    for (int i = 0; i < _snapSettings.arraySize; i++)
                    {
                        var element = _snapSettings.GetArrayElementAtIndex(i);
                        var paramNameProp = element.FindPropertyRelative("paramName");
                        var floatRangePresetProp = element.FindPropertyRelative("floatRangePreset");
                        var customFloatMinProp = element.FindPropertyRelative("customFloatMin");
                        var customFloatMaxProp = element.FindPropertyRelative("customFloatMax");
                        var divisionsProp = element.FindPropertyRelative("divisions");
                        var smoothWeightProp = element.FindPropertyRelative("smoothWeight");

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(paramNameProp.hasMultipleDifferentValues
                            ? "—"
                            : (string.IsNullOrEmpty(paramNameProp.stringValue) ? "Param" : paramNameProp.stringValue));
                        GUILayout.FlexibleSpace();
                        var action = DrawArrayRowButtons(i, _snapSettings.arraySize, canEditArray);
                        if (action != ArrayRowAction.None)
                        {
                            pendingIndex = i;
                            pendingAction = action;
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.PropertyField(paramNameProp, new GUIContent("パラメータ名"));
                        EditorGUILayout.PropertyField(floatRangePresetProp, new GUIContent("Float範囲"));

                        if (!floatRangePresetProp.hasMultipleDifferentValues
                            && (SnappedParameter.FloatRangePreset)floatRangePresetProp.enumValueIndex
                            == SnappedParameter.FloatRangePreset.Custom)
                        {
                            DrawCustomFloatRange(customFloatMinProp, customFloatMaxProp);
                        }

                        EditorGUI.showMixedValue = divisionsProp.hasMultipleDifferentValues;
                        EditorGUI.BeginChangeCheck();
                        var divisions = EditorGUILayout.IntSlider(
                            new GUIContent("分割数", "範囲を何等分するか。段階数は分割数+1（端点を含む）。"),
                            divisionsProp.intValue,
                            SnappedParameter.MinDivisions,
                            SnappedParameter.MaxDivisions);
                        if (EditorGUI.EndChangeCheck())
                            divisionsProp.intValue = divisions;
                        EditorGUI.showMixedValue = false;

                        DrawSmoothWeightField(smoothWeightProp);

                        if (showSummary)
                        {
                            var description = GetParamDescription(element);
                            if (!string.IsNullOrEmpty(description))
                                DrawHelpBoxWithDefaultFont(description, MessageType.None);

                            var paramName = string.IsNullOrEmpty(paramNameProp.stringValue)
                                ? "Param"
                                : paramNameProp.stringValue;
                            var snappedName = SnappedParameter.GetSnappedParamName(paramName);
                            var snapSmoothedName = SnappedParameter.GetSnapSmoothedParamName(paramName);

                            DrawHelpBoxWithDefaultFont(
                                "分解能に合わせてスナップとスムージングされた数値を以下から取得できます。",
                                MessageType.None);

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(snappedName, GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("コピー", GUILayout.Width(50)))
                                EditorGUIUtility.systemCopyBuffer = snappedName;
                            EditorGUILayout.EndHorizontal();

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(snapSmoothedName, GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("コピー", GUILayout.Width(50)))
                                EditorGUIUtility.systemCopyBuffer = snapSmoothedName;
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(3);
                    }

                    ApplyArrayRowAction(_snapSettings, pendingIndex, pendingAction);

                    using (new EditorGUI.DisabledScope(!canEditArray))
                    {
                        if (GUILayout.Button("+ 追加"))
                            AddElement();
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawWriteDefaultsSettings(_matchAvatarWriteDefaults, _writeDefault);
                EditorGUILayout.PropertyField(_replaceWithSnapSmoothedInAnimator, new GUIContent(
                    "Animator内のFloatパラメーターをSnapSmoothedに置き換える",
                    "参照だけ {名前}_SnapSmoothed に付け替えます。元パラメーターは残し、Parameter Driver の書き込み先は変えません。"));
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(8);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("マニュアル生成", EditorStyles.boldLabel);
                if (GUILayout.Button("Animatorをマニュアル生成", GUILayout.Height(15)))
                {
                    serializedObject.ApplyModifiedProperties();
                    ManualGenerateAnimator();
                }
                EditorGUILayout.EndVertical();

                serializedObject.ApplyModifiedProperties();
            });
        }

        private void AddElement()
        {
            var index = _snapSettings.arraySize;
            _snapSettings.InsertArrayElementAtIndex(index);
            var newElement = _snapSettings.GetArrayElementAtIndex(index);

            var paramNameProp = newElement.FindPropertyRelative("paramName");
            if (paramNameProp != null)
                paramNameProp.stringValue = "";

            var floatRangePresetProp = newElement.FindPropertyRelative("floatRangePreset");
            if (floatRangePresetProp != null)
                floatRangePresetProp.enumValueIndex = (int)SnappedParameter.FloatRangePreset.ZeroToPlusOne;

            var customFloatMinProp = newElement.FindPropertyRelative("customFloatMin");
            if (customFloatMinProp != null)
                customFloatMinProp.floatValue = 0f;

            var customFloatMaxProp = newElement.FindPropertyRelative("customFloatMax");
            if (customFloatMaxProp != null)
                customFloatMaxProp.floatValue = 1f;

            var divisionsProp = newElement.FindPropertyRelative("divisions");
            if (divisionsProp != null)
                divisionsProp.intValue = 16;

            var smoothWeightProp = newElement.FindPropertyRelative("smoothWeight");
            if (smoothWeightProp != null)
                smoothWeightProp.floatValue = 0.2f;
        }

        private void ManualGenerateAnimator()
        {
            ManualGenerateAnimatorsPerAvatar<SnappedParameter>(
                targets,
                "SnappedParameter",
                "Manual Generate SnappedParameter Animator",
                (avatarRoot, all) => SnappedParameterBuilder.BuildManual(avatarRoot, all));
        }

        private void DrawBulkSmoothWeightControls()
        {
            EditorGUILayout.LabelField("スムーズ度の一括設定", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            DrawSmoothWeightValue(null, ref _bulkSmoothWeight);
            using (new EditorGUI.DisabledScope(_snapSettings.arraySize == 0))
            {
                if (GUILayout.Button("一括適用", GUILayout.Width(110)))
                    ApplyBulkSmoothWeight();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void ApplyBulkSmoothWeight()
        {
            serializedObject.ApplyModifiedProperties();
            foreach (var t in targets)
            {
                if (t == null)
                    continue;

                var so = new SerializedObject(t);
                var settings = so.FindProperty("snapSettings");
                if (settings == null)
                    continue;

                for (int i = 0; i < settings.arraySize; i++)
                {
                    var smoothWeightProp = settings.GetArrayElementAtIndex(i).FindPropertyRelative("smoothWeight");
                    if (smoothWeightProp != null)
                        smoothWeightProp.floatValue = _bulkSmoothWeight;
                }

                so.ApplyModifiedProperties();
            }
            serializedObject.Update();
        }

        private static string GetParamDescription(SerializedProperty element)
        {
            var (rangeMin, rangeMax) = GetSourceRange(element);
            var divisions = Mathf.Clamp(
                element.FindPropertyRelative("divisions").intValue,
                SnappedParameter.MinDivisions,
                SnappedParameter.MaxDivisions);
            var resolution = divisions <= 0 ? rangeMax - rangeMin : (rangeMax - rangeMin) / divisions;
            var levelCount = divisions + 1;

            var hint = string.Empty;
            if (Mathf.Approximately(rangeMin, -1f) && Mathf.Approximately(rangeMax, 1f) && divisions % 2 != 0)
                hint = " ※0 を含めたい場合は偶数の分割数にしてください。";

            return $"{rangeMin}～{rangeMax} を {divisions} 分割（{levelCount} 段階, 分解能: {FormatNumber(resolution)}）。{hint}".Trim();
        }

        private static (float min, float max) GetSourceRange(SerializedProperty element)
        {
            var preset = (SnappedParameter.FloatRangePreset)element.FindPropertyRelative("floatRangePreset").enumValueIndex;
            switch (preset)
            {
                case SnappedParameter.FloatRangePreset.MinusOneToPlusOne:
                    return (-1f, 1f);
                case SnappedParameter.FloatRangePreset.ZeroToPlusOne:
                    return (0f, 1f);
                default:
                    return SnappedParameter.GetFloatCustomRange(
                        element.FindPropertyRelative("customFloatMin").floatValue,
                        element.FindPropertyRelative("customFloatMax").floatValue);
            }
        }

        private static void DrawCustomFloatRange(SerializedProperty minProp, SerializedProperty maxProp)
        {
            EditorGUILayout.PropertyField(minProp, new GUIContent("最小値"));
            EditorGUILayout.PropertyField(maxProp, new GUIContent("最大値"));
            if (!minProp.hasMultipleDifferentValues
                && !maxProp.hasMultipleDifferentValues
                && minProp.floatValue >= maxProp.floatValue)
                DrawHelpBoxWithDefaultFont("最大値は最小値より大きくしてください。", MessageType.Warning);
        }
    }
}
