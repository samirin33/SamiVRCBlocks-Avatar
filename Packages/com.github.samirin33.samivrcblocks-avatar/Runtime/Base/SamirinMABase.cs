using UnityEngine;
using VRC.SDKBase;

namespace Samirin33.NDMF.Base
{
    public class SamirinMABase : MonoBehaviour, IEditorOnly
    {
        [System.NonSerialized]
        public int priority = 100;
        public virtual void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject) { }
    }
}