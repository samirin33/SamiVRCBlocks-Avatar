using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.Components;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// ビルド時に生成した Animator Controller を MA Merge Animator へ登録する共通処理。
    /// FPSCounter 等の既存モジュール prefab と同様、専用子オブジェクトに Animator + Merge Animator を配置する。
    /// </summary>
    internal static class ModularAvatarMergeAnimatorUtility
    {
        public static AnimatorController ReloadControllerAtPath(string controllerPath)
        {
            if (string.IsNullOrEmpty(controllerPath))
                return null;

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(controllerPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        }

        public static AnimatorController EnsurePersistedControllerReference(AnimatorController controller)
        {
            if (controller == null)
                return null;

            var controllerPath = AssetDatabase.GetAssetPath(controller);
            if (string.IsNullOrEmpty(controllerPath))
                return controller;

            return ReloadControllerAtPath(controllerPath);
        }

        public static GameObject RegisterMergeAnimatorModule(
            GameObject parentObject,
            string moduleObjectName,
            AnimatorController controller,
            int layerPriority = 0,
            bool matchAvatarWriteDefaults = false)
        {
            controller = EnsurePersistedControllerReference(controller);
            if (parentObject == null || controller == null)
                return null;

            RemoveExistingModule(parentObject.transform, moduleObjectName);

            var moduleRoot = new GameObject(moduleObjectName);
            moduleRoot.transform.SetParent(parentObject.transform, false);

            var animator = moduleRoot.AddComponent<Animator>();
            AssignAnimatorController(animator, controller);

            var mergeAnimator = moduleRoot.AddComponent<ModularAvatarMergeAnimator>();
            ConfigureMergeAnimator(mergeAnimator, controller, layerPriority, matchAvatarWriteDefaults);

            EditorUtility.SetDirty(moduleRoot);
            EditorUtility.SetDirty(mergeAnimator);
            return moduleRoot;
        }

        private static void RemoveExistingModule(Transform parent, string moduleObjectName)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name == moduleObjectName)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void AssignAnimatorController(Animator animator, RuntimeAnimatorController controller)
        {
            var so = new SerializedObject(animator);
            var controllerProp = so.FindProperty("m_Controller");
            if (controllerProp != null)
                controllerProp.objectReferenceValue = controller;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureMergeAnimator(
            ModularAvatarMergeAnimator mergeAnimator,
            RuntimeAnimatorController controller,
            int layerPriority,
            bool matchAvatarWriteDefaults)
        {
            var so = new SerializedObject(mergeAnimator);
            var animatorProp = so.FindProperty("animator");
            if (animatorProp != null)
                animatorProp.objectReferenceValue = controller;

            var layerTypeProp = so.FindProperty("layerType");
            if (layerTypeProp != null)
                layerTypeProp.intValue = (int)VRCAvatarDescriptor.AnimLayerType.FX;

            var deleteAnimatorProp = so.FindProperty("deleteAttachedAnimator");
            if (deleteAnimatorProp != null)
                deleteAnimatorProp.boolValue = true;

            var matchWdProp = so.FindProperty("matchAvatarWriteDefaults");
            if (matchWdProp != null)
                matchWdProp.boolValue = matchAvatarWriteDefaults;

            var layerPriorityProp = so.FindProperty("layerPriority");
            if (layerPriorityProp != null)
                layerPriorityProp.intValue = layerPriority;

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
