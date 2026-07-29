using System;
using UnityEngine;
using VRC.SDKBase;

namespace Samirin33.NDMF.Base
{
    public class SamirinMABaseSingle : SamirinMABase
    {
        public virtual void OnBuildSingle(SamirinBuildPhase buildPhase, bool beforeModularAvatar, SamirinMABaseSingle[] _MAScripts, GameObject avatarRootObject, Action<GameObject, SamirinMABaseSingle[]> invokeBuilder, Action<GameObject, SamirinMABaseSingle[]> invokeReplaceBuilder) { }
    }
}