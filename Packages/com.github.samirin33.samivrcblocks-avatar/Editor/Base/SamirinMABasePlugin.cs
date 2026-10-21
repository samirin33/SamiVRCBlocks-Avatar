using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Samirin33.NDMF.Animation;
using Samirin33.NDMF.Base;
using Samirin33.NDMF.Base.Plugin;
using Samirin33.NDMF.Components;
using Samirin33.NDMF.Components.Editor;

[assembly: ExportsPlugin(typeof(SamirinMABasePlugin))]

namespace Samirin33.NDMF.Base.Plugin
{
    public class SamirinMABasePlugin : Plugin<SamirinMABasePlugin>
    {
        public override string QualifiedName => "SamirinMABase";

        public override string DisplayName => "SamiVRCBlocks";

        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Resolving);
            seq.BeforePlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinResolvingBeforeMA", ctx => RunPhase(ctx, BuildPhase.Resolving, true));

            seq = InPhase(BuildPhase.Resolving);
            seq.AfterPlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinResolvingAfterMA", ctx => RunPhase(ctx, BuildPhase.Resolving, false));

            seq = InPhase(BuildPhase.Generating);
            seq.BeforePlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinGeneratingBeforeMA", ctx => RunPhase(ctx, BuildPhase.Generating, true));

            seq = InPhase(BuildPhase.Generating);
            seq.AfterPlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinGeneratingAfterMA", ctx => RunPhase(ctx, BuildPhase.Generating, false));

            seq = InPhase(BuildPhase.Transforming);
            seq.BeforePlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinTransformingBeforeMA", ctx => RunPhase(ctx, BuildPhase.Transforming, true));
            // 上の OnBuild が MA Merge Animator を足したあと、統合前にダミー条件を揃える。
            // 仮想コントローラを書き換えるので、元の Animator アセットは変更しない。
            seq.WithRequiredExtension(typeof(AnimatorServicesContext), s =>
            {
                s.Run("SamirinFixDummyParameter", ctx => RunWithContext(ctx, () => FixDummyParameterProcessor.Execute(ctx)));
                // Bone Proxy が付け替える前に、Play Audio のパスを付け替え元の AudioSource へ向ける。
                s.Run("SamirinResolveBoneProxyPlayAudio", ctx => RunWithContext(ctx, () => BoneProxyPlayAudioPathFix.Execute(ctx)));
            });

            // Floor Adjuster は late-transform-stages で Hips を動かした直後に MA コンポーネントごと消えるため、
            // 補正量は適用前の Hips 位置との差分で求める。
            seq = InPhase(BuildPhase.Transforming);
            seq.AfterPlugin("nadena.dev.modular-avatar");
            seq.BeforePlugin("nadena.dev.modular-avatar.late-transform-stages");
            seq.Run("SamirinCaptureFloorAdjust", ctx => FloorAdjustmentTracker.CaptureBeforeAdjust(ctx.AvatarRootObject));

            seq = InPhase(BuildPhase.Transforming);
            seq.AfterPlugin("nadena.dev.modular-avatar");
            // メインプラグイン後の再バインド（late-transform-stages）より後。
            // ここで Armature を複製すると、再バインドが同名ボーンを取り違えない。
            seq.AfterPlugin("nadena.dev.modular-avatar.late-transform-stages");
            seq.Run("SamirinTransformingAfterMA", ctx =>
            {
                FloorAdjustmentTracker.ResolveAfterAdjust(ctx.AvatarRootObject);
                RunPhase(ctx, BuildPhase.Transforming, false);
            });

            seq = InPhase(BuildPhase.Optimizing);
            seq.BeforePlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinOptimizingBeforeMA", ctx => RunPhase(ctx, BuildPhase.Optimizing, true));

