using UnityEngine;
using UnityEditor;
using Samirin33.Editor;
using Samirin33.NDMF.Module;

namespace Samirin33.NDMF.Module.Editor
{
    [CustomPropertyDrawer(typeof(ModuleParamInfo.ParamInfo))]
    public class ParamInfoEditor : PropertyDrawer
    {
        public override bool CanCacheInspectorGUI(SerializedProperty property)
        {
            return false;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var paramNameProp = property.FindPropertyRelative("paramName");
            var paramTypeProp = property.FindPropertyRelative("paramType");
            var paramExplanationProp = property.FindPropertyRelative("paramExplanation");
            var defaultFloatProp = property.FindPropertyRelative("defaultFloat");
            var defaultIntProp = property.FindPropertyRelative("defaultInt");
            var defaultBoolProp = property.FindPropertyRelative("defaultBool");

            if (paramNameProp == null || paramTypeProp == null || paramExplanationProp == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                EditorGUI.EndProperty();
                return;
            }

            // ModuleParamInfoからAnimatorを取得
            Animator animator = null;
            if (property.serializedObject.targetObject is ModuleParamInfo moduleParamInfo)
            {
                animator = moduleParamInfo.animator;
                if (animator == null)
                {
                    animator = moduleParamInfo.GetComponent<Animator>();
                }
            }

            float lineHeight = EditorGUIUtility.singleLineHeight + 2f;
            float y = position.y;

            // paramName（プルダウンまたはテキストフィールド）
            Rect paramNameRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            string[] paramNames = null;
            int selectedIndex = -1;
            bool paramExistsInAnimator = false;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var parameters = animator.parameters;
                if (parameters.Length > 0)
                {
                    var namesList = new System.Collections.Generic.List<string> { "(未選択)" };
                    foreach (var param in parameters)
                    {
                        namesList.Add(param.name);
                        if (param.name == paramNameProp.stringValue)
                        {
                            selectedIndex = namesList.Count - 1;
                            paramExistsInAnimator = true;
                        }
                    }
                    paramNames = namesList.ToArray();
                }
            }

            if (paramNames != null && paramNames.Length > 1)
            {
                bool showDropdown = string.IsNullOrEmpty(paramNameProp.stringValue) || paramExistsInAnimator;

                if (showDropdown)
                {
                    EditorGUI.BeginChangeCheck();
                    int newIndex = EditorGUI.Popup(paramNameRect, "Param Name", selectedIndex < 0 ? 0 : selectedIndex, paramNames);
                    if (EditorGUI.EndChangeCheck())
                    {
                        string newValue = newIndex > 0 ? paramNames[newIndex] : "";
                        paramNameProp.stringValue = newValue;
                        if (newIndex > 0 && animator != null)
                        {
                            var enumValues = (AnimatorControllerParameterType[])System.Enum.GetValues(typeof(AnimatorControllerParameterType));
                            foreach (var param in animator.parameters)
                            {
                                if (param.name == newValue)
                                {
                                    paramTypeProp.enumValueIndex = System.Array.IndexOf(enumValues, param.type);
                                    SyncDefaultFromAnimatorParam(param, defaultFloatProp, defaultIntProp, defaultBoolProp);
                                    break;
                                }
                            }
                        }
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    string newValue = EditorGUI.TextField(paramNameRect, "Param Name", paramNameProp.stringValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        paramNameProp.stringValue = newValue;
                        if (!string.IsNullOrEmpty(newValue) && animator != null && animator.runtimeAnimatorController != null)
                        {
                            foreach (var param in animator.parameters)
                            {
                                if (param.name == newValue)
                                {
                                    var enumValues = (AnimatorControllerParameterType[])System.Enum.GetValues(typeof(AnimatorControllerParameterType));
                                    paramTypeProp.enumValueIndex = System.Array.IndexOf(enumValues, param.type);
                                    SyncDefaultFromAnimatorParam(param, defaultFloatProp, defaultIntProp, defaultBoolProp);
                                    break;
                                }
                            }
                        }
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
            }
            else
            {
                EditorGUI.PropertyField(paramNameRect, paramNameProp, new GUIContent("Param Name"));
            }

            y += lineHeight;

            // paramType（選択時に自動認識、表示のみ）
            Rect paramTypeRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            if (!string.IsNullOrEmpty(paramNameProp.stringValue) && animator != null && animator.runtimeAnimatorController != null)
            {
                var enumValues = (AnimatorControllerParameterType[])System.Enum.GetValues(typeof(AnimatorControllerParameterType));
                foreach (var param in animator.parameters)
                {
                    if (param.name == paramNameProp.stringValue)
                    {
                        int newTypeIndex = System.Array.IndexOf(enumValues, param.type);
                        if (paramTypeProp.enumValueIndex != newTypeIndex)
                        {
                            paramTypeProp.enumValueIndex = newTypeIndex;
                            property.serializedObject.ApplyModifiedProperties();
                        }
                        SyncDefaultFromAnimatorParam(param, defaultFloatProp, defaultIntProp, defaultBoolProp);
                        property.serializedObject.ApplyModifiedProperties();
                        break;
                    }
                }
            }
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.PropertyField(paramTypeRect, paramTypeProp, new GUIContent("Param Type"));
            EditorGUI.EndDisabledGroup();
            y += lineHeight;

            // paramExplanation（テキストエリアで改行対応）
            Rect paramExplanationLabelRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            float textAreaHeight = EditorGUIUtility.singleLineHeight * 2f;
            Rect paramExplanationRect = new Rect(position.x, y + EditorGUIUtility.singleLineHeight + 2f, position.width, textAreaHeight);
            EditorGUI.PrefixLabel(paramExplanationLabelRect, new GUIContent("Param Explanation"));
            string newExplanation = EditorGUI.TextArea(paramExplanationRect, paramExplanationProp.stringValue);
            if (paramExplanationProp.stringValue != newExplanation)
            {
                paramExplanationProp.stringValue = newExplanation;
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        private static void SyncDefaultFromAnimatorParam(
            AnimatorControllerParameter param,
            SerializedProperty defaultFloatProp,
            SerializedProperty defaultIntProp,
            SerializedProperty defaultBoolProp)
        {
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

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight + 2f;
            float textAreaHeight = EditorGUIUtility.singleLineHeight * 3f;
            return lineHeight * 3f + textAreaHeight + 2f;
        }
    }
}