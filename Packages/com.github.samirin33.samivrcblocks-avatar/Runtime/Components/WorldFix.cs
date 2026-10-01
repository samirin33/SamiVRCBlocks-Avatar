using UnityEngine;
using Samirin33.NDMF.Base;

#if UNITY_EDITOR
using UnityEditor;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;
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
                var constraint = target.GetComponent<VRCParentConstraint>();
                if (constraint == null) constraint = target.AddComponent<VRCParentConstraint>();
                SetParentConstraintAxes(constraint);
                FinishConstraint(constraint, sourceTransform);
            }
            else
            {
                if (fixPosition)
                {
                    var constraint = target.GetComponent<VRCPositionConstraint>();
                    if (constraint == null) constraint = target.AddComponent<VRCPositionConstraint>();
                    SetPositionConstraintAxes(constraint);
                    FinishConstraint(constraint, sourceTransform);
                }
                if (fixRotation)
                {
                    var constraint = target.GetComponent<VRCRotationConstraint>();
                    if (constraint == null) constraint = target.AddComponent<VRCRotationConstraint>();
                    SetRotationConstraintAxes(constraint);
                    FinishConstraint(constraint, sourceTransform);
                }
            }

            if (fixScale)
            {
                var constraint = target.GetComponent<VRCScaleConstraint>();
                if (constraint == null) constraint = target.AddComponent<VRCScaleConstraint>();
                SetScaleConstraintAxes(constraint);
                FinishConstraint(constraint, sourceTransform);
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
            var parent = t.parent;

            if (fixPosition && (positionX || positionY || positionZ))
            {
                var desired = parent != null
                    ? parent.InverseTransformPoint(sourceTransform.position)
                    : sourceTransform.position;
                var pos = t.localPosition;
                if (positionX) pos.x = desired.x;
                if (positionY) pos.y = desired.y;
                if (positionZ) pos.z = desired.z;
                t.localPosition = pos;
            }

            if (fixRotation && (rotationX || rotationY || rotationZ))
            {
                var desiredRotation = parent != null
                    ? Quaternion.Inverse(parent.rotation) * sourceTransform.rotation
                    : sourceTransform.rotation;
                if (rotationX && rotationY && rotationZ)
                {
                    t.localRotation = desiredRotation;
                }
                else
                {
                    var desired = desiredRotation.eulerAngles;
                    var rot = t.localEulerAngles;
                    if (rotationX) rot.x = desired.x;
                    if (rotationY) rot.y = desired.y;
                    if (rotationZ) rot.z = desired.z;
                    t.localEulerAngles = rot;
                }
            }

            if (fixScale && (scaleX || scaleY || scaleZ))
            {
                var sourceScale = sourceTransform.lossyScale;
                var parentScale = parent != null ? parent.lossyScale : Vector3.one;
                var scale = t.localScale;
                if (scaleX) scale.x = Mathf.Approximately(parentScale.x, 0) ? sourceScale.x : sourceScale.x / parentScale.x;
                if (scaleY) scale.y = Mathf.Approximately(parentScale.y, 0) ? sourceScale.y : sourceScale.y / parentScale.y;
                if (scaleZ) scale.z = Mathf.Approximately(parentScale.z, 0) ? sourceScale.z : sourceScale.z / parentScale.z;
                t.localScale = scale;
            }
        }

        private static Transform GetWorldPrefabTransform()
        {
            var path = AssetDatabase.GUIDToAssetPath(WorldPrefabGUID);
            if (string.IsNullOrEmpty(path)) return null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null ? prefab.transform : null;
        }

        private static void FinishConstraint(VRCConstraintBase constraint, Transform sourceTransform)
        {
            if (constraint.Sources.Count == 0)
            {
                var sources = constraint.Sources;
                sources.Add(new VRCConstraintSource(sourceTransform, 1f));
                constraint.Sources = sources;
            }

            constraint.GlobalWeight = 1f;
            constraint.IsActive = true;
            constraint.Locked = true;
        }

        private void SetParentConstraintAxes(VRCParentConstraint constraint)
        {
            constraint.PositionAtRest = Vector3.zero;
            constraint.RotationAtRest = Vector3.zero;
            constraint.AffectsPositionX = positionX;
            constraint.AffectsPositionY = positionY;
            constraint.AffectsPositionZ = positionZ;
            constraint.AffectsRotationX = rotationX;
            constraint.AffectsRotationY = rotationY;
            constraint.AffectsRotationZ = rotationZ;
        }

        private void SetPositionConstraintAxes(VRCPositionConstraint constraint)
        {
            constraint.AffectsPositionX = positionX;
            constraint.AffectsPositionY = positionY;
            constraint.AffectsPositionZ = positionZ;
            constraint.PositionAtRest = Vector3.zero;
            constraint.PositionOffset = Vector3.zero;
        }

        private void SetRotationConstraintAxes(VRCRotationConstraint constraint)
        {
            constraint.AffectsRotationX = rotationX;
            constraint.AffectsRotationY = rotationY;
            constraint.AffectsRotationZ = rotationZ;
            constraint.RotationAtRest = Vector3.zero;
            constraint.RotationOffset = Vector3.zero;
        }

        private void SetScaleConstraintAxes(VRCScaleConstraint constraint)
        {
            constraint.AffectsScaleX = scaleX;
            constraint.AffectsScaleY = scaleY;
            constraint.AffectsScaleZ = scaleZ;
            constraint.ScaleAtRest = Vector3.one;
            constraint.ScaleOffset = Vector3.one;
        }
#endif
    }
}