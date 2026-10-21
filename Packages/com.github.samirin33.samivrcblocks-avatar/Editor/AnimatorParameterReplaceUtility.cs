using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Samirin33.NDMF.Base;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// AnimatorController 内の任意パラメータ参照を別名に一括置換するユーティリティ。
    /// 他機能からも利用可能な共通実装。
    /// </summary>
    public static class AnimatorParameterReplaceUtility
    {
        /// <summary>
        /// 指定コントローラ内のパラメータ参照を fromParamName → toParamName に付け替える。
        /// 対象: BlendTree の blendParameter/Y と Direct 子の directBlendParameter、Transition の Condition、
        /// State の Motion Speed/Time 用パラメータ（m_SpeedParameter, m_TimeParameter 等）、
        /// AnimationClip のカーブバインド。
        /// fromParamName 自体はパラメーター一覧から削除しない。参照が無くなっても残す。
        /// VRC Parameter Driver の書き込み先（parameters[].name）と読み取り元（source）は変えない。
        /// excludedLayerNames に含まれる名前のレイヤーは置換対象外とする。
        /// 書き換えが必要な AnimationClip / BlendTree は複製してから差し替えるため、
        /// 除外レイヤーや他のコントローラと共有しているモーションには影響しない。
        /// </summary>
        public static void ReplaceParameterReferences(AnimatorController controller, string fromParamName, string toParamName, IReadOnlyCollection<string> excludedLayerNames = null)
        {
            if (controller == null || string.IsNullOrEmpty(fromParamName) || string.IsNullOrEmpty(toParamName))
                return;
            if (fromParamName == toParamName)
                return;

            var excludeSet = excludedLayerNames != null && excludedLayerNames.Count > 0
                ? new HashSet<string>(excludedLayerNames, System.StringComparer.Ordinal)
                : null;

            // 参照を外したあとも、_Smoothed でない元パラメーター定義は残す。
            var preserved = TryCaptureParameter(controller, fromParamName, out var preservedParameter)
                ? preservedParameter
                : (ParameterSnapshot?)null;

            EnsureParameterExists(controller, toParamName);

            var rewriter = new MotionRewriter(controller, fromParamName, toParamName);
            var layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (excludeSet != null && excludeSet.Contains(layer.name))
                    continue;
                ReplaceInStateMachine(layer.stateMachine, fromParamName, toParamName, rewriter);
            }

            if (preserved.HasValue)
                RestoreParameterIfMissing(controller, preserved.Value);

            if (!SamirinBuildContext.IsBuilding)
                EditorUtility.SetDirty(controller);
        }

        private struct ParameterSnapshot
        {
            public string name;
            public AnimatorControllerParameterType type;
            public float defaultFloat;
            public int defaultInt;
            public bool defaultBool;
        }

        private static bool TryCaptureParameter(AnimatorController controller, string paramName, out ParameterSnapshot snapshot)
        {
            foreach (var parameter in controller.parameters)
            {
                if (parameter.name != paramName)
                    continue;

                snapshot = new ParameterSnapshot
                {
                    name = parameter.name,
                    type = parameter.type,
                    defaultFloat = parameter.defaultFloat,
                    defaultInt = parameter.defaultInt,
                    defaultBool = parameter.defaultBool,
                };
                return true;
            }

            snapshot = default;
            return false;
        }

        private static void RestoreParameterIfMissing(AnimatorController controller, ParameterSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(snapshot.name))
                return;
            if (controller.parameters.Any(p => p.name == snapshot.name))
                return;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = snapshot.name,
                type = snapshot.type,
                defaultFloat = snapshot.defaultFloat,
                defaultInt = snapshot.defaultInt,
                defaultBool = snapshot.defaultBool,
            });
        }

        private static void EnsureParameterExists(AnimatorController controller, string paramName)
        {
            if (controller.parameters.Any(p => p.name == paramName))
                return;
            controller.AddParameter(paramName, AnimatorControllerParameterType.Float);
        }

        private static void ReplaceInStateMachine(AnimatorStateMachine stateMachine, string from, string to, MotionRewriter rewriter)
        {
            if (stateMachine == null) return;

            foreach (var state in stateMachine.states)
            {
                var s = state.state;
                if (s == null) continue;
                ReplaceInStateMotionParameters(s, from, to);
                // Parameter Driver の書き込み先は付け替えない。Behaviour は走査しない。
                var motion = rewriter.Rewrite(s.motion);
                if (motion != s.motion)
                    s.motion = motion;
            }

            foreach (var childSm in stateMachine.stateMachines)
                ReplaceInStateMachine(childSm.stateMachine, from, to, rewriter);

            ReplaceInTransitionsBase(stateMachine.anyStateTransitions, from, to);
            ReplaceInTransitionsBase(stateMachine.entryTransitions, from, to);

            foreach (var state in stateMachine.states)
            {
                if (state.state == null) continue;
                ReplaceInTransitionsBase(state.state.transitions, from, to);
            }

            foreach (var childSm in stateMachine.stateMachines)
            {
                if (childSm.stateMachine == null) continue;
                foreach (var transition in stateMachine.GetStateMachineTransitions(childSm.stateMachine))
                    ReplaceInTransition(transition, from, to);
            }
        }

        /// <summary>
        /// State の Motion Speed / Motion Time 等で参照しているパラメータ名（m_SpeedParameter, m_TimeParameter 等）を置換。
        /// </summary>
        private static void ReplaceInStateMotionParameters(AnimatorState state, string from, string to)
        {
            if (state == null) return;
            var so = new SerializedObject(state);
            bool changed = false;
            foreach (var propName in new[] { "m_SpeedParameter", "m_TimeParameter", "m_MirrorParameter", "m_CycleOffsetParameter" })
            {
                var prop = so.FindProperty(propName);
                if (prop != null && prop.stringValue == from)
                {
                    prop.stringValue = to;
                    changed = true;
                }
            }
            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ReplaceInTransitionsBase(IEnumerable<AnimatorTransitionBase> transitions, string from, string to)
        {
            if (transitions == null) return;
            foreach (var t in transitions)
                ReplaceInTransition(t, from, to);
        }

        private static void ReplaceInTransition(AnimatorTransitionBase transition, string from, string to)
        {
            if (transition == null) return;

            var so = new SerializedObject(transition);
            var conditions = so.FindProperty("m_Conditions");
            if (conditions == null) return;

            var changed = false;
            for (int i = 0; i < conditions.arraySize; i++)
            {
                var ev = conditions.GetArrayElementAtIndex(i).FindPropertyRelative("m_ConditionEvent");
                if (ev != null && ev.stringValue == from)
                {
                    ev.stringValue = to;
                    changed = true;
                }
            }

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 置換が必要なモーションだけを複製して書き換える。同じモーションは同じ複製に対応させる。
        /// </summary>
        private sealed class MotionRewriter
        {
            private readonly AnimatorController _controller;
            private readonly string _from;
            private readonly string _to;
            private readonly Dictionary<Motion, Motion> _rewritten = new Dictionary<Motion, Motion>();
            private readonly Dictionary<Motion, bool> _referencesCache = new Dictionary<Motion, bool>();

            public MotionRewriter(AnimatorController controller, string from, string to)
            {
                _controller = controller;
                _from = from;
                _to = to;
            }

            public Motion Rewrite(Motion motion)
            {
                if (motion == null || !References(motion))
                    return motion;

                if (_rewritten.TryGetValue(motion, out var cached))
                    return cached;

                Motion result;
                if (motion is BlendTree tree)
                {
                    var clone = new BlendTree();
                    EditorUtility.CopySerialized(tree, clone);
                    clone.name = tree.name;
                    _rewritten[motion] = clone;
                    RewriteBlendTree(clone);
                    result = clone;
                }
                else if (motion is AnimationClip clip)
                {
                    var clone = Object.Instantiate(clip);
                    clone.name = clip.name;
                    _rewritten[motion] = clone;
                    RewriteClip(clone);
                    result = clone;
                }
                else
                {
                    return motion;
                }

                RegisterClone(motion, result);
                return result;
            }

            private void RegisterClone(Motion original, Motion clone)
            {
                if (SamirinBuildContext.IsBuilding)
                {
                    ObjectRegistry.TryRegisterReplacedObject(ObjectRegistry.GetReference(original), clone);
                    return;
                }

                // ビルド外で保存済みコントローラを書き換える場合、複製をサブアセットにしないと再読み込みで参照が切れる。
                AnimatorControllerAssetUtility.EnsureSubAsset(clone, _controller);
            }

            private bool References(Motion motion)
            {
                if (motion == null)
                    return false;
                if (_referencesCache.TryGetValue(motion, out var cached))
                    return cached;

                // 循環参照に備えて先に false を入れておく。
                _referencesCache[motion] = false;

                var result = false;
                if (motion is BlendTree tree)
                {
                    result = tree.blendParameter == _from
                             || tree.blendParameterY == _from
                             || tree.children.Any(c => c.directBlendParameter == _from || References(c.motion));
                }
                else if (motion is AnimationClip clip)
                {
                    result = AnimationUtility.GetCurveBindings(clip)
                        .Any(b => b.type == typeof(Animator) && b.propertyName == _from);
                }

                _referencesCache[motion] = result;
                return result;
            }

            private void RewriteBlendTree(BlendTree tree)
            {
                if (tree.blendParameter == _from)
                    tree.blendParameter = _to;
                if (tree.blendParameterY == _from)
                    tree.blendParameterY = _to;

                var children = tree.children;
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].directBlendParameter == _from)
                        children[i].directBlendParameter = _to;
                    children[i].motion = Rewrite(children[i].motion);
                }
                tree.children = children;
            }

            private void RewriteClip(AnimationClip clip)
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type != typeof(Animator) || binding.propertyName != _from)
                        continue;

                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null) continue;

                    var newBinding = binding;
                    newBinding.propertyName = _to;
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                    AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                }
            }
        }
    }
}
