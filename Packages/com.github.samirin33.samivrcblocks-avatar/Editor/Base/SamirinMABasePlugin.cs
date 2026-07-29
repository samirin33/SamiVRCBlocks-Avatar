using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using nadena.dev.ndmf;
using Samirin33.NDMF.Base;
using Samirin33.NDMF.Base.Plugin;

[assembly: ExportsPlugin(typeof(SamirinMABasePlugin))]

namespace Samirin33.NDMF.Base.Plugin
{
    public class SamirinMABasePlugin : Plugin<SamirinMABasePlugin>
    {
        public override string QualifiedName => "SamirinMABase";

        public override string DisplayName => "SamirinMABase";

        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Resolving);
            seq.BeforePlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinResolvingBeforeMA", ctx =>
            {
                SamirinMABase[] scripts = ctx.AvatarRootObject.GetComponentsInChildren<SamirinMABase>(true);
                InvokeOnBuild(scripts, BuildPhase.Resolving, true, ctx.AvatarRootObject);
            });

            seq = InPhase(BuildPhase.Resolving);
            seq.AfterPlugin("SamirinResolvingBeforeMA");
            seq.Run("SamirinResolvingAfterMA", ctx =>
            {
                SamirinMABase[] scripts = ctx.AvatarRootObject.GetComponentsInChildren<SamirinMABase>(true);
                InvokeOnBuild(scripts, BuildPhase.Resolving, false, ctx.AvatarRootObject);
            });

            seq = InPhase(BuildPhase.Transforming);
            seq.BeforePlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinTransformingBeforeMA", ctx =>
            {
                SamirinMABase[] scripts = ctx.AvatarRootObject.GetComponentsInChildren<SamirinMABase>(true);
                InvokeOnBuild(scripts, BuildPhase.Transforming, true, ctx.AvatarRootObject);
            });

            seq = InPhase(BuildPhase.Transforming);
            seq.AfterPlugin("SamirinTransformingBeforeMA");
            seq.Run("SamirinTransformingAfterMA", ctx =>
            {
                SamirinMABase[] scripts = ctx.AvatarRootObject.GetComponentsInChildren<SamirinMABase>(true);
                InvokeOnBuild(scripts, BuildPhase.Transforming, false, ctx.AvatarRootObject);
            });

            seq = InPhase(BuildPhase.Generating);
            seq.BeforePlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinGeneratingBeforeMA", ctx =>
            {
                SamirinMABase[] scripts = ctx.AvatarRootObject.GetComponentsInChildren<SamirinMABase>(true);
                InvokeOnBuild(scripts, BuildPhase.Generating, true, ctx.AvatarRootObject);
            });

            seq = InPhase(BuildPhase.Transforming);
            seq.AfterPlugin("SamirinGeneratingBeforeMA");
            seq.Run("SamirinGeneratingAfterMA", ctx =>
            {
                SamirinMABase[] scripts = ctx.AvatarRootObject.GetComponentsInChildren<SamirinMABase>(true);
                InvokeOnBuild(scripts, BuildPhase.Generating, false, ctx.AvatarRootObject);
            });

            seq = InPhase(BuildPhase.Optimizing);
            seq.BeforePlugin("nadena.dev.modular-avatar");
            seq.Run("SamirinOptimizingBeforeMA", ctx =>
            {
                SamirinMABase[] scripts = ctx.AvatarRootObject.GetComponentsInChildren<SamirinMABase>(true);
                InvokeOnBuild(scripts, BuildPhase.Optimizing, true, ctx.AvatarRootObject);
            });

            seq = InPhase(BuildPhase.Optimizing);
            seq.AfterPlugin("SamirinOptimizingBeforeMA");
            seq.Run("SamirinOptimizingAfterMA", ctx =>
            {
                SamirinMABase[] scripts = ctx.AvatarRootObject.GetComponentsInChildren<SamirinMABase>(true);
                InvokeOnBuild(scripts, BuildPhase.Optimizing, false, ctx.AvatarRootObject);
            });
        }

        private static SamirinBuildPhase ToSamirinBuildPhase(BuildPhase phase)
        {
            if (phase == BuildPhase.Resolving) return SamirinBuildPhase.Resolving;
            if (phase == BuildPhase.Generating) return SamirinBuildPhase.Generating;
            if (phase == BuildPhase.Transforming) return SamirinBuildPhase.Transforming;
            if (phase == BuildPhase.Optimizing) return SamirinBuildPhase.Optimizing;
            throw new ArgumentOutOfRangeException(nameof(phase), phase?.Name, "Unsupported BuildPhase for SamirinBuildPhase.");
        }

        private static void InvokeOnBuild(SamirinMABase[] scripts, BuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            var processedSingleTypes = new HashSet<Type>();
            var processedOnBuildIds = new HashSet<int>();

            while (true)
            {
                var currentScripts = avatarRootObject.GetComponentsInChildren<SamirinMABase>(true).ToArray();
                var singleScripts = currentScripts.OfType<SamirinMABaseSingle>()
                    .Where(s => !processedSingleTypes.Contains(s.GetType()))
                    .ToArray();
                var scriptsPendingOnBuild = currentScripts
                    .Where(s => !processedOnBuildIds.Contains(s.GetInstanceID()))
                    .ToArray();

                var workItems = new List<(int priority, int order, bool isSingleGroup, object payload)>();

                foreach (var g in singleScripts.GroupBy(s => s.GetType()))
                    workItems.Add((g.Min(s => s.priority), 0, true, g.Key));

                foreach (var s in scriptsPendingOnBuild)
                    workItems.Add((s.priority, 1, false, s));

                if (workItems.Count == 0)
                    break;

                workItems.Sort((a, b) => a.priority != b.priority ? a.priority.CompareTo(b.priority) : a.order.CompareTo(b.order));
                var (_, _, isSingleGroup, payload) = workItems[0];

                var avatarRoot = avatarRootObject;

                if (isSingleGroup)
                {
                    var scriptType = (Type)payload;
                    processedSingleTypes.Add(scriptType);

                    var array = currentScripts.OfType<SamirinMABaseSingle>()
                        .Where(s => s.GetType() == scriptType)
                        .ToArray();
                    if (array.Length == 0) continue;

                    Action<GameObject, SamirinMABaseSingle[]> invokeBuilder = (root, s) =>
                        SamirinMABaseSingleBuildRegistry.Invoke(scriptType, root, s);
                    Action<GameObject, SamirinMABaseSingle[]> invokeReplaceBuilder = (root, s) =>
                        SamirinMABaseSingleBuildRegistry.InvokeReplace(scriptType, root, s);
                    array[0].OnBuildSingle(ToSamirinBuildPhase(buildPhase), beforeModularAvatar, array, avatarRoot, invokeBuilder, invokeReplaceBuilder);
                }
                else
                {
                    var script = (SamirinMABase)payload;
                    processedOnBuildIds.Add(script.GetInstanceID());
                    script.OnBuild(ToSamirinBuildPhase(buildPhase), beforeModularAvatar, avatarRootObject);
                }
            }
        }
    }
}