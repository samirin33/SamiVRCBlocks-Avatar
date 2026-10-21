using System;
using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(GameObjectResetter))]
    [CanEditMultipleObjects]
    public class GameObjectResetterEditor : SamirinMABaseEditor
    {
        private static readonly GUIContent ResetContent = new GUIContent("リセットする", "オンのとき、ビルド時にこの値を再セットします");
        private static readonly GUIContent ResetValueContent = new GUIContent("リセット値");
        private static readonly GUIContent CopyCurrentContent = new GUIContent("現在の値をコピー", "現在の Transform の値をリセット値にコピーします");
        private static readonly GUIContent CopyCurrentMultiContent = new GUIContent("現在の値をコピー", "選択中のそれぞれのオブジェクトに、自身の現在の値をコピーします");

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

                DrawHelpBoxWithDefaultFont(
                    "ビルド時にオブジェクトの有効/無効、位置、回転、スケールを再セットすることができます！",
                    MessageType.Info);

                EditorGUILayout.LabelField("有効状態");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_objectEnable, ResetContent);
                if (IsOnOrMixed(_objectEnable))
                {
                    EditorGUILayout.PropertyField(_resetObjectEnable, new GUIContent("リセット後の状態", "オンなら有効、オフなら無効にします"));
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                DrawResetSection(
                    "位置",
                    _resetPosition,
                    _isLocalPosition,
                    new GUIContent("ローカル座標", "オンなら localPosition、オフならワールド座標で再セットします"),
                    _resetPositionValue,
                    (t, local) => local ? t.localPosition : t.position);

                DrawResetSection(
                    "回転",
                    _resetRotation,
                    _isLocalRotation,
                    new GUIContent("ローカル回転", "オンなら localEulerAngles、オフならワールド回転で再セットします"),
                    _resetRotationValue,
                    (t, local) => local ? t.localEulerAngles : t.eulerAngles);

                DrawResetSection(
                    "スケール",
                    _resetScale,
                    _isLocalScale,
                    new GUIContent("ローカルスケール", "オンなら localScale、オフなら lossyScale を基準に再セットします"),
                    _resetScaleValue,
                    (t, local) => local ? t.localScale : t.lossyScale);

                EditorGUILayout.LabelField("破棄");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_destroyOnReset, new GUIContent("リセット時にオブジェクトを破棄"));
                EditorGUILayout.EndVertical();

                serializedObject.ApplyModifiedProperties();
            });
        }

        private static bool IsOnOrMixed(SerializedProperty property)
        {
            return property.boolValue || property.hasMultipleDifferentValues;
        }

        private void DrawResetSection(
            string title,
            SerializedProperty enableProp,
            SerializedProperty isLocalProp,
            GUIContent isLocalContent,
            SerializedProperty valueProp,
            Func<Transform, bool, Vector3> readCurrent)
        {
            EditorGUILayout.LabelField(title);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enableProp, ResetContent);
            if (IsOnOrMixed(enableProp))
            {
                EditorGUILayout.PropertyField(isLocalProp, isLocalContent);
                EditorGUILayout.PropertyField(valueProp, ResetValueContent);

                var multi = serializedObject.isEditingMultipleObjects;
                if (GUILayout.Button(multi ? CopyCurrentMultiContent : CopyCurrentContent))
                    CopyCurrentValues(isLocalProp.propertyPath, valueProp.propertyPath, readCurrent);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void CopyCurrentValues(string isLocalPath, string valuePath, Func<Transform, bool, Vector3> readCurrent)
        {
            serializedObject.ApplyModifiedProperties();
            foreach (var t in targets)
            {
                if (!(t is GameObjectResetter resetter))
                    continue;

                var so = new SerializedObject(resetter);
                var isLocal = so.FindProperty(isLocalPath).boolValue;
                so.FindProperty(valuePath).vector3Value = readCurrent(resetter.transform, isLocal);
                so.ApplyModifiedProperties();
            }
            serializedObject.Update();
        }
    }
}
