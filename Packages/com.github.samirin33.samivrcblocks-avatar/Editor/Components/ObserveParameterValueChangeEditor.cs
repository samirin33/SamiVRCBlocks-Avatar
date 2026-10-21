using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(ObserveParameterValueChange))]
    [CanEditMultipleObjects]
    public class ObserveParameterValueChangeEditor : SamirinMABaseEditor
    {
        private SerializedProperty _observeSettings;
        private SerializedProperty _writeDefault;
        private SerializedProperty _matchAvatarWriteDefaults;

        private void OnEnable()
        {
            _observeSettings = serializedObject.FindProperty("observeSettings");
            _writeDefault = serializedObject.FindProperty("writeDefault");
            _matchAvatarWriteDefaults = serializedObject.FindProperty("matchAvatarWriteDefaults");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "指定したパラメーターが閾値の間隔をまたいで変化したとき、(パラメーター名)_ValueChanged トリガーが有効になります。読み込み直後の値では発行しません。",
                    MessageType.Info);

                EditorGUILayout.LabelField("監視パラメータ設定");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (_observeSettings != null)
                {
                    var canEditArray = CanEditArrayStructure(serializedObject);
                    var pendingIndex = -1;
                    var pendingAction = ArrayRowAction.None;
                    for (int i = 0; i < _observeSettings.arraySize; i++)
                    {
                        var element = _observeSettings.GetArrayElementAtIndex(i);
                        var action = DrawSetting(element, i, canEditArray);
                        if (action != ArrayRowAction.None)
                        {
                            pendingIndex = i;
                            pendingAction = action;
                        }
                        EditorGUILayout.Space(3);
                    }

                    ApplyArrayRowAction(_observeSettings, pendingIndex, pendingAction);

                    using (new EditorGUI.DisabledScope(!canEditArray))
                    {
                        if (GUILayout.Button("+ 追加"))
                            AddSetting();
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawWriteDefaultsSettings(_matchAvatarWriteDefaults, _writeDefault);
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

        private ArrayRowAction DrawSetting(SerializedProperty element, int index, bool canEditArray)
        {
            var paramNameProp = element.FindPropertyRelative("paramName");
            var paramTypeProp = element.FindPropertyRelative("paramType");
            var thresholdProp = element.FindPropertyRelative("threshold");
            var floatRangePresetProp = element.FindPropertyRelative("floatRangePreset");
            var customFloatMinProp = element.FindPropertyRelative("customFloatMin");
            var customFloatMaxProp = element.FindPropertyRelative("customFloatMax");
            var intMinProp = element.FindPropertyRelative("intMin");
            var intMaxProp = element.FindPropertyRelative("intMax");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(paramNameProp.hasMultipleDifferentValues ? "—" : paramNameProp.stringValue);
            GUILayout.FlexibleSpace();
            var action = DrawArrayRowButtons(index, _observeSettings.arraySize, canEditArray);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(paramNameProp, new GUIContent("パラメータ名"));
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(paramTypeProp, new GUIContent("タイプ"));
            var paramTypeChanged = EditorGUI.EndChangeCheck();

            var paramType = (ObserveParameterValueChange.ParamType)paramTypeProp.enumValueIndex;
            if (paramTypeChanged && paramType == ObserveParameterValueChange.ParamType.Int && thresholdProp.floatValue < 1f)
                thresholdProp.floatValue = 1f;
            if (paramTypeChanged && paramType == ObserveParameterValueChange.ParamType.Float && thresholdProp.floatValue < ObserveParameterValueChange.MinFloatThreshold)
                thresholdProp.floatValue = 0.1f;

            if (paramTypeProp.hasMultipleDifferentValues)
            {
                DrawHelpBoxWithDefaultFont("タイプが異なる項目を同時に選択しています。", MessageType.None);
            }
            else if (paramType == ObserveParameterValueChange.ParamType.Bool)
            {
                DrawHelpBoxWithDefaultFont("オン / オフが切り替わったときにトリガーが有効になります。", MessageType.None);
            }
            else if (paramType == ObserveParameterValueChange.ParamType.Int)
            {
                var intThreshold = Mathf.Max(1, Mathf.RoundToInt(thresholdProp.floatValue));
                EditorGUI.showMixedValue = thresholdProp.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var editedIntThreshold = EditorGUILayout.IntField("変化間隔", intThreshold);
                if (EditorGUI.EndChangeCheck())
                    thresholdProp.floatValue = Mathf.Max(1, editedIntThreshold);
                EditorGUI.showMixedValue = false;
                EditorGUILayout.PropertyField(intMinProp, new GUIContent("最小値"));
                EditorGUILayout.PropertyField(intMaxProp, new GUIContent("最大値"));
                if (intMinProp.intValue > intMaxProp.intValue)
                    DrawHelpBoxWithDefaultFont("最大値は最小値以上にしてください。", MessageType.Warning);
            }
            else
            {
                EditorGUI.showMixedValue = thresholdProp.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var editedThreshold = EditorGUILayout.FloatField("変化間隔", thresholdProp.floatValue);
                if (EditorGUI.EndChangeCheck())
                    thresholdProp.floatValue = Mathf.Max(ObserveParameterValueChange.MinFloatThreshold, editedThreshold);
                EditorGUI.showMixedValue = false;
                if (!thresholdProp.hasMultipleDifferentValues && thresholdProp.floatValue < ObserveParameterValueChange.MinFloatThreshold)
                {
                    DrawHelpBoxWithDefaultFont(
                        $"変化間隔が {FormatNumber(ObserveParameterValueChange.MinFloatThreshold)} 未満のため、生成時は {FormatNumber(ObserveParameterValueChange.MinFloatThreshold)} として扱われます。",
                        MessageType.Warning);
                }
                EditorGUILayout.PropertyField(floatRangePresetProp, new GUIContent("Float範囲"));
                if (!floatRangePresetProp.hasMultipleDifferentValues
                    && (ObserveParameterValueChange.FloatRangePreset)floatRangePresetProp.enumValueIndex == ObserveParameterValueChange.FloatRangePreset.Custom)
                {
                    EditorGUILayout.PropertyField(customFloatMinProp, new GUIContent("最小値"));
                    EditorGUILayout.PropertyField(customFloatMaxProp, new GUIContent("最大値"));
                    if (customFloatMinProp.floatValue >= customFloatMaxProp.floatValue)
                        DrawHelpBoxWithDefaultFont("最大値は最小値より大きくしてください。", MessageType.Warning);
                }
            }

            if (!serializedObject.isEditingMultipleObjects)
                DrawBucketSummary(element);

            EditorGUILayout.EndVertical();
            return action;
        }

        private static void DrawBucketSummary(SerializedProperty element)
        {
            var setting = ReadSetting(element);
            var triggerName = ObserveParameterValueChange.GetValueChangedTriggerName(setting);
            if (!ObserveParameterValueChange.TryGetBucketInfo(setting, out var info))
                return;

            if (setting.paramType != ObserveParameterValueChange.ParamType.Bool)
            {
                if (info.bucketCount < 2)
                {
                    DrawHelpBoxWithDefaultFont("この範囲と間隔では段が1つのため、トリガーは発行されません。", MessageType.Warning);
                }
                else
                {
                    var message = $"{info.rangeMin}～{info.rangeMax} を間隔 {FormatNumber(info.effectiveThreshold)} の {info.bucketCount} 段で監視します。";
                    DrawHelpBoxWithDefaultFont(message, info.thresholdClamped ? MessageType.Warning : MessageType.None);
                    if (info.thresholdClamped)
                    {
                        DrawHelpBoxWithDefaultFont(
                            $"段数が {ObserveParameterValueChange.MaxBucketCount} を超えるため、間隔を広げて生成します。",
                            MessageType.Warning);
                    }
                }
            }

            if (setting.paramType == ObserveParameterValueChange.ParamType.Bool || info.bucketCount >= 2)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(triggerName, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("コピー", GUILayout.Width(50)))
                    EditorGUIUtility.systemCopyBuffer = triggerName;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void AddSetting()
        {
            var index = _observeSettings.arraySize;
            _observeSettings.InsertArrayElementAtIndex(index);
            var element = _observeSettings.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("paramName").stringValue = "";
            element.FindPropertyRelative("paramType").enumValueIndex = (int)ObserveParameterValueChange.ParamType.Float;
            element.FindPropertyRelative("threshold").floatValue = 0.1f;
            element.FindPropertyRelative("floatRangePreset").enumValueIndex = (int)ObserveParameterValueChange.FloatRangePreset.ZeroToPlusOne;
            element.FindPropertyRelative("customFloatMin").floatValue = 0f;
            element.FindPropertyRelative("customFloatMax").floatValue = 1f;
            element.FindPropertyRelative("intMin").intValue = 0;
            element.FindPropertyRelative("intMax").intValue = 1;
        }

        private static ObserveParameterValueChange.ObserveSetting ReadSetting(SerializedProperty element)
        {
            return new ObserveParameterValueChange.ObserveSetting
            {
                paramName = element.FindPropertyRelative("paramName").stringValue,
                paramType = (ObserveParameterValueChange.ParamType)element.FindPropertyRelative("paramType").enumValueIndex,
                threshold = element.FindPropertyRelative("threshold").floatValue,
                floatRangePreset = (ObserveParameterValueChange.FloatRangePreset)element.FindPropertyRelative("floatRangePreset").enumValueIndex,
                customFloatMin = element.FindPropertyRelative("customFloatMin").floatValue,
                customFloatMax = element.FindPropertyRelative("customFloatMax").floatValue,
                intMin = element.FindPropertyRelative("intMin").intValue,
                intMax = element.FindPropertyRelative("intMax").intValue,
            };
        }

        private void ManualGenerateAnimator()
        {
            var controllers = ManualGenerateAnimatorsPerAvatar<ObserveParameterValueChange>(
                targets,
                "ObserveParameterValueChange",
                "Manual Generate ObserveParameterValueChange Animator",
                (avatarRoot, all) => ObserveParameterValueChangeBuilder.BuildManual(avatarRoot, all));
            if (controllers == null || controllers.Count > 0)
                return;

            EditorUtility.DisplayDialog(
                "ObserveParameterValueChange",
                "有効な監視設定がないため Animator は生成されませんでした。",
                "OK");
        }
    }
}
