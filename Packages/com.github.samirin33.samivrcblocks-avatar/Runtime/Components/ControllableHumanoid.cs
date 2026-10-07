using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Samirin33.NDMF.Base;
using nadena.dev.ndmf.runtime;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// ボーンの親子間に Transform を割り込ませる。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB ControllableHumanoid")]
    public class ControllableHumanoid : SamirinMABase
    {
        public const string OriginalArmatureName = "OriginalArmature";

        /// <summary>Editor から登録されるビルド処理。</summary>
        public static Action<ControllableHumanoid, GameObject> BuildHandler;

        /// <summary>Editor から登録される FX パス書き換え処理。</summary>
        public static Action<ControllableHumanoid, GameObject> RemapFxHandler;

        [Serializable]
        public class BoneLinkEntry
        {
            [Serializable]
            public class ChildParentEntry
            {
                public HumanBodyBones bone = HumanBodyBones.Hips;
                public Transform childParent;

                [Tooltip("オンのとき、付け替え前のワールド位置・回転・スケールを維持します。オフのとき、childParent との間にローカル座標を打ち消すオブジェクトを挟み、合成結果をローカル原点（位置 0、回転 0、スケール 1）にします。親のスケールが付け替え前と違うときは、オリジナルのスケールへ補正します。")]
                public bool keepLocal = true;
            }

            public bool parentIsRoot = true;
            public HumanBodyBones parentBone = HumanBodyBones.Hips;
            public Transform parentChild;

            [Tooltip("オンのとき、ヒューマノイド子をすべて同じ childParent に付けます。オフのとき、子ボーンごとに childParent を指定します。")]
            public bool shareChildParent = true;

            public Transform childParent;

            [Tooltip("オンのとき、付け替え前のワールド位置・回転・スケールを維持します。オフのとき、childParent との間にローカル座標を打ち消すオブジェクトを挟み、合成結果をローカル原点（位置 0、回転 0、スケール 1）にします。親のスケールが付け替え前と違うときは、オリジナルのスケールへ補正します。")]
            public bool keepLocal = true;

            public List<ChildParentEntry> childParents = new List<ChildParentEntry>();

            public bool IsComplete
            {
                get
                {
                    if (parentChild == null || (!parentIsRoot && !IsBone(parentBone)))
                        return false;

                    if (shareChildParent)
                        return childParent != null && childParent != parentChild;

                    if (childParents == null)
                        return false;

                    foreach (var slot in childParents)
                    {
                        if (slot != null
                            && slot.childParent != null
                            && slot.childParent != parentChild
                            && IsBone(slot.bone))
                            return true;
                    }

                    return false;
                }
            }

            public static bool IsBone(HumanBodyBones bone) =>
                bone >= 0 && bone < HumanBodyBones.LastBone;
        }

        public List<BoneLinkEntry> links = new List<BoneLinkEntry>();

        [Serializable]
        public class PlayerFollowEntry
        {
            public HumanBodyBones bone = HumanBodyBones.Hips;
            public Transform target;
        }

        public List<PlayerFollowEntry> playerFollowObjects = new List<PlayerFollowEntry>();

        public bool addHeadChop = true;

        public const string HeadChopGlobalScaleProperty = nameof(headChopGlobalScaleFactor);

        [Range(0f, 1f)]
        public float headChopGlobalScaleFactor;

        [NonSerialized]
        public Behaviour BuiltHeadChop;

        public bool editorFollowHumanoid = true;

        /// <summary>アニメーションのプロパティ名。ビルド時に追従 Constraint の Enabled へ展開する。</summary>
        public const string SourceApplyEnabledProperty = nameof(sourceApplyEnabled);

        public bool sourceApplyEnabled = true;

        public const string BoneApplyPrefix = nameof(boneApply) + ".";

        public BoneApplyToggles boneApply = new BoneApplyToggles();

        [Serializable]
        public class BoneApplyToggles
        {
            public bool Hips = true;
            public bool Spine = true;
            public bool Chest = true;
            public bool UpperChest = true;
            public bool Neck = true;
            public bool Head = true;
            public bool LeftEye = true;
            public bool RightEye = true;
            public bool Jaw = true;
            public bool LeftShoulder = true;
            public bool LeftUpperArm = true;
            public bool LeftLowerArm = true;
            public bool LeftHand = true;
            public bool RightShoulder = true;
            public bool RightUpperArm = true;
            public bool RightLowerArm = true;
            public bool RightHand = true;
            public bool LeftUpperLeg = true;
            public bool LeftLowerLeg = true;
            public bool LeftFoot = true;
            public bool LeftToes = true;
            public bool RightUpperLeg = true;
            public bool RightLowerLeg = true;
            public bool RightFoot = true;
            public bool RightToes = true;
            public bool LeftThumbProximal = true;
            public bool LeftThumbIntermediate = true;
            public bool LeftThumbDistal = true;
            public bool LeftIndexProximal = true;
            public bool LeftIndexIntermediate = true;
            public bool LeftIndexDistal = true;
            public bool LeftMiddleProximal = true;
            public bool LeftMiddleIntermediate = true;
            public bool LeftMiddleDistal = true;
            public bool LeftRingProximal = true;
            public bool LeftRingIntermediate = true;
            public bool LeftRingDistal = true;
            public bool LeftLittleProximal = true;
            public bool LeftLittleIntermediate = true;
            public bool LeftLittleDistal = true;
            public bool RightThumbProximal = true;
            public bool RightThumbIntermediate = true;
            public bool RightThumbDistal = true;
            public bool RightIndexProximal = true;
            public bool RightIndexIntermediate = true;
            public bool RightIndexDistal = true;
            public bool RightMiddleProximal = true;
            public bool RightMiddleIntermediate = true;
            public bool RightMiddleDistal = true;
            public bool RightRingProximal = true;
            public bool RightRingIntermediate = true;
            public bool RightRingDistal = true;
            public bool RightLittleProximal = true;
            public bool RightLittleIntermediate = true;
            public bool RightLittleDistal = true;

            public bool IsEnabled(HumanBodyBones bone)
            {
                var field = GetType().GetField(bone.ToString());
                return field == null || (bool)field.GetValue(this);
            }
        }

        [NonSerialized]
        public List<BoneApplyConstraint> BoneApplyConstraints;

        public struct BoneApplyConstraint
        {
            public HumanBodyBones bone;
            public Behaviour constraint;
        }

        /// <summary>
        /// Generating で記録した旧パス→新パス。Optimizing での FX 再書き換えに使う。
        /// </summary>
        [NonSerialized]
        public List<PathRemapEntry> PendingPathRemaps;

        [NonSerialized]
        public List<Behaviour> SourceApplyConstraints;

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

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            // パス書き換えは MA のアニメータ結合後に行う。
            if (buildPhase == SamirinBuildPhase.Optimizing && beforeModularAvatar)
            {
                RemapFxHandler?.Invoke(this, avatarRootObject);
                DestroyImmediate(this);
                return;
            }

            // Merge Armature は mergeTarget をパスで解決する。
            // Generating で同名 Armature を先に置くと、衣装のマージ先がその複製になり、
            // 追従 Parent Constraint のソースがマージ削除で外れる。
            // 分割は MA のマージとヒューマノイド再バインドの後に行う。
            if (buildPhase != SamirinBuildPhase.Transforming || beforeModularAvatar)
                return;

            var all = avatarRootObject.GetComponentsInChildren<ControllableHumanoid>(true);
            if (all.Length > 1)
            {
                if (all[0] == this)
                {
                    Debug.LogWarning(
                        "[ControllableHumanoid] 1つのアバターに複数の ControllableHumanoid は同時に導入できません。" +
                        "ヒューマノイドの複製とパス書き換えが衝突するため、最初のコンポーネントのみ処理し、残りは無視します。",
                        this);
                }
                else
                {
                    Debug.LogWarning(
                        "[ControllableHumanoid] 1つのアバターに複数の ControllableHumanoid は同時に導入できません。このコンポーネントは無視されます。",
                        this);
                    DestroyImmediate(this);
                    return;
                }
            }

            BuildHandler?.Invoke(this, avatarRootObject);
        }

