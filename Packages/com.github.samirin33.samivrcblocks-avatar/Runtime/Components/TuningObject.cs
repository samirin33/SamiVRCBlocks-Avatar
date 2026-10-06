using System;
using System.Collections.Generic;
using UnityEngine;
using Samirin33.NDMF.Base;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// チューニング用のギズモ表示コンポーネント。
    /// 自身または親が選択されているとき、矢印・中心球・ボックス・カプセル・任意テキスト・半透明メッシュを Scene ビューに描画する。
    /// active 時は targetTransforms に自身+Offset を適用する。
    /// 適用は初回と、自身のローカル姿勢または Offset が変わったときだけで、それ以外は Target のローカル相対位置を維持する。
    /// previewParticles 時は Target 配下の ParticleSystem をエディタ上でプレビュー再生する。
    /// ビルド時、子が無い場合は自身の GameObject を削除する。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB TuningObject")]
    public class TuningObject : SamirinMABase
    {
        [Serializable]
        public class TargetTransform
        {
            public Transform transform;
            public Vector3 offsetPosition = Vector3.zero;
            public Vector3 offsetRotation = Vector3.zero;
            public Vector3 offsetScale = Vector3.one;
        }

        /// <summary>カプセルの長軸。回転は自身の Transform に追従する。</summary>
        public enum CapsuleDirection
        {
            X,
            Y,
            Z,
        }

        [Serializable]
        public class ArrowGizmo
        {
            [Tooltip("矢印の向き（ゼロベクトルの場合は描画しない）")]
            public Vector3 direction = Vector3.forward;

            [Tooltip("矢印の長さ。オブジェクトのスケールが反映される")]
            [Min(0f)]
            public float length = 0.1f;

            [Tooltip("矢印先端（ヘッド）の大きさ。オブジェクトのスケールが反映される")]
            [Min(0f)]
            public float headSize = 0.02f;

            [Tooltip("矢印の色")]
            public Color color = Color.cyan;

            [Tooltip("true ならローカル空間、false ならワールド空間で direction を解釈する")]
            public bool localSpace = true;
        }

        [Tooltip("true のとき、Target に自身の Transform + Offset を適用する。初回と、自身のローカル姿勢または Offset が変わったときだけで、親の移動では Target のローカル相対位置を維持する")]
        public bool active;

        [Tooltip("true のとき、TuningObject 自身の選択中のみ Target 配下の ParticleSystem をエディタ上でプレビュー再生する")]
        public bool previewParticles;

        [Header("Target Transforms")]
        public TargetTransform[] targetTransforms = { new TargetTransform() };

        /// <summary>スナップ直後のローカル姿勢が記録されているか。</summary>
        public bool HasSnapLocalPose => _hasSnapLocalPose;

        [SerializeField, HideInInspector]
        private bool _hasSnapLocalPose;

        [SerializeField, HideInInspector]
        private Vector3 _snapLocalPosition;

        [SerializeField, HideInInspector]
        private Vector3 _snapLocalEulerAngles;

        [SerializeField, HideInInspector]
        private Vector3 _snapLocalScale = Vector3.one;

        [Tooltip("中心点の球を表示する")]
        public bool showSphere = true;

        [Tooltip("中心球の半径（ローカル単位。オブジェクトのスケールが反映される）")]
        [Min(0f)]
        public float sphereRadius = 0.015f;

        [Tooltip("中心球の色")]
        public Color sphereColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        [Tooltip("中心のボックスを表示する。サイズはローカル単位で、回転とスケールは自身の Transform に追従する")]
        public bool showBox;

        [Tooltip("ボックスのサイズ（ローカル単位。オブジェクトのスケールが反映される）")]
        public Vector3 boxSize = new Vector3(0.03f, 0.03f, 0.03f);

        [Tooltip("ボックスの色")]
        public Color boxColor = new Color(0.3f, 0.85f, 0.45f, 0.9f);

        [Tooltip("中心のカプセルを表示する。サイズはローカル単位で、回転とスケールは自身の Transform に追従する")]
        public bool showCapsule;

        [Tooltip("カプセルの半径（ローカル単位。オブジェクトのスケールが反映される）")]
        [Min(0f)]
        public float capsuleRadius = 0.015f;

        [Tooltip("カプセルの高さ（端から端まで、ローカル単位）。半径の2倍未満のときは球と同じ見た目になる")]
        [Min(0f)]
        public float capsuleHeight = 0.06f;

        [Tooltip("カプセルの長軸")]
        public CapsuleDirection capsuleDirection = CapsuleDirection.Y;

        [Tooltip("カプセルの色")]
        public Color capsuleColor = new Color(0.65f, 0.45f, 1f, 0.9f);

        [Tooltip("描画する矢印の一覧（数・向き・長さ・色は自由）")]
        public List<ArrowGizmo> arrows = new List<ArrowGizmo>
        {
            new ArrowGizmo { direction = Vector3.forward, length = 0.1f, color = Color.cyan },
        };

        [Tooltip("中心の右下にテキストを表示する")]
        public bool showLabel;

        [Tooltip("表示するテキスト")]
        public string labelText = "";

        [Tooltip("テキストの色")]
        public Color labelColor = Color.white;

        [Tooltip("中心からの画面上オフセット（右・下方向のワールド換算距離）")]
        [Min(0f)]
        public float labelOffset = 0.03f;

        [Tooltip("任意メッシュを半透明で表示する")]
        public bool showMesh;

        [Tooltip("表示するメッシュ")]
        public Mesh previewMesh;

        [Tooltip("メッシュの色（アルファで半透明度を指定）")]
        public Color meshColor = new Color(0.3f, 0.8f, 1f, 0.35f);

        [Tooltip("メッシュのローカルオフセット")]
        public Vector3 meshOffset = Vector3.zero;

        [Tooltip("メッシュのローカル回転（Euler）")]
        public Vector3 meshRotation = Vector3.zero;

        [Tooltip("メッシュのローカルスケール")]
        public Vector3 meshScale = Vector3.one;

        /// <summary>
        /// 直前に Target へ適用したときの駆動元。未適用なら false。
        /// ドメインリロード後に再適用して利き手の結果を戻さないよう、インスタンスへ保存する。
        /// </summary>
        [SerializeField, HideInInspector]
        private bool _targetApplyStampValid;

        [SerializeField, HideInInspector]
        private Vector3 _targetApplyLocalPosition;

        [SerializeField, HideInInspector]
        private Quaternion _targetApplyLocalRotation;

        [SerializeField, HideInInspector]
        private Vector3 _targetApplyLocalScale;

        [SerializeField, HideInInspector]
        private int _targetApplyOffsetHash;

        private void Update()
        {
#if UNITY_EDITOR
            // エディタでは EditorUpdate 側で適用する（配置スナップ待ち中に Target を動かさない）
            if (!Application.isPlaying) return;
#endif
            TryApplyToTargetsIfDriven();
        }

        /// <summary>
        /// Active の Target 適用を、初回と駆動元（ローカル姿勢・Offset）の変化時に限る。
        /// 親だけが動いたフレームでは書き戻さないので、利き手切り替え後も子のローカル相対位置が残る。
        /// </summary>
        private void TryApplyToTargetsIfDriven()
        {
            if (!active)
            {
                _targetApplyStampValid = false;
                return;
            }

            if (!HasDrivingPoseChanged())
                return;

            ApplyToTargets();
            CaptureDrivingPose();
        }

        private bool HasDrivingPoseChanged()
        {
            if (!_targetApplyStampValid)
                return true;

            if ((transform.localPosition - _targetApplyLocalPosition).sqrMagnitude > 1e-12f)
                return true;
            if (Mathf.Abs(Quaternion.Dot(transform.localRotation, _targetApplyLocalRotation)) < 0.9999999f)
                return true;
            if ((transform.localScale - _targetApplyLocalScale).sqrMagnitude > 1e-12f)
                return true;
            return ComputeOffsetHash() != _targetApplyOffsetHash;
        }

        private void CaptureDrivingPose()
        {
            _targetApplyStampValid = true;
            _targetApplyLocalPosition = transform.localPosition;
            _targetApplyLocalRotation = transform.localRotation;
            _targetApplyLocalScale = transform.localScale;
            _targetApplyOffsetHash = ComputeOffsetHash();
        }

        private int ComputeOffsetHash()
        {
            unchecked
            {
                var hash = 17;
                if (targetTransforms == null)
                    return hash;

                hash = hash * 31 + targetTransforms.Length;
                for (int i = 0; i < targetTransforms.Length; i++)
                {
                    var entry = targetTransforms[i];
                    if (entry == null)
                    {
                        hash *= 31;
                        continue;
                    }

                    hash = hash * 31 + (entry.transform != null ? entry.transform.GetInstanceID() : 0);
                    hash = HashVector(hash, entry.offsetPosition);
                    hash = HashVector(hash, entry.offsetRotation);
                    hash = HashVector(hash, entry.offsetScale);
                }

                return hash;
            }
        }

        private static int HashVector(int hash, Vector3 value)
        {
            unchecked
            {
                hash = hash * 31 + value.x.GetHashCode();
                hash = hash * 31 + value.y.GetHashCode();
                hash = hash * 31 + value.z.GetHashCode();
                return hash;
            }
        }

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            // MA 処理後に子の有無を判定し、空なら GameObject ごと削除する
            if (buildPhase != SamirinBuildPhase.Transforming || beforeModularAvatar)
                return;

            if (transform.childCount == 0)
                DestroyImmediate(gameObject);
            else
                DestroyImmediate(this);
        }

        /// <summary>
        /// 各 Target に、自身の Transform + Offset を適用する。
        /// </summary>
        public void ApplyToTargets()
        {
            if (targetTransforms == null) return;

            for (int i = 0; i < targetTransforms.Length; i++)
                ApplyToTarget(i);
        }

        public void ApplyToTarget(int index)
        {
            if (!TryGetEntry(index, out var entry)) return;
            if (entry.transform == transform) return;

            var target = entry.transform;
            var position = transform.TransformPoint(entry.offsetPosition);
            var rotation = transform.rotation * Quaternion.Euler(entry.offsetRotation);
            var lossyScale = Vector3.Scale(transform.lossyScale, entry.offsetScale);

            target.SetPositionAndRotation(position, rotation);
            SetLossyScale(target, lossyScale);
        }

        /// <summary>
        /// sourceBefore から sourceAfter へのワールド姿勢の変化と同じだけ自身を動かす。
        /// 利き手切り替えで Bone Proxy などが動いたとき、ギズモを追従させる。
        /// Active でも Target へは書き戻さず、駆動元スタンプだけ更新する。
        /// </summary>
        public void FollowWorldPose(Matrix4x4 sourceBefore, Matrix4x4 sourceAfter, bool recordUndo = true)
        {
            if (!HasWorldPoseChanged(sourceBefore, sourceAfter))
                return;

            var result = sourceAfter * sourceBefore.inverse * transform.localToWorldMatrix;
            var position = result.GetPosition();
            var rotation = result.rotation;
            var scale = result.lossyScale;
            if (!IsFinite(position) || !IsFinite(rotation) || !IsFinite(scale))
                return;

#if UNITY_EDITOR
            if (recordUndo)
            {
                Undo.RecordObject(transform, "Move TuningObject with Dominant Hand");
                Undo.RecordObject(this, "Move TuningObject with Dominant Hand");
            }
#endif
            transform.SetPositionAndRotation(position, rotation);
            if ((scale - transform.lossyScale).sqrMagnitude > 1e-12f)
                SetLossyScale(transform, scale);

            if (active)
                CaptureDrivingPose();
            else
                _targetApplyStampValid = false;

#if UNITY_EDITOR
            EditorUtility.SetDirty(transform);
            EditorUtility.SetDirty(this);
            if (PrefabUtility.IsPartOfPrefabInstance(transform))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }

        private static bool HasWorldPoseChanged(Matrix4x4 before, Matrix4x4 after)
        {
            if ((before.GetPosition() - after.GetPosition()).sqrMagnitude > 1e-10f)
                return true;
            if (Quaternion.Angle(before.rotation, after.rotation) > 0.01f)
                return true;
            return (before.lossyScale - after.lossyScale).sqrMagnitude > 1e-10f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 自身を指定 Target のワールド位置・回転・スケールへ移動する。
        /// </summary>
        public void MoveSelfToTarget(int index)
        {
            if (!TryGetEntry(index, out var entry)) return;
            if (entry.transform == transform) return;

#if UNITY_EDITOR
            Undo.RecordObject(transform, "Move TuningObject to Target");
#endif
            var target = entry.transform;
            transform.SetPositionAndRotation(target.position, target.rotation);
            SetLossyScale(transform, target.lossyScale);
#if UNITY_EDITOR
            EditorUtility.SetDirty(transform);
#endif
        }

        /// <summary>
        /// 自身を「Target − Offset」の姿勢へ移動する（Apply の逆変換）。
        /// Active 適用後も Target が動かない位置に自身を置く。
        /// </summary>
        public void AlignSelfToTargetMinusOffset(int index, bool recordUndo = true)
        {
            if (!TryGetEntry(index, out var entry)) return;
            if (entry.transform == transform) return;

#if UNITY_EDITOR
            if (recordUndo)
                Undo.RecordObject(transform, "Align TuningObject to Target - Offset");
#endif
            var target = entry.transform;
            var rotation = target.rotation * Quaternion.Inverse(Quaternion.Euler(entry.offsetRotation));
            var lossyScale = DivideScale(target.lossyScale, entry.offsetScale);

            transform.rotation = rotation;
            SetLossyScale(transform, lossyScale);
            // TransformPoint(offset) == position + TransformVector(offset)
            transform.position = target.position - transform.TransformVector(entry.offsetPosition);
#if UNITY_EDITOR
            EditorUtility.SetDirty(transform);
            if (PrefabUtility.IsPartOfPrefabInstance(transform))
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
#endif
        }

        /// <summary>
        /// 自身と Target の現在の差分を Offset として登録する。
        /// </summary>
        public void CaptureOffsetFromTarget(int index)
        {
            if (!TryGetEntry(index, out var entry)) return;
            if (entry.transform == transform) return;

#if UNITY_EDITOR
            Undo.RecordObject(this, "Capture TuningObject Offset");
#endif
            var target = entry.transform;
            entry.offsetPosition = transform.InverseTransformPoint(target.position);
            entry.offsetRotation = (Quaternion.Inverse(transform.rotation) * target.rotation).eulerAngles;
            entry.offsetScale = DivideScale(target.lossyScale, transform.lossyScale);
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 現在のローカル姿勢をスナップ基準として記録する。
        /// </summary>
        public void RecordSnapLocalPose(bool recordUndo = true)
        {
#if UNITY_EDITOR
            if (recordUndo)
                Undo.RecordObject(this, "Record TuningObject Snap Pose");
#endif
            _hasSnapLocalPose = true;
            _snapLocalPosition = transform.localPosition;
            _snapLocalEulerAngles = transform.localEulerAngles;
            _snapLocalScale = transform.localScale;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            if (PrefabUtility.IsPartOfPrefabInstance(this))
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
        }

        /// <summary>
        /// 記録したスナップ時のローカル姿勢へ戻す。
        /// </summary>
        public void ResetToSnapLocalPose()
        {
            if (!_hasSnapLocalPose) return;

#if UNITY_EDITOR
            Undo.RecordObject(transform, "Reset TuningObject to Snap Pose");
#endif
            transform.localPosition = _snapLocalPosition;
            transform.localEulerAngles = _snapLocalEulerAngles;
            transform.localScale = _snapLocalScale;
#if UNITY_EDITOR
            EditorUtility.SetDirty(transform);
            if (PrefabUtility.IsPartOfPrefabInstance(transform))
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
#endif
        }

        private bool TryGetEntry(int index, out TargetTransform entry)
        {
            entry = null;
            if (targetTransforms == null || index < 0 || index >= targetTransforms.Length)
                return false;
            entry = targetTransforms[index];
            return entry != null && entry.transform != null;
        }

        private static void SetLossyScale(Transform target, Vector3 lossyScale)
        {
            var parent = target.parent;
            if (parent == null)
            {
                target.localScale = lossyScale;
                return;
            }

            var parentLossy = parent.lossyScale;
            target.localScale = DivideScale(lossyScale, parentLossy);
        }

        private static Vector3 DivideScale(Vector3 a, Vector3 b)
        {
            return new Vector3(
                ApproxDiv(a.x, b.x),
                ApproxDiv(a.y, b.y),
                ApproxDiv(a.z, b.z));
        }

        private static float ApproxDiv(float a, float b)
        {
            return Mathf.Abs(b) > 1e-8f ? a / b : a;
        }

#if UNITY_EDITOR
        private static Material s_translucentMaterial;
        private static Mesh s_cylinderMesh;

        private const double PlacementAlignDelaySeconds = 0.3;
        private const double PlacementAlignGiveUpSeconds = 5.0;
        private const int PlacementAlignMinFrames = 8;

        /// <summary>シーン上で一度スナップ済みか。Prefab アセット上では常に false に保つ。</summary>
        [SerializeField, HideInInspector]
        private bool _placementSnapCompleted;

        private bool _waitingForPlacementAlign;
        private double _placementAlignStartTime;
        private int _placementAlignFrames;

        private bool _particlePreviewActive;
        private double _lastParticlePreviewTime = -1;
        private readonly List<ParticleSystem> _previewParticleRoots = new List<ParticleSystem>();

        private void OnEnable()
        {
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;

            if (!_placementSnapCompleted)
                BeginPlacementAlignWait();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            StopParticlePreview();
        }

        private void Reset()
        {
            if (targetTransforms == null || targetTransforms.Length == 0)
                targetTransforms = new[] { new TargetTransform() };

            SchedulePlacementSnap();
        }

        private void OnValidate()
        {
            // Prefab アセットに「済」フラグが焼き付くと、以降の配置でスナップしなくなる
            if (EditorUtility.IsPersistent(gameObject) || PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                if (_placementSnapCompleted)
                    _placementSnapCompleted = false;
                if (_targetApplyStampValid)
                    _targetApplyStampValid = false;
            }

            if (!previewParticles && _particlePreviewActive)
                StopParticlePreview();
        }

        /// <summary>
        /// シーンへ配置されたときなど、強制的に配置スナップをやり直す。
        /// </summary>
        public void SchedulePlacementSnap()
        {
            if (Application.isPlaying) return;
            if (EditorUtility.IsPersistent(gameObject) || PrefabUtility.IsPartOfPrefabAsset(gameObject))
                return;

            _placementSnapCompleted = false;
            BeginPlacementAlignWait();
            EditorUtility.SetDirty(this);
        }

        private void BeginPlacementAlignWait()
        {
            if (Application.isPlaying || _placementSnapCompleted) return;
            if (EditorUtility.IsPersistent(gameObject) || PrefabUtility.IsPartOfPrefabAsset(gameObject))
                return;

            _waitingForPlacementAlign = true;
            _placementAlignStartTime = EditorApplication.timeSinceStartup;
            _placementAlignFrames = 0;

            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
        }

        /// <summary>
        /// ディレイ後に targetTransforms[0] − Offset へ自身を合わせる。
        /// 完了まで Active の Target 適用は行わない。
        /// </summary>
        private void TryAlignOnScenePlacement()
        {
            if (!_waitingForPlacementAlign || _placementSnapCompleted || Application.isPlaying)
                return;

            _placementAlignFrames++;
            var elapsed = EditorApplication.timeSinceStartup - _placementAlignStartTime;

            if (_placementAlignFrames < PlacementAlignMinFrames
                || elapsed < PlacementAlignDelaySeconds)
                return;

            if (TryGetEntry(0, out _))
            {
                // 自動スナップでは Undo を使わない（EditorUpdate 中の Undo が姿勢を巻き戻すことがある）
                AlignSelfToTargetMinusOffset(0, recordUndo: false);
                RecordSnapLocalPose(recordUndo: false);
                _placementSnapCompleted = true;
                _waitingForPlacementAlign = false;
                EditorUtility.SetDirty(this);
                if (PrefabUtility.IsPartOfPrefabInstance(this))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);

                return;
            }

            // Target 未設定なら諦めて Active 適用を許可（完了扱いにしてループしない）
            if (elapsed >= PlacementAlignGiveUpSeconds)
            {
                _waitingForPlacementAlign = false;
                _placementSnapCompleted = true;
                EditorUtility.SetDirty(this);
            }
        }

        private void EditorUpdate()
        {
            if (this == null || Application.isPlaying) return;

            if (_waitingForPlacementAlign && !_placementSnapCompleted)
            {
                TryAlignOnScenePlacement();
                if (_waitingForPlacementAlign)
                    return;
            }

            TryApplyToTargetsIfDriven();

            UpdateParticlePreview();
        }

        /// <summary>
        /// Target 配下の ParticleSystem をエディタ上で Simulate してプレビューする。
        /// TuningObject 自身が選択されているときのみ再生する（親・Target・パーティクル本体の選択では動かさない）。
        /// </summary>
        private void UpdateParticlePreview()
        {
            if (!previewParticles || !IsTuningObjectDirectlySelected())
            {
                if (_particlePreviewActive)
                    ReleaseParticlePreview();
                return;
            }

            CollectPreviewParticleRoots();
            if (_previewParticleRoots.Count == 0)
            {
                if (_particlePreviewActive)
                    ReleaseParticlePreview();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            // 同一時刻の多重呼び出し（Repaint 連鎖など）で dt を二重適用しない
            if (_lastParticlePreviewTime >= 0 && now <= _lastParticlePreviewTime)
                return;

            var dt = _lastParticlePreviewTime < 0 ? (1f / 60f) : (float)(now - _lastParticlePreviewTime);
            _lastParticlePreviewTime = now;
            if (dt <= 0f || dt > 0.1f)
                dt = 1f / 60f;

            if (!_particlePreviewActive)
            {
                for (int i = 0; i < _previewParticleRoots.Count; i++)
                {
                    var ps = _previewParticleRoots[i];
                    if (ps == null) continue;
                    ps.Simulate(0f, true, true, false);
                }
                _particlePreviewActive = true;
            }

            for (int i = 0; i < _previewParticleRoots.Count; i++)
            {
                var ps = _previewParticleRoots[i];
                if (ps == null) continue;
                ps.Simulate(dt, true, false, false);
            }

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
                sceneView.Repaint();
        }

        /// <summary>Hierarchy / Scene でこの TuningObject 自身が直接選択されているか。</summary>
        private bool IsTuningObjectDirectlySelected()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return false;

            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == gameObject)
                    return true;
            }

            return false;
        }

        private void CollectPreviewParticleRoots()
        {
            _previewParticleRoots.Clear();
            if (targetTransforms == null) return;

            for (int i = 0; i < targetTransforms.Length; i++)
            {
                var entry = targetTransforms[i];
                if (entry == null || entry.transform == null) continue;

                var root = entry.transform;
                var systems = root.GetComponentsInChildren<ParticleSystem>(true);
                for (int j = 0; j < systems.Length; j++)
                {
                    var ps = systems[j];
                    if (ps == null || !IsParticleRootUnder(ps, root)) continue;
                    if (!_previewParticleRoots.Contains(ps))
                        _previewParticleRoots.Add(ps);
                }
            }
        }

        /// <summary>
        /// targetRoot 配下で、親方向に別の ParticleSystem が無いルートを判定する。
        /// </summary>
        private static bool IsParticleRootUnder(ParticleSystem ps, Transform targetRoot)
        {
            for (var p = ps.transform.parent; p != null; p = p.parent)
            {
                if (p.GetComponent<ParticleSystem>() != null)
                    return false;
                if (p == targetRoot)
                    break;
                if (!p.IsChildOf(targetRoot))
                    break;
            }

            return true;
        }

        /// <summary>
        /// プレビュー更新を終了する。Stop() は呼ばず Clear のみ行い、パーティクル本体選択時の再生に干渉しない。
        /// </summary>
        private void ReleaseParticlePreview()
        {
            if (_previewParticleRoots.Count == 0)
                CollectPreviewParticleRoots();

            for (int i = 0; i < _previewParticleRoots.Count; i++)
            {
                var ps = _previewParticleRoots[i];
                if (ps == null) continue;
                ps.Clear(true);
            }

            _previewParticleRoots.Clear();
            _particlePreviewActive = false;
            _lastParticlePreviewTime = -1;
        }

        private void StopParticlePreview()
        {
            ReleaseParticlePreview();
        }

        private void OnDrawGizmos()
        {
            if (!IsSelfOrParentSelected()) return;
            DrawGizmosInternal();
        }

        private bool IsSelfOrParentSelected()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0) return false;

            for (int i = 0; i < selected.Length; i++)
            {
                var go = selected[i];
                if (go == null) continue;
                if (go == gameObject) return true;
                if (transform.IsChildOf(go.transform)) return true;
            }

            return false;
        }

        private void DrawGizmosInternal()
        {
            var origin = transform.position;

            if (showMesh && previewMesh != null)
                DrawTranslucentMesh();

            if (showSphere && sphereRadius > 0f)
                DrawSphere();

            if (showBox && boxSize.sqrMagnitude > 1e-10f)
                DrawBox();

            if (showCapsule && capsuleRadius > 0f)
                DrawCapsule();

            if (arrows != null)
            {
                for (int i = 0; i < arrows.Count; i++)
                {
                    var arrow = arrows[i];
                    if (arrow == null) continue;
                    DrawArrow(origin, arrow);
                }
            }

            if (showLabel && !string.IsNullOrEmpty(labelText))
                DrawLabel(origin);
        }

        private void DrawSphere()
        {
            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = sphereColor;
            Gizmos.DrawSphere(Vector3.zero, sphereRadius);
            DrawWireCircle(Vector3.zero, Vector3.right, Vector3.up, sphereRadius);
            DrawWireCircle(Vector3.zero, Vector3.right, Vector3.forward, sphereRadius);
            DrawWireCircle(Vector3.zero, Vector3.up, Vector3.forward, sphereRadius);
            Gizmos.matrix = prevMatrix;
        }

        private void DrawBox()
        {
            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = boxColor;
            Gizmos.DrawCube(Vector3.zero, boxSize);
            Gizmos.DrawWireCube(Vector3.zero, boxSize);
            Gizmos.matrix = prevMatrix;
        }

        private void DrawCapsule()
        {
            var radius = capsuleRadius;
            var height = Mathf.Max(capsuleHeight, radius * 2f);
            var straight = height - radius * 2f;
            GetCapsuleAxes(out var axis, out var right, out var forward);
            var top = axis * (straight * 0.5f);
            var bottom = -top;

            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = capsuleColor;
            Gizmos.DrawSphere(top, radius);
            Gizmos.DrawSphere(bottom, radius);

            if (straight > 1e-5f)
            {
                var mesh = GetCylinderMesh();
                if (mesh != null)
                {
                    // 円柱メッシュは半径 0.5・高さ 2。スケールは localToWorldMatrix が掛ける
                    var align = Quaternion.FromToRotation(Vector3.up, axis);
                    Gizmos.DrawMesh(mesh, Vector3.zero, align, new Vector3(radius * 2f, straight * 0.5f, radius * 2f));
                }
            }

            DrawWireCapsule(top, bottom, axis, right, forward, radius);
            Gizmos.matrix = prevMatrix;
        }

        private void GetCapsuleAxes(out Vector3 axis, out Vector3 right, out Vector3 forward)
        {
            switch (capsuleDirection)
            {
                case CapsuleDirection.X:
                    axis = Vector3.right;
                    right = Vector3.up;
                    forward = Vector3.forward;
                    break;
                case CapsuleDirection.Z:
                    axis = Vector3.forward;
                    right = Vector3.right;
                    forward = Vector3.up;
                    break;
                default:
                    axis = Vector3.up;
                    right = Vector3.right;
                    forward = Vector3.forward;
                    break;
            }
        }

        private static void DrawWireCapsule(Vector3 top, Vector3 bottom, Vector3 axis, Vector3 right, Vector3 forward, float radius)
        {
            Gizmos.DrawLine(top + right * radius, bottom + right * radius);
            Gizmos.DrawLine(top - right * radius, bottom - right * radius);
            Gizmos.DrawLine(top + forward * radius, bottom + forward * radius);
            Gizmos.DrawLine(top - forward * radius, bottom - forward * radius);

            DrawWireCircle(top, right, forward, radius);
            DrawWireCircle(bottom, right, forward, radius);
            DrawWireSemicircle(top, right, axis, radius);
            DrawWireSemicircle(top, forward, axis, radius);
            DrawWireSemicircle(bottom, right, -axis, radius);
            DrawWireSemicircle(bottom, forward, -axis, radius);
        }

        private static void DrawWireCircle(Vector3 center, Vector3 right, Vector3 forward, float radius)
        {
            const int segments = 24;
            var prev = center + right * radius;
            for (int i = 1; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var p = center + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        private static void DrawWireSemicircle(Vector3 center, Vector3 from, Vector3 toward, float radius)
        {
            const int segments = 12;
            var prev = center + from * radius;
            for (int i = 1; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI;
                var p = center + (from * Mathf.Cos(angle) + toward * Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        /// <summary>半径 0.5・高さ 2 の円柱。両面ポリゴンなのでギズモのカリングに依存しない。</summary>
        private static Mesh GetCylinderMesh()
        {
            if (s_cylinderMesh != null) return s_cylinderMesh;

            const int segments = 16;
            const float radius = 0.5f;
            const float halfHeight = 1f;

            var vertices = new Vector3[(segments + 1) * 2];
            for (int i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var x = Mathf.Cos(angle) * radius;
                var z = Mathf.Sin(angle) * radius;
                vertices[i] = new Vector3(x, halfHeight, z);
                vertices[i + segments + 1] = new Vector3(x, -halfHeight, z);
            }

            var triangles = new int[segments * 12];
            var t = 0;
            for (int i = 0; i < segments; i++)
            {
                int top = i;
                int nextTop = i + 1;
                int bottom = i + segments + 1;
                int nextBottom = nextTop + segments + 1;

                triangles[t++] = top;
                triangles[t++] = nextTop;
                triangles[t++] = bottom;
                triangles[t++] = nextTop;
                triangles[t++] = nextBottom;
                triangles[t++] = bottom;

                triangles[t++] = top;
                triangles[t++] = bottom;
                triangles[t++] = nextTop;
                triangles[t++] = nextTop;
                triangles[t++] = bottom;
                triangles[t++] = nextBottom;
            }

            s_cylinderMesh = new Mesh
            {
                name = "TuningObject Cylinder",
                hideFlags = HideFlags.HideAndDontSave,
            };
            s_cylinderMesh.SetVertices(vertices);
            s_cylinderMesh.SetTriangles(triangles, 0);
            s_cylinderMesh.RecalculateNormals();
            return s_cylinderMesh;
        }

        private void DrawArrow(Vector3 origin, ArrowGizmo arrow)
        {
            var dir = arrow.direction;
            if (dir.sqrMagnitude < 1e-10f || arrow.length <= 0f) return;

            dir.Normalize();
            var prevMatrix = Gizmos.matrix;
            if (arrow.localSpace)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
            }
            else
            {
                var scale = MaxAbsComponent(transform.lossyScale);
                Gizmos.matrix = Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);
            }

            origin = Vector3.zero;
            var tip = dir * arrow.length;

            Gizmos.color = arrow.color;
            Gizmos.DrawLine(origin, tip);

            var headSize = arrow.headSize > 0f ? arrow.headSize : arrow.length * 0.2f;
            var headBase = tip - dir * headSize;

            var side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-6f)
                side = Vector3.Cross(dir, Vector3.right);
            side.Normalize();
            var up = Vector3.Cross(side, dir).normalized;

            var half = headSize * 0.5f;
            Gizmos.DrawLine(tip, headBase + side * half);
            Gizmos.DrawLine(tip, headBase - side * half);
            Gizmos.DrawLine(tip, headBase + up * half);
            Gizmos.DrawLine(tip, headBase - up * half);
            Gizmos.DrawLine(headBase + side * half, headBase - side * half);
            Gizmos.DrawLine(headBase + up * half, headBase - up * half);
            Gizmos.matrix = prevMatrix;
        }

        private static float MaxAbsComponent(Vector3 scale)
        {
            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        private void DrawLabel(Vector3 origin)
        {
            var cam = SceneView.currentDrawingSceneView != null
                ? SceneView.currentDrawingSceneView.camera
                : Camera.current;

            Vector3 right = Vector3.right;
            Vector3 down = Vector3.down;
            if (cam != null)
            {
                right = cam.transform.right;
                down = -cam.transform.up;
            }

            var pos = origin + (right + down) * labelOffset;
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = labelColor },
                alignment = TextAnchor.UpperLeft,
            };
            Handles.Label(pos, labelText, style);
        }

        private void DrawTranslucentMesh()
        {
            var rotation = transform.rotation * Quaternion.Euler(meshRotation);
            var position = transform.TransformPoint(meshOffset);
            var scale = Vector3.Scale(transform.lossyScale, meshScale);
            var matrix = Matrix4x4.TRS(position, rotation, scale);

            var mat = GetTranslucentMaterial();
            mat.color = meshColor;
            mat.SetPass(0);
            Graphics.DrawMeshNow(previewMesh, matrix);
        }

        private static Material GetTranslucentMaterial()
        {
            if (s_translucentMaterial != null) return s_translucentMaterial;

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            s_translucentMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = new Color(1f, 1f, 1f, 0.35f),
            };
            s_translucentMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            s_translucentMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            s_translucentMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            s_translucentMaterial.SetInt("_ZWrite", 0);
            s_translucentMaterial.renderQueue = 3000;
            return s_translucentMaterial;
        }
#endif
    }
}
