namespace Samirin33.NDMF.Base
{
    /// <summary>
    /// NDMF の BuildPhase に相当する Runtime 用のフェーズ識別子。
    /// Editor 専用の nadena.dev.ndmf.BuildPhase はビルド時に含まれないため、Runtime ではこの enum を使用する。
    /// </summary>
    public enum SamirinBuildPhase
    {
        Resolving,
        Generating,
        Transforming,
        Optimizing,
    }
}
