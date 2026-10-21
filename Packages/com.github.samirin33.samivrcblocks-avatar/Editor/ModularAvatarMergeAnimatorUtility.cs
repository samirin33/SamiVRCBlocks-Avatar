using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using Samirin33.NDMF.Base;
using Samirin33.NDMF.Components;
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

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            // ビルド中に保存・再インポートすると、無関係な dirty アセットまで書き出したりビルド中のアセット編集をまたいだりする。
            if (SamirinBuildContext.IsBuilding)
                return controller;

            if (controller != null)
                SamirinBuildContext.SaveIfPersistent(controller);
            AssetDatabase.ImportAsset(controllerPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        }

        public static AnimatorController EnsurePersistedControllerReference(AnimatorController controller)
        {
            if (controller == null)
                return null;

            // ビルド中に生成した Controller はメモリ上のまま使う（NDMF がビルド終了時に保存する）。
            if (SamirinBuildContext.IsBuilding || !EditorUtility.IsPersistent(controller))
                return controller;

            return ReloadControllerAtPath(AssetDatabase.GetAssetPath(controller));
        }

        /// <summary>
        /// 生成した Controller の後始末。手動生成（Assets に保存したもの）は保存して読み直し、
        /// ビルド中のメモリ上の Controller はそのまま返す。
        /// </summary>
        public static AnimatorController FinalizeGeneratedController(AnimatorController controller)
        {
            if (controller == null)
                return null;

            if (SamirinBuildContext.IsBuilding || !EditorUtility.IsPersistent(controller))
                return controller;

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            return ReloadControllerAtPath(AssetDatabase.GetAssetPath(controller));
        }

        /// <summary>
        /// ビルド中はメモリ上に、手動生成時は manualAssetPath に（既存ファイルを置き換えて）生成用 Controller を作る。
        /// </summary>
        public static AnimatorController CreateGeneratedController(string name, string manualAssetPath)
        {
            if (!SamirinBuildContext.IsBuilding
                && !string.IsNullOrEmpty(manualAssetPath)
                && AssetDatabase.LoadAssetAtPath<AnimatorController>(manualAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(manualAssetPath);
            }

            return SamirinBuildContext.CreateAnimatorController(name, manualAssetPath);
        }

        /// <summary>
        /// GUID で参照する空クリップが無い環境向けの代替。ビルド中はメモリ上に作り、手動生成時は manualAssetPath に保存する。
        /// </summary>
        public static AnimationClip LoadOrCreateEmptyClip(string guid, string manualAssetPath)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                var loadedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (loadedClip != null)
                    return loadedClip;
            }

            if (SamirinBuildContext.IsBuilding || string.IsNullOrEmpty(manualAssetPath))
                return new AnimationClip { name = "Empty" };

            var emptyClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(manualAssetPath);
            if (emptyClip != null)
                return emptyClip;

            SamirinBuildContext.EnsureFolder(System.IO.Path.GetDirectoryName(manualAssetPath)?.Replace('\\', '/'));
            emptyClip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(manualAssetPath) };
            AssetDatabase.CreateAsset(emptyClip, manualAssetPath);
            return emptyClip;
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
            AttachDummyFix(moduleRoot, controller, parentObject);

            EditorUtility.SetDirty(moduleRoot);
            EditorUtility.SetDirty(mergeAnimator);
            return moduleRoot;
        }

        /// <summary>
        /// 生成 Animator 内のダミー Bool を、統合前の FixDummyParameter でアバター側の条件に揃える。
        /// 名前に Dummy を含む Bool と、アバター上で指定済みかつこの Animator にもある名前を対象にする。
        /// </summary>
        private static void AttachDummyFix(GameObject moduleRoot, AnimatorController controller, GameObject parentObject)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (var name in FixDummyParameterProcessor.FindDummyNamedBools(controller))
            {
                if (seen.Add(name))
                    names.Add(name);
            }

            var avatar = parentObject.GetComponentInParent<VRCAvatarDescriptor>(true);
            var searchRoot = avatar != null ? avatar.gameObject : parentObject;
            foreach (var fix in searchRoot.GetComponentsInChildren<FixDummyParameter>(true))
            {
                if (fix == null || fix.dummyParameterNames == null)
                    continue;

                foreach (var raw in fix.dummyParameterNames)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;

                    var name = raw.Trim();
                    if (!seen.Add(name) || !HasBoolParameter(controller, name))
                        continue;

                    names.Add(name);
                }
            }

            if (names.Count == 0)
                return;

            var fixDummy = moduleRoot.AddComponent<FixDummyParameter>();
            fixDummy.dummyParameterNames = names.ToArray();
        }

        private static bool HasBoolParameter(AnimatorController controller, string parameterName)
        {
            foreach (var parameter in controller.parameters)
            {
                if (parameter == null || parameter.type != AnimatorControllerParameterType.Bool)
                    continue;
                if (parameter.name == parameterName)
                    return true;
            }

            return false;
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
