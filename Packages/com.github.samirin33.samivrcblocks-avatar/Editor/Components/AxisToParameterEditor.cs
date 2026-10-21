using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(AxisToParameter))]
    [CanEditMultipleObjects]
    public class AxisToParameterEditor : SamirinMABaseEditor
    {
        private static readonly GUIContent[] AxisOptions =
        {
            new GUIContent("X 軸"),
            new GUIContent("Y 軸"),
            new GUIContent("Z 軸"),
        };

        private static readonly GUIContent[] RangeOptions =
        {
            new GUIContent("0〜1（0°〜360°）"),
            new GUIContent("-1〜1（-180°〜180°）"),
            new GUIContent("0〜360（0°〜360°）"),
            new GUIContent("任意範囲"),
        };

        private SerializedProperty _entries;
        private SerializedProperty _localOnly;
        private SerializedProperty _constraintActive;
        private SerializedProperty _contactActive;

        private void OnEnable()
        {
            _entries = serializedObject.FindProperty("entries");
            _localOnly = serializedObject.FindProperty("localOnly");
            _constraintActive = serializedObject.FindProperty("constraintActive");
            _contactActive = serializedObject.FindProperty("contactActive");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "オブジェクトの X/Y/Z 軸まわりの回転角を Float パラメーターとして取得します。" +
                    "ビルド時に Contact と VRC Rotation Constraint の構造と、Contact の値から角度を復元する AAP の Animator を生成します。" +
                    "Constraint 有効・Contact 有効はアニメーションで操作でき、ビルド時に生成先へ書き換えます。",
                    MessageType.Info);

                EditorGUILayout.LabelField("取得する回転");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawEntries();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Contact / Constraint");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_localOnly, new GUIContent("Local Only", _localOnly.tooltip));
                if (!_localOnly.hasMultipleDifferentValues && _localOnly.boolValue)
                {
                    DrawHelpBoxWithDefaultFont(
                        "Local Only のとき、他のプレイヤーの環境では Contact が動かないため出力値は正しくなりません。" +
                        "他のプレイヤーにも見せたい場合は、出力パラメーターを別途同期してください。",
                        MessageType.None);
                }

                EditorGUILayout.PropertyField(_constraintActive, new GUIContent("Constraint 有効", _constraintActive.tooltip));
                EditorGUILayout.PropertyField(_contactActive, new GUIContent("Contact 有効", _contactActive.tooltip));
                DrawEstimate();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(8);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("確認用", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(serializedObject.isEditingMultipleObjects))
                {
                    if (GUILayout.Button("Animator をアセットとして書き出す", GUILayout.Height(15)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        ExportController();
                    }
                }
                EditorGUILayout.EndVertical();

                serializedObject.ApplyModifiedProperties();
            });
        }

        private void DrawEntries()
        {
            var canEditArray = CanEditArrayStructure(serializedObject);
            var pendingIndex = -1;
            var pendingAction = ArrayRowAction.None;

            for (var i = 0; i < _entries.arraySize; i++)
            {
                var element = _entries.GetArrayElementAtIndex(i);
                var parameterName = element.FindPropertyRelative("parameterName");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

                EditorGUILayout.BeginHorizontal();
                var title = parameterName.hasMultipleDifferentValues
                    ? "—"
                    : string.IsNullOrEmpty(parameterName.stringValue) ? "(パラメーター名未設定)" : parameterName.stringValue;
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                var action = DrawArrayRowButtons(i, _entries.arraySize, canEditArray);
                if (action != ArrayRowAction.None)
                {
                    pendingIndex = i;
                    pendingAction = action;
                }
                EditorGUILayout.EndHorizontal();

                DrawEntry(element, i);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }

            ApplyArrayRowAction(_entries, pendingIndex, pendingAction);

            using (new EditorGUI.DisabledScope(!canEditArray))
            {
                if (GUILayout.Button("+ 追加"))
                    AddEntry();
            }
        }

        private void DrawEntry(SerializedProperty element, int index)
        {
            var parameterName = element.FindPropertyRelative("parameterName");
            var target = element.FindPropertyRelative("target");
            var reference = element.FindPropertyRelative("reference");
            var axis = element.FindPropertyRelative("axis");
            var range = element.FindPropertyRelative("range");

            EditorGUILayout.PropertyField(parameterName, new GUIContent("パラメーター名", parameterName.tooltip));
            if (!parameterName.hasMultipleDifferentValues && string.IsNullOrWhiteSpace(parameterName.stringValue))
                DrawHelpBoxWithDefaultFont("パラメーター名を指定してください。未指定のままビルドするとスキップされます。", MessageType.Warning);

            EditorGUILayout.PropertyField(target, new GUIContent("ターゲット（未指定で自身）", target.tooltip));
            EditorGUILayout.PropertyField(reference, new GUIContent("基準（未指定でワールド）", reference.tooltip));
            DrawPopup(axis, new GUIContent("回転軸", axis.tooltip), AxisOptions);
            DrawPopup(range, new GUIContent("出力範囲", range.tooltip), RangeOptions);

            if (range.hasMultipleDifferentValues || range.enumValueIndex == (int)AxisToParameter.OutputRange.Custom)
            {
                EditorGUI.indentLevel++;
                DrawMinMaxRow("角度（度）", element.FindPropertyRelative("customAngleMin"), element.FindPropertyRelative("customAngleMax"));
                DrawMinMaxRow("出力値", element.FindPropertyRelative("customValueMin"), element.FindPropertyRelative("customValueMax"));
                EditorGUI.indentLevel--;

                if (!serializedObject.isEditingMultipleObjects)
                {
                    var entry = GetEntry(index);
                    if (entry != null && !AxisToParameter.IsValidMapping(entry))
                        DrawHelpBoxWithDefaultFont("角度の最大値は最小値より大きくしてください。", MessageType.Warning);
                    else if (entry != null && entry.customAngleMax - entry.customAngleMin > 360f)
                        DrawHelpBoxWithDefaultFont("角度の幅は 360° までです。最小値 + 360° を最大値として扱います。", MessageType.Info);
                    else if (entry != null && entry.customAngleMax - entry.customAngleMin < 360f)
                        DrawHelpBoxWithDefaultFont("範囲外の角度では、近い側の端の値を出力します。", MessageType.None);
                }
            }

            if (!serializedObject.isEditingMultipleObjects)
                DrawPreview(index);
        }

        private AxisToParameter.AxisEntry GetEntry(int index)
        {
            var component = target as AxisToParameter;
            if (component == null || component.entries == null || index < 0 || index >= component.entries.Count)
                return null;
            return component.entries[index];
        }

        private void DrawPreview(int index)
        {
            var component = target as AxisToParameter;
            var entry = GetEntry(index);
            if (component == null || entry == null)
                return;

            var targetTransform = component.ResolveTarget(entry);
            var reference = component.ResolveReference(entry);
            if (reference == targetTransform)
            {
                DrawHelpBoxWithDefaultFont("基準がターゲット自身のため、角度は常に 0 になります。", MessageType.Warning);
                return;
            }

            string text;
            if (AxisToParameter.TryGetAngle(targetTransform, reference, entry.axis, out var angle))
            {
                text = AxisToParameter.IsValidMapping(entry)
                    ? $"{angle:0.0}° → {FormatValue(AxisToParameter.Evaluate(entry, angle))}"
                    : $"{angle:0.0}°";
            }
            else
            {
                text = "計測ベクトルが回転軸と平行なため角度が定まりません";
            }

            var tooltip = reference == null
                ? "エディタ上の姿勢から計算した値です。実行時のワールド基準はアバター読み込み時の向きなので、ヨーはその向きからの角度になります"
                : "エディタ上の姿勢から計算した値です。実際の値はビルド後に Contact から求めます";
            EditorGUILayout.LabelField(new GUIContent("現在の値（目安）", tooltip), new GUIContent(text));
        }

        private static string FormatValue(float value)
        {
            return value.ToString("0.###");
        }

        private void DrawEstimate()
        {
            if (serializedObject.isEditingMultipleObjects)
                return;

            var component = target as AxisToParameter;
            if (component == null)
                return;

            AxisToParameterProcessor.Estimate(component, out var senders, out var receivers, out var constraints);
            var localOnly = component.localOnly ? "（Local Only のためパフォーマンスランクに影響しません）" : string.Empty;
            DrawHelpBoxWithDefaultFont(
                $"計測方式: {AxisSensors.Current.DisplayName}\n" +
                $"生成数: Contact Sender {senders} / Contact Receiver {receivers} / VRC Rotation Constraint {constraints}{localOnly}",
                MessageType.None);
        }

        private static void DrawPopup(SerializedProperty property, GUIContent label, GUIContent[] options)
        {
            var rect = EditorGUILayout.GetControlRect();
            label = EditorGUI.BeginProperty(rect, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.Popup(rect, label, property.enumValueIndex, options);
            if (EditorGUI.EndChangeCheck())
                property.enumValueIndex = value;
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        private static void DrawMinMaxRow(string label, SerializedProperty minProperty, SerializedProperty maxProperty)
        {
            var rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));

            const float gap = 4f;
            var half = Mathf.Max(0f, (rect.width - gap) * 0.5f);
            var minRect = new Rect(rect.x, rect.y, half, rect.height);
            var maxRect = new Rect(rect.x + half + gap, rect.y, half, rect.height);

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            var previousIndent = EditorGUI.indentLevel;
            EditorGUIUtility.labelWidth = 32f;
            EditorGUI.indentLevel = 0;
            DrawFloatFieldInRect(minRect, "最小", minProperty);
            DrawFloatFieldInRect(maxRect, "最大", maxProperty);
            EditorGUI.indentLevel = previousIndent;
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private static void DrawFloatFieldInRect(Rect rect, string label, SerializedProperty property)
        {
            var content = EditorGUI.BeginProperty(rect, new GUIContent(label, property.tooltip), property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.FloatField(rect, content, property.floatValue);
            if (EditorGUI.EndChangeCheck())
                property.floatValue = value;
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        private void AddEntry()
        {
            var index = _entries.arraySize;
            _entries.InsertArrayElementAtIndex(index);
            var element = _entries.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("parameterName").stringValue = string.Empty;
            element.FindPropertyRelative("target").objectReferenceValue = null;
            element.FindPropertyRelative("reference").objectReferenceValue = null;
            element.FindPropertyRelative("axis").enumValueIndex = (int)AxisToParameter.Axis.Y;
            element.FindPropertyRelative("range").enumValueIndex = (int)AxisToParameter.OutputRange.ZeroToOne;
            element.FindPropertyRelative("customAngleMin").floatValue = -180f;
            element.FindPropertyRelative("customAngleMax").floatValue = 180f;
            element.FindPropertyRelative("customValueMin").floatValue = 0f;
            element.FindPropertyRelative("customValueMax").floatValue = 1f;
        }

        private void ExportController()
        {
            var component = target as AxisToParameter;
            var controller = AxisToParameterProcessor.ExportController(component);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("AxisToParameter", "有効な項目が無いため書き出せませんでした。Console の警告を確認してください。", "OK");
                return;
            }

            AssetDatabase.SaveAssets();
            SelectGeneratedAnimators(new[] { controller });
        }
    }
}
