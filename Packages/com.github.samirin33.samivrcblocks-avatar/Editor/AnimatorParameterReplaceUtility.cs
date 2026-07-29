using System.Collections.Generic;
using System.Linq;
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
        /// 指定コントローラ内のパラメータ参照を fromParamName → toParamName に置換する。
        /// 対象: パラメータ一覧、BlendTree の blendParameter/Y、Transition の Condition、
        /// State の Motion Speed/Time 用パラメータ（m_SpeedParameter, m_TimeParameter 等）、
        /// StateMachineBehaviour（ParameterDriver 等）の parameters 配列内 name/source、AnimationClip のカーブバインド。
        /// excludedLayerNames に含まれる名前のレイヤーは置換対象外とする。
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

            EnsureParameterExists(controller, toParamName);

            for (int i = 0; i < controller.layers.Length; i++)
            {
                var layer = controller.layers[i];
                if (excludeSet != null && excludeSet.Contains(layer.name))
                    continue;
                ReplaceInStateMachine(layer.stateMachine, fromParamName, toParamName);
            }

            var clips = CollectReferencedClips(controller, excludeSet);
            foreach (var clip in clips)
            {
                if (clip != null)
                    ReplaceParameterInClip(clip, fromParamName, toParamName);
            }

            EditorUtility.SetDirty(controller);
        }

        private static void EnsureParameterExists(AnimatorController controller, string paramName)
        {
            if (controller.parameters.Any(p => p.name == paramName))
                return;
            controller.AddParameter(paramName, AnimatorControllerParameterType.Float);
        }

        private static void ReplaceInStateMachine(AnimatorStateMachine stateMachine, string from, string to)
        {
            if (stateMachine == null) return;

            foreach (var state in stateMachine.states)
            {
                var s = state.state;
                ReplaceInStateMotionParameters(s, from, to);
                // ReplaceInStateBehaviours(s, from, to);
                if (s.motion is BlendTree blendTree)
                    ReplaceInBlendTree(blendTree, from, to);
            }

            foreach (var childSm in stateMachine.stateMachines)
                ReplaceInStateMachine(childSm.stateMachine, from, to);

            ReplaceInTransitionsBase(stateMachine.anyStateTransitions, from, to);
            ReplaceInTransitionsBase(stateMachine.entryTransitions, from, to);

            foreach (var state in stateMachine.states)
            {
                ReplaceInTransitionsBase(state.state.transitions, from, to);
            }

            foreach (var childSm in stateMachine.stateMachines)
            {
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

        // /// <summary>
        // /// State にアタッチされた StateMachineBehaviour（ParameterDriver 等）の parameters 内 name/source を置換。
        // /// </summary>
        // private static void ReplaceInStateBehaviours(AnimatorState state, string from, string to)
        // {
        //     if (state == null) return;
        //     var so = new SerializedObject(state);
        //     var behArray = so.FindProperty("m_StateMachineBehaviours");
        //     if (behArray == null || behArray.arraySize == 0) return;
        //     for (int i = 0; i < behArray.arraySize; i++)
        //     {
        //         var refProp = behArray.GetArrayElementAtIndex(i);
        //         if (refProp?.objectReferenceValue is StateMachineBehaviour beh)
        //             ReplaceParameterInBehaviour(beh, from, to);
        //     }
        // }

        // /// <summary>
        // /// ParameterDriver 等の Behaviour 内で、parameters 配列の name / source が from のものを to に置換する。
        // /// </summary>
        // private static void ReplaceParameterInBehaviour(StateMachineBehaviour behaviour, string from, string to)
        // {
        //     if (behaviour == null) return;
        //     var so = new SerializedObject(behaviour);
        //     var parametersProp = so.FindProperty("parameters");
        //     if (parametersProp == null || parametersProp.arraySize == 0) return;

        //     bool changed = false;
        //     for (int i = 0; i < parametersProp.arraySize; i++)
        //     {
        //         var entry = parametersProp.GetArrayElementAtIndex(i);
        //         var nameProp = entry.FindPropertyRelative("name");
        //         var sourceProp = entry.FindPropertyRelative("source");
        //         if (nameProp != null && nameProp.stringValue == from) { nameProp.stringValue = to; changed = true; }
        //         if (sourceProp != null && sourceProp.stringValue == from) { sourceProp.stringValue = to; changed = true; }
        //     }
        //     if (changed)
        //         so.ApplyModifiedPropertiesWithoutUndo();
        // }

        private static void ReplaceInBlendTree(BlendTree tree, string from, string to)
        {
            if (tree == null) return;

            var so = new SerializedObject(tree);
            var blendParam = so.FindProperty("m_BlendParameter");
            var blendParamY = so.FindProperty("m_BlendParameterY");
            bool changed = false;
            if (blendParam != null && blendParam.stringValue == from) { blendParam.stringValue = to; changed = true; }
            if (blendParamY != null && blendParamY.stringValue == from) { blendParamY.stringValue = to; changed = true; }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(tree);
            var childs = so.FindProperty("m_Childs");
            if (childs != null)
            {
                for (int i = 0; i < childs.arraySize; i++)
                {
                    var motionProp = childs.GetArrayElementAtIndex(i).FindPropertyRelative("m_Motion");
                    if (motionProp?.objectReferenceValue is BlendTree childTree)
                        ReplaceInBlendTree(childTree, from, to);
                }
            }
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

            for (int i = 0; i < conditions.arraySize; i++)
            {
                var ev = conditions.GetArrayElementAtIndex(i).FindPropertyRelative("m_ConditionEvent");
                if (ev != null && ev.stringValue == from)
                {
                    ev.stringValue = to;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static HashSet<AnimationClip> CollectReferencedClips(AnimatorController controller, HashSet<string> excludedLayerNames = null)
        {
            var set = new HashSet<AnimationClip>();
            for (int i = 0; i < controller.layers.Length; i++)
            {
                var layer = controller.layers[i];
                if (excludedLayerNames != null && excludedLayerNames.Contains(layer.name))
                    continue;
                CollectClipsFromStateMachine(layer.stateMachine, set);
            }
            return set;
        }

        private static void CollectClipsFromStateMachine(AnimatorStateMachine stateMachine, HashSet<AnimationClip> set)
        {
            if (stateMachine == null) return;

            foreach (var state in stateMachine.states)
            {
                var motion = state.state.motion;
                if (motion is AnimationClip clip)
                    set.Add(clip);
                else if (motion is BlendTree tree)
                    CollectClipsFromBlendTree(tree, set);
            }
            foreach (var childSm in stateMachine.stateMachines)
                CollectClipsFromStateMachine(childSm.stateMachine, set);
        }

        private static void CollectClipsFromBlendTree(BlendTree tree, HashSet<AnimationClip> set)
        {
            if (tree == null) return;
            foreach (var child in tree.children)
            {
                if (child.motion is AnimationClip clip)
                    set.Add(clip);
                else if (child.motion is BlendTree childTree)
                    CollectClipsFromBlendTree(childTree, set);
            }
        }

        private static void ReplaceParameterInClip(AnimationClip clip, string fromParamName, string toParamName)
        {
            if (clip == null) return;

            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (binding.type != typeof(Animator) || binding.propertyName != fromParamName)
                    continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;

                var newBinding = binding;
                newBinding.propertyName = toParamName;
                AnimationUtility.SetEditorCurve(clip, binding, null);
                AnimationUtility.SetEditorCurve(clip, newBinding, curve);
            }
            EditorUtility.SetDirty(clip);
        }
    }
}
