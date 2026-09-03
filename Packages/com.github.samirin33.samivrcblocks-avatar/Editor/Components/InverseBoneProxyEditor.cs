#if UNITY_EDITOR
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(InverseBoneProxy))]
    public class InverseBoneProxyEditor : SamirinMABaseEditor
    {
        static readonly string[] AttachmentModeLabels =
        {
            "As Child At Root（位置・回転とも合わせる）",
            "As Child Keep World Pose（ワールド姿勢を維持）",
            "As Child Keep Rotation（回転を維持・位置のみ合わせる）",
            "As Child Keep Position（位置を維持・回転のみ合わせる）",
        };

        static readonly BoneProxyAttachmentMode[] AttachmentModeValues =
        {
            BoneProxyAttachmentMode.AsChildAtRoot,
            BoneProxyAttachmentMode.AsChildKeepWorldPose,
            BoneProxyAttachmentMode.AsChildKeepRotation,
            BoneProxyAttachmentMode.AsChildKeepPosition,
        };

        SerializedProperty _targetObject;
        SerializedProperty _referencePath;
        SerializedProperty _targetObjectGo;
        SerializedProperty _attachmentMode;
        SerializedProperty _matchScale;
        SerializedProperty _editorApplyTransform;

        void OnEnable()
        {
            _targetObject = serializedObject.FindProperty("targetObject");
            _referencePath = _targetObject?.FindPropertyRelative("referencePath");
            _targetObjectGo = _targetObject?.FindPropertyRelative("targetObject");
            _attachmentMode = serializedObject.FindProperty("attachmentMode");
            _matchScale = serializedObject.FindProperty("matchScale");
            _editorApplyTransform = serializedObject.FindProperty("editorApplyTransform");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "対象を自身の子に置きます。",
                    MessageType.Info);

                EditorGUILayout.PropertyField(_targetObject, new GUIContent("Target Object"));

                EditorGUI.BeginChangeCheck();
                var path = _referencePath?.stringValue ?? "";
                var newPath = EditorGUILayout.TextField("Target Object Path", path);
                if (EditorGUI.EndChangeCheck() && _referencePath != null)
                    ApplyPathString(newPath);

                DrawAttachmentModePopup();
                DrawMatchScaleField();
                EditorGUILayout.PropertyField(_editorApplyTransform, new GUIContent("Editor Apply Transform"));

                serializedObject.ApplyModifiedProperties();
            });
        }

        void DrawMatchScaleField()
        {
            if (_matchScale == null)
                return;

            if (InverseBoneProxyUtil.SupportsBoneProxyMatchScale)
            {
                EditorGUILayout.PropertyField(_matchScale, new GUIContent("Match Scale"));
                return;
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_matchScale, new GUIContent("Match Scale"));
            EditorGUI.EndDisabledGroup();
            DrawHelpBoxWithDefaultFont(
                "現在の Modular Avatar の Bone Proxy には Match Scale がありません。この項目は無視されます。",
                MessageType.Info);
        }

        void DrawAttachmentModePopup()
        {
            if (_attachmentMode == null)
                return;

            var current = (BoneProxyAttachmentMode)_attachmentMode.enumValueIndex;
            // Unset は AsChildAtRoot として扱う
            if (current == BoneProxyAttachmentMode.Unset)
                current = BoneProxyAttachmentMode.AsChildAtRoot;

            var index = 0;
            for (var i = 0; i < AttachmentModeValues.Length; i++)
            {
                if (AttachmentModeValues[i] == current)
                {
                    index = i;
                    break;
                }
            }

            EditorGUI.BeginChangeCheck();
            index = EditorGUILayout.Popup("Attachment Mode", index, AttachmentModeLabels);
            if (EditorGUI.EndChangeCheck())
                _attachmentMode.enumValueIndex = (int)AttachmentModeValues[index];
        }

        void ApplyPathString(string newPath)
        {
            newPath = (newPath ?? "").Replace("\\", "/").Trim().TrimStart('/');

            if (string.IsNullOrEmpty(newPath))
            {
                _referencePath.stringValue = "";
                if (_targetObjectGo != null)
                    _targetObjectGo.objectReferenceValue = null;
                return;
            }

            var proxy = (InverseBoneProxy)target;
            var avatarRoot = nadena.dev.ndmf.runtime.RuntimeUtil.FindAvatarInParents(proxy.transform);
            if (avatarRoot != null)
            {
                if (newPath == AvatarObjectReference.AVATAR_ROOT || newPath == avatarRoot.name)
                {
                    _referencePath.stringValue = AvatarObjectReference.AVATAR_ROOT;
                    if (_targetObjectGo != null)
                        _targetObjectGo.objectReferenceValue = avatarRoot.gameObject;
                    return;
                }

                var found = avatarRoot.Find(newPath);
                _referencePath.stringValue = newPath;
                if (_targetObjectGo != null)
                    _targetObjectGo.objectReferenceValue = found != null ? found.gameObject : null;
                return;
            }

            _referencePath.stringValue = newPath;
            if (_targetObjectGo != null)
                _targetObjectGo.objectReferenceValue = null;
        }
    }
}
#endif
