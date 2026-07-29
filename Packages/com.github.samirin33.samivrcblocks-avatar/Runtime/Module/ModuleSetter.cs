using UnityEngine;
using VRC.SDKBase;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Module
{
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB ModuleSetter"), DisallowMultipleComponent]
    public class ModuleSetter : SamirinMABase
    {
        void Reset()
        {
            priority = 300;
        }
        public GameObject[] modulePrefabs;
        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Transforming || !beforeModularAvatar) return;

            var avatarObject = avatarRootObject;
            if (modulePrefabs != null)
            {
                foreach (var prefab in modulePrefabs)
                {
                    if (prefab == null) continue;
                    if (!ContainsModuleInstance(avatarObject, prefab))
                    {
                        var instance = Object.Instantiate(prefab, avatarObject.transform);
                        instance.name = prefab.name;
                    }
                }
            }

            DestroyImmediate(this);
        }

        private static bool ContainsModuleInstance(GameObject avatarObject, GameObject prefab)
        {
            var prefabName = prefab.name;
            foreach (Transform child in avatarObject.transform)
            {
                if (child.name == prefabName || child.name.StartsWith(prefabName + " "))
                    return true;
            }
            return false;
        }
    }
}