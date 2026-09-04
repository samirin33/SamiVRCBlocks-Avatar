using nadena.dev.modular_avatar.core;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// AvatarRoot からの相対パスで対象を探し、ビルド時にそのオブジェクトへ MA Bone Proxy を付与して
    /// 本コンポーネントの Transform を追従させます（通常の Bone Proxy とは付与先が逆）。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB InverseBoneProxy")]
    public class InverseBoneProxy : SamirinMABase
    {
        [Tooltip("対象 Transform（Avatar 配下）。ビルド時に MA Bone Proxy を付与します。")]
        public AvatarObjectReference targetObject = new AvatarObjectReference();

        [Tooltip("位置・回転の適用方法（MA Bone Proxy の Attachment Mode と同じ）")]
        public BoneProxyAttachmentMode attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;

        [Tooltip("スケールを対象に合わせるか（MA Bone Proxy の Match Scale と同じ）")]
        public bool matchScale;

        [Tooltip("エディタ上で対象 Transform に位置・回転・スケールをプレビュー適用するか")]
        public bool editorApplyTransform;

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar)
                return;

#if UNITY_EDITOR
            if (targetObject == null)
                targetObject = new AvatarObjectReference();

            if (!InverseBoneProxyUtil.TryResolveTarget(this, targetObject, out _, out var found))
            {
                if (!InverseBoneProxyUtil.IsReferenceEmpty(targetObject))
                {
                    Debug.LogWarning(
                        $"[InverseBoneProxy] Target not found: \"{targetObject?.referencePath}\" (on {name})",
                        this);
                }
            }
            else
            {
                InverseBoneProxyUtil.ApplyBoneProxy(found, transform, attachmentMode, matchScale);
            }
#endif
            DestroyImmediate(this);
        }

        void LateUpdate()
        {
            if (Application.isPlaying || !editorApplyTransform)
                return;

            if (targetObject == null)
                targetObject = new AvatarObjectReference();

            if (!InverseBoneProxyUtil.TryResolveTarget(this, targetObject, out _, out var found))
                return;

            InverseBoneProxyUtil.ApplyTransformLikeBoneProxy(
                found,
                transform,
                attachmentMode,
                InverseBoneProxyUtil.SupportsBoneProxyMatchScale && matchScale);
        }
    }
}
