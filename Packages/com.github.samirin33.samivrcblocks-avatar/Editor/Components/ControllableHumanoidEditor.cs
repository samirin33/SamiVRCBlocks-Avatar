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
        private const string PrefsKeyGroupPrefix = PrefsKeyPrefix + "Group.";
        private const string RootGroupKey = "Root";
        private const string OtherGroupKey = "Other";

        private static readonly (string key, string label, HumanBodyBones[] bones)[] BoneGroups =
        {
            ("Body", "体", new[]
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.UpperChest,
                HumanBodyBones.Neck,
                HumanBodyBones.Head,
            }),
            ("Eyes", "目・顎", new[]
            {
                HumanBodyBones.LeftEye,
                HumanBodyBones.RightEye,
                HumanBodyBones.Jaw,
            }),
            ("Left Arm", "左腕", new[]
            {
                HumanBodyBones.LeftShoulder,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
            }),
            ("Right Arm", "右腕", new[]
            {
                HumanBodyBones.RightShoulder,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
            }),
            ("Left Fingers", "左手の指", new[]
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
            ("Right Fingers", "右手の指", new[]
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
            ("Left Leg", "左脚", new[]
            {
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes,
            }),
            ("Right Leg", "右脚", new[]
            {
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes,
            }),
        };

        private static readonly HashSet<string> DefaultOpenGroups = new HashSet<string>
        {
            RootGroupKey, "Body", "Left Arm", "Right Arm", "Left Leg", "Right Leg", OtherGroupKey,
        };

        private static string[] _humanoidBoneOptions;
        private static int[] _humanoidBoneValues;

        private SerializedProperty _links;
        private SerializedProperty _playerFollowObjects;
        private SerializedProperty _addHeadChop;
        private SerializedProperty _headChopGlobalScaleFactor;
        private SerializedProperty _editorFollowHumanoid;
        private SerializedProperty _sourceApplyEnabled;
        private SerializedProperty _boneApply;

        private static readonly GUIContent HeadChopGlobalScaleContent = new GUIContent(
            "頭部縮小の倍率",
            "VRC Head Chop の Global Scale Factor。一人称視点で頭を縮小するときの倍率です。");
        private static readonly GUIContent EditorFollowContent = new GUIContent(
            "エディタ上で追従",
            "オンのとき、再生していないエディタ上でも制御用オブジェクトをヒューマノイドのボーンに追従させます。");
        private static readonly GUIContent AddHeadChopContent = new GUIContent(
            "Head Chop を追加",
            "ビルド時に頭へ VRC Head Chop を追加します。");

        private Animator _avatarAnimator;
        private VRCAvatarDescriptor _avatarDescriptor;
        private bool _avatarResolved;
        private readonly Dictionary<int, List<HumanBodyBones>> _childrenCache = new Dictionary<int, List<HumanBodyBones>>();

        private void OnEnable()
        {
            _links = serializedObject.FindProperty(nameof(ControllableHumanoid.links));
            _playerFollowObjects = serializedObject.FindProperty(nameof(ControllableHumanoid.playerFollowObjects));
            _addHeadChop = serializedObject.FindProperty(nameof(ControllableHumanoid.addHeadChop));
            _headChopGlobalScaleFactor = serializedObject.FindProperty(nameof(ControllableHumanoid.headChopGlobalScaleFactor));
            _editorFollowHumanoid = serializedObject.FindProperty(nameof(ControllableHumanoid.editorFollowHumanoid));
            _sourceApplyEnabled = serializedObject.FindProperty(nameof(ControllableHumanoid.sourceApplyEnabled));
            _boneApply = serializedObject.FindProperty(nameof(ControllableHumanoid.boneApply));

            InvalidateAvatarCache();
            EditorApplication.hierarchyChanged += InvalidateAvatarCache;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= InvalidateAvatarCache;
        }

        private void InvalidateAvatarCache()
        {
            _avatarResolved = false;
            _avatarAnimator = null;
            _avatarDescriptor = null;
            _childrenCache.Clear();
        }

        private void ResolveAvatar()
        {
            if (_avatarResolved)
                return;

            _avatarResolved = true;
            var component = target as ControllableHumanoid;
            var avatarRoot = component != null ? RuntimeUtil.FindAvatarInParents(component.transform) : null;
            if (avatarRoot == null)
                return;

            avatarRoot.TryGetComponent(out _avatarAnimator);
            avatarRoot.TryGetComponent(out _avatarDescriptor);
        }

        /// <returns>Humanoid Animator が無いときは null。</returns>
        private List<HumanBodyBones> GetDirectHumanoidChildren(bool parentIsRoot, HumanBodyBones parentBone)
        {
            ResolveAvatar();
            if (_avatarAnimator == null || !_avatarAnimator.isHuman)
                return null;

            var key = parentIsRoot ? -1 : (int)parentBone;
            if (!_childrenCache.TryGetValue(key, out var children))
            {
                children = ControllableHumanoidProcessor.CollectDirectHumanoidChildren(
                    _avatarAnimator,
                    _avatarDescriptor,
                    parentIsRoot,
                    parentBone);
                _childrenCache[key] = children;
            }

            return children;
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Animatorで制御できます", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(_sourceApplyEnabled, new GUIContent("ポーズ反映トグル"));
                if (_addHeadChop != null && (_addHeadChop.boolValue || _addHeadChop.hasMultipleDifferentValues))
                    EditorGUILayout.Slider(_headChopGlobalScaleFactor, 0f, 1f, HeadChopGlobalScaleContent);
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
                EditorGUILayout.PropertyField(_editorFollowHumanoid, EditorFollowContent);
                EditorGUILayout.PropertyField(_addHeadChop, AddHeadChopContent);

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
            var previousColor = GUI.color;
            GUI.color = left ? new Color(0.45f, 0.72f, 0.95f) : new Color(0.95f, 0.62f, 0.4f);
            GUI.Label(new Rect(area.x, area.y, area.width, 18f), title, FingerTitleStyle);
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

        private static GUIStyle _fingerTitleStyle;
        private static GUIStyle FingerTitleStyle =>
            _fingerTitleStyle ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };

        private static HashSet<HumanBodyBones> _fingerBones;

        private static bool IsFingerBone(HumanBodyBones bone)
        {
            if (_fingerBones == null)
            {
                _fingerBones = new HashSet<HumanBodyBones>();
                foreach (var chain in LeftFingers)
                    _fingerBones.UnionWith(chain);
                foreach (var chain in RightFingers)
                    _fingerBones.UnionWith(chain);
            }

            return _fingerBones.Contains(bone);
        }

        private static readonly Dictionary<HumanBodyBones, (string name, GUIContent tooltip, GUIContent label)> BoneContents =
            new Dictionary<HumanBodyBones, (string, GUIContent, GUIContent)>();

        private static (string name, GUIContent tooltip, GUIContent label) GetBoneContent(HumanBodyBones bone)
        {
            if (!BoneContents.TryGetValue(bone, out var content))
            {
                var name = bone.ToString();
                content = (name, new GUIContent(string.Empty, name), new GUIContent(name));
                BoneContents[bone] = content;
            }

            return content;
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
            var content = GetBoneContent(bone);
            var property = _boneApply.FindPropertyRelative(content.name);
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
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = property.hasMultipleDifferentValues
                ? new Color(0.6f, 0.6f, 0.6f)
                : on ? BoneOnColor(bone) : new Color(0.28f, 0.28f, 0.28f);
            if (GUI.Button(rect, content.tooltip))
                property.boolValue = property.hasMultipleDifferentValues || !on;
            GUI.backgroundColor = previous;

            if (!showName)
                return;

            var labelSize = EditorStyles.miniLabel.CalcSize(content.label);
            var labelOnLeft = normalized.x < 0.5f;
            var labelRect = labelOnLeft
                ? new Rect(rect.x - labelSize.x - 2f, rect.y + (rect.height - labelSize.y) * 0.5f, labelSize.x, labelSize.y)
                : new Rect(rect.xMax + 2f, rect.y + (rect.height - labelSize.y) * 0.5f, labelSize.x, labelSize.y);
            GUI.Label(labelRect, content.label, EditorStyles.miniLabel);
        }

        private static Color BoneOnColor(HumanBodyBones bone)
        {
            var name = GetBoneContent(bone).name;
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

            var canEditArray = CanEditArrayStructure(serializedObject);
            var removeIndex = -1;

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
                    DrawGroupFoldout(RootGroupKey, "ルート", rootIndices, drawn, canEditArray, ref removeIndex);
                    EditorGUILayout.Space(2);
                }

                foreach (var (key, label, bones) in BoneGroups)
                {
                    var indices = new List<int>();
                    foreach (var bone in bones)
                    {
                        if (indicesByParent.TryGetValue(bone, out var found))
                            indices.AddRange(found);
                    }

                    if (indices.Count == 0)
                        continue;

                    DrawGroupFoldout(key, label, indices, drawn, canEditArray, ref removeIndex);
                    EditorGUILayout.Space(2);
                }

                var otherIndices = new List<int>();
                for (var i = 0; i < count; i++)
                {
                    if (!drawn.Contains(i))
                        otherIndices.Add(i);
                }

                if (otherIndices.Count > 0)
                    DrawGroupFoldout(OtherGroupKey, "その他", otherIndices, drawn, canEditArray, ref removeIndex);
            }

            if (removeIndex >= 0)
                ApplyArrayRowAction(_links, removeIndex, ArrayRowAction.Remove);

            using (new EditorGUI.DisabledScope(!canEditArray))
            {
                if (GUILayout.Button("追加"))
                    AddLink();
            }

            EditorGUI.indentLevel--;
        }

        private void AddLink()
        {
            var index = _links.arraySize;
            _links.InsertArrayElementAtIndex(index);
            var entry = _links.GetArrayElementAtIndex(index);
            var parentIsRoot = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentIsRoot));
            var parentBone = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentBone));
            parentIsRoot.boolValue = true;
            parentBone.enumValueIndex = (int)HumanBodyBones.Hips;
            entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentChild)).objectReferenceValue = null;
            entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.shareChildParent)).boolValue = false;
            entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParent)).objectReferenceValue = null;
            entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.keepLocal)).boolValue = true;
            entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParents)).ClearArray();
            SyncChildParentSlots(entry, parentIsRoot, parentBone);
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
            var canEditArray = CanEditArrayStructure(serializedObject);
            var removeIndex = -1;
            for (var i = 0; i < _playerFollowObjects.arraySize; i++)
            {
                var entry = _playerFollowObjects.GetArrayElementAtIndex(i);
                var bone = entry.FindPropertyRelative(nameof(ControllableHumanoid.PlayerFollowEntry.bone));
                var target = entry.FindPropertyRelative(nameof(ControllableHumanoid.PlayerFollowEntry.target));

                EditorGUILayout.BeginHorizontal();
                DrawHumanoidBonePopup(bone);
                EditorGUILayout.PropertyField(target, GUIContent.none);
                using (new EditorGUI.DisabledScope(!canEditArray))
                {
                    if (GUILayout.Button("削除", EditorStyles.miniButton, GUILayout.Width(40)))
                        removeIndex = i;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
                ApplyArrayRowAction(_playerFollowObjects, removeIndex, ArrayRowAction.Remove);

            using (new EditorGUI.DisabledScope(!canEditArray))
            {
                if (GUILayout.Button("追加"))
                {
                    var index = _playerFollowObjects.arraySize;
                    _playerFollowObjects.InsertArrayElementAtIndex(index);
                    var entry = _playerFollowObjects.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative(nameof(ControllableHumanoid.PlayerFollowEntry.bone)).enumValueIndex =
                        (int)HumanBodyBones.Head;
                    entry.FindPropertyRelative(nameof(ControllableHumanoid.PlayerFollowEntry.target)).objectReferenceValue = null;
                }
            }
            EditorGUI.indentLevel--;
        }

        private static void EnsureHumanoidBoneOptions(SerializedProperty enumProperty)
        {
            if (_humanoidBoneOptions != null)
                return;

            var names = enumProperty.enumDisplayNames;
            var options = new List<string>();
            var values = new List<int>();
            for (var i = 0; i < names.Length; i++)
            {
                if (!ControllableHumanoid.BoneLinkEntry.IsBone((HumanBodyBones)i))
                    continue;
                options.Add(names[i]);
                values.Add(i);
            }

            _humanoidBoneOptions = options.ToArray();
            _humanoidBoneValues = values.ToArray();
        }

        private static void DrawHumanoidBonePopup(SerializedProperty bone)
        {
            EnsureHumanoidBoneOptions(bone);

            var current = bone.enumValueIndex;
            var selected = Array.IndexOf(_humanoidBoneValues, current);
            var options = _humanoidBoneOptions;
            if (selected < 0)
            {
                // 一覧外の値は選択肢に追加して表示し、触らない限り書き換えない
                options = new string[_humanoidBoneOptions.Length + 1];
                options[0] = BoneLabel(bone);
                Array.Copy(_humanoidBoneOptions, 0, options, 1, _humanoidBoneOptions.Length);
            }

            var showIndex = selected < 0 ? 0 : selected;
            EditorGUI.showMixedValue = bone.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.Popup(showIndex, options, GUILayout.Width(140));
            var changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (!changed)
                return;

            var valueIndex = selected < 0 ? next - 1 : next;
            if (valueIndex >= 0 && valueIndex < _humanoidBoneValues.Length)
                bone.enumValueIndex = _humanoidBoneValues[valueIndex];
        }

        private void DrawGroupFoldout(
            string key,
            string label,
            List<int> indices,
            HashSet<int> drawn,
            bool canEditArray,
            ref int removeIndex)
        {
            var prefsKey = PrefsKeyGroupPrefix + key;
            var open = GetFoldout(prefsKey, DefaultOpenGroups.Contains(key));
            var newOpen = EditorGUILayout.Foldout(open, $"{label} ({indices.Count})", true);
            if (newOpen != open)
                SetFoldout(prefsKey, newOpen);

            foreach (var index in indices)
                drawn.Add(index);

            if (!newOpen)
                return;

            foreach (var index in indices)
            {
                if (DrawLinkEntry(index, canEditArray))
                    removeIndex = index;
            }
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

        /// <returns>削除ボタンが押されたとき true（削除は呼び出し側で描画後に行う）。</returns>
        private bool DrawLinkEntry(int index, bool canEditArray)
        {
            var entry = _links.GetArrayElementAtIndex(index);
            var parentIsRoot = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentIsRoot));
            var parentBone = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentBone));
            var parentChild = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.parentChild));
            var shareChildParent = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.shareChildParent));
            var childParent = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParent));
            var keepLocal = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.keepLocal));

            var parentName = parentIsRoot.hasMultipleDifferentValues || parentBone.hasMultipleDifferentValues
                ? "—"
                : parentIsRoot.boolValue ? "ルート" : BoneLabel(parentBone);
            var keepLocalContent = new GUIContent(
                "ローカルを保持",
                "オンのとき、付け替え前のワールド位置・回転・スケールを維持します。Armature のスケールが 1 でないモデルでも子が原点に潰れません。オフのとき、childParent との間にローカル座標を打ち消すオブジェクトを挟み、合成結果を位置 0、回転 0、スケール 1 にします。親やボーンのスケールが 1 でなくても、生成元の見た目の大きさは保持します。");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{parentName} の子", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var removeClicked = false;
            using (new EditorGUI.DisabledScope(!canEditArray))
            {
                if (GUILayout.Button("削除", EditorStyles.miniButton, GUILayout.Width(40)))
                    removeClicked = true;
            }
            EditorGUILayout.EndHorizontal();

            var parentChanged = DrawBonePopup("親", parentIsRoot, parentBone, includeRoot: true);
            EditorGUILayout.PropertyField(parentChild, new GUIContent($"{parentName} Child"));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(shareChildParent, new GUIContent("まとめて指定"));
            var shareChanged = EditorGUI.EndChangeCheck();

            if (shareChildParent.hasMultipleDifferentValues)
            {
                DrawHelpBoxWithDefaultFont("選択中のオブジェクトで「まとめて指定」の値が異なるため、子ごとの設定は表示していません。", MessageType.Info);
            }
            else if (shareChildParent.boolValue)
            {
                EditorGUILayout.PropertyField(childParent, new GUIContent("子 Parent"));
                EditorGUILayout.PropertyField(keepLocal, keepLocalContent);
            }
            else
            {
                if ((parentChanged || shareChanged) && serializedObject.targetObjects.Length == 1)
                    SyncChildParentSlots(entry, parentIsRoot, parentBone);
                DrawPerChildParents(entry, parentIsRoot, parentBone, keepLocalContent);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(1);
            return removeClicked;
        }

        private void DrawPerChildParents(
            SerializedProperty entry,
            SerializedProperty parentIsRoot,
            SerializedProperty parentBone,
            GUIContent keepLocalContent)
        {
            if (serializedObject.targetObjects.Length > 1)
            {
                // 子ボーン一覧はアバターごとに異なるため、複数選択時は子ごとの設定を編集させない
                DrawHelpBoxWithDefaultFont("複数選択中は子ごとの設定を表示できません。1 つだけ選択してください。", MessageType.Info);
                return;
            }

            var children = GetDirectHumanoidChildren(parentIsRoot.boolValue, (HumanBodyBones)parentBone.enumValueIndex);
            var slots = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParents));

            if (children == null)
            {
                DrawHelpBoxWithDefaultFont("Humanoid Animator が見つからないため、子ボーンを列挙できません。", MessageType.Info);
            }
            else
            {
                if (children.Count == 0)
                    DrawHelpBoxWithDefaultFont("この親のヒューマノイド子はありません。", MessageType.Info);

                if (slots != null && !SlotsMatch(slots, children))
                {
                    DrawHelpBoxWithDefaultFont("子ボーンの一覧が現在のアバターと一致していません。", MessageType.Warning);
                    if (GUILayout.Button("子ボーン一覧を更新"))
                        SyncChildParentSlots(entry, parentIsRoot, parentBone);
                }
            }

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

        private static bool SlotsMatch(SerializedProperty slots, List<HumanBodyBones> bones)
        {
            if (slots.arraySize != bones.Count)
                return false;

            for (var i = 0; i < bones.Count; i++)
            {
                var boneProp = slots.GetArrayElementAtIndex(i)
                    .FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.ChildParentEntry.bone));
                if (boneProp.enumValueIndex != (int)bones[i])
                    return false;
            }

            return true;
        }

        private void SyncChildParentSlots(
            SerializedProperty entry,
            SerializedProperty parentIsRoot,
            SerializedProperty parentBone)
        {
            if (serializedObject.targetObjects.Length > 1)
                return;

            var children = GetDirectHumanoidChildren(parentIsRoot.boolValue, (HumanBodyBones)parentBone.enumValueIndex);
            if (children == null)
                return;

            var sharedKeepLocal = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.keepLocal));
            SyncChildParentSlots(entry, children, sharedKeepLocal == null || sharedKeepLocal.boolValue);
        }

        private static void SyncChildParentSlots(SerializedProperty entry, List<HumanBodyBones> bones, bool defaultKeepLocal)
        {
            var slots = entry.FindPropertyRelative(nameof(ControllableHumanoid.BoneLinkEntry.childParents));
            if (slots == null || bones == null)
                return;

            if (SlotsMatch(slots, bones))
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
            EnsureHumanoidBoneOptions(parentBone);

            var options = new List<string>();
            var values = new List<int>();
            if (includeRoot)
            {
                options.Add("ルート");
                values.Add(-1);
            }

            options.AddRange(_humanoidBoneOptions);
            values.AddRange(_humanoidBoneValues);

            var current = parentIsRoot.boolValue ? -1 : parentBone.enumValueIndex;
            var selected = values.IndexOf(current);
            if (selected < 0)
            {
                options.Insert(0, BoneLabel(parentBone));
                values.Insert(0, current);
                selected = 0;
            }

            EditorGUI.showMixedValue = parentIsRoot.hasMultipleDifferentValues
                || (!parentIsRoot.boolValue && parentBone.hasMultipleDifferentValues);
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.Popup(label, selected, options.ToArray());
            var changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (!changed)
                return false;

            if (values[next] < 0)
                parentIsRoot.boolValue = true;
            else
            {
                parentIsRoot.boolValue = false;
                parentBone.enumValueIndex = values[next];
            }

            return true;
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
            SetFoldout(PrefsKeyGroupPrefix + RootGroupKey, open);
            foreach (var (key, _, _) in BoneGroups)
                SetFoldout(PrefsKeyGroupPrefix + key, open);
            SetFoldout(PrefsKeyGroupPrefix + OtherGroupKey, open);
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
