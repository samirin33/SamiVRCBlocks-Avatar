using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// Avatar の VRCAvatarDescriptor から AnimatorController を取得するユーティリティ。
    /// 他機能からも再利用可能。ndmf の VRChatPlatformAnimatorBindings の扱いを参考にしている。
    /// </summary>
    public static class VRCAvatarDescriptorControllerUtility
    {
        /// <summary>
        /// 指定レイヤー種別のコントローラを 1 つ取得する。カスタム未設定の場合は null。
        /// </summary>
        public static AnimatorController GetController(
            GameObject avatarRootObject,
            VRCAvatarDescriptor.AnimLayerType layerType)
        {
            if (avatarRootObject == null) return null;
            if (!avatarRootObject.TryGetComponent<VRCAvatarDescriptor>(out var descriptor))
                return null;

            descriptor.customizeAnimationLayers = true;

            var rt = GetRuntimeController(descriptor, layerType);
            if (rt == null) return null;
            if (rt is AnimatorController ac) return ac;
            if (rt is AnimatorOverrideController ov && ov.runtimeAnimatorController is AnimatorController ac2)
                return ac2;
            return null;
        }

        /// <summary>
        /// 指定した複数レイヤー種別のコントローラを取得する。重複は除く。
        /// </summary>
        public static IReadOnlyList<AnimatorController> GetControllers(
            GameObject avatarRootObject,
            params VRCAvatarDescriptor.AnimLayerType[] layerTypes)
        {
            if (avatarRootObject == null || layerTypes == null || layerTypes.Length == 0)
                return Array.Empty<AnimatorController>();

            var set = new HashSet<AnimatorController>();
            foreach (var layerType in layerTypes)
            {
                var ac = GetController(avatarRootObject, layerType);
                if (ac != null) set.Add(ac);
            }
            return set.ToList();
        }

        /// <summary>
        /// baseAnimationLayers / specialAnimationLayers の全レイヤーから
        /// 指定レイヤー種別に一致する RuntimeAnimatorController を返す（isDefault の場合は null）。
        /// </summary>
        private static RuntimeAnimatorController GetRuntimeController(
            VRCAvatarDescriptor descriptor,
            VRCAvatarDescriptor.AnimLayerType layerType)
        {
            foreach (var layer in EnumerateLayers(descriptor))
            {
                if (layer.type != layerType) continue;
                if (layer.isDefault) return null;
                var ac = layer.animatorController;
                if (ac != null) return ac;
                break;
            }
            return null;
        }

        private static IEnumerable<VRCAvatarDescriptor.CustomAnimLayer> EnumerateLayers(VRCAvatarDescriptor descriptor)
        {
            if (descriptor.baseAnimationLayers != null)
            {
                foreach (var layer in descriptor.baseAnimationLayers)
                    yield return layer;
            }
            if (descriptor.specialAnimationLayers != null)
            {
                foreach (var layer in descriptor.specialAnimationLayers)
                    yield return layer;
            }
        }

    }
}
