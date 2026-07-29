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
                    var parentScale = gameObject.transform.parent.lossyScale;
                    var x = resetScaleValue.x / parentScale.x;
                    var y = resetScaleValue.y / parentScale.y;
                    var z = resetScaleValue.z / parentScale.z;
                    gameObject.transform.localScale = new Vector3(x, y, z);
                }
            }
            if (destroyOnReset)
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
