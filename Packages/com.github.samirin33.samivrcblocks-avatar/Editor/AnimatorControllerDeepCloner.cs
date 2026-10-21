using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// AnimatorController をメモリ上に複製する。
    /// レイヤー・ステートマシン・ステート・遷移・StateMachineBehaviour は別オブジェクトとして作り直し、
    /// Motion（AnimationClip / BlendTree）と AvatarMask は元を共有する。
    /// 元アセットを書き換えずにステートや Behaviour だけを変更したいビルド時処理で使う。
    /// </summary>
    internal static class AnimatorControllerDeepCloner
    {
        public static AnimatorController Clone(AnimatorController source, string name = null)
        {
            if (source == null)
                return null;

            return new Cloner().CloneController(source, string.IsNullOrEmpty(name) ? source.name : name);
        }

        private sealed class Cloner
        {
            private readonly Dictionary<AnimatorState, AnimatorState> _states = new Dictionary<AnimatorState, AnimatorState>();
            private readonly Dictionary<AnimatorStateMachine, AnimatorStateMachine> _stateMachines = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>();

            public AnimatorController CloneController(AnimatorController source, string name)
            {
                var clone = new AnimatorController { name = name };
                clone.parameters = source.parameters;

                var sourceLayers = source.layers;
                foreach (var layer in sourceLayers)
                {
                    if (layer.stateMachine != null)
                        AllocateStateMachine(layer.stateMachine);
                }

                // 遷移先の付け替えに全ステートの対応表が必要なため、確保と中身の複製を分ける。
                foreach (var pair in _stateMachines.ToList())
                    FillStateMachine(pair.Key, pair.Value);
                foreach (var pair in _states.ToList())
                    FillState(pair.Key, pair.Value);

                var layers = new AnimatorControllerLayer[sourceLayers.Length];
                for (var i = 0; i < sourceLayers.Length; i++)
                {
                    var src = sourceLayers[i];
                    layers[i] = new AnimatorControllerLayer
                    {
                        name = src.name,
                        avatarMask = src.avatarMask,
                        blendingMode = src.blendingMode,
                        defaultWeight = src.defaultWeight,
                        iKPass = src.iKPass,
                        syncedLayerIndex = src.syncedLayerIndex,
                        syncedLayerAffectsTiming = src.syncedLayerAffectsTiming,
                        stateMachine = src.stateMachine != null
                            ? _stateMachines[src.stateMachine]
                            : new AnimatorStateMachine { name = src.name },
                    };
                }

                for (var i = 0; i < sourceLayers.Length; i++)
                {
                    var src = sourceLayers[i];
                    var syncedIndex = src.syncedLayerIndex;
                    if (syncedIndex < 0 || syncedIndex >= sourceLayers.Length || sourceLayers[syncedIndex].stateMachine == null)
                        continue;

                    foreach (var state in EnumerateStates(sourceLayers[syncedIndex].stateMachine))
                    {
                        if (!_states.TryGetValue(state, out var clonedState))
                            continue;

                        var motion = src.GetOverrideMotion(state);
                        if (motion != null)
                            layers[i].SetOverrideMotion(clonedState, motion);

                        var behaviours = src.GetOverrideBehaviours(state);
                        if (behaviours != null && behaviours.Length > 0)
                            layers[i].SetOverrideBehaviours(clonedState, CloneBehaviours(behaviours));
                    }
                }

                clone.layers = layers;
                return clone;
            }

            private void AllocateStateMachine(AnimatorStateMachine source)
            {
                if (source == null || _stateMachines.ContainsKey(source))
                    return;

                _stateMachines[source] = new AnimatorStateMachine
                {
                    name = source.name,
                    hideFlags = source.hideFlags,
                };

                foreach (var child in source.states)
                {
                    if (child.state == null || _states.ContainsKey(child.state))
                        continue;

                    // AnimatorState は Instantiate で複製できないため、空のステートへシリアライズ値を写す。
                    var clonedState = new AnimatorState();
                    EditorUtility.CopySerialized(child.state, clonedState);
                    _states[child.state] = clonedState;
                }

                foreach (var child in source.stateMachines)
                    AllocateStateMachine(child.stateMachine);
            }

            private void FillStateMachine(AnimatorStateMachine source, AnimatorStateMachine clone)
            {
                clone.anyStatePosition = source.anyStatePosition;
                clone.entryPosition = source.entryPosition;
                clone.exitPosition = source.exitPosition;
                clone.parentStateMachinePosition = source.parentStateMachinePosition;
                clone.behaviours = CloneBehaviours(source.behaviours);

                clone.states = source.states
                    .Where(c => c.state != null)
                    .Select(c => new ChildAnimatorState { state = _states[c.state], position = c.position })
                    .ToArray();
                clone.stateMachines = source.stateMachines
                    .Where(c => c.stateMachine != null)
                    .Select(c => new ChildAnimatorStateMachine { stateMachine = _stateMachines[c.stateMachine], position = c.position })
                    .ToArray();

                // 遷移はステート・子ステートマシンの登録後に設定する。
                clone.entryTransitions = source.entryTransitions
                    .Where(t => t != null)
                    .Select(t => CloneTransition(t))
                    .ToArray();
                clone.anyStateTransitions = source.anyStateTransitions
                    .Where(t => t != null)
                    .Select(t => CloneTransition(t))
                    .ToArray();

                foreach (var child in source.stateMachines)
                {
                    if (child.stateMachine == null)
                        continue;

                    var transitions = source.GetStateMachineTransitions(child.stateMachine)
                        .Where(t => t != null)
                        .Select(t => CloneTransition(t))
                        .ToArray();
                    clone.SetStateMachineTransitions(_stateMachines[child.stateMachine], transitions);
                }

                // 先に設定すると states の代入で上書きされるため最後に設定する。
                clone.defaultState = source.defaultState != null && _states.TryGetValue(source.defaultState, out var defaultState)
                    ? defaultState
                    : null;
            }

            private void FillState(AnimatorState source, AnimatorState clone)
            {
                clone.behaviours = CloneBehaviours(source.behaviours);
                clone.transitions = source.transitions
                    .Where(t => t != null)
                    .Select(t => CloneTransition(t))
                    .ToArray();
            }

            private T CloneTransition<T>(T source) where T : AnimatorTransitionBase
            {
                var clone = Object.Instantiate(source);
                clone.name = source.name;
                clone.hideFlags = source.hideFlags;

                if (source.destinationState != null)
                    clone.destinationState = _states.TryGetValue(source.destinationState, out var state) ? state : null;
                if (source.destinationStateMachine != null)
                    clone.destinationStateMachine = _stateMachines.TryGetValue(source.destinationStateMachine, out var stateMachine) ? stateMachine : null;

                return clone;
            }

            private static StateMachineBehaviour[] CloneBehaviours(StateMachineBehaviour[] source)
            {
                if (source == null || source.Length == 0)
                    return new StateMachineBehaviour[0];

                return source
                    .Where(b => b != null)
                    .Select(b =>
                    {
                        var clone = Object.Instantiate(b);
                        clone.name = b.name;
                        clone.hideFlags = b.hideFlags;
                        return clone;
                    })
                    .ToArray();
            }

            private static IEnumerable<AnimatorState> EnumerateStates(AnimatorStateMachine stateMachine)
            {
                foreach (var child in stateMachine.states)
                {
                    if (child.state != null)
                        yield return child.state;
                }

                foreach (var child in stateMachine.stateMachines)
                {
                    if (child.stateMachine == null)
                        continue;

                    foreach (var state in EnumerateStates(child.stateMachine))
                        yield return state;
                }
            }
        }
    }
}
