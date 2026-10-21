using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Dynamics.Constraint.Components;
using Samirin33.NDMF.Animation;
using Samirin33.NDMF.Base;
using Samirin33.NDMF.Constraints;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// RemapPosition のビルド時処理。
    /// </summary>
    [InitializeOnLoad]
    internal static class RemapPositionProcessor
    {
        private const string IsActiveProperty = "IsActive";
        private const string GlobalWeightProperty = "GlobalWeight";

        private static readonly string[] LocalScaleProperties =
        {
            "m_LocalScale.x",
            "m_LocalScale.y",
            "m_LocalScale.z",
        };

        /// <summary>倍率カーブを 1/倍率 へ変換するときの、キー間の直線補間との許容誤差（相対）。</summary>
        private const float ReciprocalTolerance = 0.002f;
        private const int MinSubdivisionDepth = 2;
        // 倍率 1→100 の直線でも 0 付近で許容誤差に収まる深さ。分割は誤差の大きい区間だけで起きる。
        private const int MaxSubdivisionDepth = 12;

        static RemapPositionProcessor()
        {
            RemapPosition.BuildHandler = Build;
            RemapPosition.RemapAnimationHandler = RemapAnimations;
        }

        #region Build

        public static void Build(RemapPosition component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null)
                return;

            if (!Validate(component, avatarRootObject))
                return;

            var origin = component.origin != null ? component.origin : WorldFix.GetWorldOriginTransform();
            if (origin == null)
            {
                SamirinBuildContext.ReportError(
                    "[RemapPosition] 基準が未指定のため World プレハブ（ワールド原点）を使おうとしましたが、見つかりませんでした。パッケージを再インストールしてください。",
                    component);
                return;
            }

            var context = SamirinBuildContext.Current;
            if (context == null)
            {
                SamirinBuildContext.ReportError(
                    "[RemapPosition] NDMF のビルド外ではリグの生成とアニメーションの書き換えを行えません。",
                    component);
                return;
            }

            using (var animators = new SamirinAnimatorServicesScope(context))
            {
                BuildRig(component, origin);
                ExpandAnimations(component, animators.Services);
            }
        }

        private static bool Validate(RemapPosition component, GameObject avatarRootObject)
        {
            var avatarRoot = avatarRootObject.transform;
            var target = component.ResolvedTarget;
            var source = component.source;
            var origin = component.origin;

            if (source == null)
            {
                SamirinBuildContext.ReportWarning("[RemapPosition] ソースが指定されていないためスキップします。", component);
                return false;
            }

            if (target == avatarRoot || target.parent == null || !target.IsChildOf(avatarRoot))
            {
                SamirinBuildContext.ReportWarning(
                    "[RemapPosition] ターゲットはアバタールート配下（ルート自身を除く）のオブジェクトを指定してください。",
                    component, target);
                return false;
            }

            // ターゲットを動かすとソース・基準も動き、Constraint が循環する。
            if (source.IsChildOf(target))
            {
                SamirinBuildContext.ReportWarning(
                    "[RemapPosition] ソースがターゲット自身またはその子のため、循環を避けてスキップします。",
                    component, source);
                return false;
            }

            if (origin != null && origin.IsChildOf(target))
            {
                SamirinBuildContext.ReportWarning(
                    "[RemapPosition] 基準がターゲット自身またはその子のため、循環を避けてスキップします。",
                    component, origin);
                return false;
            }

            if (target.TryGetComponent<VRCPositionConstraint>(out _) || target.TryGetComponent<VRCParentConstraint>(out _))
            {
                SamirinBuildContext.ReportWarning(
                    "[RemapPosition] ターゲットに既に位置を動かす VRC Constraint があります。生成する Position Constraint と競合する可能性があります。",
                    component, target);
            }

            return true;
        }

        /// <param name="origin">基準。未指定時は World プレハブ（シーン外のアセット）で、Constraint のソースにするとワールド原点を指す。</param>
        private static void BuildRig(RemapPosition component, Transform origin)
        {
            var target = component.ResolvedTarget;
            var parent = target.parent;

            // ターゲットの親と同じ空間に置くことで、SourcePosition のローカル位置がそのままターゲットのローカル位置になる。
            var container = CreateChild(parent, UniqueChildName(parent, "RemapPosition"));
            var scaleSpace = CreateChild(container, "Remap_(" + target.name + ")");
            scaleSpace.localScale = Vector3.one * RemapPosition.ToScale(component.ratio);
            var ratioSpace = CreateChild(scaleSpace, "RemapRatio");
            var follow = CreateChild(ratioSpace, "SourcePosition");

            var constraints = new List<Behaviour>
            {
                AddPositionConstraint(ratioSpace.gameObject, origin, false, Vector3.zero, component.constraintActive, 1f),
                AddPositionConstraint(follow.gameObject, component.source, false, Vector3.zero, component.constraintActive, 1f),
            };

            // エディタ反映でターゲットの現在位置は書き換わっているため、ウェイト 0 の位置は記録済みの値を使う。
            var targetConstraint = AddPositionConstraint(
                target.gameObject, follow, true, component.restPosition, component.constraintActive, component.weight);
            targetConstraint.AffectsPositionX = component.positionX;
            targetConstraint.AffectsPositionY = component.positionY;
            targetConstraint.AffectsPositionZ = component.positionZ;
            constraints.Insert(0, targetConstraint);

            component.BuiltScaleSpace = scaleSpace;
            component.BuiltTargetConstraint = targetConstraint;
            component.BuiltConstraints = constraints.ToArray();
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// <summary>
        /// 同名の兄弟があるとアニメーションのパスが先頭の同名オブジェクトに解決されるため、名前を重複させない。
        /// </summary>
        private static string UniqueChildName(Transform parent, string baseName)
        {
            var names = new HashSet<string>();
            foreach (Transform child in parent)
                names.Add(child.name);

            if (!names.Contains(baseName))
                return baseName;

            for (var i = 1; ; i++)
            {
                var candidate = baseName + " (" + i + ")";
                if (!names.Contains(candidate))
                    return candidate;
            }
        }

        private static VRCPositionConstraint AddPositionConstraint(
            GameObject gameObject,
            Transform source,
            bool solveInLocalSpace,
            Vector3 positionAtRest,
            bool isActive,
            float weight)
        {
            var raw = gameObject.AddComponent<VRCPositionConstraint>();
            raw.AffectsPositionX = true;
            raw.AffectsPositionY = true;
            raw.AffectsPositionZ = true;

            var wrapper = (VRCPositionConstraintWrapper)AllConstraint.FromComponent(raw);
            wrapper.TargetTransform = null;
            wrapper.SolveInLocalSpace = solveInLocalSpace;
            wrapper.AddSource(source, 1f);
            // ActivateConstraint は現在の姿勢を保つオフセットを焼き込むため呼ばない。オフセット 0 で追従させる。
            wrapper.PositionOffset = Vector3.zero;
            wrapper.PositionAtRest = positionAtRest;
            wrapper.Weight = Mathf.Clamp01(weight);
            wrapper.IsActive = isActive;
            wrapper.Locked = true;
            return raw;
        }

        #endregion

        #region Animation

        /// <summary>
        /// Build 後に追加された Animator（VRCFury 等）にある RemapPosition 向けカーブを、生成したリグへ展開する。
        /// </summary>
        public static void RemapAnimations(RemapPosition component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null || component.BuiltScaleSpace == null)
                return;

            var context = SamirinBuildContext.Current;
            if (context == null)
                return;

            using (var animators = new SamirinAnimatorServicesScope(context))
            {
                ExpandAnimations(component, animators.Services);
            }
        }

        private static void ExpandAnimations(RemapPosition component, AnimatorServicesContext services)
        {
            // 仮想コントローラ内のパスはスコープを開いた時点のもの。新しいカーブも同じ基準のパスで書く。
            var remapper = services.ObjectPathRemapper;
            var componentPath = remapper.GetVirtualPathForObject(component.transform);
            var scalePath = remapper.GetVirtualPathForObject(component.BuiltScaleSpace);

            var targetConstraint = component.BuiltTargetConstraint;
            var targetConstraintPath = targetConstraint != null
                ? remapper.GetVirtualPathForObject(targetConstraint.transform)
                : null;

            var constraintPaths = (component.BuiltConstraints ?? new Behaviour[0])
                .Where(c => c != null)
                .Select(c => remapper.GetVirtualPathForObject(c.transform))
                .ToList();

            var clips = new List<VirtualClip>(services.AnimationIndex.GetClipsForObjectPath(componentPath));
            foreach (var clip in clips)
            {
                if (clip == null || clip.IsMarkerClip)
                    continue;

                var bindings = new List<EditorCurveBinding>(clip.GetFloatCurveBindings());
                foreach (var binding in bindings)
                {
                    if (binding.type != typeof(RemapPosition))
                        continue;
                    if ((binding.path ?? string.Empty) != componentPath)
                        continue;

                    var curve = clip.GetFloatCurve(binding);
                    clip.SetFloatCurve(binding, null);
                    if (curve == null)
                        continue;

                    switch (binding.propertyName)
                    {
                        case RemapPosition.RatioProperty:
                            var scaleCurve = ToScaleCurve(curve);
                            foreach (var property in LocalScaleProperties)
                                clip.SetFloatCurve(EditorCurveBinding.FloatCurve(scalePath, typeof(Transform), property), CloneCurve(scaleCurve));
                            break;
                        case RemapPosition.ConstraintActiveProperty:
                            foreach (var path in constraintPaths)
                                clip.SetFloatCurve(EditorCurveBinding.FloatCurve(path, typeof(VRCPositionConstraint), IsActiveProperty), CloneCurve(curve));
                            break;
                        case RemapPosition.WeightProperty:
                            SetTargetConstraintCurve(clip, targetConstraintPath, GlobalWeightProperty, curve);
                            break;
                        case RemapPosition.PositionXProperty:
                            SetTargetConstraintCurve(clip, targetConstraintPath, "AffectsPositionX", curve);
                            break;
                        case RemapPosition.PositionYProperty:
                            SetTargetConstraintCurve(clip, targetConstraintPath, "AffectsPositionY", curve);
                            break;
                        case RemapPosition.PositionZProperty:
                            SetTargetConstraintCurve(clip, targetConstraintPath, "AffectsPositionZ", curve);
                            break;
                    }
                }
            }
        }

        private static void SetTargetConstraintCurve(VirtualClip clip, string path, string propertyName, AnimationCurve curve)
        {
            if (path == null)
                return;
            clip.SetFloatCurve(EditorCurveBinding.FloatCurve(path, typeof(VRCPositionConstraint), propertyName), curve);
        }

        private static AnimationCurve CloneCurve(AnimationCurve curve)
        {
            return new AnimationCurve(curve.keys)
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode,
            };
        }

        /// <summary>
        /// 倍率カーブを Remap_ のスケール（1/倍率）カーブへ変換する。
        /// 1/x は非線形なので、キー間は倍率側の補間結果を細かくサンプリングし、直線補間で近似できるまで分割する。
        /// </summary>
        private static AnimationCurve ToScaleCurve(AnimationCurve ratioCurve)
        {
            var source = ratioCurve.keys;
            var keys = new List<Keyframe>();
            var steppedAfter = new List<bool>();

            if (source.Length == 0)
                return new AnimationCurve();

            AddKey(keys, steppedAfter, source[0].time, RemapPosition.ToScale(source[0].value));

            for (var i = 0; i < source.Length - 1; i++)
            {
                var a = source[i];
                var b = source[i + 1];
                var endScale = RemapPosition.ToScale(b.value);

                if (float.IsInfinity(a.outTangent) || float.IsInfinity(b.inTangent))
                {
                    steppedAfter[steppedAfter.Count - 1] = true;
                    AddKey(keys, steppedAfter, b.time, endScale);
                    continue;
                }

                if (Mathf.Approximately(a.value, b.value) && a.outTangent == 0f && b.inTangent == 0f)
                {
                    AddKey(keys, steppedAfter, b.time, endScale);
                    continue;
                }

                AppendSegment(ratioCurve, keys, steppedAfter, a.time, RemapPosition.ToScale(a.value), b.time, endScale, 0);
            }

            var result = new AnimationCurve(keys.ToArray())
            {
                preWrapMode = ratioCurve.preWrapMode,
                postWrapMode = ratioCurve.postWrapMode,
            };

            // AnimationCurve はキーをマージすることがあるため、実際のキー数でループする。
            var count = result.keys.Length;
            for (var i = 0; i < count; i++)
            {
                var stepBefore = i > 0 && i - 1 < steppedAfter.Count && steppedAfter[i - 1];
                var stepAfter = i < steppedAfter.Count && steppedAfter[i];
                AnimationUtility.SetKeyLeftTangentMode(result, i,
                    stepBefore ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(result, i,
                    stepAfter ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear);
            }

            return result;
        }

        private static void AppendSegment(
            AnimationCurve ratioCurve,
            List<Keyframe> keys,
            List<bool> steppedAfter,
            float t0,
            float s0,
            float t1,
            float s1,
            int depth)
        {
            if (depth < MaxSubdivisionDepth)
            {
                var tm = (t0 + t1) * 0.5f;
                var sm = RemapPosition.ToScale(ratioCurve.Evaluate(tm));
                var linear = (s0 + s1) * 0.5f;
                if (depth < MinSubdivisionDepth
                    || Mathf.Abs(sm - linear) > ReciprocalTolerance * Mathf.Max(Mathf.Abs(sm), 1e-4f))
                {
                    AppendSegment(ratioCurve, keys, steppedAfter, t0, s0, tm, sm, depth + 1);
                    AppendSegment(ratioCurve, keys, steppedAfter, tm, sm, t1, s1, depth + 1);
                    return;
                }
            }

            AddKey(keys, steppedAfter, t1, s1);
        }

        private static void AddKey(List<Keyframe> keys, List<bool> steppedAfter, float time, float value)
        {
            keys.Add(new Keyframe(time, value));
            steppedAfter.Add(false);
        }

        #endregion
    }
}
