using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(LinkedTransform))]
    public class LinkedTransformEditor : SamirinMABaseEditor
    {
        private SerializedProperty _target;
        private SerializedProperty _sources;

        private SerializedProperty _linkPosition;
        private SerializedProperty _positionX;
        private SerializedProperty _positionY;
        private SerializedProperty _positionZ;
        private SerializedProperty _positionSpace;
        private SerializedProperty _positionMultiplier;
        private SerializedProperty _positionOffset;

        private SerializedProperty _linkRotation;
        private SerializedProperty _rotationX;
        private SerializedProperty _rotationY;
        private SerializedProperty _rotationZ;
        private SerializedProperty _rotationSpace;
        private SerializedProperty _rotationMultiplier;
        private SerializedProperty _rotationOffset;

        private SerializedProperty _linkScale;
        private SerializedProperty _scaleX;
        private SerializedProperty _scaleY;
        private SerializedProperty _scaleZ;
        private SerializedProperty _scaleSpace;
        private SerializedProperty _scaleMultiplier;
        private SerializedProperty _scaleOffset;

        private int _previousSourcesSize = -1;

        private void OnEnable()
        {
            _target = serializedObject.FindProperty("target");
            _sources = serializedObject.FindProperty("sources");
            _previousSourcesSize = _sources != null ? _sources.arraySize : -1;

            _linkPosition = serializedObject.FindProperty("linkPosition");
            _positionX = serializedObject.FindProperty("positionX");
            _positionY = serializedObject.FindProperty("positionY");
            _positionZ = serializedObject.FindProperty("positionZ");
            _positionSpace = serializedObject.FindProperty("positionSpace");
            _positionMultiplier = serializedObject.FindProperty("positionMultiplier");
            _positionOffset = serializedObject.FindProperty("positionOffset");

            _linkRotation = serializedObject.FindProperty("linkRotation");
            _rotationX = serializedObject.FindProperty("rotationX");
            _rotationY = serializedObject.FindProperty("rotationY");
            _rotationZ = serializedObject.FindProperty("rotationZ");
            _rotationSpace = serializedObject.FindProperty("rotationSpace");
            _rotationMultiplier = serializedObject.FindProperty("rotationMultiplier");
            _rotationOffset = serializedObject.FindProperty("rotationOffset");

            _linkScale = serializedObject.FindProperty("linkScale");
            _scaleX = serializedObject.FindProperty("scaleX");
            _scaleY = serializedObject.FindProperty("scaleY");
            _scaleZ = serializedObject.FindProperty("scaleZ");
            _scaleSpace = serializedObject.FindProperty("scaleSpace");
            _scaleMultiplier = serializedObject.FindProperty("scaleMultiplier");
            _scaleOffset = serializedObject.FindProperty("scaleOffset");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "複数ソースを重み付きでブレンドし、オフセット・倍率・座標空間を指定してターゲットへコピーできます。",
                    MessageType.Info);

                EditorGUILayout.PropertyField(_target, new GUIContent("Target", "未指定の場合は自身に適用します"));
                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Sources");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_sources, new GUIContent("Sources"), true);
                if (EditorGUI.EndChangeCheck())
                    EnsureNewSourceWeightsDefaultToOne();
                else if (_sources != null)
                    _previousSourcesSize = _sources.arraySize;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                DrawAxisSection(
                    "Position",
                    _linkPosition,
                    "コピーする",
                    _positionX,
                    _positionY,
                    _positionZ,
                    _positionSpace,
                    _positionMultiplier,
                    _positionOffset);

                DrawAxisSection(
                    "Rotation",
                    _linkRotation,
                    "コピーする",
                    _rotationX,
                    _rotationY,
                    _rotationZ,
                    _rotationSpace,
                    _rotationMultiplier,
                    _rotationOffset,
                    offsetLabel: "オフセット (Euler)");

                DrawAxisSection(
                    "Scale",
                    _linkScale,
                    "コピーする",
                    _scaleX,
                    _scaleY,
                    _scaleZ,
                    _scaleSpace,
                    _scaleMultiplier,
                    _scaleOffset);

                serializedObject.ApplyModifiedProperties();
            });
        }

        private void EnsureNewSourceWeightsDefaultToOne()
        {
            if (_sources == null)
                return;

            var size = _sources.arraySize;
            if (_previousSourcesSize >= 0 && size > _previousSourcesSize)
            {
                for (var i = _previousSourcesSize; i < size; i++)
                {
                    var weightProp = _sources.GetArrayElementAtIndex(i).FindPropertyRelative("weight");
                    if (weightProp != null)
                        weightProp.floatValue = 1f;
                }
            }

            _previousSourcesSize = size;
        }

        private void DrawAxisSection(
            string title,
            SerializedProperty enableProp,
            string enableLabel,
            SerializedProperty axisX,
            SerializedProperty axisY,
            SerializedProperty axisZ,
            SerializedProperty spaceProp,
            SerializedProperty multiplierProp,
            SerializedProperty offsetProp,
            string offsetLabel = "オフセット")
        {
            EditorGUILayout.LabelField(title);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enableProp, new GUIContent(enableLabel));
            if (enableProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(spaceProp, new GUIContent("座標空間"));

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("軸");
                var labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 14f;
                EditorGUILayout.PropertyField(axisX, new GUIContent("X"), GUILayout.MinWidth(40f));
                EditorGUILayout.PropertyField(axisY, new GUIContent("Y"), GUILayout.MinWidth(40f));
                EditorGUILayout.PropertyField(axisZ, new GUIContent("Z"), GUILayout.MinWidth(40f));
                EditorGUIUtility.labelWidth = labelWidth;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(multiplierProp, new GUIContent("倍率"));
                EditorGUILayout.PropertyField(offsetProp, new GUIContent(offsetLabel));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}
