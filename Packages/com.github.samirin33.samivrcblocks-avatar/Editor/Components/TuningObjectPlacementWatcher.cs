using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// シーンへ GameObject 階層が作られたとき TuningObject の配置スナップを起動する。
    /// </summary>
    [InitializeOnLoad]
    internal static class TuningObjectPlacementWatcher
    {
        static TuningObjectPlacementWatcher()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.CreateGameObjectHierarchy)
                    continue;

                stream.GetCreateGameObjectHierarchyEvent(i, out var change);
                var go = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                if (go == null) continue;

                var tunings = go.GetComponentsInChildren<TuningObject>(true);
                for (int t = 0; t < tunings.Length; t++)
                {
                    if (tunings[t] != null)
                        tunings[t].SchedulePlacementSnap();
                }
            }
        }
    }
}
