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
    /// 自身または親が選択されているとき、矢印・中心球・任意テキスト・半透明メッシュを Scene ビューに描画する。
    /// active 時は targetTransforms に自身+Offset を適用する。
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

        [Serializable]
        public class ArrowGizmo
        {
            [Tooltip("矢印の向き（ゼロベクトルの場合は描画しない）")]
            public Vector3 direction = Vector3.forward;

            [Tooltip("矢印の長さ")]
            [Min(0f)]
            public float length = 0.1f;

            [Tooltip("矢印先端（ヘッド）の大きさ")]
            [Min(0f)]
            public float headSize = 0.02f;

            [Tooltip("矢印の色")]
            public Color color = Color.cyan;

            [Tooltip("true ならローカル空間、false ならワールド空間で direction を解釈する")]
            public bool localSpace = true;
        }

        [Tooltip("true のとき、Target に自身の Transform + Offset を継続適用する")]
        public bool active;

        [Header("Target Transforms")]
        public TargetTransform[] targetTransforms;

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

        [Tooltip("中心球の半径")]
        [Min(0f)]
        public float sphereRadius = 0.015f;

        [Tooltip("中心球の色")]
        public Color sphereColor = new Color(1f, 0.85f, 0.2f, 0.9f);

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

        private void Update()
        {
            if (!active) return;
#if UNITY_EDITOR
            // エディタでは EditorUpdate 側で適用する（配置スナップ待ち中に Target を動かさない）
            if (!Application.isPlaying) return;
#endif
            ApplyToTargets();
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

        private const double PlacementAlignDelaySeconds = 0.3;
        private const double PlacementAlignGiveUpSeconds = 5.0;
        private const int PlacementAlignMinFrames = 8;

        /// <summary>シーン上で一度スナップ済みか。Prefab アセット上では常に false に保つ。</summary>
        [SerializeField, HideInInspector]
        private bool _placementSnapCompleted;

        private bool _waitingForPlacementAlign;
        private double _placementAlignStartTime;
        private int _placementAlignFrames;

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
        }

        private void Reset()
        {
            SchedulePlacementSnap();
        }

        private void OnValidate()
        {
            // Prefab アセットに「済」フラグが焼き付くと、以降の配置でスナップしなくなる
            if (EditorUtility.IsPersistent(gameObject) || PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                if (_placementSnapCompleted)
                    _placementSnapCompleted = false;
            }
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

                if (active)
                    ApplyToTargets();
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

            if (active)
                ApplyToTargets();
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
            {
                Gizmos.color = sphereColor;
                Gizmos.DrawSphere(origin, sphereRadius);
                Gizmos.DrawWireSphere(origin, sphereRadius);
            }

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

        private void DrawArrow(Vector3 origin, ArrowGizmo arrow)
        {
            var dir = arrow.direction;
            if (dir.sqrMagnitude < 1e-10f || arrow.length <= 0f) return;

            if (arrow.localSpace)
                dir = transform.TransformDirection(dir);

            dir.Normalize();
            var tip = origin + dir * arrow.length;

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
