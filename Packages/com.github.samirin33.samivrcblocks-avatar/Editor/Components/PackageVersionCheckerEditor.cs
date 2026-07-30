using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(PackageVersionChecker))]
    public class PackageVersionCheckerEditor : SamirinMABaseEditor
    {
        private SerializedProperty _requirements;
        private readonly Dictionary<int, bool> _foldouts = new Dictionary<int, bool>();

        private void OnEnable()
        {
            _requirements = serializedObject.FindProperty(nameof(PackageVersionChecker.requirements));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.LabelField("バージョン要件", EditorStyles.boldLabel);
                DrawRequirementsList();

                EditorGUILayout.Space(8);
                if (GUILayout.Button("今すぐチェック", GUILayout.Height(24)))
                    PackageVersionCheckerService.CheckAndWarn(
                        (PackageVersionChecker)target,
                        forceDialog: true,
                        showSatisfiedDialog: true);

                serializedObject.ApplyModifiedProperties();
            });
        }

        private void DrawRequirementsList()
        {
            if (_requirements == null)
                return;

            for (var i = 0; i < _requirements.arraySize; i++)
            {
                var element = _requirements.GetArrayElementAtIndex(i);
                var packageIdProp = element.FindPropertyRelative(nameof(PackageVersionChecker.Requirement.packageId));
                var minVersionProp = element.FindPropertyRelative(nameof(PackageVersionChecker.Requirement.minVersion));
                var displayNameProp = element.FindPropertyRelative(nameof(PackageVersionChecker.Requirement.displayName));

                var packageId = packageIdProp.stringValue;
                var installed = string.IsNullOrEmpty(packageId)
                    ? null
                    : PackageVersionCheckerService.GetInstalledVersion(packageId);
                var title = string.IsNullOrEmpty(displayNameProp.stringValue)
                    ? (string.IsNullOrEmpty(packageId) ? $"要件 {i + 1}" : packageId)
                    : displayNameProp.stringValue;

                var ok = !string.IsNullOrEmpty(installed)
                         && !string.IsNullOrEmpty(minVersionProp.stringValue)
                         && !PackageVersionCheckerService.IsVersionLower(installed, minVersionProp.stringValue);
                var status = string.IsNullOrEmpty(packageId)
                    ? ""
                    : string.IsNullOrEmpty(installed)
                        ? "未インストール"
                        : ok
                            ? $"OK ({installed})"
                            : $"不足 (現在 {installed})";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                if (!_foldouts.TryGetValue(i, out var open))
                    open = true;
                _foldouts[i] = EditorGUILayout.Foldout(open, $"{title}  {status}", true);

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(i <= 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(24)))
                    {
                        _requirements.MoveArrayElement(i, i - 1);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUI.DisabledScope(i >= _requirements.arraySize - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(24)))
                    {
                        _requirements.MoveArrayElement(i, i + 1);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("×", GUILayout.Width(24)))
                {
                    _requirements.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                if (_foldouts.TryGetValue(i, out var isOpen) && isOpen)
                {
                    DrawPackageIdField(packageIdProp, displayNameProp);
                    DrawMinVersionField(packageIdProp.stringValue, minVersionProp, installed);
                    EditorGUILayout.PropertyField(displayNameProp, new GUIContent("表示名（任意）"));
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (GUILayout.Button("+ 要件を追加"))
                AddRequirement("", "");
        }

        private void DrawPackageIdField(SerializedProperty packageIdProp, SerializedProperty displayNameProp)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(packageIdProp, new GUIContent("パッケージ ID"));
            if (EditorGUI.EndChangeCheck() && string.IsNullOrEmpty(displayNameProp.stringValue))
            {
                var presetName = PackageVersionCheckerService.GetPresetDisplayName(packageIdProp.stringValue);
                if (!string.IsNullOrEmpty(presetName))
                    displayNameProp.stringValue = presetName;
            }

            if (GUILayout.Button("選択", GUILayout.Width(48)))
            {
                ShowPackagePickerForProperty(packageIdProp, displayNameProp);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMinVersionField(string packageId, SerializedProperty minVersionProp, string installed)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(minVersionProp, new GUIContent("最低バージョン"));

            if (GUILayout.Button("選択", GUILayout.Width(48)))
            {
                ShowVersionPicker(packageId, minVersionProp, installed);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(installed)))
            {
                if (GUILayout.Button($"現在のバージョンを使う ({(installed ?? "—")})", EditorStyles.miniButton))
                {
                    minVersionProp.stringValue = installed;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ShowPackagePickerForProperty(SerializedProperty packageIdProp, SerializedProperty displayNameProp)
        {
            var choices = PackageVersionCheckerService.GetInstalledPackageChoices(forceRefresh: true);
            var menu = new GenericMenu();

            foreach (var preset in PackageVersionCheckerService.CommonPackagePresets)
            {
                var packageId = preset.PackageId;
                var displayName = preset.DisplayName;
                menu.AddItem(
                    new GUIContent($"よく使うパッケージ/{displayName}"),
                    packageIdProp.stringValue == packageId,
                    () => ApplyPackageChoice(packageIdProp, displayNameProp, packageId, displayName));
            }

            menu.AddSeparator("");
            foreach (var choice in choices)
            {
                var c = choice;
                var ver = string.IsNullOrEmpty(c.InstalledVersion) ? "未インストール" : c.InstalledVersion;
                menu.AddItem(
                    new GUIContent($"インストール済み/{c.DisplayName} ({ver})"),
                    packageIdProp.stringValue == c.PackageId,
                    () => ApplyPackageChoice(packageIdProp, displayNameProp, c.PackageId, c.DisplayName));
            }

            menu.ShowAsContext();
        }

        private void ShowVersionPicker(string packageId, SerializedProperty minVersionProp, string installed)
        {
            var menu = new GenericMenu();
            if (string.IsNullOrEmpty(packageId))
            {
                menu.AddDisabledItem(new GUIContent("先にパッケージ ID を指定してください"));
                menu.ShowAsContext();
                return;
            }

            if (!string.IsNullOrEmpty(installed))
            {
                menu.AddItem(
                    new GUIContent($"現在のプロジェクト ({installed})"),
                    minVersionProp.stringValue == installed,
                    () =>
                    {
                        minVersionProp.stringValue = installed;
                        serializedObject.ApplyModifiedProperties();
                    });
                menu.AddSeparator("");
            }

            var versions = PackageVersionCheckerService.GetVersionChoices(packageId, forceRefresh: true);
            if (versions.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("候補がありません（手入力してください）"));
            }
            else
            {
                foreach (var version in versions.Take(40))
                {
                    var v = version;
                    var mark = v == installed ? " ← インストール済み" : "";
                    menu.AddItem(
                        new GUIContent(v + mark),
                        minVersionProp.stringValue == v,
                        () =>
                        {
                            minVersionProp.stringValue = v;
                            serializedObject.ApplyModifiedProperties();
                        });
                }
            }

            menu.ShowAsContext();
        }

        private void ApplyPackageChoice(
            SerializedProperty packageIdProp,
            SerializedProperty displayNameProp,
            string packageId,
            string displayName)
        {
            packageIdProp.stringValue = packageId;
            if (string.IsNullOrEmpty(displayNameProp.stringValue))
                displayNameProp.stringValue = displayName;

            for (var i = 0; i < _requirements.arraySize; i++)
            {
                var el = _requirements.GetArrayElementAtIndex(i);
                var idProp = el.FindPropertyRelative(nameof(PackageVersionChecker.Requirement.packageId));
                if (idProp.propertyPath != packageIdProp.propertyPath)
                    continue;

                var minProp = el.FindPropertyRelative(nameof(PackageVersionChecker.Requirement.minVersion));
                if (string.IsNullOrEmpty(minProp.stringValue))
                {
                    var installed = PackageVersionCheckerService.GetInstalledVersion(packageId);
                    if (!string.IsNullOrEmpty(installed))
                        minProp.stringValue = installed;
                }

                break;
            }

            serializedObject.ApplyModifiedProperties();
            Repaint();
        }

        private void AddRequirement(string packageId, string displayName, string minVersion = null)
        {
            serializedObject.Update();
            var index = _requirements.arraySize;
            _requirements.arraySize++;
            var element = _requirements.GetArrayElementAtIndex(index);
            element.FindPropertyRelative(nameof(PackageVersionChecker.Requirement.packageId)).stringValue = packageId ?? "";
            element.FindPropertyRelative(nameof(PackageVersionChecker.Requirement.displayName)).stringValue = displayName ?? "";

            if (string.IsNullOrEmpty(minVersion) && !string.IsNullOrEmpty(packageId))
                minVersion = PackageVersionCheckerService.GetInstalledVersion(packageId);

            element.FindPropertyRelative(nameof(PackageVersionChecker.Requirement.minVersion)).stringValue = minVersion ?? "";
            _foldouts[index] = true;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
