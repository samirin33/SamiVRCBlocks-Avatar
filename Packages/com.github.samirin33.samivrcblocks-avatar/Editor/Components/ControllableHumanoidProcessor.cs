using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using nadena.dev.ndmf.runtime;
using Samirin33.NDMF.Components;
using Samirin33.NDMF.Constraints;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// ControllableHumanoid の Proxy 構造生成とビルド時処理。
    /// </summary>
    [InitializeOnLoad]
    internal static class ControllableHumanoidProcessor
    {
        static ControllableHumanoidProcessor()
        {
            ControllableHumanoid.BuildHandler = Build;
            ControllableHumanoid.RemapFxHandler = RemapFxLayerPaths;
        }

        #region Proxy structure (Editor)

        public static void CreateHumanoidProxyStructure(ControllableHumanoid component)
        {
            if (component == null)
                return;

            var avatarRoot = RuntimeUtil.FindAvatarInParents(component.transform);
            if (avatarRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "ControllableHumanoid",
                    "アバタールートが見つかりません。アバター配下に配置してください。",
                    "OK");
                return;
            }

            if (!avatarRoot.TryGetComponent<Animator>(out var animator) || !animator.isHuman)
            {
                EditorUtility.DisplayDialog(
                    "ControllableHumanoid",
                    "Humanoid Animator が見つかりません。",
                    "OK");
                return;
            }

            avatarRoot.TryGetComponent<VRCAvatarDescriptor>(out var descriptor);
            var humanoidBones = CollectHumanoidBones(animator, descriptor);
            if (humanoidBones.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "ControllableHumanoid",
                    "ヒューマノイドボーンが空です。",
                    "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(component.gameObject, "Create Humanoid Proxy Structure");

            ClearExistingProxyChildren(component);

            var proxyByOriginal = new Dictionary<Transform, Transform>(humanoidBones.Count);
            var entries = new List<ControllableHumanoid.BoneControlEntry>(humanoidBones.Count);

            foreach (var kvp in humanoidBones)
            {
                var original = kvp.Key;
                var bodyBone = kvp.Value;
                var proxyGo = new GameObject(ControllableHumanoid.GetProxyObjectName(bodyBone));
                Undo.RegisterCreatedObjectUndo(proxyGo, "Create Proxy");

                var proxy = proxyGo.transform;
                proxy.SetParent(component.transform, false);
                proxy.SetPositionAndRotation(original.position, original.rotation);
                proxy.localScale = Vector3.one;

                proxyByOriginal[original] = proxy;
                entries.Add(new ControllableHumanoid.BoneControlEntry
                {
                    bone = bodyBone,
                    source = proxy,
                    target = proxy,
                });
            }

            foreach (var kvp in humanoidBones)
            {
                var original = kvp.Key;
                var proxy = proxyByOriginal[original];
                var humanoidParent = FindHumanoidParent(original, humanoidBones);

                if (humanoidParent != null && proxyByOriginal.TryGetValue(humanoidParent, out var parentProxy))
                    proxy.SetParent(parentProxy, true);
                else
                    proxy.SetParent(component.transform, true);
            }

            Undo.RecordObject(component, "Assign Bone Controls");
            component.boneControls = entries;
            EditorUtility.SetDirty(component);
        }

        private static void ClearExistingProxyChildren(ControllableHumanoid component)
        {
            var toDestroy = new List<GameObject>();
            for (var i = 0; i < component.transform.childCount; i++)
            {
                var child = component.transform.GetChild(i);
                if (child.name.StartsWith(ControllableHumanoid.ProxyNamePrefix, StringComparison.Ordinal))
                    toDestroy.Add(child.gameObject);
            }

            foreach (var go in toDestroy)
            {
                if (go != null)
                    Undo.DestroyObjectImmediate(go);
            }
        }

        private static Transform FindHumanoidParent(
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

            return null;
        }

        #endregion

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

            var controlByBone = BuildControlLookup(component);
            if (controlByBone.Count == 0)
            {
                Debug.LogWarning(
                    "[ControllableHumanoid] Source/Target が未設定です。「ヒューマノイド構造を追加」を実行してください。",
                    component);
                return;
            }

            var avatarRoot = avatarRootObject.transform;
            var oldPaths = new Dictionary<Transform, string>(humanoidBones.Count);
            var oldLocalScales = new Dictionary<Transform, Vector3>(humanoidBones.Count);
            foreach (var bone in humanoidBones.Keys)
            {
                oldPaths[bone] = AnimationUtility.CalculateTransformPath(bone, avatarRoot);
                oldLocalScales[bone] = bone.localScale;
            }

            // ヒューマノイドボーンを複製（元の名前のまま Armature 配下へ）
            var boneMap = new Dictionary<Transform, Transform>(humanoidBones.Count);
            foreach (var kvp in humanoidBones)
            {
                var original = kvp.Key;
                var clonedGo = new GameObject(original.name);
                var cloned = clonedGo.transform;
                cloned.SetPositionAndRotation(original.position, original.rotation);
                boneMap[original] = cloned;
            }

            foreach (var kvp in boneMap)
            {
                var original = kvp.Key;
                var cloned = kvp.Value;
                var parent = original.parent;

                if (parent != null && boneMap.TryGetValue(parent, out var clonedParent))
                    cloned.SetParent(clonedParent, true);
                else
                    cloned.SetParent(armatureRoot, true);

                if (oldLocalScales.TryGetValue(original, out var localScale))
                    cloned.localScale = localScale;
            }

            // アバター直下に OriginalBone を生成し、オリジナル Hips をその子へ
            var originalBoneRoot = new GameObject(ControllableHumanoid.OriginalBoneRootName).transform;
            originalBoneRoot.SetParent(avatarRoot, false);
            originalBoneRoot.localPosition = Vector3.zero;
            originalBoneRoot.localRotation = Quaternion.identity;
            originalBoneRoot.localScale = Vector3.one;
            hipsBone.SetParent(originalBoneRoot, true);

            // コンポーネント自身 → Armature ルートへ Parent 追従（Proxy_Hips と同様 / SolveInLocalSpace・最上位）
            AddProxyFollowParentConstraint(component.gameObject, armatureRoot);

            foreach (var kvp in humanoidBones)
            {
                var original = kvp.Key;
                var bodyBone = kvp.Value;
                var cloned = boneMap[original];

                original.name = bodyBone.ToString();

                if (!controlByBone.TryGetValue(bodyBone, out var entry) || entry == null)
                {
                    Debug.LogWarning(
                        $"[ControllableHumanoid] {bodyBone} の Source/Target が未設定のため Constraint をスキップします。",
                        component);
                    continue;
                }

                var source = entry.source;
                var target = entry.ResolvedTarget;

                // 複製ヒューマノイド → 制御用: Source に Constraint（Hips は Parent、他は Rotation）
                if (source != null)
                {
                    if (bodyBone == HumanBodyBones.Hips)
                        AddProxyFollowParentConstraint(source.gameObject, cloned);
                    else
                        AddProxyFollowRotationConstraint(source.gameObject, cloned);
                }

                // 制御 → オリジナル: Target に Parent / Scale（TargetTransform = オリジナル、Source = Target 自身）
                if (target != null)
                {
                    AddVrcParentConstraint(target, original);
                    AddVrcScaleConstraint(target, original);
                }

                if (bodyBone == HumanBodyBones.Head && component.addHeadChop)
                    AddHeadChop(original);
            }

            var remaps = new List<ControllableHumanoid.PathRemapEntry>(humanoidBones.Count);
            foreach (var kvp in oldPaths)
            {
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

            RemapFxLayerPaths(component, avatarRootObject);
        }

        private static Dictionary<HumanBodyBones, ControllableHumanoid.BoneControlEntry> BuildControlLookup(
            ControllableHumanoid component)
        {
            var result = new Dictionary<HumanBodyBones, ControllableHumanoid.BoneControlEntry>();
            if (component.boneControls == null)
                return result;

            foreach (var entry in component.boneControls)
            {
                if (entry == null || entry.source == null)
                    continue;
                result[entry.bone] = entry;
            }

            return result;
        }

        private static void AddVrcParentConstraint(Transform host, Transform original)
        {
            // Source 側の追従用 ParentConstraint と共存するため常に新規追加
            var raw = host.gameObject.AddComponent<VRCParentConstraint>();
            var wrapper = AllConstraint.FromComponent(raw) as VRCParentConstraintWrapper;
            if (wrapper == null)
                return;

            wrapper.TargetTransform = original;
            wrapper.AddSource(host, 1f);
            wrapper.IsActive = true;
            wrapper.Locked = true;
            wrapper.ActivateConstraint();
        }

        private static void AddVrcScaleConstraint(Transform host, Transform original)
        {
            var raw = host.gameObject.AddComponent<VRCScaleConstraint>();
            var wrapper = AllConstraint.FromComponent(raw) as VRCScaleConstraintWrapper;
            if (wrapper == null)
                return;

            wrapper.TargetTransform = original;
            wrapper.AddSource(host, 1f);
            wrapper.IsActive = true;
            wrapper.Locked = true;
            wrapper.ActivateConstraint();
        }

        /// <summary>
        /// Proxy が複製ボーンの回転に追従（SolveInLocalSpace、コンポーネント最上位）。
        /// </summary>
        private static void AddProxyFollowRotationConstraint(GameObject proxy, Transform clonedBone)
        {
            // 制御用の Constraint と共存するため常に新規追加
            var raw = proxy.AddComponent<VRCRotationConstraint>();
            var wrapper = AllConstraint.FromComponent(raw) as VRCRotationConstraintWrapper;
            if (wrapper == null)
                return;

            wrapper.TargetTransform = null;
            wrapper.SolveInLocalSpace = true;
            wrapper.AddSource(clonedBone, 1f);
            wrapper.IsActive = true;
            wrapper.Locked = true;
            wrapper.ActivateConstraint();
            MoveComponentToTop(raw);
        }

        /// <summary>
        /// Proxy_Hips が複製 Hips に Parent 追従（SolveInLocalSpace、コンポーネント最上位）。
        /// </summary>
        private static void AddProxyFollowParentConstraint(GameObject proxy, Transform clonedBone)
        {
            // 制御用の ParentConstraint と共存するため常に新規追加
            var raw = proxy.AddComponent<VRCParentConstraint>();
            var wrapper = AllConstraint.FromComponent(raw) as VRCParentConstraintWrapper;
            if (wrapper == null)
                return;

            wrapper.TargetTransform = null;
            wrapper.SolveInLocalSpace = true;
            wrapper.AddSource(clonedBone, 1f);
            wrapper.IsActive = true;
            wrapper.Locked = true;
            wrapper.ActivateConstraint();
            MoveComponentToTop(raw);
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

        private static void AddHeadChop(Transform head)
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
            headChop.globalScaleFactor = 0f;
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
            if (component?.PendingPathRemaps == null || component.PendingPathRemaps.Count == 0)
                return;

            var fx = VRCAvatarDescriptorControllerUtility.GetController(
                avatarRootObject,
                VRCAvatarDescriptor.AnimLayerType.FX);
            if (fx == null)
                return;

            RemapControllerPaths(fx, component.PendingPathRemaps);
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
