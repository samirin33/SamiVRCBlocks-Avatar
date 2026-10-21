using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(WorldFix))]
    [CanEditMultipleObjects]
    public class WorldFixEditor : SamirinMABaseEditor
    {
        private static readonly GUIContent PositionFixContent = new GUIContent("固定する", "オンのとき、位置をワールドに固定します");
        private static readonly GUIContent RotationFixContent = new GUIContent("固定する", "オンのとき、回転をワールドに固定します");
        private static readonly GUIContent ScaleFixContent = new GUIContent("固定する", "オンのとき、スケールをワールドに固定します");
        private static readonly GUIContent PositionAxisX = new GUIContent("X", "X 軸の位置を固定します。オフの軸は親に追従します");
        private static readonly GUIContent PositionAxisY = new GUIContent("Y", "Y 軸の位置を固定します。オフの軸は親に追従します");
        private static readonly GUIContent PositionAxisZ = new GUIContent("Z", "Z 軸の位置を固定します。オフの軸は親に追従します");
        private static readonly GUIContent RotationAxisX = new GUIContent("X", "X 軸の回転を固定します。オフの軸は親に追従します");
        private static readonly GUIContent RotationAxisY = new GUIContent("Y", "Y 軸の回転を固定します。オフの軸は親に追従します");
        private static readonly GUIContent RotationAxisZ = new GUIContent("Z", "Z 軸の回転を固定します。オフの軸は親に追従します");
        private static readonly GUIContent ScaleAxisX = new GUIContent("X", "X 軸のスケールを固定します。オフの軸は親に追従します");
        private static readonly GUIContent ScaleAxisY = new GUIContent("Y", "Y 軸のスケールを固定します。オフの軸は親に追従します");
        private static readonly GUIContent ScaleAxisZ = new GUIContent("Z", "Z 軸のスケールを固定します。オフの軸は親に追従します");
        private static readonly GUIContent EditorApplyContent = new GUIContent("プレイモード外でも適用", "有効にすると、プレイモードでない際にも設定がシーンビューに反映されます");

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

                DrawHelpBoxWithDefaultFont(
                    "オブジェクトをワールド固定させることができます！",
                    MessageType.Info);

                DrawAxisSection("位置", _fixPosition, PositionFixContent,
                    _positionX, PositionAxisX, _positionY, PositionAxisY, _positionZ, PositionAxisZ);
                DrawAxisSection("回転", _fixRotation, RotationFixContent,
                    _rotationX, RotationAxisX, _rotationY, RotationAxisY, _rotationZ, RotationAxisZ);
                DrawAxisSection("スケール", _fixScale, ScaleFixContent,
                    _scaleX, ScaleAxisX, _scaleY, ScaleAxisY, _scaleZ, ScaleAxisZ);

                EditorGUILayout.LabelField("エディタ");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_editorApply, EditorApplyContent);
                DrawHelpBoxWithDefaultFont("有効にすると、プレイモードでない際にも設定がシーンビューに反映されます。", MessageType.None);
                EditorGUILayout.EndVertical();

                serializedObject.ApplyModifiedProperties();
            });
        }

        private static void DrawAxisSection(
            string title,
            SerializedProperty enableProp,
            GUIContent enableContent,
            SerializedProperty axisX,
            GUIContent axisXContent,
            SerializedProperty axisY,
            GUIContent axisYContent,
            SerializedProperty axisZ,
            GUIContent axisZContent)
        {
            EditorGUILayout.LabelField(title);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enableProp, enableContent);
            if (enableProp.boolValue || enableProp.hasMultipleDifferentValues)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(axisX, axisXContent);
                EditorGUILayout.PropertyField(axisY, axisYContent);
                EditorGUILayout.PropertyField(axisZ, axisZContent);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}
