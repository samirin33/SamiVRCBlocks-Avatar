using System;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace Samirin33.NDMF.Animation
{
    /// <summary>
    /// 拡張コンテキストを宣言しない汎用パス（SamirinMABase.OnBuild）内で AnimatorServicesContext を開き、同じパス内で必ず閉じる。
    /// NDMF の実行計画上は非アクティブ扱いなので、開いたままにすると後段（VRCFury 等）が
    /// 直接編集した Animator を、ビルド終了時のコミットが古い仮想コントローラで上書きする。
    /// 既に開いている場合は閉じない。
    /// </summary>
    internal sealed class SamirinAnimatorServicesScope : IDisposable
    {
        private readonly BuildContext _context;
        private readonly bool _ownsServices;
        private readonly bool _ownsControllers;

        public AnimatorServicesContext Services { get; }

        public SamirinAnimatorServicesScope(BuildContext context)
        {
            _context = context;
            _ownsControllers = !IsActive<VirtualControllerContext>(context);
            _ownsServices = !IsActive<AnimatorServicesContext>(context);
            Services = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        }

        private static bool IsActive<T>(BuildContext context) where T : IExtensionContext
        {
            try
            {
                context.Extension<T>();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_ownsServices)
                _context.DeactivateExtensionContext<AnimatorServicesContext>();
            if (_ownsControllers)
                _context.DeactivateExtensionContext<VirtualControllerContext>();
        }
    }
}
