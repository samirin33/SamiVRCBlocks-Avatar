using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Samirin33.NDMF.Base;
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
    /// 書き換え後のパスは解決できるため、MA の FixupAbsolutePlayAudioPass（解決できないパスだけを扱う）とは二重に補正しない。
    /// </summary>
    internal static class BoneProxyPlayAudioPathFix
    {
        /// <summary>Bone Proxy 配下のオブジェクトと、付け替え後に想定されるアバタールートからのパス。</summary>
        private readonly struct Candidate
        {
            public readonly Transform Transform;
            public readonly string[] ExpectedSegments;

            /// <summary>末尾から必ず一致させるセグメント数（Bone Proxy 名とその配下の相対パス）。</summary>
            public readonly int RequiredSegments;

            public Candidate(Transform transform, string[] expectedSegments, int requiredSegments)
            {
                Transform = transform;
                ExpectedSegments = expectedSegments;
                RequiredSegments = requiredSegments;
            }
        }

        public static void Execute(BuildContext context)
        {
            var root = context.AvatarRootTransform;
            var candidates = CollectCandidates(root);
            if (candidates.Count == 0)
                return;

            var asc = context.Extension<AnimatorServicesContext>();
            var remapper = asc.ObjectPathRemapper;
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

                        ResolveSourcePath(remapper, candidates, playAudio);
                    }
                }
            }
        }

        static void ResolveSourcePath(
            ObjectPathRemapper remapper,
            List<Candidate> candidates,
            VRCAnimatorPlayAudio playAudio)
        {
            var sourcePath = playAudio.SourcePath;
            if (string.IsNullOrEmpty(sourcePath))
                return;

            if (remapper.GetObjectForPath(sourcePath) != null)
                return;

            if (!TryFindBoneProxyTarget(candidates, sourcePath, out var target, out var ambiguous))
            {
                if (ambiguous)
                {
                    SamirinBuildContext.ReportWarning(
                        "[SamiVRCBlocks] VRC Animator Play Audio のパス \"" + sourcePath +
                        "\" に当てはまる MA Bone Proxy 配下のオブジェクトが複数あるため、書き換えませんでした。",
                        playAudio);
                }

                return;
            }

            playAudio.SourcePath = remapper.GetVirtualPathForObject(target);
        }

        static bool TryFindBoneProxyTarget(
            List<Candidate> candidates,
            string sourcePath,
            out Transform target,
            out bool ambiguous)
        {
            target = null;
            ambiguous = false;

            var authored = sourcePath.Split('/');
            var scored = new List<(Candidate candidate, int score)>();
            foreach (var candidate in candidates)
            {
                var score = CountTrailingMatches(authored, candidate.ExpectedSegments);
                if (score >= candidate.RequiredSegments)
                    scored.Add((candidate, score));
            }

            if (scored.Count == 0)
                return false;

            // 付け替え先（ボーン側）のパスまで多く一致する候補を優先する。
            var best = scored.Max(entry => entry.score);
            var matches = scored.Where(entry => entry.score == best).Select(entry => entry.candidate).ToList();

            if (matches.Count == 1)
            {
                target = matches[0].Transform;
                return true;
            }

            var withAudio = matches.Where(match => match.Transform.GetComponent<AudioSource>() != null).ToList();
            if (withAudio.Count == 1)
            {
                target = withAudio[0].Transform;
                return true;
            }

            ambiguous = true;
            return false;
        }

        /// <summary>
        /// Bone Proxy 配下の各オブジェクトについて「付け替え先のパス / Bone Proxy 名 / Bone Proxy からの相対パス」を作る。
        /// 少なくとも Bone Proxy 名から下は一致させるため、別の Bone Proxy 配下にある同名オブジェクトだけでは当たらない。
        /// 付け替え先のパスはアバターごとに階層が違うことがあるため、一致した長さで優先度だけを付ける。
        /// </summary>
        static List<Candidate> CollectCandidates(Transform root)
        {
            var candidates = new List<Candidate>();
            foreach (var proxy in root.GetComponentsInChildren<ModularAvatarBoneProxy>(true))
            {
                if (proxy == null)
                    continue;

                var destination = proxy.target;
                if (destination == null)
                    continue;

                string destinationPath;
                if (destination == root)
                    destinationPath = "";
                else
                {
                    destinationPath = NdmfRuntimeUtil.RelativePath(root, destination);
                    if (destinationPath == null)
                        continue;
                }

                var proxyTransform = proxy.transform;
                var basePath = string.IsNullOrEmpty(destinationPath)
                    ? proxyTransform.name
                    : destinationPath + "/" + proxyTransform.name;

                foreach (var transform in proxyTransform.GetComponentsInChildren<Transform>(true))
                {
                    if (NearestBoneProxy(transform) != proxy)
                        continue;

                    var relative = transform == proxyTransform
                        ? ""
                        : NdmfRuntimeUtil.RelativePath(proxyTransform, transform);
                    if (relative == null)
                        continue;

                    var expected = string.IsNullOrEmpty(relative) ? basePath : basePath + "/" + relative;
                    var required = 1 + (string.IsNullOrEmpty(relative) ? 0 : relative.Split('/').Length);
                    candidates.Add(new Candidate(transform, expected.Split('/'), required));
                }
            }

            return candidates;
        }

        static ModularAvatarBoneProxy NearestBoneProxy(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (current.TryGetComponent<ModularAvatarBoneProxy>(out var proxy))
                    return proxy;
            }

            return null;
        }

        /// <summary>
        /// 書かれたパスと想定パスが末尾から何セグメント連続して一致するか。
        /// 相対 Merge Animator の前置パスなど、先頭側の違いは数えない。
        /// </summary>
        static int CountTrailingMatches(string[] authored, string[] expected)
        {
            var count = 0;
            var a = authored.Length - 1;
            var e = expected.Length - 1;
            while (a >= 0 && e >= 0 && SegmentMatches(authored[a], expected[e]))
            {
                count++;
                a--;
                e--;
            }

            return count;
        }

        /// <summary>
        /// Bone Proxy の名前衝突で付く "Head (1)" を、元の "Head" と同じセグメントとして扱う。
        /// </summary>
        static bool SegmentMatches(string a, string b)
        {
            return a == b || IsNumberedVariant(a, b) || IsNumberedVariant(b, a);
        }

        static bool IsNumberedVariant(string actual, string authored)
        {
            if (!actual.StartsWith(authored + " (", System.StringComparison.Ordinal) || !actual.EndsWith(")"))
                return false;

            var number = actual.Substring(authored.Length + 2, actual.Length - authored.Length - 3);
            return int.TryParse(number, out _);
        }
    }
}
