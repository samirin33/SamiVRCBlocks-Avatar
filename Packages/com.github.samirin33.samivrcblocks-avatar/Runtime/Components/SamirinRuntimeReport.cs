using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// ランタイムアセンブリのコンポーネントから、ビルド中の問題を報告する窓口。
    /// Editor アセンブリ（SamirinRuntimeReportBridge）がハンドラを登録し、
    /// ビルド中は NDMF のエラーレポートへ、ビルド外は Console へ出す。
    /// </summary>
    public static class SamirinRuntimeReport
    {
        public static Action<string, Object[]> WarningHandler;
        public static Action<string, Object[]> ErrorHandler;

        public static void Warning(string message, params Object[] references)
        {
            if (WarningHandler != null)
                WarningHandler(message, references);
            else
                Debug.LogWarning(message, FirstOrNull(references));
        }

        public static void Error(string message, params Object[] references)
        {
            if (ErrorHandler != null)
                ErrorHandler(message, references);
            else
                Debug.LogError(message, FirstOrNull(references));
        }

        private static Object FirstOrNull(Object[] references)
        {
            return references != null && references.Length > 0 ? references[0] : null;
        }
    }
}
