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

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// AudioClipReplacer の共有ヘルパー。ビルド本体は Editor の ProcessHandler が担当する。
    /// </summary>
    public static class AudioClipReplacerUtil
    {
        public const string GeneratedRoot = "Assets/Generated/SamiVRCBlocks/AudioClipReplacer";

        public delegate bool ProcessDelegate(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            out RuntimeAnimatorController generatedController);

        /// <summary>Editor の AudioClipReplacerProcessor が登録する。</summary>
        public static ProcessDelegate ProcessHandler;

        public static bool TryProcess(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            out RuntimeAnimatorController generatedController)
        {
            generatedController = null;

            if (ProcessHandler == null)
            {
                Debug.LogError(
                    "[AudioClipReplacer] ProcessHandler が未登録です（Editor アセンブリが読み込まれているか確認してください）",
                    replacer);
                return false;
            }

            return ProcessHandler(replacer, avatarRoot, out generatedController);
        }

        /// <summary>
        /// Merge Animator の Animator 欄を優先し、未設定なら同じ GameObject の Animator から Controller を取得する。
        /// </summary>
        public static RuntimeAnimatorController GetMergeAnimatorSource(ModularAvatarMergeAnimator mergeAnimator)
        {
            if (mergeAnimator == null)
                return null;

            if (mergeAnimator.animator != null)
                return mergeAnimator.animator;

            var attachedAnimator = mergeAnimator.GetComponent<Animator>();
            return attachedAnimator != null ? attachedAnimator.runtimeAnimatorController : null;
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

            if (!TryResolveSource(replacer, avatarRoot, out var sourceController, out _, LogWarning))
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

        public static bool TryResolveSource(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            out AnimatorController sourceController,
            out AnimatorOverrideController sourceOverride,
            Action<string, UnityEngine.Object> reportWarning)
        {
            sourceController = null;
            sourceOverride = null;

            RuntimeAnimatorController runtimeSource;

            switch (replacer.mode)
            {
                case AudioClipReplacerMode.Direct:
                    if (replacer.mergeAnimator == null)
                    {
                        reportWarning?.Invoke("[AudioClipReplacer] Merge Animator が未設定です", replacer);
                        return false;
                    }

                    runtimeSource = GetMergeAnimatorSource(replacer.mergeAnimator);
                    if (runtimeSource == null)
                    {
                        reportWarning?.Invoke(
                            "[AudioClipReplacer] Merge Animator に Animator Controller が指定されていません",
                            replacer);
                        return false;
                    }

                    replacer.sourceController = runtimeSource;
                    break;

                case AudioClipReplacerMode.PlayableLayer:
                    if (!TryGetAvatarDescriptor(replacer, avatarRoot, out var descriptor, reportWarning))
                        return false;

                    runtimeSource = GetPlayableLayerController(descriptor, replacer.playableLayer);
                    if (runtimeSource == null)
                    {
                        reportWarning?.Invoke(
                            $"[AudioClipReplacer] Playable Layer に Controller がありません: {replacer.playableLayer}",
                            replacer);
                        return false;
                    }

                    break;

                default:
                    reportWarning?.Invoke(
                        $"[AudioClipReplacer] 不明なモードです: {replacer.mode}",
                        replacer);
                    return false;
            }

            return TryUnwrapRuntimeController(replacer, runtimeSource, out sourceController, out sourceOverride, reportWarning);
        }

        public static bool TryGetAvatarDescriptor(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            out VRCAvatarDescriptor descriptor,
            Action<string, UnityEngine.Object> reportWarning)
        {
            descriptor = replacer.avatarDescriptor;
            if (descriptor != null)
                return true;

            if (avatarRoot == null)
            {
                reportWarning?.Invoke(
                    "[AudioClipReplacer] Avatar Descriptor が未設定で、アバタールートも取得できません",
                    replacer);
                return false;
            }

            descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                reportWarning?.Invoke(
                    "[AudioClipReplacer] アバタールートに VRCAvatarDescriptor がありません",
                    replacer);
                return false;
            }

            return true;
        }

        public static IEnumerable<string> GetStateNames(AudioClipOverride entry)
        {
            if (entry.stateNames == null)
                yield break;

            foreach (var stateName in entry.stateNames)
            {
                if (!string.IsNullOrWhiteSpace(stateName))
                    yield return stateName.Trim();
            }
        }

        public static IEnumerable<AnimatorState> FindStates(AnimatorController controller, string stateName)
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

        /// <summary>
        /// 置き換え用 Clip を Generated へ複製し、Force To Mono / Load In Background を有効化する。
        /// 元アセットは書き換えない。
        /// </summary>
        public static AudioClip[] PrepareReplacementClips(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return clips;

            var prepared = new AudioClip[clips.Length];
            for (var i = 0; i < clips.Length; i++)
                prepared[i] = PrepareReplacementClip(clips[i]);

            return prepared;
        }

        public static AudioClip PrepareReplacementClip(AudioClip clip)
        {
            if (clip == null)
                return null;

            var sourcePath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogWarning(
                    $"[AudioClipReplacer] Clip is not a project asset: {clip.name}");
                return clip;
            }

            if (sourcePath.StartsWith(GeneratedRoot, StringComparison.Ordinal))
            {
                EnsureAudioImporterSettings(sourcePath);
                return AssetDatabase.LoadAssetAtPath<AudioClip>(sourcePath) ?? clip;
            }

            EnsureGeneratedFolder();

            var guid = AssetDatabase.AssetPathToGUID(sourcePath);
            var extension = Path.GetExtension(sourcePath);
            var baseName = SanitizeFileName($"{clip.name}_{guid}");
            var destPath = $"{GeneratedRoot}/{baseName}{extension}";

            if (AssetDatabase.LoadAssetAtPath<AudioClip>(destPath) == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, destPath))
                {
                    Debug.LogWarning(
                        $"[AudioClipReplacer] Failed to copy clip: {sourcePath} -> {destPath}");
                    return clip;
                }
            }

            EnsureAudioImporterSettings(destPath);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(destPath) ?? clip;
        }

        private static void LogWarning(string message, UnityEngine.Object context)
        {
            Debug.LogWarning(message, context);
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

        private static bool TryFindPlayAudio(
            AnimatorController sourceController,
            IReadOnlyList<string> stateNames,
            out VRC.SDK3.Avatars.Components.VRCAnimatorPlayAudio playAudio,
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
                        if (behaviour is VRC.SDK3.Avatars.Components.VRCAnimatorPlayAudio audio)
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

        private static bool TryUnwrapRuntimeController(
            AudioClipReplacer replacer,
            RuntimeAnimatorController runtimeSource,
            out AnimatorController sourceController,
            out AnimatorOverrideController sourceOverride,
            Action<string, UnityEngine.Object> reportWarning)
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
                    reportWarning?.Invoke(
                        $"[AudioClipReplacer] AnimatorOverrideController のベース Controller が未設定です: {overrideController.name}",
                        replacer);
                    return false;
                }

                if (baseRuntime is not AnimatorController baseController)
                {
                    reportWarning?.Invoke(
                        $"[AudioClipReplacer] AnimatorOverrideController のベースが AnimatorController ではありません: {baseRuntime.name}",
                        replacer);
                    return false;
                }

                sourceController = baseController;
                return true;
            }

            reportWarning?.Invoke(
                $"[AudioClipReplacer] サポート外の RuntimeAnimatorController 型です: {runtimeSource.GetType().Name}",
                replacer);
            return false;
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

        private static void EnsureAudioImporterSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
                return;

            if (importer.forceToMono && importer.loadInBackground)
                return;

            importer.forceToMono = true;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }

        private static void EnsureGeneratedFolder()
        {
            if (!Directory.Exists("Assets/Generated"))
                Directory.CreateDirectory("Assets/Generated");

            if (!Directory.Exists("Assets/Generated/SamiVRCBlocks"))
                Directory.CreateDirectory("Assets/Generated/SamiVRCBlocks");

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
