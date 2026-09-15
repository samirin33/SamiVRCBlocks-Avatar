using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(ReplaceGrabHand))]
    [CanEditMultipleObjects]
    public class ReplaceGrabHandEditor : SamirinMABaseEditor
    {
        private SerializedProperty _targetTransform;
        private SerializedProperty _avatarContactType;
        private SerializedProperty _offsetTransform;

        private void OnEnable()
        {
            _targetTransform = serializedObject.FindProperty(nameof(ReplaceGrabHand.targetTransform));
            _avatarContactType = serializedObject.FindProperty(nameof(ReplaceGrabHand.avatarContactType));
            _offsetTransform = serializedObject.FindProperty(nameof(ReplaceGrabHand.offsetTransform));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "ビルド時（Resolving / MA 前）に、VRCAvatarDescriptor の指定コライダーを\n" +
                    "Target Transform（未指定なら自身）へ付け替えます。\n" +
                    "Offset Transform を指定すると、元のローカル位置・回転をそこに退避します。",
                    MessageType.Info);

                EditorGUILayout.PropertyField(
                    _avatarContactType,
                    new GUIContent("Contact Type", "付け替えるアバターコンタクト"));
                EditorGUILayout.PropertyField(
                    _targetTransform,
                    new GUIContent("Target Transform", "未指定なら自身の Transform"));
                EditorGUILayout.PropertyField(
                    _offsetTransform,
                    new GUIContent("Offset Transform", "元のローカル位置・回転の退避先（任意）"));

                serializedObject.ApplyModifiedProperties();
            });
        }
    }
}
