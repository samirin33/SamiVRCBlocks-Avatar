using System;
using System.Collections.Generic;
using UnityEngine;
using Samirin33.NDMF.Base;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// ユーザーが選んだ色を、開発者が登録したマテリアルのカラープロパティと
    /// パーティクルの色へ反映する。
    /// ビルド時（Resolving / MA 前）に焼き込んで自身を削除する。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB CustomizeColor")]
    public class CustomizeColor : SamirinMABase
    {
        public enum TargetType
        {
            [InspectorName("マテリアル")]
            Material,
            [InspectorName("パーティクル")]
            Particle,
        }

        public enum ColorInputMode
        {
            [InspectorName("自由指定")]
            Free,
            [InspectorName("アルファなし自由指定")]
            FreeNoAlpha,
            [InspectorName("色相のみ")]
            HueOnly,
        }

        public enum ParticleColorChannel
        {
            [InspectorName("開始色")]
            StartColor,
            [InspectorName("寿命に応じた色")]
            ColorOverLifetime,
            [InspectorName("速度に応じた色")]
            ColorBySpeed,
            [InspectorName("トレイル（寿命）")]
            TrailColorOverLifetime,
            [InspectorName("トレイル（長さ）")]
            TrailColorOverTrail,
        }

        [Serializable]
        public class ApplyTarget
        {
            public TargetType type = TargetType.Material;

            [Tooltip("色を書き込む Renderer（Mesh / Skinned Mesh / Particle System Renderer）")]
            public Renderer renderer;

            [Tooltip("sharedMaterials のスロット番号")]
            public int materialSlot;

            [Tooltip("シェーダーのカラープロパティ名")]
            public string propertyName = "_Color";

            [Tooltip("色を書き込む ParticleSystem")]
            public ParticleSystem particle;

            public ParticleColorChannel particleChannel = ParticleColorChannel.StartColor;

            [Tooltip("オンのとき、パーティクルは RGB だけ置き換え、元のアルファ（フェード）は残します。オフのときは指定色のアルファも反映します。")]
            public bool preserveAlpha = true;

            [Tooltip("オンのとき、この反映先だけ色相・彩度・明度をシフトします。")]
            public bool useHsvShift;

            [Tooltip("色相のシフト（度）。360 度で 1 周します。")]
            public float hueShift;

            [Tooltip("彩度のシフト。現在の彩度へ加算します。")]
            public float saturationShift;

            [Tooltip("明度のシフト。現在の明度へ加算します。HDR の強さは残します。")]
            public float valueShift;
        }

        [Serializable]
        public class CustomInfo
        {
            [Tooltip("ユーザーに見せる項目名")]
            public string customName = "色";

            [Tooltip("ユーザーが色を指定する方法")]
            public ColorInputMode inputMode = ColorInputMode.Free;

            [Tooltip("アバターへ反映する色。色相のみのときは、彩度・明度・アルファの基準になります。")]
            public Color customColor = Color.white;

            [Tooltip("オンのとき、カラーピッカーを HDR にします。発光色向けです。")]
            public bool hdr;

            [Tooltip("この色の反映先。マテリアルのカラープロパティとパーティクルの色を複数登録できます。")]
            public ApplyTarget[] targets = new ApplyTarget[0];
        }

        [Tooltip("ユーザーが編集する色と、その反映先")]
        public CustomInfo[] customInfos = new CustomInfo[0];

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar)
                return;

            ApplyBuild();
            DestroyImmediate(this);
        }

        /// <summary>
        /// エディタ上で見た目へ反映する。マテリアルは本体を変えず MaterialPropertyBlock で、
        /// パーティクルは元の色を覚えたうえで書き換え、無効化・保存時に元へ戻す。
        /// </summary>
        public void ApplyPreview()
        {
#if UNITY_EDITOR
            ClearMaterialPreview();
            ClearParticlePreview();
            Apply(bakeMaterials: false, logWarnings: false);
#endif
        }

        public void CollectApplyTargets(List<UnityEngine.Object> results)
        {
            if (results == null || customInfos == null)
                return;

            for (var i = 0; i < customInfos.Length; i++)
            {
                var info = customInfos[i];
                if (info?.targets == null)
                    continue;

                for (var targetIndex = 0; targetIndex < info.targets.Length; targetIndex++)
                {
                    var target = info.targets[targetIndex];
                    if (target == null)
                        continue;

                    if (target.type == TargetType.Material && target.renderer != null && !results.Contains(target.renderer))
                        results.Add(target.renderer);
                    else if (target.type == TargetType.Particle && target.particle != null && !results.Contains(target.particle))
                        results.Add(target.particle);
                }
            }
        }

        void ApplyBuild()
        {
            Apply(bakeMaterials: true, logWarnings: true);
        }

        /// <summary>
        /// 反映先の HSV シフトを色へ適用する。シフトがオフ、またはゼロのときは元の色を返す。
        /// </summary>
        public static Color ApplyHsvShift(Color color, ApplyTarget target)
        {
            if (target == null || !target.useHsvShift)
                return color;
            if (Mathf.Approximately(target.hueShift, 0f)
                && Mathf.Approximately(target.saturationShift, 0f)
                && Mathf.Approximately(target.valueShift, 0f))
                return color;

            return ShiftHsv(color, target.hueShift, target.saturationShift, target.valueShift);
        }

        /// <summary>
        /// 色相（度）・彩度・明度を加算する。アルファはそのまま残す。
        /// </summary>
        public static Color ShiftHsv(Color color, float hueDegrees, float saturationShift, float valueShift)
        {
            Color.RGBToHSV(color, out var hue, out var saturation, out var value);
            hue = Mathf.Repeat(hue + hueDegrees / 360f, 1f);
            saturation = Mathf.Clamp01(saturation + saturationShift);
            value = Mathf.Max(0f, value + valueShift);

            var shifted = Color.HSVToRGB(hue, saturation, value, true);
            shifted.a = color.a;
            return shifted;
        }

        /// <summary>
        /// 指定方法に合わせて、反映に使う色を決める。アルファなしのときはアルファを 1 にする。
        /// </summary>
        public static Color ResolveUserColor(Color color, ColorInputMode mode)
        {
            if (mode == ColorInputMode.FreeNoAlpha)
                color.a = 1f;
            return color;
        }

        public static float GetHueDegrees(Color color)
        {
            Color.RGBToHSV(color, out var hue, out _, out _);
            return hue * 360f;
        }

        /// <summary>
        /// 彩度・明度・アルファを残して色相だけ差し替える。
        /// </summary>
        public static Color WithHueDegrees(Color color, float hueDegrees)
        {
            Color.RGBToHSV(color, out _, out var saturation, out var value);
            var next = Color.HSVToRGB(Mathf.Repeat(hueDegrees / 360f, 1f), saturation, value, true);
            next.a = color.a;
            return next;
        }

        void Apply(bool bakeMaterials, bool logWarnings)
        {
            if (customInfos == null)
                return;

            Dictionary<MaterialSlotKey, Material> bakedMaterials = null;
            if (bakeMaterials)
                bakedMaterials = new Dictionary<MaterialSlotKey, Material>();

            for (var i = 0; i < customInfos.Length; i++)
            {
                var info = customInfos[i];
                if (info?.targets == null)
                    continue;

                for (var targetIndex = 0; targetIndex < info.targets.Length; targetIndex++)
                {
                    var target = info.targets[targetIndex];
                    if (target == null)
                        continue;

                    var label = FormatTarget(info, i, targetIndex);
                    var color = ApplyHsvShift(ResolveUserColor(info.customColor, info.inputMode), target);
                    if (target.type == TargetType.Material)
                        ApplyMaterial(target, color, bakedMaterials, logWarnings, label);
                    else
                        ApplyParticle(target, color, preview: !bakeMaterials, logWarnings, label);
                }
            }
        }

        void ApplyMaterial(
            ApplyTarget target,
            Color color,
            Dictionary<MaterialSlotKey, Material> bakedMaterials,
            bool logWarnings,
            string label)
        {
            var renderer = target.renderer;
            if (renderer == null)
            {
                if (logWarnings)
                    SamirinRuntimeReport.Warning($"[CustomizeColor] Renderer が未設定です。({label})", this);
                return;
            }

            var materials = renderer.sharedMaterials;
            if (materials == null || target.materialSlot < 0 || target.materialSlot >= materials.Length)
            {
                if (logWarnings)
                    SamirinRuntimeReport.Warning($"[CustomizeColor] マテリアルスロットが範囲外です。({label})", this);
                return;
            }

            var source = materials[target.materialSlot];
            if (source == null)
            {
                if (logWarnings)
                    SamirinRuntimeReport.Warning($"[CustomizeColor] スロット {target.materialSlot} のマテリアルが未設定です。({label})", this);
                return;
            }

            var propertyName = target.propertyName == null ? "" : target.propertyName.Trim();
            if (string.IsNullOrEmpty(propertyName) || !source.HasColor(propertyName))
            {
                if (logWarnings)
                    SamirinRuntimeReport.Warning($"[CustomizeColor] カラープロパティ '{propertyName}' がマテリアル '{source.name}' にありません。({label})", this);
                return;
            }

            if (bakedMaterials != null)
            {
                var material = GetOrCloneMaterial(bakedMaterials, renderer, target.materialSlot, source);
                material.SetColor(propertyName, color);
                return;
            }

#if UNITY_EDITOR
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, target.materialSlot);
            TrackPreviewSlot(renderer, target.materialSlot, propertyName, block);
            block.SetColor(propertyName, color);
            renderer.SetPropertyBlock(block, target.materialSlot);
