using System;
using System.Collections.Generic;
using System.Reflection;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
        /// <summary>
        /// 利き手（右手 / 左手）を選び、登録した MA Bone Proxy の Humanoid ボーン、
        /// FixHandVector の Hand Type、任意 Transform の位置・回転を、その利き手用の値へ切り替える。
        /// 同じ GameObject の TuningObject は、その移動のワールド差分だけ追従する。
        /// ビルド時（Resolving / MA 前）に選択中の利き手を適用して自身を削除する。
        /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB DominantHandOption")]
    public class DominantHandOption : SamirinMABase
    {
        public enum DominantHand
        {
            [InspectorName("右手")]
            Right,
            [InspectorName("左手")]
            Left,
        }

        public enum EntryType
        {
            [InspectorName("MA Bone Proxy")]
            BoneProxy,
            [InspectorName("Transform")]
            Transform,
            [InspectorName("Fix Hand Vector")]
            FixHandVector,
        }

        [Serializable]
        public class Entry
        {
            public EntryType type = EntryType.BoneProxy;

            [Tooltip("Humanoid ボーンを書き換える MA Bone Proxy")]
            public ModularAvatarBoneProxy boneProxy;

            [Tooltip("右手のとき Bone Proxy が参照する Humanoid ボーン")]
            public HumanBodyBones boneRight = HumanBodyBones.RightHand;

            [Tooltip("左手のとき Bone Proxy が参照する Humanoid ボーン")]
            public HumanBodyBones boneLeft = HumanBodyBones.LeftHand;

            [Tooltip("Hand Type を利き手に合わせる FixHandVector")]
            public FixHandVector fixHandVector;

            [Tooltip("位置・回転を書き換える Transform")]
            public Transform target;

            public bool setPosition = true;
            [Tooltip("true なら localPosition、false なら position")]
            public bool localPosition = true;
            public Vector3 positionRight;
            public Vector3 positionLeft;

            public bool setRotation = true;
            [Tooltip("true なら localEulerAngles、false なら eulerAngles")]
            public bool localRotation = true;
            public Vector3 rotationRight;
            public Vector3 rotationLeft;
        }

        /// <summary>利き手適用前の、移動対象 Transform のワールド姿勢。</summary>
        public struct MovedTransformSample
        {
            public Transform transform;
            public Matrix4x4 world;
        }

        static readonly MethodInfo BoneProxyClearCacheMethod =
            typeof(ModularAvatarBoneProxy).GetMethod(
                "ClearCache",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                new[] { typeof(bool) },
                null);

        static readonly MethodInfo BoneProxyUpdateMethod =
            typeof(ModularAvatarBoneProxy).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        [Tooltip("現在の利き手。切り替え時、およびビルド時にリストへ適用する")]
        public DominantHand dominantHand = DominantHand.Right;

        [Tooltip("利き手ごとに適用する Bone Proxy / Transform の指定")]
        public List<Entry> entries = new List<Entry>();

        public DominantHandOption()
        {
            // FixHandVector（default 100）は同じ Resolving / MA 前に自身を削除するため、それより先に適用する。
            priority = 90;
        }

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar)
                return;

            Apply();
            DestroyImmediate(this);
        }

        /// <summary>
        /// 選択中の利き手に対応する値を、リストの各指定へ適用する。
        /// </summary>
        public void Apply()
        {
            Apply(dominantHand);
        }

        public void Apply(DominantHand hand)
        {
            if (entries == null)
                return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                switch (entry.type)
                {
                    case EntryType.BoneProxy:
                        ApplyBoneProxy(entry, hand, i);
                        break;
                    case EntryType.FixHandVector:
                        ApplyFixHandVector(entry, hand, i);
                        break;
                    case EntryType.Transform:
                        ApplyTransform(entry, hand, i);
                        break;
                }
            }
        }

        /// <summary>
        /// 利き手適用で動く Transform の、現在のワールド姿勢を記録する。
        /// Apply と FixHandVector の回転補正のあとで <see cref="MoveTuningObject"/> に渡す。
        /// </summary>
        public void SampleMovedTransforms(List<MovedTransformSample> results)
        {
            if (results == null || entries == null)
                return;

            for (var i = 0; i < entries.Count; i++)
            {
                var mover = GetMovedTransform(entries[i]);
                if (mover == null || ContainsTransform(results, mover))
                    continue;

                results.Add(new MovedTransformSample
                {
                    transform = mover,
                    world = mover.localToWorldMatrix,
                });
            }
        }

        /// <summary>
        /// 同じ GameObject の TuningObject を、利き手適用で動いた Transform のワールド差分だけ移動する。
        /// 親子になっている場合は階層側が既に動くので、ここでは動かさない。
        /// 変化した Transform が親子なら、一番深いものの差分だけ使う（親の移動は子のワールド姿勢に含まれる）。
        /// </summary>
        public void MoveTuningObject(List<MovedTransformSample> before, bool recordUndo = true)
        {
            var tuning = GetComponent<TuningObject>();
            if (tuning == null || before == null || before.Count == 0)
                return;

            if (!TryGetDeepestChangedMover(before, tuning.transform, out var driver, out var driverBefore))
                return;

            tuning.FollowWorldPose(driverBefore, driver.localToWorldMatrix, recordUndo);
        }

        public void CollectApplyTargets(List<UnityEngine.Object> results)
        {
            if (results == null || entries == null)
                return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                if (entry.type == EntryType.BoneProxy && entry.boneProxy != null)
                {
                    results.Add(entry.boneProxy);
                    results.Add(entry.boneProxy.transform);
                }
                else if (entry.type == EntryType.FixHandVector && entry.fixHandVector != null)
                {
                    results.Add(entry.fixHandVector);
                    results.Add(entry.fixHandVector.transform);
                }
                else if (entry.type == EntryType.Transform && entry.target != null)
                {
                    results.Add(entry.target);
                }
            }
        }

        void ApplyBoneProxy(Entry entry, DominantHand hand, int index)
        {
            var proxy = entry.boneProxy;
            if (proxy == null)
            {
                Debug.LogWarning(
                    $"[DominantHandOption] Bone Proxy が未設定です。(#{index + 1})",
                    this);
                return;
            }

            proxy.boneReference = hand == DominantHand.Right ? entry.boneRight : entry.boneLeft;
            RefreshBoneProxy(proxy);
        }

        void ApplyFixHandVector(Entry entry, DominantHand hand, int index)
        {
            var fix = entry.fixHandVector;
            if (fix == null)
            {
                Debug.LogWarning(
                    $"[DominantHandOption] FixHandVector が未設定です。(#{index + 1})",
                    this);
                return;
            }

            fix.handType = hand == DominantHand.Right
                ? FixHandVector.HandType.Right
                : FixHandVector.HandType.Left;
        }

        void ApplyTransform(Entry entry, DominantHand hand, int index)
        {
            var targetTransform = entry.target;
            if (targetTransform == null)
            {
                if (entry.setPosition || entry.setRotation)
                {
                    Debug.LogWarning(
                        $"[DominantHandOption] Transform が未設定です。(#{index + 1})",
                        this);
                }

                return;
            }

            var isRight = hand == DominantHand.Right;

            if (entry.setPosition)
            {
                var value = isRight ? entry.positionRight : entry.positionLeft;
                if (entry.localPosition)
                    targetTransform.localPosition = value;
                else
                    targetTransform.position = value;
            }

            if (entry.setRotation)
            {
                var value = isRight ? entry.rotationRight : entry.rotationLeft;
                if (entry.localRotation)
                    targetTransform.localEulerAngles = value;
                else
                    targetTransform.eulerAngles = value;
            }
        }

        Transform GetMovedTransform(Entry entry)
        {
            if (entry == null)
                return null;

            switch (entry.type)
            {
                case EntryType.BoneProxy:
                    return entry.boneProxy != null ? entry.boneProxy.transform : null;
                case EntryType.FixHandVector:
                    return entry.fixHandVector != null ? entry.fixHandVector.transform : null;
                case EntryType.Transform:
                    if (entry.target != null && (entry.setPosition || entry.setRotation))
                        return entry.target;
                    return null;
                default:
                    return null;
            }
        }

        static bool ContainsTransform(List<MovedTransformSample> samples, Transform transform)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                if (samples[i].transform == transform)
                    return true;
            }

            return false;
        }

        static bool TryGetDeepestChangedMover(
            List<MovedTransformSample> before,
            Transform tuningTransform,
            out Transform driver,
            out Matrix4x4 driverBefore)
        {
            driver = null;
            driverBefore = Matrix4x4.identity;
            var bestDepth = int.MinValue;

            for (var i = 0; i < before.Count; i++)
            {
                var sample = before[i];
                var candidate = sample.transform;
                if (candidate == null)
                    continue;
                if (!HasWorldPoseChanged(sample.world, candidate.localToWorldMatrix))
                    continue;
                if (SharesHierarchy(tuningTransform, candidate))
                    continue;
                if (HasChangedDescendant(before, candidate))
                    continue;

                var depth = GetDepth(candidate);
                if (depth < bestDepth)
                    continue;

                bestDepth = depth;
                driver = candidate;
                driverBefore = sample.world;
            }

            return driver != null;
        }

        static bool HasChangedDescendant(List<MovedTransformSample> before, Transform ancestor)
        {
            for (var i = 0; i < before.Count; i++)
            {
                var other = before[i].transform;
                if (other == null || other == ancestor)
                    continue;
                if (!other.IsChildOf(ancestor))
                    continue;
                if (HasWorldPoseChanged(before[i].world, other.localToWorldMatrix))
                    return true;
            }

            return false;
        }

        static bool SharesHierarchy(Transform a, Transform b)
        {
            return a == b || a.IsChildOf(b) || b.IsChildOf(a);
        }

        static int GetDepth(Transform transform)
        {
            var depth = 0;
            for (var current = transform; current != null; current = current.parent)
                depth++;
            return depth;
        }

        static bool HasWorldPoseChanged(Matrix4x4 before, Matrix4x4 after)
        {
            if ((before.GetPosition() - after.GetPosition()).sqrMagnitude > 1e-10f)
                return true;
            if (Quaternion.Angle(before.rotation, after.rotation) > 0.01f)
                return true;
            return (before.lossyScale - after.lossyScale).sqrMagnitude > 1e-10f;
        }

        static void RefreshBoneProxy(ModularAvatarBoneProxy proxy)
        {
            if (proxy == null)
                return;

            BoneProxyClearCacheMethod?.Invoke(proxy, new object[] { true });
            BoneProxyUpdateMethod?.Invoke(proxy, null);
        }

    }
}
