using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Samirin33.NDMF.Base.Plugin;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    public static class ObserveParameterValueChangeBuilder
    {
        [InitializeOnLoadMethod]
        private static void RegisterBuilder()
        {
            SamirinMABaseSingleBuildRegistry.Register<ObserveParameterValueChange>(Build);
        }

        private const string EmptyMotionGUID = "4de039275b65be24c8f0a641d7a44924";
        private const string DummyParameterName = "ObserveValueChange/Dummy";
        private const string ReadyParameterName = "ObserveValueChange/Ready";
        private const string QuantizeLayerName = "Quantize";
        private static string GeneratedFolder => "Assets/Generated/SamiVRCBlocks/ObserveParameterValueChange";

        public static void Build(GameObject avatarRootObject, params ObserveParameterValueChange[] components)
        {
            BuildInternal(avatarRootObject, components);
        }

        public static AnimatorController[] BuildManual(GameObject avatarRootObject, params ObserveParameterValueChange[] components)
        {
            return BuildInternal(avatarRootObject, components);
        }

        private static AnimatorController[] BuildInternal(GameObject avatarRootObject, params ObserveParameterValueChange[] components)
        {
            if (components == null || components.Length == 0)
                return Array.Empty<AnimatorController>();

            var (settings, writeDefault, matchAvatarWriteDefaults) = MergeSettings(components);
            if (settings.Count == 0)
                return Array.Empty<AnimatorController>();

            var controller = CreateController(settings.ToArray(), writeDefault);
            if (controller == null)
                return Array.Empty<AnimatorController>();

            var moduleParent = components.FirstOrDefault(c => c != null)?.gameObject ?? avatarRootObject;
            ModularAvatarMergeAnimatorUtility.RegisterMergeAnimatorModule(
                moduleParent,
                "ObserveParameterValueChange_Module",
                controller,
                layerPriority: 10,
                matchAvatarWriteDefaults: matchAvatarWriteDefaults);

            return new[] { controller };
        }

        private static (List<ObserveParameterValueChange.ObserveSetting> settings, bool writeDefault, bool matchAvatarWriteDefaults) MergeSettings(
            ObserveParameterValueChange[] components)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var merged = new List<ObserveParameterValueChange.ObserveSetting>();
            var writeDefault = components.Length > 0 && components[0] != null && components[0].writeDefault;
            var matchAvatarWriteDefaults = false;

            foreach (var component in components)
            {
                if (component == null)
                    continue;
                if (component.writeDefault)
                    writeDefault = true;
                if (component.matchAvatarWriteDefaults)
                    matchAvatarWriteDefaults = true;
                if (component.observeSettings == null)
                    continue;

                foreach (var setting in component.observeSettings)
                {
                    if (setting == null)
                        continue;

                    var paramName = ObserveParameterValueChange.GetParamName(setting);
                    if (!seen.Add(paramName))
                        continue;

                    if (setting.paramType != ObserveParameterValueChange.ParamType.Bool
                        && !ObserveParameterValueChange.TryGetQuantizeMapping(setting, out _))
                    {
                        Debug.LogWarning($"[ObserveParameterValueChange] {paramName} は監視段が1つのためスキップしました。間隔を範囲より小さくしてください。");
                        continue;
                    }

                    if (ObserveParameterValueChange.TryGetBucketInfo(setting, out var info) && info.thresholdClamped)
                    {
                        Debug.LogWarning(
                            $"[ObserveParameterValueChange] {paramName} の段数が {ObserveParameterValueChange.MaxBucketCount} を超えるため、間隔を {info.effectiveThreshold} に広げて生成します。");
                    }

                    merged.Add(setting);
                }
            }

            return (merged, writeDefault, matchAvatarWriteDefaults);
        }

        private static AnimatorController CreateController(ObserveParameterValueChange.ObserveSetting[] settings, bool writeDefault)
        {
            if (!Directory.Exists(GeneratedFolder))
                Directory.CreateDirectory(GeneratedFolder);

            var emptyMotion = LoadEmptyMotion();
            var paramDriverType = typeof(VRCAvatarParameterDriver);

            var controllerPath = $"{GeneratedFolder}/ObserveParameterValueChange_Generated.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null)
                return null;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = DummyParameterName,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            });

            var quantizeSettings = new List<ObserveParameterValueChange.ObserveSetting>();
            var layers = new List<AnimatorControllerLayer>();
            var triggerByLayer = new Dictionary<string, string>(StringComparer.Ordinal);
            var usedLayerNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var setting in settings)
            {
                var paramName = ObserveParameterValueChange.GetParamName(setting);
                var triggerName = ObserveParameterValueChange.GetValueChangedTriggerName(paramName);
                var layerName = NextLayerName(usedLayerNames, paramName);
                EnsureParameter(controller, triggerName, AnimatorControllerParameterType.Trigger);

                if (setting.paramType == ObserveParameterValueChange.ParamType.Bool)
                {
                    EnsureParameter(controller, paramName, AnimatorControllerParameterType.Bool);
                    layers.Add(CreateBoolLayer(layerName, paramName, emptyMotion, writeDefault));
                    triggerByLayer[layerName] = triggerName;
                    continue;
                }

                if (!ObserveParameterValueChange.TryGetQuantizeMapping(setting, out var mapping))
                    continue;

                var sourceType = setting.paramType == ObserveParameterValueChange.ParamType.Int
                    ? AnimatorControllerParameterType.Int
                    : AnimatorControllerParameterType.Float;
                EnsureParameter(controller, paramName, sourceType);
                EnsureParameter(controller, ObserveParameterValueChange.GetStepParameterName(paramName), AnimatorControllerParameterType.Int);
                quantizeSettings.Add(setting);
                layers.Add(CreateStepLayer(layerName, paramName, mapping.lastIndex, emptyMotion, writeDefault));
                triggerByLayer[layerName] = triggerName;
            }

            controller.RemoveLayer(0);

            if (quantizeSettings.Count > 0)
            {
                EnsureParameter(controller, ReadyParameterName, AnimatorControllerParameterType.Bool);
                controller.AddLayer(CreateQuantizeLayer(emptyMotion, writeDefault));
            }

            foreach (var layer in layers)
                controller.AddLayer(layer);

            AnimatorControllerAssetUtility.RegisterControllerHierarchy(controller);

            if (quantizeSettings.Count > 0)
                AddQuantizeDrivers(controller, quantizeSettings, paramDriverType);

            AddTriggerDrivers(controller, triggerByLayer, paramDriverType);

            EditorUtility.SetDirty(controller);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            return ModularAvatarMergeAnimatorUtility.ReloadControllerAtPath(controllerPath);
        }

        private static AnimatorControllerLayer CreateQuantizeLayer(AnimationClip emptyMotion, bool writeDefault)
        {
            var stateMachine = new AnimatorStateMachine { name = QuantizeLayerName };
            var state = stateMachine.AddState("Quantize", new Vector3(300, 120, 0));
            state.motion = emptyMotion;
            state.writeDefaultValues = writeDefault;
            stateMachine.defaultState = state;

            var self = state.AddTransition(state);
            ConfigureInstant(self);
            self.canTransitionToSelf = true;
            self.AddCondition(AnimatorConditionMode.If, 0, DummyParameterName);

            return new AnimatorControllerLayer
            {
                name = QuantizeLayerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };
        }

        private static AnimatorControllerLayer CreateBoolLayer(string layerName, string paramName, AnimationClip emptyMotion, bool writeDefault)
        {
            var stateMachine = new AnimatorStateMachine { name = layerName };
            var init = AddMotionState(stateMachine, "Init", new Vector3(30, 0, 0), emptyMotion, writeDefault);
            var brunch = AddMotionState(stateMachine, "Brunch", new Vector3(30, 120, 0), emptyMotion, writeDefault);
            var holdFalse = AddMotionState(stateMachine, "Hold 0", new Vector3(300, 40, 0), emptyMotion, writeDefault);
            var holdTrue = AddMotionState(stateMachine, "Hold 1", new Vector3(300, 160, 0), emptyMotion, writeDefault);
            var changedFalse = AddMotionState(stateMachine, "Changed 0", new Vector3(560, 40, 0), emptyMotion, writeDefault);
            var changedTrue = AddMotionState(stateMachine, "Changed 1", new Vector3(560, 160, 0), emptyMotion, writeDefault);
            stateMachine.defaultState = init;

            var initFalse = init.AddTransition(holdFalse);
            ConfigureInstant(initFalse);
            initFalse.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);

            var initTrue = init.AddTransition(holdTrue);
            ConfigureInstant(initTrue);
            initTrue.AddCondition(AnimatorConditionMode.If, 0, paramName);

            var leaveFalse = holdFalse.AddTransition(brunch);
            ConfigureInstant(leaveFalse);
            leaveFalse.AddCondition(AnimatorConditionMode.If, 0, paramName);

            var leaveTrue = holdTrue.AddTransition(brunch);
            ConfigureInstant(leaveTrue);
            leaveTrue.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);

            var enterFalse = brunch.AddTransition(changedFalse);
            ConfigureInstant(enterFalse);
            enterFalse.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);

            var enterTrue = brunch.AddTransition(changedTrue);
            ConfigureInstant(enterTrue);
            enterTrue.AddCondition(AnimatorConditionMode.If, 0, paramName);

            var doneFalse = changedFalse.AddTransition(holdFalse);
            ConfigureInstant(doneFalse);
            doneFalse.AddCondition(AnimatorConditionMode.If, 0, DummyParameterName);

            var doneTrue = changedTrue.AddTransition(holdTrue);
            ConfigureInstant(doneTrue);
            doneTrue.AddCondition(AnimatorConditionMode.If, 0, DummyParameterName);

            return new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };
        }

        private static AnimatorControllerLayer CreateStepLayer(string layerName, string paramName, int lastIndex, AnimationClip emptyMotion, bool writeDefault)
        {
            var stepParamName = ObserveParameterValueChange.GetStepParameterName(paramName);
            var stateMachine = new AnimatorStateMachine { name = layerName };
            var init = AddMotionState(stateMachine, "Init", new Vector3(30, 0, 0), emptyMotion, writeDefault);
            var brunch = AddMotionState(stateMachine, "Brunch", new Vector3(30, 160, 0), emptyMotion, writeDefault);
            stateMachine.defaultState = init;

            for (var value = 0; value <= lastIndex; value++)
            {
                var hold = AddMotionState(stateMachine, $"Hold {value}", new Vector3(300, 40 + value * 40, 0), emptyMotion, writeDefault);
                var changed = AddMotionState(stateMachine, $"Changed {value}", new Vector3(560, 40 + value * 40, 0), emptyMotion, writeDefault);

                var initIn = init.AddTransition(hold);
                ConfigureInstant(initIn);
                initIn.AddCondition(AnimatorConditionMode.If, 0, ReadyParameterName);
                AddStepMatch(initIn, value, lastIndex, stepParamName);

                var leave = hold.AddTransition(brunch);
                ConfigureInstant(leave);
                AddStepMismatch(leave, value, lastIndex, stepParamName);

                var enter = brunch.AddTransition(changed);
                ConfigureInstant(enter);
                AddStepMatch(enter, value, lastIndex, stepParamName);

                var done = changed.AddTransition(hold);
                ConfigureInstant(done);
                done.AddCondition(AnimatorConditionMode.If, 0, DummyParameterName);
            }

            return new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };
        }

        private static void AddStepMatch(AnimatorStateTransition transition, int value, int lastIndex, string stepParamName)
        {
            if (value == 0)
                transition.AddCondition(AnimatorConditionMode.Less, 1, stepParamName);
            else if (value == lastIndex)
                transition.AddCondition(AnimatorConditionMode.Greater, lastIndex - 1, stepParamName);
            else
                transition.AddCondition(AnimatorConditionMode.Equals, value, stepParamName);
        }

        private static void AddStepMismatch(AnimatorStateTransition transition, int value, int lastIndex, string stepParamName)
        {
            if (value == 0)
                transition.AddCondition(AnimatorConditionMode.Greater, 0, stepParamName);
            else if (value == lastIndex)
                transition.AddCondition(AnimatorConditionMode.Less, lastIndex, stepParamName);
            else
                transition.AddCondition(AnimatorConditionMode.NotEqual, value, stepParamName);
        }

        private static void AddQuantizeDrivers(AnimatorController controller, List<ObserveParameterValueChange.ObserveSetting> settings, Type paramDriverType)
        {
            foreach (var layer in controller.layers)
            {
                if (layer.name != QuantizeLayerName || layer.stateMachine == null)
                    continue;

                foreach (var child in layer.stateMachine.states)
                {
                    var state = child.state;
                    if (state == null || state.name != "Quantize")
                        continue;

                    var behaviour = state.AddStateMachineBehaviour(paramDriverType);
                    if (behaviour == null)
                        continue;

                    AnimatorControllerAssetUtility.EnsureSubAsset(behaviour, controller);
                    var driver = new SerializedObject(behaviour);
                    var parametersProp = driver.FindProperty("parameters");
                    if (parametersProp == null)
                        continue;

                    parametersProp.ClearArray();
                    foreach (var setting in settings)
                    {
                        if (!ObserveParameterValueChange.TryGetQuantizeMapping(setting, out var mapping))
                            continue;

                        var paramName = ObserveParameterValueChange.GetParamName(setting);
                        AppendParamDriverCopy(
                            parametersProp,
                            paramName,
                            ObserveParameterValueChange.GetStepParameterName(paramName),
                            mapping.sourceMin,
                            BiasSourceMax(mapping),
                            mapping.destMin,
                            mapping.destMax);
                    }

                    AppendParamDriverSet(parametersProp, ReadyParameterName, 1f);
                    driver.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        /// <summary>
        /// 境界値が切り捨てで一つ下の段に落ちないよう、sourceMax を段幅の 0.1% だけ縮める。
        /// </summary>
        private static float BiasSourceMax(ObserveParameterValueChange.QuantizeMapping mapping)
        {
            var span = mapping.sourceMax - mapping.sourceMin;
            if (span <= 0f || mapping.destMax <= mapping.destMin)
                return mapping.sourceMax;

            var stepWidth = span / (mapping.destMax - mapping.destMin);
            return mapping.sourceMax - stepWidth * 0.001f;
        }

        private static void AddTriggerDrivers(AnimatorController controller, Dictionary<string, string> triggerByLayer, Type paramDriverType)
        {
            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null || layer.name == QuantizeLayerName)
                    continue;
                if (triggerByLayer == null || !triggerByLayer.TryGetValue(layer.name, out var triggerName))
                    continue;

                foreach (var child in layer.stateMachine.states)
                {
                    var state = child.state;
                    if (state == null || !state.name.StartsWith("Changed ", StringComparison.Ordinal))
                        continue;

                    var behaviour = state.AddStateMachineBehaviour(paramDriverType);
                    if (behaviour == null)
                        continue;

                    AnimatorControllerAssetUtility.EnsureSubAsset(behaviour, controller);
                    SetParamDriverSet(behaviour, triggerName, 1f);
                }
            }
        }

        private static string LayerName(string paramName) => $"Observe_{paramName.Replace('/', '_')}";

        private static string NextLayerName(HashSet<string> usedLayerNames, string paramName)
        {
            var layerName = LayerName(paramName);
            if (usedLayerNames.Add(layerName))
                return layerName;

            var suffix = 2;
            while (!usedLayerNames.Add($"{layerName}_{suffix}"))
                suffix++;
            return $"{layerName}_{suffix}";
        }

        private static AnimatorState AddMotionState(AnimatorStateMachine stateMachine, string name, Vector3 position, AnimationClip motion, bool writeDefault)
        {
            var state = stateMachine.AddState(name, position);
            state.motion = motion;
            state.writeDefaultValues = writeDefault;
            return state;
        }

        private static void ConfigureInstant(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.duration = 0f;
            transition.canTransitionToSelf = true;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var parameter in controller.parameters)
            {
                if (parameter.name == name)
                    return;
            }

            controller.AddParameter(name, type);
        }

        private static void SetParamDriverSet(StateMachineBehaviour behaviour, string destName, float value)
        {
            var so = new SerializedObject(behaviour);
            var parametersProp = so.FindProperty("parameters");
            if (parametersProp == null)
                return;

            parametersProp.ClearArray();
            AppendParamDriverSet(parametersProp, destName, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AppendParamDriverSet(SerializedProperty parametersProp, string destName, float value)
        {
            parametersProp.InsertArrayElementAtIndex(parametersProp.arraySize);
            var entry = parametersProp.GetArrayElementAtIndex(parametersProp.arraySize - 1);
            entry.FindPropertyRelative("name").stringValue = destName;
            var valueProp = entry.FindPropertyRelative("value");
            if (valueProp != null)
                valueProp.floatValue = value;

            var typeProp = entry.FindPropertyRelative("type");
            if (typeProp != null)
            {
                if (typeProp.propertyType == SerializedPropertyType.Enum)
                    typeProp.enumValueIndex = 0;
                else
                    typeProp.intValue = 0;
            }

            var sourceProp = entry.FindPropertyRelative("source");
            if (sourceProp != null)
                sourceProp.stringValue = "";
        }

        private static void AppendParamDriverCopy(SerializedProperty parametersProp, string sourceName, string destName, float sourceMin, float sourceMax, float destMin, float destMax)
        {
            parametersProp.InsertArrayElementAtIndex(parametersProp.arraySize);
            var entry = parametersProp.GetArrayElementAtIndex(parametersProp.arraySize - 1);
            entry.FindPropertyRelative("name").stringValue = destName;
            entry.FindPropertyRelative("source").stringValue = sourceName;

            var typeProp = entry.FindPropertyRelative("type");
            if (typeProp != null)
            {
                if (typeProp.propertyType == SerializedPropertyType.Enum)
                    typeProp.enumValueIndex = 3;
                else
                    typeProp.intValue = 3;
            }

            var convertProp = entry.FindPropertyRelative("convertRange");
            if (convertProp != null)
                convertProp.intValue = 1;

            var sourceMinProp = entry.FindPropertyRelative("sourceMin");
            if (sourceMinProp != null)
                sourceMinProp.floatValue = sourceMin;
            var sourceMaxProp = entry.FindPropertyRelative("sourceMax");
            if (sourceMaxProp != null)
                sourceMaxProp.floatValue = sourceMax;
            var destMinProp = entry.FindPropertyRelative("destMin");
            if (destMinProp != null)
                destMinProp.floatValue = destMin;
            var destMaxProp = entry.FindPropertyRelative("destMax");
            if (destMaxProp != null)
                destMaxProp.floatValue = destMax;
        }

        private static AnimationClip LoadEmptyMotion()
        {
            var path = AssetDatabase.GUIDToAssetPath(EmptyMotionGUID);
            if (!string.IsNullOrEmpty(path))
            {
                var loadedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (loadedClip != null)
                    return loadedClip;
            }

            if (!Directory.Exists(GeneratedFolder))
                Directory.CreateDirectory(GeneratedFolder);

            var clipPath = $"{GeneratedFolder}/ObserveParameterValueChange_Empty.anim";
            var emptyClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (emptyClip == null)
            {
                emptyClip = new AnimationClip();
                AssetDatabase.CreateAsset(emptyClip, clipPath);
                AssetDatabase.SaveAssets();
            }

            return emptyClip;
        }
    }
}
