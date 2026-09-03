using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// シーンへ GameObject 階層が作られたとき、配下の PackageVersionChecker を照合する。
    /// </summary>
    [InitializeOnLoad]
    internal static class PackageVersionCheckerPlacementWatcher
    {
        static PackageVersionCheckerPlacementWatcher()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            for (var i = 0; i < stream.length; i++)
            {
                GameObject go = null;
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                        {
                            stream.GetCreateGameObjectHierarchyEvent(i, out var change);
                            go = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                            break;
                        }
                    case ObjectChangeKind.ChangeGameObjectParent:
                        {
                            stream.GetChangeGameObjectParentEvent(i, out var change);
                            go = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                            break;
                        }
                    case ObjectChangeKind.ChangeGameObjectStructure:
                        {
                            stream.GetChangeGameObjectStructureEvent(i, out var change);
                            go = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                            break;
                        }
                }

                if (ShouldIgnore(go))
                    continue;

                var checkers = go.GetComponentsInChildren<PackageVersionChecker>(true);
                if (checkers == null || checkers.Length == 0)
                    continue;

                // ObjectChangeEvents 中にダイアログを出さない。delayCall 側で照合する。
                PackageVersionCheckerService.CheckPlacedHierarchy(go, forceDialog: true);
            }
        }

        private static bool ShouldIgnore(GameObject go)
        {
            if (go == null)
                return true;
            if (EditorUtility.IsPersistent(go))
                return true;
            if ((go.hideFlags & HideFlags.DontSave) != 0)
                return true;
            if (!go.scene.IsValid() || !go.scene.isLoaded)
                return true;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene == go.scene)
                return true;

            return false;
        }
    }
}
