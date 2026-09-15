using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Samirin33.NDMF.Base;
using nadena.dev.ndmf.runtime;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// ビルド時にヒューマノイドを複製し、Source は複製へ、Target はオリジナルへ
    /// Constraint で接続する。エディタ上では Source/Target がヒューマノイドに追従する。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB ControllableHumanoid")]
    public class ControllableHumanoid : SamirinMABase
    {
        public const string ProxyNamePrefix = "Proxy_";
        public const string OriginalBoneRootName = "OriginalBone";

        /// <summary>Editor から登録されるビルド処理。</summary>
        public static Action<ControllableHumanoid, GameObject> BuildHandler;

        /// <summary>Editor から登録される FX パス書き換え処理。</summary>
        public static Action<ControllableHumanoid, GameObject> RemapFxHandler;

        [Serializable]
        public class BoneControlEntry
        {
            public HumanBodyBones bone = HumanBodyBones.Hips;

            [Tooltip("複製ヒューマノイド → 制御用 Constraint の付与先。未設定ならスキップ。")]
            public Transform source;

            [Tooltip("制御 → オリジナル Constraint の付与先。未設定なら Source と同じ。")]
            public Transform target;

            /// <summary>Target 未設定時は Source を返す。</summary>
            public Transform ResolvedTarget => target != null ? target : source;
        }

        [Tooltip("ボーンごとの Source/Target。構造追加時に Proxy_* が両方へ自動設定され、後から変更可能。")]
        public List<BoneControlEntry> boneControls = new List<BoneControlEntry>();

        [Tooltip("Head に VRCHeadChop を付与する（一人称スケール抑制）。")]
        public bool addHeadChop = true;

        [Tooltip("エディタ上で Source/Target をヒューマノイドへ追従させる")]
        public bool editorFollowHumanoid = true;

        /// <summary>
        /// Generating で記録した旧パス→新パス。Optimizing での FX 再書き換えに使う。
        /// </summary>
        [NonSerialized]
        public List<PathRemapEntry> PendingPathRemaps;

        [Serializable]
        public struct PathRemapEntry
        {
            public string oldPath;
            public string newPath;
        }

        public ControllableHumanoid()
        {
            priority = 50;
        }

        public static string GetProxyObjectName(HumanBodyBones bone) => ProxyNamePrefix + bone;

        public BoneControlEntry FindEntry(HumanBodyBones bone)
        {
            if (boneControls == null)
                return null;

            for (var i = 0; i < boneControls.Count; i++)
            {
                var entry = boneControls[i];
                if (entry != null && entry.bone == bone)
                    return entry;
            }

            return null;
        }

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (!beforeModularAvatar)
                return;

            var all = avatarRootObject.GetComponentsInChildren<ControllableHumanoid>(true);
            if (all.Length > 0 && all[0] != this)
            {
                if (buildPhase == SamirinBuildPhase.Generating)
                {
                    Debug.LogWarning(
                        "[ControllableHumanoid] アバター内に複数検出されたため、このコンポーネントは無視されます。",
                        this);
                    DestroyImmediate(this);
                }
                return;
            }

            if (buildPhase == SamirinBuildPhase.Generating)
            {
                BuildHandler?.Invoke(this, avatarRootObject);
                return;
            }

            if (buildPhase == SamirinBuildPhase.Optimizing)
            {
                RemapFxHandler?.Invoke(this, avatarRootObject);
                DestroyImmediate(this);
            }
        }

#if UNITY_EDITOR
        private void LateUpdate()
        {
            if (Application.isPlaying || !editorFollowHumanoid)
                return;

            ApplyEditorFollow();
        }

        /// <summary>
        /// コンポーネント自身を Armature ルートへ、Source/Target を対応ヒューマノイドへ合わせる。
        /// </summary>
        public void ApplyEditorFollow()
        {
            var avatarRoot = RuntimeUtil.FindAvatarInParents(transform);
            if (avatarRoot == null)
                return;

            if (!avatarRoot.TryGetComponent<Animator>(out var animator) || !animator.isHuman)
                return;

            avatarRoot.TryGetComponent<VRCAvatarDescriptor>(out var descriptor);

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var armatureRoot = hips != null ? hips.parent : null;
            if (armatureRoot != null)
                transform.SetPositionAndRotation(armatureRoot.position, armatureRoot.rotation);

            if (boneControls == null || boneControls.Count == 0)
                return;

            foreach (var entry in boneControls)
            {
                if (entry == null)
                    continue;

                var bone = ResolveHumanoidBone(animator, descriptor, entry.bone);
                if (bone == null)
                    continue;

                if (entry.source != null)
                    entry.source.SetPositionAndRotation(bone.position, bone.rotation);

                var resolvedTarget = entry.ResolvedTarget;
                if (resolvedTarget != null && resolvedTarget != entry.source)
                    resolvedTarget.SetPositionAndRotation(bone.position, bone.rotation);
            }
        }

        private static Transform ResolveHumanoidBone(
            Animator animator,
            VRCAvatarDescriptor descriptor,
            HumanBodyBones bodyBone)
        {
            Transform bone = animator.GetBoneTransform(bodyBone);

            if (descriptor != null)
            {
                switch (bodyBone)
                {
                    case HumanBodyBones.LeftEye:
                        if (descriptor.customEyeLookSettings.leftEye != null)
                            bone = descriptor.customEyeLookSettings.leftEye;
                        break;
                    case HumanBodyBones.RightEye:
                        if (descriptor.customEyeLookSettings.rightEye != null)
                            bone = descriptor.customEyeLookSettings.rightEye;
                        break;
                }
            }

            return bone;
        }
#endif
    }
}
