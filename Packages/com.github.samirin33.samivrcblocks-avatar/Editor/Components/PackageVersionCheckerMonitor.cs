using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// シーン配置・コンポーネント追加時に PackageVersionChecker を起動する。
    /// </summary>
    [InitializeOnLoad]
    internal static class PackageVersionCheckerMonitor
    {
        static PackageVersionCheckerMonitor()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
            PrefabUtility.prefabInstanceUpdated += OnPrefabInstanceUpdated;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += CheckAllInOpenScenes;
        }

        private static void OnComponentAdded(Component component)
        {
            if (component is PackageVersionChecker checker)
                PackageVersionCheckerService.ScheduleCheck(checker, forceDialog: true);
        }

        private static void OnPrefabInstanceUpdated(GameObject instance)
        {
            if (instance == null)
                return;

            foreach (var checker in instance.GetComponentsInChildren<PackageVersionChecker>(true))
                PackageVersionCheckerService.ScheduleCheck(checker, forceDialog: true);
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            CheckScene(scene, forceDialog: false);
        }

        private static void CheckAllInOpenScenes()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                CheckScene(SceneManager.GetSceneAt(i), forceDialog: false);
        }

        private static void CheckScene(Scene scene, bool forceDialog)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var checker in root.GetComponentsInChildren<PackageVersionChecker>(true))
                    PackageVersionCheckerService.ScheduleCheck(checker, forceDialog);
            }
        }
    }
}