#endif
        }

        static Material GetOrCloneMaterial(
            Dictionary<MaterialSlotKey, Material> bakedMaterials,
            Renderer renderer,
            int slot,
            Material source)
        {
            var key = new MaterialSlotKey(renderer.GetInstanceID(), slot);
            if (bakedMaterials.TryGetValue(key, out var cached))
                return cached;

            var clone = new Material(source)
            {
                name = source.name,
                hideFlags = HideFlags.None,
            };

            var materials = renderer.sharedMaterials;
            materials[slot] = clone;
            renderer.sharedMaterials = materials;
            bakedMaterials.Add(key, clone);
            return clone;
        }

        void ApplyParticle(ApplyTarget target, Color color, bool preview, bool logWarnings, string label)
        {
            var particle = target.particle;
            if (particle == null)
            {
                if (logWarnings)
                    SamirinRuntimeReport.Warning($"[CustomizeColor] ParticleSystem が未設定です。({label})", this);
                return;
            }

            var current = GetParticleGradient(particle, target.particleChannel);
            var next = Recolor(current, color, target.preserveAlpha);
            if (Approximately(current, next))
                return;

#if UNITY_EDITOR
            if (preview)
                TrackParticlePreview(particle, target.particleChannel, current, next);
#endif
            SetParticleGradient(particle, target.particleChannel, next);
        }

        static ParticleSystem.MinMaxGradient GetParticleGradient(ParticleSystem particle, ParticleColorChannel channel)
        {
            switch (channel)
            {
                case ParticleColorChannel.ColorOverLifetime:
                    return particle.colorOverLifetime.color;
                case ParticleColorChannel.ColorBySpeed:
                    return particle.colorBySpeed.color;
                case ParticleColorChannel.TrailColorOverLifetime:
                    return particle.trails.colorOverLifetime;
                case ParticleColorChannel.TrailColorOverTrail:
                    return particle.trails.colorOverTrail;
                default:
                    return particle.main.startColor;
            }
        }

        static void SetParticleGradient(ParticleSystem particle, ParticleColorChannel channel, ParticleSystem.MinMaxGradient value)
        {
            switch (channel)
            {
                case ParticleColorChannel.ColorOverLifetime:
                {
                    var module = particle.colorOverLifetime;
                    module.color = value;
                    break;
                }
                case ParticleColorChannel.ColorBySpeed:
                {
                    var module = particle.colorBySpeed;
                    module.color = value;
                    break;
                }
                case ParticleColorChannel.TrailColorOverLifetime:
                {
                    var module = particle.trails;
                    module.colorOverLifetime = value;
                    break;
                }
                case ParticleColorChannel.TrailColorOverTrail:
                {
                    var module = particle.trails;
                    module.colorOverTrail = value;
                    break;
                }
                default:
                {
                    var main = particle.main;
                    main.startColor = value;
                    break;
                }
            }
        }

        static ParticleSystem.MinMaxGradient Recolor(ParticleSystem.MinMaxGradient source, Color color, bool preserveAlpha)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                {
                    var solid = color;
                    if (preserveAlpha)
                        solid.a = source.color.a;
                    return new ParticleSystem.MinMaxGradient(solid);
                }
                case ParticleSystemGradientMode.TwoColors:
                {
                    var min = color;
                    var max = color;
                    if (preserveAlpha)
                    {
                        min.a = source.colorMin.a;
                        max.a = source.colorMax.a;
                    }

                    return new ParticleSystem.MinMaxGradient(min, max);
                }
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(RecolorGradient(source.gradient, color, preserveAlpha));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        RecolorGradient(source.gradientMin, color, preserveAlpha),
                        RecolorGradient(source.gradientMax, color, preserveAlpha));
                case ParticleSystemGradientMode.RandomColor:
                {
                    var result = new ParticleSystem.MinMaxGradient(RecolorGradient(source.gradient, color, preserveAlpha));
                    result.mode = ParticleSystemGradientMode.RandomColor;
                    return result;
                }
                default:
                    return new ParticleSystem.MinMaxGradient(color);
            }
        }

        static Gradient RecolorGradient(Gradient source, Color color, bool preserveAlpha)
        {
            var gradient = new Gradient();
            GradientColorKey[] colorKeys;
            GradientAlphaKey[] alphaKeys;

            if (source == null || source.colorKeys == null || source.colorKeys.Length == 0)
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f),
                };
            }
            else
            {
                colorKeys = (GradientColorKey[])source.colorKeys.Clone();
                for (var i = 0; i < colorKeys.Length; i++)
                {
                    var key = colorKeys[i];
                    key.color = new Color(color.r, color.g, color.b, preserveAlpha ? key.color.a : color.a);
                    colorKeys[i] = key;
                }
            }

            if (source == null || source.alphaKeys == null || source.alphaKeys.Length == 0)
            {
                alphaKeys = new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(color.a, 1f),
                };
            }
            else if (!preserveAlpha)
            {
                alphaKeys = (GradientAlphaKey[])source.alphaKeys.Clone();
                for (var i = 0; i < alphaKeys.Length; i++)
                {
                    var key = alphaKeys[i];
                    key.alpha = color.a;
                    alphaKeys[i] = key;
                }
            }
            else
            {
                alphaKeys = (GradientAlphaKey[])source.alphaKeys.Clone();
            }

            colorKeys = EnsureTwoColorKeys(colorKeys, color);
            alphaKeys = EnsureTwoAlphaKeys(alphaKeys, preserveAlpha ? alphaKeys[0].alpha : color.a);
            gradient.SetKeys(colorKeys, alphaKeys);
            if (source != null)
                gradient.mode = source.mode;
            return gradient;
        }

        static GradientColorKey[] EnsureTwoColorKeys(GradientColorKey[] keys, Color color)
        {
            if (keys == null || keys.Length == 0)
                return new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) };
            if (keys.Length == 1)
            {
                var only = keys[0];
                if (only.time > 0.5f)
                    return new[] { new GradientColorKey(only.color, 0f), only };
                return new[] { only, new GradientColorKey(only.color, 1f) };
            }
            return keys;
        }

        static GradientAlphaKey[] EnsureTwoAlphaKeys(GradientAlphaKey[] keys, float alpha)
        {
            if (keys == null || keys.Length == 0)
                return new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) };
            if (keys.Length == 1)
            {
                var only = keys[0];
                if (only.time > 0.5f)
                    return new[] { new GradientAlphaKey(only.alpha, 0f), only };
                return new[] { only, new GradientAlphaKey(only.alpha, 1f) };
            }
            return keys;
        }

        static bool Approximately(ParticleSystem.MinMaxGradient a, ParticleSystem.MinMaxGradient b)
        {
            if (a.mode != b.mode)
                return false;

            switch (a.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return ColorEquals(a.color, b.color);
                case ParticleSystemGradientMode.TwoColors:
                    return ColorEquals(a.colorMin, b.colorMin) && ColorEquals(a.colorMax, b.colorMax);
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    return GradientEquals(a.gradient, b.gradient);
                case ParticleSystemGradientMode.TwoGradients:
                    return GradientEquals(a.gradientMin, b.gradientMin) && GradientEquals(a.gradientMax, b.gradientMax);
                default:
                    return false;
            }
        }

        static bool GradientEquals(Gradient a, Gradient b)
        {
            if (a == null || b == null)
                return a == b;
            if (a.mode != b.mode)
                return false;

            var aColors = a.colorKeys;
            var bColors = b.colorKeys;
            if (aColors.Length != bColors.Length)
                return false;
            for (var i = 0; i < aColors.Length; i++)
            {
                if (!Mathf.Approximately(aColors[i].time, bColors[i].time) || !ColorEquals(aColors[i].color, bColors[i].color))
                    return false;
            }

            var aAlphas = a.alphaKeys;
            var bAlphas = b.alphaKeys;
            if (aAlphas.Length != bAlphas.Length)
                return false;
            for (var i = 0; i < aAlphas.Length; i++)
            {
                if (!Mathf.Approximately(aAlphas[i].time, bAlphas[i].time) || !Mathf.Approximately(aAlphas[i].alpha, bAlphas[i].alpha))
                    return false;
            }

            return true;
        }

        static bool ColorEquals(Color a, Color b)
        {
            const float epsilon = 0.0001f;
            return Mathf.Abs(a.r - b.r) < epsilon
                && Mathf.Abs(a.g - b.g) < epsilon
                && Mathf.Abs(a.b - b.b) < epsilon
                && Mathf.Abs(a.a - b.a) < epsilon;
        }

        static string FormatTarget(CustomInfo info, int infoIndex, int targetIndex)
        {
            var name = info != null && !string.IsNullOrWhiteSpace(info.customName)
                ? info.customName
                : $"色 {infoIndex + 1}";
            return $"{name} / {targetIndex + 1}";
        }

        readonly struct MaterialSlotKey : IEquatable<MaterialSlotKey>
        {
            readonly int _rendererId;
            readonly int _slot;

            public MaterialSlotKey(int rendererId, int slot)
            {
                _rendererId = rendererId;
                _slot = slot;
            }

            public bool Equals(MaterialSlotKey other)
            {
                return _rendererId == other._rendererId && _slot == other._slot;
            }

            public override bool Equals(object obj)
            {
                return obj is MaterialSlotKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (_rendererId * 397) ^ _slot;
            }
        }

