using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using NdmfRuntimeUtil = nadena.dev.ndmf.runtime.RuntimeUtil;

namespace Samirin33.NDMF.Animation
{
    /// <summary>
    /// Bone Proxy の付け替え先を想定して書かれた VRCAnimatorPlayAudio.SourcePath を、
    /// MA 処理前に Bone Proxy 配下の実在する AudioSource のパスへ置き換える。
    /// 以降の Bone Proxy の移動や Head (1) のような改名は NDMF のパス追跡で反映される。
    /// Behaviour のインスタンスは他プラグインの複製で入れ替わるため、保持しない。
    /// </summary>
    internal static class BoneProxyPlayAudioPathFix
    {
        public static void Execute(BuildContext context)
        {
            var root = context.AvatarRootTransform;
            if (root.GetComponentInChildren<ModularAvatarBoneProxy>(true) == null)
                return;

            var asc = context.Extension<AnimatorServicesContext>();
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

                        ResolveSourcePath(remapper, indexed, playAudio);
                    }
                }
            }
        }

        static void ResolveSourcePath(
            ObjectPathRemapper remapper,
            List<(string path, Transform transform)> indexed,
            VRCAnimatorPlayAudio playAudio)
        {
            var sourcePath = playAudio.SourcePath;
            if (string.IsNullOrEmpty(sourcePath))
                return;

            if (remapper.GetObjectForPath(sourcePath) != null)
                return;

            if (!TryFindBoneProxyTarget(indexed, sourcePath, out var target, out var ambiguous))
            {
                // if (ambiguous)
                // {
                //     Debug.LogWarning(
                //         "[SamiVRCBlocks] VRC Audio Player のパス \"" + sourcePath +
                //         "\" は解決できず、MA Bone Proxy 配下の候補も一意ではないため書き換えをスキップしました。");
                // }

                return;
            }

            var resolvedPath = remapper.GetVirtualPathForObject(target);
            playAudio.SourcePath = resolvedPath;
            // Debug.Log(
            //     "[SamiVRCBlocks] VRC Audio Player のパスを MA Bone Proxy 配下の AudioSource へ合わせました: \"" +
            //     sourcePath + "\" -> \"" + resolvedPath + "\"");
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
