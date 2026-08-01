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
        private SerializedProperty _active;
        private SerializedProperty _targetTransforms;
        private SerializedProperty _showSphere;
        private SerializedProperty _sphereRadius;
        private SerializedProperty _sphereColor;
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
            _targetTransforms = serializedObject.FindProperty(nameof(TuningObject.targetTransforms));
            _showSphere = serializedObject.FindProperty(nameof(TuningObject.showSphere));
            _sphereRadius = serializedObject.FindProperty(nameof(TuningObject.sphereRadius));
            _sphereColor = serializedObject.FindProperty(nameof(TuningObject.sphereColor));
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

                var canResetToSnap = !serializedObject.isEditingMultipleObjects
                    && target is TuningObject tuningForReset
                    && tuningForReset.HasSnapLocalPose;
                if (canResetToSnap)
                {
                    if (GUILayout.Button("最初の状態にリセット"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        ((TuningObject)target).ResetToSnapLocalPose();
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUILayout.PropertyField(_active, new GUIContent("Active"));

                DrawTargetTransforms();

                EditorGUILayout.Space(8);
                DrawSphereSection();
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(_arrows, new GUIContent("Arrows"), true);
                EditorGUILayout.Space(4);
                DrawLabelSection();
                EditorGUILayout.Space(4);
                DrawMeshSection();

                serializedObject.ApplyModifiedProperties();
            });
        }

        private void DrawSphereSection()
        {
            EditorGUILayout.LabelField("Center Sphere", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showSphere, new GUIContent("Show Sphere"));
            if (!_showSphere.boolValue && !_showSphere.hasMultipleDifferentValues)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_sphereRadius, new GUIContent("Sphere Radius"));
            EditorGUILayout.PropertyField(_sphereColor, new GUIContent("Sphere Color"));
            EditorGUI.indentLevel--;
        }

        private void DrawLabelSection()
        {
            EditorGUILayout.LabelField("Label", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showLabel, new GUIContent("Show Label"));
            if (!_showLabel.boolValue && !_showLabel.hasMultipleDifferentValues)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_labelText, new GUIContent("Label Text"));
            EditorGUILayout.PropertyField(_labelColor, new GUIContent("Label Color"));
            EditorGUILayout.PropertyField(_labelOffset, new GUIContent("Label Offset"));
            EditorGUI.indentLevel--;
        }

        private void DrawMeshSection()
        {
            EditorGUILayout.LabelField("Preview Mesh", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showMesh, new GUIContent("Show Mesh"));
            if (!_showMesh.boolValue && !_showMesh.hasMultipleDifferentValues)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_previewMesh, new GUIContent("Preview Mesh"));
            EditorGUILayout.PropertyField(_meshColor, new GUIContent("Mesh Color"));
            EditorGUILayout.PropertyField(_meshOffset, new GUIContent("Mesh Offset"));
            EditorGUILayout.PropertyField(_meshRotation, new GUIContent("Mesh Rotation"));
            EditorGUILayout.PropertyField(_meshScale, new GUIContent("Mesh Scale"));
            EditorGUI.indentLevel--;
        }

        private void DrawTargetTransforms()
        {
            EditorGUILayout.LabelField("Target Transforms", EditorStyles.boldLabel);

            if (_targetTransforms.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(_targetTransforms, true);
                return;
            }

            EditorGUI.indentLevel++;
            _targetTransforms.arraySize = EditorGUILayout.IntField("Size", _targetTransforms.arraySize);
            EditorGUI.indentLevel--;

            var showButtons = !_active.boolValue && !serializedObject.isEditingMultipleObjects;

            for (int i = 0; i < _targetTransforms.arraySize; i++)
            {
                var element = _targetTransforms.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(element, new GUIContent($"Element {i}"), true);

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

        private static bool HasTargetTransform(SerializedProperty element)
        {
            var transformProp = element.FindPropertyRelative("transform");
            return transformProp != null && transformProp.objectReferenceValue != null;
        }
    }
}
