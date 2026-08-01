using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;
using nadena.dev.ndmf;
using Samirin33.NDMF.Base.Plugin;
using Samirin33.NDMF.Components;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// HalfSyncParam の指定 bit 数を MA Information の同期パラメーター使用量として報告する。
    /// ビルド時に登録される Bool（SUM/HalfParam/...）と同じ名前・型で提供し、二重カウントを避ける。
    /// </summary>
    [ParameterProviderFor(typeof(HalfSyncParam))]
    internal class HalfSyncParamParameterProvider : IParameterProvider
    {
        private readonly HalfSyncParam _component;

        public HalfSyncParamParameterProvider(HalfSyncParam component)
        {
            _component = component;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context = null)
        {
            if (_component == null || _component.syncParamSettings == null)
                yield break;

            var seenNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var setting in _component.syncParamSettings)
            {
                if (setting == null) continue;

                var bitCount = HalfSyncParam.GetBitCount(setting);
                if (bitCount < 1) continue;

                var paramName = HalfSyncParam.GetParamName(setting);
                if (!seenNames.Add(paramName))
                    continue;

                for (var i = 0; i < bitCount; i++)
                {
                    var syncParamName = HalfSyncParam.GetSyncBoolParamName(paramName, i);
                    yield return new ProvidedParameter(
                        syncParamName,
                        ParameterNamespace.Animator,
                        _component,
                        SamirinMABasePlugin.Instance,
                        AnimatorControllerParameterType.Bool)
                    {
                        WantSynced = true,
                        IsAnimatorOnly = false,
                        DefaultValue = 0f,
                    };
                }
            }
        }

        public void RemapParameters(
            ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap,
            BuildContext context = null)
        {
            // no-op
        }
    }
}
