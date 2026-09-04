using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Samirin33.NDMF.Base.Plugin;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    public static class ResizableSyncParametersBuilder
    {
        [InitializeOnLoadMethod]
        private static void RegisterBuilder()
        {
            SamirinMABaseSingleBuildRegistry.Register<ResizableSyncParameters>(Build);
            SamirinMABaseSingleBuildRegistry.RegisterReplace<ResizableSyncParameters>(RunReplace);
        }

        private const string EmptyMotionGUID = "4de039275b65be24c8f0a641d7a44924";
        private static string GeneratedFolder => "Assets/Generated/SamiVRCBlocks/ResizableSyncParameters";

        private static int GetBitCount(ResizableSyncParameters.SyncParamSetting setting)
            => ResizableSyncParameters.GetBitCount(setting);

        public static void Build(GameObject avatarRootObject, params ResizableSyncParameters[] resizableSyncParameters)
        {
            // Bit 分解同期に置き換えるため、元パラメーターが Synced 登録されていれば解除する
            ForceUnsyncOriginalParameters(avatarRootObject, resizableSyncParameters);
            BuildInternal(avatarRootObject, ensureFpsCounterModule: true, resizableSyncParameters);
        }

        /// <summary>
        /// ResizableSyncParameters で指定したパラメーターが、アバター内の
        /// VRCExpressionParameters / ModularAvatarParameters で Synced として登録されている場合、
        /// Synced でない状態に書き換える（同期 Bit は SUM/ResizableSync/... 側に移す）。
        /// NDMF ビルド時のみ呼び出すこと（プロジェクト上の ExpressionParameters アセットを汚さないようクローンする）。
        /// </summary>
        private static void ForceUnsyncOriginalParameters(GameObject avatarRootObject,
            params ResizableSyncParameters[] resizableSyncParameters)
        {
            if (avatarRootObject == null || resizableSyncParameters == null || resizableSyncParameters.Length == 0)
                return;

            var paramNames = CollectSpecifiedParamNames(resizableSyncParameters);
            if (paramNames.Count == 0)
                return;

            ForceUnsyncModularAvatarParameters(avatarRootObject, paramNames);
            ForceUnsyncVrcExpressionParameters(avatarRootObject, paramNames);
        }

        private static HashSet<string> CollectSpecifiedParamNames(ResizableSyncParameters[] resizableSyncParameters)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var component in resizableSyncParameters)
            {
                if (component?.syncParamSettings == null) continue;
                foreach (var setting in component.syncParamSettings)
                {
                    if (setting == null || GetBitCount(setting) < 1) continue;
                    names.Add(ResizableSyncParameters.GetParamName(setting));
                }
            }
            return names;
        }

        private static void ForceUnsyncModularAvatarParameters(GameObject avatarRootObject, HashSet<string> paramNames)
        {
            var maParametersList = avatarRootObject.GetComponentsInChildren<ModularAvatarParameters>(true);
            foreach (var maParameters in maParametersList)
            {
                if (maParameters?.parameters == null || maParameters.parameters.Count == 0)
                    continue;

                var changed = false;
                for (var i = 0; i < maParameters.parameters.Count; i++)
                {
                    var config = maParameters.parameters[i];
                    if (config.isPrefix) continue;
                    if (string.IsNullOrEmpty(config.nameOrPrefix)) continue;
                    if (!paramNames.Contains(config.nameOrPrefix)) continue;
                    if (config.syncType == ParameterSyncType.NotSynced) continue;
                    if (config.localOnly) continue;

                    config.localOnly = true;
                    maParameters.parameters[i] = config;
                    changed = true;
                }

                if (changed)
                    EditorUtility.SetDirty(maParameters);
            }
        }

        private static void ForceUnsyncVrcExpressionParameters(GameObject avatarRootObject, HashSet<string> paramNames)
        {
            var descriptor = avatarRootObject.GetComponent<VRCAvatarDescriptor>();
            if (descriptor?.expressionParameters?.parameters == null)
                return;

            var sourceParams = descriptor.expressionParameters;
            var parameters = sourceParams.parameters;
            if (parameters == null || parameters.Length == 0)
                return;

            var needsUnsync = false;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter == null) continue;
                if (string.IsNullOrEmpty(parameter.name)) continue;
                if (!paramNames.Contains(parameter.name)) continue;
                if (!parameter.networkSynced) continue;
                needsUnsync = true;
                break;
            }

            if (!needsUnsync)
                return;

            // プロジェクト上のアセットを直接書き換えないよう、永続アセットならクローンする
            var expParams = sourceParams;
            var assetPath = AssetDatabase.GetAssetPath(sourceParams);
            if (!string.IsNullOrEmpty(assetPath))
            {
                expParams = UnityEngine.Object.Instantiate(sourceParams);
                descriptor.expressionParameters = expParams;
                parameters = expParams.parameters;
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter == null) continue;
                if (string.IsNullOrEmpty(parameter.name)) continue;
                if (!paramNames.Contains(parameter.name)) continue;
                if (!parameter.networkSynced) continue;

                parameter.networkSynced = false;
                parameters[i] = parameter;
            }

            expParams.parameters = parameters;
            EditorUtility.SetDirty(descriptor);
        }

        /// <summary>
        /// インスペクターからのマニュアル生成。ExtendedParameters（FPSCounter）は付けない。
        /// </summary>
        public static AnimatorController[] BuildManual(GameObject avatarRootObject, params ResizableSyncParameters[] resizableSyncParameters)
        {
            return BuildInternal(avatarRootObject, ensureFpsCounterModule: false, resizableSyncParameters);
        }

        private static AnimatorController[] BuildInternal(GameObject avatarRootObject, bool ensureFpsCounterModule,
            params ResizableSyncParameters[] resizableSyncParameters)
        {
            if (resizableSyncParameters == null || resizableSyncParameters.Length == 0)
                return Array.Empty<AnimatorController>();

            var (mergedSettings, writeDefault) = MergeSettingsFromModule(resizableSyncParameters);
            if (mergedSettings.Count == 0)
                return Array.Empty<AnimatorController>();

            var controller = CreateControllerFromScratch(mergedSettings.ToArray(), writeDefault, out var paramNamesToRegister);
            if (controller == null)
                return Array.Empty<AnimatorController>();

            var result = new List<AnimatorController> { controller };

            var moduleParent = resizableSyncParameters.FirstOrDefault(c => c != null)?.gameObject ?? avatarRootObject;
            AddModularAvatarModule(moduleParent, controller, paramNamesToRegister);

            var smoothingInfos = ExtractFloatSmoothingInfos(resizableSyncParameters);
            if (smoothingInfos.Count > 0)
            {
                var smoothingController = ParameterSmoothingBuilder.BuildFromResizableSyncParameters(
                    avatarRootObject, smoothingInfos.ToArray(), moduleParent);
                if (smoothingController != null)
                    result.Add(smoothingController);

                if (ensureFpsCounterModule)
                {
                    foreach (var component in resizableSyncParameters)
                    {
                        if (component == null || !HasFloatSettings(component)) continue;
                        ParameterSmoothingBuilder.EnsureFPSCounterModule(component.gameObject);
                    }
                }
            }

            return result.ToArray();
        }

        private static bool HasFloatSettings(ResizableSyncParameters resizableSyncParam)
        {
            if (resizableSyncParam.syncParamSettings == null) return false;
            return resizableSyncParam.syncParamSettings.Any(s => s.paramType == ResizableSyncParameters.ParamType.Float);
        }

        private static List<ParameterSmoothing.ParameterSmoothingInfo> ExtractFloatSmoothingInfos(ResizableSyncParameters[] resizableSyncParameters)
        {
            var processedParamNames = new HashSet<string>(StringComparer.Ordinal);
            var infos = new List<ParameterSmoothing.ParameterSmoothingInfo>();

            foreach (var component in resizableSyncParameters)
            {
                if (component?.syncParamSettings == null) continue;

                foreach (var setting in component.syncParamSettings)
                {
                    if (setting.paramType != ResizableSyncParameters.ParamType.Float) continue;

                    var paramName = ResizableSyncParameters.GetParamName(setting);

                    if (!processedParamNames.Add(paramName)) continue;

                    infos.Add(new ParameterSmoothing.ParameterSmoothingInfo
                    {
                        parameterName = $"{paramName}_Snapped",
                        useDefaultSmoothWeight = false,
                        smoothWeight = setting.smoothWeight,
                        smoothedParameterName = $"{paramName}_Smoothed"
                    });
                }
            }

            return infos;
        }

        /// <summary>
        /// 置換処理で除外するレイヤー名（Smoothing 関連）。ParameterSmoothing / ResizableSyncParameters 由来のレイヤーを除外する。
        /// </summary>
        private static readonly string[] DefaultExcludedLayerNames = { "ParameterSmoothing", "Smoothed" };

        /// <summary>
        /// Generating 後（afterModularAvatar）で呼ばれる置換処理。VRCAvatarDescriptor の FX レイヤーに作用する。
        /// Smoothing 関連レイヤーは除外レイヤーとして指定し、置換対象外とする。
        /// </summary>
        public static void RunReplace(GameObject avatarRootObject, params ResizableSyncParameters[] resizableSyncParameters)
        {
            if (avatarRootObject == null || resizableSyncParameters == null || resizableSyncParameters.Length == 0)
                return;

            var fxController = VRCAvatarDescriptorControllerUtility.GetController(
                avatarRootObject,
                VRCAvatarDescriptor.AnimLayerType.FX);
            if (fxController == null) return;

            var processedParamNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var component in resizableSyncParameters)
            {
                if (component == null || !component.replaceWithSmoothedInAnimator) continue;
                if (component.syncParamSettings == null) continue;

                foreach (var setting in component.syncParamSettings)
                {
                    if (setting.paramType != ResizableSyncParameters.ParamType.Float) continue;

                    var paramName = ResizableSyncParameters.GetParamName(setting);

                    if (!processedParamNames.Add(paramName)) continue;

                    var smoothedName = $"{paramName}_Smoothed";
                    AnimatorParameterReplaceUtility.ReplaceParameterReferences(fxController, paramName, smoothedName, DefaultExcludedLayerNames);
                }
            }
        }

        private static (List<ResizableSyncParameters.SyncParamSetting> settings, bool writeDefault) MergeSettingsFromModule(
            ResizableSyncParameters[] resizableSyncParameters)
        {
            var processedParamNames = new HashSet<string>(StringComparer.Ordinal);
            var mergedSettings = new List<ResizableSyncParameters.SyncParamSetting>();
            var writeDefault = resizableSyncParameters.Length > 0 && resizableSyncParameters[0].writeDefault;

            foreach (var component in resizableSyncParameters)
            {
                if (component.syncParamSettings == null) continue;

                foreach (var setting in component.syncParamSettings)
                {
                    if (GetBitCount(setting) < 1) continue;

                    var paramName = ResizableSyncParameters.GetParamName(setting);

                    if (processedParamNames.Contains(paramName))
                        continue;

                    processedParamNames.Add(paramName);
                    mergedSettings.Add(setting);
                }

                if (component.writeDefault)
                    writeDefault = true;
            }

            return (mergedSettings, writeDefault);
        }

        private static AnimatorController CreateControllerFromScratch(ResizableSyncParameters.SyncParamSetting[] settings,
            bool writeDefault, out List<(string name, ParameterSyncType syncType)> paramNamesToRegister)
        {
            paramNamesToRegister = new List<(string, ParameterSyncType)>();

            if (!Directory.Exists(GeneratedFolder))
                Directory.CreateDirectory(GeneratedFolder);

            var emptyMotion = LoadEmptyMotion();
            var paramDriverType = GetVRCAvatarParameterDriverType();
            if (paramDriverType == null)
            {
                Debug.LogError("[ResizableSyncParameters] VRCAvatarParameterDriver 型が見つかりません。VRChat SDK3 Avatars がインストールされているか確認してください。");
                return null;
            }

            var controllerPath = $"{GeneratedFolder}/ResizableSyncParameters_Generated.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null)
                return null;

            controller.AddParameter("IsLocal", AnimatorControllerParameterType.Bool);

            var layersToAdd = new List<(AnimatorControllerLayer layer, string paramName, string intParamName, int bitCount, int maxValue, bool isFloat)>();

            foreach (var setting in settings)
            {
                var bitCount = GetBitCount(setting);
                var paramName = ResizableSyncParameters.GetParamName(setting);
                var maxValue = (1 << bitCount) - 1;
                var isFloat = setting.paramType == ResizableSyncParameters.ParamType.Float;
                var intParamName = $"{paramName}_Int";

                if (isFloat)
                {
                    controller.AddParameter(paramName, AnimatorControllerParameterType.Float);
                    controller.AddParameter($"{paramName}_Snapped", AnimatorControllerParameterType.Float);
                    controller.AddParameter($"{paramName}_Smoothed", AnimatorControllerParameterType.Float);
                }
                else
                {
                    controller.AddParameter(paramName, AnimatorControllerParameterType.Int);
                }
                controller.AddParameter(intParamName, AnimatorControllerParameterType.Int);
                for (int i = 0; i < bitCount; i++)
                {
                    var syncParamName = ResizableSyncParameters.GetSyncBoolParamName(paramName, i);
                    controller.AddParameter(syncParamName, AnimatorControllerParameterType.Bool);
                    paramNamesToRegister.Add((syncParamName, ParameterSyncType.Bool));
                }

                var layer = CreateLayerForParam(intParamName, bitCount, maxValue, emptyMotion, writeDefault);
                layersToAdd.Add((layer, paramName, intParamName, bitCount, maxValue, isFloat));
            }

            controller.RemoveLayer(0);

            if (settings.Length > 0)
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = "dummy",
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = true
                });

                var rangeLayer = CreateRangeConvertLayer(settings, writeDefault);
                if (rangeLayer != null)
                    controller.AddLayer(rangeLayer);
            }

            foreach (var (layer, _, _, _, _, _) in layersToAdd)
                controller.AddLayer(layer);

            AnimatorControllerAssetUtility.RegisterControllerHierarchy(controller);

            foreach (var (_, _, intParamName, bitCount, maxValue, _) in layersToAdd)
                AddParamDriverBehaviours(controller, intParamName, bitCount, maxValue, paramDriverType);

            AddRangeConvertParamDrivers(controller, settings, paramDriverType);

            EditorUtility.SetDirty(controller);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            return ModularAvatarMergeAnimatorUtility.ReloadControllerAtPath(controllerPath);
        }

        /// <summary>
        /// State がコントローラーのサブアセットのとき、AddStateMachineBehaviour は Behaviour を自動登録するため二重登録を避ける。
        /// </summary>
        private static void EnsureSubAsset(UnityEngine.Object obj, UnityEngine.Object mainAsset)
            => AnimatorControllerAssetUtility.EnsureSubAsset(obj, mainAsset);

        private static AnimatorControllerLayer CreateLayerForParam(string paramName, int bitCount, int maxValue,
            AnimationClip emptyMotion, bool writeDefault)
        {
            var layerName = $"Convert_IntParam{paramName}{bitCount}bit";
            var rootSm = new AnimatorStateMachine { name = layerName };

            var localSm = new AnimatorStateMachine { name = "Local" };
            var remoteSm = new AnimatorStateMachine { name = "Remote" };

            rootSm.AddStateMachine(localSm, new Vector3(300, 120, 0));
            rootSm.AddStateMachine(remoteSm, new Vector3(300, 160, 0));

            var localStates = new AnimatorState[maxValue + 1];
            var remoteStates = new AnimatorState[maxValue + 1];

            for (int value = 0; value <= maxValue; value++)
            {
                var localState = localSm.AddState($"Binary {value}", new Vector3(300, 120 + value * 40, 0));
                localState.motion = emptyMotion;
                localState.writeDefaultValues = writeDefault;
                localStates[value] = localState;

                var remoteState = remoteSm.AddState($"Binary {value}", new Vector3(300, 120 + value * 40, 0));
                remoteState.motion = emptyMotion;
                remoteState.writeDefaultValues = writeDefault;
                remoteStates[value] = remoteState;
            }

            localSm.defaultState = localStates[0];
            remoteSm.defaultState = remoteStates[0];
            rootSm.defaultState = localStates[0];

            for (int value = 0; value <= maxValue; value++)
            {
                var localTransition = rootSm.AddAnyStateTransition(localStates[value]);
                localTransition.hasExitTime = false;
                localTransition.exitTime = 0f;
                localTransition.duration = 0f;
                localTransition.canTransitionToSelf = false;
                localTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
                if (value == 0)
                    localTransition.AddCondition(AnimatorConditionMode.Less, 1, paramName);  // paramName < 1
                else if (value == maxValue)
                    localTransition.AddCondition(AnimatorConditionMode.Greater, maxValue - 1, paramName);  // paramName > maxValue-1
                else
                    localTransition.AddCondition(AnimatorConditionMode.Equals, value, paramName);

                var remoteTransition = rootSm.AddAnyStateTransition(remoteStates[value]);
                remoteTransition.hasExitTime = false;
                remoteTransition.exitTime = 0f;
                remoteTransition.duration = 0f;
                remoteTransition.canTransitionToSelf = false;
                remoteTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
                for (int b = 0; b < bitCount; b++)
                {
                    // paramName は呼び出し側から渡される int パラメータ名（{名前}_Int）
                    var syncParamName = $"SUM/ResizableSync/{paramName}/{b}";
                    var boolVal = ((value >> b) & 1) != 0;
                    remoteTransition.AddCondition(boolVal ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, syncParamName);
                }
            }

            var layer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = rootSm
            };

            return layer;
        }

        private static AnimatorControllerLayer CreateRangeConvertLayer(ResizableSyncParameters.SyncParamSetting[] settings, bool writeDefault)
        {
            if (settings.Length == 0) return null;

            var emptyMotion = LoadEmptyMotion();
            var rootSm = new AnimatorStateMachine { name = "RangeConvert" };

            var brunchState = rootSm.AddState("Brunch", new Vector3(300, 120, 0));
            brunchState.motion = emptyMotion;
            brunchState.writeDefaultValues = writeDefault;

            var localState = rootSm.AddState("Local", new Vector3(180, 180, 0));
            localState.motion = emptyMotion;
            localState.writeDefaultValues = writeDefault;

            var remoteState = rootSm.AddState("Remote", new Vector3(420, 180, 0));
            remoteState.motion = emptyMotion;
            remoteState.writeDefaultValues = writeDefault;

            rootSm.defaultState = brunchState;

            var toLocal = brunchState.AddTransition(localState);
            toLocal.hasExitTime = false;
            toLocal.duration = 0f;
            toLocal.canTransitionToSelf = false;
            toLocal.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");

            var toRaw = brunchState.AddTransition(remoteState);
            toRaw.hasExitTime = false;
            toRaw.duration = 0f;
            toRaw.canTransitionToSelf = false;
            toRaw.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");

            var localSelfTransition = localState.AddTransition(localState);
            localSelfTransition.hasExitTime = false;
            localSelfTransition.duration = 0f;
            localSelfTransition.exitTime = 0f;
            localSelfTransition.canTransitionToSelf = true;
            localSelfTransition.AddCondition(AnimatorConditionMode.If, 0, "dummy");

            var remoteSelfTransition = remoteState.AddTransition(remoteState);
            remoteSelfTransition.hasExitTime = false;
            remoteSelfTransition.duration = 0f;
            remoteSelfTransition.exitTime = 0f;
            remoteSelfTransition.canTransitionToSelf = true;
            remoteSelfTransition.AddCondition(AnimatorConditionMode.If, 0, "dummy");

            return new AnimatorControllerLayer
            {
                name = "RangeConvert",
                defaultWeight = 1f,
                stateMachine = rootSm
            };
        }

        private static void AddRangeConvertParamDrivers(AnimatorController controller, ResizableSyncParameters.SyncParamSetting[] settings,
            Type paramDriverType)
        {
            if (settings.Length == 0 || paramDriverType == null) return;

            foreach (var layer in controller.layers)
            {
                if (layer.name != "RangeConvert") continue;

                foreach (var state in layer.stateMachine.states)
                {
                    var stateName = state.state.name;
                    if (stateName == "Local")
                    {
                        foreach (var setting in settings)
                        {
                            var bitCount = GetBitCount(setting);
                            var maxValue = (1 << bitCount) - 1;
                            var paramName = GetParamName(setting);
                            var intParamName = $"{paramName}_Int";
                            var (inputMin, inputMax, syncMin, syncMax) = GetInputToSyncRanges(setting, maxValue);

                            var behaviour = state.state.AddStateMachineBehaviour(paramDriverType);
                            if (behaviour == null) continue;

                            EnsureSubAsset(behaviour, controller);
                            SetParamDriverCopy(behaviour, paramName, intParamName, inputMin, inputMax, syncMin, syncMax, clearFirst: true);

                            if (setting.paramType == ResizableSyncParameters.ParamType.Float)
                            {
                                var (destMin, destMax, sourceMin, sourceMax) = GetSyncToOutputRanges(setting, maxValue);
                                SetParamDriverCopy(behaviour, intParamName, $"{paramName}_Snapped", sourceMin, sourceMax, destMin, destMax, clearFirst: false);
                            }
                        }
                    }
                    else if (stateName == "Remote")
                    {
                        foreach (var setting in settings)
                        {
                            var bitCount = GetBitCount(setting);
                            var maxValue = (1 << bitCount) - 1;
                            var paramName = GetParamName(setting);
                            var intParamName = $"{paramName}_Int";
                            var (outputMin, outputMax) = GetSourceRange(setting);
                            var (destMin, destMax, sourceMin, sourceMax) = GetSyncToOutputRanges(setting, maxValue);

                            var behaviour = state.state.AddStateMachineBehaviour(paramDriverType);
                            if (behaviour == null) continue;

                            EnsureSubAsset(behaviour, controller);

                            if (setting.paramType == ResizableSyncParameters.ParamType.Float)
                            {
                                SetParamDriverCopy(behaviour, intParamName, $"{paramName}_Snapped", sourceMin, sourceMax, destMin, destMax, clearFirst: true);
                                SetParamDriverCopy(behaviour, $"{paramName}_Snapped", paramName, destMin, destMax, outputMin, outputMax, clearFirst: false);
                            }
                            else
                            {
                                SetParamDriverCopy(behaviour, intParamName, paramName, sourceMin, sourceMax, outputMin, outputMax, clearFirst: true);
                            }
                        }
                    }
                }
            }
        }

        private static string GetParamName(ResizableSyncParameters.SyncParamSetting setting)
            => ResizableSyncParameters.GetParamName(setting);

        private static (float min, float max) GetSourceRange(ResizableSyncParameters.SyncParamSetting setting)
        {
            if (setting.paramType == ResizableSyncParameters.ParamType.Float)
            {
                switch (setting.floatRangePreset)
                {
                    case ResizableSyncParameters.FloatRangePreset.MinusOneToPlusOne:
                        return (-1f, 1f);
                    case ResizableSyncParameters.FloatRangePreset.ZeroToPlusOne:
                        return (0f, 1f);
                    case ResizableSyncParameters.FloatRangePreset.Custom:
                        return GetFloatCustomRange(setting.customFloatMin, setting.customFloatMax);
                }
            }
            else
            {
                return GetIntSourceRange(setting);
            }

            return (0f, 1f);
        }

        private static (float min, float max) GetIntSourceRange(ResizableSyncParameters.SyncParamSetting setting)
        {
            var span = ResizableSyncParameters.GetIntRangeSpan(setting);
            var min = setting.intRangePreset == ResizableSyncParameters.IntRangePreset.FromZero
                ? 0
                : setting.customIntMin;
            return (min, min + span - 1);
        }

        private static (float min, float max) GetFloatCustomRange(float min, float max)
        {
            if (min >= max)
                max = min + 0.0001f;
            return (min, max);
        }

        /// <summary>
        /// ユーザー入力レンジから同期用 Int レンジへの変換範囲。
        /// </summary>
        private static (float inputMin, float inputMax, float syncMin, float syncMax) GetInputToSyncRanges(
            ResizableSyncParameters.SyncParamSetting setting, int maxValue)
        {
            var (rangeMin, rangeMax) = GetSourceRange(setting);
            if (setting.paramType == ResizableSyncParameters.ParamType.Int)
                return (rangeMin, rangeMax, 0f, maxValue);

            // 偶数分割: maxValue+1 等分のビン中心へ写像（端から半ステップ内側）
            if (setting.divisionType == ResizableSyncParameters.DivisionType.Even)
            {
                var step = (rangeMax - rangeMin) / (maxValue + 1f);
                return (rangeMin + step * 0.5f, rangeMax - step * 0.5f, 0f, maxValue);
            }

            // 奇数分割: 端点を含む線形写像
            return (rangeMin, rangeMax, 0f, maxValue);
        }

        /// <summary>
        /// 同期用 Int レンジから出力レンジへの変換範囲。
        /// </summary>
        private static (float destMin, float destMax, float sourceMin, float sourceMax) GetSyncToOutputRanges(
            ResizableSyncParameters.SyncParamSetting setting, int maxValue)
        {
            var (rangeMin, rangeMax) = GetSourceRange(setting);
            if (setting.paramType == ResizableSyncParameters.ParamType.Int)
                return (rangeMin, rangeMax, 0f, maxValue);

            // 偶数分割: maxValue+1 等分のビン中心へ写像（端から半ステップ内側）
            if (setting.divisionType == ResizableSyncParameters.DivisionType.Even)
            {
                var step = (rangeMax - rangeMin) / (maxValue + 1f);
                return (rangeMin + step * 0.5f, rangeMax - step * 0.5f, 0f, maxValue);
            }

            // 奇数分割: 端点を含む線形写像
            return (rangeMin, rangeMax, 0f, maxValue);
        }

        private static void SetParamDriverCopy(StateMachineBehaviour behaviour, string sourceName, string destName, float sourceMin, float sourceMax, float destMin, float destMax, bool clearFirst = true)
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

        private static void AddParamDriverBehaviours(AnimatorController controller, string paramName, int bitCount, int maxValue,
            Type paramDriverType)
        {
            if (paramDriverType == null || !typeof(StateMachineBehaviour).IsAssignableFrom(paramDriverType)) return;

            var layerName = $"Convert_IntParam{paramName}{bitCount}bit";
            foreach (var layer in controller.layers)
            {
                if (layer.name != layerName) continue;

                foreach (var childSm in layer.stateMachine.stateMachines)
                {
                    var isLocal = childSm.stateMachine.name == "Local";
                    var states = childSm.stateMachine.states;

                    for (int i = 0; i < states.Length; i++)
                    {
                        var value = i;
                        var state = states[i].state;

                        var behaviour = state.AddStateMachineBehaviour(paramDriverType);
                        if (behaviour != null)
                        {
                            EnsureSubAsset(behaviour, controller);

                            var boolValues = new bool[bitCount];
                            for (int b = 0; b < bitCount; b++)
                                boolValues[b] = ((value >> b) & 1) != 0;

                            SetParamDriverParameters(behaviour, paramName, value, boolValues, isLocal);
                        }
                    }
                }
            }
        }

        private static void SetParamDriverParameters(StateMachineBehaviour behaviour, string paramName, int intValue, bool[] boolValues, bool isLocal)
        {
            var so = new SerializedObject(behaviour);
            var parametersProp = so.FindProperty("parameters");
            if (parametersProp == null) return;

            parametersProp.ClearArray();

            if (isLocal)
            {
                for (int i = 0; i < boolValues.Length; i++)
                {
                    // paramName は int パラメータ名（{名前}_Int）
                    var syncParamName = $"SUM/ResizableSync/{paramName}/{i}";
                    parametersProp.InsertArrayElementAtIndex(i);
                    SetParamDriverEntry(parametersProp.GetArrayElementAtIndex(i), syncParamName, boolValues[i] ? 1 : 0);
                }
            }
            else
            {
                parametersProp.InsertArrayElementAtIndex(0);
                SetParamDriverEntry(parametersProp.GetArrayElementAtIndex(0), paramName, intValue);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetParamDriverEntry(SerializedProperty entry, string paramName, int value)
        {
            entry.FindPropertyRelative("name").stringValue = paramName;

            var valueProp = entry.FindPropertyRelative("value");
            if (valueProp != null)
            {
                if (valueProp.propertyType == SerializedPropertyType.Float)
                    valueProp.floatValue = value;
                else
                    valueProp.intValue = value;
            }

            var typeProp = entry.FindPropertyRelative("type");
            if (typeProp != null && typeProp.propertyType == SerializedPropertyType.Enum)
                typeProp.enumValueIndex = 0; // 0 = Set (enum は enumValueIndex のみ使用可能)

            var sourceProp = entry.FindPropertyRelative("source");
            if (sourceProp != null) sourceProp.stringValue = "";
            var valueMinProp = entry.FindPropertyRelative("valueMin");
            if (valueMinProp != null) valueMinProp.floatValue = 0f;
            var valueMaxProp = entry.FindPropertyRelative("valueMax");
            if (valueMaxProp != null) valueMaxProp.floatValue = 0f;
        }

        private static AnimationClip LoadEmptyMotion()
        {
            var path = AssetDatabase.GUIDToAssetPath(EmptyMotionGUID);
            if (!string.IsNullOrEmpty(path))
            {
                var loadedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (loadedClip != null) return loadedClip;
            }

            if (!Directory.Exists(GeneratedFolder))
                Directory.CreateDirectory(GeneratedFolder);

            var clipPath = $"{GeneratedFolder}/ResizableSyncParameters_Empty.anim";
            var emptyClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (emptyClip == null)
            {
                emptyClip = new AnimationClip();
                AssetDatabase.CreateAsset(emptyClip, clipPath);
                AssetDatabase.SaveAssets();
            }
            return emptyClip;
        }

        private static Type GetVRCAvatarParameterDriverType()
        {
            return typeof(VRCAvatarParameterDriver);
        }

        private static void AddModularAvatarModule(GameObject parentObject, AnimatorController controller,
            List<(string name, ParameterSyncType syncType)> paramNamesToRegister)
        {
            var moduleRoot = ModularAvatarMergeAnimatorUtility.RegisterMergeAnimatorModule(
                parentObject,
                "ResizableSyncParameters_Module",
                controller,
                layerPriority: 0,
                matchAvatarWriteDefaults: false);
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
                    localOnly = false,
                    defaultValue = 0f,
                    saved = false,
                    hasExplicitDefaultValue = true
                });
            }

            EditorUtility.SetDirty(moduleRoot);
        }
    }
}
