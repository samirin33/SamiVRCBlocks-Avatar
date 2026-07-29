using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Animations;

namespace Samirin33.NDMF.Constraints
{
    /// <summary>
    /// GameObjectからConstraintラッパーを取得する拡張メソッド
    /// </summary>
    public static class ConstraintExtensions
    {
        /// <summary>
        /// 指定した型のConstraintラッパーを取得する。
        /// UnityまたはVRCのConstraintを検出し、共通インターフェースで返す。
        /// </summary>
        /// <typeparam name="T">PositionConstraintWrapper, RotationConstraintWrapper, ParentConstraintWrapper, ScaleConstraintWrapper, AimConstraintWrapper, LookAtConstraintWrapper のいずれか</typeparam>
        /// <returns>ラッパー型。見つからない場合はnull</returns>
        public static T GetConstraint<T>(this GameObject gameObject) where T : AllConstraint
        {
            var component = GetConstraintComponent(gameObject, typeof(T));
            return component != null ? AllConstraint.FromComponent(component) as T : null;
        }

        /// <summary>
        /// 指定した型のConstraintラッパーを取得する（Transform用）
        /// </summary>
        public static T GetConstraint<T>(this Transform transform) where T : AllConstraint
        {
            return transform != null ? transform.gameObject.GetConstraint<T>() : null;
        }

        private static Component GetConstraintComponent(GameObject gameObject, Type constraintType)
        {
            if (constraintType == typeof(PositionConstraintWrapper))
            {
                return gameObject.GetComponent<UnityEngine.Animations.PositionConstraint>()
                    ?? (Component)gameObject.GetComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint>();
            }
            if (constraintType == typeof(RotationConstraintWrapper))
            {
                return gameObject.GetComponent<UnityEngine.Animations.RotationConstraint>()
                    ?? (Component)gameObject.GetComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint>();
            }
            if (constraintType == typeof(ParentConstraintWrapper))
            {
                return gameObject.GetComponent<UnityEngine.Animations.ParentConstraint>()
                    ?? (Component)gameObject.GetComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint>();
            }
            if (constraintType == typeof(ScaleConstraintWrapper))
            {
                return gameObject.GetComponent<UnityEngine.Animations.ScaleConstraint>()
                    ?? (Component)gameObject.GetComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint>();
            }
            if (constraintType == typeof(AimConstraintWrapper))
            {
                return gameObject.GetComponent<UnityEngine.Animations.AimConstraint>()
                    ?? (Component)gameObject.GetComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCAimConstraint>();
            }
            if (constraintType == typeof(LookAtConstraintWrapper))
            {
                return gameObject.GetComponent<UnityEngine.Animations.LookAtConstraint>()
                    ?? (Component)gameObject.GetComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint>();
            }
            if (constraintType == typeof(AllConstraint))
            {
                // AllConstraintの場合は全ての型を順に試す
                var component = GetConstraintComponent(gameObject, typeof(PositionConstraintWrapper))
                    ?? GetConstraintComponent(gameObject, typeof(RotationConstraintWrapper))
                    ?? GetConstraintComponent(gameObject, typeof(ParentConstraintWrapper))
                    ?? GetConstraintComponent(gameObject, typeof(ScaleConstraintWrapper))
                    ?? GetConstraintComponent(gameObject, typeof(AimConstraintWrapper))
                    ?? GetConstraintComponent(gameObject, typeof(LookAtConstraintWrapper));
                return component;
            }
            return null;
        }
    }

    /// <summary>
    /// Unity従来のConstraintとVRC Constraintを共通化して扱うための抽象基底クラス
    /// </summary>
    public abstract class AllConstraint
    {
        public abstract Component RawComponent { get; }

        public abstract float Weight { get; set; }
        public abstract bool IsActive { get; set; }

        public abstract int SourceCount { get; }

        public abstract Transform GetSource(int index);
        public abstract void SetSource(int index, Transform t);
        public abstract void AddSource(Transform t, float weight);
        public abstract void RemoveSource(int index);

