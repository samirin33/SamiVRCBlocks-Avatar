using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(RemapPosition))]
    [CanEditMultipleObjects]
    public class RemapPositionEditor : SamirinMABaseEditor
    {
        private static readonly GUIContent AxisLabel = new GUIContent("軸", "チェックした軸だけリマップします");
        private static readonly GUIContent AxisXContent = new GUIContent("X", "X 軸をリマップします");
        private static readonly GUIContent AxisYContent = new GUIContent("Y", "Y 軸をリマップします");
        private static readonly GUIContent AxisZContent = new GUIContent("Z", "Z 軸をリマップします");

        private SerializedProperty _target;
        private SerializedProperty _source;
        private SerializedProperty _origin;
        private SerializedProperty _ratio;
        private SerializedProperty _constraintActive;
        private SerializedProperty _weight;
        private SerializedProperty _restPosition;
        private SerializedProperty _positionX;
        private SerializedProperty _positionY;
        private SerializedProperty _positionZ;
        private SerializedProperty _editorApply;

        private void OnEnable()
        {
            _target = serializedObject.FindProperty("target");
            _source = serializedObject.FindProperty("source");
            _origin = serializedObject.FindProperty("origin");
            _ratio = serializedObject.FindProperty("ratio");
            _constraintActive = serializedObject.FindProperty("constraintActive");
            _weight = serializedObject.FindProperty("weight");
            _restPosition = serializedObject.FindProperty("restPosition");
            _positionX = serializedObject.FindProperty("positionX");
            _positionY = serializedObject.FindProperty("positionY");
            _positionZ = serializedObject.FindProperty("positionZ");
            _editorApply = serializedObject.FindProperty("editorApply");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "ソースの位置（基準からの相対）に倍率をかけた位置へ、ターゲットを VRC Position Constraint で動かします。" +
                    "ビルド時にターゲットの親の下へリマップ用のオブジェクトを生成します。" +
                    "倍率・Constraint 有効・ウェイト・軸はアニメーションで操作でき、ビルド時に生成先へ書き換えます。",
                    MessageType.Info);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_target, new GUIContent("ターゲット", _target.tooltip));
                var targetChanged = EditorGUI.EndChangeCheck();

                EditorGUILayout.PropertyField(_source, new GUIContent("ソース", _source.tooltip));
                if (!_source.hasMultipleDifferentValues && _source.objectReferenceValue == null)
                    DrawHelpBoxWithDefaultFont("ソースを指定してください。未指定のままビルドするとスキップされます。", MessageType.Warning);

                EditorGUILayout.PropertyField(_origin, new GUIContent("基準", _origin.tooltip));
                if (!_origin.hasMultipleDifferentValues && _origin.objectReferenceValue == null)
                    DrawHelpBoxWithDefaultFont("基準が未指定のため、ワールド原点 (0, 0, 0) を基準にします。", MessageType.Info);
                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("リマップ");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_ratio, new GUIContent("倍率", _ratio.tooltip));
                if (!_ratio.hasMultipleDifferentValues && Mathf.Abs(_ratio.floatValue) < RemapPosition.MinAbsRatio)
                {
                    DrawHelpBoxWithDefaultFont(
                        $"倍率の絶対値が {RemapPosition.MinAbsRatio} 未満のときは {RemapPosition.MinAbsRatio} として扱います。",
                        MessageType.Info);
                }

                EditorGUILayout.PropertyField(_constraintActive, new GUIContent("Constraint 有効", _constraintActive.tooltip));
                EditorGUILayout.PropertyField(_weight, new GUIContent("ウェイト", _weight.tooltip));
                EditorGUILayout.PropertyField(_restPosition, new GUIContent("ウェイト 0 の位置", _restPosition.tooltip));

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(AxisLabel);
                var labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 14f;
                EditorGUILayout.PropertyField(_positionX, AxisXContent, GUILayout.MinWidth(40f));
                EditorGUILayout.PropertyField(_positionY, AxisYContent, GUILayout.MinWidth(40f));
                EditorGUILayout.PropertyField(_positionZ, AxisZContent, GUILayout.MinWidth(40f));
                EditorGUIUtility.labelWidth = labelWidth;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(_editorApply, new GUIContent("エディタで反映", _editorApply.tooltip));

                serializedObject.ApplyModifiedProperties();

                if (targetChanged)
                    RecordRestPositions();
            });
        }

        /// <summary>
        /// 差し替えたターゲットの、エディタ反映で書き換わる前のローカル位置を「ウェイト 0 の位置」にする。
        /// </summary>
        private void RecordRestPositions()
        {
            foreach (var obj in targets)
            {
                if (!(obj is RemapPosition component))
                    continue;

                Undo.RecordObject(component, "Record RemapPosition Rest Position");
                component.restPosition = component.ResolvedTarget.localPosition;
                EditorUtility.SetDirty(component);
            }
        }
    }
}
