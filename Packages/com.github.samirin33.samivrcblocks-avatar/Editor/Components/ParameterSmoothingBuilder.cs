using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using Samirin33.NDMF.Base.Plugin;
using Samirin33.NDMF.Components;
using Samirin33.NDMF.Module;

namespace Samirin33.NDMF.Components.Editor
{
    public static class ParameterSmoothingBuilder
    {
        [InitializeOnLoadMethod]
        private static void RegisterBuilder()
        {
            SamirinMABaseSingleBuildRegistry.Register<ParameterSmoothing>(Build);
        }

        private const string EmptyMotionGUID = "4de039275b65be24c8f0a641d7a44924";
        private const string FPSCounterGUID = "9b06db4aacbe94745a2bcd84f67103eb";
        private static string GeneratedFolder => "Assets/Generated/SamiVRCBlocks/ParameterSmoothing";

        public static void Build(GameObject avatarRootObject, params ParameterSmoothing[] parameterSmoothings)
        {
            BuildInternal(avatarRootObject, ensureFpsCounterModule: true, parameterSmoothings);
        }

        /// <summary>
        /// インスペクターからのマニュアル生成。ExtendedParameters（FPSCounter）は付けない。
        /// </summary>
        public static AnimatorController[] BuildManual(GameObject avatarRootObject, params ParameterSmoothing[] parameterSmoothings)
        {
            return BuildInternal(avatarRootObject, ensureFpsCounterModule: false, parameterSmoothings);
        }

        private static AnimatorController[] BuildInternal(GameObject avatarRootObject, bool ensureFpsCounterModule,
            params ParameterSmoothing[] parameterSmoothings)
        {
            if (parameterSmoothings == null || parameterSmoothings.Length == 0)
                return Array.Empty<AnimatorController>();

            var standaloneComponents = parameterSmoothings
                .Where(c => c != null && !IsHandledByResizableSyncParameters(c))
                .ToArray();
            if (standaloneComponents.Length == 0)
                return Array.Empty<AnimatorController>();

            var mergedInfos = MergeParameterSmoothingData(standaloneComponents);
            if (mergedInfos.Count == 0)
                return Array.Empty<AnimatorController>();

            var moduleParent = standaloneComponents.FirstOrDefault()?.gameObject;
            var controller = BuildFromInfos(avatarRootObject, mergedInfos.ToArray(), moduleParent);

            if (ensureFpsCounterModule)
            {
                foreach (var component in standaloneComponents)
                    EnsureFPSCounterModule(component.gameObject);
            }

            return controller != null
                ? new[] { controller }
                : Array.Empty<AnimatorController>();
        }

        private static bool IsHandledByResizableSyncParameters(ParameterSmoothing component)
        {
            var resizableSyncParam = component.GetComponent<ResizableSyncParameters>();
            if (resizableSyncParam?.syncParamSettings == null)
                return false;

            return resizableSyncParam.syncParamSettings.Any(s => s.paramType == ResizableSyncParameters.ParamType.Float);
        }

