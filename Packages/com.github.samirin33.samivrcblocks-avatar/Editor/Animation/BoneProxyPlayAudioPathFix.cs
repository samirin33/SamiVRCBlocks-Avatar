using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Samirin33.NDMF.Components;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using NdmfRuntimeUtil = nadena.dev.ndmf.runtime.RuntimeUtil;

namespace Samirin33.NDMF.Animation
{
    /// <summary>
    /// VRCAnimatorPlayAudio.SourcePath を、Bone Proxy の改名と OriginalArmature への移動が終わった階層へ合わせる。
    /// 他の Bone Proxy と同じ骨へ付くと Head は Head (1) のように番号が付く。
    /// その AudioSource は付け替え先のボーンと一緒に OriginalArmature 以下へ移る。
    /// </summary>
    internal static class BoneProxyPlayAudioPathFix
    {
        sealed class Pending
        {
            public VRCAnimatorPlayAudio PlayAudio;
            public Transform Target;
            public string AuthoredPath;
        }

        static readonly List<Pending> PendingBindings = new List<Pending>();

        public static void Execute(BuildContext context)
        {
            PendingBindings.Clear();

            var root = context.AvatarRootTransform;
            if (root.GetComponentInChildren<ModularAvatarBoneProxy>(true) == null)
                return;

            var asc = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var remapper = asc.ObjectPathRemapper;
            var indexed = IndexPaths(root);
            var seen = new HashSet<int>();

            foreach (var controller in asc.ControllerContext.GetAllControllers())
            {
                if (controller == null)
                    continue;

                foreach (var node in controller.AllReachableNodes())
                {
                    IEnumerable<StateMachineBehaviour> behaviours = node switch
                    {
                        VirtualState state => state.Behaviours,
                        VirtualStateMachine stateMachine => stateMachine.Behaviours,
                        _ => null
                    };
                    if (behaviours == null)
                        continue;

                    foreach (var playAudio in behaviours.OfType<VRCAnimatorPlayAudio>())
                    {
                        if (playAudio == null || !seen.Add(playAudio.GetInstanceID()))
                            continue;

                        TryCapture(remapper, indexed, playAudio);
                    }
                }
            }
        }

        /// <summary>
        /// Bone Proxy の番号付き改名と、ControllableHumanoid の OriginalArmature 移動の後に呼ぶ。
        /// </summary>
        public static void ApplyResolvedPaths(BuildContext context)
        {
            var root = context.AvatarRootTransform;
            try
            {
                foreach (var pending in PendingBindings)
                {
                    if (pending.PlayAudio == null)
                        continue;

                    var target = pending.Target;
                    if (target == null && !TryFindResolvedTarget(root, pending.AuthoredPath, out target))
                    {
                        Debug.LogWarning(
                            "[SamiVRCBlocks] VRC Audio Player のパス \"" + pending.AuthoredPath +
                            "\" に対応する AudioSource が OriginalArmature 以下に見つかりません。");
                        continue;
                    }

                    var resolvedPath = NdmfRuntimeUtil.RelativePath(root, target);
                    if (string.IsNullOrEmpty(resolvedPath))
                    {
                        Debug.LogWarning(
                            "[SamiVRCBlocks] VRC Audio Player の対象 \"" + target.name +
                            "\" がアバター配下にないためパスを更新できません。");
                        continue;
                    }

                    if (pending.PlayAudio.SourcePath == resolvedPath)
                        continue;

                    var authoredPath = pending.PlayAudio.SourcePath;
                    pending.PlayAudio.SourcePath = resolvedPath;
                    Debug.Log(
                        "[SamiVRCBlocks] VRC Audio Player のパスを付け替え後の位置へ更新しました: \"" +
                        authoredPath + "\" -> \"" + resolvedPath + "\"");
                }
            }
            finally
            {
                PendingBindings.Clear();
            }
        }

