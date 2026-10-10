using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// MA Floor Adjuster による高さ補正をビルド中に記録する。
    /// Floor Adjuster は Hips の localPosition を下げたあとコンポーネントごと削除されるため、
    /// 補正前に姿勢を記録したコンポーネントは、ここから補正量を引く。
    /// </summary>
    public static class FloorAdjustmentTracker
    {
        private class State
        {
            public Transform hips;
            public Vector3 hipsLocalBefore;
            public Vector3 worldDelta;
            public bool resolved;
            public readonly HashSet<Transform> moved = new HashSet<Transform>();
            public readonly Dictionary<Transform, Vector3> localPositionDeltas = new Dictionary<Transform, Vector3>();
        }

        private static readonly Dictionary<GameObject, State> States = new Dictionary<GameObject, State>();

        /// <summary>MA Floor Adjuster の実行前に呼ぶ。</summary>
        public static void CaptureBeforeAdjust(GameObject avatarRoot)
        {
            if (avatarRoot == null)
                return;

            States.Remove(avatarRoot);

            if (avatarRoot.GetComponentsInChildren<ModularAvatarFloorAdjuster>(false).Length != 1)
                return;
            if (!avatarRoot.TryGetComponent<Animator>(out var animator) || !animator.isHuman)
                return;

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null || hips.parent == null)
                return;

            var state = new State
            {
                hips = hips,
                hipsLocalBefore = hips.localPosition,
            };
            foreach (var t in hips.GetComponentsInChildren<Transform>(true))
                state.moved.Add(t);
            States[avatarRoot] = state;
        }

        /// <summary>MA Floor Adjuster の実行後、他の処理が Hips を動かす前に呼ぶ。</summary>
        public static void ResolveAfterAdjust(GameObject avatarRoot)
        {
            if (avatarRoot == null || !States.TryGetValue(avatarRoot, out var state) || state.resolved)
                return;

            if (state.hips == null || state.hips.parent == null)
            {
                States.Remove(avatarRoot);
                return;
            }

            var localDelta = state.hips.localPosition - state.hipsLocalBefore;
            if (localDelta.sqrMagnitude < 1e-12f)
            {
                States.Remove(avatarRoot);
                return;
            }

            state.worldDelta = LocalToWorldVector(state.hips.parent, localDelta);
            state.localPositionDeltas[state.hips] = localDelta;
            state.resolved = true;
        }

        /// <summary>補正前に記録したワールド位置を、補正後の値に直す。</summary>
        public static Vector3 CorrectWorldPosition(GameObject avatarRoot, Transform source, Vector3 capturedWorldPosition)
        {
            if (source == null || !TryGetResolved(avatarRoot, out var state) || !state.moved.Contains(source))
                return capturedWorldPosition;
            return capturedWorldPosition + state.worldDelta;
        }

        /// <summary>補正前に記録したローカル位置を、補正後の値に直す。</summary>
        public static Vector3 CorrectLocalPosition(GameObject avatarRoot, Transform source, Vector3 capturedLocalPosition)
        {
            if (source == null || !TryGetResolved(avatarRoot, out var state))
                return capturedLocalPosition;
            return state.localPositionDeltas.TryGetValue(source, out var delta)
                ? capturedLocalPosition + delta
                : capturedLocalPosition;
        }

        private static bool TryGetResolved(GameObject avatarRoot, out State state)
        {
            state = null;
            return avatarRoot != null
                && States.TryGetValue(avatarRoot, out state)
                && state.resolved;
        }

        private static Vector3 LocalToWorldVector(Transform parent, Vector3 localVector)
        {
            TransformMath.GetWorldTRS(parent, out _, out var rotation, out var scale);
            return rotation * Vector3.Scale(scale, localVector);
        }
    }
}