        public static void EnsureFPSCounterModule(GameObject targetObject)
        {
            if (targetObject == null) return;

            var fpsCounterPath = AssetDatabase.GUIDToAssetPath(FPSCounterGUID);
            var fpsCounter = !string.IsNullOrEmpty(fpsCounterPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(fpsCounterPath)
                : null;
            if (fpsCounter == null)
            {
                Debug.LogError($"[ParameterSmoothing] FPSCounter not found (GUID: {FPSCounterGUID}, path: {fpsCounterPath})");
                return;
            }

            var extendedParameters = targetObject.GetComponent<ExtendedParameters>();
            if (extendedParameters == null)
                extendedParameters = targetObject.AddComponent<ExtendedParameters>();

            // 既存の parameterPrefabs を消さず FPSCounter を追加する
            var list = new System.Collections.Generic.List<GameObject>();
            if (extendedParameters.parameterPrefabs != null)
            {
                foreach (var prefab in extendedParameters.parameterPrefabs)
                {
                    if (prefab != null && !list.Contains(prefab))
                        list.Add(prefab);
                }
            }
            if (!list.Contains(fpsCounter))
                list.Add(fpsCounter);
            extendedParameters.parameterPrefabs = list.ToArray();
            EditorUtility.SetDirty(targetObject);
        }

        private const string ResizableSyncSmoothingModuleName = "ResizableSyncParameters_Smoothing_Module";
        private const string StandaloneSmoothingModuleName = "ParameterSmoothing_Module";

        public static AnimatorController BuildFromResizableSyncParameters(GameObject avatarRootObject,
            ParameterSmoothing.ParameterSmoothingInfo[] infos, GameObject moduleParent)
        {
            return BuildFromInfos(avatarRootObject, infos, moduleParent, ResizableSyncSmoothingModuleName, fromResizableSyncParameters: true);
        }

        public static AnimatorController BuildFromInfos(GameObject avatarRootObject, ParameterSmoothing.ParameterSmoothingInfo[] infos,
            GameObject moduleParent = null, string moduleObjectName = StandaloneSmoothingModuleName,
            bool fromResizableSyncParameters = false)
        {
            if (avatarRootObject == null || infos == null || infos.Length == 0)
                return null;

            var controller = CreateControllerFromParameterSmoothingData(infos, out var paramNamesToRegister, fromResizableSyncParameters);
            if (controller == null)
            {
                Debug.LogError("[ParameterSmoothing] Animator Controller の生成に失敗しました。MA Merge Animator は登録されません。");
                return null;
            }

            AddModularAvatarModule(moduleParent ?? avatarRootObject, controller, paramNamesToRegister, moduleObjectName);
            return controller;
        }

        private static string GetSmoothedParamName(ParameterSmoothing.ParameterSmoothingInfo info)
            => string.IsNullOrEmpty(info.smoothedParameterName) ? $"{info.parameterName}_Smoothed" : info.smoothedParameterName;

        /// <summary>
        /// 同じ SmoothWeight 値は同一の FixedSmoothWeight パラメーターを共有する。
        /// </summary>
        private static string GetFixedWeightParamName(float smoothWeight)
        {
            var rounded = Mathf.Round(smoothWeight * 1_000_000f) / 1_000_000f;
            return $"FixedSmoothWeight_{rounded:0.######}";
        }

        private static List<ParameterSmoothing.ParameterSmoothingInfo> MergeParameterSmoothingData(
            ParameterSmoothing[] parameterSmoothings)
        {
            var processedParamNames = new HashSet<string>(StringComparer.Ordinal);
            var merged = new List<ParameterSmoothing.ParameterSmoothingInfo>();

            foreach (var component in parameterSmoothings)
            {
                if (component.parameterSmoothingData == null) continue;

                foreach (var info in component.parameterSmoothingData)
                {
                    if (string.IsNullOrEmpty(info.parameterName)) continue;
                    if (processedParamNames.Contains(info.parameterName)) continue;

                    processedParamNames.Add(info.parameterName);
                    merged.Add(new ParameterSmoothing.ParameterSmoothingInfo
                    {
                        parameterName = info.parameterName,
                        useDefaultSmoothWeight = false,
                        smoothWeight = info.GetEffectiveSmoothWeight(component.defaultSmoothWeight),
                        smoothedParameterName = info.smoothedParameterName
                    });
                }
            }

            return merged;
        }

        private static AnimatorController CreateControllerFromParameterSmoothingData(
            ParameterSmoothing.ParameterSmoothingInfo[] infos,
            out List<(string name, ParameterSyncType syncType)> paramNamesToRegister,
            bool fromResizableSyncParameters = false)
        {
            paramNamesToRegister = new List<(string, ParameterSyncType)>();

            if (!Directory.Exists(GeneratedFolder))
                Directory.CreateDirectory(GeneratedFolder);

            var controllerPath = fromResizableSyncParameters
                ? $"{GeneratedFolder}/ResizableSyncParameters_Smoothing_Generated.controller"
                : $"{GeneratedFolder}/ParameterSmoothing_Generated.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null)
                return null;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "ConstOne",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });
            controller.AddParameter("FPS/Value", AnimatorControllerParameterType.Float);
            paramNamesToRegister.Add(("FPS/Value", ParameterSyncType.Float));

            var addedFixedWeightParams = new HashSet<string>(StringComparer.Ordinal);

            foreach (var info in infos)
            {
                var paramName = info.parameterName;
                var smoothedParamName = GetSmoothedParamName(info);
                var fixedWeightParamName = GetFixedWeightParamName(info.smoothWeight);

                controller.AddParameter(paramName, AnimatorControllerParameterType.Float);
                controller.AddParameter(smoothedParamName, AnimatorControllerParameterType.Float);

                if (addedFixedWeightParams.Add(fixedWeightParamName))
                    controller.AddParameter(fixedWeightParamName, AnimatorControllerParameterType.Float);

                if (!fromResizableSyncParameters)
                {
                    paramNamesToRegister.Add((paramName, ParameterSyncType.Float));
                    paramNamesToRegister.Add((smoothedParamName, ParameterSyncType.Float));
                }
            }

            if (!fromResizableSyncParameters)
            {
                foreach (var fixedWeightParamName in addedFixedWeightParams)
                    paramNamesToRegister.Add((fixedWeightParamName, ParameterSyncType.Float));
            }

            controller.RemoveLayer(0);

            var layer = CreateParameterSmoothingLayer(infos, controller);
            controller.AddLayer(layer);

            AnimatorControllerAssetUtility.RegisterControllerHierarchy(controller);

