using System;
using UnityEngine;
using Samirin33.NDMF.Base;
using nadena.dev.ndmf.runtime;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// 任意の複数 Transform、または Humanoid ボーンを重み付きでブレンドし、
    /// オフセット・倍率・座標空間を指定してターゲットへコピーする。
    /// コンポーネント追加時のターゲットは自身。未指定時も自身に適用する。
    /// エディタ上でプレビュー適用できる。ビルドではボーン移動前にソース姿勢を記録し、
    /// ControllableHumanoid と Modular Avatar の後にターゲットへベイクして自身を削除する。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB LinkedTransform")]
    public class LinkedTransform : SamirinMABase
    {
        public enum TransformSpace
        {
            World,
            Local,
        }

        public enum SourceKind
        {
            [InspectorName("Transform")]
            Transform,
            [InspectorName("Humanoid ボーン")]
            HumanoidBone,
        }

        [Serializable]
        public class Source
        {
            [Tooltip("Transform を直接指定するか、Humanoid ボーンを指定するか")]
            public SourceKind kind = SourceKind.Transform;

            public Transform transform;

            [Tooltip("アバターの Humanoid ボーン。Root は Armature（Hips の親）")]
            public HumanBodyBones humanoidBone = HumanBodyBones.Hips;

            [Min(0f)]
            public float weight = 1f;

            /// <summary>Humanoid 指定の Root。Armature（Hips の親）を指す。</summary>
            public const int HumanoidRoot = -1;

            public static bool IsHumanoidBone(HumanBodyBones bone) =>
                bone >= 0 && bone < HumanBodyBones.LastBone;

            public static bool IsHumanoidRoot(HumanBodyBones bone) =>
                (int)bone == HumanoidRoot;

            public Transform Resolve(Animator animator)
            {
                if (kind != SourceKind.HumanoidBone)
                    return transform;

                if (animator == null || !animator.isHuman)
                    return null;

                if (IsHumanoidRoot(humanoidBone))
                    return ResolveArmatureRoot(animator);

                if (!IsHumanoidBone(humanoidBone))
                    return null;

                return animator.GetBoneTransform(humanoidBone);
            }

            public static Transform ResolveArmatureRoot(Animator animator)
            {
                if (animator == null)
                    return null;

                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                return hips != null ? hips.parent : null;
            }
        }

        [Tooltip("適用先 Transform。コンポーネント追加時は自身。未指定なら自身")]
        public Transform target;

        [Tooltip("コピー元。Transform または Humanoid ボーンと重み")]
        public Source[] sources = Array.Empty<Source>();

        [Header("Position")]
        public bool linkPosition = true;
        public bool positionX = true, positionY = true, positionZ = true;
        [Tooltip("World: ワールド座標 / Local: ローカル座標")]
        public TransformSpace positionSpace = TransformSpace.Local;
        [Tooltip("ブレンド後の位置に対する軸ごとの倍率")]
        public Vector3 positionMultiplier = Vector3.one;
        [Tooltip("倍率適用後に加算するオフセット")]
        public Vector3 positionOffset = Vector3.zero;

        [Header("Rotation")]
        public bool linkRotation = true;
        public bool rotationX = true, rotationY = true, rotationZ = true;
        [Tooltip("World: ワールド回転 / Local: ローカル回転")]
        public TransformSpace rotationSpace = TransformSpace.Local;
        [Tooltip("ブレンド後の回転（Euler）に対する軸ごとの倍率")]
        public Vector3 rotationMultiplier = Vector3.one;
        [Tooltip("倍率適用後に加算するオフセット（Euler）")]
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Scale")]
        public bool linkScale = true;
        public bool scaleX = true, scaleY = true, scaleZ = true;
        [Tooltip("World: lossyScale / Local: localScale")]
        public TransformSpace scaleSpace = TransformSpace.World;
        [Tooltip("ブレンド後のスケールに対する軸ごとの倍率")]
        public Vector3 scaleMultiplier = Vector3.one;
        [Tooltip("倍率適用後に加算するオフセット")]
        public Vector3 scaleOffset = Vector3.zero;

        public Transform ResolvedTarget => target != null ? target : transform;

        private struct SourceCapture
        {
            public bool valid;
            public Transform source;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public Vector3 worldScale;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        [NonSerialized]
        private SourceCapture[] _capturedSources;

        public LinkedTransform()
        {
            // ControllableHumanoid（Transforming, 50。MA の後）より後にベイクする。
            priority = 200;
        }

        private void Reset()
        {
            target = transform;
        }

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase == SamirinBuildPhase.Resolving && beforeModularAvatar)
            {
                CaptureSources(avatarRootObject);
                return;
            }

            if (buildPhase != SamirinBuildPhase.Transforming || beforeModularAvatar)
                return;

            ApplyLink(avatarRootObject);
            DestroyImmediate(this);
        }

