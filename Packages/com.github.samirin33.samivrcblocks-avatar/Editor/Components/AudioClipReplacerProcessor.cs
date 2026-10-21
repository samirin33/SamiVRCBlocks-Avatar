using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Samirin33.NDMF.Base;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// AudioClipReplacer のビルド時処理。AudioSource の音源を書き換え、
    /// Animator ステートの VRC Animator Play Audio を変更する場合は複製元 Controller をメモリ上に複製して差し替える。
    /// 元の AnimatorController アセットや Assets 配下には何も書き込まない（生成物は NDMF がビルド終了時に保存する）。
    /// </summary>
    internal static class AudioClipReplacerProcessor
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            AudioClipReplacerUtil.ProcessHandler = Process;
        }

        private static void ReportWarning(string message, Object context)
        {
            SamirinBuildContext.ReportWarning(message, context);
        }

        public static bool Process(
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
                if (entry == null || !entry.enabled || entry.target != AudioClipOverrideTarget.AudioSource)
                    continue;

                if (ApplyOverrideToAudioSource(entry, replacer, avatarRoot))
                    appliedAny = true;
            }

            var hasStateOverrides = overrides.Any(entry =>
                entry != null
                && entry.enabled
                && entry.target == AudioClipOverrideTarget.AnimatorState
                && AudioClipReplacerUtil.GetStateNames(entry).Any());

            if (!hasStateOverrides)
                return appliedAny;

            if (!AudioClipReplacerUtil.TryResolveSource(
                    replacer,
                    avatarRoot,
                    out var sourceController,
                    out var sourceOverride,
                    ReportWarning))
            {
                return appliedAny;
            }

            if (replacer.mode == AudioClipReplacerMode.Direct && !IsInsideAvatar(replacer.mergeAnimator, avatarRoot))
            {
                SamirinBuildContext.ReportError(
                    "[AudioClipReplacer] Merge Animator がビルド対象アバターの外にあるため処理をスキップしました",
                    replacer);
                return appliedAny;
            }

            var clone = AnimatorControllerDeepCloner.Clone(sourceController, $"{sourceController.name} (AudioClipReplacer)");

            var appliedStateAny = false;
            foreach (var entry in overrides)
            {
                if (entry == null || !entry.enabled || entry.target != AudioClipOverrideTarget.AnimatorState)
                    continue;

                foreach (var stateName in AudioClipReplacerUtil.GetStateNames(entry))
                {
                    var states = AudioClipReplacerUtil.FindStates(clone, stateName).ToList();
                    if (states.Count == 0)
                    {
                        ReportWarning(
                            $"[AudioClipReplacer] ステートが見つかりません: \"{stateName}\"（{sourceController.name}）",
                            replacer);
                        continue;
                    }

                    foreach (var state in states)
                    {
                        if (ApplyOverrideToState(state, entry))
                            appliedStateAny = true;
                    }
                }
            }

            if (!appliedStateAny)
                return appliedAny;

            ObjectRegistry.TryRegisterReplacedObject(ObjectRegistry.GetReference(sourceController), clone);

            RuntimeAnimatorController generated = clone;
            if (sourceOverride != null)
            {
                generated = CloneOverrideController(sourceOverride, clone);
                ObjectRegistry.TryRegisterReplacedObject(ObjectRegistry.GetReference(sourceOverride), generated);
            }

            if (!TryApplyTarget(replacer, avatarRoot, generated))
                return appliedAny;

            generatedController = generated;
            return true;
        }

        private static bool IsInsideAvatar(Component component, GameObject avatarRoot)
        {
            return component != null && (avatarRoot == null || component.transform.IsChildOf(avatarRoot.transform));
        }

        private static AnimatorOverrideController CloneOverrideController(
            AnimatorOverrideController source,
            AnimatorController newBaseController)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            source.GetOverrides(overrides);

            // 複製した Controller は元と同じクリップを参照するので、元のオーバーライド対応表をそのまま使える。
            var clone = new AnimatorOverrideController(newBaseController)
            {
                name = $"{source.name} (AudioClipReplacer)",
            };
            clone.ApplyOverrides(overrides);
            return clone;
        }

        private static bool ApplyOverrideToAudioSource(
            AudioClipOverride entry,
            AudioClipReplacer replacer,
            GameObject avatarRoot)
        {
            if (entry.audioSource == null)
            {
                ReportWarning("[AudioClipReplacer] AudioSource が未設定です", replacer);
                return false;
            }

            if (!IsInsideAvatar(entry.audioSource, avatarRoot))
            {
                ReportWarning(
                    $"[AudioClipReplacer] AudioSource \"{entry.audioSource.name}\" がビルド対象アバターの外にあるため変更しません",
                    replacer);
                return false;
            }

            var changed = false;

            if (entry.clips != null && entry.clips.Length > 0 && entry.clips[0] != null)
            {
                entry.audioSource.clip = AudioClipReplacerUtil.PrepareReplacementClip(entry.clips[0]);
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

            return changed;
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
                    playAudio.Clips = AudioClipReplacerUtil.PrepareReplacementClips(entry.clips);
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
                    applied = true;
            }

            return applied;
        }

        private static bool TryApplyTarget(
            AudioClipReplacer replacer,
            GameObject avatarRoot,
            RuntimeAnimatorController generatedController)
        {
            switch (replacer.mode)
            {
                case AudioClipReplacerMode.Direct:
                {
                    var mergeAnimator = replacer.mergeAnimator;
                    if (mergeAnimator == null)
                    {
                        ReportWarning("[AudioClipReplacer] Merge Animator が未設定です", replacer);
                        return false;
                    }

                    var previous = AudioClipReplacerUtil.GetMergeAnimatorSource(mergeAnimator);
                    mergeAnimator.animator = generatedController;

                    var attachedAnimator = mergeAnimator.GetComponent<Animator>();
                    if (attachedAnimator != null && attachedAnimator.runtimeAnimatorController == previous)
                        attachedAnimator.runtimeAnimatorController = generatedController;

                    return true;
                }

                case AudioClipReplacerMode.PlayableLayer:
                {
                    // 同じ layerType の Merge Animator は他ギミックの Controller なので触らない。Descriptor のレイヤーだけ差し替える。
                    if (!AudioClipReplacerUtil.TryGetAvatarDescriptor(replacer, avatarRoot, out var descriptor, ReportWarning))
                        return false;

                    if (!SetPlayableLayerController(descriptor, replacer.playableLayer, generatedController))
                    {
                        ReportWarning(
                            $"[AudioClipReplacer] Avatar Descriptor に Playable Layer が見つかりません: {replacer.playableLayer}",
                            replacer);
                        return false;
                    }

                    return true;
                }

                default:
                    return false;
            }
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
    }
}