        static void TryCapture(
            ObjectPathRemapper remapper,
            List<(string path, Transform transform)> indexed,
            VRCAnimatorPlayAudio playAudio)
        {
            var sourcePath = playAudio.SourcePath;
            if (string.IsNullOrEmpty(sourcePath))
                return;

            var resolved = remapper.GetObjectForPath(sourcePath);
            if (resolved != null)
            {
                if (!IsUnderBoneProxy(resolved.transform))
                    return;

                PendingBindings.Add(new Pending
                {
                    PlayAudio = playAudio,
                    Target = resolved.transform,
                    AuthoredPath = sourcePath,
                });
                return;
            }

            if (!TryFindBoneProxyTarget(indexed, sourcePath, out var target, out var ambiguous))
            {
                if (ambiguous)
                {
                    Debug.LogWarning(
                        "[SamiVRCBlocks] VRC Audio Player のパス \"" + sourcePath +
                        "\" は解決できず、MA Bone Proxy 配下の候補も一意ではないため書き換えをスキップしました。");
                }

                return;
            }

            PendingBindings.Add(new Pending
            {
                PlayAudio = playAudio,
                Target = target,
                AuthoredPath = sourcePath,
            });
        }

        static bool TryFindResolvedTarget(Transform root, string authoredPath, out Transform target)
        {
            target = null;
            if (string.IsNullOrEmpty(authoredPath))
                return false;

            var indexed = IndexPaths(root);
            var matches = FindSuffixMatches(indexed, authoredPath);
            if (matches.Count == 0)
                return false;

            var underOriginal = matches
                .Where(match => IsUnderNamedRoot(match.path, ControllableHumanoid.OriginalArmatureName))
                .ToList();
            if (underOriginal.Count > 0)
                matches = underOriginal;

            var withAudio = matches.Where(match => match.transform.GetComponent<AudioSource>() != null).ToList();
            if (withAudio.Count == 1)
            {
                target = withAudio[0].transform;
                return true;
            }

            if (matches.Count == 1)
            {
                target = matches[0].transform;
                return true;
            }

            return false;
        }

        static bool TryFindBoneProxyTarget(
            List<(string path, Transform transform)> indexed,
            string sourcePath,
            out Transform target,
            out bool ambiguous)
        {
            target = null;
            ambiguous = false;
            var matches = FindSuffixMatches(indexed, sourcePath);
            var proxied = matches.Where(match => IsUnderBoneProxy(match.transform)).ToList();
            if (proxied.Count == 0)
                return false;

            var withAudio = proxied.Where(match => match.transform.GetComponent<AudioSource>() != null).ToList();
            if (withAudio.Count == 1)
            {
                target = withAudio[0].transform;
                return true;
            }

            if (proxied.Count == 1)
            {
                target = proxied[0].transform;
                return true;
            }

            ambiguous = true;
            return false;
        }

        static List<(string path, Transform transform)> FindSuffixMatches(
            List<(string path, Transform transform)> indexed,
            string sourcePath)
        {
            var authored = sourcePath.Split('/');
            for (var start = 0; start < authored.Length; start++)
            {
                var suffixLength = authored.Length - start;
                var matches = indexed.Where(entry => EndsWithNumberedSegments(entry.path, authored, start, suffixLength)).ToList();
                if (matches.Count > 0)
                    return matches;
            }

            return new List<(string path, Transform transform)>();
        }

        static bool EndsWithNumberedSegments(string path, string[] authored, int start, int suffixLength)
        {
            var actual = path.Split('/');
            if (actual.Length < suffixLength)
                return false;

            var offset = actual.Length - suffixLength;
            for (var i = 0; i < suffixLength; i++)
            {
                if (!SegmentMatches(actual[offset + i], authored[start + i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Bone Proxy の名前衝突で付く "Head (1)" を、元の "Head" と同じセグメントとして扱う。
        /// </summary>
        static bool SegmentMatches(string actual, string authored)
        {
            if (actual == authored)
                return true;
            if (!actual.StartsWith(authored + " (", System.StringComparison.Ordinal) || !actual.EndsWith(")"))
                return false;

            var number = actual.Substring(authored.Length + 2, actual.Length - authored.Length - 3);
            return int.TryParse(number, out _);
        }

        static bool IsUnderNamedRoot(string path, string rootName)
        {
            return path == rootName || path.StartsWith(rootName + "/", System.StringComparison.Ordinal);
        }

        static List<(string path, Transform transform)> IndexPaths(Transform root)
        {
            var indexed = new List<(string path, Transform transform)>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var path = NdmfRuntimeUtil.RelativePath(root, transform);
                if (string.IsNullOrEmpty(path))
                    continue;
                indexed.Add((path, transform));
            }

            return indexed;
        }

        static bool IsUnderBoneProxy(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (current.GetComponent<ModularAvatarBoneProxy>() != null)
                    return true;
            }

            return false;
        }
    }
}
