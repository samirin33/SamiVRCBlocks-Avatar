using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Samirin33.NDMF.Base
{
    /// <summary>
    /// SamirinMABasePlugin が実行中の NDMF BuildContext を保持し、ビルド時処理から
    /// エラー報告・生成アセットの扱いを共通化する。
    /// ビルド外（インスペクタの手動生成ボタンなど）では Current が null になり、従来どおり Assets に保存する。
    /// </summary>
    public static class SamirinBuildContext
    {
        public static BuildContext Current { get; private set; }

        public static bool IsBuilding => Current != null;

        // NDMF の SimpleError はキーで文言を引くため、キーをそのまま文言として返す Localizer を使う。
        private static Localizer _localizer;

        private static Localizer Localizer
        {
            get
            {
                if (_localizer == null)
                {
                    Func<string, string> passthrough = key =>
                        key == null || key.EndsWith(":description", StringComparison.Ordinal) || key.EndsWith(":hint", StringComparison.Ordinal)
                            ? null
                            : key;
                    _localizer = new Localizer("en-US", () => new List<(string, Func<string, string>)>
                    {
                        ("en-US", passthrough),
                        ("ja-JP", passthrough),
                    });
                }

                return _localizer;
            }
        }

        public static IDisposable Enter(BuildContext context)
        {
            var previous = Current;
            Current = context;
            return new Scope(previous);
        }

        public static void ReportError(string message, params Object[] references)
        {
            Report(ErrorSeverity.Error, message, references);
        }

        public static void ReportWarning(string message, params Object[] references)
        {
            Report(ErrorSeverity.NonFatal, message, references);
        }

        public static void ReportInfo(string message, params Object[] references)
        {
            Report(ErrorSeverity.Information, message, references);
        }

        private static void Report(ErrorSeverity severity, string message, Object[] references)
        {
            if (!IsBuilding)
            {
                var context = references != null && references.Length > 0 ? references[0] : null;
                if (severity == ErrorSeverity.Error)
                    Debug.LogError(message, context);
                else if (severity == ErrorSeverity.NonFatal)
                    Debug.LogWarning(message, context);
                else
                    Debug.Log(message, context);
                return;
            }

            // {0} 等は SimpleError の置換対象になるため、メッセージ中の波括弧を全角に逃がす。
            var safeMessage = (message ?? string.Empty).Replace("{", "｛").Replace("}", "｝");
            ErrorReport.ReportError(Localizer, severity, safeMessage, references ?? Array.Empty<Object>());
        }

        /// <summary>
        /// ビルド中はメモリ上に、ビルド外では manualAssetPath に AnimatorController を作る。
        /// ビルド中の生成物は NDMF がビルド終了時にまとめて保存するため Assets を汚さない。
        /// </summary>
        public static AnimatorController CreateAnimatorController(string name, string manualAssetPath)
        {
            if (IsBuilding || string.IsNullOrEmpty(manualAssetPath))
            {
                var controller = new AnimatorController { name = name };
                controller.AddLayer("Base Layer");
                return controller;
            }

            EnsureFolder(System.IO.Path.GetDirectoryName(manualAssetPath)?.Replace('\\', '/'));
            var persisted = AnimatorController.CreateAnimatorControllerAtPath(manualAssetPath);
            persisted.name = name;
            return persisted;
        }

        /// <summary>
        /// container が保存済みアセットならサブアセットとして追加する。
        /// メモリ上の container の場合は、NDMF のシリアライズ時に参照を辿って保存されるので何もしない。
        /// </summary>
        public static void AddSubAsset(Object obj, Object container)
        {
            if (obj == null || container == null)
                return;
            if (!EditorUtility.IsPersistent(container) || EditorUtility.IsPersistent(obj))
                return;

            obj.hideFlags |= HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(obj, container);
        }

        /// <summary>
        /// ビルド外で生成したアセットを保存する。ビルド中は何もしない（無関係な dirty アセットまで保存しないため）。
        /// </summary>
        public static void SaveIfPersistent(Object asset)
        {
            if (IsBuilding || asset == null || !EditorUtility.IsPersistent(asset))
                return;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        private sealed class Scope : IDisposable
        {
            private readonly BuildContext _previous;
            private bool _disposed;

            public Scope(BuildContext previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                Current = _previous;
            }
        }
    }
}
