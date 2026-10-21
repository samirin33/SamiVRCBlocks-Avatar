using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(TuningObject))]
    [CanEditMultipleObjects]
    public class TuningObjectEditor : SamirinMABaseEditor
    {
        const string ShowDeveloperSettingsPrefsKey = "Samirin33.TuningObject.ShowDeveloperSettings";
        const string DeveloperSettingsMenuPath = "CONTEXT/TuningObject/開発者設定を表示";

        static bool ShowDeveloperSettings
        {
            get => GetDeveloperSettingsPref(ShowDeveloperSettingsPrefsKey);
            set => SetDeveloperSettingsPref(ShowDeveloperSettingsPrefsKey, value);
        }

        [MenuItem(DeveloperSettingsMenuPath, false, 1000)]
        static void ToggleDeveloperSettings()
        {
            ToggleDeveloperSettingsPref(ShowDeveloperSettingsPrefsKey);
        }

        [MenuItem(DeveloperSettingsMenuPath, true)]
        static bool ToggleDeveloperSettingsValidate()
        {
            return ValidateDeveloperSettingsMenu(DeveloperSettingsMenuPath, ShowDeveloperSettingsPrefsKey);
        }

        private static readonly GUIContent TargetSizeContent = new GUIContent("ターゲット数", "Target として扱う Transform の数");
        private static readonly GUIContent TargetTransformContent = new GUIContent("Transform", "自身の Transform と Offset を適用する対象");
        private static readonly GUIContent OffsetPositionContent = new GUIContent("位置オフセット", "Target に適用する位置のオフセット");
        private static readonly GUIContent OffsetRotationContent = new GUIContent("回転オフセット", "Target に適用する回転のオフセット（Euler）");
        private static readonly GUIContent OffsetScaleContent = new GUIContent("スケール倍率", "Target に適用するスケールの倍率");

        private SerializedProperty _active;
        private SerializedProperty _previewParticles;
        private SerializedProperty _targetTransforms;
        private SerializedProperty _showSphere;
        private SerializedProperty _sphereRadius;
        private SerializedProperty _sphereColor;
        private SerializedProperty _showBox;
        private SerializedProperty _boxSize;
        private SerializedProperty _boxColor;
        private SerializedProperty _showCapsule;
        private SerializedProperty _capsuleRadius;
        private SerializedProperty _capsuleHeight;
        private SerializedProperty _capsuleDirection;
        private SerializedProperty _capsuleColor;
        private SerializedProperty _arrows;
        private SerializedProperty _showLabel;
        private SerializedProperty _labelText;
        private SerializedProperty _labelColor;
        private SerializedProperty _labelOffset;
        private SerializedProperty _showMesh;
        private SerializedProperty _previewMesh;
        private SerializedProperty _meshColor;
        private SerializedProperty _meshOffset;
        private SerializedProperty _meshRotation;
        private SerializedProperty _meshScale;

        private void OnEnable()
        {
            _active = serializedObject.FindProperty(nameof(TuningObject.active));
            _previewParticles = serializedObject.FindProperty(nameof(TuningObject.previewParticles));
            _targetTransforms = serializedObject.FindProperty(nameof(TuningObject.targetTransforms));
            _showSphere = serializedObject.FindProperty(nameof(TuningObject.showSphere));
            _sphereRadius = serializedObject.FindProperty(nameof(TuningObject.sphereRadius));
            _sphereColor = serializedObject.FindProperty(nameof(TuningObject.sphereColor));
            _showBox = serializedObject.FindProperty(nameof(TuningObject.showBox));
            _boxSize = serializedObject.FindProperty(nameof(TuningObject.boxSize));
            _boxColor = serializedObject.FindProperty(nameof(TuningObject.boxColor));
            _showCapsule = serializedObject.FindProperty(nameof(TuningObject.showCapsule));
            _capsuleRadius = serializedObject.FindProperty(nameof(TuningObject.capsuleRadius));
            _capsuleHeight = serializedObject.FindProperty(nameof(TuningObject.capsuleHeight));
            _capsuleDirection = serializedObject.FindProperty(nameof(TuningObject.capsuleDirection));
            _capsuleColor = serializedObject.FindProperty(nameof(TuningObject.capsuleColor));
            _arrows = serializedObject.FindProperty(nameof(TuningObject.arrows));
            _showLabel = serializedObject.FindProperty(nameof(TuningObject.showLabel));
            _labelText = serializedObject.FindProperty(nameof(TuningObject.labelText));
            _labelColor = serializedObject.FindProperty(nameof(TuningObject.labelColor));
            _labelOffset = serializedObject.FindProperty(nameof(TuningObject.labelOffset));
            _showMesh = serializedObject.FindProperty(nameof(TuningObject.showMesh));
            _previewMesh = serializedObject.FindProperty(nameof(TuningObject.previewMesh));
            _meshColor = serializedObject.FindProperty(nameof(TuningObject.meshColor));
            _meshOffset = serializedObject.FindProperty(nameof(TuningObject.meshOffset));
            _meshRotation = serializedObject.FindProperty(nameof(TuningObject.meshRotation));
            _meshScale = serializedObject.FindProperty(nameof(TuningObject.meshScale));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                var showDeveloperSettings = ShowDeveloperSettings;
                if (showDeveloperSettings)
                {
                    DrawHelpBoxWithDefaultFont(
                        "Scene ビューに矢印・球・ボックス・カプセル・テキスト・半透明メッシュを描画するチューニング用ギズモです。\n" +
                        "有効のとき、Target に自身の Transform と Offset を適用します。適用は初回と、自身のローカル姿勢または Offset が変わったときだけで、親の移動では Target の相対位置を維持します。\n" +
                        "「最初の状態を更新」で現在の姿勢を基準にします。ユーザーに見えるのは「最初の状態にリセット」だけです。\n" +
                        "ビルド時、子が無い場合はこの GameObject を削除します。",
                        MessageType.Info);
                }

                DrawResetButton();

                if (showDeveloperSettings)
                {
                    EditorGUILayout.Space(6);
                    Field(_active, "有効");
                    Field(_previewParticles, "パーティクルをプレビュー");

                    DrawTargetTransforms();

                    EditorGUILayout.Space(8);
                    DrawSphereSection();
                    EditorGUILayout.Space(4);
                    DrawBoxSection();
                    EditorGUILayout.Space(4);
                    DrawCapsuleSection();
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(_arrows, new GUIContent("矢印", _arrows.tooltip), true);
                    EditorGUILayout.Space(4);
                    DrawLabelSection();
                    EditorGUILayout.Space(4);
                    DrawMeshSection();

                    var tuning = target as TuningObject;
                    if (tuning != null && !serializedObject.isEditingMultipleObjects)
                    {
                        if (GUILayout.Button("最初の状態を更新"))
                        {
                            serializedObject.ApplyModifiedProperties();
                            tuning.RecordSnapLocalPose();
                            serializedObject.Update();
                        }
                    }

                    if (GUILayout.Button("開発者設定を隠す"))
                        ShowDeveloperSettings = false;
                }

                serializedObject.ApplyModifiedProperties();
            });
        }

        private static void Field(SerializedProperty property, string label)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }

        private void DrawResetButton()
        {
            var tuning = target as TuningObject;
            if (tuning == null || serializedObject.isEditingMultipleObjects)
                return;

            using (new EditorGUI.DisabledScope(!tuning.HasSnapLocalPose))
            {
                if (GUILayout.Button("最初の状態にリセット"))
                {
                    serializedObject.ApplyModifiedProperties();
                    tuning.ResetToSnapLocalPose();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawSphereSection()
        {
            EditorGUILayout.LabelField("中心の球", EditorStyles.boldLabel);
            Field(_showSphere, "球を表示");
            if (!_showSphere.boolValue && !_showSphere.hasMultipleDifferentValues)
                return;

            EditorGUI.indentLevel++;
            Field(_sphereRadius, "半径");
            Field(_sphereColor, "色");
            EditorGUI.indentLevel--;
        }

        private void DrawBoxSection()
        {
            EditorGUILayout.LabelField("中心のボックス", EditorStyles.boldLabel);
            Field(_showBox, "ボックスを表示");
            if (!_showBox.boolValue && !_showBox.hasMultipleDifferentValues)
                return;

            EditorGUI.indentLevel++;
            Field(_boxSize, "サイズ");
            Field(_boxColor, "色");
            EditorGUI.indentLevel--;
        }

        private void DrawCapsuleSection()
        {
            EditorGUILayout.LabelField("中心のカプセル", EditorStyles.boldLabel);
            Field(_showCapsule, "カプセルを表示");
            if (!_showCapsule.boolValue && !_showCapsule.hasMultipleDifferentValues)
                return;

            EditorGUI.indentLevel++;
            Field(_capsuleRadius, "半径");
            Field(_capsuleHeight, "高さ");
            Field(_capsuleDirection, "向き");
            Field(_capsuleColor, "色");
            EditorGUI.indentLevel--;
        }

        private void DrawLabelSection()
        {
            EditorGUILayout.LabelField("テキスト", EditorStyles.boldLabel);
            Field(_showLabel, "テキストを表示");
            if (!_showLabel.boolValue && !_showLabel.hasMultipleDifferentValues)
                return;

            EditorGUI.indentLevel++;
            Field(_labelText, "内容");
            Field(_labelColor, "色");
            Field(_labelOffset, "オフセット");
            EditorGUI.indentLevel--;
        }

        private void DrawMeshSection()
        {
            EditorGUILayout.LabelField("プレビューメッシュ", EditorStyles.boldLabel);
            Field(_showMesh, "メッシュを表示");
            if (!_showMesh.boolValue && !_showMesh.hasMultipleDifferentValues)
                return;

            EditorGUI.indentLevel++;
            Field(_previewMesh, "メッシュ");
            Field(_meshColor, "色");
            Field(_meshOffset, "位置オフセット");
            Field(_meshRotation, "回転");
            Field(_meshScale, "スケール");
            EditorGUI.indentLevel--;
        }

        private void DrawTargetTransforms()
        {
            EditorGUILayout.LabelField("ターゲット", EditorStyles.boldLabel);

            if (_targetTransforms.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(_targetTransforms, new GUIContent("ターゲット"), true);
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            var size = EditorGUILayout.DelayedIntField(TargetSizeContent, _targetTransforms.arraySize);
            if (EditorGUI.EndChangeCheck())
                _targetTransforms.arraySize = Mathf.Max(0, size);
            EditorGUI.indentLevel--;

            var showButtons = !_active.boolValue && !serializedObject.isEditingMultipleObjects;

            for (int i = 0; i < _targetTransforms.arraySize; i++)
            {
                var element = _targetTransforms.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawTargetElement(element, i);

                if (showButtons)
                {
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(!HasTargetTransform(element)))
                    {
                        if (GUILayout.Button("Targetへ移動"))
                        {
                            serializedObject.ApplyModifiedProperties();
                            foreach (var t in targets)
                            {
                                if (t is TuningObject tuning)
                                    tuning.MoveSelfToTarget(i);
                            }
                            GUIUtility.ExitGUI();
                        }

                        if (GUILayout.Button("差分をOffsetに登録"))
                        {
                            serializedObject.ApplyModifiedProperties();
                            foreach (var t in targets)
                            {
                                if (t is TuningObject tuning)
                                    tuning.CaptureOffsetFromTarget(i);
                            }
                            serializedObject.Update();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawTargetElement(SerializedProperty element, int index)
        {
            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, $"ターゲット {index}", true);
            if (!element.isExpanded)
                return;

            EditorGUI.indentLevel++;
            DrawRelative(element, nameof(TuningObject.TargetTransform.transform), TargetTransformContent);
            DrawRelative(element, nameof(TuningObject.TargetTransform.offsetPosition), OffsetPositionContent);
            DrawRelative(element, nameof(TuningObject.TargetTransform.offsetRotation), OffsetRotationContent);
            DrawRelative(element, nameof(TuningObject.TargetTransform.offsetScale), OffsetScaleContent);
            EditorGUI.indentLevel--;
        }

        private static void DrawRelative(SerializedProperty parent, string name, GUIContent label)
        {
            var property = parent.FindPropertyRelative(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, label);
        }

        private static bool HasTargetTransform(SerializedProperty element)
        {
            var transformProp = element.FindPropertyRelative("transform");
            return transformProp != null && transformProp.objectReferenceValue != null;
        }
    }
}
