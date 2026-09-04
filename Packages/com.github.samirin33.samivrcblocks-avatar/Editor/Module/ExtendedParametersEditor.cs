using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Samirin33.Editor;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Module;

namespace Samirin33.NDMF.Module.Editor
{
    [CustomEditor(typeof(ExtendedParameters))]
    public class ExtendedParametersEditor : SamirinMABaseEditor
    {
        private const string ParameterPrefabsFolderGUID = "c657f4c093306024f8e25fa39b21baf0";

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "Animatorで新しく取得できるパラメーターを追加することができます！",
                    MessageType.Info);

                // parameterPrefabs以外をデフォルト表示
                DrawPropertiesExcluding(serializedObject, "parameterPrefabs", "m_Script");

                // プレファブ選択UI（モジュール用フォルダから選択）
                DrawParameterPrefabsSelector();

                serializedObject.ApplyModifiedProperties();

                var extendedParameters = (ExtendedParameters)target;

                if (extendedParameters.parameterPrefabs != null && extendedParameters.parameterPrefabs.Length > 0)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("取得できるパラメーター");

                    foreach (var prefab in extendedParameters.parameterPrefabs)
                    {
                        if (prefab == null) continue;

                        var paramInfos = prefab.GetComponentsInChildren<ExtendedParamInfo>(true);
                        if (paramInfos.Length == 0) continue;

                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField(prefab.name);
                        EditorGUILayout.EndVertical();

                        foreach (var paramInfo in paramInfos)
                        {
                            var componentPath = GetGameObjectPath(paramInfo.gameObject, prefab.transform);

                            if (paramInfo.paramInfos == null || paramInfo.paramInfos.Length == 0)
                            {
                                DrawHelpBoxWithDefaultFont("パラメータ情報がありません", MessageType.Info);
                                continue;
                            }

                            for (var paramIndex = 0; paramIndex < paramInfo.paramInfos.Length; paramIndex++)
                            {
                                var param = paramInfo.paramInfos[paramIndex];
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.PrefixLabel($"    {param.paramName}");
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("コピー", GUILayout.Width(50)))
                                {
                                    EditorGUIUtility.systemCopyBuffer = param.paramName ?? "";
                                }
                                if (GUILayout.Button("削除", GUILayout.Width(50)))
                                {
                                    var so = new SerializedObject(paramInfo);
                                    var paramInfosProp = so.FindProperty("paramInfos");
                                    if (paramInfosProp != null && paramIndex >= 0 && paramIndex < paramInfosProp.arraySize)
                                    {
                                        so.Update();
                                        paramInfosProp.DeleteArrayElementAtIndex(paramIndex);
                                        so.ApplyModifiedProperties();
                                        EditorUtility.SetDirty(paramInfo);
                                        AssetDatabase.SaveAssets();
                                    }
                                    break;
                                }
                                EditorGUILayout.EndHorizontal();

                                var explanation = param.paramExplanation ?? "";
                                var defaultStr = GetDefaultValueString(param);
                                var wordWrappedLabel = EditorStyles.wordWrappedLabel;

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField($"Type: {param.paramType.ToString()}", GUILayout.Width(80));
                                EditorGUILayout.LabelField(string.IsNullOrEmpty(defaultStr) ? "" : $"Default: {defaultStr}", GUILayout.Width(120));
                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                DrawWithDefaultFont(() => EditorGUILayout.LabelField(explanation, wordWrappedLabel));
                                EditorGUILayout.EndVertical();
                            }
                        }

