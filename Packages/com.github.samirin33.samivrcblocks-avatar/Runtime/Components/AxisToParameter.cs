using System;
using System.Collections.Generic;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// 自身または指定オブジェクトの、基準オブジェクトから見た X/Y/Z 軸まわりの回転角を Float パラメーターとして取得する。
    /// ビルド時（Generating, MA の前）に、Contact で読んだ Sender 座標（sin/cos 相当）から角度を復元する
    /// AAP BlendTree の Animator を生成して MA Merge Animator で統合する。
    /// 計測 Contact は SDK に応じて選ぶ（3.10.6+ では Advanced Parameters の SenderLocalPosition、
    /// それ以外では Box Face Proximity）。パラメーター名はそれぞれ
    /// `{prefix}_SenderLocalPositionX/Y/Z` / `{prefix}/X|Y|Z`。
    /// Transforming（MA の後）に、このオブジェクトの下へ VRC Rotation Constraint と Contact の構造を生成する。
    /// このコンポーネントの Constraint 有効・Contact 有効を操作するアニメーションは、生成した Constraint / Contact へ書き換える。
    /// VRCFury 等が後から追加したアニメーションも拾うため、Optimizing（MA の前）でもう一度書き換えてから自身を削除する。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB AxisToParameter")]
    public class AxisToParameter : SamirinMABase
    {
        public enum Axis
        {
            X,
            Y,
            Z,
        }

        public enum OutputRange
        {
            ZeroToOne,
            MinusOneToOne,
            ZeroTo360,
            Custom,
        }

        [Serializable]
        public class AxisEntry
        {
            [Tooltip("出力先の Float パラメーター名")]
            public string parameterName;

            [Tooltip("回転を取得するオブジェクト。未指定なら自身")]
            public Transform target;

            [Tooltip("このオブジェクトから見た回転を測る。未指定なら WorldFix と同じワールド基準（ワールド軸）")]
            public Transform reference;

            [Tooltip("回転軸（基準オブジェクトのローカル軸。基準が未指定ならワールドの軸）")]
            public Axis axis = Axis.Y;

            [Tooltip("角度をどの範囲の値に変換するか")]
            public OutputRange range = OutputRange.ZeroToOne;

            [Tooltip("任意範囲: この角度で最小値を出力する")]
            public float customAngleMin = -180f;

            [Tooltip("任意範囲: この角度で最大値を出力する。範囲外の角度は近い側の端の値になる")]
            public float customAngleMax = 180f;

            [Tooltip("任意範囲: 出力の最小値")]
            public float customValueMin;

            [Tooltip("任意範囲: 出力の最大値")]
            public float customValueMax = 1f;
        }

        public const string ConstraintActiveProperty = nameof(constraintActive);
        public const string ContactActiveProperty = nameof(contactActive);

        /// <summary>Editor から登録される Animator の生成処理。</summary>
        public static Action<AxisToParameter, GameObject> GenerateAnimatorHandler;

        /// <summary>Editor から登録される Contact / Constraint の生成とアニメーション書き換え処理。</summary>
        public static Action<AxisToParameter, GameObject> BuildRigHandler;

        /// <summary>Editor から登録される、後から追加されたアニメーションの書き換え処理。</summary>
        public static Action<AxisToParameter, GameObject> RemapAnimationHandler;

        [Tooltip("取得する回転のリスト")]
        public List<AxisEntry> entries = new List<AxisEntry>();

        [Tooltip("Contact をローカルでのみ動かす。パフォーマンスランクに影響しなくなる代わりに、他のプレイヤーからは値が取れない")]
        public bool localOnly;

        [Tooltip("オフのとき生成した Constraint を止めます。アニメーションで変更できます")]
        public bool constraintActive = true;

        [Tooltip("オフのとき生成した Contact を止めます。アニメーションで変更できます")]
        public bool contactActive = true;

        public AxisToParameter()
        {
            // RemapPosition（Transforming, 100）と同じく、ControllableHumanoid が Armature を分割した後に参照を解決する。
            priority = 100;
        }

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            // MA Merge Animator が統合する前に Animator を足す。
            if (buildPhase == SamirinBuildPhase.Generating && beforeModularAvatar)
            {
                GenerateAnimatorHandler?.Invoke(this, avatarRootObject);
                return;
            }

            // VRCFury は NDMF の Transforming と Optimizing の間に走るため、そこで足されたカーブはここで初めて見える。
            if (buildPhase == SamirinBuildPhase.Optimizing && beforeModularAvatar)
            {
                RemapAnimationHandler?.Invoke(this, avatarRootObject);
                DestroyImmediate(this);
                return;
            }

            // Bone Proxy / Merge Armature で親が変わった後の階層でリグを組む。
            if (buildPhase == SamirinBuildPhase.Transforming && !beforeModularAvatar)
                BuildRigHandler?.Invoke(this, avatarRootObject);
        }

        public Transform ResolveTarget(AxisEntry entry)
        {
            return entry != null && entry.target != null ? entry.target : transform;
        }

        /// <summary>null はワールド基準。</summary>
        public Transform ResolveReference(AxisEntry entry)
        {
            return entry != null ? entry.reference : null;
        }

        /// <summary>
        /// 軸ごとの計測方向（基準のローカル空間）。回転 0° のとき zeroDirection を向くベクトルが、
        /// 軸まわりに +90° 回ると quarterDirection を向く。Unity の localEulerAngles と同じ向きを正にする。
        /// </summary>
        public static void GetMeasureDirections(Axis axis, out Vector3 zeroDirection, out Vector3 quarterDirection)
        {
            switch (axis)
            {
                case Axis.X:
                    zeroDirection = Vector3.forward;
                    quarterDirection = Vector3.down;
                    break;
                case Axis.Z:
                    zeroDirection = Vector3.up;
                    quarterDirection = Vector3.left;
                    break;
                default:
                    zeroDirection = Vector3.forward;
                    quarterDirection = Vector3.right;
                    break;
            }
        }

        public static Vector3 GetAxisVector(Axis axis)
        {
            switch (axis)
            {
                case Axis.X: return Vector3.right;
                case Axis.Z: return Vector3.forward;
                default: return Vector3.up;
            }
        }

        /// <summary>
        /// ターゲットの回転を基準から見て、軸まわりの角度（-180〜180）を求める。
        /// 0° 方向のベクトルを軸に垂直な平面へ投影した向きで測るため、他の軸の回転が大きいほど誤差が出る。
        /// 投影がほぼ 0（ベクトルが軸と平行）のときは false。
        /// </summary>
        public static bool TryGetAngle(Transform target, Transform reference, Axis axis, out float angle)
        {
            angle = 0f;
            if (target == null)
                return false;

            var referenceRotation = reference != null ? reference.rotation : Quaternion.identity;
            var local = Quaternion.Inverse(referenceRotation) * target.rotation;
            GetMeasureDirections(axis, out var zero, out var quarter);
            var measured = local * zero;
            var sin = Vector3.Dot(measured, quarter);
            var cos = Vector3.Dot(measured, zero);
            if (sin * sin + cos * cos < 1e-6f)
                return false;

            angle = Mathf.Atan2(sin, cos) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>出力範囲を「角度の範囲 → 値の範囲」に揃える。任意範囲の角度幅は 360° までに丸める。</summary>
        public static void GetMapping(AxisEntry entry, out float angleMin, out float angleMax, out float valueMin, out float valueMax)
        {
            switch (entry.range)
            {
                case OutputRange.MinusOneToOne:
                    angleMin = -180f; angleMax = 180f; valueMin = -1f; valueMax = 1f;
                    return;
                case OutputRange.ZeroTo360:
                    angleMin = 0f; angleMax = 360f; valueMin = 0f; valueMax = 360f;
                    return;
                case OutputRange.Custom:
                    angleMin = entry.customAngleMin;
                    angleMax = Mathf.Min(entry.customAngleMax, entry.customAngleMin + 360f);
                    valueMin = entry.customValueMin;
                    valueMax = entry.customValueMax;
                    return;
                default:
                    angleMin = 0f; angleMax = 360f; valueMin = 0f; valueMax = 1f;
                    return;
            }
        }

        public static bool IsValidMapping(AxisEntry entry)
        {
            GetMapping(entry, out var angleMin, out var angleMax, out _, out _);
            return angleMax - angleMin > 0.01f;
        }

        /// <summary>
        /// 出力が不連続になる角度。360° 全体を使うときは角度範囲の始点、
        /// 一部だけ使うときは範囲外の弧の中央（最小値側と最大値側が切り替わる位置）。
        /// </summary>
        public static float GetSeamAngle(AxisEntry entry)
        {
            GetMapping(entry, out var angleMin, out var angleMax, out _, out _);
            var span = angleMax - angleMin;
            if (span >= 360f - 0.001f)
                return angleMin;
            return angleMax + (360f - span) * 0.5f;
        }

        /// <summary>不連続点から測った角度 0〜360 に対する出力値。両端は不連続点の左右の極限。</summary>
        public static float EvaluateFromSeam(AxisEntry entry, float angleFromSeam)
        {
            GetMapping(entry, out var angleMin, out var angleMax, out var valueMin, out var valueMax);
            var span = angleMax - angleMin;
            var u = Mathf.Clamp(angleFromSeam, 0f, 360f);
            if (span >= 360f - 0.001f)
                return Mathf.LerpUnclamped(valueMin, valueMax, u / 360f);

            var outside = (360f - span) * 0.5f;
            if (u <= outside)
                return valueMin;
            if (u >= outside + span)
                return valueMax;
            return Mathf.LerpUnclamped(valueMin, valueMax, (u - outside) / span);
        }

        /// <summary>角度（度）に対する出力値。</summary>
        public static float Evaluate(AxisEntry entry, float angle)
        {
            return EvaluateFromSeam(entry, Mathf.Repeat(angle - GetSeamAngle(entry), 360f));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (entries == null)
                return;

            foreach (var entry in entries)
            {
                var target = ResolveTarget(entry);
                if (entry == null || target == null)
                    continue;

                var reference = ResolveReference(entry);
                var referenceRotation = reference != null ? reference.rotation : Quaternion.identity;
                GetMeasureDirections(entry.axis, out var zero, out _);
                var size = UnityEditor.HandleUtility.GetHandleSize(target.position) * 0.5f;

                var axisColor = entry.axis == Axis.X ? Color.red : entry.axis == Axis.Y ? Color.green : Color.blue;
                var axisWorld = referenceRotation * GetAxisVector(entry.axis);
                UnityEditor.Handles.color = new Color(axisColor.r, axisColor.g, axisColor.b, 0.35f);
                UnityEditor.Handles.DrawWireDisc(target.position, axisWorld, size);

                Gizmos.color = new Color(axisColor.r, axisColor.g, axisColor.b, 0.5f);
                Gizmos.DrawLine(target.position, target.position + referenceRotation * zero * size);
                Gizmos.color = axisColor;
                Gizmos.DrawLine(target.position, target.position + target.rotation * zero * size);
            }
        }
#endif
    }
}
