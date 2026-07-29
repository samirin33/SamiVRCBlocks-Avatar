using UnityEditor;
using UnityEngine;
using Samirin33.Editor;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(WorldFix))]
    public class WorldFixEditor : SamirinMABaseEditor
    {
        private SerializedProperty _fixPosition;
        private SerializedProperty _positionX;
        private SerializedProperty _positionY;
        private SerializedProperty _positionZ;
        private SerializedProperty _fixRotation;
        private SerializedProperty _rotationX;
        private SerializedProperty _rotationY;
        private SerializedProperty _rotationZ;
        private SerializedProperty _fixScale;
        private SerializedProperty _scaleX;
        private SerializedProperty _scaleY;
        private SerializedProperty _scaleZ;
        private SerializedProperty _editorApply;

        private void OnEnable()
        {
            _fixPosition = serializedObject.FindProperty("fixPosition");
            _positionX = serializedObject.FindProperty("positionX");
            _positionY = serializedObject.FindProperty("positionY");
            _positionZ = serializedObject.FindProperty("positionZ");
            _fixRotation = serializedObject.FindProperty("fixRotation");
            _rotationX = serializedObject.FindProperty("rotationX");
            _rotationY = serializedObject.FindProperty("rotationY");
            _rotationZ = serializedObject.FindProperty("rotationZ");
            _fixScale = serializedObject.FindProperty("fixScale");
            _scaleX = serializedObject.FindProperty("scaleX");
            _scaleY = serializedObject.FindProperty("scaleY");
            _scaleZ = serializedObject.FindProperty("scaleZ");
            _editorApply = serializedObject.FindProperty("editorApply");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "オブジェクトをワールド固定させることができます！",
                    MessageType.Info);

                // Position
                EditorGUILayout.LabelField("Position");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_fixPosition, new GUIContent("固定する"));
                if (_fixPosition.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_positionX, new GUIContent("X"));
                    EditorGUILayout.PropertyField(_positionY, new GUIContent("Y"));
                    EditorGUILayout.PropertyField(_positionZ, new GUIContent("Z"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                // Rotation
                EditorGUILayout.LabelField("Rotation");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_fixRotation, new GUIContent("固定する"));
                if (_fixRotation.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_rotationX, new GUIContent("X"));
                    EditorGUILayout.PropertyField(_rotationY, new GUIContent("Y"));
                    EditorGUILayout.PropertyField(_rotationZ, new GUIContent("Z"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                // Scale
                EditorGUILayout.LabelField("Scale");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_fixScale, new GUIContent("固定する"));
                if (_fixScale.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_scaleX, new GUIContent("X"));
                    EditorGUILayout.PropertyField(_scaleY, new GUIContent("Y"));
                    EditorGUILayout.PropertyField(_scaleZ, new GUIContent("Z"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                // エディタ適用
                EditorGUILayout.LabelField("エディタ");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_editorApply, new GUIContent("プレイモード外でも適用"));
                DrawHelpBoxWithDefaultFont("有効にすると、プレイモードでない際にも設定がシーンビューに反映されます。", MessageType.None);
                EditorGUILayout.EndVertical();

                serializedObject.ApplyModifiedProperties();
            });
        }
    }
}