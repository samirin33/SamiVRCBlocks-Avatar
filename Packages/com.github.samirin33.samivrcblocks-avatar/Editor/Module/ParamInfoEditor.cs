using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Samirin33.NDMF.Module;

namespace Samirin33.NDMF.Module.Editor
{
    [CustomPropertyDrawer(typeof(ExtendedParamInfo.ParamInfo))]
    public class ParamInfoEditor : PropertyDrawer
    {
        private const float RowSpacing = 2f;
        private const int ExplanationLines = 2;
        private const float SyncButtonWidth = 48f;

        private static readonly GUIContent ParamNameContent = new GUIContent("パラメーター名", "Animator のパラメーターから選ぶか、名前を直接入力します。");
        private static readonly GUIContent ParamTypeContent = new GUIContent("型", "Animator に同名のパラメーターがあるときは自動で設定されます。");
        private static readonly GUIContent ParamTypeMismatchContent = new GUIContent("型（Animator と未同期）", "Animator 側の型または初期値と異なります。「同期」で Animator の型と初期値を取り込みます。");
        private static readonly GUIContent ParamExplanationContent = new GUIContent("説明", "ExtendedParameters のインスペクターに表示される説明文です。");
        private static readonly GUIContent SyncContent = new GUIContent("同期", "Animator の型と初期値を取り込みます。");
        private const string UnselectedOption = "(未選択)";

        private static readonly AnimatorControllerParameterType[] ParameterTypes =
            (AnimatorControllerParameterType[])Enum.GetValues(typeof(AnimatorControllerParameterType));

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var paramNameProp = property.FindPropertyRelative(nameof(ExtendedParamInfo.ParamInfo.paramName));
            var paramTypeProp = property.FindPropertyRelative(nameof(ExtendedParamInfo.ParamInfo.paramType));
            var paramExplanationProp = property.FindPropertyRelative(nameof(ExtendedParamInfo.ParamInfo.paramExplanation));
            var defaultFloatProp = property.FindPropertyRelative(nameof(ExtendedParamInfo.ParamInfo.defaultFloat));
            var defaultIntProp = property.FindPropertyRelative(nameof(ExtendedParamInfo.ParamInfo.defaultInt));
            var defaultBoolProp = property.FindPropertyRelative(nameof(ExtendedParamInfo.ParamInfo.defaultBool));

            if (paramNameProp == null || paramTypeProp == null || paramExplanationProp == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                EditorGUI.EndProperty();
                return;
            }

            var parameters = GetControllerParameters(property);
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var rect = new Rect(position.x, position.y, position.width, lineHeight);

            DrawParamName(rect, parameters, paramNameProp, paramTypeProp, defaultFloatProp, defaultIntProp, defaultBoolProp);
            rect.y += lineHeight + RowSpacing;

            var matched = paramNameProp.hasMultipleDifferentValues ? null : FindParameter(parameters, paramNameProp.stringValue);
            DrawParamType(rect, matched, paramTypeProp, defaultFloatProp, defaultIntProp, defaultBoolProp);
            rect.y += lineHeight + RowSpacing;

            EditorGUI.LabelField(rect, ParamExplanationContent);
            rect.y += lineHeight + RowSpacing;

            var explanationRect = new Rect(position.x, rect.y, position.width, lineHeight * ExplanationLines);
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = paramExplanationProp.hasMultipleDifferentValues;
            var newExplanation = EditorGUI.TextArea(explanationRect, paramExplanationProp.stringValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                paramExplanationProp.stringValue = newExplanation;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            return (lineHeight + RowSpacing) * 3f + lineHeight * ExplanationLines + RowSpacing;
        }

        private static void DrawParamName(
            Rect rect,
            AnimatorControllerParameter[] parameters,
            SerializedProperty paramNameProp,
            SerializedProperty paramTypeProp,
            SerializedProperty defaultFloatProp,
            SerializedProperty defaultIntProp,
            SerializedProperty defaultBoolProp)
        {
            var current = paramNameProp.stringValue ?? string.Empty;
            var existsInAnimator = FindParameter(parameters, current) != null;
            var useDropdown = parameters != null && parameters.Length > 0
                && !paramNameProp.hasMultipleDifferentValues
                && (string.IsNullOrEmpty(current) || existsInAnimator);

            string newValue;
            EditorGUI.BeginChangeCheck();
            if (useDropdown)
            {
                var options = new string[parameters.Length + 1];
                options[0] = UnselectedOption;
                var selected = 0;
                for (var i = 0; i < parameters.Length; i++)
                {
                    options[i + 1] = parameters[i].name;
                    if (parameters[i].name == current)
                        selected = i + 1;
                }

                var index = EditorGUI.Popup(rect, ParamNameContent.text, selected, options);
                newValue = index > 0 ? options[index] : string.Empty;
            }
            else
            {
                EditorGUI.showMixedValue = paramNameProp.hasMultipleDifferentValues;
                newValue = EditorGUI.DelayedTextField(rect, ParamNameContent, current);
                EditorGUI.showMixedValue = false;
            }

            if (!EditorGUI.EndChangeCheck())
                return;

            paramNameProp.stringValue = newValue;
            var matched = FindParameter(parameters, newValue);
            if (matched != null)
                SyncFromAnimatorParam(matched, paramTypeProp, defaultFloatProp, defaultIntProp, defaultBoolProp);
        }

