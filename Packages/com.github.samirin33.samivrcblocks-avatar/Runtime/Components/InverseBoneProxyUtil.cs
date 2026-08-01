using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.runtime;
using UnityEngine;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// InverseBoneProxy 用のパス解決・Bone Proxy 付与ユーティリティ。
    /// </summary>
    public static class InverseBoneProxyUtil
    {
        public static bool IsReferenceEmpty(AvatarObjectReference reference)
        {
            return reference == null || string.IsNullOrWhiteSpace(reference.referencePath);
        }

        public static bool TryResolveTarget(
            Component container,
            AvatarObjectReference reference,
            out Transform avatarRoot,
            out Transform found)
        {
            avatarRoot = null;
            found = null;

            if (container == null || IsReferenceEmpty(reference))
                return false;

            avatarRoot = RuntimeUtil.FindAvatarInParents(container.transform);
            if (avatarRoot == null)
                return false;

            var go = reference.Get(container);
            if (go == null)
                return false;

            found = go.transform;
            return true;
        }

        /// <summary>
        /// ビルド時のみ呼び出す。対象に MA Bone Proxy を付与し、source を追従させる。
        /// </summary>
        public static ModularAvatarBoneProxy ApplyBoneProxy(
            Transform found,
            Transform source,
            BoneProxyAttachmentMode attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot,
            bool matchScale = false)
        {
            if (found == null || source == null)
                return null;

            foreach (var proxy in found.GetComponents<ModularAvatarBoneProxy>())
                Object.DestroyImmediate(proxy);

            var created = found.gameObject.AddComponent<ModularAvatarBoneProxy>();
            SetBoneProxyTarget(created, source);

            created.attachmentMode = attachmentMode == BoneProxyAttachmentMode.Unset
                ? BoneProxyAttachmentMode.AsChildAtRoot
                : attachmentMode;
            created.matchScale = matchScale;

            return created;
        }

        /// <summary>
        /// エディタプレビュー用。MA Bone Proxy の Update と同じ規則で follower に source の姿勢を適用する。
        /// </summary>
        public static void ApplyTransformLikeBoneProxy(
            Transform follower,
            Transform source,
            BoneProxyAttachmentMode attachmentMode,
            bool matchScale)
        {
            if (follower == null || source == null)
                return;

            var mode = attachmentMode == BoneProxyAttachmentMode.Unset
                ? BoneProxyAttachmentMode.AsChildAtRoot
                : attachmentMode;

            switch (mode)
            {
                case BoneProxyAttachmentMode.AsChildAtRoot:
                    follower.position = source.position;
                    follower.rotation = source.rotation;
                    break;
                case BoneProxyAttachmentMode.AsChildKeepPosition:
                    follower.rotation = source.rotation;
                    break;
                case BoneProxyAttachmentMode.AsChildKeepRotation:
                    follower.position = source.position;
                    break;
                case BoneProxyAttachmentMode.AsChildKeepWorldPose:
                    // ワールド姿勢を維持するため位置・回転は変更しない
                    break;
            }

            if (!matchScale)
                return;

            var sourceMat = source.localToWorldMatrix;
            var parentMat = follower.parent != null
                ? follower.parent.worldToLocalMatrix
                : Matrix4x4.identity;
            var transRotMat = Matrix4x4.TRS(
                follower.localPosition,
                follower.localRotation,
                Vector3.one);
            var finalMat = transRotMat * parentMat * sourceMat;
            follower.localScale = finalMat.lossyScale;
        }

        static void SetBoneProxyTarget(ModularAvatarBoneProxy proxy, Transform targetTransform)
        {
            if (proxy == null || targetTransform == null)
                return;

            var avatarRoot = RuntimeUtil.FindAvatarInParents(proxy.transform);
            if (avatarRoot == null)
                return;

            if (targetTransform == avatarRoot)
            {
                proxy.boneReference = HumanBodyBones.LastBone;
                proxy.subPath = "$$AVATAR";
                return;
            }

            var path = RuntimeUtil.RelativePath(avatarRoot.gameObject, targetTransform.gameObject);
            if (path == null)
                return;

            proxy.boneReference = HumanBodyBones.LastBone;
            proxy.subPath = path;
        }
    }
}
