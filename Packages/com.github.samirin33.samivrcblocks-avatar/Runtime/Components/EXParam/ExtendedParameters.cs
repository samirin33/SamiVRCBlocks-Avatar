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

        /// <summary>
        /// アバター直下に同じプレファブが置かれているか。
        /// プレファブ接続が残っていればその元アセットで判定する（Apply on Play など）。
        /// アップロードや Manual Bake ではアバターが Instantiate で複製され接続が切れるため、
        /// 名前の完全一致か Unity の複製名「名前 (n)」だけを同じものとみなす。
        /// </summary>
        private static bool ContainsParameterPrefabInstance(GameObject avatarObject, GameObject prefab)
        {
            var prefabName = prefab.name;
            foreach (Transform child in avatarObject.transform)
            {
#if UNITY_EDITOR
                var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (source != null)
                {
                    // Variant を経由していても、元のどこかで同じプレファブなら配置済み。
                    var matched = false;
                    for (var s = source; s != null; s = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(s))
                    {
                        if (s == prefab)
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (matched)
                        return true;
                    continue;
                }
#endif
                if (IsSameOrDuplicateName(child.name, prefabName))
                    return true;
            }
            return false;
        }

        private static bool IsSameOrDuplicateName(string name, string prefabName)
        {
            if (name == prefabName)
                return true;

            var prefix = prefabName + " (";
            if (!name.StartsWith(prefix, System.StringComparison.Ordinal) || !name.EndsWith(")", System.StringComparison.Ordinal))
                return false;

            var digits = name.Length - prefix.Length - 1;
            if (digits <= 0)
                return false;
            for (var i = prefix.Length; i < name.Length - 1; i++)
            {
                if (!char.IsDigit(name[i]))
                    return false;
            }
            return true;
        }
    }
}
