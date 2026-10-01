using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Samirin33.NDMF.Base.Editor;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    [CustomEditor(typeof(CustomizeColor))]
    [CanEditMultipleObjects]
    public class CustomizeColorEditor : SamirinMABaseEditor
    {
        const string ShowDeveloperSettingsPrefsKey = "Samirin33.CustomizeColor.ShowDeveloperSettings";
        const string DeveloperSettingsMenuPath = "CONTEXT/CustomizeColor/開発者設定を隠す";

        static bool ShowDeveloperSettings
        {
            get => EditorPrefs.GetBool(ShowDeveloperSettingsPrefsKey, false);
            set => EditorPrefs.SetBool(ShowDeveloperSettingsPrefsKey, value);
        }

        [MenuItem(DeveloperSettingsMenuPath, false, 1000)]
        static void ToggleDeveloperSettings()
        {
            ShowDeveloperSettings = !ShowDeveloperSettings;
            InternalEditorUtility.RepaintAllViews();
        }

        [MenuItem(DeveloperSettingsMenuPath, true)]
        static bool ToggleDeveloperSettingsValidate()
        {
            Menu.SetChecked(DeveloperSettingsMenuPath, ShowDeveloperSettings);
            return true;
        }

        SerializedProperty _customInfos;

        void OnEnable()
        {
            _customInfos = serializedObject.FindProperty(nameof(CustomizeColor.customInfos));
        }

        public override void OnInspectorGUI()
        {
            DrawWithBlueBackground(() =>
            {
                if (_customInfos == null)
                    return;

                serializedObject.Update();

                var applyRequested = false;
                EditorGUI.BeginChangeCheck();
                if (ShowDeveloperSettings)
                    applyRequested = DrawDeveloper();
                else
                    DrawUserColors();
                var changed = EditorGUI.EndChangeCheck();

                var undoGroup = -1;
                if (changed)
                {
                    Undo.SetCurrentGroupName("色を反映");
                    undoGroup = Undo.GetCurrentGroup();
                }

                serializedObject.ApplyModifiedProperties();

                if (changed || applyRequested)
                {
                    ApplyToTargets();
                    if (undoGroup >= 0)
                        Undo.CollapseUndoOperations(undoGroup);
                }
            });
        }

        void DrawUserColors()
        {
            if (_customInfos.arraySize == 0)
            {
                DrawHelpBoxWithDefaultFont(
                    "設定できる色はありません。\n開発者はコンポーネント名を右クリックし、「開発者設定」から反映先を登録できます。",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("お好みの色を設定してください！");

            for (var i = 0; i < _customInfos.arraySize; i++)
            {
                var element = _customInfos.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawUserColor(element, i);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }
        }

        bool DrawDeveloper()
        {
            DrawHelpBoxWithDefaultFont(
                "指定した色を、メッシュのマテリアルとパーティクルへ反映します。\n" +
                "ユーザーに見えるのは色の項目だけです。反映先はこの開発者設定で編集します。\n" +
                "アップロード時（Modular Avatar より前）にマテリアルを複製して色を焼き込み、このコンポーネントは削除されます。\n" +
                "パーティクルの色はシーン上の ParticleSystem へ直接書き込みます。",
                MessageType.Info);

            EditorGUILayout.LabelField("色の設定");

            if (_customInfos.arraySize == 0)
            {
                DrawHelpBoxWithDefaultFont(
                    "色がありません。「色を追加」から項目を作り、反映先を登録してください。",
                    MessageType.Info);
            }

            var removeIndex = -1;
            var moveFrom = -1;
            var moveTo = -1;

            for (var i = 0; i < _customInfos.arraySize; i++)
            {
                var element = _customInfos.GetArrayElementAtIndex(i);
                DrawCustomInfo(element, i, ref removeIndex, ref moveFrom, ref moveTo);
            }

            if (removeIndex >= 0)
                _customInfos.DeleteArrayElementAtIndex(removeIndex);
            else if (moveFrom >= 0)
                _customInfos.MoveArrayElement(moveFrom, moveTo);

            if (GUILayout.Button("色を追加"))
                AddCustomInfo();

            EditorGUILayout.Space(4);
            var applyRequested = GUILayout.Button("色を反映");
            if (GUILayout.Button("開発者設定を隠す"))
                ShowDeveloperSettings = false;
            return applyRequested;
        }

        static void DrawUserColor(SerializedProperty element, int index)
        {
            var nameProp = element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.customName));
            var colorProp = element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.customColor));
            var hdrProp = element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.hdr));
            var inputModeProp = element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.inputMode));
            DrawUserColorControl(ColorLabel(nameProp, index), colorProp, hdrProp, inputModeProp);
        }

        void DrawCustomInfo(
            SerializedProperty element,
            int index,
            ref int removeIndex,
            ref int moveFrom,
            ref int moveTo)
        {
            var nameProp = element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.customName));
            var colorProp = element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.customColor));
            var hdrProp = element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.hdr));
            var inputModeProp = element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.inputMode));
            var targetsProp = element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.targets));
            var title = ColorLabel(nameProp, index);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            element.isExpanded = EditorGUILayout.Foldout(
                element.isExpanded,
                $"{index + 1}. {title}（反映先 {targetsProp.arraySize}）",
                true);

            if (element.isExpanded)
            {
                EditorGUILayout.PropertyField(nameProp, new GUIContent("項目名"));
                DrawInputModeField(inputModeProp, colorProp);
                DrawDeveloperColorField(colorProp, hdrProp, inputModeProp);
                EditorGUILayout.PropertyField(hdrProp, new GUIContent("HDR", "カラーピッカーを HDR にします。発光色向けです。"));

                EditorGUILayout.Space(4);
                DrawTargets(element, targetsProp);

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("上へ"))
                    {
                        moveFrom = index;
                        moveTo = index - 1;
                    }
                }

                using (new EditorGUI.DisabledScope(index >= _customInfos.arraySize - 1))
                {
                    if (GUILayout.Button("下へ"))
                    {
                        moveFrom = index;
                        moveTo = index + 1;
                    }
                }

                if (GUILayout.Button("削除"))
                    removeIndex = index;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        void DrawTargets(SerializedProperty info, SerializedProperty targetsProp)
        {
            EditorGUILayout.LabelField("反映先");

            if (targetsProp.arraySize == 0)
            {
                DrawHelpBoxWithDefaultFont(
                    "反映先がありません。マテリアルのカラープロパティか、パーティクルの色を追加してください。",
                    MessageType.Info);
            }

            var removeIndex = -1;
            var moveFrom = -1;
            var moveTo = -1;

            for (var i = 0; i < targetsProp.arraySize; i++)
            {
                var element = targetsProp.GetArrayElementAtIndex(i);
                DrawTarget(info, element, i, targetsProp.arraySize, ref removeIndex, ref moveFrom, ref moveTo);
            }

            if (removeIndex >= 0)
                targetsProp.DeleteArrayElementAtIndex(removeIndex);
            else if (moveFrom >= 0)
                targetsProp.MoveArrayElement(moveFrom, moveTo);

            if (GUILayout.Button("反映先を追加"))
                AddTarget(targetsProp);
        }

        void DrawTarget(
            SerializedProperty info,
            SerializedProperty element,
            int index,
            int count,
            ref int removeIndex,
            ref int moveFrom,
            ref int moveTo)
        {
            var typeProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.type));
            var type = (CustomizeColor.TargetType)typeProp.enumValueIndex;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, TargetTitle(element, type), true);

            if (element.isExpanded)
            {
                EditorGUILayout.PropertyField(typeProp, new GUIContent("種類"));
                if (type == CustomizeColor.TargetType.Material)
                    DrawMaterialTarget(element);
                else
                    DrawParticleTarget(element);

                DrawHsvShift(info, element);

                using (new EditorGUI.DisabledScope(targets.Length != 1 || !CanCapture(element, type)))
                {
                    if (GUILayout.Button("現在の色を読み取る"))
                        CaptureColor(info, element, type);
                }

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("上へ"))
                    {
                        moveFrom = index;
                        moveTo = index - 1;
                    }
                }

                using (new EditorGUI.DisabledScope(index >= count - 1))
                {
                    if (GUILayout.Button("下へ"))
                    {
                        moveFrom = index;
                        moveTo = index + 1;
                    }
                }

                if (GUILayout.Button("削除"))
                    removeIndex = index;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        void DrawMaterialTarget(SerializedProperty element)
        {
            var rendererProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.renderer));
            var slotProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.materialSlot));
            var propertyNameProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.propertyName));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(rendererProp, new GUIContent("Renderer"));
            var rendererChanged = EditorGUI.EndChangeCheck();

            var renderer = rendererProp.objectReferenceValue as Renderer;
            var slotChanged = DrawMaterialSlot(slotProp, renderer);
            if (rendererChanged || slotChanged)
                TryAssignDefaultProperty(renderer, slotProp.intValue, propertyNameProp);

            var material = GetSlotMaterial(renderer, slotProp.intValue);
            DrawPropertyPopup(propertyNameProp, material);

            if (renderer != null && material == null && !slotProp.hasMultipleDifferentValues)
            {
                DrawHelpBoxWithDefaultFont("このスロットにマテリアルがありません。", MessageType.Warning);
            }
            else if (material != null && !propertyNameProp.hasMultipleDifferentValues)
            {
                var propertyName = propertyNameProp.stringValue == null ? "" : propertyNameProp.stringValue.Trim();
                if (string.IsNullOrEmpty(propertyName))
                    DrawHelpBoxWithDefaultFont("カラープロパティを指定してください。", MessageType.Warning);
                else if (!material.HasColor(propertyName))
                    DrawHelpBoxWithDefaultFont($"'{propertyName}' は {material.name} のカラープロパティではありません。", MessageType.Warning);
            }
        }

        void DrawParticleTarget(SerializedProperty element)
        {
            var particleProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.particle));
            var channelProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.particleChannel));
            var preserveAlphaProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.preserveAlpha));

            EditorGUILayout.PropertyField(particleProp, new GUIContent("Particle System"));
            EditorGUILayout.PropertyField(channelProp, new GUIContent("色"));
            EditorGUILayout.PropertyField(
                preserveAlphaProp,
                new GUIContent("アルファを維持", "オンのとき、パーティクルは RGB だけ置き換え、元のアルファ（フェード）は残します。"));

            var particle = particleProp.objectReferenceValue as ParticleSystem;
            if (particle == null || channelProp.hasMultipleDifferentValues)
                return;

            var channel = (CustomizeColor.ParticleColorChannel)channelProp.enumValueIndex;
            if (!IsParticleChannelEnabled(particle, channel))
            {
                DrawHelpBoxWithDefaultFont(
                    "このモジュールはオフです。色は保存されますが、モジュールをオンにするまで見た目には出ません。",
                    MessageType.Info);
            }
        }

        void DrawHsvShift(SerializedProperty info, SerializedProperty element)
        {
            var useProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.useHsvShift));
            EditorGUILayout.PropertyField(
                useProp,
                new GUIContent("色調補正 (HSV)", "オンのとき、この反映先だけ色相・彩度・明度をシフトします。"));

            if (!useProp.boolValue && !useProp.hasMultipleDifferentValues)
                return;

            var hueProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.hueShift));
            var saturationProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.saturationShift));
            var valueProp = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.valueShift));

            EditorGUI.indentLevel++;
            var hue = DrawShiftSlider(hueProp, new GUIContent("色相", "度で加算します。360 度で 1 周します。"), -180f, 180f);
            var saturation = DrawShiftSlider(saturationProp, new GUIContent("彩度", "現在の彩度へ加算します。"), -1f, 1f);
            var value = DrawShiftSlider(valueProp, new GUIContent("明度", "現在の明度へ加算します。HDR の強さは残します。"), -1f, 1f);
            DrawShiftedColorPreview(info, hue, saturation, value);
            EditorGUI.indentLevel--;
        }

        static float DrawShiftSlider(SerializedProperty property, GUIContent label, float min, float max)
        {
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.Slider(label, property.floatValue, min, max);
            if (EditorGUI.EndChangeCheck())
                property.floatValue = next;
            EditorGUI.showMixedValue = false;
            return property.hasMultipleDifferentValues ? property.floatValue : next;
        }

        void DrawShiftedColorPreview(SerializedProperty info, float hue, float saturation, float value)
        {
            if (info == null || targets.Length != 1)
                return;

            var colorProp = info.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.customColor));
            var hdrProp = info.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.hdr));
            if (colorProp == null || colorProp.hasMultipleDifferentValues)
                return;

            var shifted = colorProp.colorValue;
            var inputModeProp = info.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.inputMode));
            if (inputModeProp != null && !inputModeProp.hasMultipleDifferentValues)
            {
                shifted = CustomizeColor.ResolveUserColor(
                    shifted,
                    (CustomizeColor.ColorInputMode)inputModeProp.enumValueIndex);
            }

            if (!Mathf.Approximately(hue, 0f)
                || !Mathf.Approximately(saturation, 0f)
                || !Mathf.Approximately(value, 0f))
                shifted = CustomizeColor.ShiftHsv(shifted, hue, saturation, value);

            var rect = EditorGUILayout.GetControlRect();
            var previewEvent = Event.current;
            if (previewEvent.type == EventType.MouseDown && rect.Contains(previewEvent.mousePosition))
                previewEvent.Use();

            EditorGUI.ColorField(
                rect,
                new GUIContent("シフト後", "この反映先に書き込まれる色です。"),
                shifted,
                false,
                true,
                UseHdr(hdrProp));
        }

        bool DrawMaterialSlot(SerializedProperty slotProp, Renderer renderer)
        {
            if (slotProp.hasMultipleDifferentValues || renderer == null)
            {
                EditorGUILayout.PropertyField(slotProp, new GUIContent("スロット"));
                return false;
            }

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                EditorGUILayout.PropertyField(slotProp, new GUIContent("スロット"));
                DrawHelpBoxWithDefaultFont("Renderer にマテリアルがありません。", MessageType.Warning);
                return false;
            }

            var current = slotProp.intValue;
            if (current < 0 || current >= materials.Length)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(slotProp, new GUIContent("スロット"));
                var slotEdited = EditorGUI.EndChangeCheck();
                DrawHelpBoxWithDefaultFont("スロット番号がマテリアル数の範囲外です。", MessageType.Warning);
                return slotEdited;
            }

            var labels = new GUIContent[materials.Length];
            for (var i = 0; i < materials.Length; i++)
            {
                var materialName = materials[i] != null ? materials[i].name : "(None)";
                labels[i] = new GUIContent($"{i}: {materialName}");
            }

            var next = EditorGUILayout.Popup(new GUIContent("スロット"), current, labels);
            if (next == current)
                return false;

            slotProp.intValue = next;
            return true;
        }

        static void DrawPropertyPopup(SerializedProperty propertyNameProp, Material material)
        {
            if (propertyNameProp.hasMultipleDifferentValues || material == null || material.shader == null)
            {
                EditorGUILayout.PropertyField(propertyNameProp, new GUIContent("プロパティ"));
                return;
            }

            var shader = material.shader;
            var names = new List<string>();
            var labels = new List<GUIContent>();
            var count = ShaderUtil.GetPropertyCount(shader);
            for (var i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.Color)
                    continue;

                var propertyName = ShaderUtil.GetPropertyName(shader, i);
                var description = ShaderUtil.GetPropertyDescription(shader, i);
                names.Add(propertyName);
                labels.Add(new GUIContent(string.IsNullOrEmpty(description)
                    ? propertyName
                    : $"{description} ({propertyName})"));
            }

            if (names.Count == 0)
            {
                EditorGUILayout.PropertyField(propertyNameProp, new GUIContent("プロパティ"));
                DrawHelpBoxWithDefaultFontStatic("このシェーダーにカラープロパティがありません。", MessageType.Warning);
                return;
            }

            var currentName = propertyNameProp.stringValue ?? "";
            var index = names.IndexOf(currentName);
            if (index < 0)
            {
                names.Insert(0, currentName);
                labels.Insert(0, new GUIContent(string.IsNullOrEmpty(currentName) ? "(未設定)" : currentName));
                index = 0;
            }

            var next = EditorGUILayout.Popup(new GUIContent("プロパティ"), index, labels.ToArray());
            if (next != index && next >= 0 && next < names.Count)
                propertyNameProp.stringValue = names[next];
        }

        static void TryAssignDefaultProperty(Renderer renderer, int slot, SerializedProperty propertyNameProp)
        {
            if (propertyNameProp == null)
                return;

            var material = GetSlotMaterial(renderer, slot);
            var current = propertyNameProp.stringValue ?? "";
            if (material != null && !string.IsNullOrEmpty(current) && material.HasColor(current))
                return;

            var first = FirstColorProperty(material);
            if (!string.IsNullOrEmpty(first))
                propertyNameProp.stringValue = first;
        }

        static string FirstColorProperty(Material material)
        {
            if (material == null || material.shader == null)
                return null;

            var shader = material.shader;
            var count = ShaderUtil.GetPropertyCount(shader);
            for (var i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.Color)
                    return ShaderUtil.GetPropertyName(shader, i);
            }

            return null;
        }

        static void DrawUserColorControl(
            string label,
            SerializedProperty colorProp,
            SerializedProperty hdrProp,
            SerializedProperty inputModeProp)
        {
            var hdr = UseHdr(hdrProp);
            var content = new GUIContent(label);
            if (inputModeProp == null || inputModeProp.hasMultipleDifferentValues)
            {
                DrawColorField(content, colorProp, hdr, true);
                return;
            }

            switch ((CustomizeColor.ColorInputMode)inputModeProp.enumValueIndex)
            {
                case CustomizeColor.ColorInputMode.FreeNoAlpha:
                    DrawColorField(content, colorProp, hdr, false);
                    break;
                case CustomizeColor.ColorInputMode.HueOnly:
                    DrawHueSlider(content, colorProp, hdr);
                    break;
                default:
                    DrawColorField(content, colorProp, hdr, true);
                    break;
            }
        }

        static void DrawInputModeField(SerializedProperty inputModeProp, SerializedProperty colorProp)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                inputModeProp,
                new GUIContent("指定方法", "ユーザーがこの色を指定する方法です。"));
            if (!EditorGUI.EndChangeCheck() || inputModeProp.hasMultipleDifferentValues)
                return;

            if ((CustomizeColor.ColorInputMode)inputModeProp.enumValueIndex != CustomizeColor.ColorInputMode.FreeNoAlpha)
                return;

            var color = colorProp.colorValue;
            color.a = 1f;
            colorProp.colorValue = color;
        }

        static void DrawDeveloperColorField(
            SerializedProperty colorProp,
            SerializedProperty hdrProp,
            SerializedProperty inputModeProp)
        {
            var hdr = UseHdr(hdrProp);
            if (inputModeProp != null
                && !inputModeProp.hasMultipleDifferentValues
                && (CustomizeColor.ColorInputMode)inputModeProp.enumValueIndex == CustomizeColor.ColorInputMode.HueOnly)
            {
                DrawColorField(
                    new GUIContent("基準色", "彩度・明度・アルファはここを使います。ユーザーは色相だけ変更できます。"),
                    colorProp,
                    hdr,
                    true);
                return;
            }

            var showAlpha = inputModeProp == null
                || inputModeProp.hasMultipleDifferentValues
                || (CustomizeColor.ColorInputMode)inputModeProp.enumValueIndex != CustomizeColor.ColorInputMode.FreeNoAlpha;
            DrawColorField(new GUIContent("色"), colorProp, hdr, showAlpha);
        }

        static void DrawHueSlider(GUIContent label, SerializedProperty colorProp, bool hdr)
        {
            var current = colorProp.colorValue;
            EditorGUILayout.BeginHorizontal();
            EditorGUI.showMixedValue = colorProp.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var nextHue = EditorGUILayout.Slider(label, CustomizeColor.GetHueDegrees(current), 0f, 360f);
            var changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (changed)
            {
                current = CustomizeColor.WithHueDegrees(current, nextHue);
                colorProp.colorValue = current;
            }

            if (!colorProp.hasMultipleDifferentValues)
                DrawLockedSwatch(current, hdr);
            EditorGUILayout.EndHorizontal();
        }

        static void DrawLockedSwatch(Color color, bool hdr)
        {
            var rect = GUILayoutUtility.GetRect(
                48f,
                EditorGUIUtility.singleLineHeight,
                GUILayout.Width(48f),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            var previewEvent = Event.current;
            if ((previewEvent.type == EventType.MouseDown || previewEvent.type == EventType.MouseDrag)
                && rect.Contains(previewEvent.mousePosition))
                previewEvent.Use();

            EditorGUI.ColorField(rect, GUIContent.none, color, false, false, hdr);
        }

        static void DrawColorField(GUIContent label, SerializedProperty colorProp, bool hdr, bool showAlpha)
        {
            if (colorProp == null)
                return;

            EditorGUI.showMixedValue = colorProp.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.ColorField(label, colorProp.colorValue, true, showAlpha, hdr);
            if (EditorGUI.EndChangeCheck())
            {
                if (!showAlpha)
                    next.a = 1f;
                colorProp.colorValue = next;
            }

            EditorGUI.showMixedValue = false;
        }

        static void DrawHelpBoxWithDefaultFontStatic(string message, MessageType type)
        {
            Samirin33.Editor.SamirinEditorStyleHelper.DrawHelpBoxWithDefaultFont(message, type);
        }

        static string ColorLabel(SerializedProperty nameProp, int index)
        {
            if (nameProp == null || nameProp.hasMultipleDifferentValues || string.IsNullOrWhiteSpace(nameProp.stringValue))
                return $"色 {index + 1}";

            return nameProp.stringValue;
        }

        static bool UseHdr(SerializedProperty hdrProp)
        {
            return hdrProp != null && (hdrProp.boolValue || hdrProp.hasMultipleDifferentValues);
        }

        static string TargetTitle(SerializedProperty element, CustomizeColor.TargetType type)
        {
            if (type == CustomizeColor.TargetType.Material)
            {
                var renderer = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.renderer)).objectReferenceValue as Renderer;
                var propertyName = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.propertyName)).stringValue;
                var rendererName = renderer != null ? renderer.name : "未設定";
                var propertyLabel = string.IsNullOrWhiteSpace(propertyName) ? "プロパティ未設定" : propertyName;
                return $"マテリアル: {rendererName} / {propertyLabel}";
            }

            var particle = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.particle)).objectReferenceValue as ParticleSystem;
            var channel = (CustomizeColor.ParticleColorChannel)element
                .FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.particleChannel)).enumValueIndex;
            var particleName = particle != null ? particle.name : "未設定";
            return $"パーティクル: {particleName} / {ChannelLabel(channel)}";
        }

        static string ChannelLabel(CustomizeColor.ParticleColorChannel channel)
        {
            switch (channel)
            {
                case CustomizeColor.ParticleColorChannel.StartColor:
                    return "開始色";
                case CustomizeColor.ParticleColorChannel.ColorOverLifetime:
                    return "寿命に応じた色";
                case CustomizeColor.ParticleColorChannel.ColorBySpeed:
                    return "速度に応じた色";
                case CustomizeColor.ParticleColorChannel.TrailColorOverLifetime:
                    return "トレイル（寿命）";
                case CustomizeColor.ParticleColorChannel.TrailColorOverTrail:
                    return "トレイル（長さ）";
                default:
                    return channel.ToString();
            }
        }

        static bool IsParticleChannelEnabled(ParticleSystem particle, CustomizeColor.ParticleColorChannel channel)
        {
            switch (channel)
            {
                case CustomizeColor.ParticleColorChannel.ColorOverLifetime:
                    return particle.colorOverLifetime.enabled;
                case CustomizeColor.ParticleColorChannel.ColorBySpeed:
                    return particle.colorBySpeed.enabled;
                case CustomizeColor.ParticleColorChannel.TrailColorOverLifetime:
                case CustomizeColor.ParticleColorChannel.TrailColorOverTrail:
                    return particle.trails.enabled;
                default:
                    return true;
            }
        }

        static bool CanCapture(SerializedProperty element, CustomizeColor.TargetType type)
        {
            if (type == CustomizeColor.TargetType.Material)
            {
                var renderer = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.renderer)).objectReferenceValue as Renderer;
                var slot = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.materialSlot)).intValue;
                var propertyName = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.propertyName)).stringValue;
                var material = GetSlotMaterial(renderer, slot);
                return material != null && !string.IsNullOrEmpty(propertyName) && material.HasColor(propertyName);
            }

            return element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.particle)).objectReferenceValue != null;
        }

        static void CaptureColor(SerializedProperty info, SerializedProperty element, CustomizeColor.TargetType type)
        {
            var colorProp = info.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.customColor));
            if (type == CustomizeColor.TargetType.Material)
            {
                var renderer = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.renderer)).objectReferenceValue as Renderer;
                var slot = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.materialSlot)).intValue;
                var propertyName = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.propertyName)).stringValue;
                var material = GetSlotMaterial(renderer, slot);
                if (material != null && material.HasColor(propertyName))
                    colorProp.colorValue = material.GetColor(propertyName);
                return;
            }

            var particle = element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.particle)).objectReferenceValue as ParticleSystem;
            if (particle == null)
                return;

            var channel = (CustomizeColor.ParticleColorChannel)element
                .FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.particleChannel)).enumValueIndex;
            colorProp.colorValue = ReadParticleColor(particle, channel);
        }

        static Color ReadParticleColor(ParticleSystem particle, CustomizeColor.ParticleColorChannel channel)
        {
            ParticleSystem.MinMaxGradient gradient;
            switch (channel)
            {
                case CustomizeColor.ParticleColorChannel.ColorOverLifetime:
                    gradient = particle.colorOverLifetime.color;
                    break;
                case CustomizeColor.ParticleColorChannel.ColorBySpeed:
                    gradient = particle.colorBySpeed.color;
                    break;
                case CustomizeColor.ParticleColorChannel.TrailColorOverLifetime:
                    gradient = particle.trails.colorOverLifetime;
                    break;
                case CustomizeColor.ParticleColorChannel.TrailColorOverTrail:
                    gradient = particle.trails.colorOverTrail;
                    break;
                default:
                    gradient = particle.main.startColor;
                    break;
            }

            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.TwoColors:
                    return gradient.colorMax;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    return FirstGradientColor(gradient.gradient);
                case ParticleSystemGradientMode.TwoGradients:
                    return FirstGradientColor(gradient.gradientMax);
                default:
                    return gradient.color;
            }
        }

        static Color FirstGradientColor(Gradient gradient)
        {
            if (gradient == null || gradient.colorKeys == null || gradient.colorKeys.Length == 0)
                return Color.white;

            var color = gradient.colorKeys[0].color;
            if (gradient.alphaKeys != null && gradient.alphaKeys.Length > 0)
                color.a = gradient.alphaKeys[0].alpha;
            return color;
        }

        static Material GetSlotMaterial(Renderer renderer, int slot)
        {
            if (renderer == null)
                return null;

            var materials = renderer.sharedMaterials;
            if (materials == null || slot < 0 || slot >= materials.Length)
                return null;

            return materials[slot];
        }

        void AddCustomInfo()
        {
            _customInfos.arraySize++;
            var element = _customInfos.GetArrayElementAtIndex(_customInfos.arraySize - 1);
            element.isExpanded = true;
            element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.customName)).stringValue = "色";
            element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.inputMode)).enumValueIndex =
                (int)CustomizeColor.ColorInputMode.Free;
            element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.customColor)).colorValue = Color.white;
            element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.hdr)).boolValue = false;
            element.FindPropertyRelative(nameof(CustomizeColor.CustomInfo.targets)).arraySize = 0;
        }

        static void AddTarget(SerializedProperty targetsProp)
        {
            targetsProp.arraySize++;
            var element = targetsProp.GetArrayElementAtIndex(targetsProp.arraySize - 1);
            element.isExpanded = true;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.type)).enumValueIndex =
                (int)CustomizeColor.TargetType.Material;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.renderer)).objectReferenceValue = null;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.materialSlot)).intValue = 0;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.propertyName)).stringValue = "_Color";
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.particle)).objectReferenceValue = null;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.particleChannel)).enumValueIndex =
                (int)CustomizeColor.ParticleColorChannel.StartColor;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.preserveAlpha)).boolValue = true;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.useHsvShift)).boolValue = false;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.hueShift)).floatValue = 0f;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.saturationShift)).floatValue = 0f;
            element.FindPropertyRelative(nameof(CustomizeColor.ApplyTarget.valueShift)).floatValue = 0f;
        }

        void ApplyToTargets()
        {
            var mutated = new List<Object>();
            var components = new List<CustomizeColor>();

            foreach (var selected in targets)
            {
                if (selected is not CustomizeColor customize)
                    continue;

                components.Add(customize);
                customize.CollectApplyTargets(mutated);
            }

            if (mutated.Count > 0)
                Undo.RecordObjects(mutated.ToArray(), "色を反映");

            foreach (var customize in components)
            {
                customize.ApplyPreview();
                EditorUtility.SetDirty(customize);
            }

            foreach (var mutatedObject in mutated)
                EditorUtility.SetDirty(mutatedObject);
        }
    }
}
