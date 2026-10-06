using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf.runtime;
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
        private const string PrefsKeyBoneToggles = PrefsKeyPrefix + "BoneToggles";
        private const string PrefsKeyFingerToggles = PrefsKeyPrefix + "FingerToggles";
        private const string PrefsKeyPlayerFollow = PrefsKeyPrefix + "PlayerFollow";

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
            "Root", "Body", "Left Arm", "Right Arm", "Left Leg", "Right Leg", "Other",
        };

        private SerializedProperty _links;
        private SerializedProperty _playerFollowObjects;
        private SerializedProperty _addHeadChop;
        private SerializedProperty _headChopGlobalScaleFactor;
        private SerializedProperty _editorFollowHumanoid;
        private SerializedProperty _sourceApplyEnabled;
        private SerializedProperty _boneApply;

        private void OnEnable()
        {
            _links = serializedObject.FindProperty(nameof(ControllableHumanoid.links));
            _playerFollowObjects = serializedObject.FindProperty(nameof(ControllableHumanoid.playerFollowObjects));
            _addHeadChop = serializedObject.FindProperty(nameof(ControllableHumanoid.addHeadChop));
            _headChopGlobalScaleFactor = serializedObject.FindProperty(nameof(ControllableHumanoid.headChopGlobalScaleFactor));
            _editorFollowHumanoid = serializedObject.FindProperty(nameof(ControllableHumanoid.editorFollowHumanoid));
            _sourceApplyEnabled = serializedObject.FindProperty(nameof(ControllableHumanoid.sourceApplyEnabled));
            _boneApply = serializedObject.FindProperty(nameof(ControllableHumanoid.boneApply));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Animatorで制御できます", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(_sourceApplyEnabled, new GUIContent("ポーズ反映トグル"));
                if (_addHeadChop != null && _addHeadChop.boolValue)
                    EditorGUILayout.Slider(_headChopGlobalScaleFactor, 0f, 1f, new GUIContent("Global Scale Factor"));
                var boneToggleOpen = GetFoldout(PrefsKeyBoneToggles, false);
                var newBoneToggleOpen = EditorGUILayout.Foldout(boneToggleOpen, "個別トグル", true);
                if (newBoneToggleOpen != boneToggleOpen)
                    SetFoldout(PrefsKeyBoneToggles, newBoneToggleOpen);
                if (newBoneToggleOpen)
                {
                    DrawBoneApplyFigure();
                    var fingerOpen = GetFoldout(PrefsKeyFingerToggles, false);
                    var newFingerOpen = EditorGUILayout.Foldout(fingerOpen, "指を表示", true);
                    if (newFingerOpen != fingerOpen)
                        SetFoldout(PrefsKeyFingerToggles, newFingerOpen);
                    if (newFingerOpen)
                        DrawFingerZoom();
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);
                DrawLinks();

                EditorGUILayout.Space(6);
                DrawPlayerFollowObjects();

                EditorGUILayout.Space(8);
                EditorGUILayout.PropertyField(_editorFollowHumanoid, new GUIContent("Editor Follow"));
                EditorGUILayout.PropertyField(_addHeadChop, new GUIContent("Add Head Chop"));

                serializedObject.ApplyModifiedProperties();
            });
        }

        private static readonly (HumanBodyBones a, HumanBodyBones b)[] FigureBones =
        {
            (HumanBodyBones.Head, HumanBodyBones.Neck),
            (HumanBodyBones.Neck, HumanBodyBones.UpperChest),
            (HumanBodyBones.UpperChest, HumanBodyBones.Chest),
            (HumanBodyBones.Chest, HumanBodyBones.Spine),
            (HumanBodyBones.Spine, HumanBodyBones.Hips),
            (HumanBodyBones.Head, HumanBodyBones.LeftEye),
            (HumanBodyBones.Head, HumanBodyBones.RightEye),
            (HumanBodyBones.Head, HumanBodyBones.Jaw),
            (HumanBodyBones.UpperChest, HumanBodyBones.LeftShoulder),
            (HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            (HumanBodyBones.UpperChest, HumanBodyBones.RightShoulder),
            (HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            (HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg),
            (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),
            (HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
            (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            (HumanBodyBones.RightFoot, HumanBodyBones.RightToes),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftThumbProximal),
            (HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate),
            (HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftIndexProximal),
            (HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate),
            (HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal),
            (HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate),
            (HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftRingProximal),
            (HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate),
            (HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftLittleProximal),
            (HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate),
            (HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal),
            (HumanBodyBones.RightHand, HumanBodyBones.RightThumbProximal),
            (HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate),
            (HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal),
            (HumanBodyBones.RightHand, HumanBodyBones.RightIndexProximal),
            (HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate),
            (HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal),
            (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal),
            (HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate),
            (HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal),
            (HumanBodyBones.RightHand, HumanBodyBones.RightRingProximal),
            (HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate),
            (HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal),
            (HumanBodyBones.RightHand, HumanBodyBones.RightLittleProximal),
            (HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate),
            (HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal),
        };

        private static readonly Dictionary<HumanBodyBones, Vector2> FigurePoints = new Dictionary<HumanBodyBones, Vector2>
        {
            { HumanBodyBones.Head, new Vector2(0.50f, 0.06f) },
            { HumanBodyBones.LeftEye, new Vector2(0.46f, 0.045f) },
            { HumanBodyBones.RightEye, new Vector2(0.54f, 0.045f) },
            { HumanBodyBones.Jaw, new Vector2(0.50f, 0.09f) },
            { HumanBodyBones.Neck, new Vector2(0.50f, 0.13f) },
            { HumanBodyBones.UpperChest, new Vector2(0.50f, 0.20f) },
            { HumanBodyBones.Chest, new Vector2(0.50f, 0.28f) },
            { HumanBodyBones.Spine, new Vector2(0.50f, 0.36f) },
            { HumanBodyBones.Hips, new Vector2(0.50f, 0.46f) },
            { HumanBodyBones.LeftShoulder, new Vector2(0.36f, 0.18f) },
            { HumanBodyBones.LeftUpperArm, new Vector2(0.28f, 0.26f) },
            { HumanBodyBones.LeftLowerArm, new Vector2(0.22f, 0.34f) },
            { HumanBodyBones.LeftHand, new Vector2(0.16f, 0.42f) },
            { HumanBodyBones.RightShoulder, new Vector2(0.64f, 0.18f) },
            { HumanBodyBones.RightUpperArm, new Vector2(0.72f, 0.26f) },
            { HumanBodyBones.RightLowerArm, new Vector2(0.78f, 0.34f) },
            { HumanBodyBones.RightHand, new Vector2(0.84f, 0.42f) },
            { HumanBodyBones.LeftUpperLeg, new Vector2(0.42f, 0.58f) },
            { HumanBodyBones.LeftLowerLeg, new Vector2(0.40f, 0.70f) },
            { HumanBodyBones.LeftFoot, new Vector2(0.38f, 0.82f) },
            { HumanBodyBones.LeftToes, new Vector2(0.34f, 0.90f) },
            { HumanBodyBones.RightUpperLeg, new Vector2(0.58f, 0.58f) },
            { HumanBodyBones.RightLowerLeg, new Vector2(0.60f, 0.70f) },
            { HumanBodyBones.RightFoot, new Vector2(0.62f, 0.82f) },
            { HumanBodyBones.RightToes, new Vector2(0.66f, 0.90f) },
            { HumanBodyBones.LeftThumbProximal, new Vector2(0.08f, 0.48f) },
            { HumanBodyBones.LeftThumbIntermediate, new Vector2(0.05f, 0.54f) },
            { HumanBodyBones.LeftThumbDistal, new Vector2(0.03f, 0.60f) },
            { HumanBodyBones.LeftIndexProximal, new Vector2(0.12f, 0.50f) },
            { HumanBodyBones.LeftIndexIntermediate, new Vector2(0.10f, 0.56f) },
            { HumanBodyBones.LeftIndexDistal, new Vector2(0.08f, 0.62f) },
            { HumanBodyBones.LeftMiddleProximal, new Vector2(0.16f, 0.50f) },
            { HumanBodyBones.LeftMiddleIntermediate, new Vector2(0.15f, 0.57f) },
            { HumanBodyBones.LeftMiddleDistal, new Vector2(0.14f, 0.64f) },
            { HumanBodyBones.LeftRingProximal, new Vector2(0.20f, 0.50f) },
            { HumanBodyBones.LeftRingIntermediate, new Vector2(0.20f, 0.56f) },
            { HumanBodyBones.LeftRingDistal, new Vector2(0.20f, 0.62f) },
            { HumanBodyBones.LeftLittleProximal, new Vector2(0.24f, 0.49f) },
            { HumanBodyBones.LeftLittleIntermediate, new Vector2(0.25f, 0.54f) },
            { HumanBodyBones.LeftLittleDistal, new Vector2(0.26f, 0.59f) },
            { HumanBodyBones.RightThumbProximal, new Vector2(0.92f, 0.48f) },
            { HumanBodyBones.RightThumbIntermediate, new Vector2(0.95f, 0.54f) },
            { HumanBodyBones.RightThumbDistal, new Vector2(0.97f, 0.60f) },
            { HumanBodyBones.RightIndexProximal, new Vector2(0.88f, 0.50f) },
            { HumanBodyBones.RightIndexIntermediate, new Vector2(0.90f, 0.56f) },
            { HumanBodyBones.RightIndexDistal, new Vector2(0.92f, 0.62f) },
            { HumanBodyBones.RightMiddleProximal, new Vector2(0.84f, 0.50f) },
            { HumanBodyBones.RightMiddleIntermediate, new Vector2(0.85f, 0.57f) },
            { HumanBodyBones.RightMiddleDistal, new Vector2(0.86f, 0.64f) },
            { HumanBodyBones.RightRingProximal, new Vector2(0.80f, 0.50f) },
            { HumanBodyBones.RightRingIntermediate, new Vector2(0.80f, 0.56f) },
            { HumanBodyBones.RightRingDistal, new Vector2(0.80f, 0.62f) },
            { HumanBodyBones.RightLittleProximal, new Vector2(0.76f, 0.49f) },
            { HumanBodyBones.RightLittleIntermediate, new Vector2(0.75f, 0.54f) },
            { HumanBodyBones.RightLittleDistal, new Vector2(0.74f, 0.59f) },
        };

        private void DrawBoneApplyFigure()
        {
            if (_boneApply == null)
                return;

            var area = GUILayoutUtility.GetRect(10f, 460f, GUILayout.ExpandWidth(true));
            var figure = new Rect(area.x + 8f, area.y + 4f, area.width - 16f, area.height - 8f);
            var line = new Color(0.55f, 0.62f, 0.7f, 0.9f);
            foreach (var (a, b) in FigureBones)
            {
                if (IsFingerBone(a) || IsFingerBone(b))
                    continue;
                if (!FigurePoints.TryGetValue(a, out var pa) || !FigurePoints.TryGetValue(b, out var pb))
                    continue;
                DrawFigureLine(FigurePoint(figure, pa), FigurePoint(figure, pb), line);
            }

            foreach (var pair in FigurePoints)
            {
                if (IsFingerBone(pair.Key))
                    continue;
                DrawBoneToggle(figure, pair.Key, pair.Value);
            }
        }

        private void DrawFingerZoom()
        {
            if (_boneApply == null)
                return;

            var area = GUILayoutUtility.GetRect(10f, 260f, GUILayout.ExpandWidth(true));
            var gap = 12f;
            var width = (area.width - gap) * 0.5f;
            DrawFingerHand(new Rect(area.x, area.y, width, area.height), left: true);
            DrawFingerHand(new Rect(area.x + width + gap, area.y, width, area.height), left: false);
        }

        private void DrawFingerHand(Rect area, bool left)
        {
            var title = left ? "L" : "R";
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            var previousColor = GUI.color;
            GUI.color = left ? new Color(0.45f, 0.72f, 0.95f) : new Color(0.95f, 0.62f, 0.4f);
            GUI.Label(new Rect(area.x, area.y, area.width, 18f), title, titleStyle);
            GUI.color = previousColor;

            var hand = new Rect(area.x, area.y + 18f, area.width, area.height - 18f);
            var line = new Color(0.55f, 0.62f, 0.7f, 0.9f);
            var knuckle = HandPoint(hand, left, new Vector2(0.42f, 0.70f));
            var thumbBase = HandPoint(hand, left, new Vector2(0.58f, 0.84f));
            var fingers = left ? LeftFingers : RightFingers;
            for (var i = 0; i < fingers.Length; i++)
            {
                var previous = i == 0 ? thumbBase : knuckle;
                var chain = fingers[i];
                for (var joint = 0; joint < chain.Length; joint++)
                {
                    var normalized = MirrorHand(HandFingerShape[i * 3 + joint], left);
                    var point = new Vector2(
                        hand.x + hand.width * normalized.x,
                        hand.y + hand.height * normalized.y);
                    DrawFigureLine(previous, point, line);
                    previous = point;
                    DrawBoneToggle(hand, chain[joint], normalized, showName: false);
                }
            }
        }

        private static Vector2 HandPoint(Rect hand, bool left, Vector2 normalized)
        {
            normalized = MirrorHand(normalized, left);
            return new Vector2(hand.x + hand.width * normalized.x, hand.y + hand.height * normalized.y);
        }

        private static Vector2 MirrorHand(Vector2 point, bool left)
        {
            return left ? point : new Vector2(1f - point.x, point.y);
        }

        private static bool IsFingerBone(HumanBodyBones bone)
        {
            var name = bone.ToString();
            return name.Contains("Thumb")
                || name.Contains("Index")
                || name.Contains("Middle")
                || name.Contains("Ring")
                || name.Contains("Little");
        }

        private static readonly HumanBodyBones[][] LeftFingers =
        {
            new[] { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal },
            new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
            new[] { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
        };

        private static readonly HumanBodyBones[][] RightFingers =
        {
            new[] { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal },
            new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
        };

        private static readonly Vector2[] HandFingerShape =
        {
            new Vector2(0.64f, 0.72f), new Vector2(0.80f, 0.60f), new Vector2(0.92f, 0.46f),
            new Vector2(0.58f, 0.50f), new Vector2(0.60f, 0.32f), new Vector2(0.62f, 0.14f),
            new Vector2(0.44f, 0.46f), new Vector2(0.44f, 0.26f), new Vector2(0.44f, 0.06f),
            new Vector2(0.30f, 0.50f), new Vector2(0.28f, 0.32f), new Vector2(0.26f, 0.16f),
            new Vector2(0.16f, 0.56f), new Vector2(0.12f, 0.40f), new Vector2(0.09f, 0.26f),
        };

        private void DrawBoneToggle(Rect figure, HumanBodyBones bone, Vector2 normalized, bool showName = true)
        {
            var property = _boneApply.FindPropertyRelative(bone.ToString());
            if (property == null)
                return;

            var center = FigurePoint(figure, normalized);
            var finger = IsFingerBone(bone);
            var size = !showName
                ? 16f
                : finger
                    || bone == HumanBodyBones.LeftEye
                    || bone == HumanBodyBones.RightEye
                    || bone == HumanBodyBones.Jaw
                    ? 12f
                    : 16f;
            var rect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            var on = property.boolValue;
            var name = bone.ToString();
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = on ? BoneOnColor(bone) : new Color(0.28f, 0.28f, 0.28f);
            if (GUI.Button(rect, new GUIContent(string.Empty, name)))
                property.boolValue = !on;
            GUI.backgroundColor = previous;

            if (!showName)
                return;

            var labelSize = EditorStyles.miniLabel.CalcSize(new GUIContent(name));
            var labelOnLeft = normalized.x < 0.5f;
            var labelRect = labelOnLeft
                ? new Rect(rect.x - labelSize.x - 2f, rect.y + (rect.height - labelSize.y) * 0.5f, labelSize.x, labelSize.y)
                : new Rect(rect.xMax + 2f, rect.y + (rect.height - labelSize.y) * 0.5f, labelSize.x, labelSize.y);
            GUI.Label(labelRect, name, EditorStyles.miniLabel);
        }

        private static Color BoneOnColor(HumanBodyBones bone)
        {
            var name = bone.ToString();
            if (name.StartsWith("Left", StringComparison.Ordinal))
                return new Color(0.45f, 0.72f, 0.95f);
            if (name.StartsWith("Right", StringComparison.Ordinal))
                return new Color(0.95f, 0.62f, 0.4f);
            return new Color(0.45f, 0.85f, 0.5f);
        }

        private static Vector2 FigurePoint(Rect figure, Vector2 normalized)
        {
            return new Vector2(
                figure.x + figure.width * normalized.x,
                figure.y + figure.height * normalized.y);
        }

        private static void DrawFigureLine(Vector2 from, Vector2 to, Color color)
        {
            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            var delta = to - from;
            var length = delta.magnitude;
            if (length < 0.5f)
                return;

            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - 1f, length, 2f), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void DrawLinks()
        {
            var count = _links != null ? _links.arraySize : 0;
            var rootOpen = GetFoldout(PrefsKeyRoot, true);
            var newRootOpen = EditorGUILayout.Foldout(rootOpen, $"制御用オブジェクト ({count})", true);
            if (newRootOpen != rootOpen)
                SetFoldout(PrefsKeyRoot, newRootOpen);

            if (!newRootOpen)
                return;

            EditorGUI.indentLevel++;

            if (count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("すべて展開", EditorStyles.miniButtonLeft))
                    SetAllGroupFoldouts(true);
                if (GUILayout.Button("すべて折りたたむ", EditorStyles.miniButtonRight))
                    SetAllGroupFoldouts(false);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);

                var rootIndices = new List<int>();
                var indicesByParent = new Dictionary<HumanBodyBones, List<int>>();
                CollectLinkIndices(rootIndices, indicesByParent);
                var drawn = new HashSet<int>();

                if (rootIndices.Count > 0)
                {
                    if (DrawGroupFoldout("Root", rootIndices, drawn))
                    {
                        EditorGUI.indentLevel--;
                        return;
                    }
                    EditorGUILayout.Space(2);
                }

                foreach (var (label, bones) in BoneGroups)
                {
                    var indices = new List<int>();
                    foreach (var bone in bones)
                    {
                        if (indicesByParent.TryGetValue(bone, out var found))
                            indices.AddRange(found);
                    }

                    if (indices.Count == 0)
                        continue;

                    if (DrawGroupFoldout(label, indices, drawn))
                    {
                        EditorGUI.indentLevel--;
                        return;
                    }
                    EditorGUILayout.Space(2);
                }

                var otherIndices = new List<int>();
                for (var i = 0; i < count; i++)
                {
                    if (!drawn.Contains(i))
                        otherIndices.Add(i);
                }

                if (otherIndices.Count > 0)
                    if (DrawGroupFoldout("Other", otherIndices, drawn))
                    {
                        EditorGUI.indentLevel--;
                        return;
                    }
            }

            if (GUILayout.Button("追加"))
            {
                var index = _links.arraySize;
                _links.InsertArrayElementAtIndex(index);
                var entry = _links.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentIsRoot)).boolValue = true;
                entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentBone)).enumValueIndex =
                    (int)HumanBodyBones.Hips;
                entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentChild)).objectReferenceValue = null;
                entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.shareChildParent)).boolValue = false;
                entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParent)).objectReferenceValue = null;
                entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.keepLocal)).boolValue = true;
                entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParents)).ClearArray();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawPlayerFollowObjects()
        {
            var count = _playerFollowObjects != null ? _playerFollowObjects.arraySize : 0;
            var open = GetFoldout(PrefsKeyPlayerFollow, true);
            var newOpen = EditorGUILayout.Foldout(open, $"プレイヤー追従オブジェクト ({count})", true);
            if (newOpen != open)
                SetFoldout(PrefsKeyPlayerFollow, newOpen);
            if (!newOpen || _playerFollowObjects == null)
                return;

            EditorGUI.indentLevel++;
            for (var i = 0; i < _playerFollowObjects.arraySize; i++)
            {
                var entry = _playerFollowObjects.GetArrayElementAtIndex(i);
                var bone = entry.FindPropertyRelative(nameof(ControllableHumanoid.PlayerFollowEntry.bone));
                var target = entry.FindPropertyRelative(nameof(ControllableHumanoid.PlayerFollowEntry.target));

                EditorGUILayout.BeginHorizontal();
                DrawHumanoidBonePopup(bone);
                EditorGUILayout.PropertyField(target, GUIContent.none);
                if (GUILayout.Button("削除", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    _playerFollowObjects.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("追加"))
            {
                var index = _playerFollowObjects.arraySize;
                _playerFollowObjects.InsertArrayElementAtIndex(index);
                var entry = _playerFollowObjects.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative(nameof(ControllableHumanoid.PlayerFollowEntry.bone)).enumValueIndex =
                    (int)HumanBodyBones.Head;
                entry.FindPropertyRelative(nameof(ControllableHumanoid.PlayerFollowEntry.target)).objectReferenceValue = null;
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawHumanoidBonePopup(SerializedProperty bone)
        {
            var names = bone.enumDisplayNames;
            var options = new List<string>();
            var values = new List<int>();
            for (var i = 0; i < names.Length; i++)
            {
                if (!ControllableHumanoid.BoneLinkEntry.IsBone((HumanBodyBones)i))
                    continue;
                options.Add(names[i]);
                values.Add(i);
            }

            var current = bone.enumValueIndex;
            var selected = 0;
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == current)
                {
                    selected = i;
                    break;
                }
            }

            var next = EditorGUILayout.Popup(selected, options.ToArray(), GUILayout.Width(140));
            bone.enumValueIndex = values[next];
        }

        private bool DrawGroupFoldout(string label, List<int> indices, HashSet<int> drawn)
        {
            var key = PrefsKeyPrefix + label;
            var open = GetFoldout(key, DefaultOpenGroups.Contains(label));
            var newOpen = EditorGUILayout.Foldout(open, $"{label} ({indices.Count})", true);
            if (newOpen != open)
                SetFoldout(key, newOpen);

            foreach (var index in indices)
                drawn.Add(index);

            if (!newOpen)
                return false;

            foreach (var index in indices)
            {
                if (DrawLinkEntry(index))
                    return true;
            }

            return false;
        }

        private void CollectLinkIndices(
            List<int> rootIndices,
            Dictionary<HumanBodyBones, List<int>> byParent)
        {
            for (var i = 0; i < _links.arraySize; i++)
            {
                var entry = _links.GetArrayElementAtIndex(i);
                var parentIsRoot = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentIsRoot));
                if (parentIsRoot != null && parentIsRoot.boolValue)
                {
                    rootIndices.Add(i);
                    continue;
                }

                var parentProp = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentBone));
                if (parentProp == null)
                    continue;

                var bone = (HumanBodyBones)parentProp.enumValueIndex;
                if (!ControllableHumanoid.BoneLinkEntry.IsBone(bone))
                    continue;

                if (!byParent.TryGetValue(bone, out var list))
                {
                    list = new List<int>();
                    byParent[bone] = list;
                }

                list.Add(i);
            }
        }

        private bool DrawLinkEntry(int index)
        {
            var entry = _links.GetArrayElementAtIndex(index);
            var parentIsRoot = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentIsRoot));
            var parentBone = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentBone));
            var parentChild = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentChild));
            var shareChildParent = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.shareChildParent));
            var childParent = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParent));
            var keepLocal = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.keepLocal));

            var parentName = parentIsRoot.boolValue ? "Root" : BoneLabel(parentBone);
            var keepLocalContent = new GUIContent(
                "ローカルを保持",
                "オンのとき、付け替え前のワールド位置・回転・スケールを維持します。Armature のスケールが 1 でないモデルでも子が原点に潰れません。オフのとき、childParent との間にローカル座標を打ち消すオブジェクトを挟み、合成結果を位置 0、回転 0、スケール 1 にします。");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{parentName} の子", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("削除", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                _links.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            DrawBonePopup("親", parentIsRoot, parentBone, includeRoot: true);
            EditorGUILayout.PropertyField(parentChild, new GUIContent($"{parentName} Child"));
            EditorGUILayout.PropertyField(shareChildParent, new GUIContent("まとめて指定"));

            if (shareChildParent.boolValue)
            {
                EditorGUILayout.PropertyField(childParent, new GUIContent("子 Parent"));
                EditorGUILayout.PropertyField(keepLocal, keepLocalContent);
            }
            else
            {
                DrawPerChildParents(entry, parentIsRoot, parentBone, keepLocalContent);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(1);
            return false;
        }

        private void DrawPerChildParents(
            SerializedProperty entry,
            SerializedProperty parentIsRoot,
            SerializedProperty parentBone,
            GUIContent keepLocalContent)
        {
            var component = target as ControllableHumanoid;
            var avatarRoot = component != null ? RuntimeUtil.FindAvatarInParents(component.transform) : null;
            Animator animator = null;
            VRCAvatarDescriptor descriptor = null;
            if (avatarRoot != null)
            {
                avatarRoot.TryGetComponent(out animator);
                avatarRoot.TryGetComponent(out descriptor);
            }
            var children = ControllableHumanoidProcessor.CollectDirectHumanoidChildren(
                animator,
                descriptor,
                parentIsRoot.boolValue,
                (HumanBodyBones)parentBone.enumValueIndex);

            if (children == null)
            {
                EditorGUILayout.HelpBox("Humanoid Animator が見つからないため、子ボーンを列挙できません。", MessageType.Info);
            }
            else
            {
                var sharedKeepLocal = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.keepLocal));
                SyncChildParentSlots(entry, children, sharedKeepLocal == null || sharedKeepLocal.boolValue);
                if (children.Count == 0)
                    EditorGUILayout.HelpBox("この親のヒューマノイド子はありません。", MessageType.Info);
            }

            var slots = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParents));
            if (slots == null)
                return;

            for (var i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                var bone = slot.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.bone));
                var slotParent = slot.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.childParent));
                var slotKeepLocal = slot.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.keepLocal));
                var boneName = BoneLabel(bone);

                EditorGUILayout.LabelField(boneName, EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(slotParent, new GUIContent("子 Parent"));
                EditorGUILayout.PropertyField(slotKeepLocal, keepLocalContent);
                EditorGUI.indentLevel--;
            }
        }

        private static void SyncChildParentSlots(SerializedProperty entry, List<HumanBodyBones> bones, bool defaultKeepLocal)
        {
            var slots = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParents));
            if (slots == null || bones == null)
                return;

            var same = slots.arraySize == bones.Count;
            if (same)
            {
                for (var i = 0; i < bones.Count; i++)
                {
                    var boneProp = slots.GetArrayElementAtIndex(i)
                        .FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.bone));
                    if (boneProp.enumValueIndex != (int)bones[i])
                    {
                        same = false;
                        break;
                    }
                }
            }

            if (same)
                return;

            var savedParents = new Dictionary<int, UnityEngine.Object>();
            var savedKeep = new Dictionary<int, bool>();
            for (var i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                var boneIndex = slot.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.bone)).enumValueIndex;
                savedParents[boneIndex] = slot.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.childParent)).objectReferenceValue;
                savedKeep[boneIndex] = slot.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.keepLocal)).boolValue;
            }

            slots.arraySize = bones.Count;
            for (var i = 0; i < bones.Count; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                var boneIndex = (int)bones[i];
                slot.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.bone)).enumValueIndex = boneIndex;
                slot.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.childParent)).objectReferenceValue =
                    savedParents.TryGetValue(boneIndex, out var parent) ? parent : null;
                slot.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.keepLocal)).boolValue =
                    savedKeep.TryGetValue(boneIndex, out var keep) ? keep : defaultKeepLocal;
            }
        }

        private static bool DrawBonePopup(
            string label,
            SerializedProperty parentIsRoot,
            SerializedProperty parentBone,
            bool includeRoot)
        {
            var options = new List<string>();
            var values = new List<int>();
            if (includeRoot)
            {
                options.Add("Root");
                values.Add(-1);
            }

            var names = parentBone.enumDisplayNames;
            for (var i = 0; i < names.Length; i++)
            {
                if (!ControllableHumanoid.BoneLinkEntry.IsBone((HumanBodyBones)i))
                    continue;
                options.Add(names[i]);
                values.Add(i);
            }

            var current = parentIsRoot.boolValue ? -1 : parentBone.enumValueIndex;
            var selected = 0;
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == current)
                {
                    selected = i;
                    break;
                }
            }

            var next = EditorGUILayout.Popup(label, selected, options.ToArray());
            var changed = values[next] != current;
            if (values[next] < 0)
                parentIsRoot.boolValue = true;
            else
            {
                parentIsRoot.boolValue = false;
                parentBone.enumValueIndex = values[next];
            }

            return changed;
        }

        private static string BoneLabel(SerializedProperty boneProperty)
        {
            if (boneProperty == null)
                return "Bone";

            var index = boneProperty.enumValueIndex;
            if (index < 0 || index >= boneProperty.enumDisplayNames.Length)
                return "Bone";

            return boneProperty.enumDisplayNames[index];
        }

        private static void SetAllGroupFoldouts(bool open)
        {
            SetFoldout(PrefsKeyPrefix + "Root", open);
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
