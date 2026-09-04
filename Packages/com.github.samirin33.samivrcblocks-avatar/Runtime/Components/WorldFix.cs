using UnityEngine;
using UnityEngine.Animations;
using Samirin33.NDMF.Base;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Samirin33.NDMF.Components
{
    [ExecuteAlways]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB WorldFix")]
    public class WorldFix : SamirinMABase
    {
        private const string WorldPrefabGUID = "b68724081431dfe428986a441453c12b";

        public bool fixPosition = false;
        public bool positionX = true, positionY = true, positionZ = true;
        public bool fixRotation = false;
        public bool rotationX = true, rotationY = true, rotationZ = true;
        public bool fixScale = false;
        public bool scaleX = true, scaleY = true, scaleZ = true;
        public bool editorApply = true;

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar) return;
#if UNITY_EDITOR
            var sourceTransform = GetWorldPrefabTransform();
            if (sourceTransform == null)
            {
                DestroyImmediate(this);
                return;
            }

            var target = gameObject;

            if (fixPosition && fixRotation)
            {
                var constraint = target.GetComponent<ParentConstraint>();
                if (constraint == null) constraint = target.AddComponent<ParentConstraint>();
                SetParentConstraintAxes(constraint);
                AddSourceIfNeeded(constraint, sourceTransform);
            }
            else
            {
                if (fixPosition)
                {
                    var constraint = target.GetComponent<PositionConstraint>();
                    if (constraint == null) constraint = target.AddComponent<PositionConstraint>();
                    SetPositionConstraintAxes(constraint);
                    AddSourceIfNeeded(constraint, sourceTransform);
                }
                if (fixRotation)
                {
                    var constraint = target.GetComponent<RotationConstraint>();
                    if (constraint == null) constraint = target.AddComponent<RotationConstraint>();
                    SetRotationConstraintAxes(constraint);
                    AddSourceIfNeeded(constraint, sourceTransform);
                }
            }

            if (fixScale)
            {
                var constraint = target.GetComponent<ScaleConstraint>();
                if (constraint == null) constraint = target.AddComponent<ScaleConstraint>();
                SetScaleConstraintAxes(constraint);
                AddSourceIfNeeded(constraint, sourceTransform);
            }

            DestroyImmediate(this);
#endif
        }

#if UNITY_EDITOR
        private void LateUpdate()
        {
            if (editorApply && !Application.isPlaying)
            {
                ApplyEditorFix();
            }
        }

        private void ApplyEditorFix()
        {
            var sourceTransform = GetWorldPrefabTransform();
            if (sourceTransform == null) return;

            var t = transform;

            if (fixPosition)
            {
                var pos = t.position;
                if (positionX) pos.x = sourceTransform.position.x;
                if (positionY) pos.y = sourceTransform.position.y;
                if (positionZ) pos.z = sourceTransform.position.z;
                t.position = pos;
            }

            if (fixRotation)
            {
                var rot = t.eulerAngles;
                var sourceEuler = sourceTransform.eulerAngles;
                if (rotationX) rot.x = sourceEuler.x;
                if (rotationY) rot.y = sourceEuler.y;
                if (rotationZ) rot.z = sourceEuler.z;
                t.eulerAngles = rot;
            }

            if (fixScale)
            {
                var current = t.lossyScale;
                var desired = current;
                var sourceScale = sourceTransform.lossyScale;
                if (scaleX) desired.x = sourceScale.x;
                if (scaleY) desired.y = sourceScale.y;
                if (scaleZ) desired.z = sourceScale.z;
                var parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
                t.localScale = new Vector3(
                    Mathf.Approximately(parentScale.x, 0) ? desired.x : desired.x / parentScale.x,
                    Mathf.Approximately(parentScale.y, 0) ? desired.y : desired.y / parentScale.y,
                    Mathf.Approximately(parentScale.z, 0) ? desired.z : desired.z / parentScale.z);
            }
        }

        private static Transform GetWorldPrefabTransform()
        {
            var path = AssetDatabase.GUIDToAssetPath(WorldPrefabGUID);
            if (string.IsNullOrEmpty(path)) return null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null ? prefab.transform : null;
        }

        private static void AddSourceIfNeeded<T>(T constraint, Transform sourceTransform) where T : Behaviour, IConstraint
        {
            if (constraint.sourceCount > 0) return;

            var source = new ConstraintSource
            {
                sourceTransform = sourceTransform,
                weight = 1f
            };
            constraint.AddSource(source);
        }

        private void SetParentConstraintAxes(ParentConstraint constraint)
        {
            constraint.constraintActive = true;
            constraint.locked = true;
            constraint.translationAxis = (Axis)(
                (positionX ? (int)Axis.X : 0) |
                (positionY ? (int)Axis.Y : 0) |
                (positionZ ? (int)Axis.Z : 0));
            constraint.rotationAxis = (Axis)(
                (rotationX ? (int)Axis.X : 0) |
                (rotationY ? (int)Axis.Y : 0) |
                (rotationZ ? (int)Axis.Z : 0));
        }

        private void SetPositionConstraintAxes(PositionConstraint constraint)
        {
            constraint.constraintActive = true;
            constraint.locked = true;
            constraint.translationAxis = (Axis)(
                (positionX ? (int)Axis.X : 0) |
                (positionY ? (int)Axis.Y : 0) |
                (positionZ ? (int)Axis.Z : 0));
        }

        private void SetRotationConstraintAxes(RotationConstraint constraint)
        {
            constraint.constraintActive = true;
            constraint.locked = true;
            constraint.rotationAxis = (Axis)(
                (rotationX ? (int)Axis.X : 0) |
                (rotationY ? (int)Axis.Y : 0) |
                (rotationZ ? (int)Axis.Z : 0));
        }

        private void SetScaleConstraintAxes(ScaleConstraint constraint)
        {
            constraint.constraintActive = true;
            constraint.locked = true;
            constraint.scalingAxis = (Axis)(
                (scaleX ? (int)Axis.X : 0) |
                (scaleY ? (int)Axis.Y : 0) |
                (scaleZ ? (int)Axis.Z : 0));
        }
#endif
    }
}