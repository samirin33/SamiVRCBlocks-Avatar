using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(PreventDuplicateObjects))]
    [CanEditMultipleObjects]
    public class PreventDuplicateObjectsEditor : SamirinMABaseEditor
    {
        private SerializedProperty _id;

        private void OnEnable()
        {
            _id = serializedObject.FindProperty(nameof(PreventDuplicateObjects.id));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "同じidを持つコンポーネントがアバター内に複数ある場合に重複を回避します。",
                    MessageType.Info);

                EditorGUILayout.PropertyField(_id, new GUIContent("ID", "同じ ID を持つオブジェクトはアバター内で 1 つだけ残します"));

                serializedObject.ApplyModifiedProperties();
            });
        }
    }
}
