using System;
using UnityEngine;
using nadena.dev.ndmf.runtime;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// ソースの位置（基準からの相対。基準が未指定ならワールド原点からの相対）に倍率をかけた位置へ、
    /// ターゲットを VRC Position Constraint で動かす。
    /// ビルド時（Transforming, MA の後）にターゲットの親の下へ
    /// RemapPosition / Remap_(ターゲット名)（スケール 1/倍率）/ RemapRatio（基準へ追従）/ SourcePosition（ソースへ追従）
    /// を生成し、ターゲットに SourcePosition をローカル解決で追う Position Constraint を付ける。
    /// このコンポーネントの倍率・有効・ウェイト・軸を操作するアニメーションは、生成した Transform / Constraint へ書き換える。
    /// VRCFury 等が後から追加したアニメーションも拾うため、Optimizing（MA の前）でもう一度書き換えてから自身を削除する。
    /// エディタ非再生時は、ビルド後と同じ計算でターゲットの位置を継続適用できる。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB RemapPosition")]
    public class RemapPosition : SamirinMABase
    {
        public const string RatioProperty = nameof(ratio);
        public const string ConstraintActiveProperty = nameof(constraintActive);
        public const string WeightProperty = nameof(weight);
        public const string PositionXProperty = nameof(positionX);
        public const string PositionYProperty = nameof(positionY);
        public const string PositionZProperty = nameof(positionZ);

        /// <summary>倍率 0 はスケール無限大になるため、この絶対値まで丸める。</summary>
        public const float MinAbsRatio = 0.001f;

        /// <summary>Editor から登録されるリグ生成とアニメーション書き換え処理。</summary>
        public static Action<RemapPosition, GameObject> BuildHandler;

        /// <summary>Editor から登録される、後から追加されたアニメーションの書き換え処理。</summary>
        public static Action<RemapPosition, GameObject> RemapAnimationHandler;

        [Tooltip("位置を動かすオブジェクト。コンポーネント追加時は自身。未指定なら自身")]
        public Transform target;

        [Tooltip("位置の元になるオブジェクト")]
        public Transform source;

        [Tooltip("ソースの位置をこのオブジェクトからの相対で測る。未指定ならワールド原点 (0, 0, 0) からの相対")]
        public Transform origin;

        [Tooltip("ソースの相対位置にかける倍率。アニメーションで変更できます")]
        public float ratio = 1f;

        [Tooltip("オフのとき Constraint を止めます。アニメーションで変更できます")]
        public bool constraintActive = true;

        [Tooltip("ターゲットの Position Constraint のウェイト。0 で「ウェイト 0 の位置」、1 でリマップ後の位置。アニメーションで変更できます")]
        [Range(0f, 1f)]
        public float weight = 1f;

        [Tooltip("ウェイト 0 のときのターゲットのローカル位置。コンポーネント追加時・ターゲット変更時に、ターゲットの現在のローカル位置を記録します")]
        public Vector3 restPosition;

        [Tooltip("リマップする軸。アニメーションで変更できます")]
        public bool positionX = true, positionY = true, positionZ = true;

        [Tooltip("オンのとき、エディタ非再生時にもターゲットの位置へ反映します")]
        public bool editorApply = true;

        /// <summary>ビルドで生成した、スケール 1/倍率 の空間（Remap_(ターゲット名)）。</summary>
        [NonSerialized]
        public Transform BuiltScaleSpace;

        /// <summary>ビルドで生成したターゲットの Position Constraint。</summary>
        [NonSerialized]
        public Behaviour BuiltTargetConstraint;

        /// <summary>ビルドで生成した Constraint すべて（ターゲット・RemapRatio・SourcePosition）。</summary>
        [NonSerialized]
        public Behaviour[] BuiltConstraints;

        public Transform ResolvedTarget => target != null ? target : transform;

        public RemapPosition()
        {
            // ControllableHumanoid（Transforming, 50。MA の後）が Armature を分割した後にリグを置く。
            priority = 100;
        }

        private void Reset()
        {
            target = transform;
            restPosition = transform.localPosition;
        }

        /// <summary>倍率から Remap_ に設定するスケールを求める。</summary>
        public static float ToScale(float ratio)
        {
            if (Mathf.Abs(ratio) < MinAbsRatio)
                ratio = ratio < 0f ? -MinAbsRatio : MinAbsRatio;
            return 1f / ratio;
        }

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            // VRCFury は NDMF の Transforming と Optimizing の間に走るため、そこで足されたカーブはここで初めて見える。
            if (buildPhase == SamirinBuildPhase.Optimizing && beforeModularAvatar)
            {
                RemapAnimationHandler?.Invoke(this, avatarRootObject);
                DestroyImmediate(this);
                return;
            }

            // Bone Proxy 等でターゲットの親が変わった後に、親の下へリグを置く。
            if (buildPhase != SamirinBuildPhase.Transforming || beforeModularAvatar)
                return;

            BuildHandler?.Invoke(this, avatarRootObject);
        }

        /// <summary>
        /// ビルド後の Constraint と同じ計算で、ターゲットのローカル位置を求める。
        /// Constraint 無効・ソース未指定・循環する指定のときは false。
        /// </summary>
        public bool TryGetRemappedLocalPosition(out Vector3 localPosition)
        {
            localPosition = default;
            var t = ResolvedTarget;
            var parent = t != null ? t.parent : null;
            if (parent == null || source == null || !constraintActive)
                return false;

            // ターゲットを動かすとソース・基準も動くため、毎フレーム適用するとずれ続ける。
            if (source.IsChildOf(t) || (origin != null && origin.IsChildOf(t)) || RuntimeUtil.IsAvatarRoot(t))
                return false;

            // RemapRatio は基準の位置・親の回転・親のスケール×(1/倍率) の空間になる。
            // その空間での SourcePosition のローカル位置が、そのままターゲットのローカル位置になる。
            var originPosition = origin != null ? origin.position : Vector3.zero;
            var remapped = parent.InverseTransformVector(source.position - originPosition) * (1f / ToScale(ratio));
            var blended = Vector3.LerpUnclamped(restPosition, remapped, Mathf.Clamp01(weight));

            localPosition = t.localPosition;
            if (positionX) localPosition.x = blended.x;
            if (positionY) localPosition.y = blended.y;
            if (positionZ) localPosition.z = blended.z;
            return true;
        }

        /// <summary>
        /// ビルド後と同じ計算でターゲットの位置を適用する。
        /// </summary>
        public void ApplyRemap()
        {
            if (!TryGetRemappedLocalPosition(out var localPosition))
                return;

            var t = ResolvedTarget;
            if (localPosition != t.localPosition)
                t.localPosition = localPosition;
        }

#if UNITY_EDITOR
        private void LateUpdate()
        {
            if (Application.isPlaying || !editorApply)
                return;

            ApplyRemap();
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetRemappedLocalPosition(out var localPosition))
                return;

            var t = ResolvedTarget;
            var remapped = t.parent.TransformPoint(localPosition);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawLine(origin != null ? origin.position : Vector3.zero, source.position);
            Gizmos.DrawWireSphere(remapped, 0.02f);
        }
#endif
    }
}
