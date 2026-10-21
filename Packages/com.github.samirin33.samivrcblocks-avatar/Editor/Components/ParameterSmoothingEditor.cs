using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(ParameterSmoothing))]
    [CanEditMultipleObjects]
    public class ParameterSmoothingEditor : SamirinMABaseEditor
    {
        private SerializedProperty _defaultSmoothWeight;
        private SerializedProperty _parameterSmoothingData;

        private void OnEnable()
        {
            _defaultSmoothWeight = serializedObject.FindProperty("defaultSmoothWeight");
            _parameterSmoothingData = serializedObject.FindProperty("parameterSmoothingData");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "FloatパラメーターにAAPスムージングをかけることができます！",
                    MessageType.Info);

                EditorGUILayout.LabelField("パラメータスムージング設定");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (_defaultSmoothWeight != null)
                    DrawSmoothWeightField(_defaultSmoothWeight, new GUIContent("デフォルトの重み"));

                EditorGUILayout.Space(4);

                if (_parameterSmoothingData != null)
                {
                    var canEditArray = CanEditArrayStructure(serializedObject);
                    var pendingIndex = -1;
                    var pendingAction = ArrayRowAction.None;

                    for (int i = 0; i < _parameterSmoothingData.arraySize; i++)
                    {
                        var element = _parameterSmoothingData.GetArrayElementAtIndex(i);
                        var parameterNameProp = element.FindPropertyRelative("parameterName");
                        var useDefaultProp = element.FindPropertyRelative("useDefaultSmoothWeight");
                        var smoothWeightProp = element.FindPropertyRelative("smoothWeight");
                        var specifyClampRangeProp = element.FindPropertyRelative("specifyClampRange");
                        var clampMinProp = element.FindPropertyRelative("clampMin");
                        var clampMaxProp = element.FindPropertyRelative("clampMax");

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(parameterNameProp.hasMultipleDifferentValues ? "—" : parameterNameProp.stringValue);
                        GUILayout.FlexibleSpace();
                        var action = DrawArrayRowButtons(i, _parameterSmoothingData.arraySize, canEditArray);
                        if (action != ArrayRowAction.None)
                        {
                            pendingIndex = i;
                            pendingAction = action;
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.PropertyField(parameterNameProp, new GUIContent("パラメータ名"));

                        if (useDefaultProp != null)
                        {
                            EditorGUI.BeginChangeCheck();
                            EditorGUILayout.PropertyField(useDefaultProp, new GUIContent("デフォルトの設定値を使う"));
                            if (EditorGUI.EndChangeCheck() && !useDefaultProp.boolValue && _defaultSmoothWeight != null)
                                smoothWeightProp.floatValue = _defaultSmoothWeight.floatValue;

                            if (!useDefaultProp.boolValue || useDefaultProp.hasMultipleDifferentValues)
                                DrawSmoothWeightField(smoothWeightProp);
                        }
                        else
                        {
                            DrawSmoothWeightField(smoothWeightProp);
                        }

                        DrawClampRangeFields(specifyClampRangeProp, clampMinProp, clampMaxProp);

                        var paramName = parameterNameProp.stringValue;
                        if (!parameterNameProp.hasMultipleDifferentValues && !string.IsNullOrEmpty(paramName))
                        {
                            var smoothedName = $"{paramName}_Smoothed";

                            DrawHelpBoxWithDefaultFont(
                                "以下のパラメータが出力されます。",
                                MessageType.None);

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(smoothedName, GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("コピー", GUILayout.Width(50)))
                                EditorGUIUtility.systemCopyBuffer = smoothedName;
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(3);
                    }

                    ApplyArrayRowAction(_parameterSmoothingData, pendingIndex, pendingAction);

                    using (new EditorGUI.DisabledScope(!canEditArray))
                    {
                        if (GUILayout.Button("+ 追加"))
                            AddElement();
                    }
                }
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
            var index = _parameterSmoothingData.arraySize;
            _parameterSmoothingData.InsertArrayElementAtIndex(index);
            var newElement = _parameterSmoothingData.GetArrayElementAtIndex(index);
            var useDefaultProp = newElement.FindPropertyRelative("useDefaultSmoothWeight");
            if (useDefaultProp != null)
                useDefaultProp.boolValue = true;
            var smoothWeightProp = newElement.FindPropertyRelative("smoothWeight");
            if (smoothWeightProp != null && _defaultSmoothWeight != null)
                smoothWeightProp.floatValue = _defaultSmoothWeight.floatValue;
            var parameterNameProp = newElement.FindPropertyRelative("parameterName");
            if (parameterNameProp != null)
                parameterNameProp.stringValue = "";
            var smoothedNameProp = newElement.FindPropertyRelative("smoothedParameterName");
            if (smoothedNameProp != null)
                smoothedNameProp.stringValue = "";
            var specifyClampRangeProp = newElement.FindPropertyRelative("specifyClampRange");
            if (specifyClampRangeProp != null)
                specifyClampRangeProp.boolValue = false;
            var clampMinProp = newElement.FindPropertyRelative("clampMin");
            if (clampMinProp != null)
                clampMinProp.floatValue = 0f;
            var clampMaxProp = newElement.FindPropertyRelative("clampMax");
            if (clampMaxProp != null)
                clampMaxProp.floatValue = 1f;
        }

        private static void DrawClampRangeFields(
            SerializedProperty specifyClampRangeProp,
            SerializedProperty clampMinProp,
            SerializedProperty clampMaxProp)
        {
            if (specifyClampRangeProp == null || clampMinProp == null || clampMaxProp == null)
                return;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                specifyClampRangeProp,
                new GUIContent("クランプ範囲を指定する", "オフのときは 0〜1 にクランプします。オンにすると、スムージングの入出力を最小〜最大の範囲に収めます。"));
            if (EditorGUI.EndChangeCheck()
                && specifyClampRangeProp.boolValue
                && clampMaxProp.floatValue <= clampMinProp.floatValue)
            {
                clampMinProp.floatValue = 0f;
                clampMaxProp.floatValue = 1f;
            }

            if (!specifyClampRangeProp.boolValue && !specifyClampRangeProp.hasMultipleDifferentValues)
                return;

            var rect = EditorGUILayout.GetControlRect();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, labelWidth, rect.height), "クランプ範囲");

            var gap = 4f;
            var fieldsX = rect.x + labelWidth;
            var half = Mathf.Max(0f, (rect.width - labelWidth - gap) * 0.5f);
            var minRect = new Rect(fieldsX, rect.y, half, rect.height);
            var maxRect = new Rect(fieldsX + half + gap, rect.y, half, rect.height);

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            var previousIndent = EditorGUI.indentLevel;
            EditorGUIUtility.labelWidth = 36f;
            EditorGUI.indentLevel = 0;
            DrawFloatFieldInRect(minRect, "最小", clampMinProp);
            DrawFloatFieldInRect(maxRect, "最大", clampMaxProp);
            EditorGUI.indentLevel = previousIndent;
            EditorGUIUtility.labelWidth = previousLabelWidth;

            if (!clampMinProp.hasMultipleDifferentValues
                && !clampMaxProp.hasMultipleDifferentValues
                && clampMinProp.floatValue >= clampMaxProp.floatValue)
                DrawHelpBoxWithDefaultFont("最小値は最大値より小さくしてください。", MessageType.Warning);
        }

        private static void DrawFloatFieldInRect(Rect rect, string label, SerializedProperty property)
        {
            var content = new GUIContent(label);
            content = EditorGUI.BeginProperty(rect, content, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.FloatField(rect, content, property.floatValue);
            if (EditorGUI.EndChangeCheck())
                property.floatValue = value;
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        private void ManualGenerateAnimator()
        {
            ManualGenerateAnimatorsPerAvatar<ParameterSmoothing>(
                targets,
                "ParameterSmoothing",
                "Manual Generate ParameterSmoothing Animator",
                (avatarRoot, all) => ParameterSmoothingBuilder.BuildManual(avatarRoot, all));
        }
    }
}
