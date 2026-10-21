using UnityEditor;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// ランタイム側の SamirinRuntimeReport を SamirinBuildContext（NDMF ErrorReport）へつなぐ。
    /// </summary>
    internal static class SamirinRuntimeReportBridge
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            SamirinRuntimeReport.WarningHandler = SamirinBuildContext.ReportWarning;
            SamirinRuntimeReport.ErrorHandler = SamirinBuildContext.ReportError;
        }
    }
}
