using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(ParameterSmoothing))]
    [CanEditMultipleObjects]
    public class ParameterSmoothingEditor : SamirinMABaseEditor
    {
        private SerializedProperty _defaultSmoothWeight;
        private SerializedProperty _parameterSmoothingData;

        private void OnEnable()
        {
            _defaultSmoothWeight = serializedObject.FindProperty("defaultSmoothWeight");
            _parameterSmoothingData = serializedObject.FindProperty("parameterSmoothingData");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "FloatパラメーターにAAPスムージングをかけることができます！",
                    MessageType.Info);

                EditorGUILayout.LabelField("パラメータスムージング設定");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (_defaultSmoothWeight != null)
                    DrawSmoothWeightField(_defaultSmoothWeight, new GUIContent("デフォルトの重み"));

                EditorGUILayout.Space(4);

                if (_parameterSmoothingData != null)
                {
                    for (int i = 0; i < _parameterSmoothingData.arraySize; i++)
                    {
                        var element = _parameterSmoothingData.GetArrayElementAtIndex(i);
                        var parameterNameProp = element.FindPropertyRelative("parameterName");
                        var useDefaultProp = element.FindPropertyRelative("useDefaultSmoothWeight");
                        var smoothWeightProp = element.FindPropertyRelative("smoothWeight");

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(parameterNameProp.stringValue);
                        GUILayout.FlexibleSpace();
                        EditorGUI.BeginDisabledGroup(i == 0);
                        if (GUILayout.Button("↑", GUILayout.Width(24)))
                        {
                            _parameterSmoothingData.MoveArrayElement(i, i - 1);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                        EditorGUI.EndDisabledGroup();
                        EditorGUI.BeginDisabledGroup(i == _parameterSmoothingData.arraySize - 1);
                        if (GUILayout.Button("↓", GUILayout.Width(24)))
                        {
                            _parameterSmoothingData.MoveArrayElement(i, i + 1);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                        EditorGUI.EndDisabledGroup();
                        if (GUILayout.Button("削除", GUILayout.Width(50)))
                        {
                            _parameterSmoothingData.DeleteArrayElementAtIndex(i);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.PropertyField(parameterNameProp, new GUIContent("パラメータ名"));

                        if (useDefaultProp != null)
                        {
                            EditorGUI.BeginChangeCheck();
                            EditorGUILayout.PropertyField(useDefaultProp, new GUIContent("デフォルトの設定値を使う"));
                            if (EditorGUI.EndChangeCheck() && !useDefaultProp.boolValue && _defaultSmoothWeight != null)
                                smoothWeightProp.floatValue = _defaultSmoothWeight.floatValue;

                            if (!useDefaultProp.boolValue)
                                DrawSmoothWeightField(smoothWeightProp);
                        }
                        else
                        {
                            DrawSmoothWeightField(smoothWeightProp);
                        }

                        var paramName = parameterNameProp.stringValue;
                        if (!string.IsNullOrEmpty(paramName))
                        {
                            var smoothedName = $"{paramName}_Smoothed";

                            EditorGUILayout.HelpBox(
                                "以下のパラメータが出力されます。",
                                MessageType.None);

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(smoothedName, GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("コピー", GUILayout.Width(50)))
                                EditorGUIUtility.systemCopyBuffer = smoothedName;
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(3);
                    }

                    if (GUILayout.Button("+ 追加"))
                    {
                        _parameterSmoothingData.arraySize++;
                        var newElement = _parameterSmoothingData.GetArrayElementAtIndex(_parameterSmoothingData.arraySize - 1);
                        var useDefaultProp = newElement.FindPropertyRelative("useDefaultSmoothWeight");
                        if (useDefaultProp != null)
                            useDefaultProp.boolValue = true;
                        var smoothWeightProp = newElement.FindPropertyRelative("smoothWeight");
                        if (smoothWeightProp != null && _defaultSmoothWeight != null)
                            smoothWeightProp.floatValue = _defaultSmoothWeight.floatValue;
                        var parameterNameProp = newElement.FindPropertyRelative("parameterName");
                        if (parameterNameProp != null)
                            parameterNameProp.stringValue = "";
                        var smoothedNameProp = newElement.FindPropertyRelative("smoothedParameterName");
                        if (smoothedNameProp != null)
                            smoothedNameProp.stringValue = "";
                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(8);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("マニュアル生成", EditorStyles.boldLabel);
                if (GUILayout.Button("Animatorをマニュアル生成", GUILayout.Height(15)))
                {
                    serializedObject.ApplyModifiedProperties();
                    ManualGenerateAnimator();
                }
                EditorGUILayout.EndVertical();

                serializedObject.ApplyModifiedProperties();
            });
        }

        private void ManualGenerateAnimator()
        {
            var selected = targets.OfType<ParameterSmoothing>().Where(c => c != null).ToArray();
            if (selected.Length == 0)
                return;

            var avatarRoot = FindAvatarRoot(selected[0].transform);
            if (avatarRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "ParameterSmoothing",
                    "親階層に VRCAvatarDescriptor が見つかりません。アバター配下に配置してから実行してください。",
                    "OK");
                return;
            }

            var all = avatarRoot.GetComponentsInChildren<ParameterSmoothing>(true);
            Undo.RegisterCompleteObjectUndo(all.Cast<Object>().ToArray(), "Manual Generate ParameterSmoothing Animator");

            var controllers = ParameterSmoothingBuilder.BuildManual(avatarRoot, all);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SelectGeneratedAnimators(controllers);

            // EditorUtility.DisplayDialog(
            //     "ParameterSmoothing",
            //     "Animator を生成しました。\n（ResizableSyncParameters の Float と同居している場合は、そちら側の生成に含まれます）",
            //     "OK");
        }

        private static void SelectGeneratedAnimators(UnityEditor.Animations.AnimatorController[] controllers)
        {
            var objects = controllers?.Where(c => c != null).Cast<Object>().ToArray();
            if (objects == null || objects.Length == 0)
                return;

            Selection.objects = objects;
            EditorGUIUtility.PingObject(objects[0]);
        }

        private static GameObject FindAvatarRoot(Transform start)
        {
            var current = start;
            while (current != null)
            {
                if (current.GetComponent<VRCAvatarDescriptor>() != null)
                    return current.gameObject;
                current = current.parent;
            }

            return null;
        }
    }
}
