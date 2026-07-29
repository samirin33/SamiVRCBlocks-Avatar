using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(AudioClipReplacer))]
    public class AudioClipReplacerEditor : SamirinMABaseEditor
    {
        private const string DeveloperFoldoutPrefsKey = "Samirin33.AudioClipReplacer.DeveloperExpanded";

        private SerializedProperty _mode;
        private SerializedProperty _sourceController;
        private SerializedProperty _mergeAnimator;
        private SerializedProperty _avatarDescriptor;
        private SerializedProperty _playableLayer;
        private SerializedProperty _overrides;

        private bool _developerSettingsExpanded;
        private readonly List<bool> _userItemFoldouts = new List<bool>();
        private GUIStyle _userFoldoutHeaderStyle;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty(nameof(AudioClipReplacer.mode));
            _sourceController = serializedObject.FindProperty(nameof(AudioClipReplacer.sourceController));
            _mergeAnimator = serializedObject.FindProperty(nameof(AudioClipReplacer.mergeAnimator));
            _avatarDescriptor = serializedObject.FindProperty(nameof(AudioClipReplacer.avatarDescriptor));
            _playableLayer = serializedObject.FindProperty(nameof(AudioClipReplacer.playableLayer));
            _overrides = serializedObject.FindProperty(nameof(AudioClipReplacer.overrides));

            _developerSettingsExpanded = EditorPrefs.GetBool(DeveloperFoldoutPrefsKey, false);
        }

        private GUIStyle GetUserFoldoutHeaderStyle()
        {
            if (_userFoldoutHeaderStyle != null)
                return _userFoldoutHeaderStyle;

            var baseStyle = EditorStyles.foldout;
            _userFoldoutHeaderStyle = new GUIStyle(baseStyle)
            {
                fontSize = baseStyle.fontSize + 2,
                fontStyle = FontStyle.Bold,
            };
            return _userFoldoutHeaderStyle;
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                serializedObject.Update();

                SyncDirectModeSourceController();

                DrawUserSettings();

                EditorGUILayout.Space(8f);

                var developerExpanded = EditorGUILayout.Foldout(
                    _developerSettingsExpanded,
                    "開発者向け設定",
                    true);
                if (developerExpanded != _developerSettingsExpanded)
                {
                    _developerSettingsExpanded = developerExpanded;
                    EditorPrefs.SetBool(DeveloperFoldoutPrefsKey, developerExpanded);
                }

                if (_developerSettingsExpanded)
                {
                    EditorGUI.indentLevel++;
                    DrawDeveloperSettings();
                    EditorGUI.indentLevel--;
                }

                serializedObject.ApplyModifiedProperties();
            });
        }

        private void SyncDirectModeSourceController()
        {
            if ((AudioClipReplacerMode)_mode.enumValueIndex != AudioClipReplacerMode.Direct)
                return;

            var mergeAnimator = _mergeAnimator.objectReferenceValue as ModularAvatarMergeAnimator;
            if (mergeAnimator == null)
                return;

            var animator = mergeAnimator.GetComponent<Animator>();
            var controller = animator != null ? animator.runtimeAnimatorController : null;
            if (_sourceController.objectReferenceValue != controller)
                _sourceController.objectReferenceValue = controller;
        }

        private void DrawUserSettings()
        {
            if (_overrides.arraySize == 0)
            {
                DrawHelpBoxWithDefaultFont("置き換え項目がありません。", MessageType.None);
            }
            else
            {
                DrawHelpBoxWithDefaultFont(
                    "音を置き換えたい項目を有効にしてClipsを指定してください",
                    MessageType.Info);
            }

            EnsureUserFoldoutState(_overrides.arraySize);

            for (var i = 0; i < _overrides.arraySize; i++)
                DrawUserOverrideElement(i);
        }

        private void EnsureUserFoldoutState(int count)
        {
            while (_userItemFoldouts.Count < count)
                _userItemFoldouts.Add(false);

            while (_userItemFoldouts.Count > count)
                _userItemFoldouts.RemoveAt(_userItemFoldouts.Count - 1);
        }

        private void DrawUserOverrideElement(int index)
        {
            var element = _overrides.GetArrayElementAtIndex(index);
            var enabled = element.FindPropertyRelative(nameof(AudioClipOverride.enabled));
            var description = element.FindPropertyRelative(nameof(AudioClipOverride.description));
            var target = element.FindPropertyRelative(nameof(AudioClipOverride.target));
            var clips = element.FindPropertyRelative(nameof(AudioClipOverride.clips));
            var overrideVolume = element.FindPropertyRelative(nameof(AudioClipOverride.overrideVolume));
            var volume = element.FindPropertyRelative(nameof(AudioClipOverride.volume));
            var overridePitch = element.FindPropertyRelative(nameof(AudioClipOverride.overridePitch));
            var pitch = element.FindPropertyRelative(nameof(AudioClipOverride.pitch));
            var overridePlaybackOrder = element.FindPropertyRelative(
                nameof(AudioClipOverride.overridePlaybackOrder));
            var playbackOrder = element.FindPropertyRelative(nameof(AudioClipOverride.playbackOrder));
            var playbackOrderApplySettings = element.FindPropertyRelative(
                nameof(AudioClipOverride.playbackOrderApplySettings));
            var overrideDelayInSeconds = element.FindPropertyRelative(
                nameof(AudioClipOverride.overrideDelayInSeconds));
            var delayInSeconds = element.FindPropertyRelative(nameof(AudioClipOverride.delayInSeconds));

            var isAudioSourceTarget =
                (AudioClipOverrideTarget)target.enumValueIndex
                == AudioClipOverrideTarget.AudioSource;

            var header = string.IsNullOrWhiteSpace(description.stringValue)
                ? $"置き換え {index + 1}"
                : description.stringValue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            const float leftMargin = 8f;
            const float toggleWidth = 18f;
            const float gapBeforeFoldout = 6f;

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var lineRect = EditorGUILayout.GetControlRect(false, lineHeight);
            var toggleRect = new Rect(lineRect.x + leftMargin, lineRect.y, toggleWidth, lineHeight);
            var foldoutRect = new Rect(
                toggleRect.xMax + gapBeforeFoldout,
                lineRect.y,
                lineRect.xMax - (toggleRect.xMax + gapBeforeFoldout),
                lineHeight);

            enabled.boolValue = EditorGUI.Toggle(toggleRect, enabled.boolValue);

            using (new EditorGUI.DisabledScope(!enabled.boolValue))
            {
                _userItemFoldouts[index] = EditorGUI.Foldout(
                    foldoutRect,
                    _userItemFoldouts[index],
                    header,
                    true,
                    GetUserFoldoutHeaderStyle());
            }

            if (_userItemFoldouts[index])
            {
                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                {
                    EditorGUI.indentLevel++;
                    if (isAudioSourceTarget)
                    {
                        DrawHelpBoxWithDefaultFont(
                            "AudioSource ターゲットでは Clips の先頭（1つ目）が使用されます。",
                            MessageType.None);
                    }

                    EditorGUILayout.PropertyField(clips, new GUIContent("Clips"));
                    EditorGUI.indentLevel--;

                    EditorGUILayout.PropertyField(overrideVolume, new GUIContent("Volume を上書き"));
                    if (overrideVolume.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        if (isAudioSourceTarget)
                        {
                            var vol = volume.vector2Value;
                            EditorGUI.BeginChangeCheck();
                            vol.x = EditorGUILayout.Slider(new GUIContent("Volume"), vol.x, 0f, 1f);
                            if (EditorGUI.EndChangeCheck())
                            {
                                vol.y = vol.x;
                                volume.vector2Value = vol;
                            }
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(volume, new GUIContent("Volume"));
                        }

                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.PropertyField(overridePitch, new GUIContent("Pitch を上書き"));
                    if (overridePitch.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        if (isAudioSourceTarget)
                        {
                            var p = pitch.vector2Value;
                            EditorGUI.BeginChangeCheck();
                            p.x = EditorGUILayout.FloatField(new GUIContent("Pitch"), p.x);
                            if (EditorGUI.EndChangeCheck())
                            {
                                p.y = p.x;
                                pitch.vector2Value = p;
                            }
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(pitch, new GUIContent("Pitch"));
                        }

                        EditorGUI.indentLevel--;
                    }

                    if (!isAudioSourceTarget)
                    {
                        EditorGUILayout.PropertyField(overridePlaybackOrder, new GUIContent("Playback Order を上書き"));
                        if (overridePlaybackOrder.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(playbackOrder, new GUIContent("Playback Order"));
                            EditorGUILayout.PropertyField(
                                playbackOrderApplySettings,
                                new GUIContent("Apply Settings"));
                            EditorGUI.indentLevel--;
                        }

                        EditorGUILayout.PropertyField(overrideDelayInSeconds, new GUIContent("Play On Enter Delay を上書き"));
                        if (overrideDelayInSeconds.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(
                                delayInSeconds,
                                new GUIContent("Play On Enter Delay In Seconds"));
                            delayInSeconds.floatValue = Mathf.Clamp(delayInSeconds.floatValue, 0f, 60f);
                            EditorGUI.indentLevel--;
                        }
                    }
                }

                if (GUILayout.Button("デフォルト値を取得"))
                {
                    ApplyDefaultsFromSource(
                        index,
                        clips,
                        overrideVolume,
                        volume,
                        overridePitch,
                        pitch,
                        overridePlaybackOrder,
                        playbackOrder,
                        playbackOrderApplySettings,
                        overrideDelayInSeconds,
                        delayInSeconds);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        private void ApplyDefaultsFromSource(
            int index,
            SerializedProperty clips,
            SerializedProperty overrideVolume,
            SerializedProperty volume,
            SerializedProperty overridePitch,
            SerializedProperty pitch,
            SerializedProperty overridePlaybackOrder,
            SerializedProperty playbackOrder,
            SerializedProperty playbackOrderApplySettings,
            SerializedProperty overrideDelayInSeconds,
            SerializedProperty delayInSeconds)
        {
            var replacer = (AudioClipReplacer)target;
            if (replacer.overrides == null || index < 0 || index >= replacer.overrides.Length)
                return;

            var entry = replacer.overrides[index];
            var avatarRoot = GetAvatarRoot(replacer);

            if (!AudioClipReplacerUtil.TryApplyDefaultsFromSource(
                    replacer,
                    avatarRoot,
                    entry,
                    out var message))
            {
                Debug.LogWarning($"[AudioClipReplacer] {message}", replacer);
                return;
            }

            SyncClipsProperty(clips, entry.clips);
            overrideVolume.boolValue = entry.overrideVolume;
            volume.vector2Value = entry.volume;
            overridePitch.boolValue = entry.overridePitch;
            pitch.vector2Value = entry.pitch;
            overridePlaybackOrder.boolValue = entry.overridePlaybackOrder;
            playbackOrder.enumValueIndex = (int)entry.playbackOrder;
            playbackOrderApplySettings.enumValueIndex = (int)entry.playbackOrderApplySettings;
            overrideDelayInSeconds.boolValue = entry.overrideDelayInSeconds;
            delayInSeconds.floatValue = entry.delayInSeconds;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(replacer);
            Debug.Log($"[AudioClipReplacer] {message}", replacer);
        }

        private static void SyncClipsProperty(SerializedProperty clips, AudioClip[] values)
        {
            if (values == null || values.Length == 0)
            {
                clips.ClearArray();
                return;
            }

            clips.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static GameObject GetAvatarRoot(AudioClipReplacer replacer)
        {
            var current = replacer.transform;
            while (current != null)
            {
                if (current.GetComponent<VRCAvatarDescriptor>() != null)
                    return current.gameObject;
                current = current.parent;
            }

            return null;
        }

        private void DrawDeveloperSettings()
        {
            EditorGUILayout.LabelField("モード", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mode, new GUIContent("Mode"));

            var mode = (AudioClipReplacerMode)_mode.enumValueIndex;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("指定先", EditorStyles.boldLabel);

            switch (mode)
            {
                case AudioClipReplacerMode.Direct:
                    EditorGUILayout.PropertyField(_mergeAnimator, new GUIContent("Merge Animator"));
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(
                            _sourceController,
                            new GUIContent(
                                "Source Controller",
                                "Merge Animator と同じ GameObject の Animator から自動取得されます"));
                    }

                    var mergeAnimator = _mergeAnimator.objectReferenceValue as ModularAvatarMergeAnimator;
                    if (mergeAnimator != null && mergeAnimator.GetComponent<Animator>() == null)
                    {
                        DrawHelpBoxWithDefaultFont(
                            "Merge Animator と同じ GameObject に Animator が必要です。",
                            MessageType.Warning);
                    }
                    break;

                case AudioClipReplacerMode.PlayableLayer:
                    EditorGUILayout.PropertyField(_avatarDescriptor, new GUIContent("Avatar Descriptor"));
                    EditorGUILayout.PropertyField(_playableLayer, new GUIContent("Playable Layer"));
                    break;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("置き換え項目", EditorStyles.boldLabel);

            if (GUILayout.Button("項目を追加"))
                _overrides.arraySize++;

            if (_overrides.arraySize == 0)
            {
                DrawHelpBoxWithDefaultFont(
                    "「項目を追加」で置き換え項目を作成し、ターゲット・説明・ステート名（または AudioSource）を指定してください。",
                    MessageType.None);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("説明・ターゲット", EditorStyles.boldLabel);

            for (var i = 0; i < _overrides.arraySize; i++)
                DrawDeveloperOverrideElement(i);
        }

        private static void DrawStateNamesProperty(SerializedProperty stateNames)
        {
            if (stateNames == null)
                return;

            EditorGUILayout.LabelField("検索ステート名", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            for (var i = 0; i < stateNames.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(
                    stateNames.GetArrayElementAtIndex(i),
                    new GUIContent($"ステート {i + 1}"));
                if (GUILayout.Button("削除", GUILayout.Width(48f)))
                {
                    stateNames.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("ステート名を追加"))
                stateNames.arraySize++;

            EditorGUI.indentLevel--;
        }

        private void DrawDeveloperOverrideElement(int index)
        {
            var element = _overrides.GetArrayElementAtIndex(index);
            var description = element.FindPropertyRelative(nameof(AudioClipOverride.description));
            var target = element.FindPropertyRelative(nameof(AudioClipOverride.target));
            var stateNames = element.FindPropertyRelative(nameof(AudioClipOverride.stateNames));
            var audioSource = element.FindPropertyRelative(nameof(AudioClipOverride.audioSource));

            var label = string.IsNullOrWhiteSpace(description.stringValue)
                ? $"置き換え {index + 1}"
                : description.stringValue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("↑", GUILayout.Width(28f)))
                {
                    _overrides.MoveArrayElement(index, index - 1);
                    GUI.FocusControl(null);
                }
            }

            using (new EditorGUI.DisabledScope(index >= _overrides.arraySize - 1))
            {
                if (GUILayout.Button("↓", GUILayout.Width(28f)))
                {
                    _overrides.MoveArrayElement(index, index + 1);
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

            if (GUILayout.Button("削除", GUILayout.Width(48f)))
            {
                _overrides.DeleteArrayElementAtIndex(index);
                GUI.FocusControl(null);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(
                description,
                new GUIContent("説明", "なんの音を置き換えるか（ユーザー向けに表示されます）"));

            EditorGUILayout.PropertyField(target, new GUIContent("ターゲット"));

            var targetType = (AudioClipOverrideTarget)target.enumValueIndex;
            if (targetType == AudioClipOverrideTarget.AudioSource)
            {
                EditorGUILayout.PropertyField(audioSource, new GUIContent("Audio Source"));
            }
            else
            {
                DrawStateNamesProperty(stateNames);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }
}
