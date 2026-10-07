using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(FixDummyParameter))]
    public class FixDummyParameterEditor : SamirinMABaseEditor
    {
        private SerializedProperty _dummyParameterNames;

        private void OnEnable()
        {
            _dummyParameterNames = serializedObject.FindProperty(nameof(FixDummyParameter.dummyParameterNames));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "同じオブジェクトの MA Merge Animator が統合する Animator と、マージ先の Animator で、指定したダミー Bool の遷移条件が true / false で違うとき、統合される側をアバター側の条件に揃えます。このパッケージが生成する Animator も、同じ名前か名前に Dummy を含む Bool を揃えます。",
                    MessageType.Info);

                EditorGUILayout.PropertyField(_dummyParameterNames, new GUIContent("ダミーパラメーター"), true);

                var component = (FixDummyParameter)target;
                var merges = component.GetComponents<ModularAvatarMergeAnimator>();
                if (merges == null || merges.Length == 0)
                {
                    EditorGUILayout.HelpBox("同じオブジェクトに MA Merge Animator がありません。", MessageType.Warning);
                }
                else if (GUILayout.Button("名前に Dummy を含む Bool を追加"))
                {
                    AddDummyNamedBools(_dummyParameterNames, merges);
                }

                if (merges != null)
                {
                    foreach (var merge in merges)
                        DrawMergeStatus(component, merge);
                }

                serializedObject.ApplyModifiedProperties();
            });
        }

        private static void AddDummyNamedBools(SerializedProperty namesProp, ModularAvatarMergeAnimator[] merges)
        {
            var existing = new HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < namesProp.arraySize; i++)
            {
                var value = namesProp.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(value))
                    existing.Add(value.Trim());
            }

            foreach (var merge in merges)
            {
                if (merge == null)
                    continue;

                foreach (var name in FixDummyParameterProcessor.FindDummyNamedBools(merge.animator))
                {
                    if (!existing.Add(name))
                        continue;

                    var index = namesProp.arraySize;
                    namesProp.arraySize = index + 1;
                    namesProp.GetArrayElementAtIndex(index).stringValue = name;
                }
            }
        }

        private void DrawMergeStatus(FixDummyParameter component, ModularAvatarMergeAnimator merge)
        {
            if (merge == null)
                return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("MA Merge Animator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Layer", merge.layerType.ToString());

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Animator", merge.animator, typeof(RuntimeAnimatorController), false);
            }

            if (merge.animator == null)
            {
                EditorGUILayout.HelpBox("Animator が割り当てられていません。", MessageType.Warning);
                return;
            }

            if (merge.mergeAnimatorMode == MergeAnimatorMode.Replace)
            {
                EditorGUILayout.HelpBox(
                    "Replace はマージ先を置き換えるため、このコンポーネントでは条件を揃えません。",
                    MessageType.Warning);
                return;
            }

            var source = FixDummyParameterProcessor.UnwrapController(merge.animator);
            var destination = FindDestinationController(component, merge);
            if (destination.controller == null)
            {
                EditorGUILayout.HelpBox(destination.message, MessageType.Warning);
                return;
            }

            if (_dummyParameterNames.arraySize == 0)
            {
                EditorGUILayout.HelpBox("ダミーパラメーター名が空です。", MessageType.Warning);
                return;
            }

            for (var i = 0; i < _dummyParameterNames.arraySize; i++)
            {
                var parameterName = _dummyParameterNames.GetArrayElementAtIndex(i).stringValue;
                if (string.IsNullOrWhiteSpace(parameterName))
                    continue;

                parameterName = parameterName.Trim();
                var sourceUsage = FixDummyParameterProcessor.GetUsage(source, parameterName);
                var destinationUsage = FixDummyParameterProcessor.GetUsage(destination.controller, parameterName);
                DrawParameterStatus(parameterName, sourceUsage, destinationUsage);
            }
        }

        private static void DrawParameterStatus(
            string parameterName,
            FixDummyParameterProcessor.DummyUsage source,
            FixDummyParameterProcessor.DummyUsage destination)
        {
            var sourceLabel = FixDummyParameterProcessor.Describe(source);
            var destinationLabel = FixDummyParameterProcessor.Describe(destination);
            var message = $"{parameterName}\n統合される Animator: {sourceLabel}\nマージ先: {destinationLabel}";

            if (source.Polarity == FixDummyParameterProcessor.DummyUsage.Kind.Absent
                || destination.Polarity == FixDummyParameterProcessor.DummyUsage.Kind.Absent)
            {
                EditorGUILayout.HelpBox(message, MessageType.None);
                return;
            }

            if (!source.IsDecisive || !destination.IsDecisive)
            {
                EditorGUILayout.HelpBox(message + "\nこのパラメーターは揃えません。", MessageType.Warning);
                return;
            }

            if (source.Polarity == destination.Polarity)
            {
                EditorGUILayout.HelpBox(message + "\n使用条件は一致しています。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                message + $"\nビルド時に統合される Animator をマージ先の {destinationLabel} に揃えます。",
                MessageType.Warning);
        }

        private static (AnimatorController controller, string message) FindDestinationController(
            FixDummyParameter component,
            ModularAvatarMergeAnimator merge)
        {
            var avatar = component.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (avatar == null)
            {
                return (null, "親に VRC Avatar Descriptor がありません。プレハブ単体ではマージ先を比較できません。");
            }

            foreach (var candidate in avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (candidate == null || candidate == merge)
                    continue;
                if (candidate.layerType != merge.layerType || candidate.mergeAnimatorMode != MergeAnimatorMode.Replace)
                    continue;

                var replaceController = FixDummyParameterProcessor.UnwrapController(candidate.animator);
                if (replaceController == null)
                    return (null, "Replace の MA Merge Animator に Animator が割り当てられていません。");
                return (replaceController, null);
            }

            var layerController = FindCustomLayerController(avatar, merge.layerType);
            if (layerController == null)
            {
                return (null, $"{merge.layerType} レイヤーにカスタム Animator がありません。");
            }

            return (layerController, null);
        }

        private static AnimatorController FindCustomLayerController(
            VRCAvatarDescriptor descriptor,
            VRCAvatarDescriptor.AnimLayerType layerType)
        {
            var controller = FindLayerController(descriptor.baseAnimationLayers, layerType);
            if (controller != null)
                return controller;
            return FindLayerController(descriptor.specialAnimationLayers, layerType);
        }

        private static AnimatorController FindLayerController(
            VRCAvatarDescriptor.CustomAnimLayer[] layers,
            VRCAvatarDescriptor.AnimLayerType layerType)
        {
            if (layers == null)
                return null;

            foreach (var layer in layers)
            {
                if (layer.type != layerType || layer.isDefault)
                    continue;
                return FixDummyParameterProcessor.UnwrapController(layer.animatorController);
            }

            return null;
        }
    }
}
