using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Samirin33.NDMF.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// 指定したダミー Bool の遷移条件がマージ元とマージ先で違うとき、
    /// 統合される Animator をアバター側の true / false に揃える。
    /// </summary>
    internal static class FixDummyParameterProcessor
    {
        internal readonly struct DummyUsage
        {
            public enum Kind
            {
                Absent,
                NotBool,
                True,
                False,
                Mixed,
            }

            public readonly Kind Polarity;
            public readonly bool UsedDefault;

            public DummyUsage(Kind polarity, bool usedDefault)
            {
                Polarity = polarity;
                UsedDefault = usedDefault;
            }

            public bool IsDecisive => Polarity == Kind.True || Polarity == Kind.False;

            public static DummyUsage Absent => new DummyUsage(Kind.Absent, false);
            public static DummyUsage NotBool => new DummyUsage(Kind.NotBool, false);
            public static DummyUsage Mixed => new DummyUsage(Kind.Mixed, false);

            public static DummyUsage Condition(bool expectTrue)
            {
                return new DummyUsage(expectTrue ? Kind.True : Kind.False, false);
            }

            public static DummyUsage FromDefault(bool defaultBool)
            {
                return new DummyUsage(defaultBool ? Kind.True : Kind.False, true);
            }
        }

        public static void Execute(BuildContext context)
        {
            var avatarRoot = context.AvatarRootObject;
            if (avatarRoot == null)
                return;

            var components = avatarRoot.GetComponentsInChildren<FixDummyParameter>(true);
            if (components.Length == 0)
                return;

            var services = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();

            foreach (var component in components)
            {
                if (component == null)
                    continue;

                try
                {
                    Process(context, services, avatarRoot, component);
                }
                finally
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        internal static string Describe(DummyUsage usage)
        {
            switch (usage.Polarity)
            {
                case DummyUsage.Kind.Absent:
                    return "パラメーターなし";
                case DummyUsage.Kind.NotBool:
                    return "Bool ではない";
                case DummyUsage.Kind.Mixed:
                    return "true と false が混在";
                case DummyUsage.Kind.True:
                    return usage.UsedDefault ? "初期値 true" : "true で遷移";
                case DummyUsage.Kind.False:
                    return usage.UsedDefault ? "初期値 false" : "false で遷移";
                default:
                    return string.Empty;
            }
        }

        internal static AnimatorController UnwrapController(RuntimeAnimatorController runtime)
        {
            if (runtime is AnimatorController controller)
                return controller;
            if (runtime is AnimatorOverrideController ov)
                return UnwrapController(ov.runtimeAnimatorController);
            return null;
        }

        internal static DummyUsage GetUsage(AnimatorController controller, string parameterName)
        {
            if (controller == null || string.IsNullOrEmpty(parameterName))
                return DummyUsage.Absent;

            AnimatorControllerParameter parameter = null;
            foreach (var candidate in controller.parameters)
            {
                if (candidate != null && candidate.name == parameterName)
                {
                    parameter = candidate;
                    break;
                }
            }

            if (parameter == null)
                return DummyUsage.Absent;
            if (parameter.type != AnimatorControllerParameterType.Bool)
                return DummyUsage.NotBool;

            var sawTrue = false;
            var sawFalse = false;
            var visited = new HashSet<AnimatorStateMachine>();
            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine != null)
                    CollectModes(layer.stateMachine, parameterName, visited, ref sawTrue, ref sawFalse);
            }

            return ResolveUsage(parameter.defaultBool, sawTrue, sawFalse);
        }

        internal static IEnumerable<string> FindDummyNamedBools(RuntimeAnimatorController runtime)
        {
            var controller = UnwrapController(runtime);
            if (controller == null)
                yield break;

            foreach (var parameter in controller.parameters)
            {
                if (parameter == null || parameter.type != AnimatorControllerParameterType.Bool)
                    continue;
                if (string.IsNullOrEmpty(parameter.name))
                    continue;
                if (parameter.name.IndexOf("dummy", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                yield return parameter.name;
            }
        }

        private static void Process(
            BuildContext context,
            AnimatorServicesContext services,
            GameObject avatarRoot,
            FixDummyParameter component)
        {
            var names = CollectNames(component.dummyParameterNames);
            if (names.Count == 0)
            {
                Debug.LogWarning("[FixDummyParameter] ダミーパラメーター名が未設定のため、何もしません。", component);
                return;
            }

            var merges = component.GetComponents<ModularAvatarMergeAnimator>();
            if (merges == null || merges.Length == 0)
            {
                Debug.LogWarning("[FixDummyParameter] 同じオブジェクトに MA Merge Animator がありません。", component);
                return;
            }

            foreach (var merge in merges)
            {
                if (merge == null)
                    continue;
                if (merge.animator == null)
                {
                    Debug.LogWarning("[FixDummyParameter] MA Merge Animator に Animator が割り当てられていません。", merge);
                    continue;
                }

                if (merge.mergeAnimatorMode == MergeAnimatorMode.Replace)
                {
                    Debug.LogWarning(
                        "[FixDummyParameter] Replace の MA Merge Animator はマージ先を置き換えるため、ダミー条件は揃えません。",
                        merge);
                    continue;
                }

                if (!TryGetMergeController(context, services, merge, out var source))
                {
                    Debug.LogWarning("[FixDummyParameter] 統合される Animator を取得できませんでした。", merge);
                    continue;
                }

                if (!TryGetDestinationController(context, services, avatarRoot, merge, out var destination))
                {
                    Debug.LogWarning("[FixDummyParameter] マージ先の Animator を取得できませんでした。", merge);
                    continue;
                }

                if (ReferenceEquals(source, destination))
                    continue;

                foreach (var parameterName in names)
                    AlignToDestination(source, destination, parameterName, component);
            }
        }

        private static void AlignToDestination(
            VirtualAnimatorController source,
            VirtualAnimatorController destination,
            string parameterName,
            FixDummyParameter component)
        {
            var sourceUsage = GetUsage(source, parameterName);
            var destinationUsage = GetUsage(destination, parameterName);

            if (sourceUsage.Polarity == DummyUsage.Kind.Absent || destinationUsage.Polarity == DummyUsage.Kind.Absent)
                return;

            if (sourceUsage.Polarity == DummyUsage.Kind.NotBool || destinationUsage.Polarity == DummyUsage.Kind.NotBool)
            {
                Debug.LogWarning($"[FixDummyParameter] {parameterName} は Bool ではないためスキップしました。", component);
                return;
            }

            if (sourceUsage.Polarity == DummyUsage.Kind.Mixed || destinationUsage.Polarity == DummyUsage.Kind.Mixed)
            {
                Debug.LogWarning(
                    $"[FixDummyParameter] {parameterName} は true と false の両方が遷移条件にあるため、ダミー条件を揃えられません。",
                    component);
                return;
            }

            if (sourceUsage.Polarity == destinationUsage.Polarity)
                return;

            var expectTrue = destinationUsage.Polarity == DummyUsage.Kind.True;
            var changedConditions = RewriteConditions(source, parameterName, expectTrue);
            var changedDefault = RewriteDefault(source, parameterName, expectTrue);
            var changedCurves = RewriteCurves(source, parameterName, expectTrue);
            var changedDrivers = RewriteDrivers(source, parameterName, expectTrue);

            if (!changedConditions && !changedDefault && !changedCurves && !changedDrivers)
                return;

            var polarity = expectTrue ? "true" : "false";
            Debug.Log($"[FixDummyParameter] {parameterName} の使用条件をアバター側の {polarity} に揃えました。", component);
        }

        private static DummyUsage GetUsage(VirtualAnimatorController controller, string parameterName)
        {
            if (controller == null || string.IsNullOrEmpty(parameterName))
                return DummyUsage.Absent;
            if (!controller.Parameters.TryGetValue(parameterName, out var parameter) || parameter == null)
                return DummyUsage.Absent;
            if (parameter.type != AnimatorControllerParameterType.Bool)
                return DummyUsage.NotBool;

            var sawTrue = false;
            var sawFalse = false;
            foreach (var node in controller.AllReachableNodes())
            {
                if (node is not VirtualTransitionBase transition)
                    continue;

                foreach (var condition in transition.Conditions)
                {
                    if (!string.Equals(condition.parameter, parameterName, StringComparison.Ordinal))
                        continue;
                    if (condition.mode == AnimatorConditionMode.If)
                        sawTrue = true;
                    else if (condition.mode == AnimatorConditionMode.IfNot)
                        sawFalse = true;
                }
            }

            return ResolveUsage(parameter.defaultBool, sawTrue, sawFalse);
        }

        private static DummyUsage ResolveUsage(bool defaultBool, bool sawTrue, bool sawFalse)
        {
            if (sawTrue && sawFalse)
                return DummyUsage.Mixed;
            if (sawTrue)
                return DummyUsage.Condition(true);
            if (sawFalse)
                return DummyUsage.Condition(false);
            return DummyUsage.FromDefault(defaultBool);
        }

        private static bool RewriteConditions(VirtualAnimatorController controller, string parameterName, bool expectTrue)
        {
            var desired = expectTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
            var changed = false;

            foreach (var node in controller.AllReachableNodes())
            {
                if (node is not VirtualTransitionBase transition)
                    continue;

                var conditions = transition.Conditions;
                var updated = conditions;
                for (var i = 0; i < updated.Count; i++)
                {
                    var condition = updated[i];
                    if (!string.Equals(condition.parameter, parameterName, StringComparison.Ordinal))
                        continue;
                    if (condition.mode != AnimatorConditionMode.If && condition.mode != AnimatorConditionMode.IfNot)
                        continue;
                    if (condition.mode == desired)
                        continue;

                    condition.mode = desired;
                    updated = updated.SetItem(i, condition);
                    changed = true;
                }

                if (!ReferenceEquals(updated, conditions))
                    transition.Conditions = updated;
            }

            return changed;
        }

        private static bool RewriteDefault(VirtualAnimatorController controller, string parameterName, bool expectTrue)
        {
            if (!controller.Parameters.TryGetValue(parameterName, out var parameter) || parameter == null)
                return false;
            if (parameter.type != AnimatorControllerParameterType.Bool || parameter.defaultBool == expectTrue)
                return false;

            controller.SetParameter(parameterName, new AnimatorControllerParameter
            {
                name = parameterName,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = expectTrue,
                defaultFloat = parameter.defaultFloat,
                defaultInt = parameter.defaultInt,
            });
            return true;
        }

        private static bool RewriteCurves(VirtualAnimatorController controller, string parameterName, bool expectTrue)
        {
            var target = expectTrue ? 1f : 0f;
            var changed = false;

            foreach (var node in controller.AllReachableNodes())
            {
                if (node is not VirtualClip clip || clip.IsMarkerClip)
                    continue;

                foreach (var binding in clip.GetFloatCurveBindings())
                {
                    // 仮想化で相対パスが付いたあとも、Animator パラメーター曲線はプロパティ名で判別する。
                    if (binding.type != typeof(Animator))
                        continue;
                    if (!string.Equals(binding.propertyName, parameterName, StringComparison.Ordinal))
                        continue;

                    var curve = clip.GetFloatCurve(binding);
                    if (curve == null)
                        continue;

                    var keys = curve.keys;
                    var curveChanged = false;
                    for (var i = 0; i < keys.Length; i++)
                    {
                        if (Mathf.Approximately(keys[i].value, target))
                            continue;

                        var key = keys[i];
                        key.value = target;
                        keys[i] = key;
                        curveChanged = true;
                    }

                    if (!curveChanged)
                        continue;

                    curve.keys = keys;
                    clip.SetFloatCurve(binding, curve);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool RewriteDrivers(VirtualAnimatorController controller, string parameterName, bool expectTrue)
        {
            var target = expectTrue ? 1f : 0f;
            var changed = false;

            foreach (var node in controller.AllReachableNodes())
            {
                switch (node)
                {
                    case VirtualState state when RewriteDriverList(state.Behaviours, parameterName, target):
                        state.Behaviours = state.Behaviours;
                        changed = true;
                        break;
                    case VirtualStateMachine stateMachine when RewriteDriverList(stateMachine.Behaviours, parameterName, target):
                        stateMachine.Behaviours = stateMachine.Behaviours;
                        changed = true;
                        break;
                    case VirtualLayer layer:
                        var layerChanged = false;
                        foreach (var behaviours in layer.SyncedLayerBehaviourOverrides.Values)
                        {
                            if (RewriteDriverList(behaviours, parameterName, target))
                                layerChanged = true;
                        }

                        if (layerChanged)
                        {
                            layer.SyncedLayerBehaviourOverrides = layer.SyncedLayerBehaviourOverrides;
                            changed = true;
                        }
                        break;
                }
            }

            return changed;
        }

        private static bool RewriteDriverList(
            IEnumerable<StateMachineBehaviour> behaviours,
            string parameterName,
            float target)
        {
            if (behaviours == null)
                return false;

            var changed = false;
            foreach (var behaviour in behaviours)
            {
                if (behaviour is not VRCAvatarParameterDriver)
                    continue;

                var serialized = new SerializedObject(behaviour);
                var parameters = serialized.FindProperty("parameters");
                if (parameters == null)
                    continue;

                var entryChanged = false;
                for (var i = 0; i < parameters.arraySize; i++)
                {
                    var entry = parameters.GetArrayElementAtIndex(i);
                    var nameProp = entry.FindPropertyRelative("name");
                    if (nameProp == null || !string.Equals(nameProp.stringValue, parameterName, StringComparison.Ordinal))
                        continue;

                    // VRCAvatarParameterDriver.ChangeType.Set == 0
                    var typeProp = entry.FindPropertyRelative("type");
                    if (ReadDriverType(typeProp) != 0)
                    {
                        Debug.LogWarning(
                            $"[FixDummyParameter] {parameterName} を Set 以外の Parameter Driver で書いているため、その書き込みは変えません。",
                            behaviour);
                        continue;
                    }

                    var valueProp = entry.FindPropertyRelative("value");
                    if (valueProp == null || Mathf.Approximately(valueProp.floatValue, target))
                        continue;

                    valueProp.floatValue = target;
                    entryChanged = true;
                }

                if (!entryChanged)
                    continue;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            return changed;
        }

        private static int ReadDriverType(SerializedProperty typeProp)
        {
            if (typeProp == null)
                return 0;
            if (typeProp.propertyType == SerializedPropertyType.Enum)
                return typeProp.enumValueIndex;
            return typeProp.intValue;
        }

        private static bool TryGetDestinationController(
            BuildContext context,
            AnimatorServicesContext services,
            GameObject avatarRoot,
            ModularAvatarMergeAnimator merge,
            out VirtualAnimatorController destination)
        {
            destination = null;
            var controllers = services.ControllerContext.Controllers;

            foreach (var candidate in avatarRoot.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (candidate == null || candidate == merge)
                    continue;
                if (candidate.layerType != merge.layerType)
                    continue;
                if (candidate.mergeAnimatorMode != MergeAnimatorMode.Replace || candidate.animator == null)
                    continue;

                return TryGetMergeController(context, services, candidate, out destination);
            }

            return controllers.TryGetValue(merge.layerType, out destination) && destination != null;
        }

        private static bool TryGetMergeController(
            BuildContext context,
            AnimatorServicesContext services,
            ModularAvatarMergeAnimator merge,
            out VirtualAnimatorController controller)
        {
            controller = null;
            if (merge == null)
                return false;

            var controllers = services.ControllerContext.Controllers;
            if (controllers.TryGetValue(merge, out controller) && controller != null)
                return true;
            if (merge.animator == null)
                return false;

            var virtualizer = (IVirtualizeAnimatorController)merge;
            var basePath = virtualizer.GetMotionBasePath(context, false);
            controller = services.ControllerContext.CloneContext.CloneDistinct(
                merge.animator,
                virtualizer.TargetControllerKey);
            if (controller == null)
                return false;

            controllers[merge] = controller;
            if (!string.IsNullOrEmpty(basePath))
                new AnimationIndex(new[] { controller }).ApplyPathPrefix(basePath);

            // 次に仮想化するとき、付与済みのパスへもう一度前置しない。
            virtualizer.GetMotionBasePath(context, true);
            return true;
        }

        private static List<string> CollectNames(string[] names)
        {
            var list = new List<string>();
            if (names == null)
                return list;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                var trimmed = name.Trim();
                if (seen.Add(trimmed))
                    list.Add(trimmed);
            }

            return list;
        }

        private static void CollectModes(
            AnimatorStateMachine stateMachine,
            string parameterName,
            HashSet<AnimatorStateMachine> visited,
            ref bool sawTrue,
            ref bool sawFalse)
        {
            if (stateMachine == null || !visited.Add(stateMachine))
                return;

            Note(stateMachine.anyStateTransitions, parameterName, ref sawTrue, ref sawFalse);
            Note(stateMachine.entryTransitions, parameterName, ref sawTrue, ref sawFalse);

            foreach (var child in stateMachine.states)
            {
                if (child.state != null)
                    Note(child.state.transitions, parameterName, ref sawTrue, ref sawFalse);
            }

            foreach (var child in stateMachine.stateMachines)
            {
                if (child.stateMachine == null)
                    continue;

                Note(stateMachine.GetStateMachineTransitions(child.stateMachine), parameterName, ref sawTrue, ref sawFalse);
                CollectModes(child.stateMachine, parameterName, visited, ref sawTrue, ref sawFalse);
            }
        }

        private static void Note(
            IEnumerable<AnimatorTransitionBase> transitions,
            string parameterName,
            ref bool sawTrue,
            ref bool sawFalse)
        {
            if (transitions == null)
                return;

            foreach (var transition in transitions)
            {
                if (transition == null)
                    continue;

                foreach (var condition in transition.conditions)
                {
                    if (!string.Equals(condition.parameter, parameterName, StringComparison.Ordinal))
                        continue;
                    if (condition.mode == AnimatorConditionMode.If)
                        sawTrue = true;
                    else if (condition.mode == AnimatorConditionMode.IfNot)
                        sawFalse = true;
                }
            }
        }
    }
}