        /// <summary>
        /// Componentから適切なAllConstraintラッパーを取得する
        /// </summary>
        public static AllConstraint FromComponent(Component component)
        {
            if (component == null) return null;

            // Position
            if (component is UnityEngine.Animations.PositionConstraint unityPos)
                return new UnityPositionConstraintWrapper(unityPos);
            if (component is VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint vrcPos)
                return new VRCPositionConstraintWrapper(vrcPos);

            // Rotation
            if (component is UnityEngine.Animations.RotationConstraint unityRot)
                return new UnityRotationConstraintWrapper(unityRot);
            if (component is VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint vrcRot)
                return new VRCRotationConstraintWrapper(vrcRot);

            // Parent
            if (component is UnityEngine.Animations.ParentConstraint unityParent)
                return new UnityParentConstraintWrapper(unityParent);
            if (component is VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint vrcParent)
                return new VRCParentConstraintWrapper(vrcParent);

            // Scale
            if (component is UnityEngine.Animations.ScaleConstraint unityScale)
                return new UnityScaleConstraintWrapper(unityScale);
            if (component is VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint vrcScale)
                return new VRCScaleConstraintWrapper(vrcScale);

            // Aim
            if (component is UnityEngine.Animations.AimConstraint unityAim)
                return new UnityAimConstraintWrapper(unityAim);
            if (component is VRC.SDK3.Dynamics.Constraint.Components.VRCAimConstraint vrcAim)
                return new VRCAimConstraintWrapper(vrcAim);

            // LookAt
            if (component is UnityEngine.Animations.LookAtConstraint unityLookAt)
                return new UnityLookAtConstraintWrapper(unityLookAt);
            if (component is VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint vrcLookAt)
                return new VRCLookAtConstraintWrapper(vrcLookAt);

            return null;
        }

        public static PositionConstraintWrapper FromComponentAsPositionConstraint(Component component) =>
            FromComponent(component) as PositionConstraintWrapper;
        public static RotationConstraintWrapper FromComponentAsRotationConstraint(Component component) =>
            FromComponent(component) as RotationConstraintWrapper;
        public static ParentConstraintWrapper FromComponentAsParentConstraint(Component component) =>
            FromComponent(component) as ParentConstraintWrapper;
        public static ScaleConstraintWrapper FromComponentAsScaleConstraint(Component component) =>
            FromComponent(component) as ScaleConstraintWrapper;
        public static AimConstraintWrapper FromComponentAsAimConstraint(Component component) =>
            FromComponent(component) as AimConstraintWrapper;
        public static LookAtConstraintWrapper FromComponentAsLookAtConstraint(Component component) =>
            FromComponent(component) as LookAtConstraintWrapper;

        /// <summary>VRC Constraintかどうか</summary>
        public bool IsVRC => this is IVRCConstraint;

        /// <summary>IVRCConstraintとして取得。Unity Constraintの場合はnull</summary>
        public IVRCConstraint AsVRC => this as IVRCConstraint;
    }

    #region Position Constraint

    /// <summary>
    /// VRC Constraint固有の設定にアクセスするためのインターフェース。
    /// VRCラッパー型にキャストして使用する。
    /// </summary>
    public interface IVRCConstraint
    {
        bool Locked { get; set; }
        Transform TargetTransform { get; set; }
        bool SolveInLocalSpace { get; set; }
        bool FreezeToWorld { get; set; }
        bool RebakeOffsetsWhenUnfrozen { get; set; }
        void ActivateConstraint();
        void ZeroConstraint();
        float GetSourceWeight(int index);
        void SetSourceWeight(int index, float weight);
    }

    public abstract class PositionConstraintWrapper : AllConstraint { }

    public class UnityPositionConstraintWrapper : PositionConstraintWrapper
    {
        private readonly UnityEngine.Animations.PositionConstraint _constraint;
        public UnityPositionConstraintWrapper(UnityEngine.Animations.PositionConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.weight; set => _constraint.weight = value; }
        public override bool IsActive { get => _constraint.constraintActive; set => _constraint.constraintActive = value; }
        public override int SourceCount => _constraint.sourceCount;
        public override Transform GetSource(int index) => _constraint.GetSource(index).sourceTransform;
        public override void SetSource(int index, Transform t)
        {
            var s = _constraint.GetSource(index);
            s.sourceTransform = t;
            _constraint.SetSource(index, s);
        }
        public override void AddSource(Transform t, float weight) =>
            _constraint.AddSource(new ConstraintSource { sourceTransform = t, weight = weight });
        public override void RemoveSource(int index) => _constraint.RemoveSource(index);
    }

