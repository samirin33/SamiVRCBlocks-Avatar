using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(DominantHandOption))]
    [CanEditMultipleObjects]
    public class DominantHandOptionEditor : SamirinMABaseEditor
    {
        const string ShowDeveloperSettingsPrefsKey = "Samirin33.DominantHandOption.ShowDeveloperSettings";
        const string DeveloperSettingsMenuPath = "CONTEXT/DominantHandOption/開発者設定を隠す";

        static readonly string[] HandLabels = { "左手", "右手" };
        static readonly int[] HandValues =
        {
            (int)DominantHandOption.DominantHand.Left,
            (int)DominantHandOption.DominantHand.Right,
        };

        static bool ShowDeveloperSettings
        {
            get => EditorPrefs.GetBool(ShowDeveloperSettingsPrefsKey, false);
            set => EditorPrefs.SetBool(ShowDeveloperSettingsPrefsKey, value);
        }

        [MenuItem(DeveloperSettingsMenuPath, false, 1000)]
        static void ToggleDeveloperSettings()
        {
            ShowDeveloperSettings = !ShowDeveloperSettings;
            InternalEditorUtility.RepaintAllViews();
        }

        [MenuItem(DeveloperSettingsMenuPath, true)]
        static bool ToggleDeveloperSettingsValidate()
        {
            Menu.SetChecked(DeveloperSettingsMenuPath, ShowDeveloperSettings);
            return true;
        }

        SerializedProperty _dominantHand;
        SerializedProperty _entries;

        void OnEnable()
        {
            _dominantHand = serializedObject.FindProperty(nameof(DominantHandOption.dominantHand));
            _entries = serializedObject.FindProperty(nameof(DominantHandOption.entries));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                var showDeveloperSettings = ShowDeveloperSettings;
                if (showDeveloperSettings)
                {
                    DrawHelpBoxWithDefaultFont(
                        "利き手を選ぶと、リストに登録した MA Bone Proxy の Humanoid ボーン、FixHandVector の Hand Type、任意の Transform の位置・回転を、その利き手用の値に切り替えます。\n" +
                        "同じオブジェクトの TuningObject も、その移動に合わせて動きます。\n" +
                        "Bone Proxy の Humanoid や Transform の姿勢を合わせたあと「現在値を記録」し、数値は「選択中の利き手を適用」で反映します。\n" +
                        "アップロード時（MA より前）にもう一度適用し、このコンポーネントは削除されます。",
                        MessageType.Info);
                }

                var handChanged = DrawHandSelector();
                var applyClicked = false;
                if (showDeveloperSettings)
                {
                    EditorGUILayout.Space(6);
                    DrawEntries();
                    EditorGUILayout.Space(4);
                    applyClicked = GUILayout.Button("選択中の利き手を適用");
                    if (GUILayout.Button("開発者設定を隠す"))
                        ShowDeveloperSettings = false;
                }

                var undoGroup = -1;
                if (handChanged || applyClicked)
                {
                    Undo.SetCurrentGroupName("利き手を適用");
                    undoGroup = Undo.GetCurrentGroup();
                }

                serializedObject.ApplyModifiedProperties();

                if (handChanged || applyClicked)
                {
                    ApplyToTargets();
                    Undo.CollapseUndoOperations(undoGroup);
                }
            });
        }

        bool DrawHandSelector()
        {
            EditorGUILayout.LabelField("ギミックを使用する手を選択してください！");

            EditorGUI.BeginChangeCheck();

            if (_dominantHand.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(_dominantHand, GUIContent.none);
            }
            else
            {
                var current = 0;
                for (var i = 0; i < HandValues.Length; i++)
                {
                    if (HandValues[i] == _dominantHand.enumValueIndex)
                    {
                        current = i;
                        break;
                    }
                }

                var next = GUILayout.Toolbar(current, HandLabels, GUILayout.Height(28));
                if (next != current)
                    _dominantHand.enumValueIndex = HandValues[next];
            }

            return EditorGUI.EndChangeCheck();
        }

        void DrawEntries()
        {
            EditorGUILayout.LabelField("切り替えリスト");

            if (_entries.arraySize == 0)
            {
                DrawHelpBoxWithDefaultFont(
                    "指定がありません。「指定を追加」から Bone Proxy、Fix Hand Vector、または Transform を登録してください。",
                    MessageType.Info);
            }

            var removeIndex = -1;
            var moveFrom = -1;
            var moveTo = -1;

            for (var i = 0; i < _entries.arraySize; i++)
            {
                var element = _entries.GetArrayElementAtIndex(i);
                DrawEntry(element, i, ref removeIndex, ref moveFrom, ref moveTo);
            }

            if (removeIndex >= 0)
                _entries.DeleteArrayElementAtIndex(removeIndex);
            else if (moveFrom >= 0)
                _entries.MoveArrayElement(moveFrom, moveTo);

            if (GUILayout.Button("指定を追加"))
                AddEntry();
        }

        void DrawEntry(
            SerializedProperty element,
            int index,
            ref int removeIndex,
            ref int moveFrom,
            ref int moveTo)
        {
            var typeProp = element.FindPropertyRelative(nameof(DominantHandOption.Entry.type));
            var type = (DominantHandOption.EntryType)typeProp.enumValueIndex;
            var typeName = TypeName(type);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, $"{index + 1}. {typeName}", true);

            if (element.isExpanded)
            {
                EditorGUILayout.PropertyField(typeProp, new GUIContent("種類"));

                switch (type)
                {
                    case DominantHandOption.EntryType.BoneProxy:
                        DrawBoneProxyFields(element);
                        break;
                    case DominantHandOption.EntryType.FixHandVector:
                        DrawFixHandVectorFields(element);
                        break;
                    default:
                        DrawTransformFields(element);
                        break;
                }

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("上へ"))
                    {
                        moveFrom = index;
                        moveTo = index - 1;
                    }
                }

                using (new EditorGUI.DisabledScope(index >= _entries.arraySize - 1))
                {
                    if (GUILayout.Button("下へ"))
                    {
                        moveFrom = index;
                        moveTo = index + 1;
                    }
                }

                if (GUILayout.Button("削除"))
                    removeIndex = index;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        void DrawBoneProxyFields(SerializedProperty element)
        {
            var boneProxy = element.FindPropertyRelative(nameof(DominantHandOption.Entry.boneProxy));
            var boneRight = element.FindPropertyRelative(nameof(DominantHandOption.Entry.boneRight));
            var boneLeft = element.FindPropertyRelative(nameof(DominantHandOption.Entry.boneLeft));

            EditorGUILayout.PropertyField(boneProxy, new GUIContent("Bone Proxy"));
            EditorGUILayout.PropertyField(boneRight, new GUIContent("右手", "右手のとき Bone Proxy が参照する Humanoid ボーン"));
            EditorGUILayout.PropertyField(boneLeft, new GUIContent("左手", "左手のとき Bone Proxy が参照する Humanoid ボーン"));

            var proxy = boneProxy.objectReferenceValue as ModularAvatarBoneProxy;
            using (new EditorGUI.DisabledScope(proxy == null || targets.Length != 1))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("現在値を右手へ記録"))
                    CaptureBoneProxy(element, proxy, right: true);
                if (GUILayout.Button("現在値を左手へ記録"))
                    CaptureBoneProxy(element, proxy, right: false);
                EditorGUILayout.EndHorizontal();
            }
        }

        static string TypeName(DominantHandOption.EntryType type)
        {
            switch (type)
            {
                case DominantHandOption.EntryType.BoneProxy:
                    return "Bone Proxy";
                case DominantHandOption.EntryType.FixHandVector:
                    return "Fix Hand Vector";
                default:
                    return "Transform";
            }
        }

        void DrawFixHandVectorFields(SerializedProperty element)
        {
            var fixHandVector = element.FindPropertyRelative(nameof(DominantHandOption.Entry.fixHandVector));
            EditorGUILayout.PropertyField(
                fixHandVector,
                new GUIContent("Fix Hand Vector", "選択中の利き手に Hand Type を合わせ、回転補正をかけ直します"));
        }

        void DrawTransformFields(SerializedProperty element)
        {
            var target = element.FindPropertyRelative(nameof(DominantHandOption.Entry.target));
            var setPosition = element.FindPropertyRelative(nameof(DominantHandOption.Entry.setPosition));
            var localPosition = element.FindPropertyRelative(nameof(DominantHandOption.Entry.localPosition));
            var positionRight = element.FindPropertyRelative(nameof(DominantHandOption.Entry.positionRight));
            var positionLeft = element.FindPropertyRelative(nameof(DominantHandOption.Entry.positionLeft));
            var setRotation = element.FindPropertyRelative(nameof(DominantHandOption.Entry.setRotation));
            var localRotation = element.FindPropertyRelative(nameof(DominantHandOption.Entry.localRotation));
            var rotationRight = element.FindPropertyRelative(nameof(DominantHandOption.Entry.rotationRight));
            var rotationLeft = element.FindPropertyRelative(nameof(DominantHandOption.Entry.rotationLeft));

            EditorGUILayout.PropertyField(target, new GUIContent("Transform"));

            EditorGUILayout.PropertyField(setPosition, new GUIContent("位置を設定する"));
            if (setPosition.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(localPosition, new GUIContent("ローカル座標"));
                EditorGUILayout.PropertyField(positionRight, new GUIContent("右手"));
                EditorGUILayout.PropertyField(positionLeft, new GUIContent("左手"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(setRotation, new GUIContent("回転を設定する"));
            if (setRotation.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(localRotation, new GUIContent("ローカル回転"));
                EditorGUILayout.PropertyField(rotationRight, new GUIContent("右手"));
                EditorGUILayout.PropertyField(rotationLeft, new GUIContent("左手"));
                EditorGUI.indentLevel--;
            }

            var transform = target.objectReferenceValue as Transform;
            using (new EditorGUI.DisabledScope(transform == null || targets.Length != 1))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("現在値を右手へ記録"))
                    CaptureTransform(element, transform, right: true);
                if (GUILayout.Button("現在値を左手へ記録"))
                    CaptureTransform(element, transform, right: false);
                EditorGUILayout.EndHorizontal();
            }
        }

        static void CaptureBoneProxy(SerializedProperty element, ModularAvatarBoneProxy proxy, bool right)
        {
            if (proxy == null)
                return;

            var boneProp = element.FindPropertyRelative(right
                ? nameof(DominantHandOption.Entry.boneRight)
                : nameof(DominantHandOption.Entry.boneLeft));
            boneProp.enumValueIndex = (int)proxy.boneReference;
        }

        static void CaptureTransform(SerializedProperty element, Transform transform, bool right)
        {
            if (transform == null)
                return;

            var setPosition = element.FindPropertyRelative(nameof(DominantHandOption.Entry.setPosition));
            if (setPosition.boolValue)
            {
                var local = element.FindPropertyRelative(nameof(DominantHandOption.Entry.localPosition)).boolValue;
                var positionProp = element.FindPropertyRelative(right
                    ? nameof(DominantHandOption.Entry.positionRight)
                    : nameof(DominantHandOption.Entry.positionLeft));
                positionProp.vector3Value = local ? transform.localPosition : transform.position;
            }

            var setRotation = element.FindPropertyRelative(nameof(DominantHandOption.Entry.setRotation));
            if (setRotation.boolValue)
            {
                var local = element.FindPropertyRelative(nameof(DominantHandOption.Entry.localRotation)).boolValue;
                var rotationProp = element.FindPropertyRelative(right
                    ? nameof(DominantHandOption.Entry.rotationRight)
                    : nameof(DominantHandOption.Entry.rotationLeft));
                rotationProp.vector3Value = local ? transform.localEulerAngles : transform.eulerAngles;
            }
        }

        void AddEntry()
        {
            _entries.arraySize++;
            var element = _entries.GetArrayElementAtIndex(_entries.arraySize - 1);
            element.isExpanded = true;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.type)).enumValueIndex =
                (int)DominantHandOption.EntryType.BoneProxy;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.boneProxy)).objectReferenceValue = null;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.boneRight)).enumValueIndex =
                (int)HumanBodyBones.RightHand;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.boneLeft)).enumValueIndex =
                (int)HumanBodyBones.LeftHand;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.fixHandVector)).objectReferenceValue = null;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.target)).objectReferenceValue = null;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.setPosition)).boolValue = true;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.localPosition)).boolValue = true;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.positionRight)).vector3Value = Vector3.zero;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.positionLeft)).vector3Value = Vector3.zero;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.setRotation)).boolValue = true;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.localRotation)).boolValue = true;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.rotationRight)).vector3Value = Vector3.zero;
            element.FindPropertyRelative(nameof(DominantHandOption.Entry.rotationLeft)).vector3Value = Vector3.zero;
        }

        void ApplyToTargets()
        {
            var mutated = new List<Object>();
            var options = new List<DominantHandOption>();
            var samples = new List<List<DominantHandOption.MovedTransformSample>>();

            foreach (var selected in targets)
            {
                if (selected is not DominantHandOption option)
                    continue;

                options.Add(option);
                option.CollectApplyTargets(mutated);

                var sample = new List<DominantHandOption.MovedTransformSample>();
                option.SampleMovedTransforms(sample);
                samples.Add(sample);

                var tuning = option.GetComponent<TuningObject>();
                if (tuning != null)
                {
                    mutated.Add(tuning);
                    mutated.Add(tuning.transform);
                }
            }

            if (mutated.Count > 0)
                Undo.RecordObjects(mutated.ToArray(), "利き手を適用");

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                option.Apply();
                ReapplyFixHandVectors(option);
                option.MoveTuningObject(samples[i], recordUndo: false);
                EditorUtility.SetDirty(option);
            }

            foreach (var mutatedObject in mutated)
                EditorUtility.SetDirty(mutatedObject);
        }

        static void ReapplyFixHandVectors(DominantHandOption option)
        {
            if (option.entries == null)
                return;

            for (var i = 0; i < option.entries.Count; i++)
            {
                var entry = option.entries[i];
                if (entry == null || entry.type != DominantHandOption.EntryType.FixHandVector)
                    continue;

                FixHandVectorApplier.ApplyNow(entry.fixHandVector, recordUndo: false);
            }
        }
    }
}