            EditorUtility.SetDirty(controller);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            return ModularAvatarMergeAnimatorUtility.ReloadControllerAtPath(controllerPath);
        }

        private static AnimatorControllerLayer CreateParameterSmoothingLayer(
            ParameterSmoothing.ParameterSmoothingInfo[] infos,
            AnimatorController controller)
        {
            var rootSm = new AnimatorStateMachine { name = "ParameterSmoothing" };
            var smoothingState = rootSm.AddState("Smoothing", new Vector3(280, 120, 0));
            smoothingState.writeDefaultValues = true;
            rootSm.defaultState = smoothingState;

            var rootBlendTree = CreateRootDirectBlendTree(infos, controller);
            smoothingState.motion = rootBlendTree;

            // State の Time Parameter を FPS/Value に設定
            var stateSo = new SerializedObject(smoothingState);
            var timeParamProp = stateSo.FindProperty("m_TimeParameterActive");
            if (timeParamProp != null) timeParamProp.boolValue = true;
            var timeParamNameProp = stateSo.FindProperty("m_TimeParameter");
            if (timeParamNameProp != null) timeParamNameProp.stringValue = "FPS/Value";
            stateSo.ApplyModifiedPropertiesWithoutUndo();

            return new AnimatorControllerLayer
            {
                name = "ParameterSmoothing",
                defaultWeight = 1f,
                stateMachine = rootSm
            };
        }

        /// <summary>
        /// ParamSmooth.controller のルートと同じ構造: Direct, 第一パラメータでブレンド。
        /// Child 0: WeightFix (空・後で指示), Child 1: 第一パラメータの Smoothing ツリー。
        /// 複数パラメータ時は 0, 0.5, 1 等で各パラメータの Smoothing ツリーを追加。
        /// </summary>
        private static BlendTree CreateRootDirectBlendTree(
            ParameterSmoothing.ParameterSmoothingInfo[] infos,
            AnimatorController controller)
        {
            var firstParamName = infos[0].parameterName;
            var directTree = new BlendTree
            {
                name = "Blend Tree",
                blendType = BlendTreeType.Direct,
                blendParameter = firstParamName,
                blendParameterY = firstParamName,
                useAutomaticThresholds = true,
                minThreshold = 0f,
                maxThreshold = 1f
            };
            AssetDatabase.AddObjectToAsset(directTree, controller);

            // WeightFix: 数式に従ってキーフレームを生成した Clip を配置（SampleRate 255, フレーム 1~255、全パラメータ分のカーブ）
            var weightFixClip = CreateWeightFixClip(infos, controller);
            directTree.AddChild(weightFixClip, 0f);

            for (int i = 0; i < infos.Length; i++)
            {
                var paramSmoothingTree = CreateParamSmoothingBlendTree(infos[i], controller);
                var threshold = infos.Length == 1 ? 1f : (i == 0 ? 0.5f : 1f);
                directTree.AddChild(paramSmoothingTree, threshold);
            }

            SetDirectBlendTreeChildrenParameter(directTree, "ConstOne");

            return directTree;
        }

        private const int WeightFixSampleRate = 255;
        private const int WeightFixFrameStart = 1;
        private const int WeightFixFrameEnd = 255;

