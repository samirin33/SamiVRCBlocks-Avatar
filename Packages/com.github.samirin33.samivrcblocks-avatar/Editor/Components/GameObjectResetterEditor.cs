using UnityEditor;
using UnityEngine;
using Samirin33.Editor;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(GameObjectResetter))]
    public class GameObjectResetterEditor : SamirinMABaseEditor
    {
        private SerializedProperty _objectEnable;
        private SerializedProperty _resetObjectEnable;
        private SerializedProperty _resetPosition;
        private SerializedProperty _resetPositionValue;
        private SerializedProperty _isLocalPosition;
        private SerializedProperty _resetRotation;
        private SerializedProperty _resetRotationValue;
        private SerializedProperty _isLocalRotation;
        private SerializedProperty _resetScale;
        private SerializedProperty _resetScaleValue;
        private SerializedProperty _isLocalScale;
        private SerializedProperty _destroyOnReset;

        private void OnEnable()
        {
            _objectEnable = serializedObject.FindProperty("objectEnable");
            _resetObjectEnable = serializedObject.FindProperty("resetObjectEnable");
            _resetPosition = serializedObject.FindProperty("resetPosition");
            _resetPositionValue = serializedObject.FindProperty("resetPositionValue");
            _isLocalPosition = serializedObject.FindProperty("isLocalPosition");
            _resetRotation = serializedObject.FindProperty("resetRotation");
            _resetRotationValue = serializedObject.FindProperty("resetRotationValue");
            _isLocalRotation = serializedObject.FindProperty("isLocalRotation");
            _resetScale = serializedObject.FindProperty("resetScale");
            _resetScaleValue = serializedObject.FindProperty("resetScaleValue");
            _isLocalScale = serializedObject.FindProperty("isLocalScale");
            _destroyOnReset = serializedObject.FindProperty("destroyOnReset");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "ビルド時にオブジェクトの有効/無効、位置、回転、スケールを再セットすることができます！",
                    MessageType.Info);

                var resetter = (GameObjectResetter)target;

                // オブジェクトの有効/無効
                EditorGUILayout.LabelField("Active");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_objectEnable, new GUIContent("リセットする"));
                if (_objectEnable.boolValue)
                {
                    EditorGUILayout.PropertyField(_resetObjectEnable, new GUIContent("リセット後の状態"));
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                // Position
                EditorGUILayout.LabelField("Position");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_resetPosition, new GUIContent("リセットする"));
                if (_resetPosition.boolValue)
                {
                    EditorGUILayout.PropertyField(_isLocalPosition, new GUIContent("ローカル座標"));
                    EditorGUILayout.PropertyField(_resetPositionValue, new GUIContent("リセット値"));
                    if (GUILayout.Button("現在の値をコピー"))
                    {
                        _resetPositionValue.vector3Value = resetter.isLocalPosition
                            ? resetter.transform.localPosition
                            : resetter.transform.position;
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                // Rotation
                EditorGUILayout.LabelField("Rotation");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_resetRotation, new GUIContent("リセットする"));
                if (_resetRotation.boolValue)
                {
                    EditorGUILayout.PropertyField(_isLocalRotation, new GUIContent("ローカル回転"));
                    EditorGUILayout.PropertyField(_resetRotationValue, new GUIContent("リセット値"));
                    if (GUILayout.Button("現在の値をコピー"))
                    {
                        _resetRotationValue.vector3Value = resetter.isLocalRotation
                            ? resetter.transform.localEulerAngles
                            : resetter.transform.eulerAngles;
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                // Scale
                EditorGUILayout.LabelField("Scale");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_resetScale, new GUIContent("リセットする"));
                if (_resetScale.boolValue)
                {
                    EditorGUILayout.PropertyField(_isLocalScale, new GUIContent("ローカルスケール"));
                    EditorGUILayout.PropertyField(_resetScaleValue, new GUIContent("リセット値"));
                    if (GUILayout.Button("現在の値をコピー"))
                    {
                        _resetScaleValue.vector3Value = resetter.isLocalScale
                            ? resetter.transform.localScale
                            : resetter.transform.lossyScale;
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                // リセット時に破棄
                EditorGUILayout.LabelField("Destroy");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_destroyOnReset, new GUIContent("リセット時にオブジェクトを破棄"));
                EditorGUILayout.EndVertical();

                serializedObject.ApplyModifiedProperties();
            });
        }
    }
}