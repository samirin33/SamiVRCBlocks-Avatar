using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.runtime;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(FixHandVector))]
    [CanEditMultipleObjects]
    public class FixHandVectorEditor : SamirinMABaseEditor
    {
        private SerializedProperty _handType;
        private SerializedProperty _tipVector;
        private SerializedProperty _upVector;

        private void OnEnable()
        {
            _handType = serializedObject.FindProperty(nameof(FixHandVector.handType));
            _tipVector = serializedObject.FindProperty(nameof(FixHandVector.TipVector));
            _upVector = serializedObject.FindProperty(nameof(FixHandVector.UpVector));

            foreach (var t in targets)
            {
                if (t is FixHandVector fix)
                    FixHandVectorApplier.ScheduleApply(fix);
            }
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_handType, new GUIContent("Hand Type"));
                EditorGUILayout.PropertyField(_tipVector, new GUIContent("Tip Vector"));
                EditorGUILayout.PropertyField(_upVector, new GUIContent("Up Vector"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    foreach (var t in targets)
                    {
                        if (t is FixHandVector fix)
                            FixHandVectorApplier.ScheduleApply(fix);
                    }
                }
                else
                {
                    serializedObject.ApplyModifiedProperties();
                }

                EditorGUILayout.Space(8);
                if (GUILayout.Button("手の向きに合わせて回転を再適用"))
                {
                    foreach (var t in targets)
                    {
                        if (t is FixHandVector fix)
                            FixHandVectorApplier.ApplyNow(fix, recordUndo: true);
                    }
                }
            });
        }
    }

    /// <summary>
    /// 配置・親変更時に FixHandVector の回転補正を行う（プレイ／NDMF ビルド中は何もしない）。
    /// </summary>
    [InitializeOnLoad]
    internal static class FixHandVectorApplier
    {
        private static readonly HashSet<int> ScheduledInstanceIds = new HashSet<int>();

        static FixHandVectorApplier()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (!CanApplyInEditor()) return;

            for (var i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeGameObjectParent:
                        {
                            stream.GetChangeGameObjectParentEvent(i, out var evt);
                            TryScheduleByInstanceId(evt.instanceId);
                            break;
                        }
                    case ObjectChangeKind.ChangeGameObjectStructure:
                        {
                            stream.GetChangeGameObjectStructureEvent(i, out var evt);
                            TryScheduleByInstanceId(evt.instanceId);
                            break;
                        }
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                        {
                            stream.GetCreateGameObjectHierarchyEvent(i, out var evt);
                            TryScheduleByInstanceId(evt.instanceId);
                            break;
                        }
                }
            }
        }

        private static void TryScheduleByInstanceId(int instanceId)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (obj == null) return;

            var fixes = obj.GetComponentsInChildren<FixHandVector>(true);
            for (var i = 0; i < fixes.Length; i++)
                ScheduleApply(fixes[i]);

            // コンポーネント追加直後は同一 GO 上のみのこともある
            var self = obj.GetComponent<FixHandVector>();
            if (self != null)
                ScheduleApply(self);
        }

        public static void ScheduleApply(FixHandVector fix)
        {
            if (fix == null || !CanApplyInEditor()) return;

            var id = fix.GetInstanceID();
            if (!ScheduledInstanceIds.Add(id)) return;

            EditorApplication.delayCall += () =>
            {
                ScheduledInstanceIds.Remove(id);
                var obj = EditorUtility.InstanceIDToObject(id) as FixHandVector;
                if (obj == null) return;
                ApplyNow(obj, recordUndo: false);
            };
        }

        public static void ApplyNow(FixHandVector fix, bool recordUndo)
        {
            if (fix == null || !CanApplyInEditor(fix.gameObject)) return;

            var tipLocal = fix.TipVector;
            var upLocal = fix.UpVector;
            if (tipLocal.sqrMagnitude < 1e-8f || upLocal.sqrMagnitude < 1e-8f) return;
            tipLocal.Normalize();
            upLocal.Normalize();
            if (Mathf.Abs(Vector3.Dot(tipLocal, upLocal)) > 0.999f) return;

            var avatarRoot = RuntimeUtil.FindAvatarInParents(fix.transform);
            if (avatarRoot == null) return;

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;

            if (!TryResolveParentHumanoidBone(fix.transform, animator, out _))
                return;

            if (!TryGetHandWorldAxes(animator, avatarRoot, fix.handType, out var tipWorld, out var upWorld))
                return;

            if (!IsFinite(tipWorld) || !IsFinite(upWorld)) return;

            var worldRot = Quaternion.LookRotation(tipWorld, upWorld);
            var localAxesRot = Quaternion.LookRotation(tipLocal, upLocal);
            var targetRot = worldRot * Quaternion.Inverse(localAxesRot);

            if (!IsFinite(targetRot)) return;
            if (Quaternion.Angle(fix.transform.rotation, targetRot) < 0.01f) return;

            // 自動適用では Undo を使わない（Inspector 描画中の SerializedProperty 破壊を避ける）
            if (recordUndo)
                Undo.RecordObject(fix.transform, "Fix Hand Vector Rotation");

            fix.transform.rotation = targetRot;
            EditorUtility.SetDirty(fix.transform);
        }

        private static bool CanApplyInEditor()
        {
            return !RuntimeUtil.IsPlaying;
        }

        private static bool CanApplyInEditor(GameObject go)
        {
            if (!CanApplyInEditor()) return false;
            if (go == null) return false;
            if (EditorUtility.IsPersistent(go)) return false;
            if ((go.hideFlags & HideFlags.DontSave) != 0) return false;
            if (!go.scene.IsValid() || !go.scene.isLoaded) return false;
            return true;
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        private static bool IsFinite(Quaternion q)
        {
            return IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        private static bool TryResolveParentHumanoidBone(
            Transform self,
            Animator animator,
            out HumanBodyBones bone)
        {
            bone = HumanBodyBones.LastBone;
            var boneMap = BuildHumanoidBoneMap(animator);
            if (boneMap.Count == 0) return false;

            if (TryResolveBoneFromProxyOrTransform(boneMap, self, requireHumanoidTransform: false, out bone))
                return true;

            for (var current = self.parent; current != null; current = current.parent)
            {
                if (TryResolveBoneFromProxyOrTransform(boneMap, current, requireHumanoidTransform: true, out bone))
                    return true;
            }

            return false;
        }

        private static bool TryResolveBoneFromProxyOrTransform(
            Dictionary<Transform, HumanBodyBones> boneMap,
            Transform node,
            bool requireHumanoidTransform,
            out HumanBodyBones bone)
        {
            bone = HumanBodyBones.LastBone;

            var proxy = node.GetComponent<ModularAvatarBoneProxy>();
            if (proxy != null && TryResolveBoneFromProxy(boneMap, proxy, out bone))
                return true;

            if (!requireHumanoidTransform)
                return false;

            return TryFindHumanoidBoneForTransform(boneMap, node, out bone);
        }

        private static bool TryResolveBoneFromProxy(
            Dictionary<Transform, HumanBodyBones> boneMap,
            ModularAvatarBoneProxy proxy,
            out HumanBodyBones bone)
        {
            bone = HumanBodyBones.LastBone;

            if (proxy.boneReference != HumanBodyBones.LastBone)
            {
                bone = proxy.boneReference;
                return true;
            }

            // target ゲッターは階層購読の副作用があるため使わない
            if (string.IsNullOrWhiteSpace(proxy.subPath) || proxy.subPath == "$$AVATAR")
                return false;

            var avatarRoot = RuntimeUtil.FindAvatarInParents(proxy.transform);
            if (avatarRoot == null) return false;

            var resolved = avatarRoot.Find(proxy.subPath);
            return resolved != null && TryFindHumanoidBoneForTransform(boneMap, resolved, out bone);
        }

        private static Dictionary<Transform, HumanBodyBones> BuildHumanoidBoneMap(Animator animator)
        {
            var map = new Dictionary<Transform, HumanBodyBones>();
            foreach (HumanBodyBones boneType in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (boneType == HumanBodyBones.LastBone) continue;
                var boneTransform = animator.GetBoneTransform(boneType);
                if (boneTransform != null)
                    map[boneTransform] = boneType;
            }
            return map;
        }

        private static bool TryFindHumanoidBoneForTransform(
            Dictionary<Transform, HumanBodyBones> boneMap,
            Transform start,
            out HumanBodyBones bone)
        {
            bone = HumanBodyBones.LastBone;
            for (var iter = start; iter != null; iter = iter.parent)
            {
                if (boneMap.TryGetValue(iter, out bone))
                    return true;
            }
            return false;
        }

        private static bool TryGetHandWorldAxes(
            Animator animator,
            Transform avatarRoot,
            FixHandVector.HandType handType,
            out Vector3 tipWorld,
            out Vector3 upWorld)
        {
            tipWorld = default;
            upWorld = default;

            var isRight = handType == FixHandVector.HandType.Right;
            var handBone = isRight ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
            var lowerArmBone = isRight ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm;
            var thumbBone = isRight ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal;
            var indexBone = isRight ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal;
            var pinkyBone = isRight ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal;

            var hand = animator.GetBoneTransform(handBone);
            if (hand == null) return false;

            if (!TryGetTipDirection(animator, hand, handType, lowerArmBone, out tipWorld))
                return false;

            Vector3 sideDir;
            var thumb = animator.GetBoneTransform(thumbBone);
            if (thumb != null)
            {
                sideDir = thumb.position - hand.position;
            }
            else
            {
                var index = animator.GetBoneTransform(indexBone);
                var pinky = animator.GetBoneTransform(pinkyBone);
                if (index != null && pinky != null)
                    sideDir = index.position - pinky.position;
                else
                    sideDir = avatarRoot.forward;
            }

            if (sideDir.sqrMagnitude < 1e-8f)
                sideDir = avatarRoot.forward;

            var palmOrBack = Vector3.Cross(tipWorld, sideDir.normalized);
            if (palmOrBack.sqrMagnitude < 1e-8f) return false;

            upWorld = (isRight ? -palmOrBack : palmOrBack).normalized;
            upWorld = (upWorld - tipWorld * Vector3.Dot(upWorld, tipWorld)).normalized;
            return upWorld.sqrMagnitude > 1e-8f;
        }

        private static bool TryGetTipDirection(
            Animator animator,
            Transform hand,
            FixHandVector.HandType handType,
            HumanBodyBones lowerArmBone,
            out Vector3 tipWorld)
        {
            tipWorld = default;
            var isRight = handType == FixHandVector.HandType.Right;

            var fingerChains = isRight
                ? new[]
                {
                    new[] { HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleProximal },
                    new[] { HumanBodyBones.RightIndexDistal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexProximal },
                    new[] { HumanBodyBones.RightRingDistal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingProximal },
                    new[] { HumanBodyBones.RightLittleDistal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleProximal },
                }
                : new[]
                {
                    new[] { HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleProximal },
                    new[] { HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexProximal },
                    new[] { HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingProximal },
                    new[] { HumanBodyBones.LeftLittleDistal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleProximal },
                };

            foreach (var chain in fingerChains)
            {
                foreach (var bone in chain)
                {
                    var tip = animator.GetBoneTransform(bone);
                    if (tip == null) continue;
                    var dir = tip.position - hand.position;
                    if (dir.sqrMagnitude < 1e-8f) continue;
                    tipWorld = dir.normalized;
                    return true;
                }
            }

            var lowerArm = animator.GetBoneTransform(lowerArmBone);
            if (lowerArm == null) return false;
            var fallback = hand.position - lowerArm.position;
            if (fallback.sqrMagnitude < 1e-8f) return false;
            tipWorld = fallback.normalized;
            return true;
        }
    }
}
