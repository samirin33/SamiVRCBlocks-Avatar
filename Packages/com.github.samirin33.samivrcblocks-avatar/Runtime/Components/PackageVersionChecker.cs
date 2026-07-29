using System;
using System.Collections.Generic;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// シーン配置時に、指定パッケージ／SDK の要求バージョンとプロジェクト内の実バージョンを照合する。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB PackageVersionChecker")]
    public class PackageVersionChecker : SamirinMABase
    {
        [Serializable]
        public class Requirement
        {
            [Tooltip("パッケージ ID（例: com.vrchat.avatars）")]
            public string packageId = "";

            [Tooltip("このギミックが必要とする最低バージョン（例: 3.10.0）")]
            public string minVersion = "";

            [Tooltip("ダイアログ表示用の任意ラベル（空なら packageId を表示）")]
            public string displayName = "";
        }

        [Tooltip("照合するパッケージ／SDK と最低バージョンの一覧")]
        public List<Requirement> requirements = new List<Requirement>();
    }
}
