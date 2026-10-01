using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(ObserveParameterValueChange))]
    [CanEditMultipleObjects]
    public class ObserveParameterValueChangeEditor : SamirinMABaseEditor
    {
        private SerializedProperty _observeSettings;
        private SerializedProperty _writeDefault;
        private SerializedProperty _matchAvatarWriteDefaults;

        private void OnEnable()
        {
            _observeSettings = serializedObject.FindProperty("observeSettings");
            _writeDefault = serializedObject.FindProperty("writeDefault");
            _matchAvatarWriteDefaults = serializedObject.FindProperty("matchAvatarWriteDefaults");
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    "指定したパラメーターが閾値の間隔をまたいで変化したとき、(パラメーター名)_ValueChanged トリガーが有効になります。読み込み直後の値では発行しません。",
                    MessageType.Info);

                EditorGUILayout.LabelField("監視パラメータ設定");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (_observeSettings != null)
                {
                    for (int i = 0; i < _observeSettings.arraySize; i++)
                    {
                        var element = _observeSettings.GetArrayElementAtIndex(i);
                        if (!DrawSetting(element, i))
                            break;
                        EditorGUILayout.Space(3);
                    }

                    if (GUILayout.Button("+ 追加"))
                        AddSetting();
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_matchAvatarWriteDefaults, new GUIContent("Write Defaultをアバターに合わせる"));
                var matchAvatarWriteDefaults = _matchAvatarWriteDefaults != null && _matchAvatarWriteDefaults.boolValue;
                EditorGUI.BeginDisabledGroup(matchAvatarWriteDefaults);
                EditorGUILayout.PropertyField(_writeDefault, new GUIContent("生成されるステートのWrite Default"));
                EditorGUI.EndDisabledGroup();
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

        /// <returns>削除や並べ替えでループを抜けるときは false。</returns>
        private bool DrawSetting(SerializedProperty element, int index)
        {
            var paramNameProp = element.FindPropertyRelative("paramName");
            var paramTypeProp = element.FindPropertyRelative("paramType");
            var thresholdProp = element.FindPropertyRelative("threshold");
            var floatRangePresetProp = element.FindPropertyRelative("floatRangePreset");
            var customFloatMinProp = element.FindPropertyRelative("customFloatMin");
            var customFloatMaxProp = element.FindPropertyRelative("customFloatMax");
            var intMinProp = element.FindPropertyRelative("intMin");
            var intMaxProp = element.FindPropertyRelative("intMax");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(paramNameProp.stringValue);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(index == 0);
            if (GUILayout.Button("↑", GUILayout.Width(24)))
            {
                _observeSettings.MoveArrayElement(index, index - 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return false;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(index == _observeSettings.arraySize - 1);
            if (GUILayout.Button("↓", GUILayout.Width(24)))
            {
                _observeSettings.MoveArrayElement(index, index + 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return false;
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("削除", GUILayout.Width(50)))
            {
                _observeSettings.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(paramNameProp, new GUIContent("パラメータ名"));
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(paramTypeProp, new GUIContent("タイプ"));
            var paramTypeChanged = EditorGUI.EndChangeCheck();

            var paramType = (ObserveParameterValueChange.ParamType)paramTypeProp.enumValueIndex;
            if (paramTypeChanged && paramType == ObserveParameterValueChange.ParamType.Int && thresholdProp.floatValue < 1f)
                thresholdProp.floatValue = 1f;
            if (paramTypeChanged && paramType == ObserveParameterValueChange.ParamType.Float && thresholdProp.floatValue < ObserveParameterValueChange.MinFloatThreshold)
                thresholdProp.floatValue = 0.1f;

            if (paramType == ObserveParameterValueChange.ParamType.Bool)
            {
                EditorGUILayout.HelpBox("オン / オフが切り替わったときにトリガーが有効になります。", MessageType.None);
            }
            else if (paramType == ObserveParameterValueChange.ParamType.Int)
            {
                var intThreshold = Mathf.Max(1, Mathf.RoundToInt(thresholdProp.floatValue));
                EditorGUI.BeginChangeCheck();
                var editedIntThreshold = EditorGUILayout.IntField("変化間隔", intThreshold);
                if (EditorGUI.EndChangeCheck())
                    thresholdProp.floatValue = Mathf.Max(1, editedIntThreshold);
                else if (!Mathf.Approximately(thresholdProp.floatValue, intThreshold))
                    thresholdProp.floatValue = intThreshold;
                EditorGUILayout.PropertyField(intMinProp, new GUIContent("最小値"));
                EditorGUILayout.PropertyField(intMaxProp, new GUIContent("最大値"));
                if (intMinProp.intValue > intMaxProp.intValue)
                    EditorGUILayout.HelpBox("最大値は最小値以上にしてください。", MessageType.Warning);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var editedThreshold = EditorGUILayout.FloatField("変化間隔", thresholdProp.floatValue);
                if (EditorGUI.EndChangeCheck())
                    thresholdProp.floatValue = Mathf.Max(ObserveParameterValueChange.MinFloatThreshold, editedThreshold);
                else if (thresholdProp.floatValue < ObserveParameterValueChange.MinFloatThreshold)
                    thresholdProp.floatValue = ObserveParameterValueChange.MinFloatThreshold;
                EditorGUILayout.PropertyField(floatRangePresetProp, new GUIContent("Float範囲"));
                if ((ObserveParameterValueChange.FloatRangePreset)floatRangePresetProp.enumValueIndex == ObserveParameterValueChange.FloatRangePreset.Custom)
                {
                    EditorGUILayout.PropertyField(customFloatMinProp, new GUIContent("最小値"));
                    EditorGUILayout.PropertyField(customFloatMaxProp, new GUIContent("最大値"));
                    if (customFloatMinProp.floatValue >= customFloatMaxProp.floatValue)
                        EditorGUILayout.HelpBox("最大値は最小値より大きくしてください。", MessageType.Warning);
                }
            }

            DrawBucketSummary(element);

            EditorGUILayout.EndVertical();
            return true;
        }

        private static void DrawBucketSummary(SerializedProperty element)
        {
            var setting = ReadSetting(element);
            var triggerName = ObserveParameterValueChange.GetValueChangedTriggerName(setting);
            if (!ObserveParameterValueChange.TryGetBucketInfo(setting, out var info))
                return;

            if (setting.paramType != ObserveParameterValueChange.ParamType.Bool)
            {
                if (info.bucketCount < 2)
                {
                    EditorGUILayout.HelpBox("この範囲と間隔では段が1つのため、トリガーは発行されません。", MessageType.Warning);
                }
                else
                {
                    var message = $"{info.rangeMin}～{info.rangeMax} を間隔 {FormatNumber(info.effectiveThreshold)} の {info.bucketCount} 段で監視します。";
                    EditorGUILayout.HelpBox(message, info.thresholdClamped ? MessageType.Warning : MessageType.None);
                    if (info.thresholdClamped)
                    {
                        EditorGUILayout.HelpBox(
                            $"段数が {ObserveParameterValueChange.MaxBucketCount} を超えるため、間隔を広げて生成します。",
                            MessageType.Warning);
                    }
                }
            }

            if (setting.paramType == ObserveParameterValueChange.ParamType.Bool || info.bucketCount >= 2)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(triggerName, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("コピー", GUILayout.Width(50)))
                    EditorGUIUtility.systemCopyBuffer = triggerName;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void AddSetting()
        {
            _observeSettings.arraySize++;
            var element = _observeSettings.GetArrayElementAtIndex(_observeSettings.arraySize - 1);
            element.FindPropertyRelative("paramName").stringValue = "";
            element.FindPropertyRelative("paramType").enumValueIndex = (int)ObserveParameterValueChange.ParamType.Float;
            element.FindPropertyRelative("threshold").floatValue = 0.1f;
            element.FindPropertyRelative("floatRangePreset").enumValueIndex = (int)ObserveParameterValueChange.FloatRangePreset.ZeroToPlusOne;
            element.FindPropertyRelative("customFloatMin").floatValue = 0f;
            element.FindPropertyRelative("customFloatMax").floatValue = 1f;
            element.FindPropertyRelative("intMin").intValue = 0;
            element.FindPropertyRelative("intMax").intValue = 1;
        }

        private static ObserveParameterValueChange.ObserveSetting ReadSetting(SerializedProperty element)
        {
            return new ObserveParameterValueChange.ObserveSetting
            {
                paramName = element.FindPropertyRelative("paramName").stringValue,
                paramType = (ObserveParameterValueChange.ParamType)element.FindPropertyRelative("paramType").enumValueIndex,
                threshold = element.FindPropertyRelative("threshold").floatValue,
                floatRangePreset = (ObserveParameterValueChange.FloatRangePreset)element.FindPropertyRelative("floatRangePreset").enumValueIndex,
                customFloatMin = element.FindPropertyRelative("customFloatMin").floatValue,
                customFloatMax = element.FindPropertyRelative("customFloatMax").floatValue,
                intMin = element.FindPropertyRelative("intMin").intValue,
                intMax = element.FindPropertyRelative("intMax").intValue,
            };
        }

        private static string FormatNumber(float value)
        {
            var rounded = Mathf.Round(value);
            if (Mathf.Approximately(value, rounded))
                return rounded.ToString("0");

            var text = value.ToString("0.######");
            return text.TrimEnd('0').TrimEnd('.');
        }

        private void ManualGenerateAnimator()
        {
            var selected = targets.OfType<ObserveParameterValueChange>().Where(c => c != null).ToArray();
            if (selected.Length == 0)
                return;

            var avatarRoot = FindAvatarRoot(selected[0].transform);
            if (avatarRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "ObserveParameterValueChange",
                    "親階層に VRCAvatarDescriptor が見つかりません。アバター配下に配置してから実行してください。",
                    "OK");
                return;
            }

            var all = avatarRoot.GetComponentsInChildren<ObserveParameterValueChange>(true);
            Undo.RegisterCompleteObjectUndo(all.Cast<Object>().ToArray(), "Manual Generate ObserveParameterValueChange Animator");

            var controllers = ObserveParameterValueChangeBuilder.BuildManual(avatarRoot, all);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (controllers == null || controllers.Length == 0 || controllers[0] == null)
            {
                EditorUtility.DisplayDialog(
                    "ObserveParameterValueChange",
                    "有効な監視設定がないため Animator は生成されませんでした。",
                    "OK");
                return;
            }

            Selection.objects = controllers.Where(c => c != null).Cast<Object>().ToArray();
            EditorGUIUtility.PingObject(controllers[0]);
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
