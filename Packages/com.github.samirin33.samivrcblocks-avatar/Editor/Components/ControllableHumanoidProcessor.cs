using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
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
        }

        #region Build

        public static void Build(ControllableHumanoid component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null)
                return;

            if (!avatarRootObject.TryGetComponent<Animator>(out var animator) || !animator.isHuman)
            {
                Debug.LogWarning(
                    "[ControllableHumanoid] Humanoid Animator が見つからないためスキップします。",
                    component);
                return;
            }

            if (!avatarRootObject.TryGetComponent<VRCAvatarDescriptor>(out var descriptor))
            {
                Debug.LogWarning(
                    "[ControllableHumanoid] VRCAvatarDescriptor が見つからないためスキップします。",
                    component);
                return;
            }

            var hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsBone == null)
            {
                Debug.LogWarning("[ControllableHumanoid] Hips ボーンが見つかりません。", component);
                return;
            }

            // ルート（Armature）は Hips 移動前に取得
            var armatureRoot = hipsBone.parent;
            if (armatureRoot == null)
            {
                Debug.LogWarning("[ControllableHumanoid] Armature ルート（Hips の親）が見つかりません。", component);
                return;
            }

            var humanoidBones = CollectHumanoidBones(animator, descriptor);
            if (humanoidBones.Count == 0)
            {
                Debug.LogWarning("[ControllableHumanoid] ヒューマノイドボーンが空です。", component);
                return;
            }

            var avatarRoot = avatarRootObject.transform;
            var oldPaths = new Dictionary<Transform, string>();
            TrackHierarchy(armatureRoot, avatarRoot, oldPaths);
            if (component.links != null)
            {
                foreach (var entry in component.links)
                {
                    if (entry?.parentChild != null)
                        TrackHierarchy(entry.parentChild, avatarRoot, oldPaths);
                }
            }

            if (component.playerFollowObjects != null)
            {
                foreach (var entry in component.playerFollowObjects)
                {
                    if (entry?.target != null)
                        TrackHierarchy(entry.target, avatarRoot, oldPaths);
                }
            }

            var clonedArmature = new GameObject(armatureRoot.name).transform;
            clonedArmature.SetParent(armatureRoot.parent, false);
            TransformMath.CopyLocalPose(armatureRoot, clonedArmature);

            var boneMap = new Dictionary<Transform, Transform>(humanoidBones.Count);
            foreach (var kvp in humanoidBones)
            {
                var original = kvp.Key;
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
                });
            }

            component.BuiltHeadChop = null;
            if (component.addHeadChop
                && originalByBone.TryGetValue(HumanBodyBones.Head, out var head)
                && head != null)
                component.BuiltHeadChop = AddHeadChop(head, component.headChopGlobalScaleFactor);

            PlacePlayerFollowObjects(component, originalByBone, boneMap);

            var remaps = new List<ControllableHumanoid.PathRemapEntry>(oldPaths.Count);
            foreach (var kvp in oldPaths)
            {
                if (kvp.Key == null)
                    continue;

                var newPath = AnimationUtility.CalculateTransformPath(kvp.Key, avatarRoot);
                if (string.IsNullOrEmpty(kvp.Value) || kvp.Value == newPath)
                    continue;

                remaps.Add(new ControllableHumanoid.PathRemapEntry
                {
                    oldPath = kvp.Value,
                    newPath = newPath,
                });
            }

            remaps.Sort((a, b) => b.oldPath.Length.CompareTo(a.oldPath.Length));
            component.PendingPathRemaps = remaps;
            component.SourceApplyConstraints = sourceConstraints;
            component.BoneApplyConstraints = boneApplyConstraints;

            RemapFxLayerPaths(component, avatarRootObject);
        }

        private static void TrackHierarchy(Transform root, Transform avatarRoot, Dictionary<Transform, string> paths)
        {
            if (root == null || root == avatarRoot || paths.ContainsKey(root))
                return;

            paths[root] = AnimationUtility.CalculateTransformPath(root, avatarRoot);
            for (var i = 0; i < root.childCount; i++)
                TrackHierarchy(root.GetChild(i), avatarRoot, paths);
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
            // 元ボーンの子としてローカル原点に置けば、ボーンと同じ場所になる。
            parentChild.SetParent(originalParent, false);
            parentChild.localPosition = Vector3.zero;
            parentChild.localRotation = Quaternion.identity;
            parentChild.localScale = Vector3.one;

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

            if (keepLocal)
            {
                // ローカル値のコピーだと、Armature スケール 100 のモデルでは
                // Neck→Head のような小さいローカル座標がスケール 1 の親の上で原点に潰れる。
                var position = TransformMath.GetWorldPosition(bone);
                var rotation = TransformMath.GetWorldRotation(bone);
                var scale = TransformMath.GetWorldScale(bone);
                bone.SetParent(parent, false);
                TransformMath.SetWorldPose(bone, position, rotation, scale);
                return;
            }

            var localPosition = bone.localPosition;
            var localRotation = bone.localRotation;
            var localScale = bone.localScale;

            var cancel = new GameObject(bone.name + "_LocalCancel").transform;
            cancel.SetParent(parent, false);
            SetInverseLocal(cancel, localPosition, localRotation, localScale);
            bone.SetParent(cancel, false);
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

        public static void RemapFxLayerPaths(ControllableHumanoid component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null)
                return;

            if (component.PendingPathRemaps != null && component.PendingPathRemaps.Count > 0)
            {
                var controllers = VRCAvatarDescriptorControllerUtility.GetControllers(
                    avatarRootObject,
                    VRCAvatarDescriptor.AnimLayerType.Base,
                    VRCAvatarDescriptor.AnimLayerType.Additive,
                    VRCAvatarDescriptor.AnimLayerType.Gesture,
                    VRCAvatarDescriptor.AnimLayerType.Action,
                    VRCAvatarDescriptor.AnimLayerType.FX);
                foreach (var controller in controllers)
                    RemapControllerPaths(controller, component.PendingPathRemaps);
            }

            ExpandConstraintToggleAnimations(component, avatarRootObject);
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

        private static void ExpandConstraintToggleAnimations(ControllableHumanoid component, GameObject avatarRootObject)
        {
            var avatarRoot = avatarRootObject.transform;
            var componentPath = AnimationUtility.CalculateTransformPath(component.transform, avatarRoot);
            var sourceBindings = BuildToggleBindings(component.SourceApplyConstraints, avatarRoot, "m_Enabled");
            var boneBindings = BuildBoneToggleBindings(component.BoneApplyConstraints, avatarRoot);
            var headChopBinding = BuildHeadChopBinding(component.BuiltHeadChop, avatarRoot);

            var controllers = VRCAvatarDescriptorControllerUtility.GetControllers(
                avatarRootObject,
                VRCAvatarDescriptor.AnimLayerType.Base,
                VRCAvatarDescriptor.AnimLayerType.Additive,
                VRCAvatarDescriptor.AnimLayerType.Gesture,
                VRCAvatarDescriptor.AnimLayerType.Action,
                VRCAvatarDescriptor.AnimLayerType.FX);

            var remaps = component.PendingPathRemaps;
            foreach (var controller in controllers)
            {
                foreach (var clip in CollectReferencedClips(controller))
                    ExpandClipToggles(clip, componentPath, remaps, sourceBindings, boneBindings, headChopBinding);
            }
        }

        private static List<ConstraintToggleBinding> BuildToggleBindings(List<Behaviour> constraints, Transform avatarRoot)
        {
            return BuildToggleBindings(constraints, avatarRoot, null);
        }

        private static List<ConstraintToggleBinding> BuildToggleBindings(
            List<Behaviour> constraints,
            Transform avatarRoot,
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

                var path = AnimationUtility.CalculateTransformPath(constraint.transform, avatarRoot);
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
            Transform avatarRoot)
        {
            var result = new Dictionary<string, List<ConstraintToggleBinding>>();
            if (constraints == null)
                return result;

            foreach (var entry in constraints)
            {
                if (entry.constraint == null)
                    continue;

                var key = ControllableHumanoid.BoneApplyPrefix + entry.bone;
                result[key] = BuildToggleBindings(new List<Behaviour> { entry.constraint }, avatarRoot);
            }

            return result;
        }

        private static ConstraintToggleBinding? BuildHeadChopBinding(Behaviour headChop, Transform avatarRoot)
        {
            if (headChop == null)
                return null;

            return new ConstraintToggleBinding
            {
                path = AnimationUtility.CalculateTransformPath(headChop.transform, avatarRoot),
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
            AnimationClip clip,
            string componentPath,
            List<ControllableHumanoid.PathRemapEntry> remaps,
            List<ConstraintToggleBinding> sourceBindings,
            Dictionary<string, List<ConstraintToggleBinding>> boneBindings,
            ConstraintToggleBinding? headChopBinding)
        {
            if (clip == null)
                return;

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var changed = false;
            foreach (var binding in bindings)
            {
                if (binding.type != typeof(ControllableHumanoid))
                    continue;

                var path = binding.path ?? string.Empty;
                var remappedPath = RemapPath(path, remaps);
                if (path != componentPath && remappedPath != componentPath)
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

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                AnimationUtility.SetEditorCurve(clip, binding, null);
                changed = true;

                if (curve == null)
                    continue;

                if (singleTarget.HasValue)
                {
                    var target = singleTarget.Value;
                    var updated = EditorCurveBinding.FloatCurve(target.path, target.type, target.propertyName);
                    AnimationUtility.SetEditorCurve(clip, updated, curve);
                    continue;
                }

                if (targets == null || targets.Count == 0)
                    continue;

                foreach (var target in targets)
                {
                    var updated = EditorCurveBinding.FloatCurve(target.path, target.type, target.propertyName);
                    AnimationUtility.SetEditorCurve(clip, updated, curve);
                }
            }

            if (changed)
                EditorUtility.SetDirty(clip);
        }

        private static void RemapControllerPaths(
            AnimatorController controller,
            List<ControllableHumanoid.PathRemapEntry> remaps)
        {
            if (controller == null || remaps == null || remaps.Count == 0)
                return;

            var clips = CollectReferencedClips(controller);
            foreach (var clip in clips)
                RemapClipPaths(clip, remaps);
        }

        private static HashSet<AnimationClip> CollectReferencedClips(AnimatorController controller)
        {
            var set = new HashSet<AnimationClip>();
            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine != null)
                    CollectClipsFromStateMachine(layer.stateMachine, set);
            }

            return set;
        }

        private static void CollectClipsFromStateMachine(AnimatorStateMachine stateMachine, HashSet<AnimationClip> set)
        {
            if (stateMachine == null)
                return;

            foreach (var child in stateMachine.states)
            {
                if (child.state == null)
                    continue;
                CollectClipsFromMotion(child.state.motion, set);
            }

            foreach (var childSm in stateMachine.stateMachines)
                CollectClipsFromStateMachine(childSm.stateMachine, set);
        }

        private static void CollectClipsFromMotion(Motion motion, HashSet<AnimationClip> set)
        {
            if (motion == null)
                return;

            if (motion is AnimationClip clip)
            {
                set.Add(clip);
                return;
            }

            if (motion is BlendTree tree)
            {
                foreach (var child in tree.children)
                    CollectClipsFromMotion(child.motion, set);
            }
        }

        private static void RemapClipPaths(AnimationClip clip, List<ControllableHumanoid.PathRemapEntry> remaps)
        {
            if (clip == null)
                return;

            var floatBindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in floatBindings)
            {
                var newPath = RemapPath(binding.path, remaps);
                if (newPath == binding.path)
                    continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                AnimationUtility.SetEditorCurve(clip, binding, null);
                var updated = binding;
                updated.path = newPath;
                AnimationUtility.SetEditorCurve(clip, updated, curve);
            }

            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (var binding in objectBindings)
            {
                var newPath = RemapPath(binding.path, remaps);
                if (newPath == binding.path)
                    continue;

                var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                var updated = binding;
                updated.path = newPath;
                AnimationUtility.SetObjectReferenceCurve(clip, updated, curve);
            }

            EditorUtility.SetDirty(clip);
        }

        private static string RemapPath(string path, List<ControllableHumanoid.PathRemapEntry> remaps)
        {
            if (string.IsNullOrEmpty(path) || remaps == null)
                return path;

            foreach (var entry in remaps)
            {
                if (string.IsNullOrEmpty(entry.oldPath))
                    continue;

                if (path == entry.oldPath)
                    return entry.newPath;

                if (path.StartsWith(entry.oldPath + "/", StringComparison.Ordinal))
                    return entry.newPath + path.Substring(entry.oldPath.Length);
            }

            return path;
        }

        #endregion
    }
}
