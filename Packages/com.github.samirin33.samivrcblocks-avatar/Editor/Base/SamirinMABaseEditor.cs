using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.ndmf.runtime;
using Samirin33.Editor;
using Samirin33.NDMF.Module;

namespace Samirin33.NDMF.Base.Editor
{
    [CustomEditor(typeof(SamirinMABase), true)]
    [CanEditMultipleObjects]
    public class SamirinMABaseEditor : UnityEditor.Editor
    {
        protected enum ArrayRowAction
        {
            None,
            MoveUp,
            MoveDown,
            Remove,
        }

        private static readonly GUIContent DefaultSmoothWeightLabel = new GUIContent("スムージング重み(高いほどゆっくり)");
        private static readonly GUIContent SmoothWeightRawFieldLabel = new GUIContent(
            string.Empty,
            "1 以上を許可: 1 を超える値（よりゆっくり）はこちらに直接入力します。");

        private static readonly GUIContent MoveUpCompactContent = new GUIContent("↑", "1つ上へ移動");
        private static readonly GUIContent MoveDownCompactContent = new GUIContent("↓", "1つ下へ移動");
        private static readonly GUIContent MoveUpContent = new GUIContent("上へ");
        private static readonly GUIContent MoveDownContent = new GUIContent("下へ");
        private static readonly GUIContent RemoveContent = new GUIContent("削除");

        private const string MultiEditArrayNote = "複数選択中は項目の追加・削除・並べ替えはできません。1つずつ選択して編集してください。";

        public override void OnInspectorGUI()
        {
            SamirinEditorStyleHelper.DrawWithBlueBackgroundForInspectorOrSettings(() => DrawDefaultInspector());
        }

        protected void DrawWithBlueBackground(Action drawContent)
        {
            SamirinEditorStyleHelper.DrawWithBlueBackgroundForInspectorOrSettings(drawContent);
        }

        protected static void DrawHelpBoxWithDefaultFont(string message, MessageType type)
        {
            SamirinEditorStyleHelper.DrawHelpBoxWithDefaultFont(message, type);
        }

        protected void DrawWithDefaultFont(Action drawAction)
        {
            SamirinEditorStyleHelper.DrawWithDefaultFont(drawAction);
        }

        protected static void DrawSmoothWeightField(
            SerializedProperty smoothWeightProp,
            GUIContent label = null)
        {
            var value = smoothWeightProp.floatValue;
            if (DrawSmoothWeightValue(label, ref value, smoothWeightProp.hasMultipleDifferentValues))
                smoothWeightProp.floatValue = value;
        }

        /// <returns>値が編集されたとき true。</returns>
        protected static bool DrawSmoothWeightValue(GUIContent label, ref float value, bool mixed = false)
        {
            label ??= DefaultSmoothWeightLabel;
            var changed = false;

            EditorGUILayout.BeginHorizontal();
            EditorGUI.showMixedValue = mixed;

            // スライダーは常に 0〜1 固定にし、1 を超える値は右の欄だけで扱う（ドラッグ中に UI が切り替わらないように）
            EditorGUI.BeginChangeCheck();
            var sliderValue = EditorGUILayout.Slider(label, Mathf.Clamp01(value), 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                value = sliderValue;
                changed = true;
            }

            var previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var rawRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(56f));
            GUI.Label(rawRect, SmoothWeightRawFieldLabel);
            EditorGUI.BeginChangeCheck();
            var rawValue = EditorGUI.DelayedFloatField(rawRect, value);
            if (EditorGUI.EndChangeCheck())
            {
                value = Mathf.Max(0f, rawValue);
                changed = true;
            }
            EditorGUI.indentLevel = previousIndent;

            EditorGUI.showMixedValue = false;
            EditorGUILayout.EndHorizontal();
            return changed;
        }

        /// <summary>
        /// 複数選択中は配列の追加・削除・並べ替えを禁止し、その旨を表示する。
        /// 複数選択時の arraySize は最小値で、書き込むと全ターゲットが切り詰められるため。
        /// </summary>
        /// <returns>配列の構造を編集してよいとき true。</returns>
        protected static bool CanEditArrayStructure(SerializedObject serializedObject, bool drawNote = true)
        {
            if (serializedObject == null || !serializedObject.isEditingMultipleObjects)
                return true;

            if (drawNote)
                DrawHelpBoxWithDefaultFont(MultiEditArrayNote, MessageType.Info);
            return false;
        }

        /// <summary>
        /// ↑ / ↓ / 削除 ボタンを横並びで描画する。呼び出し側の BeginHorizontal 内で使う。
        /// 押されたボタンは戻り値で返すだけなので、配列の変更はループの外で <see cref="ApplyArrayRowAction"/> を呼ぶ。
        /// </summary>
        protected static ArrayRowAction DrawArrayRowButtons(int index, int count, bool enabled = true, bool compact = true)
        {
            var action = ArrayRowAction.None;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (compact
                            ? GUILayout.Button(MoveUpCompactContent, GUILayout.Width(24f))
                            : GUILayout.Button(MoveUpContent))
                        action = ArrayRowAction.MoveUp;
                }

                using (new EditorGUI.DisabledScope(index >= count - 1))
                {
                    if (compact
                            ? GUILayout.Button(MoveDownCompactContent, GUILayout.Width(24f))
                            : GUILayout.Button(MoveDownContent))
                        action = ArrayRowAction.MoveDown;
                }

