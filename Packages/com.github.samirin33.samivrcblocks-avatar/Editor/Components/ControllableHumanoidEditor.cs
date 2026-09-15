using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(ControllableHumanoid))]
    [CanEditMultipleObjects]
    public class ControllableHumanoidEditor : SamirinMABaseEditor
    {
        private const string PrefsKeyPrefix = "Samirin33.ControllableHumanoid.Foldout.";
        private const string PrefsKeyRoot = PrefsKeyPrefix + "Root";

        private static readonly (string label, HumanBodyBones[] bones)[] BoneGroups =
        {
            ("Body", new[]
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.UpperChest,
                HumanBodyBones.Neck,
                HumanBodyBones.Head,
            }),
            ("Eyes", new[]
            {
                HumanBodyBones.LeftEye,
                HumanBodyBones.RightEye,
                HumanBodyBones.Jaw,
            }),
            ("Left Arm", new[]
            {
                HumanBodyBones.LeftShoulder,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
            }),
            ("Right Arm", new[]
            {
                HumanBodyBones.RightShoulder,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
            }),
            ("Left Fingers", new[]
            {
                HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftThumbIntermediate,
                HumanBodyBones.LeftThumbDistal,
                HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftIndexIntermediate,
                HumanBodyBones.LeftIndexDistal,
                HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftMiddleIntermediate,
                HumanBodyBones.LeftMiddleDistal,
                HumanBodyBones.LeftRingProximal,
                HumanBodyBones.LeftRingIntermediate,
                HumanBodyBones.LeftRingDistal,
                HumanBodyBones.LeftLittleProximal,
                HumanBodyBones.LeftLittleIntermediate,
                HumanBodyBones.LeftLittleDistal,
            }),
            ("Right Fingers", new[]
            {
                HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightThumbIntermediate,
                HumanBodyBones.RightThumbDistal,
                HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightIndexIntermediate,
                HumanBodyBones.RightIndexDistal,
                HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightMiddleIntermediate,
                HumanBodyBones.RightMiddleDistal,
                HumanBodyBones.RightRingProximal,
                HumanBodyBones.RightRingIntermediate,
                HumanBodyBones.RightRingDistal,
                HumanBodyBones.RightLittleProximal,
                HumanBodyBones.RightLittleIntermediate,
                HumanBodyBones.RightLittleDistal,
            }),
            ("Left Leg", new[]
            {
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes,
            }),
            ("Right Leg", new[]
            {
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes,
            }),
        };

        private static readonly HashSet<string> DefaultOpenGroups = new HashSet<string>
        {
            "Body", "Left Arm", "Right Arm", "Left Leg", "Right Leg", "Other",
        };

        private SerializedProperty _boneControls;
        private SerializedProperty _addHeadChop;
        private SerializedProperty _editorFollowHumanoid;

        private void OnEnable()
        {
            _boneControls = serializedObject.FindProperty(nameof(ControllableHumanoid.boneControls));
            _addHeadChop = serializedObject.FindProperty(nameof(ControllableHumanoid.addHeadChop));
            _editorFollowHumanoid = serializedObject.FindProperty(nameof(ControllableHumanoid.editorFollowHumanoid));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "「ヒューマノイド構造を追加」で Proxy_* 階層を生成します。\n" +
                    "Source: 複製 → 制御用 Constraint の付与先\n" +
                    "Target: 制御 → オリジナル Constraint の付与先（未設定時は Source と同じ）",
                    MessageType.Info);

                EditorGUI.BeginDisabledGroup(targets.Length != 1);
                if (GUILayout.Button("ヒューマノイド構造を追加", GUILayout.Height(28)))
                {
                    foreach (var t in targets)
                    {
                        if (t is ControllableHumanoid ch)
                            ControllableHumanoidProcessor.CreateHumanoidProxyStructure(ch);
                    }

                    serializedObject.Update();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(6);
                EditorGUILayout.PropertyField(
                    _editorFollowHumanoid,
                    new GUIContent("Editor Follow", "Source/Target をヒューマノイドへ追従"));
                EditorGUILayout.PropertyField(
                    _addHeadChop,
                    new GUIContent("Add Head Chop", "Head に VRCHeadChop を付与する"));

                EditorGUILayout.Space(6);
                DrawBoneControls();

                serializedObject.ApplyModifiedProperties();
            });
        }

        private void DrawBoneControls()
        {
            var count = _boneControls != null ? _boneControls.arraySize : 0;
            var rootOpen = GetFoldout(PrefsKeyRoot, true);
            var newRootOpen = EditorGUILayout.Foldout(
                rootOpen,
                $"制御用オブジェクト ({count})",
                true);
            if (newRootOpen != rootOpen)
                SetFoldout(PrefsKeyRoot, newRootOpen);

            if (!newRootOpen)
                return;

            EditorGUI.indentLevel++;

            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "未設定です。「ヒューマノイド構造を追加」を押すと自動で埋まります。",
                    MessageType.None);
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("すべて展開", EditorStyles.miniButtonLeft))
                SetAllGroupFoldouts(true);
            if (GUILayout.Button("すべて折りたたむ", EditorStyles.miniButtonRight))
                SetAllGroupFoldouts(false);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            var indexByBone = BuildIndexByBone();
            var drawn = new HashSet<int>();

            foreach (var (label, bones) in BoneGroups)
            {
                var indices = new List<int>();
                foreach (var bone in bones)
                {
                    if (indexByBone.TryGetValue(bone, out var index))
                        indices.Add(index);
                }

                if (indices.Count == 0)
                    continue;

                DrawGroupFoldout(label, indices, drawn);
                EditorGUILayout.Space(2);
            }

            var otherIndices = new List<int>();
            for (var i = 0; i < count; i++)
            {
                if (!drawn.Contains(i))
                    otherIndices.Add(i);
            }

            if (otherIndices.Count > 0)
                DrawGroupFoldout("Other", otherIndices, drawn);

            EditorGUI.indentLevel--;
        }

        private void DrawGroupFoldout(string label, List<int> indices, HashSet<int> drawn)
        {
            var key = PrefsKeyPrefix + label;
            var open = GetFoldout(key, DefaultOpenGroups.Contains(label));
            var newOpen = EditorGUILayout.Foldout(open, $"{label} ({indices.Count})", true);
            if (newOpen != open)
                SetFoldout(key, newOpen);

            foreach (var index in indices)
                drawn.Add(index);

            if (!newOpen)
                return;

            foreach (var index in indices)
                DrawBoneEntry(index);
        }

        private Dictionary<HumanBodyBones, int> BuildIndexByBone()
        {
            var map = new Dictionary<HumanBodyBones, int>();
            for (var i = 0; i < _boneControls.arraySize; i++)
            {
                var entry = _boneControls.GetArrayElementAtIndex(i);
                var boneProp = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneControlEntry.bone));
                if (boneProp == null)
                    continue;

                var bone = (HumanBodyBones)boneProp.enumValueIndex;
                if (!map.ContainsKey(bone))
                    map[bone] = i;
            }

            return map;
        }

        private void DrawBoneEntry(int index)
        {
            var entry = _boneControls.GetArrayElementAtIndex(index);
            var boneProp = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneControlEntry.bone));
            var sourceProp = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneControlEntry.source));
            var targetProp = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneControlEntry.target));

            var boneName = boneProp != null
                ? boneProp.enumDisplayNames[boneProp.enumValueIndex]
                : $"Element {index}";

            var source = sourceProp?.objectReferenceValue as Transform;
            var target = targetProp?.objectReferenceValue as Transform;
            var linked = source != null && source == target;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(boneName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (linked)
            {
                var prev = GUI.color;
                GUI.color = new Color(0.55f, 0.85f, 0.55f);
                GUILayout.Label("Source = Target", EditorStyles.miniLabel);
                GUI.color = prev;
            }
            else if (source != null && target == null)
            {
                GUILayout.Label("Target → Source", EditorStyles.miniLabel);
            }
            else if (source != null && target != null && source != target)
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.4f);
                GUILayout.Label("分離中", EditorStyles.miniLabel);
                GUI.color = prev;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Source");
            EditorGUILayout.PropertyField(sourceProp, GUIContent.none);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Target");
            EditorGUILayout.PropertyField(targetProp, GUIContent.none);

            EditorGUI.BeginDisabledGroup(source == null);
            if (GUILayout.Button(
                    new GUIContent("=S", "Target を Source と同じにする"),
                    EditorStyles.miniButton,
                    GUILayout.Width(28)))
            {
                targetProp.objectReferenceValue = sourceProp.objectReferenceValue;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(1);
        }

        private static void SetAllGroupFoldouts(bool open)
        {
            foreach (var (label, _) in BoneGroups)
                SetFoldout(PrefsKeyPrefix + label, open);
            SetFoldout(PrefsKeyPrefix + "Other", open);
        }

        private static bool GetFoldout(string key, bool defaultValue)
        {
            return EditorPrefs.GetBool(key, defaultValue);
        }

        private static void SetFoldout(string key, bool value)
        {
            EditorPrefs.SetBool(key, value);
        }
    }
}
