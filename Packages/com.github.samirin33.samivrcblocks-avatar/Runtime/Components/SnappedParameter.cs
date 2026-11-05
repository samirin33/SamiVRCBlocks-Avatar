using System;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// 指定した Float パラメーターを任意範囲で n 分割し、分解能にスナップした値とスムージング後の値を出力する Animator を生成する。
    /// 同期 Bit 分解は行わない（ResizableSyncParameters のスナップ部分のみ）。
    /// Resolving（MA の前）で Animator を生成し、Optimizing（MA の前）で必要なら参照置換してコンポーネントを削除する。
    /// </summary>
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB SnappedParameter")]
    public class SnappedParameter : SamirinMABaseSingle
    {
        public SnappedParameter()
        {
            // priority は NonSerialized のため Reset ではビルド時に残らない。
            priority = 50;
        }

        private void Reset()
        {
            matchAvatarWriteDefaults = true;
        }

        [Serializable]
        public class SnapSetting
        {
            public string paramName;

            public FloatRangePreset floatRangePreset = FloatRangePreset.ZeroToPlusOne;
            public float customFloatMin;
            public float customFloatMax = 1f;

            /// <summary>
            /// 範囲を何等分するか。出力の段階数は divisions+1（端点を含む）。
            /// </summary>
            public int divisions = 16;

            public float smoothWeight = 0.2f;
        }

        public enum FloatRangePreset
        {
            [InspectorName("-1~1")]
            MinusOneToPlusOne,
            [InspectorName("0~1")]
            ZeroToPlusOne,
            [InspectorName("カスタム")]
            Custom,
        }

        public const int MinDivisions = 1;
        public const int MaxDivisions = 256;

        public SnapSetting[] snapSettings;

        public bool writeDefault;

        /// <summary>
        /// true の場合、生成する MA Merge Animator の Match Avatar Write Defaults を有効にする。
        /// </summary>
        public bool matchAvatarWriteDefaults;

        /// <summary>
        /// true の場合、ビルド時に親 Animator 内の Float パラメーター参照を _SnapSmoothed に付け替える。
        /// </summary>
        public bool replaceWithSnapSmoothedInAnimator = true;

        public static int GetDivisions(SnapSetting setting)
        {
            if (setting == null)
                return MinDivisions;
            return Mathf.Clamp(setting.divisions, MinDivisions, MaxDivisions);
        }

        /// <summary>
        /// スナップ用 Int の最大値（0..max）。分割数そのもの。
        /// </summary>
        public static int GetMaxSnapValue(SnapSetting setting)
            => GetDivisions(setting);

        public static string GetParamName(SnapSetting setting)
        {
            if (setting == null || string.IsNullOrEmpty(setting.paramName))
                return "Param";
            return setting.paramName;
        }

        public static string GetSnappedParamName(string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
                paramName = "Param";
            return $"{paramName}_Snapped";
        }

        public static string GetSnapSmoothedParamName(string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
                paramName = "Param";
            return $"{paramName}_SnapSmoothed";
        }

        public static string GetSnapIntParamName(string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
                paramName = "Param";
            return $"{paramName}_SnapInt";
        }

        public static (float min, float max) GetSourceRange(SnapSetting setting)
        {
            if (setting == null)
                return (0f, 1f);

            switch (setting.floatRangePreset)
            {
                case FloatRangePreset.MinusOneToPlusOne:
                    return (-1f, 1f);
                case FloatRangePreset.ZeroToPlusOne:
                    return (0f, 1f);
                case FloatRangePreset.Custom:
                    return GetFloatCustomRange(setting.customFloatMin, setting.customFloatMax);
                default:
                    return (0f, 1f);
            }
        }

        public static (float min, float max) GetFloatCustomRange(float min, float max)
        {
            if (min >= max)
                max = min + 0.0001f;
            return (min, max);
        }

        public override void OnBuildSingle(SamirinBuildPhase buildPhase, bool beforeModularAvatar, SamirinMABaseSingle[] _MAScripts, GameObject avatarRootObject, Action<GameObject, SamirinMABaseSingle[]> invokeBuilder, Action<GameObject, SamirinMABaseSingle[]> invokeReplaceBuilder)
        {
            if (buildPhase == SamirinBuildPhase.Resolving && beforeModularAvatar)
            {
                invokeBuilder(avatarRootObject, _MAScripts);
            }

            if (buildPhase == SamirinBuildPhase.Optimizing && beforeModularAvatar)
            {
                invokeReplaceBuilder?.Invoke(avatarRootObject, _MAScripts);

                // 同一タイプは OnBuildSingle が代表1件にしか来ないため、渡された全インスタンスを破棄する
                if (_MAScripts == null) return;
                foreach (var script in _MAScripts)
                {
                    if (script != null)
                        DestroyImmediate(script);
                }
            }
        }
    }
}