#if UNITY_EDITOR
        struct PreviewSlot
        {
            public Renderer renderer;
            public int materialSlot;
            public string propertyName;
            public bool hadColor;
            public Color originalColor;
            public bool blockWasEmpty;
        }

        struct ParticlePreview
        {
            public ParticleSystem particle;
            public ParticleColorChannel channel;
            public ParticleSystem.MinMaxGradient original;
            public ParticleSystem.MinMaxGradient written;
        }

        [NonSerialized] List<PreviewSlot> _previewSlots;
        [NonSerialized] List<ParticlePreview> _particlePreviews;
        [NonSerialized] bool _previewQueued;
        [NonSerialized] bool _previewSuspendedForSave;

        void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            PrefabStage.prefabSaving -= OnPrefabSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
            PrefabStage.prefabSaved -= OnPrefabSaved;
            PrefabStage.prefabSaved += OnPrefabSaved;
            SchedulePreview();
        }

        void OnValidate()
        {
            SchedulePreview();
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            PrefabStage.prefabSaving -= OnPrefabSaving;
            PrefabStage.prefabSaved -= OnPrefabSaved;
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ClearMaterialPreview();
            ClearParticlePreview();
        }

        // パーティクルのプレビュー色はシーン／プレハブへ保存しない。保存の直前に元へ戻し、直後に掛け直す。
        void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (this == null || gameObject.scene != scene)
                return;
            SuspendPreviewForSave();
        }

        void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            if (this == null || gameObject.scene != scene)
                return;
            ResumePreviewAfterSave();
        }

        void OnPrefabSaving(GameObject prefabRoot)
        {
            if (this == null || prefabRoot == null || !transform.IsChildOf(prefabRoot.transform))
                return;
            SuspendPreviewForSave();
        }

        void OnPrefabSaved(GameObject prefabRoot)
        {
            if (this == null || prefabRoot == null || !transform.IsChildOf(prefabRoot.transform))
                return;
            ResumePreviewAfterSave();
        }

        void SuspendPreviewForSave()
        {
            if (_particlePreviews == null || _particlePreviews.Count == 0)
                return;
            ClearParticlePreview();
            _previewSuspendedForSave = true;
        }

        void ResumePreviewAfterSave()
        {
            if (!_previewSuspendedForSave)
                return;
            _previewSuspendedForSave = false;
            SchedulePreview();
        }

        void OnUndoRedo()
        {
            SchedulePreview();
        }

        void SchedulePreview()
        {
            if (_previewQueued)
                return;

            _previewQueued = true;
            EditorApplication.delayCall += DeferredPreview;
        }

        void DeferredPreview()
        {
            _previewQueued = false;
            if (this == null || !isActiveAndEnabled)
                return;
            if (!gameObject.scene.IsValid())
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                SchedulePreview();
                return;
            }

            ApplyPreview();
        }

        /// <summary>
        /// プレビューで書き込む前の MaterialPropertyBlock の状態を、スロット・プロパティごとに初回だけ記録する。
        /// </summary>
        void TrackPreviewSlot(Renderer renderer, int materialSlot, string propertyName, MaterialPropertyBlock blockBeforeWrite)
        {
            _previewSlots ??= new List<PreviewSlot>();
            for (var i = 0; i < _previewSlots.Count; i++)
            {
                var existing = _previewSlots[i];
                if (existing.renderer == renderer
                    && existing.materialSlot == materialSlot
                    && existing.propertyName == propertyName)
                    return;
            }

            var hadColor = blockBeforeWrite.HasColor(propertyName);
            _previewSlots.Add(new PreviewSlot
            {
                renderer = renderer,
                materialSlot = materialSlot,
                propertyName = propertyName,
                hadColor = hadColor,
                originalColor = hadColor ? blockBeforeWrite.GetColor(propertyName) : default,
                blockWasEmpty = blockBeforeWrite.isEmpty,
            });
        }

        /// <summary>
        /// このコンポーネントが書いたプロパティだけを元に戻す。
        /// MaterialPropertyBlock は 1 プロパティだけ消す API が無いため、
        /// 書き込み前にブロックが空だったときだけ Clear し、他ツールの値が既にあったときは
        /// 元の値（無ければマテリアル自身の値）を書き戻して他の値を残す。
        /// </summary>
        void ClearMaterialPreview()
        {
            if (_previewSlots == null)
                return;

            for (var i = _previewSlots.Count - 1; i >= 0; i--)
            {
                var slot = _previewSlots[i];
                if (slot.renderer == null)
                    continue;

                var materials = slot.renderer.sharedMaterials;
                if (materials == null || slot.materialSlot < 0 || slot.materialSlot >= materials.Length)
                    continue;

                var block = new MaterialPropertyBlock();
                slot.renderer.GetPropertyBlock(block, slot.materialSlot);
                if (slot.hadColor)
                {
                    block.SetColor(slot.propertyName, slot.originalColor);
                }
                else if (slot.blockWasEmpty)
                {
                    block.Clear();
                }
                else
                {
                    var material = materials[slot.materialSlot];
                    if (material == null || !material.HasColor(slot.propertyName))
                        continue;
                    block.SetColor(slot.propertyName, material.GetColor(slot.propertyName));
                }

                slot.renderer.SetPropertyBlock(block, slot.materialSlot);
            }

            _previewSlots.Clear();
        }

        void TrackParticlePreview(
            ParticleSystem particle,
            ParticleColorChannel channel,
            ParticleSystem.MinMaxGradient original,
            ParticleSystem.MinMaxGradient written)
        {
            _particlePreviews ??= new List<ParticlePreview>();
            for (var i = 0; i < _particlePreviews.Count; i++)
            {
                var existing = _particlePreviews[i];
                if (existing.particle != particle || existing.channel != channel)
                    continue;

                existing.written = written;
                _particlePreviews[i] = existing;
                return;
            }

            _particlePreviews.Add(new ParticlePreview
            {
                particle = particle,
                channel = channel,
                original = original,
                written = written,
            });
        }

        void ClearParticlePreview()
        {
            if (_particlePreviews == null)
                return;

            for (var i = _particlePreviews.Count - 1; i >= 0; i--)
            {
                var preview = _particlePreviews[i];
                if (preview.particle == null)
                    continue;

                // プレビュー後にユーザーが手で変えた色は上書きしない。
                var current = GetParticleGradient(preview.particle, preview.channel);
                if (!Approximately(current, preview.written))
                    continue;

                SetParticleGradient(preview.particle, preview.channel, preview.original);
            }

            _particlePreviews.Clear();
        }
#endif
    }
}
