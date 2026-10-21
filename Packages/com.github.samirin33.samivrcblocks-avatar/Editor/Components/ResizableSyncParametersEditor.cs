using UnityEditor;
using UnityEngine;
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
        private SerializedProperty _matchAvatarWriteDefaults;
        private SerializedProperty _replaceWithSmoothedInAnimator;
        private float _bulkSmoothWeight = 0.2f;

        private void OnEnable()
        {
            _syncParamSettings = serializedObject.FindProperty("syncParamSettings");
            _writeDefault = serializedObject.FindProperty("writeDefault");
            _matchAvatarWriteDefaults = serializedObject.FindProperty("matchAvatarWriteDefaults");
            _replaceWithSmoothedInAnimator = serializedObject.FindProperty("replaceWithSmoothedInAnimator");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "パラメーターの同期範囲や精度を削いでBit数を削減できます！",
                    MessageType.Info);

                EditorGUILayout.LabelField("同期パラメータ設定");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (_syncParamSettings != null)
                {
                    DrawBulkSmoothWeightControls();

                    var canEditArray = CanEditArrayStructure(serializedObject);
                    var showSummary = !serializedObject.isEditingMultipleObjects;
                    var pendingIndex = -1;
                    var pendingAction = ArrayRowAction.None;

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
                        EditorGUILayout.LabelField(paramNameProp.hasMultipleDifferentValues ? "—" : paramNameProp.stringValue);
                        GUILayout.FlexibleSpace();
                        var action = DrawArrayRowButtons(i, _syncParamSettings.arraySize, canEditArray);
                        if (action != ArrayRowAction.None)
                        {
                            pendingIndex = i;
                            pendingAction = action;
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.PropertyField(paramNameProp, new GUIContent("パラメータ名"));
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(paramTypeProp, new GUIContent("タイプ"));
                        var paramTypeChanged = EditorGUI.EndChangeCheck();
                        EditorGUILayout.PropertyField(bitTypeProp, new GUIContent("ビット数"));
                        if (!bitTypeProp.hasMultipleDifferentValues
                            && (ResizableSyncParameters.BitType)bitTypeProp.enumValueIndex == ResizableSyncParameters.BitType.Custom)
                        {
                            EditorGUI.showMixedValue = customBitCountProp.hasMultipleDifferentValues;
                            EditorGUI.BeginChangeCheck();
                            var bitCount = EditorGUILayout.IntSlider(
                                "カスタムBit数",
                                customBitCountProp.intValue,
                                ResizableSyncParameters.MinCustomBitCount,
                                ResizableSyncParameters.MaxCustomBitCount);
                            if (EditorGUI.EndChangeCheck())
                                customBitCountProp.intValue = bitCount;
                            EditorGUI.showMixedValue = false;
                        }

                        var paramType = (ResizableSyncParameters.ParamType)paramTypeProp.enumValueIndex;
                        if (paramTypeProp.hasMultipleDifferentValues)
                        {
                            DrawHelpBoxWithDefaultFont("タイプが異なる項目を同時に選択しています。", MessageType.None);
                        }
                        else if (paramType == ResizableSyncParameters.ParamType.Int)
                        {
                            EditorGUILayout.PropertyField(intRangePresetProp, new GUIContent("Int範囲"));
                            if (!intRangePresetProp.hasMultipleDifferentValues
                                && (ResizableSyncParameters.IntRangePreset)intRangePresetProp.enumValueIndex == ResizableSyncParameters.IntRangePreset.Custom)
                            {
                                DrawIntCustomMin(customIntMinProp, element, showSummary);
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

                            if (!floatRangePresetProp.hasMultipleDifferentValues
                                && (ResizableSyncParameters.FloatRangePreset)floatRangePresetProp.enumValueIndex == ResizableSyncParameters.FloatRangePreset.Custom)
                            {
                                DrawCustomFloatRange(customFloatMinProp, customFloatMaxProp);
                            }
                            DrawSmoothWeightField(smoothWeightProp);

                            EditorGUILayout.PropertyField(divisionTypeProp, new GUIContent("分割方式"));
                        }

                        if (showSummary)
                        {
                            var description = GetParamDescription(paramType, element);
                            if (!string.IsNullOrEmpty(description))
                                DrawHelpBoxWithDefaultFont(description, MessageType.None);
                        }

                        var fireValueChangedTriggerProp = element.FindPropertyRelative("fireValueChangedTrigger");
                        EditorGUILayout.PropertyField(fireValueChangedTriggerProp, new GUIContent("値変化トリガー"));
                        if (showSummary && fireValueChangedTriggerProp != null && fireValueChangedTriggerProp.boolValue)
                        {
                            var triggerSourceName = string.IsNullOrEmpty(paramNameProp.stringValue)
                                ? GetDefaultParamName(element)
                                : paramNameProp.stringValue;
                            var triggerName = ResizableSyncParameters.GetValueChangedTriggerName(triggerSourceName);
                            DrawHelpBoxWithDefaultFont(
                                "同期段が変わったフレームにトリガーが有効になります。読み込み直後の初期値では発行しません。",
                                MessageType.None);

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(triggerName, GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("コピー", GUILayout.Width(50)))
                                EditorGUIUtility.systemCopyBuffer = triggerName;
                            EditorGUILayout.EndHorizontal();

                            var stepCount = GetMaxValue(element) + 1;
                            if (stepCount > 128)
                            {
                                DrawHelpBoxWithDefaultFont(
                                    $"同期段が {stepCount} あるため、トリガー用のステートも同数追加されます。",
                                    MessageType.Warning);
                            }
                        }

                        if (showSummary && paramType == ResizableSyncParameters.ParamType.Float)
                        {
                            var paramName = paramNameProp.stringValue;
                            if (string.IsNullOrEmpty(paramName))
                                paramName = GetDefaultParamName(element);
                            var snappedName = $"{paramName}_Snapped";
                            var smoothedName = $"{paramName}_Smoothed";

                            DrawHelpBoxWithDefaultFont(
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

                    ApplyArrayRowAction(_syncParamSettings, pendingIndex, pendingAction);

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
                EditorGUILayout.PropertyField(_replaceWithSmoothedInAnimator, new GUIContent(
                    "Animator内のFloatパラメーターをSmoothedに置き換える",
                    "参照だけ {名前}_Smoothed に付け替えます。_Smoothed でない元パラメーターは残し、Parameter Driver の書き込み先は変えません。"));
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
            var index = _syncParamSettings.arraySize;
            _syncParamSettings.InsertArrayElementAtIndex(index);
            var newElement = _syncParamSettings.GetArrayElementAtIndex(index);
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

        private void ManualGenerateAnimator()
        {
            ManualGenerateAnimatorsPerAvatar<ResizableSyncParameters>(
                targets,
                "ResizableSyncParameters",
                "Manual Generate ResizableSyncParameters Animator",
                (avatarRoot, all) => ResizableSyncParametersBuilder.BuildManual(avatarRoot, all));
        }

        private void DrawBulkSmoothWeightControls()
        {
            EditorGUILayout.LabelField("スムーズ度の一括設定", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            DrawSmoothWeightValue(null, ref _bulkSmoothWeight);
            using (new EditorGUI.DisabledScope(GetFloatElementCount() == 0))
            {
                if (GUILayout.Button("Floatに一括適用", GUILayout.Width(110)))
                    ApplyBulkSmoothWeight();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private int GetFloatElementCount()
        {
            var count = 0;
            for (int i = 0; i < _syncParamSettings.arraySize; i++)
            {
                var element = _syncParamSettings.GetArrayElementAtIndex(i);
                var paramTypeProp = element.FindPropertyRelative("paramType");
                if (paramTypeProp.hasMultipleDifferentValues
                    || (ResizableSyncParameters.ParamType)paramTypeProp.enumValueIndex == ResizableSyncParameters.ParamType.Float)
                    count++;
            }
            return count;
        }

        private void ApplyBulkSmoothWeight()
        {
            // 複数選択時は配列長・タイプがターゲットごとに違うので、ターゲットごとに個別の SerializedObject で書き込む
            serializedObject.ApplyModifiedProperties();
            foreach (var t in targets)
            {
                if (t == null)
                    continue;

                var so = new SerializedObject(t);
                var settings = so.FindProperty("syncParamSettings");
                if (settings == null)
                    continue;

                for (int i = 0; i < settings.arraySize; i++)
                {
                    var element = settings.GetArrayElementAtIndex(i);
                    var paramTypeProp = element.FindPropertyRelative("paramType");
                    if ((ResizableSyncParameters.ParamType)paramTypeProp.enumValueIndex != ResizableSyncParameters.ParamType.Float)
                        continue;

                    var smoothWeightProp = element.FindPropertyRelative("smoothWeight");
                    if (smoothWeightProp != null)
                        smoothWeightProp.floatValue = _bulkSmoothWeight;
                }

                so.ApplyModifiedProperties();
            }
            serializedObject.Update();
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

            var floatResolution = GetResolution(rangeMin, rangeMax, maxValue);
            return $"{rangeMin}～{rangeMax}のFloatを同期できます。(分解能: {FormatNumber(floatResolution)}, {divisionLabel})";
        }

        private static void ApplyDefaultDivisionForFloatRange(SerializedProperty floatRangePresetProp, SerializedProperty divisionTypeProp)
        {
            if (floatRangePresetProp == null || divisionTypeProp == null) return;
            if (floatRangePresetProp.hasMultipleDifferentValues) return;

            switch ((ResizableSyncParameters.FloatRangePreset)floatRangePresetProp.enumValueIndex)
            {
                case ResizableSyncParameters.FloatRangePreset.ZeroToPlusOne:
                case ResizableSyncParameters.FloatRangePreset.Custom:
                    divisionTypeProp.enumValueIndex = (int)ResizableSyncParameters.DivisionType.Even;
                    break;
                case ResizableSyncParameters.FloatRangePreset.MinusOneToPlusOne:
                    // 0 を表現しやすくするため奇数分割
                    divisionTypeProp.enumValueIndex = (int)ResizableSyncParameters.DivisionType.Odd;
                    break;
            }
        }

        private static void DrawIntCustomMin(SerializedProperty minProp, SerializedProperty element, bool showMax)
        {
            EditorGUILayout.PropertyField(minProp, new GUIContent("最小値"));
            if (!showMax || minProp.hasMultipleDifferentValues)
                return;

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
            if (!minProp.hasMultipleDifferentValues
                && !maxProp.hasMultipleDifferentValues
                && minProp.floatValue >= maxProp.floatValue)
                DrawHelpBoxWithDefaultFont("最大値は最小値より大きくしてください。", MessageType.Warning);
        }

        private static (float min, float max) GetFloatCustomRange(float min, float max)
        {
            if (min >= max)
                max = min + 0.0001f;
            return (min, max);
        }

        private static float GetResolution(float rangeMin, float rangeMax, int maxValue)
        {
            // 偶数: (max-min)/(2^bit-1) / 奇数: (max-min)/(2^bit-2)
            if (maxValue <= 0) return rangeMax - rangeMin;
            return (rangeMax - rangeMin) / maxValue;
        }

        private static int GetMaxValue(SerializedProperty element)
        {
            var fullMax = GetIntRangeSpan(element) - 1;
            var paramType = (ResizableSyncParameters.ParamType)element.FindPropertyRelative("paramType").enumValueIndex;
            if (paramType != ResizableSyncParameters.ParamType.Float)
                return fullMax;

            var divisionType = (ResizableSyncParameters.DivisionType)element.FindPropertyRelative("divisionType").enumValueIndex;
            if (divisionType == ResizableSyncParameters.DivisionType.Odd)
                return Mathf.Max(1, fullMax - 1); // 2^bit - 2
            return fullMax; // 2^bit - 1
        }
    }
}
