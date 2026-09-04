using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(ResizableSyncParameters))]
    [CanEditMultipleObjects]
    public class ResizableSyncParametersEditor : SamirinMABaseEditor
    {
        private SerializedProperty _syncParamSettings;
        private SerializedProperty _writeDefault;
        private SerializedProperty _replaceWithSmoothedInAnimator;
        private float _bulkSmoothWeight = 0.2f;

        private void OnEnable()
        {
            _syncParamSettings = serializedObject.FindProperty("syncParamSettings");
            _writeDefault = serializedObject.FindProperty("writeDefault");
            _replaceWithSmoothedInAnimator = serializedObject.FindProperty("replaceWithSmoothedInAnimator");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "パラメーターの同期範囲や精度を削いでBit数を削減できます！",
                    MessageType.Info);

                EditorGUILayout.LabelField("同期パラメータ設定");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (_syncParamSettings != null)
                {
                    DrawBulkSmoothWeightControls();

                    for (int i = 0; i < _syncParamSettings.arraySize; i++)
                    {
                        var element = _syncParamSettings.GetArrayElementAtIndex(i);
                        var paramNameProp = element.FindPropertyRelative("paramName");
                        var paramTypeProp = element.FindPropertyRelative("paramType");
                        var bitTypeProp = element.FindPropertyRelative("bitType");
                        var customBitCountProp = element.FindPropertyRelative("customBitCount");
                        var intRangePresetProp = element.FindPropertyRelative("intRangePreset");
                        var floatRangePresetProp = element.FindPropertyRelative("floatRangePreset");
                        var customIntMinProp = element.FindPropertyRelative("customIntMin");
                        var customFloatMinProp = element.FindPropertyRelative("customFloatMin");
                        var customFloatMaxProp = element.FindPropertyRelative("customFloatMax");
                        var divisionTypeProp = element.FindPropertyRelative("divisionType");
                        var smoothWeightProp = element.FindPropertyRelative("smoothWeight");

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(paramNameProp.stringValue);
                        GUILayout.FlexibleSpace();
                        EditorGUI.BeginDisabledGroup(i == 0);
                        if (GUILayout.Button("↑", GUILayout.Width(24)))
                        {
                            _syncParamSettings.MoveArrayElement(i, i - 1);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                        EditorGUI.EndDisabledGroup();
                        EditorGUI.BeginDisabledGroup(i == _syncParamSettings.arraySize - 1);
                        if (GUILayout.Button("↓", GUILayout.Width(24)))
                        {
                            _syncParamSettings.MoveArrayElement(i, i + 1);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                        EditorGUI.EndDisabledGroup();
                        if (GUILayout.Button("削除", GUILayout.Width(50)))
                        {
                            _syncParamSettings.DeleteArrayElementAtIndex(i);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.PropertyField(paramNameProp, new GUIContent("パラメータ名"));
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(paramTypeProp, new GUIContent("タイプ"));
                        var paramTypeChanged = EditorGUI.EndChangeCheck();
                        EditorGUILayout.PropertyField(bitTypeProp, new GUIContent("ビット数"));
                        if ((ResizableSyncParameters.BitType)bitTypeProp.enumValueIndex == ResizableSyncParameters.BitType.Custom)
                        {
                            customBitCountProp.intValue = EditorGUILayout.IntSlider(
                                "カスタムBit数",
                                customBitCountProp.intValue,
                                ResizableSyncParameters.MinCustomBitCount,
                                ResizableSyncParameters.MaxCustomBitCount);
                        }

                        var paramType = (ResizableSyncParameters.ParamType)paramTypeProp.enumValueIndex;
                        if (paramType == ResizableSyncParameters.ParamType.Int)
                        {
                            EditorGUILayout.PropertyField(intRangePresetProp, new GUIContent("Int範囲"));
                            if ((ResizableSyncParameters.IntRangePreset)intRangePresetProp.enumValueIndex == ResizableSyncParameters.IntRangePreset.Custom)
                            {
                                DrawIntCustomMin(customIntMinProp, element);
                            }
                        }
                        else
                        {
                            EditorGUI.BeginChangeCheck();
                            EditorGUILayout.PropertyField(floatRangePresetProp, new GUIContent("Float範囲"));
                            if (EditorGUI.EndChangeCheck())
                                ApplyDefaultDivisionForFloatRange(floatRangePresetProp, divisionTypeProp);

                            if (paramTypeChanged)
                                ApplyDefaultDivisionForFloatRange(floatRangePresetProp, divisionTypeProp);

                            if ((ResizableSyncParameters.FloatRangePreset)floatRangePresetProp.enumValueIndex == ResizableSyncParameters.FloatRangePreset.Custom)
                            {
                                DrawCustomFloatRange(customFloatMinProp, customFloatMaxProp);
                            }
                            DrawSmoothWeightField(smoothWeightProp);

                            EditorGUILayout.PropertyField(divisionTypeProp, new GUIContent("分割方式"));
                        }

                        var description = GetParamDescription(paramType, element);
                        if (!string.IsNullOrEmpty(description))
                            EditorGUILayout.HelpBox(description, MessageType.None);

                        if (paramType == ResizableSyncParameters.ParamType.Float)
                        {
                            var paramName = paramNameProp.stringValue;
                            if (string.IsNullOrEmpty(paramName))
                                paramName = GetDefaultParamName(element);
                            var snappedName = $"{paramName}_Snapped";
                            var smoothedName = $"{paramName}_Smoothed";

                            EditorGUILayout.HelpBox(
                                "分解能に合わせてスナップとスムージングされた数値を以下から取得できます。(Remoteでの結果はこれに一致します)",
                                MessageType.None);

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(snappedName, GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("コピー", GUILayout.Width(50)))
                                EditorGUIUtility.systemCopyBuffer = snappedName;
                            EditorGUILayout.EndHorizontal();

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(smoothedName, GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("コピー", GUILayout.Width(50)))
                                EditorGUIUtility.systemCopyBuffer = smoothedName;
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(3);
                    }

                    if (GUILayout.Button("+ 追加"))
                    {
                        _syncParamSettings.arraySize++;
                        var newElement = _syncParamSettings.GetArrayElementAtIndex(_syncParamSettings.arraySize - 1);
                        var bitTypeProp = newElement.FindPropertyRelative("bitType");
                        if (bitTypeProp != null)
                            bitTypeProp.enumValueIndex = (int)ResizableSyncParameters.BitType._4bit;
                        var smoothWeightPropNew = newElement.FindPropertyRelative("smoothWeight");
                        if (smoothWeightPropNew != null)
                            smoothWeightPropNew.floatValue = 0.2f;
                        var intRangePresetPropNew = newElement.FindPropertyRelative("intRangePreset");
                        if (intRangePresetPropNew != null)
                            intRangePresetPropNew.enumValueIndex = (int)ResizableSyncParameters.IntRangePreset.FromZero;
                        var floatRangePresetPropNew = newElement.FindPropertyRelative("floatRangePreset");
                        if (floatRangePresetPropNew != null)
                            floatRangePresetPropNew.enumValueIndex = (int)ResizableSyncParameters.FloatRangePreset.ZeroToPlusOne;
                        var customBitCountPropNew = newElement.FindPropertyRelative("customBitCount");
                        if (customBitCountPropNew != null)
                            customBitCountPropNew.intValue = 8;
                        var customIntMinPropNew = newElement.FindPropertyRelative("customIntMin");
                        if (customIntMinPropNew != null)
                            customIntMinPropNew.intValue = 0;
                        var customFloatMaxPropNew = newElement.FindPropertyRelative("customFloatMax");
                        if (customFloatMaxPropNew != null)
                            customFloatMaxPropNew.floatValue = 1f;
                        var divisionTypePropNew = newElement.FindPropertyRelative("divisionType");
                        if (divisionTypePropNew != null && floatRangePresetPropNew != null)
                            ApplyDefaultDivisionForFloatRange(floatRangePresetPropNew, divisionTypePropNew);
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_writeDefault, new GUIContent("生成されるステートのWrite Default"));
                EditorGUILayout.PropertyField(_replaceWithSmoothedInAnimator, new GUIContent("Animator内のFloatパラメーターをSmoothedに置き換える"));
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

        private void ManualGenerateAnimator()
        {
            var selected = targets.OfType<ResizableSyncParameters>().Where(c => c != null).ToArray();
            if (selected.Length == 0)
                return;

            var avatarRoot = FindAvatarRoot(selected[0].transform);
            if (avatarRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "ResizableSyncParameters",
                    "親階層に VRCAvatarDescriptor が見つかりません。アバター配下に配置してから実行してください。",
                    "OK");
                return;
            }

            var all = avatarRoot.GetComponentsInChildren<ResizableSyncParameters>(true);
            Undo.RegisterCompleteObjectUndo(all.Cast<Object>().ToArray(), "Manual Generate ResizableSyncParameters Animator");

            var controllers = ResizableSyncParametersBuilder.BuildManual(avatarRoot, all);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SelectGeneratedAnimators(controllers);

            // EditorUtility.DisplayDialog(
            //     "ResizableSyncParameters",
            //     "Animator を生成しました。",
            //     "OK");
        }

        private static void SelectGeneratedAnimators(UnityEditor.Animations.AnimatorController[] controllers)
        {
            var objects = controllers?.Where(c => c != null).Cast<Object>().ToArray();
            if (objects == null || objects.Length == 0)
                return;

            Selection.objects = objects;
            EditorGUIUtility.PingObject(objects[0]);
        }

        private static GameObject FindAvatarRoot(Transform start)
        {
            var current = start;
            while (current != null)
            {
                if (current.GetComponent<VRCAvatarDescriptor>() != null)
                    return current.gameObject;
                current = current.parent;
            }

            return null;
        }

        private void DrawBulkSmoothWeightControls()
        {
            EditorGUILayout.LabelField("スムーズ度の一括設定", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _bulkSmoothWeight = DrawSmoothWeightValue(_bulkSmoothWeight);
            EditorGUI.BeginDisabledGroup(GetFloatElementCount() == 0);
            if (GUILayout.Button("Floatに一括適用", GUILayout.Width(110)))
                ApplyBulkSmoothWeight();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private static float DrawSmoothWeightValue(float value)
        {
            var label = new GUIContent("スムージング重み(高いほどゆっくり)");
            if (value >= 1f)
                return EditorGUILayout.FloatField(label, value);

            return EditorGUILayout.Slider(label, value, 0f, 1f);
        }

        private int GetFloatElementCount()
        {
            var count = 0;
            for (int i = 0; i < _syncParamSettings.arraySize; i++)
            {
                var element = _syncParamSettings.GetArrayElementAtIndex(i);
                var paramTypeProp = element.FindPropertyRelative("paramType");
                if ((ResizableSyncParameters.ParamType)paramTypeProp.enumValueIndex == ResizableSyncParameters.ParamType.Float)
                    count++;
            }
            return count;
        }

        private void ApplyBulkSmoothWeight()
        {
            for (int i = 0; i < _syncParamSettings.arraySize; i++)
            {
                var element = _syncParamSettings.GetArrayElementAtIndex(i);
                var paramTypeProp = element.FindPropertyRelative("paramType");
                if ((ResizableSyncParameters.ParamType)paramTypeProp.enumValueIndex != ResizableSyncParameters.ParamType.Float)
                    continue;

                var smoothWeightProp = element.FindPropertyRelative("smoothWeight");
                if (smoothWeightProp != null)
                    smoothWeightProp.floatValue = _bulkSmoothWeight;
            }
        }

        private static string GetDefaultParamName(SerializedProperty element)
        {
            var paramType = (ResizableSyncParameters.ParamType)element.FindPropertyRelative("paramType").enumValueIndex;
            var bitType = (ResizableSyncParameters.BitType)element.FindPropertyRelative("bitType").enumValueIndex;
            return $"Param_{paramType}{bitType}";
        }

        private static string GetParamDescription(ResizableSyncParameters.ParamType paramType, SerializedProperty element)
        {
            var maxValue = GetMaxValue(element);
            var divisionType = (ResizableSyncParameters.DivisionType)element.FindPropertyRelative("divisionType").enumValueIndex;
            var divisionLabel = divisionType == ResizableSyncParameters.DivisionType.Even ? "偶数分割" : "奇数分割";

            if (paramType == ResizableSyncParameters.ParamType.Int)
            {
                var (min, max) = GetIntSourceRange(element);
                return $"{min}~{max}のIntを同期できます。";
            }

            var floatPreset = (ResizableSyncParameters.FloatRangePreset)element.FindPropertyRelative("floatRangePreset").enumValueIndex;
            float rangeMin, rangeMax;
            switch (floatPreset)
            {
                case ResizableSyncParameters.FloatRangePreset.MinusOneToPlusOne:
                    rangeMin = -1f;
                    rangeMax = 1f;
                    break;
                case ResizableSyncParameters.FloatRangePreset.ZeroToPlusOne:
                    rangeMin = 0f;
                    rangeMax = 1f;
                    break;
                default:
                    (rangeMin, rangeMax) = GetFloatCustomRange(
                        element.FindPropertyRelative("customFloatMin").floatValue,
                        element.FindPropertyRelative("customFloatMax").floatValue);
                    break;
            }

            var floatResolution = GetResolution(rangeMin, rangeMax, maxValue, divisionType);
            return $"{rangeMin}～{rangeMax}のFloatを同期できます。(分解能: {FormatResolution(floatResolution)}, {divisionLabel})";
        }

        private static void ApplyDefaultDivisionForFloatRange(SerializedProperty floatRangePresetProp, SerializedProperty divisionTypeProp)
        {
            if (floatRangePresetProp == null || divisionTypeProp == null) return;

            switch ((ResizableSyncParameters.FloatRangePreset)floatRangePresetProp.enumValueIndex)
            {
                case ResizableSyncParameters.FloatRangePreset.ZeroToPlusOne:
                    divisionTypeProp.enumValueIndex = (int)ResizableSyncParameters.DivisionType.Even;
                    break;
                case ResizableSyncParameters.FloatRangePreset.MinusOneToPlusOne:
                    divisionTypeProp.enumValueIndex = (int)ResizableSyncParameters.DivisionType.Odd;
                    break;
            }
        }

        private static void DrawIntCustomMin(SerializedProperty minProp, SerializedProperty element)
        {
            EditorGUILayout.PropertyField(minProp, new GUIContent("最小値"));
            var span = GetIntRangeSpan(element);
            EditorGUILayout.LabelField("最大値", (minProp.intValue + span).ToString());
        }

        private static (int min, int max) GetIntSourceRange(SerializedProperty element)
        {
            var span = GetIntRangeSpan(element);
            var preset = (ResizableSyncParameters.IntRangePreset)element.FindPropertyRelative("intRangePreset").enumValueIndex;
            var min = preset == ResizableSyncParameters.IntRangePreset.FromZero
                ? 0
                : element.FindPropertyRelative("customIntMin").intValue;
            return (min, min + span);
        }

        private static int GetIntRangeSpan(SerializedProperty element)
        {
            return 1 << GetBitCount(element);
        }

        private static int GetBitCount(SerializedProperty element)
        {
            var bitType = (ResizableSyncParameters.BitType)element.FindPropertyRelative("bitType").enumValueIndex;
            if (bitType == ResizableSyncParameters.BitType.Custom)
                return Mathf.Clamp(element.FindPropertyRelative("customBitCount").intValue, ResizableSyncParameters.MinCustomBitCount, ResizableSyncParameters.MaxCustomBitCount);

            switch (bitType)
            {
                case ResizableSyncParameters.BitType._1bit: return 1;
                case ResizableSyncParameters.BitType._2bit: return 2;
                case ResizableSyncParameters.BitType._3bit: return 3;
                case ResizableSyncParameters.BitType._4bit: return 4;
                case ResizableSyncParameters.BitType._5bit: return 5;
                case ResizableSyncParameters.BitType._6bit: return 6;
                case ResizableSyncParameters.BitType._7bit: return 7;
                default: return 1;
            }
        }

        private static void DrawCustomFloatRange(SerializedProperty minProp, SerializedProperty maxProp)
        {
            EditorGUILayout.PropertyField(minProp, new GUIContent("最小値"));
            EditorGUILayout.PropertyField(maxProp, new GUIContent("最大値"));
            if (minProp.floatValue >= maxProp.floatValue)
                EditorGUILayout.HelpBox("最大値は最小値より大きくしてください。", MessageType.Warning);
        }

        private static (float min, float max) GetFloatCustomRange(float min, float max)
        {
            if (min >= max)
                max = min + 0.0001f;
            return (min, max);
        }

        private static float GetResolution(float rangeMin, float rangeMax, int maxValue, ResizableSyncParameters.DivisionType divisionType)
        {
            var range = rangeMax - rangeMin;
            return divisionType == ResizableSyncParameters.DivisionType.Odd
                ? range / (maxValue + 1f)
                : range / maxValue;
        }

        private static string FormatResolution(float value)
        {
            var rounded = Mathf.Round(value);
            if (Mathf.Approximately(value, rounded))
                return rounded.ToString("0");

            var text = value.ToString("0.######");
            return text.TrimEnd('0').TrimEnd('.');
        }

        private static int GetMaxValue(SerializedProperty element)
        {
            return GetIntRangeSpan(element) - 1;
        }
    }
}
