using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB GameObjectResetter")]
    public class GameObjectResetter : SamirinMABase
    {
        public bool objectEnable = false;
        public bool resetObjectEnable = false;
        public bool resetPosition = false;
        public Vector3 resetPositionValue = Vector3.zero;
        public bool isLocalPosition = true;
        public bool resetRotation = false;
        public Vector3 resetRotationValue = Vector3.zero;
        public bool isLocalRotation = true;
        public bool resetScale = false;
        public Vector3 resetScaleValue = Vector3.one;
        public bool isLocalScale = true;

        public bool destroyOnReset = false;

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase == SamirinBuildPhase.Transforming && !beforeModularAvatar)
            {
                if (objectEnable)
                {
                    gameObject.SetActive(resetObjectEnable);
                }

                DestroyImmediate(this);
            }
            else if (buildPhase == SamirinBuildPhase.Resolving && beforeModularAvatar)
            {
                OnBuildResolvingBeforeMA(avatarRootObject);
            }
        }

        private void OnBuildResolvingBeforeMA(GameObject avatarRootObject)
        {
            if (resetPosition)
            {
                if (isLocalPosition) gameObject.transform.localPosition = resetPositionValue;
                else gameObject.transform.position = resetPositionValue;
            }
            if (resetRotation)
            {
                if (isLocalRotation) gameObject.transform.localEulerAngles = resetRotationValue;
                else gameObject.transform.eulerAngles = resetRotationValue;
            }
            if (resetScale)
            {
                if (isLocalScale) gameObject.transform.localScale = resetScaleValue;
                else
                {
                    var parent = gameObject.transform.parent;
                    var parentScale = parent != null ? parent.lossyScale : Vector3.one;
                    gameObject.transform.localScale = new Vector3(
                        DivideScale(resetScaleValue.x, parentScale.x),
                        DivideScale(resetScaleValue.y, parentScale.y),
                        DivideScale(resetScaleValue.z, parentScale.z));
                }
            }
            if (destroyOnReset)
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        // 親スケールが 0 の軸は割ると NaN/Infinity になるため、目標値をそのまま使う。
        private static float DivideScale(float value, float parentScale)
        {
            return Mathf.Abs(parentScale) > 1e-8f ? value / parentScale : value;
        }
    }
}
