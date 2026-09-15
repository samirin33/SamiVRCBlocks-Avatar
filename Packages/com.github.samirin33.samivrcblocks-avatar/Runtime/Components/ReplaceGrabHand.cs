using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// ビルド時に VRCAvatarDescriptor の指定コライダー（掴み用ハンド等）を、
    /// このオブジェクト（または targetTransform）へ付け替える。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB ReplaceGrabHand")]
    public class ReplaceGrabHand : SamirinMABase
    {
        public enum AvatarContactType
        {
            Head,
            Torso,
            HandL,
            HandR,
            FootL,
            FootR,
            FingerIndexL,
            FingerIndexR,
            FingerMiddleL,
            FingerMiddleR,
            FingerRingL,
            FingerRingR,
            FingerLittleL,
            FingerLittleR,
        }

        [Tooltip("コライダーを付け替える先。未指定なら自身の Transform")]
        public Transform targetTransform;

        [Tooltip("付け替えるアバターコンタクトの種類")]
        public AvatarContactType avatarContactType = AvatarContactType.HandR;

        [Tooltip("元のコライダーローカル位置・回転を退避する Transform（任意）")]
        public Transform offsetTransform;

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar)
                return;

            var descriptor = avatarRootObject != null
                ? avatarRootObject.GetComponent<VRCAvatarDescriptor>()
                : null;
            SetContact(descriptor);
            DestroyImmediate(this);
        }

        public void SetContact(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null)
            {
                Transform current = transform;
                while (current != null)
                {
                    descriptor = current.GetComponent<VRCAvatarDescriptor>();
                    if (descriptor != null)
                        break;
                    current = current.parent;
                }
            }

            if (descriptor == null)
                return;

            Transform target = targetTransform != null ? targetTransform : transform;
            if (!TryGetColliderConfig(descriptor, avatarContactType, out var colliderConfig))
                return;

            colliderConfig.isMirrored = false;
            colliderConfig.state = VRCAvatarDescriptor.ColliderConfig.State.Custom;

            Transform colliderTransform = colliderConfig.transform;
            if (colliderTransform != null)
            {
                Vector3 localOffset = colliderConfig.position;
                Vector3 worldPosition = colliderTransform.TransformPoint(localOffset);
                Quaternion localRotation = colliderConfig.rotation;
                Quaternion worldRotation = colliderTransform.rotation * localRotation;

                target.position = worldPosition;
                target.rotation = worldRotation;
            }

            var originalPosition = colliderConfig.position;
            var originalRotation = colliderConfig.rotation;
            if (offsetTransform != null)
            {
                offsetTransform.localPosition = originalPosition;
                offsetTransform.localRotation = originalRotation;
            }

            colliderConfig.position = Vector3.zero;
            colliderConfig.rotation = Quaternion.identity;
            colliderConfig.transform = target;

            SetColliderConfig(descriptor, avatarContactType, colliderConfig);
        }

        private static bool TryGetColliderConfig(
            VRCAvatarDescriptor descriptor,
            AvatarContactType contactType,
            out VRCAvatarDescriptor.ColliderConfig colliderConfig)
        {
            switch (contactType)
            {
                case AvatarContactType.Head:
                    colliderConfig = descriptor.collider_head;
                    return true;
                case AvatarContactType.Torso:
                    colliderConfig = descriptor.collider_torso;
                    return true;
                case AvatarContactType.HandL:
                    colliderConfig = descriptor.collider_handL;
                    return true;
                case AvatarContactType.HandR:
                    colliderConfig = descriptor.collider_handR;
                    return true;
                case AvatarContactType.FootL:
                    colliderConfig = descriptor.collider_footL;
                    return true;
                case AvatarContactType.FootR:
                    colliderConfig = descriptor.collider_footR;
                    return true;
                case AvatarContactType.FingerIndexL:
                    colliderConfig = descriptor.collider_fingerIndexL;
                    return true;
                case AvatarContactType.FingerIndexR:
                    colliderConfig = descriptor.collider_fingerIndexR;
                    return true;
                case AvatarContactType.FingerMiddleL:
                    colliderConfig = descriptor.collider_fingerMiddleL;
                    return true;
                case AvatarContactType.FingerMiddleR:
                    colliderConfig = descriptor.collider_fingerMiddleR;
                    return true;
                case AvatarContactType.FingerRingL:
                    colliderConfig = descriptor.collider_fingerRingL;
                    return true;
                case AvatarContactType.FingerRingR:
                    colliderConfig = descriptor.collider_fingerRingR;
                    return true;
                case AvatarContactType.FingerLittleL:
                    colliderConfig = descriptor.collider_fingerLittleL;
                    return true;
                case AvatarContactType.FingerLittleR:
                    colliderConfig = descriptor.collider_fingerLittleR;
                    return true;
                default:
                    colliderConfig = default;
                    return false;
            }
        }

        private static void SetColliderConfig(
            VRCAvatarDescriptor descriptor,
            AvatarContactType contactType,
            VRCAvatarDescriptor.ColliderConfig colliderConfig)
        {
            switch (contactType)
            {
                case AvatarContactType.Head:
                    descriptor.collider_head = colliderConfig;
                    break;
                case AvatarContactType.Torso:
                    descriptor.collider_torso = colliderConfig;
                    break;
                case AvatarContactType.HandL:
                    descriptor.collider_handL = colliderConfig;
                    break;
                case AvatarContactType.HandR:
                    descriptor.collider_handR = colliderConfig;
                    break;
                case AvatarContactType.FootL:
                    descriptor.collider_footL = colliderConfig;
                    break;
                case AvatarContactType.FootR:
                    descriptor.collider_footR = colliderConfig;
                    break;
                case AvatarContactType.FingerIndexL:
                    descriptor.collider_fingerIndexL = colliderConfig;
                    break;
                case AvatarContactType.FingerIndexR:
                    descriptor.collider_fingerIndexR = colliderConfig;
                    break;
                case AvatarContactType.FingerMiddleL:
                    descriptor.collider_fingerMiddleL = colliderConfig;
                    break;
                case AvatarContactType.FingerMiddleR:
                    descriptor.collider_fingerMiddleR = colliderConfig;
                    break;
                case AvatarContactType.FingerRingL:
                    descriptor.collider_fingerRingL = colliderConfig;
                    break;
                case AvatarContactType.FingerRingR:
                    descriptor.collider_fingerRingR = colliderConfig;
                    break;
                case AvatarContactType.FingerLittleL:
                    descriptor.collider_fingerLittleL = colliderConfig;
                    break;
                case AvatarContactType.FingerLittleR:
                    descriptor.collider_fingerLittleR = colliderConfig;
                    break;
            }
        }
    }
}
