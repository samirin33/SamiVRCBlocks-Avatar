using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.Components;
using Samirin33.NDMF.Base;
using Samirin33.NDMF.Base.Plugin;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    public static class SnappedParameterBuilder
    {
        [InitializeOnLoadMethod]
        private static void RegisterBuilder()
        {
            SamirinMABaseSingleBuildRegistry.Register<SnappedParameter>(Build);
            SamirinMABaseSingleBuildRegistry.RegisterReplace<SnappedParameter>(RunReplace);
        }

        private const string EmptyMotionGUID = "4de039275b65be24c8f0a641d7a44924";
        private static string GeneratedFolder => "Assets/Generated/SamiVRCBlocks/SnappedParameter";

        private static readonly string[] DefaultExcludedLayerNames =
        {
            "ParameterSmoothing",
            "Smoothed",
            "SnappedParameter",
        };

        public static void Build(GameObject avatarRootObject, params SnappedParameter[] snappedParameters)
        {
            BuildInternal(avatarRootObject, ensureFpsCounterModule: true, snappedParameters);
        }

        /// <summary>
        /// インスペクターからのマニュアル生成。ExtendedParameters（FPSCounter）は付けない。
        /// </summary>
        public static AnimatorController[] BuildManual(GameObject avatarRootObject, params SnappedParameter[] snappedParameters)
        {
            return BuildInternal(avatarRootObject, ensureFpsCounterModule: false, snappedParameters);
        }

        private static AnimatorController[] BuildInternal(GameObject avatarRootObject, bool ensureFpsCounterModule,
            params SnappedParameter[] snappedParameters)
        {
            if (snappedParameters == null || snappedParameters.Length == 0)
                return Array.Empty<AnimatorController>();

            var (mergedSettings, writeDefault, matchAvatarWriteDefaults) = MergeSettingsFromModule(snappedParameters);
            if (mergedSettings.Count == 0)
                return Array.Empty<AnimatorController>();

            var controller = CreateControllerFromScratch(mergedSettings.ToArray(), writeDefault, out var paramNamesToRegister);
            if (controller == null)
                return Array.Empty<AnimatorController>();

            var result = new List<AnimatorController> { controller };

            var moduleParent = snappedParameters.FirstOrDefault(c => c != null)?.gameObject ?? avatarRootObject;
            AddModularAvatarModule(moduleParent, controller, paramNamesToRegister, matchAvatarWriteDefaults);

            var smoothingInfos = ExtractSmoothingInfos(mergedSettings);
            if (smoothingInfos.Count > 0)
            {
                var smoothingController = ParameterSmoothingBuilder.BuildFromInfos(
                    avatarRootObject,
                    smoothingInfos.ToArray(),
                    moduleParent,
                    "SnappedParameter_Smoothing_Module",
                    fromResizableSyncParameters: false,
                    matchAvatarWriteDefaults: matchAvatarWriteDefaults);
                if (smoothingController != null)
                    result.Add(smoothingController);

                if (ensureFpsCounterModule)
                {
                    foreach (var component in snappedParameters)
                    {
                        if (component == null || !HasSettings(component)) continue;
                        ParameterSmoothingBuilder.EnsureFPSCounterModule(component.gameObject);
                    }
                }
            }

            return result.ToArray();
        }

        private static bool HasSettings(SnappedParameter component)
            => component.snapSettings != null && component.snapSettings.Length > 0;

        private static List<ParameterSmoothing.ParameterSmoothingInfo> ExtractSmoothingInfos(
            List<SnappedParameter.SnapSetting> settings)
        {
            var infos = new List<ParameterSmoothing.ParameterSmoothingInfo>();
            foreach (var setting in settings)
            {
                var paramName = SnappedParameter.GetParamName(setting);
                var (rangeMin, rangeMax) = SnappedParameter.GetSourceRange(setting);
                infos.Add(new ParameterSmoothing.ParameterSmoothingInfo
                {
                    parameterName = SnappedParameter.GetSnappedParamName(paramName),
                    useDefaultSmoothWeight = false,
                    smoothWeight = setting.smoothWeight,
                    smoothedParameterName = SnappedParameter.GetSnapSmoothedParamName(paramName),
                    specifyClampRange = true,
                    clampMin = rangeMin,
                    clampMax = rangeMax,
                });
            }

            return infos;
        }

        /// <summary>
        /// Optimizing（Modular Avatar より前）で呼ばれる置換処理。
        /// Float 参照だけを _SnapSmoothed に付け替え、元パラメーターは残す。Parameter Driver の書き込み先は変えない。
        /// </summary>
        public static void RunReplace(GameObject avatarRootObject, params SnappedParameter[] snappedParameters)
        {
            if (avatarRootObject == null || snappedParameters == null || snappedParameters.Length == 0)
                return;

            var fxController = VRCAvatarDescriptorControllerUtility.GetController(
                avatarRootObject,
                VRCAvatarDescriptor.AnimLayerType.FX);
            if (fxController == null) return;

            var processedParamNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var component in snappedParameters)
            {
                if (component == null || !component.replaceWithSnapSmoothedInAnimator) continue;
                if (component.snapSettings == null) continue;

                foreach (var setting in component.snapSettings)
                {
                    if (setting == null) continue;

                    var paramName = SnappedParameter.GetParamName(setting);
                    if (!processedParamNames.Add(paramName)) continue;

                    var smoothedName = SnappedParameter.GetSnapSmoothedParamName(paramName);
                    AnimatorParameterReplaceUtility.ReplaceParameterReferences(
                        fxController, paramName, smoothedName, DefaultExcludedLayerNames);
                }
            }
        }

        private static (List<SnappedParameter.SnapSetting> settings, bool writeDefault, bool matchAvatarWriteDefaults) MergeSettingsFromModule(
            SnappedParameter[] snappedParameters)
        {
            var processedParamNames = new HashSet<string>(StringComparer.Ordinal);
            var mergedSettings = new List<SnappedParameter.SnapSetting>();
            var writeDefault = snappedParameters.Length > 0 && snappedParameters[0].writeDefault;
            var matchAvatarWriteDefaults = false;

            foreach (var component in snappedParameters)
            {
                if (component == null) continue;
                if (component.writeDefault)
                    writeDefault = true;
                if (component.matchAvatarWriteDefaults)
                    matchAvatarWriteDefaults = true;
                if (component.snapSettings == null) continue;

                foreach (var setting in component.snapSettings)
                {
                    if (setting == null) continue;
                    if (SnappedParameter.GetDivisions(setting) < 1) continue;

                    var paramName = SnappedParameter.GetParamName(setting);
                    if (!processedParamNames.Add(paramName))
                        continue;

                    mergedSettings.Add(setting);
                }
            }

            return (mergedSettings, writeDefault, matchAvatarWriteDefaults);
        }

        private static AnimatorController CreateControllerFromScratch(SnappedParameter.SnapSetting[] settings,
            bool writeDefault, out List<(string name, ParameterSyncType syncType)> paramNamesToRegister)
        {
            paramNamesToRegister = new List<(string, ParameterSyncType)>();

            var paramDriverType = typeof(VRCAvatarParameterDriver);
            var emptyMotion = LoadEmptyMotion();

            const string controllerName = "SnappedParameter_Generated";
            var controller = ModularAvatarMergeAnimatorUtility.CreateGeneratedController(
                controllerName,
                $"{GeneratedFolder}/{controllerName}.controller");
            if (controller == null)
                return null;

            foreach (var setting in settings)
            {
                var paramName = SnappedParameter.GetParamName(setting);
                controller.AddParameter(paramName, AnimatorControllerParameterType.Float);
                controller.AddParameter(SnappedParameter.GetSnappedParamName(paramName), AnimatorControllerParameterType.Float);
                controller.AddParameter(SnappedParameter.GetSnapSmoothedParamName(paramName), AnimatorControllerParameterType.Float);
                controller.AddParameter(SnappedParameter.GetSnapIntParamName(paramName), AnimatorControllerParameterType.Int);

                // 元パラメーターはユーザー側定義に任せる。
                // _SnapSmoothed は ParameterSmoothing モジュール側で登録する。
                paramNamesToRegister.Add((SnappedParameter.GetSnappedParamName(paramName), ParameterSyncType.Float));
            }

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "dummy",
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            });

            controller.RemoveLayer(0);

            var snapLayer = CreateSnapLayer(emptyMotion, writeDefault);
            if (snapLayer != null)
                controller.AddLayer(snapLayer);

            AnimatorControllerAssetUtility.RegisterControllerHierarchy(controller);
            AddSnapParamDrivers(controller, settings, paramDriverType);

            return ModularAvatarMergeAnimatorUtility.FinalizeGeneratedController(controller);
        }

        private static AnimatorControllerLayer CreateSnapLayer(AnimationClip emptyMotion, bool writeDefault)
        {
            var rootSm = new AnimatorStateMachine { name = "SnappedParameter" };

            var snapState = rootSm.AddState("Snap", new Vector3(300, 120, 0));
            snapState.motion = emptyMotion;
            snapState.writeDefaultValues = writeDefault;
            rootSm.defaultState = snapState;

            // Parameter Driver を毎フレーム回すため self 遷移
            var selfTransition = snapState.AddTransition(snapState);
            selfTransition.hasExitTime = false;
            selfTransition.duration = 0f;
            selfTransition.exitTime = 0f;
            selfTransition.canTransitionToSelf = true;
            selfTransition.AddCondition(AnimatorConditionMode.If, 0, "dummy");

            return new AnimatorControllerLayer
            {
                name = "SnappedParameter",
                defaultWeight = 1f,
                stateMachine = rootSm
            };
        }

        private static void AddSnapParamDrivers(AnimatorController controller, SnappedParameter.SnapSetting[] settings,
            Type paramDriverType)
        {
            if (settings == null || settings.Length == 0 || paramDriverType == null) return;

            foreach (var layer in controller.layers)
            {
                if (layer.name != "SnappedParameter") continue;

                foreach (var state in layer.stateMachine.states)
                {
                    if (state.state.name != "Snap") continue;

                    foreach (var setting in settings)
                    {
                        var maxValue = SnappedParameter.GetMaxSnapValue(setting);
                        var paramName = SnappedParameter.GetParamName(setting);
                        var snapIntName = SnappedParameter.GetSnapIntParamName(paramName);
                        var snappedName = SnappedParameter.GetSnappedParamName(paramName);
                        var (inputMin, inputMax, syncMin, syncMax) = GetInputToSnapRanges(setting, maxValue);
                        var (destMin, destMax, sourceMin, sourceMax) = GetSnapToOutputRanges(setting, maxValue);

                        var behaviour = state.state.AddStateMachineBehaviour(paramDriverType);
                        if (behaviour == null) continue;

                        AnimatorControllerAssetUtility.EnsureSubAsset(behaviour, controller);
                        SetParamDriverCopy(behaviour, paramName, snapIntName, inputMin, inputMax, syncMin, syncMax, clearFirst: true);
                        SetParamDriverCopy(behaviour, snapIntName, snappedName, sourceMin, sourceMax, destMin, destMax, clearFirst: false);
                    }
                }
            }
        }

        /// <summary>
        /// ユーザー入力レンジからスナップ用 Int レンジへの変換範囲。
        /// Driver の切り捨てを丸め込み相当にするため、source を分解能/2 だけ負方向へオフセットする。
        /// </summary>
        private static (float inputMin, float inputMax, float syncMin, float syncMax) GetInputToSnapRanges(
            SnappedParameter.SnapSetting setting, int maxValue)
        {
            var (rangeMin, rangeMax) = SnappedParameter.GetSourceRange(setting);
            if (maxValue <= 0)
                return (rangeMin, rangeMax, 0f, maxValue);

            var halfStep = (rangeMax - rangeMin) / maxValue * 0.5f;
            return (rangeMin - halfStep, rangeMax - halfStep, 0f, maxValue);
        }

        private static (float destMin, float destMax, float sourceMin, float sourceMax) GetSnapToOutputRanges(
            SnappedParameter.SnapSetting setting, int maxValue)
        {
            var (rangeMin, rangeMax) = SnappedParameter.GetSourceRange(setting);
            return (rangeMin, rangeMax, 0f, maxValue);
        }

        private static void SetParamDriverCopy(StateMachineBehaviour behaviour, string sourceName, string destName,
            float sourceMin, float sourceMax, float destMin, float destMax, bool clearFirst = true)
        {
            var so = new SerializedObject(behaviour);
            var parametersProp = so.FindProperty("parameters");
            if (parametersProp == null) return;

            if (clearFirst)
                parametersProp.ClearArray();
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
            if (convertProp != null) convertProp.intValue = 1;
            var sm = entry.FindPropertyRelative("sourceMin");
            if (sm != null) sm.floatValue = sourceMin;
            var sM = entry.FindPropertyRelative("sourceMax");
            if (sM != null) sM.floatValue = sourceMax;
            var dm = entry.FindPropertyRelative("destMin");
            if (dm != null) dm.floatValue = destMin;
            var dM = entry.FindPropertyRelative("destMax");
            if (dM != null) dM.floatValue = destMax;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimationClip LoadEmptyMotion()
            => ModularAvatarMergeAnimatorUtility.LoadOrCreateEmptyClip(EmptyMotionGUID, $"{GeneratedFolder}/SnappedParameter_Empty.anim");

        private static void AddModularAvatarModule(GameObject parentObject, AnimatorController controller,
            List<(string name, ParameterSyncType syncType)> paramNamesToRegister, bool matchAvatarWriteDefaults)
        {
            var moduleRoot = ModularAvatarMergeAnimatorUtility.RegisterMergeAnimatorModule(
                parentObject,
                "SnappedParameter_Module",
                controller,
                layerPriority: 0,
                matchAvatarWriteDefaults: matchAvatarWriteDefaults);
            if (moduleRoot == null)
                return;

            var maParameters = moduleRoot.GetComponent<ModularAvatarParameters>();
            if (maParameters == null)
                maParameters = moduleRoot.AddComponent<ModularAvatarParameters>();

            foreach (var (paramName, syncType) in paramNamesToRegister)
            {
                if (maParameters.parameters.Exists(p => p.nameOrPrefix == paramName))
                    continue;

                maParameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = paramName,
                    remapTo = "",
                    internalParameter = false,
                    isPrefix = false,
                    syncType = syncType,
                    localOnly = true,
                    defaultValue = 0f,
                    saved = false,
                    hasExplicitDefaultValue = true
                });
            }

            EditorUtility.SetDirty(moduleRoot);
        }
    }
}
