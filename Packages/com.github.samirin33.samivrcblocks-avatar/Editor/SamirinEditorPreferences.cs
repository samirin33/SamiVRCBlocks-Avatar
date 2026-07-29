using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Samirin33.Editor
{
    public static class SamirinEditorPreferences
    {
        public const string PrefsKeyEnableRealtimeAnimation = "Samirin33.SamirinVRCUtility.EnableRealtimeAnimation";
        public const string PrefsKeyUseCustomFont = "Samirin33.SamirinVRCUtility.UseCustomFont";
        public const string PrefsKeyUseCustomBackground = "Samirin33.SamirinVRCUtility.UseCustomBackground";

        private const bool DefaultEnableRealtimeAnimation = true;
        private const bool DefaultUseCustomFont = true;
        private const bool DefaultUseCustomBackground = true;

        public static bool EnableRealtimeAnimation
        {
            get => EditorPrefs.GetBool(PrefsKeyEnableRealtimeAnimation, DefaultEnableRealtimeAnimation);
            set => EditorPrefs.SetBool(PrefsKeyEnableRealtimeAnimation, value);
        }

        public static bool UseCustomFont
        {
            get => EditorPrefs.GetBool(PrefsKeyUseCustomFont, DefaultUseCustomFont);
            set => EditorPrefs.SetBool(PrefsKeyUseCustomFont, value);
        }

        public static bool UseCustomBackground
        {
            get => EditorPrefs.GetBool(PrefsKeyUseCustomBackground, DefaultUseCustomBackground);
            set => EditorPrefs.SetBool(PrefsKeyUseCustomBackground, value);
        }

        [SettingsProvider]
        public static SettingsProvider CreateSamirinVRCUtilityPreferences()
        {
            return new SettingsProvider("Preferences/Samirin VRC Utility", SettingsScope.User)
            {
                label = "Samirin VRC Utility",
                keywords = new HashSet<string>(new[] { "Samirin", "VRC", "Avatar", "Animation", "Realtime", "Font", "Background" }),
                guiHandler = (searchContext) =>
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Editor UI", EditorStyles.boldLabel);
                    EditorGUILayout.Space(4);

                    TogglePref("リアルタイムアニメーション", EnableRealtimeAnimation, v => EnableRealtimeAnimation = v,
                        "エディタUIの点滅やホバー時の拡大アニメーションを有効にします。オフにするとアニメーションなしで表示されます。");
                    TogglePref("カスタムフォント", UseCustomFont, v => UseCustomFont = v,
                        "パッケージ付属のカスタムフォントでインスペクターを描画します。オフにするとUnity標準フォントで軽く表示します。");
                    TogglePref("カスタム背景", UseCustomBackground, v => UseCustomBackground = v,
                        "パネルにカスタム背景画像（角丸・枠線）を表示します。オフにすると背景処理を省略して軽量化します。");

                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox(
                        "オフにした項目は描画処理が省略され、エディタの負荷を下げられます。",
                        MessageType.None
                    );
                }
            };
        }

        private static void TogglePref(string label, bool current, System.Action<bool> set, string tooltip)
        {
            var next = EditorGUILayout.Toggle(new GUIContent(label, tooltip), current);
            if (next != current) set(next);
        }
    }
}
