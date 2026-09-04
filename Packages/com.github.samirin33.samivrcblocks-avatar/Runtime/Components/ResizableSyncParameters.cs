using System;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB ResizableSyncParameters")]
    public class ResizableSyncParameters : SamirinMABaseSingle
    {
        private void Reset()
        {
            priority = 50;
        }

        [System.Serializable]
        public class SyncParamSetting
        {
            public string paramName;
            public ParamType paramType;
            public BitType bitType;
            public int customBitCount = 8;

            public IntRangePreset intRangePreset = IntRangePreset.FromZero;
            public FloatRangePreset floatRangePreset = FloatRangePreset.ZeroToPlusOne;
            public int customIntMin;
            public float customFloatMin;
            public float customFloatMax = 1f;

            public DivisionType divisionType = DivisionType.Even;
            public float smoothWeight = 0.2f;
        }

        public enum ParamType
        {
            Int,
            Float,
        }

        public enum IntRangePreset
        {
            [InspectorName("0~2^n")]
            FromZero,
            [InspectorName("カスタム")]
            Custom,
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

        public enum DivisionType
        {
            /// <summary>
            /// 同期 Int は 0..(2^bit-2) を使用。分解能は (max-min)/(2^bit-2)。
            /// 例: 0~1・2bit → Int 0~2、分解能 1/2。
            /// </summary>
            [InspectorName("奇数分割")]
            Odd,
            /// <summary>
            /// 同期 Int は 0..(2^bit-1) を使用。分解能は (max-min)/(2^bit-1)。
            /// 例: 0~1・2bit → Int 0~3、分解能 1/3。
            /// </summary>
            [InspectorName("偶数分割")]
            Even,
        }

        public enum BitType
        {
            _1bit, //1bitで0-1の値を送信
            _2bit, //2bitで0-3の値を送信
            _3bit, //3bitで0-7の値を送信
            _4bit, //4bitで0-15の値を送信
            _5bit, //5bitで0-31の値を送信
            _6bit, //6bitで0-63の値を送信
            _7bit, //7bitで0-127の値を送信
            [InspectorName("カスタム")]
            Custom,
        }

        public const int MinCustomBitCount = 1;
        public const int MaxCustomBitCount = 16;

        public static int GetBitCount(SyncParamSetting setting)
        {
            if (setting == null) return MinCustomBitCount;
            if (setting.bitType == BitType.Custom)
                return Mathf.Clamp(setting.customBitCount, MinCustomBitCount, MaxCustomBitCount);

            switch (setting.bitType)
            {
                case BitType._1bit: return 1;
                case BitType._2bit: return 2;
                case BitType._3bit: return 3;
                case BitType._4bit: return 4;
                case BitType._5bit: return 5;
                case BitType._6bit: return 6;
                case BitType._7bit: return 7;
                default: return 1;
            }
        }

        public static string GetParamName(SyncParamSetting setting)
        {
            if (setting == null) return "Param";
            return string.IsNullOrEmpty(setting.paramName)
                ? $"Param_{setting.paramType}{setting.bitType}"
                : setting.paramName;
        }

        /// <summary>
        /// ビルド時に MA Parameters へ登録する同期用 Bool 名（1 bit = 1 Bool）。
        /// </summary>
        public static string GetSyncBoolParamName(string paramName, int bitIndex)
        {
            return $"SUM/ResizableSync/{paramName}_Int/{bitIndex}";
        }

        public static int GetIntRangeSpan(SyncParamSetting setting)
        {
            return 1 << GetBitCount(setting);
        }

        /// <summary>
        /// Float 同期で使う Int の最大値（0..max）。
        /// 偶数分割: 2^bit-1 / 奇数分割: 2^bit-2（1bit 時は最低 1）。
        /// Int 型は常に 2^bit-1。
        /// </summary>
        public static int GetMaxSyncValue(SyncParamSetting setting)
        {
            var fullMax = GetIntRangeSpan(setting) - 1;
            if (setting == null || setting.paramType != ParamType.Float)
                return fullMax;
            if (setting.divisionType == DivisionType.Odd)
                return Mathf.Max(1, fullMax - 1); // 2^bit - 2
            return fullMax; // 2^bit - 1
        }

        public SyncParamSetting[] syncParamSettings;

        public bool writeDefault = false;

        /// <summary> true の場合、ビルド時に親 Animator 内の Float パラメータ参照をすべて _Smoothed に置換する。 </summary>
        public bool replaceWithSmoothedInAnimator = true;

        public override void OnBuildSingle(SamirinBuildPhase buildPhase, bool beforeModularAvatar, SamirinMABaseSingle[] _MAScripts, GameObject avatarRootObject, Action<GameObject, SamirinMABaseSingle[]> invokeBuilder, Action<GameObject, SamirinMABaseSingle[]> invokeReplaceBuilder)
        {
            if (buildPhase == SamirinBuildPhase.Resolving && beforeModularAvatar)
            {
                invokeBuilder(avatarRootObject, _MAScripts);
            }

            if (buildPhase == SamirinBuildPhase.Optimizing && beforeModularAvatar)
            {
                invokeReplaceBuilder?.Invoke(avatarRootObject, _MAScripts);
                DestroyImmediate(this);
            }
        }
    }
}