            seq = InPhase(BuildPhase.Optimizing);
            seq.AfterPlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinOptimizingAfterMA", ctx =>
            {
                RunPhase(ctx, BuildPhase.Optimizing, false);
                FloorAdjustmentTracker.Release(ctx.AvatarRootObject);
            });
        }

        private static void RunWithContext(BuildContext ctx, Action action)
        {
            using (SamirinBuildContext.Enter(ctx))
            {
                action();
            }
        }

        private static void RunPhase(BuildContext ctx, BuildPhase buildPhase, bool beforeModularAvatar)
        {
            using (SamirinBuildContext.Enter(ctx))
            {
                InvokeOnBuild(buildPhase, beforeModularAvatar, ctx.AvatarRootObject);
            }
        }

        private static SamirinBuildPhase ToSamirinBuildPhase(BuildPhase phase)
        {
            if (phase == BuildPhase.Resolving) return SamirinBuildPhase.Resolving;
            if (phase == BuildPhase.Generating) return SamirinBuildPhase.Generating;
            if (phase == BuildPhase.Transforming) return SamirinBuildPhase.Transforming;
            if (phase == BuildPhase.Optimizing) return SamirinBuildPhase.Optimizing;
            throw new ArgumentOutOfRangeException(nameof(phase), phase?.Name, "Unsupported BuildPhase for SamirinBuildPhase.");
        }

        private static void InvokeOnBuild(BuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            var samirinPhase = ToSamirinBuildPhase(buildPhase);

            // 型単位ではなくインスタンス単位。ExtendedParameters 等が後から追加した
            // SamirinMABaseSingle も同一フェーズ内で再処理できるようにする。
            var processedSingleIds = new HashSet<int>();
            var processedOnBuildIds = new HashSet<int>();

            while (true)
            {
                var currentScripts = avatarRootObject.GetComponentsInChildren<SamirinMABase>(true);

                (int priority, int order, string typeName, int index, SamirinMABase script, Type singleType) next = default;
                var found = false;

                for (var i = 0; i < currentScripts.Length; i++)
                {
                    var s = currentScripts[i];
                    if (s == null)
                        continue;

                    var id = s.GetInstanceID();
                    // 同一 priority では通常 OnBuild（ExtendedParameters 等）を Single より先に実行し、
                    // プレファブ展開後のコンポーネントを取り込めるようにする。
                    // それでも並ぶ場合は型名・ヒエラルキー順で決め、実行順をビルドごとに変えない。
                    (int, int, string, int, SamirinMABase, Type) candidate;
                    if (s is SamirinMABaseSingle)
                    {
                        if (processedSingleIds.Contains(id))
                            continue;
                        candidate = (s.priority, 1, s.GetType().FullName, i, s, s.GetType());
                    }
                    else
                    {
                        if (processedOnBuildIds.Contains(id))
                            continue;
                        candidate = (s.priority, 0, s.GetType().FullName, i, s, null);
                    }

                    if (!found || Compare(candidate, next) < 0)
                    {
                        next = candidate;
                        found = true;
                    }
                }

                if (!found)
                    break;

                if (next.singleType != null)
                {
                    var scriptType = next.singleType;
                    var array = currentScripts
                        .OfType<SamirinMABaseSingle>()
                        .Where(s => s != null && s.GetType() == scriptType && !processedSingleIds.Contains(s.GetInstanceID()))
                        .ToArray();

                    foreach (var s in array)
                        processedSingleIds.Add(s.GetInstanceID());

                    Action<GameObject, SamirinMABaseSingle[]> invokeBuilder = (root, s) =>
                        SamirinMABaseSingleBuildRegistry.Invoke(scriptType, root, s);
                    Action<GameObject, SamirinMABaseSingle[]> invokeReplaceBuilder = (root, s) =>
                        SamirinMABaseSingleBuildRegistry.InvokeReplace(scriptType, root, s);

                    ErrorReport.WithContextObject(array[0], () =>
                        array[0].OnBuildSingle(samirinPhase, beforeModularAvatar, array, avatarRootObject, invokeBuilder, invokeReplaceBuilder));
                }
                else
                {
                    var script = next.script;
                    processedOnBuildIds.Add(script.GetInstanceID());
                    ErrorReport.WithContextObject(script, () =>
                        script.OnBuild(samirinPhase, beforeModularAvatar, avatarRootObject));
                }
            }
        }

        private static int Compare(
            (int priority, int order, string typeName, int index, SamirinMABase script, Type singleType) a,
            (int priority, int order, string typeName, int index, SamirinMABase script, Type singleType) b)
        {
            if (a.priority != b.priority) return a.priority.CompareTo(b.priority);
            if (a.order != b.order) return a.order.CompareTo(b.order);
            var byType = string.CompareOrdinal(a.typeName, b.typeName);
            if (byType != 0) return byType;
            return a.index.CompareTo(b.index);
        }
    }
}
