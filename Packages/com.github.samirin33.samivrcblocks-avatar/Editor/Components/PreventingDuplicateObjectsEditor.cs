using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(PreventingDuplicateObjects))]
    public class PreventingDuplicateObjectsEditor : SamirinMABaseEditor
    {
        private SerializedProperty _id;

        private void OnEnable()
        {
            _id = serializedObject.FindProperty(nameof(PreventingDuplicateObjects.id));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "同じ id を持つコンポーネントがアバター内に複数ある場合、ビルド時に警告ダイアログを出し、最初の1つ以外の GameObject を削除します。",
                    MessageType.Info);

                EditorGUILayout.PropertyField(_id, new GUIContent("ID"));

                serializedObject.ApplyModifiedProperties();
            });
        }
    }
}
