using System.Collections.Generic;
using UnityEngine;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// ビルド中の非アクティブなクローンでは Transform.position / rotation / lossyScale が
    /// 親スケール（アバタールートが 1 でない場合など）を含まない。
    /// ローカル TRS を親から積んでワールド値を組み立てる。
    /// </summary>
    public static class TransformMath
    {
        public static Matrix4x4 LocalToWorldMatrix(Transform transform)
        {
            var chain = CollectChain(transform);
            var matrix = Matrix4x4.identity;
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                var current = chain[i];
                matrix *= Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale);
            }

            return matrix;
        }

        public static Vector3 GetWorldPosition(Transform transform)
        {
            return LocalToWorldMatrix(transform).MultiplyPoint3x4(Vector3.zero);
        }

        public static Quaternion GetWorldRotation(Transform transform)
        {
            var rotation = Quaternion.identity;
            var chain = CollectChain(transform);
            for (var i = chain.Count - 1; i >= 0; i--)
                rotation *= chain[i].localRotation;
            return rotation;
        }

        public static Vector3 GetWorldScale(Transform transform)
        {
            return LocalToWorldMatrix(transform).lossyScale;
        }

        public static void SetWorldPosition(Transform target, Vector3 worldPosition)
        {
            var parent = target.parent;
            if (parent == null)
            {
                target.localPosition = worldPosition;
                return;
            }

            target.localPosition = InverseTransformPoint(LocalToWorldMatrix(parent), worldPosition);
        }

        public static void SetWorldRotation(Transform target, Quaternion worldRotation)
        {
            var parent = target.parent;
            if (parent == null)
            {
                target.localRotation = worldRotation;
                return;
            }

            target.localRotation = Quaternion.Inverse(GetWorldRotation(parent)) * worldRotation;
        }

        public static void SetWorldScale(Transform target, Vector3 worldScale)
        {
            var parent = target.parent;
            if (parent == null)
            {
                target.localScale = worldScale;
                return;
            }

            var parentScale = GetWorldScale(parent);
            target.localScale = new Vector3(
                DivideScale(worldScale.x, parentScale.x),
                DivideScale(worldScale.y, parentScale.y),
                DivideScale(worldScale.z, parentScale.z));
        }

        public static void SetWorldPose(Transform target, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            SetWorldScale(target, scale);
            SetWorldRotation(target, rotation);
            SetWorldPosition(target, position);
        }

        public static void CopyLocalPose(Transform source, Transform target)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        public static void CopyWorldPose(Transform source, Transform target)
        {
            SetWorldPose(
                target,
                GetWorldPosition(source),
                GetWorldRotation(source),
                GetWorldScale(source));
        }

        private static Vector3 InverseTransformPoint(Matrix4x4 localToWorld, Vector3 worldPoint)
        {
            return localToWorld.inverse.MultiplyPoint3x4(worldPoint);
        }

        private static float DivideScale(float value, float parent)
        {
            return Mathf.Abs(parent) > 1e-8f ? value / parent : value;
        }

        private static List<Transform> CollectChain(Transform transform)
        {
            var chain = new List<Transform>();
            for (var current = transform; current != null; current = current.parent)
                chain.Add(current);
            return chain;
        }
    }
}