#if UNITY_EDITOR
        private void LateUpdate()
        {
            if (Application.isPlaying)
                return;

            ApplyLink(null);
        }
#endif

        /// <summary>
        /// ソース群を重み付きブレンドし、オフセット・倍率・座標空間に従ってターゲットへ適用する。
        /// </summary>
        public void ApplyLink()
        {
            ApplyLink(null);
        }

        private void CaptureSources(GameObject avatarRootObject)
        {
            if (sources == null || sources.Length == 0)
            {
                _capturedSources = null;
                return;
            }

            var animator = FindHumanoidAnimator();
            _capturedSources = new SourceCapture[sources.Length];
            for (var i = 0; i < sources.Length; i++)
            {
                var entry = sources[i];
                if (entry == null)
                    continue;

                var source = entry.Resolve(animator);
                if (source == null)
                    continue;

                _capturedSources[i] = new SourceCapture
                {
                    valid = true,
                    source = source,
                    worldPosition = TransformMath.GetWorldPosition(source),
                    worldRotation = TransformMath.GetWorldRotation(source),
                    worldScale = TransformMath.GetWorldScale(source),
                    localPosition = source.localPosition,
                    localRotation = source.localRotation,
                    localScale = source.localScale,
                };
            }
        }

        private void ApplyLink(GameObject avatarRootObject)
        {
            if (sources == null || sources.Length == 0)
                return;

            if (avatarRootObject != null)
                CorrectCapturesForFloorAdjust(avatarRootObject);

            var animator = FindHumanoidAnimator();
            ApplyTo(ResolvedTarget, animator);

            // HipPos のように適用先が別オブジェクトでも、コンポーネント自身（ギミック階層）も合わせる。
            if (target != null && target != transform)
                ApplyTo(transform, animator);
        }

        /// <summary>
        /// ソースは Floor Adjuster より前に記録しているため、その後に下がった分を位置へ反映する。
        /// </summary>
        private void CorrectCapturesForFloorAdjust(GameObject avatarRootObject)
        {
            if (_capturedSources == null)
                return;

            for (var i = 0; i < _capturedSources.Length; i++)
            {
                var capture = _capturedSources[i];
                if (!capture.valid)
                    continue;

                capture.worldPosition = FloorAdjustmentTracker.CorrectWorldPosition(
                    avatarRootObject, capture.source, capture.worldPosition);
                capture.localPosition = FloorAdjustmentTracker.CorrectLocalPosition(
                    avatarRootObject, capture.source, capture.localPosition);
                _capturedSources[i] = capture;
            }
        }

        private void ApplyTo(Transform t, Animator animator)
        {
            if (t == null)
                return;

            // 位置は最後に書く。回転やスケールの反映でワールド位置がずれても、ソースの位置を残す。
            if (linkScale)
                ApplyScale(t, animator);

            if (linkRotation)
                ApplyRotation(t, animator);

            if (linkPosition)
                ApplyPosition(t, animator);
        }

        private bool TryGetSource(int index, Animator animator, out Transform live, out SourceCapture capture)
        {
            live = null;
            capture = default;
            if (index < 0 || sources == null || index >= sources.Length)
                return false;

            var entry = sources[index];
            if (entry == null || entry.weight <= 0f)
                return false;

            if (_capturedSources != null && index < _capturedSources.Length && _capturedSources[index].valid)
            {
                capture = _capturedSources[index];
                return true;
            }

            live = entry.Resolve(animator);
            return live != null;
        }

        private void ApplyPosition(Transform t, Animator animator)
        {
            if (!TryBlendVector3(animator, positionSpace, isScale: false, out var blended))
                return;

            var desired = Vector3.Scale(blended, positionMultiplier) + positionOffset;

            if (positionSpace == TransformSpace.World)
            {
                var pos = TransformMath.GetWorldPosition(t);
                if (positionX) pos.x = desired.x;
                if (positionY) pos.y = desired.y;
                if (positionZ) pos.z = desired.z;
                TransformMath.SetWorldPosition(t, pos);
            }
            else
            {
                var pos = t.localPosition;
                if (positionX) pos.x = desired.x;
                if (positionY) pos.y = desired.y;
                if (positionZ) pos.z = desired.z;
                t.localPosition = pos;
            }
        }

        private void ApplyRotation(Transform t, Animator animator)
        {
            if (!TryBlendRotation(animator, rotationSpace, out var blended))
                return;

            var desired = Vector3.Scale(blended.eulerAngles, rotationMultiplier) + rotationOffset;

            if (rotationSpace == TransformSpace.World)
            {
                var rot = TransformMath.GetWorldRotation(t).eulerAngles;
                if (rotationX) rot.x = desired.x;
                if (rotationY) rot.y = desired.y;
                if (rotationZ) rot.z = desired.z;
                TransformMath.SetWorldRotation(t, Quaternion.Euler(rot));
            }
            else
            {
                var rot = t.localEulerAngles;
                if (rotationX) rot.x = desired.x;
                if (rotationY) rot.y = desired.y;
                if (rotationZ) rot.z = desired.z;
                t.localEulerAngles = rot;
            }
        }

        private void ApplyScale(Transform t, Animator animator)
        {
            if (!TryBlendVector3(animator, scaleSpace, isScale: true, out var blended))
                return;

            var desired = Vector3.Scale(blended, scaleMultiplier) + scaleOffset;

            if (scaleSpace == TransformSpace.World)
            {
                var current = TransformMath.GetWorldScale(t);
                if (scaleX) current.x = desired.x;
                if (scaleY) current.y = desired.y;
                if (scaleZ) current.z = desired.z;
                TransformMath.SetWorldScale(t, current);
            }
            else
            {
                var current = t.localScale;
                if (scaleX) current.x = desired.x;
                if (scaleY) current.y = desired.y;
                if (scaleZ) current.z = desired.z;
                t.localScale = current;
            }
        }

        private bool TryBlendVector3(Animator animator, TransformSpace space, bool isScale, out Vector3 blended)
        {
            blended = Vector3.zero;
            var totalWeight = 0f;

            for (var i = 0; i < sources.Length; i++)
            {
                if (!TryGetSource(i, animator, out var live, out var capture))
                    continue;

                Vector3 value;
                if (capture.valid)
                {
                    if (space == TransformSpace.World)
                        value = isScale ? capture.worldScale : capture.worldPosition;
                    else
                        value = isScale ? capture.localScale : capture.localPosition;
                }
                else if (isScale)
                    value = space == TransformSpace.World ? TransformMath.GetWorldScale(live) : live.localScale;
                else
                    value = space == TransformSpace.World ? TransformMath.GetWorldPosition(live) : live.localPosition;

                blended += value * sources[i].weight;
                totalWeight += sources[i].weight;
            }

            if (totalWeight <= 1e-8f)
                return false;

            blended /= totalWeight;
            return true;
        }

        private bool TryBlendRotation(Animator animator, TransformSpace space, out Quaternion blended)
        {
            blended = Quaternion.identity;
            var accum = Vector4.zero;
            var totalWeight = 0f;
            var hasReference = false;
            var reference = Quaternion.identity;

            for (var i = 0; i < sources.Length; i++)
            {
                if (!TryGetSource(i, animator, out var live, out var capture))
                    continue;

                Quaternion q;
                if (capture.valid)
                    q = space == TransformSpace.World ? capture.worldRotation : capture.localRotation;
                else
                    q = space == TransformSpace.World ? TransformMath.GetWorldRotation(live) : live.localRotation;

                if (!hasReference)
                {
                    reference = q;
                    hasReference = true;
                }
                else if (Quaternion.Dot(reference, q) < 0f)
                {
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                }

                accum += new Vector4(q.x, q.y, q.z, q.w) * sources[i].weight;
                totalWeight += sources[i].weight;
            }

            if (totalWeight <= 1e-8f)
                return false;

            accum /= totalWeight;
            var magSq = accum.sqrMagnitude;
            if (magSq <= 1e-16f)
                return false;

            blended = Quaternion.Normalize(new Quaternion(accum.x, accum.y, accum.z, accum.w));
            return true;
        }

        private Animator FindHumanoidAnimator()
        {
            var avatarRoot = RuntimeUtil.FindAvatarInParents(transform);
            if (avatarRoot == null)
                return null;

            if (avatarRoot.TryGetComponent(out Animator avatarAnimator) && avatarAnimator.isHuman)
                return avatarAnimator;

            return null;
        }
    }
}
