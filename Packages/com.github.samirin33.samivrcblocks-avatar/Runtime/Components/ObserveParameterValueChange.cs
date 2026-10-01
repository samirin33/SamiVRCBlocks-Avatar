using System;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// 指定した Animator パラメーターが閾値間隔をまたいで変化したフレームに、
    /// (パラメーター名)_ValueChanged トリガーを有効化する Animator を生成する。
    /// </summary>
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB ObserveParameterValueChange")]
    public class ObserveParameterValueChange : SamirinMABaseSingle
    {
        public ObserveParameterValueChange()
        {
            priority = 50;
        }

        private void Reset()
        {
            priority = 50;
            matchAvatarWriteDefaults = true;
        }

        public const int MaxBucketCount = 256;
        public const float MinFloatThreshold = 0.0001f;

        [Serializable]
        public class ObserveSetting
        {
            public string paramName;
            public ParamType paramType = ParamType.Float;

            /// <summary>
            /// この間隔をまたいだときだけ変化とみなす。Float はパラメーター単位、Int は整数段。
            /// </summary>
            public float threshold = 0.1f;

            public FloatRangePreset floatRangePreset = FloatRangePreset.ZeroToPlusOne;
            public float customFloatMin;
            public float customFloatMax = 1f;

            public int intMin;
            public int intMax = 1;
        }

        public enum ParamType
        {
            Int,
            Float,
            Bool,
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

        public struct BucketInfo
        {
            public int bucketCount;
            public float effectiveThreshold;
            public bool thresholdClamped;
            public float rangeMin;
            public float rangeMax;
        }

        public struct QuantizeMapping
        {
            public int lastIndex;
            public float sourceMin;
            public float sourceMax;
            public float destMin;
            public float destMax;
        }

        public ObserveSetting[] observeSettings;
        public bool writeDefault;

        /// <summary>
        /// true の場合、生成する MA Merge Animator の Match Avatar Write Defaults を有効にする。
        /// </summary>
        public bool matchAvatarWriteDefaults = true;

        public static string GetParamName(ObserveSetting setting)
        {
            if (setting == null || string.IsNullOrEmpty(setting.paramName))
                return "Param";
            return setting.paramName;
        }

        public static string GetValueChangedTriggerName(string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
                paramName = "Param";
            return $"{paramName}_ValueChanged";
        }

        public static string GetValueChangedTriggerName(ObserveSetting setting)
            => GetValueChangedTriggerName(GetParamName(setting));

        /// <summary>
        /// 量子化結果を保持する内部 Int。トリガー判定はこの段番号で行う。
        /// </summary>
        public static string GetStepParameterName(string paramName)
            => $"ObserveValueChange/{paramName}_Step";

        public static bool TryGetBucketInfo(ObserveSetting setting, out BucketInfo info)
        {
            info = default;
            if (setting == null)
                return false;

            if (setting.paramType == ParamType.Bool)
            {
                info.bucketCount = 2;
                info.effectiveThreshold = 1f;
                info.rangeMin = 0f;
                info.rangeMax = 1f;
                return true;
            }

            var (min, max) = GetSourceRange(setting);
            info.rangeMin = min;
            info.rangeMax = max;
            var span = max - min;
            if (span <= 0f)
            {
                info.bucketCount = 1;
                info.effectiveThreshold = setting.paramType == ParamType.Int ? 1f : MinFloatThreshold;
                return true;
            }

            if (setting.paramType == ParamType.Int)
            {
                var requested = Mathf.Max(1, Mathf.RoundToInt(setting.threshold));
                var raw = FloorDivPlusOne(span, requested);
                var step = requested;
                if (raw > MaxBucketCount)
                {
                    step = Mathf.Max(1, Mathf.CeilToInt(span / (MaxBucketCount - 1f)));
                    raw = Mathf.Min(MaxBucketCount, FloorDivPlusOne(span, step));
                    info.thresholdClamped = true;
                }

                info.bucketCount = Mathf.Max(1, raw);
                info.effectiveThreshold = step;
                return true;
            }

            var threshold = Mathf.Max(MinFloatThreshold, setting.threshold);
            var rawCount = Mathf.Max(1, Mathf.CeilToInt(span / threshold - 0.00001f));
            if (rawCount > MaxBucketCount)
            {
                rawCount = MaxBucketCount;
                threshold = span / MaxBucketCount;
                info.thresholdClamped = true;
            }

            info.bucketCount = rawCount;
            info.effectiveThreshold = threshold;
            return true;
        }

        /// <summary>
        /// Float / Int を 0..lastIndex の段番号へ写す Parameter Driver の変換範囲。
        /// 終端はクランプで最終段に含まれる。
        /// </summary>
        public static bool TryGetQuantizeMapping(ObserveSetting setting, out QuantizeMapping mapping)
        {
            mapping = default;
            if (setting == null || setting.paramType == ParamType.Bool)
                return false;
            if (!TryGetBucketInfo(setting, out var info) || info.bucketCount < 2)
                return false;

            var lastIndex = info.bucketCount - 1;
            mapping.lastIndex = lastIndex;
            mapping.sourceMin = info.rangeMin;
            mapping.sourceMax = info.rangeMin + info.effectiveThreshold * lastIndex;
            if (mapping.sourceMax <= mapping.sourceMin)
                mapping.sourceMax = mapping.sourceMin + Mathf.Max(info.effectiveThreshold, MinFloatThreshold);
            mapping.destMin = 0f;
            mapping.destMax = lastIndex;
            return true;
        }

        public static (float min, float max) GetSourceRange(ObserveSetting setting)
        {
            if (setting == null)
                return (0f, 1f);

            if (setting.paramType == ParamType.Int)
            {
                var min = setting.intMin;
                var max = setting.intMax;
                if (max < min)
                    max = min;
                return (min, max);
            }

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
                invokeBuilder(avatarRootObject, _MAScripts);

            if (buildPhase == SamirinBuildPhase.Optimizing && beforeModularAvatar)
            {
                if (_MAScripts == null) return;
                foreach (var script in _MAScripts)
                {
                    if (script != null)
                        DestroyImmediate(script);
                }
            }
        }

        private static int FloorDivPlusOne(float span, int step)
        {
            if (step < 1)
                step = 1;
            var count = (long)Math.Floor(span / step) + 1L;
            if (count > int.MaxValue)
                return int.MaxValue;
            if (count < 1)
                return 1;
            return (int)count;
        }
    }
}
