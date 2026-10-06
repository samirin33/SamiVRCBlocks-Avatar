using System.Collections.Generic;
using UnityEngine;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// 非アクティブな階層や、回転したスケール 100 の Armature では
    /// Transform.position と Matrix4x4.inverse / lossyScale が軸を入れ替えてスケールを落とす。
    /// ローカル TRS を親からそのまま積む。
    /// </summary>
    public static class TransformMath
    {
        public static void GetWorldTRS(Transform transform, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;
            var chain = CollectChain(transform);
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                var current = chain[i];
                position += rotation * Vector3.Scale(scale, current.localPosition);
                rotation *= current.localRotation;
                scale = Vector3.Scale(scale, current.localScale);
            }
        }

        public static Matrix4x4 LocalToWorldMatrix(Transform transform)
        {
            GetWorldTRS(transform, out var position, out var rotation, out var scale);
            return Matrix4x4.TRS(position, rotation, scale);
        }

        public static Vector3 GetWorldPosition(Transform transform)
        {
            GetWorldTRS(transform, out var position, out _, out _);
            return position;
        }

        public static Quaternion GetWorldRotation(Transform transform)
        {
            GetWorldTRS(transform, out _, out var rotation, out _);
            return rotation;
        }

        public static Vector3 GetWorldScale(Transform transform)
        {
            GetWorldTRS(transform, out _, out _, out var scale);
            return scale;
        }

        public static void SetWorldPosition(Transform target, Vector3 worldPosition)
        {
            var parent = target.parent;
            if (parent == null)
            {
                target.localPosition = worldPosition;
                return;
            }

            GetWorldTRS(parent, out var parentPosition, out var parentRotation, out var parentScale);
            var inParent = Quaternion.Inverse(parentRotation) * (worldPosition - parentPosition);
            target.localPosition = new Vector3(
                DivideScale(inParent.x, parentScale.x),
                DivideScale(inParent.y, parentScale.y),
                DivideScale(inParent.z, parentScale.z));
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
            GetWorldTRS(source, out var position, out var rotation, out var scale);
            SetWorldPose(target, position, rotation, scale);
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
