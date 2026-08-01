using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// エディタ上でシーンに配置されたとき、アバターの手の先端・甲方向に合わせて回転を補正する。
    /// TipVector / UpVector で指定したローカル軸が、それぞれ指先方向・手の甲方向を向く。
    /// 補正処理は Editor アセンブリ側で行い、ビルド時には自身を削除する。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB FixHandVector")]
    public class FixHandVector : SamirinMABase
    {
        public enum HandType
        {
            Left,
            Right,
        }

        [Tooltip("補正に使う手（半身）")]
        public HandType handType = HandType.Right;

        [Tooltip("手の先端方向に合わせるローカル軸")]
        public Vector3 TipVector = Vector3.forward;

        [Tooltip("手の甲方向に合わせるローカル軸")]
        public Vector3 UpVector = Vector3.up;

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar)
                return;

            DestroyImmediate(this);
        }
    }
}
