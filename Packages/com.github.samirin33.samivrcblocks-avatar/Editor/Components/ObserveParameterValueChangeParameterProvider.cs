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
    /// ObserveParameterValueChange が生成するローカルトリガーを Animator パラメーターとして報告する。
    /// </summary>
    [ParameterProviderFor(typeof(ObserveParameterValueChange))]
    internal class ObserveParameterValueChangeParameterProvider : IParameterProvider
    {
        private readonly ObserveParameterValueChange _component;

        public ObserveParameterValueChangeParameterProvider(ObserveParameterValueChange component)
        {
            _component = component;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context = null)
        {
            if (_component == null || _component.observeSettings == null)
                yield break;

            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var setting in _component.observeSettings)
            {
                if (setting == null)
                    continue;

                var paramName = ObserveParameterValueChange.GetParamName(setting);
                if (!seenNames.Add(paramName))
                    continue;

                var isBool = setting.paramType == ObserveParameterValueChange.ParamType.Bool;
                if (!isBool && !ObserveParameterValueChange.TryGetQuantizeMapping(setting, out _))
                    continue;

                yield return new ProvidedParameter(
                    ObserveParameterValueChange.GetValueChangedTriggerName(paramName),
                    ParameterNamespace.Animator,
                    _component,
                    SamirinMABasePlugin.Instance,
                    AnimatorControllerParameterType.Trigger)
                {
                    WantSynced = false,
                    IsAnimatorOnly = true,
                    DefaultValue = 0f,
                };
            }
        }

        public void RemapParameters(
            ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap,
            BuildContext context = null)
        {
        }
    }
}
