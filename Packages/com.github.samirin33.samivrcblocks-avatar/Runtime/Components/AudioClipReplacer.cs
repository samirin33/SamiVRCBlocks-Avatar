using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    public enum AudioClipReplacerMode
    {
        [Tooltip("Merge Animator に付いている Animator の Controller を複製・差し替え")]
        Direct,

        [Tooltip("Avatar Descriptor の Playable Layer から取得・置き換え")]
        PlayableLayer,
    }

    public enum AudioClipOverrideTarget
    {
        [Tooltip("Animator ステート上の VRC Animator Play Audio を置き換え")]
        AnimatorState,

        [Tooltip("指定 AudioSource に直接アタッチされている音源を置き換え")]
        AudioSource,
    }

    [Serializable]
    public class AudioClipOverride
    {
        public AudioClipOverrideTarget target = AudioClipOverrideTarget.AnimatorState;

        public string[] stateNames = Array.Empty<string>();

        [Tooltip("AudioSource ターゲット時に置き換える対象")]
        public AudioSource audioSource;

        [TextArea(1, 3)]
        public string description;

        public bool enabled = true;

        public AudioClip[] clips;

        public bool overrideVolume;
        public Vector2 volume = Vector2.one;

        public bool overridePitch;
        public Vector2 pitch = Vector2.one;

        public bool overridePlaybackOrder;
        public VRCAnimatorPlayAudio.Order playbackOrder = VRCAnimatorPlayAudio.Order.Random;

        public VRC_AnimatorPlayAudio.ApplySettings playbackOrderApplySettings =
            VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;

        public bool overrideDelayInSeconds;
        public float delayInSeconds;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB AudioClipReplacer")]
    public class AudioClipReplacer : SamirinMABase
    {
        public AudioClipReplacerMode mode = AudioClipReplacerMode.Direct;

        [Tooltip("Direct モードでは Merge Animator と同じ GameObject の Animator から自動取得（表示用）")]
        public RuntimeAnimatorController sourceController;

        [Tooltip("生成した AnimatorController を割り当てる MA Merge Animator")]
        public ModularAvatarMergeAnimator mergeAnimator;

        [Tooltip("未設定時はビルド対象アバターの Avatar Descriptor を使用")]
        public VRCAvatarDescriptor avatarDescriptor;

        public VRCAvatarDescriptor.AnimLayerType playableLayer = VRCAvatarDescriptor.AnimLayerType.FX;

        public AudioClipOverride[] overrides = Array.Empty<AudioClipOverride>();

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar)
                return;

#if UNITY_EDITOR
            AudioClipReplacerUtil.TryProcess(this, avatarRootObject, out _);
#endif
            DestroyImmediate(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncSourceControllerFromMergeAnimator();
        }

        /// <summary>
        /// Direct モード時、Merge Animator と同じ GameObject の Animator.runtimeAnimatorController を sourceController に反映する。
        /// </summary>
        public void SyncSourceControllerFromMergeAnimator()
        {
            if (mode != AudioClipReplacerMode.Direct || mergeAnimator == null)
                return;

            var animator = mergeAnimator.GetComponent<Animator>();
            if (animator == null)
                return;

            sourceController = animator.runtimeAnimatorController;
        }
#endif
    }
}