                        EditorGUILayout.EndVertical();
                    }
                }

                EditorGUILayout.Space(10);
                if (GUILayout.Button("Animatorにパラメーターを追加"))
                {
                    AddMissingParametersToAnimator(extendedParameters);
                }
            });
        }

        private static void AddMissingParametersToAnimator(ExtendedParameters extendedParameters)
        {
            var animator = extendedParameters.GetComponentInParent<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("エラー", "自身または親にAnimatorが見つかりません。", "OK");
                return;
            }

            var controller = GetAnimatorController(animator);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("エラー", "AnimatorにAnimatorControllerが設定されていません。\nAnimatorOverrideControllerの場合はベースのAnimatorControllerを編集します。", "OK");
                return;
            }

            var paramsToAdd = CollectParamsFromPrefabs(extendedParameters);
            if (paramsToAdd.Count == 0)
            {
                EditorUtility.DisplayDialog("情報", "追加するパラメーターがありません。", "OK");
                return;
            }

            var existing = new HashSet<string>();
            foreach (var p in controller.parameters)
                existing.Add(p.name);

            var addedNames = new List<string>();
            foreach (var param in paramsToAdd)
            {
                if (string.IsNullOrEmpty(param.paramName) || existing.Contains(param.paramName))
                    continue;

                Undo.RecordObject(controller, "Add parameter: " + param.paramName);
                var animParam = new AnimatorControllerParameter
                {
                    name = param.paramName,
                    type = param.paramType
                };
                switch (param.paramType)
                {
                    case AnimatorControllerParameterType.Float:
                        animParam.defaultFloat = param.defaultFloat;
                        break;
                    case AnimatorControllerParameterType.Int:
                        animParam.defaultInt = param.defaultInt;
                        break;
                    case AnimatorControllerParameterType.Bool:
                        animParam.defaultBool = param.defaultBool;
                        break;
                }
                controller.AddParameter(animParam);
                existing.Add(param.paramName);
                addedNames.Add(param.paramName);
            }

            if (addedNames.Count > 0)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);
                Debug.Log($"[ExtendedParameters] {controller.name}: {addedNames.Count} 個のパラメータを追加しました。");
            }
            else
            {
                EditorUtility.DisplayDialog("情報", "不足しているパラメーターはありません。", "OK");
            }
        }

        private static AnimatorController GetAnimatorController(Animator animator)
        {
            var runtimeController = animator.runtimeAnimatorController;
            if (runtimeController == null) return null;

            if (runtimeController is AnimatorOverrideController aoc)
                return aoc.runtimeAnimatorController as AnimatorController;

            return runtimeController as AnimatorController;
        }

        private struct ParamToAdd
        {
            public string paramName;
            public AnimatorControllerParameterType paramType;
            public float defaultFloat;
            public int defaultInt;
            public bool defaultBool;
        }

        private static List<ParamToAdd> CollectParamsFromPrefabs(ExtendedParameters extendedParameters)
        {
            var result = new List<ParamToAdd>();
            var seen = new HashSet<string>();

            if (extendedParameters.parameterPrefabs == null) return result;

            foreach (var prefab in extendedParameters.parameterPrefabs)
            {
                if (prefab == null) continue;

                var paramInfos = prefab.GetComponentsInChildren<ExtendedParamInfo>(true);
                foreach (var paramInfo in paramInfos)
                {
                    if (paramInfo.paramInfos == null) continue;

                    foreach (var param in paramInfo.paramInfos)
                    {
                        if (!string.IsNullOrEmpty(param.paramName) && !seen.Contains(param.paramName))
                        {
                            seen.Add(param.paramName);
                            result.Add(new ParamToAdd
                            {
                                paramName = param.paramName,
                                paramType = param.paramType,
                                defaultFloat = param.defaultFloat,
                                defaultInt = param.defaultInt,
                                defaultBool = param.defaultBool
                            });
                        }
                    }
                }
            }

            return result;
        }

        private static string GetDefaultValueString(ExtendedParamInfo.ParamInfo param)
        {
            switch (param.paramType)
            {
                case AnimatorControllerParameterType.Float: return param.defaultFloat.ToString("G3");
                case AnimatorControllerParameterType.Int: return param.defaultInt.ToString();
                case AnimatorControllerParameterType.Bool: return param.defaultBool.ToString();
                default: return "";
            }
        }

        private static string GetGameObjectPath(GameObject obj, Transform root)
        {
            var path = obj.name;
            var current = obj.transform.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private void DrawParameterPrefabsSelector()
        {
            var prefabsProp = serializedObject.FindProperty("parameterPrefabs");
            if (prefabsProp == null) return;

            var folderPath = AssetDatabase.GUIDToAssetPath(ParameterPrefabsFolderGUID);
            if (string.IsNullOrEmpty(folderPath))
            {
                DrawHelpBoxWithDefaultFont($"GUID {ParameterPrefabsFolderGUID} のフォルダが見つかりません。", MessageType.Warning);
                EditorGUILayout.PropertyField(prefabsProp, true);
                return;
            }

            var availablePrefabs = GetPrefabsInFolder(folderPath);
            if (availablePrefabs.Count == 0)
            {
                DrawHelpBoxWithDefaultFont($"フォルダ \"{folderPath}\" にプレファブがありません。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("パラメーター追加プレファブを追加"))
                {
                    ShowPrefabSelectionMenu(availablePrefabs);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(3);
            DrawWithDefaultFont(() => EditorGUILayout.PropertyField(prefabsProp, new GUIContent("選択中のプレファブ"), true));
        }

        private static List<GameObject> GetPrefabsInFolder(string folderPath)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            var result = new List<GameObject>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    result.Add(prefab);
            }
            return result.OrderBy(p => p.name).ToList();
        }

        private void ShowPrefabSelectionMenu(List<GameObject> availablePrefabs)
        {
            var menu = new GenericMenu();
            var extendedParameters = (ExtendedParameters)target;
            var currentPrefabs = new HashSet<GameObject>();
            if (extendedParameters.parameterPrefabs != null)
            {
                foreach (var p in extendedParameters.parameterPrefabs)
                    if (p != null) currentPrefabs.Add(p);
            }

            foreach (var prefab in availablePrefabs)
            {
                var p = prefab;
                var isSelected = currentPrefabs.Contains(p);
                var content = new GUIContent(p.name + (isSelected ? " ✓" : ""));
                menu.AddItem(content, isSelected, () =>
                {
                    var s = target as ExtendedParameters;
                    if (s == null) return;

                    Undo.RecordObject(s, "Parameter Prefabs Toggle");
                    var list = new List<GameObject>();
                    if (s.parameterPrefabs != null)
                    {
                        foreach (var existing in s.parameterPrefabs)
                            if (existing != null) list.Add(existing);
                    }

                    if (list.Contains(p))
                        list.Remove(p);
                    else
                        list.Add(p);

                    s.parameterPrefabs = list.ToArray();
                    EditorUtility.SetDirty(s);
                });
            }

            menu.ShowAsContext();
        }
    }
}
