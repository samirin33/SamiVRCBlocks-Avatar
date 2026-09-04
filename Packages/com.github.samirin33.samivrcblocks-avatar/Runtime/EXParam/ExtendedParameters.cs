using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Module
{
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB ExtendedParameters"), DisallowMultipleComponent]
    public class ExtendedParameters : SamirinMABase
    {
        public ExtendedParameters()
        {
            // priority は NonSerialized のため Reset ではビルド時に残らない。
            // コンストラクタで ParameterSmoothing 等（default 100）より先に展開する。
            priority = 10;
        }

        void Reset()
        {
            priority = 10;
        }

        [FormerlySerializedAs("modulePrefabs")]
        public GameObject[] parameterPrefabs;

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            // Resolving で配置し、プレファブ内の SamirinMABase が同フェーズで処理されるようにする
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar) return;

            var avatarObject = avatarRootObject;
            if (parameterPrefabs != null)
            {
                foreach (var prefab in parameterPrefabs)
                {
                    if (prefab == null) continue;
                    if (!ContainsParameterPrefabInstance(avatarObject, prefab))
                    {
                        var instance = Object.Instantiate(prefab, avatarObject.transform);
                        instance.name = prefab.name;
                    }
                }
            }

            DestroyImmediate(this);
        }

        private static bool ContainsParameterPrefabInstance(GameObject avatarObject, GameObject prefab)
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