        /// <summary>
        /// WeightFix 用クリップを生成。SampleRate 255、フレーム 1~255 にキーフレーム。
        /// 全パラメータ分の AnimatorFloat カーブを追加。
        /// value = (1 - Mathf.Pow((1/targetWeight), (1/(256 - frameIndex))))
        /// </summary>
        private static AnimationClip CreateWeightFixClip(
            ParameterSmoothing.ParameterSmoothingInfo[] infos,
            AnimatorController controller)
        {
            var clip = new AnimationClip();
            clip.frameRate = WeightFixSampleRate;

            var keyframeCount = WeightFixFrameEnd - WeightFixFrameStart + 1;
            var processedFixedWeightParams = new HashSet<string>(StringComparer.Ordinal);

            foreach (var info in infos)
            {
                var fixedWeightParamName = GetFixedWeightParamName(info.smoothWeight);
                if (!processedFixedWeightParams.Add(fixedWeightParamName))
                    continue;

                var inputWeight = Mathf.Max(info.smoothWeight, 0.01f) * 0.01f;
                var targetWeight = Mathf.Max(inputWeight * inputWeight, 0.0001f);

                var keyframes = new Keyframe[keyframeCount];
                for (int i = 0; i < keyframeCount; i++)
                {
                    int frameIndex = WeightFixFrameStart + i;
                    float time = frameIndex / (float)WeightFixSampleRate;
                    float value = 2f - Mathf.Pow(1f / targetWeight, 1f / frameIndex);
                    keyframes[i] = new Keyframe(time, value);
                }

                var curve = new AnimationCurve(keyframes);
                // curve は Unity によりキーがマージされることがあるため、実際のキー数でループする
                for (int i = 0; i < curve.keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }

                var binding = new EditorCurveBinding
                {
                    path = "",
                    type = typeof(Animator),
                    propertyName = fixedWeightParamName
                };
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            clip.name = "WeightFix";
            AssetDatabase.AddObjectToAsset(clip, controller);
            return clip;
        }

        /// <summary>
        /// 「Hoge Smoothing」相当: FixedSmoothWeight で Raw と Smoothed をブレンド。
        /// </summary>
        private static BlendTree CreateParamSmoothingBlendTree(
            ParameterSmoothing.ParameterSmoothingInfo info,
            AnimatorController controller)
        {
            var paramName = info.parameterName;
            var smoothedParamName = GetSmoothedParamName(info);
            var fixedWeightParamName = GetFixedWeightParamName(info.smoothWeight);

            var clip0 = CreateEmbeddedParamClip(smoothedParamName, 0f, controller);
            var clip1 = CreateEmbeddedParamClip(smoothedParamName, 1f, controller);

            var rawTree = CreateRawBlendTree(paramName, smoothedParamName, clip0, clip1, controller);
            var smoothedTree = CreateSmoothedBlendTree(paramName, smoothedParamName, clip0, clip1, controller);

            var tree = new BlendTree
            {
                name = $"{paramName} Smoothing",
                blendType = BlendTreeType.Simple1D,
                blendParameter = fixedWeightParamName,
                useAutomaticThresholds = true,
                minThreshold = 0f,
                maxThreshold = 1f
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(rawTree, 0f);
            tree.AddChild(smoothedTree, 1f);
            return tree;
        }

        private static BlendTree CreateRawBlendTree(string paramName, string smoothedParamName,
            Motion clip0, Motion clip1, AnimatorController controller)
        {
            var tree = new BlendTree
            {
                name = "Raw",
                blendType = BlendTreeType.Simple1D,
                blendParameter = paramName,
                useAutomaticThresholds = true,
                minThreshold = 0f,
                maxThreshold = 1f
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(clip0, 0f);
            tree.AddChild(clip1, 1f);
            return tree;
        }

        private static BlendTree CreateSmoothedBlendTree(string paramName, string smoothedParamName,
            Motion clip0, Motion clip1, AnimatorController controller)
        {
            var tree = new BlendTree
            {
                name = "Smoothed",
                blendType = BlendTreeType.Simple1D,
                blendParameter = smoothedParamName,
                useAutomaticThresholds = true,
                minThreshold = 0f,
                maxThreshold = 1f
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(clip0, 0f);
            tree.AddChild(clip1, 1f);
            return tree;
        }

        private static void SetDirectBlendTreeChildrenParameter(BlendTree blendTree, string parameterName)
        {
            var so = new SerializedObject(blendTree);
            var childrenProp = so.FindProperty("m_Childs");
            if (childrenProp == null) return;

            for (int i = 0; i < childrenProp.arraySize; i++)
            {
                var childProp = childrenProp.GetArrayElementAtIndex(i);
                var directBlendProp = childProp.FindPropertyRelative("m_DirectBlendParameter");
                if (directBlendProp != null)
                    directBlendProp.stringValue = parameterName;

                var motionProp = childProp.FindPropertyRelative("m_Motion");
                if (motionProp != null && motionProp.objectReferenceValue is BlendTree childTree)
                    SetDirectBlendTreeChildrenParameter(childTree, parameterName);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimationClip CreateEmbeddedParamClip(string paramName, float value,
            AnimatorController controller)
        {
            var clip = CreateAnimatorParamClip(paramName, value);
            clip.name = $"{paramName} {value}";
            AssetDatabase.AddObjectToAsset(clip, controller);
            return clip;
        }

        private static AnimationClip CreateAnimatorParamClip(string paramName, float value)
        {
            var clip = new AnimationClip();
            var curve = AnimationCurve.Constant(0f, 0f, value);
            var binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(Animator),
                propertyName = paramName
            };
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            return clip;
        }

        private static void AddModularAvatarModule(GameObject parentObject, AnimatorController controller,
            List<(string name, ParameterSyncType syncType)> paramNamesToRegister, string moduleObjectName)
        {
            var moduleRoot = ModularAvatarMergeAnimatorUtility.RegisterMergeAnimatorModule(
                parentObject,
                moduleObjectName,
                controller,
                layerPriority: 0,
                matchAvatarWriteDefaults: false);
            if (moduleRoot == null)
            {
                Debug.LogError("[ParameterSmoothing] MA Merge Animator の登録に失敗しました。Animator Controller の参照を確認してください。");
                return;
            }

            if (paramNamesToRegister == null || paramNamesToRegister.Count == 0)
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
