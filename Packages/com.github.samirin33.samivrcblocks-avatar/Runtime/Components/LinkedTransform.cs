using System;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// 任意の複数 Transform を重み付きでブレンドし、オフセット・倍率・座標空間を指定してターゲットへコピーする。
    /// ターゲット未指定時は自身。エディタ上でプレビュー適用でき、ビルド時に一度ベイクして自身を削除する。
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

        [Serializable]
        public class Source
        {
            public Transform transform;
            [Min(0f)]
            public float weight = 1f;
        }

        [Tooltip("適用先 Transform（未指定なら自身）")]
        public Transform target;

        [Tooltip("コピー元の Transform と重み（Constraint と同様）")]
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

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar)
                return;

            ApplyLink();
            DestroyImmediate(this);
        }

#if UNITY_EDITOR
        private void LateUpdate()
        {
            if (Application.isPlaying)
                return;

            ApplyLink();
        }
#endif

        /// <summary>
        /// ソース群を重み付きブレンドし、オフセット・倍率・座標空間に従ってターゲットへ適用する。
        /// </summary>
        public void ApplyLink()
        {
            var t = ResolvedTarget;
            if (t == null || sources == null || sources.Length == 0)
                return;

            if (linkPosition)
                ApplyPosition(t);

            if (linkRotation)
                ApplyRotation(t);

            if (linkScale)
                ApplyScale(t);
        }

        private void ApplyPosition(Transform t)
        {
            if (!TryBlendVector3(positionSpace, GetPosition, out var blended))
                return;

            var desired = Vector3.Scale(blended, positionMultiplier) + positionOffset;

            if (positionSpace == TransformSpace.World)
            {
                var pos = t.position;
                if (positionX) pos.x = desired.x;
                if (positionY) pos.y = desired.y;
                if (positionZ) pos.z = desired.z;
                t.position = pos;
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

        private void ApplyRotation(Transform t)
        {
            if (!TryBlendRotation(rotationSpace, out var blended))
                return;

            var desired = Vector3.Scale(blended.eulerAngles, rotationMultiplier) + rotationOffset;

            if (rotationSpace == TransformSpace.World)
            {
                var rot = t.eulerAngles;
                if (rotationX) rot.x = desired.x;
                if (rotationY) rot.y = desired.y;
                if (rotationZ) rot.z = desired.z;
                t.eulerAngles = rot;
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

        private void ApplyScale(Transform t)
        {
            if (!TryBlendVector3(scaleSpace, GetScale, out var blended))
                return;

            var desired = Vector3.Scale(blended, scaleMultiplier) + scaleOffset;

            if (scaleSpace == TransformSpace.World)
            {
                var current = t.lossyScale;
                if (scaleX) current.x = desired.x;
                if (scaleY) current.y = desired.y;
                if (scaleZ) current.z = desired.z;
                SetLossyScale(t, current);
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

        private bool TryBlendVector3(TransformSpace space, Func<Transform, TransformSpace, Vector3> getter, out Vector3 blended)
        {
            blended = Vector3.zero;
            var totalWeight = 0f;

            for (var i = 0; i < sources.Length; i++)
            {
                var entry = sources[i];
                if (entry == null || entry.transform == null || entry.weight <= 0f)
                    continue;

                blended += getter(entry.transform, space) * entry.weight;
                totalWeight += entry.weight;
            }

            if (totalWeight <= 1e-8f)
                return false;

            blended /= totalWeight;
            return true;
        }

        private bool TryBlendRotation(TransformSpace space, out Quaternion blended)
        {
            blended = Quaternion.identity;
            var accum = Vector4.zero;
            var totalWeight = 0f;
            var hasReference = false;
            var reference = Quaternion.identity;

            for (var i = 0; i < sources.Length; i++)
            {
                var entry = sources[i];
                if (entry == null || entry.transform == null || entry.weight <= 0f)
                    continue;

                var q = GetRotation(entry.transform, space);
                if (!hasReference)
                {
                    reference = q;
                    hasReference = true;
                }
                else if (Quaternion.Dot(reference, q) < 0f)
                {
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                }

                accum += new Vector4(q.x, q.y, q.z, q.w) * entry.weight;
                totalWeight += entry.weight;
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

        private static Vector3 GetPosition(Transform source, TransformSpace space)
        {
            return space == TransformSpace.World ? source.position : source.localPosition;
        }

        private static Quaternion GetRotation(Transform source, TransformSpace space)
        {
            return space == TransformSpace.World ? source.rotation : source.localRotation;
        }

        private static Vector3 GetScale(Transform source, TransformSpace space)
        {
            return space == TransformSpace.World ? source.lossyScale : source.localScale;
        }

        private static void SetLossyScale(Transform target, Vector3 lossyScale)
        {
            var parent = target.parent;
            if (parent == null)
            {
                target.localScale = lossyScale;
                return;
            }

            var parentLossy = parent.lossyScale;
            target.localScale = new Vector3(
                ApproxDiv(lossyScale.x, parentLossy.x),
                ApproxDiv(lossyScale.y, parentLossy.y),
                ApproxDiv(lossyScale.z, parentLossy.z));
        }

        private static float ApproxDiv(float a, float b)
        {
            return Mathf.Abs(b) > 1e-8f ? a / b : a;
        }
    }
}
