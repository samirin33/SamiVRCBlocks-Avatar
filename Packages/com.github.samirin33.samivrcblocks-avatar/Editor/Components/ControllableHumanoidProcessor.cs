using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using Samirin33.NDMF.Base;
using Samirin33.NDMF.Components;
using Samirin33.NDMF.Constraints;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// ControllableHumanoid のビルド時処理。
    /// </summary>
    [InitializeOnLoad]
    internal static class ControllableHumanoidProcessor
    {
        static ControllableHumanoidProcessor()
        {
            ControllableHumanoid.BuildHandler = Build;
            ControllableHumanoid.RemapFxHandler = RemapFxLayerPaths;
            ControllableHumanoid.RestoreFollowSpaceHandler = RestoreFollowSpaces;
        }

        /// <summary>
        /// ControllableHumanoid の処理は拡張コンテキストを宣言しない汎用パス内で動くため、
        /// AnimatorServicesContext を自前で開き、同じパス内で必ず閉じる。
        /// NDMF の実行計画上は非アクティブ扱いなので、開いたままにすると後段（VRCFury 等）が
        /// 直接編集した Animator を、ビルド終了時のコミットが古い仮想コントローラで上書きする。
        /// 閉じたとき ObjectPathRemapper が、開いた時点から移動したオブジェクトのパスを
        /// 全 Animator（特殊レイヤー・PlayAudio 含む）へ反映する。
        /// </summary>
        private sealed class AnimatorServicesScope : IDisposable
        {
            private readonly BuildContext _context;
            private readonly bool _ownsServices;
            private readonly bool _ownsControllers;

            public AnimatorServicesContext Services { get; }

            private AnimatorServicesScope(BuildContext context)
            {
                _context = context;
                _ownsControllers = !IsActive<VirtualControllerContext>(context);
                _ownsServices = !IsActive<AnimatorServicesContext>(context);
                Services = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            }

            public static AnimatorServicesScope Open(ControllableHumanoid component)
            {
                var context = SamirinBuildContext.Current;
                if (context != null)
                    return new AnimatorServicesScope(context);

                SamirinBuildContext.ReportError(
                    "[ControllableHumanoid] NDMF のビルド外では Armature の分割とアニメーションのパス書き換えを行えません。",
                    component);
                return null;
            }

            private static bool IsActive<T>(BuildContext context) where T : IExtensionContext
            {
                try
                {
                    context.Extension<T>();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public void Dispose()
            {
                if (_ownsServices)
                    _context.DeactivateExtensionContext<AnimatorServicesContext>();
                if (_ownsControllers)
                    _context.DeactivateExtensionContext<VirtualControllerContext>();
            }
        }

        #region Build

        public static void Build(ControllableHumanoid component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null)
                return;

            if (!avatarRootObject.TryGetComponent<Animator>(out var animator) || !animator.isHuman)
            {
                SamirinBuildContext.ReportWarning(
                    "[ControllableHumanoid] Humanoid Animator が見つからないためスキップします。",
                    component);
                return;
            }

            if (!avatarRootObject.TryGetComponent<VRCAvatarDescriptor>(out var descriptor))
            {
                SamirinBuildContext.ReportWarning(
                    "[ControllableHumanoid] VRCAvatarDescriptor が見つからないためスキップします。",
                    component);
                return;
            }

            var hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsBone == null)
            {
                SamirinBuildContext.ReportWarning("[ControllableHumanoid] Hips ボーンが見つかりません。", component);
                return;
            }

            // ルート（Armature）は Hips 移動前に取得
            var armatureRoot = hipsBone.parent;
            if (armatureRoot == null)
            {
                SamirinBuildContext.ReportWarning("[ControllableHumanoid] Armature ルート（Hips の親）が見つかりません。", component);
                return;
            }

            var humanoidBones = CollectHumanoidBones(animator, descriptor);
            if (humanoidBones.Count == 0)
            {
                SamirinBuildContext.ReportWarning("[ControllableHumanoid] ヒューマノイドボーンが空です。", component);
                return;
            }

            // 階層を動かす前に開き、分割前のパスを ObjectPathRemapper に記録させる。
            using (var animators = AnimatorServicesScope.Open(component))
            {
                if (animators == null)
                    return;

                BuildSplit(component, avatarRootObject, armatureRoot, humanoidBones, animators.Services);
            }
        }

        private static void BuildSplit(
            ControllableHumanoid component,
            GameObject avatarRootObject,
            Transform armatureRoot,
            Dictionary<Transform, HumanBodyBones> humanoidBones,
            AnimatorServicesContext services)
        {
            var avatarRoot = avatarRootObject.transform;

            var clonedArmature = new GameObject(armatureRoot.name).transform;
            clonedArmature.SetParent(armatureRoot.parent, false);
            TransformMath.CopyLocalPose(armatureRoot, clonedArmature);

            // hand.R と Little Proximal.R の間の handrole.R のように、
            // ヒューマノイド骨の間にある非ヒューマノイドも複製しないと親子が崩れ、
            // Parent Constraint のローカル解決がオリジナルとずれる。
            var cloneSources = CollectCloneSources(humanoidBones, armatureRoot);
            var boneMap = new Dictionary<Transform, Transform>(cloneSources.Count);
            foreach (var original in cloneSources)
            {
                var cloned = new GameObject(original.name).transform;
                cloned.SetParent(clonedArmature, false);
                boneMap[original] = cloned;
            }

            // 親より先に子を置くと、ワールド姿勢のコピーが親の初期値（原点）基準になる。
            var pending = new List<Transform>(boneMap.Keys);
            var placed = new HashSet<Transform>();
            var guard = pending.Count;
            while (pending.Count > 0 && guard-- >= 0)
            {
                var index = pending.FindIndex(original => IsCloneParentReady(original, armatureRoot, boneMap, placed));
                if (index < 0)
                    index = 0;

                var original = pending[index];
                pending.RemoveAt(index);
                PlaceCloneBone(original, boneMap[original], armatureRoot, clonedArmature, boneMap, humanoidBones);
                placed.Add(original);
            }

            WarnIfCloneLocalPoseDiffers(component, boneMap, armatureRoot, clonedArmature, avatarRoot);

            var originalArmature = new GameObject(ControllableHumanoid.OriginalArmatureName).transform;
            originalArmature.SetParent(avatarRoot, false);
            originalArmature.localPosition = Vector3.zero;
            originalArmature.localRotation = Quaternion.identity;
            originalArmature.localScale = Vector3.one;
            var armatureWorldPosition = TransformMath.GetWorldPosition(armatureRoot);
            var armatureWorldRotation = TransformMath.GetWorldRotation(armatureRoot);
            var armatureWorldScale = TransformMath.GetWorldScale(armatureRoot);
            armatureRoot.SetParent(originalArmature, false);
            TransformMath.SetWorldPose(armatureRoot, armatureWorldPosition, armatureWorldRotation, armatureWorldScale);

            var originalByBone = new Dictionary<HumanBodyBones, Transform>(humanoidBones.Count);
            foreach (var kvp in humanoidBones)
                originalByBone[kvp.Value] = kvp.Key;

            var sourceConstraints = new List<Behaviour>();
            var boneApplyConstraints = new List<ControllableHumanoid.BoneApplyConstraint>();
            if (component.links != null)
            {
                foreach (var entry in component.links)
                    ApplyLink(entry, armatureRoot, clonedArmature, originalByBone, boneMap, humanoidBones);
            }

            foreach (var kvp in boneMap)
            {
                if (!humanoidBones.TryGetValue(kvp.Key, out var bodyBone))
                    continue;

                var follow = AddProxyFollowParentConstraint(kvp.Key.gameObject, kvp.Value);
                if (follow == null)
                    continue;

                var boneEnabled = component.boneApply == null || component.boneApply.IsEnabled(bodyBone);
                follow.enabled = component.sourceApplyEnabled;
                SetConstraintActive(follow, boneEnabled);
                sourceConstraints.Add(follow);
                boneApplyConstraints.Add(new ControllableHumanoid.BoneApplyConstraint
                {
                    bone = bodyBone,
                    constraint = follow,
                    parent = kvp.Key.parent,
                    source = kvp.Value,
                    path = AnimationUtility.CalculateTransformPath(kvp.Key, avatarRoot),
                });
            }

            component.BuiltHeadChop = null;
            if (component.addHeadChop
                && originalByBone.TryGetValue(HumanBodyBones.Head, out var head)
                && head != null)
                component.BuiltHeadChop = AddHeadChop(head, component.headChopGlobalScaleFactor);

            PlacePlayerFollowObjects(component, originalByBone, boneMap);

            component.SourceApplyConstraints = sourceConstraints;
            component.BoneApplyConstraints = boneApplyConstraints;

            // 移動したボーン（OriginalArmature 配下へ移った元 Armature 等）のパスは、
            // スコープを閉じるときに ObjectPathRemapper が書き換える。
            ExpandConstraintToggleAnimations(component, services);
        }

        private static void ApplyLink(
            ControllableHumanoid.BoneLinkEntry entry,
            Transform armatureRoot,
            Transform clonedArmature,
            Dictionary<HumanBodyBones, Transform> originalByBone,
            Dictionary<Transform, Transform> boneMap,
            Dictionary<Transform, HumanBodyBones> humanoidBones)
        {
            if (entry == null || !entry.IsComplete)
                return;

            Transform originalParent;
            Transform clonedParent;
            if (entry.parentIsRoot)
            {
                originalParent = armatureRoot;
                clonedParent = clonedArmature;
            }
            else if (!originalByBone.TryGetValue(entry.parentBone, out originalParent) || originalParent == null)
            {
                return;
            }
            else if (!boneMap.TryGetValue(originalParent, out clonedParent) || clonedParent == null)
            {
                return;
            }

            var parentChild = entry.parentChild;
            if (parentChild == null || parentChild == originalParent)
                return;
            if (IsUnder(originalParent, parentChild))
                return;

            // 複製側のワールド姿勢へ合わせると、スケール 100 の Armature では
            // 逆変換が軸を入れ替えて NeckChild が約 (0, -0.01, 0) に寄る。
            // 元ボーンの子としてローカル位置・回転だけ原点に合わせ、
            // ビルド時のワールドスケールは親スケールが 1 でなくても維持する。
            var parentChildWorldScale = TransformMath.GetWorldScale(parentChild);
            parentChild.SetParent(originalParent, false);
            parentChild.localPosition = Vector3.zero;
            parentChild.localRotation = Quaternion.identity;
            TransformMath.SetWorldScale(parentChild, parentChildWorldScale);

            var children = new List<Transform>();
            foreach (var child in humanoidBones.Keys)
            {
                if (child == null || child == originalParent || child == parentChild)
                    continue;
                if (NearestHumanoidAncestor(child, humanoidBones) != originalParent)
                    continue;
                children.Add(child);
            }

            children.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            if (entry.shareChildParent)
            {
                foreach (var child in children)
                    PlaceChild(child, entry.childParent, entry.keepLocal);
                return;
            }

            if (entry.childParents == null)
                return;

            var slots = new Dictionary<HumanBodyBones, ControllableHumanoid.BoneLinkEntry.ChildParentEntry>();
            foreach (var slot in entry.childParents)
            {
                if (slot == null || slot.childParent == null || !ControllableHumanoid.BoneLinkEntry.IsBone(slot.bone))
                    continue;
                slots[slot.bone] = slot;
            }

            foreach (var child in children)
            {
                if (!humanoidBones.TryGetValue(child, out var bone))
                    continue;
                if (!slots.TryGetValue(bone, out var slot))
                    continue;
                PlaceChild(child, slot.childParent, slot.keepLocal);
            }
        }

        private static void PlaceChild(Transform bone, Transform parent, bool keepLocal)
        {
            if (bone == null || parent == null || bone == parent)
                return;
            if (IsUnder(parent, bone))
                return;

            var worldPosition = TransformMath.GetWorldPosition(bone);
            var worldRotation = TransformMath.GetWorldRotation(bone);
            var worldScale = TransformMath.GetWorldScale(bone);
            var localPosition = bone.localPosition;
            var localRotation = bone.localRotation;
            var localScale = bone.localScale;
            // ParentChild が自身のワールドスケールを保つと、ChildParent のスケールは
            // 元ボーンの親とずれる。ボーン側はオリジナルのスケールへ戻す。
            var receiver = ParentCorrectedToOriginalScale(parent, bone.name, worldScale, localScale);

            if (keepLocal)
            {
                // ローカル値のコピーだと、Armature スケール 100 のモデルでは
                // Neck→Head のような小さいローカル座標がスケール 1 の親の上で原点に潰れる。
                bone.SetParent(receiver, false);
                TransformMath.SetWorldPose(bone, worldPosition, worldRotation, worldScale);
                return;
            }

            var cancel = new GameObject(bone.name + "_LocalCancel").transform;
            cancel.SetParent(receiver, false);
            SetInverseLocal(cancel, localPosition, localRotation, localScale);
            bone.SetParent(cancel, false);
        }

        /// <summary>
        /// childParent のワールドスケールが付け替え前の実効親スケールと違うとき、
        /// その差分を吸収する子を挟み、ボーンのローカルスケールがオリジナルのままになるようにする。
        /// </summary>
        private static Transform ParentCorrectedToOriginalScale(
            Transform parent,
            string boneName,
            Vector3 boneWorldScale,
            Vector3 boneLocalScale)
        {
            var originalScale = new Vector3(
                DivideScale(boneWorldScale.x, boneLocalScale.x),
                DivideScale(boneWorldScale.y, boneLocalScale.y),
                DivideScale(boneWorldScale.z, boneLocalScale.z));
            if (ScalesMatch(TransformMath.GetWorldScale(parent), originalScale))
                return parent;

            var anchor = new GameObject(boneName + "_OriginalScale").transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = Vector3.zero;
            anchor.localRotation = Quaternion.identity;
            TransformMath.SetWorldScale(anchor, originalScale);
            return anchor;
        }

        private static bool ScalesMatch(Vector3 a, Vector3 b)
        {
            const float tolerance = 0.0001f;
            return Mathf.Abs(a.x - b.x) <= tolerance
                && Mathf.Abs(a.y - b.y) <= tolerance
                && Mathf.Abs(a.z - b.z) <= tolerance;
        }

        private static float DivideScale(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 1e-8f ? value / divisor : value;
        }

        /// <summary>
        /// cancel のローカル姿勢と bone のローカル姿勢の合成が恒等になるようにする。
        /// </summary>
        private static void SetInverseLocal(Transform cancel, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var inverseScale = new Vector3(InverseScale(scale.x), InverseScale(scale.y), InverseScale(scale.z));
            var inverseRotation = Quaternion.Inverse(rotation);
            cancel.localScale = inverseScale;
            cancel.localRotation = inverseRotation;
            cancel.localPosition = -(inverseRotation * Vector3.Scale(position, inverseScale));
        }

        private static float InverseScale(float scale)
        {
            if (Mathf.Abs(scale) < 1e-6f)
                return 0f;
            return 1f / scale;
        }

        public static List<HumanBodyBones> CollectDirectHumanoidChildren(
            Animator animator,
            VRCAvatarDescriptor descriptor,
            bool parentIsRoot,
            HumanBodyBones parentBone)
        {
            if (animator == null || !animator.isHuman)
                return null;

            var humanoidBones = CollectHumanoidBones(animator, descriptor);
            Transform parent = null;
            if (parentIsRoot)
            {
                parent = animator.GetBoneTransform(HumanBodyBones.Hips)?.parent;
            }
            else
            {
                foreach (var pair in humanoidBones)
                {
                    if (pair.Value != parentBone)
                        continue;
                    parent = pair.Key;
                    break;
                }
            }

            var result = new List<HumanBodyBones>();
            if (parent == null)
                return result;

            var children = new List<(HumanBodyBones bone, int sibling)>();
            foreach (var pair in humanoidBones)
            {
                if (pair.Key == null || pair.Key == parent)
                    continue;
                if (NearestHumanoidAncestor(pair.Key, humanoidBones) != parent)
                    continue;
                children.Add((pair.Value, pair.Key.GetSiblingIndex()));
            }

            children.Sort((a, b) => a.sibling.CompareTo(b.sibling));
            foreach (var child in children)
                result.Add(child.bone);
            return result;
        }

        /// <summary>
        /// ヒューマノイド骨と、Armature からそこまでの中間 Transform を複製対象にする。
        /// </summary>
        private static List<Transform> CollectCloneSources(
            Dictionary<Transform, HumanBodyBones> humanoidBones,
            Transform armatureRoot)
        {
            var result = new List<Transform>();
            var seen = new HashSet<Transform>();
            foreach (var bone in humanoidBones.Keys)
            {
                if (bone == null || !seen.Add(bone))
                    continue;

                result.Add(bone);
                if (armatureRoot == null || !IsUnder(bone, armatureRoot))
                    continue;

                var current = bone.parent;
                while (current != null && current != armatureRoot)
                {
                    if (!seen.Add(current))
                        break;
                    result.Add(current);
                    current = current.parent;
                }
            }

            return result;
        }

        private static void PlaceCloneBone(
            Transform original,
            Transform cloned,
            Transform armatureRoot,
            Transform clonedArmature,
            Dictionary<Transform, Transform> boneMap,
            Dictionary<Transform, HumanBodyBones> humanoidBones)
        {
            var parent = original.parent;

            if (parent == armatureRoot)
            {
                cloned.SetParent(clonedArmature, false);
                TransformMath.CopyLocalPose(original, cloned);
                return;
            }

            // 中間ボーンも含めて直親が複製されていれば、ローカル姿勢をそのまま写す。
            if (parent != null && boneMap.TryGetValue(parent, out var clonedParentBone))
            {
                cloned.SetParent(clonedParentBone, false);
                TransformMath.CopyLocalPose(original, cloned);
                return;
            }

            var ancestor = NearestHumanoidAncestor(original, humanoidBones);
            if (ancestor != null && boneMap.TryGetValue(ancestor, out var clonedAncestor))
            {
                cloned.SetParent(clonedAncestor, false);
                TransformMath.CopyWorldPose(original, cloned);
            }
            else
            {
                cloned.SetParent(clonedArmature, false);
                TransformMath.CopyWorldPose(original, cloned);
            }
        }

        /// <summary>
        /// 追従 Parent Constraint はローカル空間で解決するため、
        /// 複製ボーンの親対応とローカル姿勢がオリジナルと一致している必要がある。
        /// </summary>
        private static void WarnIfCloneLocalPoseDiffers(
            ControllableHumanoid component,
            Dictionary<Transform, Transform> boneMap,
            Transform armatureRoot,
            Transform clonedArmature,
            Transform avatarRoot)
        {
            var mismatches = new List<string>();
            foreach (var kvp in boneMap)
            {
                var original = kvp.Key;
                var cloned = kvp.Value;
                if (original == null || cloned == null)
                    continue;

                Transform expectedParent = null;
                if (original.parent == armatureRoot)
                    expectedParent = clonedArmature;
                else if (original.parent != null)
                    boneMap.TryGetValue(original.parent, out expectedParent);

                if (cloned.parent == expectedParent && LocalPoseMatches(original, cloned))
                    continue;

                mismatches.Add(AnimationUtility.CalculateTransformPath(original, avatarRoot));
            }

            if (mismatches.Count == 0)
                return;

            SamirinBuildContext.ReportWarning(
                "[ControllableHumanoid] 複製ボーンの親子関係またはローカル座標がオリジナルと一致しません。" +
                "ローカル解決の追従がずれる可能性があります。\n" + string.Join("\n", mismatches),
                component);
        }

        private static bool LocalPoseMatches(Transform a, Transform b)
        {
            const float positionTolerance = 1e-5f;
            const float angleTolerance = 0.01f;
            return (a.localPosition - b.localPosition).sqrMagnitude <= positionTolerance * positionTolerance
                && Quaternion.Angle(a.localRotation, b.localRotation) <= angleTolerance
                && ScalesMatch(a.localScale, b.localScale);
        }

        private static bool IsCloneParentReady(
            Transform original,
            Transform armatureRoot,
            Dictionary<Transform, Transform> boneMap,
            HashSet<Transform> placed)
        {
            var parent = original.parent;
            while (parent != null && parent != armatureRoot)
            {
                if (boneMap.ContainsKey(parent))
                    return placed.Contains(parent);
                parent = parent.parent;
            }

            return true;
        }

        private static Transform NearestHumanoidAncestor(
            Transform bone,
            Dictionary<Transform, HumanBodyBones> humanoidBones)
        {
            var parent = bone.parent;
            while (parent != null)
            {
                if (humanoidBones.ContainsKey(parent))
                    return parent;
                parent = parent.parent;
            }

            return bone.parent;
        }

        private static bool IsUnder(Transform node, Transform ancestor)
        {
            while (node != null)
            {
                if (node == ancestor)
                    return true;
                node = node.parent;
            }

            return false;
        }

        /// <summary>
        /// 複製ボーンに Parent 追従する。
        /// </summary>
        private static Behaviour AddProxyFollowParentConstraint(GameObject proxy, Transform clonedBone)
        {
            // 制御用の ParentConstraint と共存するため常に新規追加
            var raw = proxy.AddComponent<VRCParentConstraint>();
            var wrapper = AllConstraint.FromComponent(raw) as VRCParentConstraintWrapper;
            if (wrapper == null)
                return null;

            wrapper.TargetTransform = null;
            wrapper.SolveInLocalSpace = true;
            wrapper.AddSource(clonedBone, 1f);
            wrapper.IsActive = true;
            wrapper.Locked = true;
            wrapper.ActivateConstraint();
            MoveComponentToTop(raw);
            return raw;
        }

        private static void SetConstraintActive(Behaviour constraint, bool active)
        {
            var wrapper = AllConstraint.FromComponent(constraint);
            if (wrapper == null)
                return;

            wrapper.IsActive = active;
        }

        private static void MoveComponentToTop(Component component)
        {
            if (component == null)
                return;

            // Transform の直後（コンポーネント一覧の最上位）まで上げる
            for (var i = 0; i < 64; i++)
            {
                if (!UnityEditorInternal.ComponentUtility.MoveComponentUp(component))
                    break;
            }
        }

        private static void PlacePlayerFollowObjects(
            ControllableHumanoid component,
            Dictionary<HumanBodyBones, Transform> originalByBone,
            Dictionary<Transform, Transform> boneMap)
        {
            if (component.playerFollowObjects == null)
                return;

            foreach (var entry in component.playerFollowObjects)
            {
                if (entry == null || entry.target == null)
                    continue;
                if (!ControllableHumanoid.BoneLinkEntry.IsBone(entry.bone))
                    continue;
                if (!originalByBone.TryGetValue(entry.bone, out var original) || original == null)
                    continue;
                if (!boneMap.TryGetValue(original, out var cloned) || cloned == null)
                    continue;
                if (entry.target == cloned || IsUnder(cloned, entry.target))
                    continue;

                var worldPosition = TransformMath.GetWorldPosition(entry.target);
                var worldRotation = TransformMath.GetWorldRotation(entry.target);
                var worldScale = TransformMath.GetWorldScale(entry.target);
                entry.target.SetParent(cloned, false);
                TransformMath.SetWorldPose(entry.target, worldPosition, worldRotation, worldScale);
            }
        }

        private static Behaviour AddHeadChop(Transform head, float globalScaleFactor)
        {
            var headChop = head.gameObject.GetComponent<VRCHeadChop>();
            if (headChop == null)
                headChop = head.gameObject.AddComponent<VRCHeadChop>();

            headChop.targetBones = new[]
            {
                new VRCHeadChop.HeadChopBone
                {
                    transform = head,
                    scaleFactor = 1f,
                },
            };
            headChop.globalScaleFactor = Mathf.Clamp01(globalScaleFactor);
            return headChop;
        }

        #endregion

        #region Shared / FX remap

        public static Dictionary<Transform, HumanBodyBones> CollectHumanoidBones(
            Animator animator,
            VRCAvatarDescriptor descriptor)
        {
            var result = new Dictionary<Transform, HumanBodyBones>();
            if (animator == null)
                return result;

            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bodyBone = (HumanBodyBones)i;
                Transform bone = animator.GetBoneTransform(bodyBone);
                if (bone == null)
                    continue;

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

                if (bone == null || result.ContainsKey(bone))
                    continue;

                result.Add(bone, bodyBone);
            }

            return result;
        }

        /// <summary>
        /// 分割後に追加された Animator（VRCFury 等）にある ControllableHumanoid 向けカーブを、
        /// 追従 Constraint / HeadChop のカーブへ展開する。
        /// 分割で移動したボーンのパスは Build 時点のスコープで書き換え済み。
        /// </summary>
        public static void RemapFxLayerPaths(ControllableHumanoid component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null)
                return;

            using (var animators = AnimatorServicesScope.Open(component))
            {
                if (animators == null)
                    return;

                ExpandConstraintToggleAnimations(component, animators.Services);
            }
        }

        /// <summary>
        /// 分割後に元ボーンと複製時の親の間へ Transform が挟まれていたら
        /// （VRCFury Cross Eye Fix の Eye.Up 等）、その下に複製時の親と同じ姿勢の空間を置き、
        /// ローカル解決の基準を複製側の親と揃え直す。
        /// </summary>
        public static void RestoreFollowSpaces(ControllableHumanoid component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null || component.BoneApplyConstraints == null)
                return;

            var targets = new List<ControllableHumanoid.BoneApplyConstraint>();
            foreach (var entry in component.BoneApplyConstraints)
            {
                if (entry.constraint == null || entry.parent == null || entry.source == null)
                    continue;

                var bone = entry.constraint.transform;
                if (bone.parent == entry.parent)
                    continue;

                if (!IsUnder(bone, entry.parent))
                {
                    SamirinBuildContext.ReportWarning(
                        $"[ControllableHumanoid] {bone.name} が複製時の親 {entry.parent.name} の外へ移動されたため、" +
                        "ローカル解決の追従を補正できません。",
                        component, bone);
                    continue;
                }

                targets.Add(entry);
            }

            if (targets.Count == 0)
                return;

            // 移動前に開くと、ここで挟む _FollowSpace による移動を ObjectPathRemapper が反映する。
            using (var animators = AnimatorServicesScope.Open(component))
            {
                if (animators == null)
                    return;

                var remapper = animators.Services.ObjectPathRemapper;
                var aliases = new Dictionary<string, string>();
                foreach (var entry in targets)
                {
                    var bone = entry.constraint.transform;
                    var movedPath = remapper.GetVirtualPathForObject(bone);

                    // 分割直後のパスを参照したまま残っているカーブも同じボーンへ向ける。
                    // そのパスに今は別オブジェクトがある場合、その参照を奪わないよう付け替えない。
                    if (!string.IsNullOrEmpty(entry.path)
                        && entry.path != movedPath
                        && remapper.GetObjectForPath(entry.path) == null)
                        aliases[entry.path] = movedPath;

                    var space = new GameObject(bone.name + "_FollowSpace").transform;
                    space.SetParent(bone.parent, false);
                    TransformMath.CopyWorldPose(entry.parent, space);
                    bone.SetParent(space, false);
                    // ビルド中に Constraint が評価済みだと現在のワールド姿勢はずれているので、追従元のローカル値を正とする。
                    TransformMath.CopyLocalPose(entry.source, bone);
                }

                if (aliases.Count > 0)
                    animators.Services.AnimationIndex.RewritePaths(aliases);
            }
        }

        private struct ConstraintToggleBinding
        {
            public string path;
            public Type type;
            public string propertyName;
        }

        private static readonly string[] ActivePropertyCandidates =
        {
            "IsActive",
            "m_IsActive",
            "_isActive",
            "m_Active",
        };

        private static void ExpandConstraintToggleAnimations(ControllableHumanoid component, AnimatorServicesContext services)
        {
            // 仮想コントローラ内のパスはスコープを開いた時点のもの。新しいカーブも同じ基準のパスで書く。
            var remapper = services.ObjectPathRemapper;
            Func<Transform, string> pathOf = remapper.GetVirtualPathForObject;

            var componentPath = pathOf(component.transform);
            var sourceBindings = BuildToggleBindings(component.SourceApplyConstraints, pathOf, "m_Enabled");
            var boneBindings = BuildBoneToggleBindings(component.BoneApplyConstraints, pathOf);
            var headChopBinding = BuildHeadChopBinding(component.BuiltHeadChop, pathOf);

            var clips = new List<VirtualClip>(services.AnimationIndex.GetClipsForObjectPath(componentPath));
            foreach (var clip in clips)
                ExpandClipToggles(clip, componentPath, sourceBindings, boneBindings, headChopBinding);
        }

        private static List<ConstraintToggleBinding> BuildToggleBindings(
            List<Behaviour> constraints,
            Func<Transform, string> pathOf)
        {
            return BuildToggleBindings(constraints, pathOf, null);
        }

        private static List<ConstraintToggleBinding> BuildToggleBindings(
            List<Behaviour> constraints,
            Func<Transform, string> pathOf,
            string propertyNameOverride)
        {
            var result = new List<ConstraintToggleBinding>();
            if (constraints == null)
                return result;

            var seen = new HashSet<string>();
            foreach (var constraint in constraints)
            {
                if (constraint == null)
                    continue;

                var path = pathOf(constraint.transform);
                var propertyName = string.IsNullOrEmpty(propertyNameOverride)
                    ? ResolveActivePropertyName(constraint)
                    : propertyNameOverride;
                var key = path + "\n" + constraint.GetType().FullName + "\n" + propertyName;
                if (!seen.Add(key))
                    continue;

                result.Add(new ConstraintToggleBinding
                {
                    path = path,
                    type = constraint.GetType(),
                    propertyName = propertyName,
                });
            }

            return result;
        }

        private static Dictionary<string, List<ConstraintToggleBinding>> BuildBoneToggleBindings(
            List<ControllableHumanoid.BoneApplyConstraint> constraints,
            Func<Transform, string> pathOf)
        {
            var result = new Dictionary<string, List<ConstraintToggleBinding>>();
            if (constraints == null)
                return result;

            foreach (var entry in constraints)
            {
                if (entry.constraint == null)
                    continue;

                var key = ControllableHumanoid.BoneApplyPrefix + entry.bone;
                result[key] = BuildToggleBindings(new List<Behaviour> { entry.constraint }, pathOf);
            }

            return result;
        }

        private static ConstraintToggleBinding? BuildHeadChopBinding(Behaviour headChop, Func<Transform, string> pathOf)
        {
            if (headChop == null)
                return null;

            return new ConstraintToggleBinding
            {
                path = pathOf(headChop.transform),
                type = typeof(VRCHeadChop),
                propertyName = "globalScaleFactor",
            };
        }

        private static string ResolveActivePropertyName(Behaviour constraint)
        {
            var serialized = new SerializedObject(constraint);
            foreach (var candidate in ActivePropertyCandidates)
            {
                var property = serialized.FindProperty(candidate);
                if (property != null && property.propertyType == SerializedPropertyType.Boolean)
                    return candidate;
            }

            return "IsActive";
        }

        private static void ExpandClipToggles(
            VirtualClip clip,
            string componentPath,
            List<ConstraintToggleBinding> sourceBindings,
            Dictionary<string, List<ConstraintToggleBinding>> boneBindings,
            ConstraintToggleBinding? headChopBinding)
        {
            if (clip == null || clip.IsMarkerClip)
                return;

            var bindings = new List<EditorCurveBinding>(clip.GetFloatCurveBindings());
            foreach (var binding in bindings)
            {
                if (binding.type != typeof(ControllableHumanoid))
                    continue;
                if ((binding.path ?? string.Empty) != componentPath)
                    continue;

                List<ConstraintToggleBinding> targets = null;
                ConstraintToggleBinding? singleTarget = null;
                if (binding.propertyName == ControllableHumanoid.SourceApplyEnabledProperty)
                    targets = sourceBindings;
                else if (binding.propertyName == ControllableHumanoid.HeadChopGlobalScaleProperty)
                    singleTarget = headChopBinding;
                else if (boneBindings == null
                    || !boneBindings.TryGetValue(binding.propertyName, out targets))
                    continue;

                var curve = clip.GetFloatCurve(binding);
                clip.SetFloatCurve(binding, null);

                if (curve == null)
                    continue;

                if (singleTarget.HasValue)
                {
                    var target = singleTarget.Value;
                    clip.SetFloatCurve(EditorCurveBinding.FloatCurve(target.path, target.type, target.propertyName), curve);
                    continue;
                }

                if (targets == null || targets.Count == 0)
                    continue;

                foreach (var target in targets)
                    clip.SetFloatCurve(EditorCurveBinding.FloatCurve(target.path, target.type, target.propertyName), curve);
            }
        }

        #endregion
    }
}
