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
                    "同じidを持つコンポーネントがアバター内に複数ある場合に重複を回避します。",
                    MessageType.Info);

                EditorGUILayout.PropertyField(_id, new GUIContent("ID"));

                serializedObject.ApplyModifiedProperties();
            });
        }
    }
}
