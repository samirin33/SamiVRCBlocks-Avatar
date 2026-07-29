using System;
using System.Collections.Generic;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Base.Plugin
{
    /// <summary>
    /// SamirinMABaseSingle を継承したクラス用のビルダーを型ごとに登録・実行するレジストリ。
    /// 新規に Single 用クラスを追加する場合は、対応する Builder 側で Register を呼び出してください。
    /// </summary>
    public static class SamirinMABaseSingleBuildRegistry
    {
        private static readonly Dictionary<Type, Action<GameObject, SamirinMABaseSingle[]>> Builders = new Dictionary<Type, Action<GameObject, SamirinMABaseSingle[]>>();
        private static readonly Dictionary<Type, Action<GameObject, SamirinMABaseSingle[]>> ReplaceHandlers = new Dictionary<Type, Action<GameObject, SamirinMABaseSingle[]>>();

        /// <summary>
        /// 指定した型用のビルダーを登録する。新規 Single クラス実装時に Builder から呼ぶ。
        /// </summary>
        public static void Register<T>(Action<GameObject, T[]> builder) where T : SamirinMABaseSingle
        {
            var type = typeof(T);
            Builders[type] = (root, scripts) =>
            {
                var cast = new T[scripts.Length];
                for (var i = 0; i < scripts.Length; i++)
                    cast[i] = (T)scripts[i];
                builder(root, cast);
            };
        }

        /// <summary>
        /// 指定した型用の置換処理（例: Generating 後などで実行）を登録する。
        /// </summary>
        public static void RegisterReplace<T>(Action<GameObject, T[]> replaceHandler) where T : SamirinMABaseSingle
        {
            var type = typeof(T);
            ReplaceHandlers[type] = (root, scripts) =>
            {
                var cast = new T[scripts.Length];
                for (var i = 0; i < scripts.Length; i++)
                    cast[i] = (T)scripts[i];
                replaceHandler(root, cast);
            };
        }

        /// <summary>
        /// 登録済みビルダーを scriptType に応じて実行する。未登録の型の場合は何もしない。
        /// </summary>
        public static void Invoke(Type scriptType, GameObject root, SamirinMABaseSingle[] scripts)
        {
            if (scripts == null || scripts.Length == 0) return;
            if (!Builders.TryGetValue(scriptType, out var build)) return;
            build(root, scripts);
        }

        /// <summary>
        /// 登録済み置換処理を scriptType に応じて実行する。未登録の型の場合は何もしない。
        /// </summary>
        public static void InvokeReplace(Type scriptType, GameObject root, SamirinMABaseSingle[] scripts)
        {
            if (scripts == null || scripts.Length == 0) return;
            if (!ReplaceHandlers.TryGetValue(scriptType, out var replace)) return;
            replace(root, scripts);
        }
    }
}
