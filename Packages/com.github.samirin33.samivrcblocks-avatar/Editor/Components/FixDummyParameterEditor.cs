using System.Collections.Generic;
using System.Text;
using nadena.dev.modular_avatar.core;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Samirin33.NDMF.Components.Editor
{
    // 同じ GameObject の MA Merge Animator を target から参照するため、複数選択編集には対応しない
    [CustomEditor(typeof(FixDummyParameter))]
    public class FixDummyParameterEditor : SamirinMABaseEditor
    {
        private SerializedProperty _dummyParameterNames;

        private sealed class MergeStatus
        {
            public ModularAvatarMergeAnimator Merge;
            public string Message;
            public MessageType MessageType;
            public readonly List<(string message, MessageType type)> Parameters = new List<(string, MessageType)>();
        }

        private readonly List<MergeStatus> _statusCache = new List<MergeStatus>();
        private ModularAvatarMergeAnimator[] _mergesCache;
        private string _cacheSignature;

        private void OnEnable()
        {
            _dummyParameterNames = serializedObject.FindProperty(nameof(FixDummyParameter.dummyParameterNames));
            InvalidateCache();
            EditorApplication.hierarchyChanged += InvalidateCache;
            EditorApplication.projectChanged += InvalidateCache;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= InvalidateCache;
            EditorApplication.projectChanged -= InvalidateCache;
        }

        private void InvalidateCache()
        {
            _cacheSignature = null;
            _mergesCache = null;
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                DrawHelpBoxWithDefaultFont(
                    "同じオブジェクトの MA Merge Animator が統合する Animator と、マージ先の Animator で、指定したダミー Bool の遷移条件が true / false で違うとき、統合される側をアバター側の条件に揃えます。このパッケージが生成する Animator も、同じ名前か名前に Dummy を含む Bool を揃えます。",
                    MessageType.Info);

                EditorGUILayout.PropertyField(_dummyParameterNames, new GUIContent("ダミーパラメーター"), true);

                var component = (FixDummyParameter)target;
                if (_mergesCache == null)
                    _mergesCache = component.GetComponents<ModularAvatarMergeAnimator>();
                var merges = _mergesCache;

                if (merges.Length == 0)
                {
                    DrawHelpBoxWithDefaultFont("同じオブジェクトに MA Merge Animator がありません。", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("名前に Dummy を含む Bool を追加"))
                        AddDummyNamedBools(_dummyParameterNames, merges);
                    if (GUILayout.Button(new GUIContent("再チェック", "Animator の状態を読み直して比較結果を更新します"), GUILayout.Width(80)))
                        InvalidateCache();
                    EditorGUILayout.EndHorizontal();
                }

                if (_mergesCache != null)
                {
                    EnsureStatusCache(component, _mergesCache);
                    foreach (var status in _statusCache)
                        DrawMergeStatus(status);
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

        private List<string> CollectParameterNames()
        {
            var names = new List<string>();
            for (var i = 0; i < _dummyParameterNames.arraySize; i++)
            {
                var parameterName = _dummyParameterNames.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrWhiteSpace(parameterName))
                    names.Add(parameterName.Trim());
            }

            return names;
        }

        private static string BuildSignature(ModularAvatarMergeAnimator[] merges, List<string> names, int nameCount)
        {
            var builder = new StringBuilder();
            builder.Append(nameCount).Append('|');
            foreach (var name in names)
                builder.Append(name).Append('\n');
            foreach (var merge in merges)
            {
                if (merge == null)
                {
                    builder.Append("null;");
                    continue;
                }

                builder.Append(merge.GetInstanceID()).Append(',')
                    .Append(merge.animator != null ? merge.animator.GetInstanceID() : 0).Append(',')
                    .Append((int)merge.layerType).Append(',')
                    .Append((int)merge.mergeAnimatorMode).Append(';');
            }

            return builder.ToString();
        }

        private void EnsureStatusCache(FixDummyParameter component, ModularAvatarMergeAnimator[] merges)
        {
            var names = CollectParameterNames();
            var signature = BuildSignature(merges, names, _dummyParameterNames.arraySize);
            if (signature == _cacheSignature)
                return;

            _cacheSignature = signature;
            _statusCache.Clear();
            foreach (var merge in merges)
            {
                if (merge != null)
                    _statusCache.Add(BuildMergeStatus(component, merge, names));
            }
        }

        private MergeStatus BuildMergeStatus(
            FixDummyParameter component,
            ModularAvatarMergeAnimator merge,
            List<string> names)
        {
            var status = new MergeStatus { Merge = merge };

            if (merge.animator == null)
            {
                status.Message = "Animator が割り当てられていません。";
                status.MessageType = MessageType.Warning;
                return status;
            }

            if (merge.mergeAnimatorMode == MergeAnimatorMode.Replace)
            {
                status.Message = "Replace はマージ先を置き換えるため、このコンポーネントでは条件を揃えません。";
                status.MessageType = MessageType.Warning;
                return status;
            }

            var source = FixDummyParameterProcessor.UnwrapController(merge.animator);
            var destination = FindDestinationController(component, merge);
            if (destination.controller == null)
            {
                status.Message = destination.message;
                status.MessageType = MessageType.Warning;
                return status;
            }

            if (_dummyParameterNames.arraySize == 0)
            {
                status.Message = "ダミーパラメーター名が空です。";
                status.MessageType = MessageType.Warning;
                return status;
            }

            foreach (var parameterName in names)
            {
                var sourceUsage = FixDummyParameterProcessor.GetUsage(source, parameterName);
                var destinationUsage = FixDummyParameterProcessor.GetUsage(destination.controller, parameterName);
                status.Parameters.Add(DescribeParameterStatus(parameterName, sourceUsage, destinationUsage));
            }

            return status;
        }

        private static void DrawMergeStatus(MergeStatus status)
        {
            var merge = status.Merge;
            if (merge == null)
                return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("MA Merge Animator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("レイヤー", merge.layerType.ToString());

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Animator", merge.animator, typeof(RuntimeAnimatorController), false);
            }

            if (!string.IsNullOrEmpty(status.Message))
            {
                DrawHelpBoxWithDefaultFont(status.Message, status.MessageType);
                return;
            }

            foreach (var (message, type) in status.Parameters)
                DrawHelpBoxWithDefaultFont(message, type);
        }

        private static (string message, MessageType type) DescribeParameterStatus(
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
                return (message, MessageType.None);
            }

            if (!source.IsDecisive || !destination.IsDecisive)
                return (message + "\nこのパラメーターは揃えません。", MessageType.Warning);

            if (source.Polarity == destination.Polarity)
                return (message + "\n使用条件は一致しています。", MessageType.Info);

            return (message + $"\nビルド時に統合される Animator をマージ先の {destinationLabel} に揃えます。", MessageType.Warning);
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