                if (compact
                        ? GUILayout.Button(RemoveContent, GUILayout.Width(50f))
                        : GUILayout.Button(RemoveContent))
                    action = ArrayRowAction.Remove;
            }

            return action;
        }

        /// <returns>配列を変更したとき true。</returns>
        protected static bool ApplyArrayRowAction(SerializedProperty array, int index, ArrayRowAction action)
        {
            if (array == null || index < 0 || index >= array.arraySize)
                return false;

            switch (action)
            {
                case ArrayRowAction.MoveUp:
                    if (index <= 0)
                        return false;
                    array.MoveArrayElement(index, index - 1);
                    break;
                case ArrayRowAction.MoveDown:
                    if (index >= array.arraySize - 1)
                        return false;
                    array.MoveArrayElement(index, index + 1);
                    break;
                case ArrayRowAction.Remove:
                    array.DeleteArrayElementAtIndex(index);
                    break;
                default:
                    return false;
            }

            GUI.FocusControl(null);
            return true;
        }

        protected static void DrawWriteDefaultsSettings(SerializedProperty matchAvatarWriteDefaults, SerializedProperty writeDefault)
        {
            EditorGUILayout.PropertyField(matchAvatarWriteDefaults, new GUIContent(
                "Write Defaultをアバターに合わせる",
                "オンのとき、アバターの既存レイヤーの Write Defaults 設定に合わせます。"));
            var match = matchAvatarWriteDefaults != null
                && matchAvatarWriteDefaults.boolValue
                && !matchAvatarWriteDefaults.hasMultipleDifferentValues;
            using (new EditorGUI.DisabledScope(match))
            {
                EditorGUILayout.PropertyField(writeDefault, new GUIContent("生成されるステートのWrite Default"));
            }
        }

        protected static string FormatNumber(float value)
        {
            var rounded = Mathf.Round(value);
            if (Mathf.Approximately(value, rounded))
                return rounded.ToString("0");

            var text = value.ToString("0.######");
            return text.TrimEnd('0').TrimEnd('.');
        }

        protected static GameObject FindAvatarRootObject(Component component)
        {
            if (component == null)
                return null;

            var root = RuntimeUtil.FindAvatarInParents(component.transform);
            return root != null ? root.gameObject : null;
        }

        protected static void SelectGeneratedAnimators(IEnumerable<AnimatorController> controllers)
        {
            var objects = controllers?.Where(c => c != null).Cast<UnityEngine.Object>().ToArray();
            if (objects == null || objects.Length == 0)
                return;

            Selection.objects = objects;
            EditorGUIUtility.PingObject(objects[0]);
        }

        /// <summary>
        /// 選択中のコンポーネントをアバターごとにまとめ、アバター内の同型コンポーネント全体で Animator を手動生成する。
        /// </summary>
        /// <returns>生成された AnimatorController（null を除く）。対象アバターが無く中断したときは null。</returns>
        protected static List<AnimatorController> ManualGenerateAnimatorsPerAvatar<T>(
            IEnumerable<UnityEngine.Object> selectedTargets,
            string dialogTitle,
            string undoName,
            Func<GameObject, T[], AnimatorController[]> build)
            where T : Component
        {
            var generated = new List<AnimatorController>();
            var selected = selectedTargets.OfType<T>().Where(c => c != null).ToArray();
            if (selected.Length == 0)
                return null;

            var avatarRoots = new List<GameObject>();
            var missingAvatar = false;
            foreach (var component in selected)
            {
                var root = FindAvatarRootObject(component);
                if (root == null)
                {
                    missingAvatar = true;
                    continue;
                }

                if (!avatarRoots.Contains(root))
                    avatarRoots.Add(root);
            }

            if (missingAvatar)
            {
                EditorUtility.DisplayDialog(
                    dialogTitle,
                    avatarRoots.Count == 0
                        ? "親階層に VRCAvatarDescriptor が見つかりません。アバター配下に配置してから実行してください。"
                        : "一部のコンポーネントは親階層に VRCAvatarDescriptor が無いためスキップします。",
                    "OK");
                if (avatarRoots.Count == 0)
                    return null;
            }

            foreach (var avatarRoot in avatarRoots)
            {
                var all = avatarRoot.GetComponentsInChildren<T>(true);
                Undo.RegisterCompleteObjectUndo(all.Cast<UnityEngine.Object>().ToArray(), undoName);
                var controllers = build(avatarRoot, all);
                if (controllers != null)
                    generated.AddRange(controllers.Where(c => c != null));
            }

            AssetDatabase.SaveAssets();
            SelectGeneratedAnimators(generated);
            return generated;
        }

        protected static bool GetDeveloperSettingsPref(string prefsKey)
        {
            return EditorPrefs.GetBool(prefsKey, false);
        }

        protected static void SetDeveloperSettingsPref(string prefsKey, bool value)
        {
            EditorPrefs.SetBool(prefsKey, value);
        }

        protected static void ToggleDeveloperSettingsPref(string prefsKey)
        {
            SetDeveloperSettingsPref(prefsKey, !GetDeveloperSettingsPref(prefsKey));
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        protected static bool ValidateDeveloperSettingsMenu(string menuPath, string prefsKey)
        {
            Menu.SetChecked(menuPath, GetDeveloperSettingsPref(prefsKey));
            return true;
        }
    }
}
