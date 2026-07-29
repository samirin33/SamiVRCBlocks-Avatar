using System;
using UnityEditor;
using UnityEngine;
using Samirin33.Editor;
using Samirin33.NDMF.Module;

namespace Samirin33.NDMF.Base.Editor
{
    [CustomEditor(typeof(SamirinMABase), true)]
    [CanEditMultipleObjects]
    public class SamirinMABaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            SamirinEditorStyleHelper.DrawWithBlueBackgroundForInspectorOrSettings(() => DrawDefaultInspector());
        }

        protected void DrawWithBlueBackground(Action drawContent)
        {
            SamirinEditorStyleHelper.DrawWithBlueBackgroundForInspectorOrSettings(drawContent);
        }

        protected void DrawHelpBoxWithDefaultFont(string message, MessageType type)
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
            label ??= new GUIContent("スムージング重み(高いほどゆっくり)");

            if (smoothWeightProp.floatValue >= 1f)
            {
                EditorGUILayout.PropertyField(smoothWeightProp, label);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var newValue = EditorGUILayout.Slider(label, smoothWeightProp.floatValue, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                    smoothWeightProp.floatValue = newValue;
            }
        }
    }
}