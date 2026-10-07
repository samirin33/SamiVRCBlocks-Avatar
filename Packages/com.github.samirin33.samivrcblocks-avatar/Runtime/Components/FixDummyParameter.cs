using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// 1フレーム即時遷移用のダミー Bool について、同じオブジェクトの MA Merge Animator が
    /// 統合する Animator の遷移条件を、マージ先（アバター側）の true / false に揃える。
    /// 名前の表記は作者ごとに異なるため、対象パラメーター名はここで指定する。
    /// このパッケージが生成する Animator も、同じ名前か Dummy を含む Bool を統合前に揃える。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB FixDummyParameter")]
    public class FixDummyParameter : SamirinMABase
    {
        [Tooltip("1フレーム即時遷移に使うダミー Bool のパラメーター名。大文字小文字は区別されます。")]
        public string[] dummyParameterNames = new string[0];
    }
}