    public class VRCPositionConstraintWrapper : PositionConstraintWrapper, IVRCConstraint
    {
        private static readonly MethodInfo ApplyModifiedChangesMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint)
            .GetMethod("ApplyModifiedChanges", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ActivateConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint)
            .GetMethod("ActivateConstraint", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ZeroConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint)
            .GetMethod("ZeroConstraint", BindingFlags.Public | BindingFlags.Instance);
        private readonly VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint _constraint;
        public VRCPositionConstraintWrapper(VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.GlobalWeight; set { _constraint.GlobalWeight = value; ApplyModifiedChanges(); } }
        public override bool IsActive { get => _constraint.IsActive; set { _constraint.IsActive = value; ApplyModifiedChanges(); } }
        public override int SourceCount => _constraint.Sources.Count;
        public override Transform GetSource(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].SourceTransform : null;
        public override void SetSource(int index, Transform t)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.SourceTransform = t;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }
        public override void AddSource(Transform t, float weight) { _constraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource(t, weight)); ApplyModifiedChanges(); }
        public override void RemoveSource(int index) { if (index >= 0 && index < _constraint.Sources.Count) { _constraint.Sources.RemoveAt(index); ApplyModifiedChanges(); } }
        private void ApplyModifiedChanges() => ApplyModifiedChangesMethod?.Invoke(_constraint, null);

        // IVRCConstraint
        public bool Locked { get => _constraint.Locked; set { _constraint.Locked = value; ApplyModifiedChanges(); } }
        public Transform TargetTransform { get => _constraint.TargetTransform; set { _constraint.TargetTransform = value; ApplyModifiedChanges(); } }
        public bool SolveInLocalSpace { get => _constraint.SolveInLocalSpace; set { _constraint.SolveInLocalSpace = value; ApplyModifiedChanges(); } }
        public bool FreezeToWorld { get => _constraint.FreezeToWorld; set { _constraint.FreezeToWorld = value; ApplyModifiedChanges(); } }
        public bool RebakeOffsetsWhenUnfrozen { get => _constraint.RebakeOffsetsWhenUnfrozen; set { _constraint.RebakeOffsetsWhenUnfrozen = value; ApplyModifiedChanges(); } }
        public void ActivateConstraint() => ActivateConstraintMethod?.Invoke(_constraint, null);
        public void ZeroConstraint() => ZeroConstraintMethod?.Invoke(_constraint, null);
        public float GetSourceWeight(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].Weight : 0f;
        public void SetSourceWeight(int index, float weight)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.Weight = weight;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }

        // Position固有
        public Vector3 PositionAtRest { get => _constraint.PositionAtRest; set { _constraint.PositionAtRest = value; ApplyModifiedChanges(); } }
        public Vector3 PositionOffset { get => _constraint.PositionOffset; set { _constraint.PositionOffset = value; ApplyModifiedChanges(); } }
    }

    #endregion

    #region Rotation Constraint

    public abstract class RotationConstraintWrapper : AllConstraint { }

    public class UnityRotationConstraintWrapper : RotationConstraintWrapper
    {
        private readonly UnityEngine.Animations.RotationConstraint _constraint;
        public UnityRotationConstraintWrapper(UnityEngine.Animations.RotationConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.weight; set => _constraint.weight = value; }
        public override bool IsActive { get => _constraint.constraintActive; set => _constraint.constraintActive = value; }
        public override int SourceCount => _constraint.sourceCount;
        public override Transform GetSource(int index) => _constraint.GetSource(index).sourceTransform;
        public override void SetSource(int index, Transform t)
        {
            var s = _constraint.GetSource(index);
            s.sourceTransform = t;
            _constraint.SetSource(index, s);
        }
        public override void AddSource(Transform t, float weight) =>
            _constraint.AddSource(new ConstraintSource { sourceTransform = t, weight = weight });
        public override void RemoveSource(int index) => _constraint.RemoveSource(index);
    }

    public class VRCRotationConstraintWrapper : RotationConstraintWrapper, IVRCConstraint
    {
        private static readonly MethodInfo ApplyModifiedChangesMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint)
            .GetMethod("ApplyModifiedChanges", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ActivateConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint)
            .GetMethod("ActivateConstraint", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ZeroConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint)
            .GetMethod("ZeroConstraint", BindingFlags.Public | BindingFlags.Instance);
        private readonly VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint _constraint;
        public VRCRotationConstraintWrapper(VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.GlobalWeight; set { _constraint.GlobalWeight = value; ApplyModifiedChanges(); } }
        public override bool IsActive { get => _constraint.IsActive; set { _constraint.IsActive = value; ApplyModifiedChanges(); } }
        public override int SourceCount => _constraint.Sources.Count;
        public override Transform GetSource(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].SourceTransform : null;
        public override void SetSource(int index, Transform t)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.SourceTransform = t;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }
        public override void AddSource(Transform t, float weight) { _constraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource(t, weight)); ApplyModifiedChanges(); }
        public override void RemoveSource(int index) { if (index >= 0 && index < _constraint.Sources.Count) { _constraint.Sources.RemoveAt(index); ApplyModifiedChanges(); } }
        private void ApplyModifiedChanges() => ApplyModifiedChangesMethod?.Invoke(_constraint, null);

        public bool Locked { get => _constraint.Locked; set { _constraint.Locked = value; ApplyModifiedChanges(); } }
        public Transform TargetTransform { get => _constraint.TargetTransform; set { _constraint.TargetTransform = value; ApplyModifiedChanges(); } }
        public bool SolveInLocalSpace { get => _constraint.SolveInLocalSpace; set { _constraint.SolveInLocalSpace = value; ApplyModifiedChanges(); } }
        public bool FreezeToWorld { get => _constraint.FreezeToWorld; set { _constraint.FreezeToWorld = value; ApplyModifiedChanges(); } }
        public bool RebakeOffsetsWhenUnfrozen { get => _constraint.RebakeOffsetsWhenUnfrozen; set { _constraint.RebakeOffsetsWhenUnfrozen = value; ApplyModifiedChanges(); } }
        public void ActivateConstraint() => ActivateConstraintMethod?.Invoke(_constraint, null);
        public void ZeroConstraint() => ZeroConstraintMethod?.Invoke(_constraint, null);
        public float GetSourceWeight(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].Weight : 0f;
        public void SetSourceWeight(int index, float weight)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.Weight = weight;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }

        public Vector3 RotationAtRest { get => _constraint.RotationAtRest; set { _constraint.RotationAtRest = value; ApplyModifiedChanges(); } }
        public Vector3 RotationOffset { get => _constraint.RotationOffset; set { _constraint.RotationOffset = value; ApplyModifiedChanges(); } }
    }

    #endregion

    #region Parent Constraint

    public abstract class ParentConstraintWrapper : AllConstraint { }

    public class UnityParentConstraintWrapper : ParentConstraintWrapper
    {
        private readonly UnityEngine.Animations.ParentConstraint _constraint;
        public UnityParentConstraintWrapper(UnityEngine.Animations.ParentConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.weight; set => _constraint.weight = value; }
        public override bool IsActive { get => _constraint.constraintActive; set => _constraint.constraintActive = value; }
        public override int SourceCount => _constraint.sourceCount;
        public override Transform GetSource(int index) => _constraint.GetSource(index).sourceTransform;
        public override void SetSource(int index, Transform t)
        {
            var s = _constraint.GetSource(index);
            s.sourceTransform = t;
            _constraint.SetSource(index, s);
        }
        public override void AddSource(Transform t, float weight) =>
            _constraint.AddSource(new ConstraintSource { sourceTransform = t, weight = weight });
        public override void RemoveSource(int index) => _constraint.RemoveSource(index);
    }

    public class VRCParentConstraintWrapper : ParentConstraintWrapper, IVRCConstraint
    {
        private static readonly MethodInfo ApplyModifiedChangesMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint)
            .GetMethod("ApplyModifiedChanges", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ActivateConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint)
            .GetMethod("ActivateConstraint", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ZeroConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint)
            .GetMethod("ZeroConstraint", BindingFlags.Public | BindingFlags.Instance);
        private readonly VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint _constraint;
        public VRCParentConstraintWrapper(VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.GlobalWeight; set { _constraint.GlobalWeight = value; ApplyModifiedChanges(); } }
        public override bool IsActive { get => _constraint.IsActive; set { _constraint.IsActive = value; ApplyModifiedChanges(); } }
        public override int SourceCount => _constraint.Sources.Count;
        public override Transform GetSource(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].SourceTransform : null;
        public override void SetSource(int index, Transform t)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.SourceTransform = t;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }
        public override void AddSource(Transform t, float weight) { _constraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource(t, weight)); ApplyModifiedChanges(); }
        public override void RemoveSource(int index) { if (index >= 0 && index < _constraint.Sources.Count) { _constraint.Sources.RemoveAt(index); ApplyModifiedChanges(); } }
        private void ApplyModifiedChanges() => ApplyModifiedChangesMethod?.Invoke(_constraint, null);

        public bool Locked { get => _constraint.Locked; set { _constraint.Locked = value; ApplyModifiedChanges(); } }
        public Transform TargetTransform { get => _constraint.TargetTransform; set { _constraint.TargetTransform = value; ApplyModifiedChanges(); } }
        public bool SolveInLocalSpace { get => _constraint.SolveInLocalSpace; set { _constraint.SolveInLocalSpace = value; ApplyModifiedChanges(); } }
        public bool FreezeToWorld { get => _constraint.FreezeToWorld; set { _constraint.FreezeToWorld = value; ApplyModifiedChanges(); } }
        public bool RebakeOffsetsWhenUnfrozen { get => _constraint.RebakeOffsetsWhenUnfrozen; set { _constraint.RebakeOffsetsWhenUnfrozen = value; ApplyModifiedChanges(); } }
        public void ActivateConstraint() => ActivateConstraintMethod?.Invoke(_constraint, null);
        public void ZeroConstraint() => ZeroConstraintMethod?.Invoke(_constraint, null);
        public float GetSourceWeight(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].Weight : 0f;
        public void SetSourceWeight(int index, float weight)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.Weight = weight;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }

        /// <summary>指定インデックスのソースのPositionOffsetを取得/設定</summary>
        public Vector3 GetSourcePositionOffset(int index)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return Vector3.zero;
            return _constraint.Sources[index].ParentPositionOffset;
        }
        public void SetSourcePositionOffset(int index, Vector3 value)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.ParentPositionOffset = value;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }
        /// <summary>指定インデックスのソースのRotationOffsetを取得/設定</summary>
        public Vector3 GetSourceRotationOffset(int index)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return Vector3.zero;
            return _constraint.Sources[index].ParentRotationOffset;
        }
        public void SetSourceRotationOffset(int index, Vector3 value)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.ParentRotationOffset = value;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }
    }

    #endregion

    #region Scale Constraint

    public abstract class ScaleConstraintWrapper : AllConstraint { }

    public class UnityScaleConstraintWrapper : ScaleConstraintWrapper
    {
        private readonly UnityEngine.Animations.ScaleConstraint _constraint;
        public UnityScaleConstraintWrapper(UnityEngine.Animations.ScaleConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.weight; set => _constraint.weight = value; }
        public override bool IsActive { get => _constraint.constraintActive; set => _constraint.constraintActive = value; }
        public override int SourceCount => _constraint.sourceCount;
        public override Transform GetSource(int index) => _constraint.GetSource(index).sourceTransform;
        public override void SetSource(int index, Transform t)
        {
            var s = _constraint.GetSource(index);
            s.sourceTransform = t;
            _constraint.SetSource(index, s);
        }
        public override void AddSource(Transform t, float weight) =>
            _constraint.AddSource(new ConstraintSource { sourceTransform = t, weight = weight });
        public override void RemoveSource(int index) => _constraint.RemoveSource(index);
    }

    public class VRCScaleConstraintWrapper : ScaleConstraintWrapper, IVRCConstraint
    {
        private static readonly MethodInfo ApplyModifiedChangesMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint)
            .GetMethod("ApplyModifiedChanges", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ActivateConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint)
            .GetMethod("ActivateConstraint", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ZeroConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint)
            .GetMethod("ZeroConstraint", BindingFlags.Public | BindingFlags.Instance);
        private readonly VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint _constraint;
        public VRCScaleConstraintWrapper(VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.GlobalWeight; set { _constraint.GlobalWeight = value; ApplyModifiedChanges(); } }
        public override bool IsActive { get => _constraint.IsActive; set { _constraint.IsActive = value; ApplyModifiedChanges(); } }
        public override int SourceCount => _constraint.Sources.Count;
        public override Transform GetSource(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].SourceTransform : null;
        public override void SetSource(int index, Transform t)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.SourceTransform = t;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }
        public override void AddSource(Transform t, float weight) { _constraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource(t, weight)); ApplyModifiedChanges(); }
        public override void RemoveSource(int index) { if (index >= 0 && index < _constraint.Sources.Count) { _constraint.Sources.RemoveAt(index); ApplyModifiedChanges(); } }
        private void ApplyModifiedChanges() => ApplyModifiedChangesMethod?.Invoke(_constraint, null);

        public bool Locked { get => _constraint.Locked; set { _constraint.Locked = value; ApplyModifiedChanges(); } }
        public Transform TargetTransform { get => _constraint.TargetTransform; set { _constraint.TargetTransform = value; ApplyModifiedChanges(); } }
        public bool SolveInLocalSpace { get => _constraint.SolveInLocalSpace; set { _constraint.SolveInLocalSpace = value; ApplyModifiedChanges(); } }
        public bool FreezeToWorld { get => _constraint.FreezeToWorld; set { _constraint.FreezeToWorld = value; ApplyModifiedChanges(); } }
        public bool RebakeOffsetsWhenUnfrozen { get => _constraint.RebakeOffsetsWhenUnfrozen; set { _constraint.RebakeOffsetsWhenUnfrozen = value; ApplyModifiedChanges(); } }
        public void ActivateConstraint() => ActivateConstraintMethod?.Invoke(_constraint, null);
        public void ZeroConstraint() => ZeroConstraintMethod?.Invoke(_constraint, null);
        public float GetSourceWeight(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].Weight : 0f;
        public void SetSourceWeight(int index, float weight)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.Weight = weight;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }

        public Vector3 ScaleAtRest { get => _constraint.ScaleAtRest; set { _constraint.ScaleAtRest = value; ApplyModifiedChanges(); } }
        public Vector3 ScaleOffset { get => _constraint.ScaleOffset; set { _constraint.ScaleOffset = value; ApplyModifiedChanges(); } }
    }

    #endregion

    #region Aim Constraint

    public abstract class AimConstraintWrapper : AllConstraint { }

    public class UnityAimConstraintWrapper : AimConstraintWrapper
    {
        private readonly UnityEngine.Animations.AimConstraint _constraint;
        public UnityAimConstraintWrapper(UnityEngine.Animations.AimConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.weight; set => _constraint.weight = value; }
        public override bool IsActive { get => _constraint.constraintActive; set => _constraint.constraintActive = value; }
        public override int SourceCount => _constraint.sourceCount;
        public override Transform GetSource(int index) => _constraint.GetSource(index).sourceTransform;
        public override void SetSource(int index, Transform t)
        {
            var s = _constraint.GetSource(index);
            s.sourceTransform = t;
            _constraint.SetSource(index, s);
        }
        public override void AddSource(Transform t, float weight) =>
            _constraint.AddSource(new ConstraintSource { sourceTransform = t, weight = weight });
        public override void RemoveSource(int index) => _constraint.RemoveSource(index);
    }

    public class VRCAimConstraintWrapper : AimConstraintWrapper, IVRCConstraint
    {
        private static readonly MethodInfo ApplyModifiedChangesMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCAimConstraint)
            .GetMethod("ApplyModifiedChanges", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ActivateConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCAimConstraint)
            .GetMethod("ActivateConstraint", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ZeroConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCAimConstraint)
            .GetMethod("ZeroConstraint", BindingFlags.Public | BindingFlags.Instance);
        private readonly VRC.SDK3.Dynamics.Constraint.Components.VRCAimConstraint _constraint;
        public VRCAimConstraintWrapper(VRC.SDK3.Dynamics.Constraint.Components.VRCAimConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.GlobalWeight; set { _constraint.GlobalWeight = value; ApplyModifiedChanges(); } }
        public override bool IsActive { get => _constraint.IsActive; set { _constraint.IsActive = value; ApplyModifiedChanges(); } }
        public override int SourceCount => _constraint.Sources.Count;
        public override Transform GetSource(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].SourceTransform : null;
        public override void SetSource(int index, Transform t)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.SourceTransform = t;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }
        public override void AddSource(Transform t, float weight) { _constraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource(t, weight)); ApplyModifiedChanges(); }
        public override void RemoveSource(int index) { if (index >= 0 && index < _constraint.Sources.Count) { _constraint.Sources.RemoveAt(index); ApplyModifiedChanges(); } }
        private void ApplyModifiedChanges() => ApplyModifiedChangesMethod?.Invoke(_constraint, null);

        public bool Locked { get => _constraint.Locked; set { _constraint.Locked = value; ApplyModifiedChanges(); } }
        public Transform TargetTransform { get => _constraint.TargetTransform; set { _constraint.TargetTransform = value; ApplyModifiedChanges(); } }
        public bool SolveInLocalSpace { get => _constraint.SolveInLocalSpace; set { _constraint.SolveInLocalSpace = value; ApplyModifiedChanges(); } }
        public bool FreezeToWorld { get => _constraint.FreezeToWorld; set { _constraint.FreezeToWorld = value; ApplyModifiedChanges(); } }
        public bool RebakeOffsetsWhenUnfrozen { get => _constraint.RebakeOffsetsWhenUnfrozen; set { _constraint.RebakeOffsetsWhenUnfrozen = value; ApplyModifiedChanges(); } }
        public void ActivateConstraint() => ActivateConstraintMethod?.Invoke(_constraint, null);
        public void ZeroConstraint() => ZeroConstraintMethod?.Invoke(_constraint, null);
        public float GetSourceWeight(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].Weight : 0f;
        public void SetSourceWeight(int index, float weight)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.Weight = weight;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }

        public Vector3 AimAxis { get => _constraint.AimAxis; set { _constraint.AimAxis = value; ApplyModifiedChanges(); } }
        public Vector3 UpAxis { get => _constraint.UpAxis; set { _constraint.UpAxis = value; ApplyModifiedChanges(); } }
        public Transform WorldUpTransform { get => _constraint.WorldUpTransform; set { _constraint.WorldUpTransform = value; ApplyModifiedChanges(); } }
        public Vector3 WorldUpVector { get => _constraint.WorldUpVector; set { _constraint.WorldUpVector = value; ApplyModifiedChanges(); } }
        public VRC.Dynamics.VRCConstraintBase.WorldUpType WorldUp { get => _constraint.WorldUp; set { _constraint.WorldUp = value; ApplyModifiedChanges(); } }
    }

    #endregion

    #region LookAt Constraint

    public abstract class LookAtConstraintWrapper : AllConstraint { }

    public class UnityLookAtConstraintWrapper : LookAtConstraintWrapper
    {
        private readonly UnityEngine.Animations.LookAtConstraint _constraint;
        public UnityLookAtConstraintWrapper(UnityEngine.Animations.LookAtConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.weight; set => _constraint.weight = value; }
        public override bool IsActive { get => _constraint.constraintActive; set => _constraint.constraintActive = value; }
        public override int SourceCount => _constraint.sourceCount;
        public override Transform GetSource(int index) => _constraint.GetSource(index).sourceTransform;
        public override void SetSource(int index, Transform t)
        {
            var s = _constraint.GetSource(index);
            s.sourceTransform = t;
            _constraint.SetSource(index, s);
        }
        public override void AddSource(Transform t, float weight) =>
            _constraint.AddSource(new ConstraintSource { sourceTransform = t, weight = weight });
        public override void RemoveSource(int index) => _constraint.RemoveSource(index);
    }

    public class VRCLookAtConstraintWrapper : LookAtConstraintWrapper, IVRCConstraint
    {
        private static readonly MethodInfo ApplyModifiedChangesMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint)
            .GetMethod("ApplyModifiedChanges", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ActivateConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint)
            .GetMethod("ActivateConstraint", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ZeroConstraintMethod = typeof(VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint)
            .GetMethod("ZeroConstraint", BindingFlags.Public | BindingFlags.Instance);
        private readonly VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint _constraint;
        public VRCLookAtConstraintWrapper(VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint constraint) => _constraint = constraint;
        public override Component RawComponent => _constraint;
        public override float Weight { get => _constraint.GlobalWeight; set { _constraint.GlobalWeight = value; ApplyModifiedChanges(); } }
        public override bool IsActive { get => _constraint.IsActive; set { _constraint.IsActive = value; ApplyModifiedChanges(); } }
        public override int SourceCount => _constraint.Sources.Count;
        public override Transform GetSource(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].SourceTransform : null;
        public override void SetSource(int index, Transform t)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.SourceTransform = t;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }
        public override void AddSource(Transform t, float weight) { _constraint.Sources.Add(new VRC.Dynamics.VRCConstraintSource(t, weight)); ApplyModifiedChanges(); }
        public override void RemoveSource(int index) { if (index >= 0 && index < _constraint.Sources.Count) { _constraint.Sources.RemoveAt(index); ApplyModifiedChanges(); } }
        private void ApplyModifiedChanges() => ApplyModifiedChangesMethod?.Invoke(_constraint, null);

        public bool Locked { get => _constraint.Locked; set { _constraint.Locked = value; ApplyModifiedChanges(); } }
        public Transform TargetTransform { get => _constraint.TargetTransform; set { _constraint.TargetTransform = value; ApplyModifiedChanges(); } }
        public bool SolveInLocalSpace { get => _constraint.SolveInLocalSpace; set { _constraint.SolveInLocalSpace = value; ApplyModifiedChanges(); } }
        public bool FreezeToWorld { get => _constraint.FreezeToWorld; set { _constraint.FreezeToWorld = value; ApplyModifiedChanges(); } }
        public bool RebakeOffsetsWhenUnfrozen { get => _constraint.RebakeOffsetsWhenUnfrozen; set { _constraint.RebakeOffsetsWhenUnfrozen = value; ApplyModifiedChanges(); } }
        public void ActivateConstraint() => ActivateConstraintMethod?.Invoke(_constraint, null);
        public void ZeroConstraint() => ZeroConstraintMethod?.Invoke(_constraint, null);
        public float GetSourceWeight(int index) => (index >= 0 && index < _constraint.Sources.Count) ? _constraint.Sources[index].Weight : 0f;
        public void SetSourceWeight(int index, float weight)
        {
            if (index < 0 || index >= _constraint.Sources.Count) return;
            var s = _constraint.Sources[index];
            s.Weight = weight;
            _constraint.Sources[index] = s;
            ApplyModifiedChanges();
        }

        public float Roll { get => _constraint.Roll; set { _constraint.Roll = value; ApplyModifiedChanges(); } }
        public Transform WorldUpTransform { get => _constraint.WorldUpTransform; set { _constraint.WorldUpTransform = value; ApplyModifiedChanges(); } }
        public bool UseUpTransform { get => _constraint.UseUpTransform; set { _constraint.UseUpTransform = value; ApplyModifiedChanges(); } }
    }

    #endregion
}
