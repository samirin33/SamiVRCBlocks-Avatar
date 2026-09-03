using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Samirin33.NDMF.Base;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// シーン配置時に、指定パッケージ／SDK の要求バージョンとプロジェクト内の実バージョンを照合する。
    /// 照合処理は Editor アセンブリ側で行い、ビルド時には自身を削除する。
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

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase != SamirinBuildPhase.Resolving || !beforeModularAvatar)
                return;

            DestroyImmediate(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // プレイ中は不要（ビルド処理も含めてランタイムで動かない）
            if (Application.isPlaying)
                return;

            // プレハブアセットや非永続オブジェクトでは確認しない
            if (EditorUtility.IsPersistent(gameObject))
                return;

            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
                return;

            // Editor 専用サービスを、ランタイム側から直接参照せず反射で呼ぶ
            const string serviceTypeName = "Samirin33.NDMF.Components.Editor.PackageVersionCheckerService";
            Type serviceType = null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                serviceType = assemblies[i].GetType(serviceTypeName);
                if (serviceType != null)
                    break;
            }

            if (serviceType == null)
                return;

            var scheduleMethod = serviceType.GetMethod(
                "ScheduleCheck",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(PackageVersionChecker), typeof(bool) },
                null);
            if (scheduleMethod == null)
                return;

            // シーン配置・値変更時は即座に（ただしサービス側でデバウンス/セッション抑制あり）
            scheduleMethod.Invoke(null, new object[] { this, true });
        }
#endif
    }
}
