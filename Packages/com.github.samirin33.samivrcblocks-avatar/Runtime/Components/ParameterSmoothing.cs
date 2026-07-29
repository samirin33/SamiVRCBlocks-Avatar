using System;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB ParameterSmoothing")]
    public class ParameterSmoothing : SamirinMABaseSingle
    {
        [System.Serializable]
        public class ParameterSmoothingInfo
        {
            public string parameterName;
            /// <summary>
            /// true の場合は親の defaultSmoothWeight を使用する。
            /// </summary>
            public bool useDefaultSmoothWeight = true;
            public float smoothWeight;
            /// <summary>
            /// 空の場合は parameterName + "_Smoothed" を使用。
            /// HalfSyncParam 連携時は元パラメータ名の _Smoothed を指定する。
            /// </summary>
            public string smoothedParameterName;

            public float GetEffectiveSmoothWeight(float defaultWeight)
                => useDefaultSmoothWeight ? defaultWeight : smoothWeight;
        }

        /// <summary>
        /// リスト上部のデフォルト重み。useDefaultSmoothWeight が true の要素で使用する。
        /// </summary>
        public float defaultSmoothWeight = 0.2f;

        public ParameterSmoothingInfo[] parameterSmoothingData;

        public override void OnBuildSingle(SamirinBuildPhase buildPhase, bool beforeModularAvatar, SamirinMABaseSingle[] _MAScripts, GameObject avatarRootObject, Action<GameObject, SamirinMABaseSingle[]> invokeBuilder, Action<GameObject, SamirinMABaseSingle[]> invokeReplaceBuilder)
        {
            if (buildPhase == SamirinBuildPhase.Resolving && beforeModularAvatar)
            {
                invokeBuilder(avatarRootObject, _MAScripts);
                DestroyImmediate(this);
            }
        }
    }
}