        private static void DrawParamType(
            Rect rect,
            AnimatorControllerParameter matched,
            SerializedProperty paramTypeProp,
            SerializedProperty defaultFloatProp,
            SerializedProperty defaultIntProp,
            SerializedProperty defaultBoolProp)
        {
            if (matched == null)
            {
                EditorGUI.PropertyField(rect, paramTypeProp, ParamTypeContent);
                return;
            }

            var mismatch = paramTypeProp.hasMultipleDifferentValues
                || paramTypeProp.enumValueIndex != Array.IndexOf(ParameterTypes, matched.type)
                || !DefaultMatches(matched, defaultFloatProp, defaultIntProp, defaultBoolProp);
            var fieldRect = rect;
            if (mismatch)
            {
                fieldRect.width -= SyncButtonWidth + 4f;
                var buttonRect = new Rect(fieldRect.xMax + 4f, rect.y, SyncButtonWidth, rect.height);
                if (GUI.Button(buttonRect, SyncContent, EditorStyles.miniButton))
                    SyncFromAnimatorParam(matched, paramTypeProp, defaultFloatProp, defaultIntProp, defaultBoolProp);
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(fieldRect, paramTypeProp, mismatch ? ParamTypeMismatchContent : ParamTypeContent);
        }

        private static AnimatorControllerParameter[] GetControllerParameters(SerializedProperty property)
        {
            if (!(property.serializedObject.targetObject is ExtendedParamInfo info))
                return null;

            var animator = info.animator != null ? info.animator : info.GetComponent<Animator>();
            if (animator == null)
                return null;

            // Animator.parameters はエディットモードでは空になることがあるため、Controller から直接読む
            var runtimeController = animator.runtimeAnimatorController;
            if (runtimeController is AnimatorOverrideController overrideController)
                runtimeController = overrideController.runtimeAnimatorController;

            return runtimeController is AnimatorController controller ? controller.parameters : null;
        }

        private static AnimatorControllerParameter FindParameter(AnimatorControllerParameter[] parameters, string name)
        {
            if (parameters == null || string.IsNullOrEmpty(name))
                return null;

            foreach (var parameter in parameters)
            {
                if (parameter.name == name)
                    return parameter;
            }

            return null;
        }

        private static bool DefaultMatches(
            AnimatorControllerParameter param,
            SerializedProperty defaultFloatProp,
            SerializedProperty defaultIntProp,
            SerializedProperty defaultBoolProp)
        {
            if (defaultFloatProp == null || defaultIntProp == null || defaultBoolProp == null) return true;

            switch (param.type)
            {
                case AnimatorControllerParameterType.Float:
                    return !defaultFloatProp.hasMultipleDifferentValues
                        && Mathf.Approximately(defaultFloatProp.floatValue, param.defaultFloat);
                case AnimatorControllerParameterType.Int:
                    return !defaultIntProp.hasMultipleDifferentValues && defaultIntProp.intValue == param.defaultInt;
                case AnimatorControllerParameterType.Bool:
                    return !defaultBoolProp.hasMultipleDifferentValues && defaultBoolProp.boolValue == param.defaultBool;
                default:
                    return true;
            }
        }

        private static void SyncFromAnimatorParam(
            AnimatorControllerParameter param,
            SerializedProperty paramTypeProp,
            SerializedProperty defaultFloatProp,
            SerializedProperty defaultIntProp,
            SerializedProperty defaultBoolProp)
        {
            var typeIndex = Array.IndexOf(ParameterTypes, param.type);
            if (typeIndex >= 0)
                paramTypeProp.enumValueIndex = typeIndex;

            if (defaultFloatProp == null || defaultIntProp == null || defaultBoolProp == null) return;

            switch (param.type)
            {
                case AnimatorControllerParameterType.Float:
                    defaultFloatProp.floatValue = param.defaultFloat;
                    break;
                case AnimatorControllerParameterType.Int:
                    defaultIntProp.intValue = param.defaultInt;
                    break;
                case AnimatorControllerParameterType.Bool:
                    defaultBoolProp.boolValue = param.defaultBool;
                    break;
            }
        }
    }
}
