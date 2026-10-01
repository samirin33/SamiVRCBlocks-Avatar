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

        static void RefreshBoneProxy(ModularAvatarBoneProxy proxy)
        {
            if (proxy == null)
                return;

            BoneProxyClearCacheMethod?.Invoke(proxy, new object[] { true });
            BoneProxyUpdateMethod?.Invoke(proxy, null);
        }

    }
}