#if UNITY_EDITOR
        private void LateUpdate()
        {
            if (Application.isPlaying || !editorFollowHumanoid)
                return;

            ApplyEditorFollow();
        }

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
            if (links == null)
                return;

            foreach (var entry in links)
            {
                if (entry == null || entry.parentChild == null)
                    continue;
                if (!entry.parentIsRoot && !BoneLinkEntry.IsBone(entry.parentBone))
                    continue;

                var parent = entry.parentIsRoot
                    ? armatureRoot
                    : ResolveHumanoidBone(animator, descriptor, entry.parentBone);
                if (parent == null || entry.parentChild == parent)
                    continue;

                // Armature が回転 -90° かつスケール 100 のとき、Transform.position は
                // 親スケールを落とすか軸を入れ替える。NeckChild が (0, -0.01, 0) 付近に寄る。
                TransformMath.SetWorldPosition(entry.parentChild, TransformMath.GetWorldPosition(parent));
                TransformMath.SetWorldRotation(entry.parentChild, TransformMath.GetWorldRotation(parent));
            }
        }

        private static Transform ResolveHumanoidBone(
            Animator animator,
            VRCAvatarDescriptor descriptor,
            HumanBodyBones bodyBone)
        {
            Transform bone = animator.GetBoneTransform(bodyBone);
            if (bone == null)
                return null;

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
