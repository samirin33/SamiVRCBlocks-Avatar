#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Samirin33.NDMF.Components
{
    public static class AudioClipReplacerUtil
    {
        public const string GeneratedRoot = "Assets/Generated/SamirinVRCUtility/AudioClipReplacer";

        public static bool TryProcess(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            out RuntimeAnimatorController generatedController)
        {
            generatedController = null;

            if (replacer == null)
                return false;

            var overrides = replacer.overrides;
            if (overrides == null || overrides.Length == 0)
                return false;

            var appliedAny = false;

            foreach (var entry in overrides)
            {
                if (entry == null || !entry.enabled)
                    continue;

                if (entry.target != AudioClipOverrideTarget.AudioSource)
                    continue;

                if (ApplyOverrideToAudioSource(entry, replacer))
                    appliedAny = true;
            }

            var hasStateOverrides = overrides.Any(entry =>
                entry != null
                && entry.enabled
                && entry.target == AudioClipOverrideTarget.AnimatorState
                && GetStateNames(entry).Any());

            if (!hasStateOverrides)
                return appliedAny;

            if (!TryResolveSource(
                    replacer,
                    avatarRoot,
                    out var sourceController,
                    out var sourceOverride,
                    out var saveSuffix))
            {
                return appliedAny;
            }

            if (!TryCloneController(sourceController, saveSuffix, out var clone, out var assetPath))
            {
                Debug.LogError(
                    $"[AudioClipReplacer] Failed to clone controller: {sourceController.name}",
                    replacer);
                return appliedAny;
            }

            var appliedStateAny = false;
            foreach (var entry in overrides)
            {
                if (entry == null || !entry.enabled)
                    continue;

                if (entry.target != AudioClipOverrideTarget.AnimatorState)
                    continue;

                var stateNames = GetStateNames(entry).ToList();
                if (stateNames.Count == 0)
                    continue;

                var entryApplied = false;
                foreach (var stateName in stateNames)
                {
                    var states = FindStates(clone, stateName).ToList();
                    if (states.Count == 0)
                    {
                        Debug.LogWarning(
                            $"[AudioClipReplacer] State not found: \"{stateName}\" in {sourceController.name}",
                            replacer);
                        continue;
                    }

                    foreach (var state in states)
                    {
                        if (ApplyOverrideToState(state, entry))
                            entryApplied = true;
                    }
                }

                if (entryApplied)
                    appliedStateAny = true;
            }

            if (!appliedStateAny)
            {
                AssetDatabase.DeleteAsset(assetPath);
                return appliedAny;
            }

            FinalizeGeneratedController(clone, assetPath);
            var reloadedClone = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (reloadedClone == null)
            {
                Debug.LogError(
                    $"[AudioClipReplacer] Failed to reload generated controller: {assetPath}",
                    replacer);
                return appliedAny;
            }

            if (sourceOverride != null)
            {
                if (!TryCloneOverrideController(
                        sourceOverride,
                        reloadedClone,
                        saveSuffix,
                        out var clonedOverride,
                        out _))
                {
                    Debug.LogError(
                        $"[AudioClipReplacer] Failed to clone override controller: {sourceOverride.name}",
                        replacer);
                    return appliedAny;
                }

                generatedController = clonedOverride;
            }
            else
            {
                generatedController = reloadedClone;
            }

            if (TryApplyTarget(replacer, avatarRoot, generatedController))
                appliedAny = true;

            return appliedAny;
        }

        public static bool TryApplyDefaultsFromSource(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            AudioClipOverride entry,
            out string message)
        {
            message = null;

            if (replacer == null || entry == null)
            {
                message = "設定が不正です。";
                return false;
            }

            if (entry.target == AudioClipOverrideTarget.AudioSource)
                return TryApplyDefaultsFromAudioSource(entry, out message);

            if (!TryResolveSource(replacer, avatarRoot, out var sourceController, out _, out _))
            {
                message = "複製元の AnimatorController を取得できません。開発者向け設定を確認してください。";
                return false;
            }

            var stateNames = GetStateNames(entry).ToList();
            if (stateNames.Count == 0)
            {
                message = "検索ステート名が未設定です。開発者向け設定でステート名を指定してください。";
                return false;
            }

            if (!TryFindPlayAudio(sourceController, stateNames, out var playAudio, out var foundStateName))
            {
                message =
                    $"VRC Animator Play Audio が見つかりませんでした。（{string.Join(", ", stateNames)}）";
                return false;
            }

            entry.clips = playAudio.Clips != null
                ? playAudio.Clips.Where(c => c != null).ToArray()
                : Array.Empty<AudioClip>();
            entry.overrideVolume = true;
            entry.volume = playAudio.Volume;
            entry.overridePitch = true;
            entry.pitch = playAudio.Pitch;
            entry.overridePlaybackOrder = true;
            entry.playbackOrder = playAudio.PlaybackOrder;
            entry.playbackOrderApplySettings = playAudio.ClipsApplySettings;
            entry.overrideDelayInSeconds = true;
            entry.delayInSeconds = playAudio.DelayInSeconds;

            message = $"\"{foundStateName}\" の VRC Animator Play Audio から取得しました。";
            return true;
        }

        private static bool TryApplyDefaultsFromAudioSource(
            AudioClipOverride entry,
            out string message)
        {
            if (entry.audioSource == null)
            {
                message = "AudioSource が未設定です。開発者向け設定で対象を指定してください。";
                return false;
            }

            entry.clips = entry.audioSource.clip != null
                ? new[] { entry.audioSource.clip }
                : Array.Empty<AudioClip>();
            entry.overrideVolume = true;
            entry.volume = new Vector2(entry.audioSource.volume, entry.audioSource.volume);
            entry.overridePitch = true;
            entry.pitch = new Vector2(entry.audioSource.pitch, entry.audioSource.pitch);
            entry.overridePlaybackOrder = false;
            entry.overrideDelayInSeconds = false;

            message = $"AudioSource \"{entry.audioSource.name}\" から取得しました。";
            return true;
        }

        private static bool ApplyOverrideToAudioSource(
            AudioClipOverride entry,
            AudioClipReplacer replacer)
        {
            if (entry.audioSource == null)
            {
                Debug.LogWarning(
                    "[AudioClipReplacer] AudioSource が未設定です",
                    replacer);
                return false;
            }

            var changed = false;

            if (entry.clips != null && entry.clips.Length > 0 && entry.clips[0] != null)
            {
                entry.audioSource.clip = entry.clips[0];
                changed = true;
            }

            if (entry.overrideVolume)
            {
                entry.audioSource.volume = Mathf.Clamp01(entry.volume.x);
                changed = true;
            }

            if (entry.overridePitch)
            {
                entry.audioSource.pitch = entry.pitch.x;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(entry.audioSource);

            return changed;
        }

        private static bool TryFindPlayAudio(
            AnimatorController sourceController,
            IReadOnlyList<string> stateNames,
            out VRCAnimatorPlayAudio playAudio,
            out string foundStateName)
        {
            playAudio = null;
            foundStateName = null;

            foreach (var stateName in stateNames)
            {
                foreach (var state in FindStates(sourceController, stateName))
                {
                    foreach (var behaviour in state.behaviours)
                    {
                        if (behaviour is VRCAnimatorPlayAudio audio)
                        {
                            playAudio = audio;
                            foundStateName = stateName;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool TryCloneController(
            AnimatorController source,
            string saveSuffix,
            out AnimatorController clone,
            out string assetPath)
        {
            clone = null;
            assetPath = null;

            EnsureGeneratedFolder();

            var baseName = SanitizeFileName($"{source.name}_{saveSuffix}");
            assetPath = $"{GeneratedRoot}/{baseName}.controller";

            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogWarning(
                    $"[AudioClipReplacer] Source controller is not a project asset: {source.name}");
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            if (!AssetDatabase.CopyAsset(sourcePath, assetPath))
            {
                Debug.LogWarning(
                    $"[AudioClipReplacer] CopyAsset failed: {sourcePath} -> {assetPath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            clone = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (clone == null)
            {
                Debug.LogWarning(
                    $"[AudioClipReplacer] Failed to load cloned controller: {assetPath}");
            }

            return clone != null;
        }

        private static bool TryCloneOverrideController(
            AnimatorOverrideController source,
            AnimatorController newBaseController,
            string saveSuffix,
            out AnimatorOverrideController clone,
            out string assetPath)
        {
            clone = null;
            assetPath = null;

            EnsureGeneratedFolder();

            var baseName = SanitizeFileName($"{source.name}_{saveSuffix}");
            assetPath = $"{GeneratedRoot}/{baseName}.overrideController";

            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogWarning(
                    $"[AudioClipReplacer] Source override controller is not a project asset: {source.name}");
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            if (!AssetDatabase.CopyAsset(sourcePath, assetPath))
            {
                Debug.LogWarning(
                    $"[AudioClipReplacer] CopyAsset failed: {sourcePath} -> {assetPath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            clone = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath);
            if (clone == null)
            {
                Debug.LogWarning(
                    $"[AudioClipReplacer] Failed to load cloned override controller: {assetPath}");
                return false;
            }

            clone.runtimeAnimatorController = newBaseController;
            EditorUtility.SetDirty(clone);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            return true;
        }

        private static void FinalizeGeneratedController(AnimatorController controller, string assetPath)
        {
            if (controller == null || string.IsNullOrEmpty(assetPath))
                return;

            EditorUtility.SetDirty(controller);
            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null)
                    continue;

                MarkStateMachineHierarchyDirty(layer.stateMachine);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void MarkStateMachineHierarchyDirty(AnimatorStateMachine stateMachine)
        {
            EditorUtility.SetDirty(stateMachine);

            foreach (var child in stateMachine.states)
            {
                if (child.state == null)
                    continue;

                EditorUtility.SetDirty(child.state);
                foreach (var behaviour in child.state.behaviours)
                {
                    if (behaviour != null)
                        EditorUtility.SetDirty(behaviour);
                }
            }

            foreach (var child in stateMachine.stateMachines)
            {
                if (child.stateMachine != null)
                    MarkStateMachineHierarchyDirty(child.stateMachine);
            }
        }

        private static bool TryResolveSource(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            out AnimatorController sourceController,
            out AnimatorOverrideController sourceOverride,
            out string saveSuffix)
        {
            sourceController = null;
            sourceOverride = null;
            saveSuffix = replacer.name;

            RuntimeAnimatorController runtimeSource;

            switch (replacer.mode)
            {
                case AudioClipReplacerMode.Direct:
                    if (replacer.mergeAnimator == null)
                    {
                        Debug.LogWarning(
                            "[AudioClipReplacer] mergeAnimator is not assigned",
                            replacer);
                        return false;
                    }

                    var attachedAnimator = replacer.mergeAnimator.GetComponent<Animator>();
                    if (attachedAnimator == null)
                    {
                        Debug.LogWarning(
                            "[AudioClipReplacer] Merge Animator と同じ GameObject に Animator がありません",
                            replacer);
                        return false;
                    }

                    runtimeSource = attachedAnimator.runtimeAnimatorController;
                    if (runtimeSource == null)
                    {
                        Debug.LogWarning(
                            "[AudioClipReplacer] Merge Animator に付いている Animator の Controller が未設定です",
                            replacer);
                        return false;
                    }

                    replacer.sourceController = runtimeSource;
                    saveSuffix = $"{replacer.mergeAnimator.gameObject.name}_{replacer.name}";
                    break;

                case AudioClipReplacerMode.PlayableLayer:
                    if (!TryGetAvatarDescriptor(replacer, avatarRoot, out var descriptor))
                        return false;

                    runtimeSource = GetPlayableLayerController(descriptor, replacer.playableLayer);
                    if (runtimeSource == null)
                    {
                        Debug.LogWarning(
                            $"[AudioClipReplacer] No controller on Playable Layer: {replacer.playableLayer}",
                            replacer);
                        return false;
                    }

                    saveSuffix = $"{descriptor.name}_{replacer.playableLayer}_{replacer.name}";
                    break;

                default:
                    Debug.LogWarning(
                        $"[AudioClipReplacer] Unknown mode: {replacer.mode}",
                        replacer);
                    return false;
            }

            return TryUnwrapRuntimeController(replacer, runtimeSource, out sourceController, out sourceOverride);
        }

        private static bool TryUnwrapRuntimeController(
            AudioClipReplacer replacer,
            RuntimeAnimatorController runtimeSource,
            out AnimatorController sourceController,
            out AnimatorOverrideController sourceOverride)
        {
            sourceController = null;
            sourceOverride = null;

            if (runtimeSource is AnimatorController directController)
            {
                sourceController = directController;
                return true;
            }

            if (runtimeSource is AnimatorOverrideController overrideController)
            {
                sourceOverride = overrideController;
                var baseRuntime = overrideController.runtimeAnimatorController;
                if (baseRuntime == null)
                {
                    Debug.LogWarning(
                        $"[AudioClipReplacer] AnimatorOverrideController のベース Controller が未設定です: {overrideController.name}",
                        replacer);
                    return false;
                }

                if (baseRuntime is not AnimatorController baseController)
                {
                    Debug.LogWarning(
                        $"[AudioClipReplacer] AnimatorOverrideController のベースが AnimatorController ではありません: {baseRuntime.name}",
                        replacer);
                    return false;
                }

                sourceController = baseController;
                return true;
            }

            Debug.LogWarning(
                $"[AudioClipReplacer] サポート外の RuntimeAnimatorController 型です: {runtimeSource.GetType().Name}",
                replacer);
            return false;
        }

        private static bool TryApplyTarget(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            RuntimeAnimatorController generatedController)
        {
            switch (replacer.mode)
            {
                case AudioClipReplacerMode.Direct:
                    if (replacer.mergeAnimator == null)
                    {
                        Debug.LogWarning(
                            "[AudioClipReplacer] mergeAnimator is not assigned",
                            replacer);
                        return false;
                    }

                    replacer.mergeAnimator.animator = generatedController;

                    var attachedAnimator = replacer.mergeAnimator.GetComponent<Animator>();
                    if (attachedAnimator != null)
                    {
                        attachedAnimator.runtimeAnimatorController = generatedController;
                        EditorUtility.SetDirty(attachedAnimator);
                    }

                    EditorUtility.SetDirty(replacer.mergeAnimator);
                    return true;

                case AudioClipReplacerMode.PlayableLayer:
                    if (!TryGetAvatarDescriptor(replacer, avatarRoot, out var descriptor))
                        return false;

                    if (!SetPlayableLayerController(descriptor, replacer.playableLayer, generatedController))
                    {
                        Debug.LogWarning(
                            $"[AudioClipReplacer] Playable Layer not found on Avatar Descriptor: {replacer.playableLayer}",
                            replacer);
                        return false;
                    }

                    var mergeAnimators = avatarRoot
                        .GetComponentsInChildren<ModularAvatarMergeAnimator>(true)
                        .Where(m => m.layerType == replacer.playableLayer)
                        .ToList();

                    foreach (var mergeAnimator in mergeAnimators)
                        mergeAnimator.animator = generatedController;

                    return true;

                default:
                    return false;
            }
        }

        private static bool TryGetAvatarDescriptor(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            out VRCAvatarDescriptor descriptor)
        {
            descriptor = replacer.avatarDescriptor;
            if (descriptor != null)
                return true;

            if (avatarRoot == null)
            {
                Debug.LogWarning(
                    "[AudioClipReplacer] avatarDescriptor is not assigned and avatar root is null",
                    replacer);
                return false;
            }

            descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                Debug.LogWarning(
                    "[AudioClipReplacer] VRCAvatarDescriptor not found on avatar root",
                    replacer);
                return false;
            }

            return true;
        }

        private static RuntimeAnimatorController GetPlayableLayerController(
            VRCAvatarDescriptor descriptor,
            VRCAvatarDescriptor.AnimLayerType layerType)
        {
            var fromBase = GetFromLayers(descriptor.baseAnimationLayers, layerType);
            if (fromBase != null)
                return fromBase;

            return GetFromLayers(descriptor.specialAnimationLayers, layerType);
        }

        private static RuntimeAnimatorController GetFromLayers(
            VRCAvatarDescriptor.CustomAnimLayer[] layers,
            VRCAvatarDescriptor.AnimLayerType layerType)
        {
            if (layers == null)
                return null;

            foreach (var layer in layers)
            {
                if (layer.type == layerType && layer.animatorController != null)
                    return layer.animatorController;
            }

            return null;
        }

        private static bool SetPlayableLayerController(
            VRCAvatarDescriptor descriptor,
            VRCAvatarDescriptor.AnimLayerType layerType,
            RuntimeAnimatorController controller)
        {
            var updated = false;

            if (descriptor.baseAnimationLayers != null)
                updated |= SetInLayers(descriptor.baseAnimationLayers, layerType, controller);

            if (descriptor.specialAnimationLayers != null)
                updated |= SetInLayers(descriptor.specialAnimationLayers, layerType, controller);

            if (updated)
                descriptor.customizeAnimationLayers = true;

            return updated;
        }

        private static bool SetInLayers(
            VRCAvatarDescriptor.CustomAnimLayer[] layers,
            VRCAvatarDescriptor.AnimLayerType layerType,
            RuntimeAnimatorController controller)
        {
            var updated = false;

            for (var i = 0; i < layers.Length; i++)
            {
                if (layers[i].type != layerType)
                    continue;

                layers[i].animatorController = controller;
                layers[i].isDefault = false;
                updated = true;
            }

            return updated;
        }

        private static bool ApplyOverrideToState(AnimatorState state, AudioClipOverride entry)
        {
            var behaviours = state.behaviours;
            if (behaviours == null || behaviours.Length == 0)
                return false;

            var applied = false;
            foreach (var behaviour in behaviours)
            {
                if (behaviour is not VRCAnimatorPlayAudio playAudio)
                    continue;

                var changed = false;
                if (entry.clips != null && entry.clips.Length > 0)
                {
                    playAudio.Clips = entry.clips;
                    playAudio.ClipsApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;
                    changed = true;
                }

                if (entry.overrideVolume)
                {
                    playAudio.Volume = entry.volume;
                    playAudio.VolumeApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;
                    changed = true;
                }

                if (entry.overridePitch)
                {
                    playAudio.Pitch = entry.pitch;
                    playAudio.PitchApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;
                    changed = true;
                }

                if (entry.overridePlaybackOrder)
                {
                    playAudio.PlaybackOrder = entry.playbackOrder;
                    playAudio.ClipsApplySettings = entry.playbackOrderApplySettings;
                    changed = true;
                }

                if (entry.overrideDelayInSeconds)
                {
                    playAudio.DelayInSeconds = Mathf.Clamp(entry.delayInSeconds, 0f, 60f);
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(playAudio);
                    applied = true;
                }
            }

            return applied;
        }

        private static IEnumerable<string> GetStateNames(AudioClipOverride entry)
        {
            if (entry.stateNames == null)
                yield break;

            foreach (var stateName in entry.stateNames)
            {
                if (!string.IsNullOrWhiteSpace(stateName))
                    yield return stateName.Trim();
            }
        }

        private static IEnumerable<AnimatorState> FindStates(AnimatorController controller, string stateName)
        {
            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null)
                    continue;

                foreach (var state in EnumerateStates(layer.stateMachine))
                {
                    if (state.name == stateName)
                        yield return state;
                }
            }
        }

        private static IEnumerable<AnimatorState> EnumerateStates(AnimatorStateMachine stateMachine)
        {
            foreach (var child in stateMachine.states)
            {
                if (child.state != null)
                    yield return child.state;
            }

            foreach (var child in stateMachine.stateMachines)
            {
                if (child.stateMachine == null)
                    continue;

                foreach (var state in EnumerateStates(child.stateMachine))
                    yield return state;
            }
        }

        private static void EnsureGeneratedFolder()
        {
            if (!Directory.Exists("Assets/Generated"))
                Directory.CreateDirectory("Assets/Generated");

            if (!Directory.Exists("Assets/Generated/SamirinVRCUtility"))
                Directory.CreateDirectory("Assets/Generated/SamirinVRCUtility");

            if (!Directory.Exists(GeneratedRoot))
                Directory.CreateDirectory(GeneratedRoot);

            AssetDatabase.Refresh();
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name.Replace('/', '_').Replace('\\', '_');
        }
    }
}
#endif
