using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Samirin33.NDMF.Components.Editor
{
    internal static class AnimatorControllerAssetUtility
    {
        public static void EnsureSubAsset(Object obj, Object mainAsset)
        {
            if (obj == null || mainAsset == null) return;

            var mainPath = AssetDatabase.GetAssetPath(mainAsset);
            if (string.IsNullOrEmpty(mainPath)) return;
            if (AssetDatabase.GetAssetPath(obj) == mainPath)
                return;

            AssetDatabase.AddObjectToAsset(obj, mainAsset);
        }

        public static void RegisterControllerHierarchy(AnimatorController controller)
        {
            if (controller == null) return;

            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine != null)
                    RegisterStateMachineHierarchy(layer.stateMachine, controller);
            }
        }

        private static void RegisterStateMachineHierarchy(AnimatorStateMachine stateMachine, AnimatorController controller)
        {
            if (stateMachine == null || controller == null) return;

            EnsureSubAsset(stateMachine, controller);

            foreach (var transition in stateMachine.anyStateTransitions)
                EnsureSubAsset(transition, controller);

            foreach (var transition in stateMachine.entryTransitions)
                EnsureSubAsset(transition, controller);

            foreach (var childState in stateMachine.states)
            {
                if (childState.state == null) continue;
                EnsureSubAsset(childState.state, controller);
                foreach (var transition in childState.state.transitions)
                    EnsureSubAsset(transition, controller);
            }

            foreach (var childMachine in stateMachine.stateMachines)
            {
                if (childMachine.stateMachine == null) continue;
                foreach (var transition in stateMachine.GetStateMachineTransitions(childMachine.stateMachine))
                    EnsureSubAsset(transition, controller);
                RegisterStateMachineHierarchy(childMachine.stateMachine, controller);
            }

            EditorUtility.SetDirty(stateMachine);
        }
    }
}
