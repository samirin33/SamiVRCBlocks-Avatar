using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Samirin33.NDMF.Animation;
using Samirin33.NDMF.Base;
using Samirin33.NDMF.Constraints;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// AxisToParameter のビルド時処理。
    /// 計測方式（<see cref="AxisSensor"/>）が Sender の座標に比例するパラメーターを用意し、
    /// Direct BlendTree でその線形結合（項目ごとに不連続点を真後ろへ回した sin / cos）を AAP に書き、
    /// 不連続点の左右で分けた 2D Simple Directional で角度ごとの出力値をブレンドする。
    /// 角度は sin / cos の比だけで決まるので、計測方式は座標に線形でさえあれば単位や倍率を問わない。
    /// </summary>
    [InitializeOnLoad]
    public static class AxisToParameterProcessor
    {
        private const string ParameterRoot = "SB_AxisToParameter";
        private const string OneParameter = ParameterRoot + "/One";
        private const string ModuleObjectName = "AxisToParameter_Module";
        private const string ControllerName = "AxisToParameter_Generated";
        private const string LayerName = "AxisToParameter";
        private static string GeneratedFolder => "Assets/Generated/SamiVRCBlocks/AxisToParameter";

        /// <summary>Sender が回る半径。</summary>
        public const float OrbitRadius = 0.05f;

        /// <summary>リグ同士の間隔。Receiver の Box が重ならない距離。</summary>
        private const float UnitSpacing = 0.2f;

        /// <summary>
        /// Simple Directional のサンプル間隔。隣り合うサンプル間は弦上の直線補間になるため、
        /// 角度に対する誤差は最大でも間隔の約 0.1%（15° で約 0.016°）。
        /// </summary>
        private const float SampleStep = 15f;

        /// <summary>
        /// P, Q（sin / cos 成分）にかける倍率。Simple Directional は中心にモーションが無いと、
        /// 入力がサンプルの円（半径 1）より内側の分だけ全サンプルの平均へ寄るため、円の外側へ出して向きだけでブレンドさせる。
        /// 他の軸の回転で成分の長さが 1/100 未満（傾き約 89.4° 以上）になるまでは円の外に収まる。
        /// </summary>
        private const float DirectionScale = 100f;

        /// <summary>不連続点の左右を切り替える幅（P の値）。この幅の中だけ左右の値が混ざる。</summary>
        private const float SeamEpsilon = 0.001f;

        private const string IsActiveProperty = "IsActive";
        private const string EnabledProperty = "m_Enabled";

        private sealed class Unit
        {
            public Transform Target;
            /// <summary>コンポーネントに指定された基準。null ならワールド（読み込み時の向きで固定したアンカー）。</summary>
            public Transform Reference;
            /// <summary>回転 0° のとき Sender がある方向（計測空間の +Y か +Z）。</summary>
            public Vector3 SenderDirection;
            /// <summary>読む座標成分（計測空間の X / Y / Z）。</summary>
            public readonly bool[] Channels = new bool[3];
            public string ParameterPrefix;
        }

        private sealed class EntryPlan
        {
            public AxisToParameter.AxisEntry Entry;
            public Unit Unit;
            public string OutputParameter;
            public int SinAxis;
            public float SinSign;
            public int CosAxis;
            public string PParameter;
            public string QParameter;
        }

        private sealed class Plan
        {
            /// <summary>Animator とリグで同じ計測方式を使うよう、計画時に固定する。</summary>
            public AxisSensor Sensor;
            public readonly List<Unit> Units = new List<Unit>();
            public readonly List<EntryPlan> Entries = new List<EntryPlan>();
            public readonly List<Behaviour> Constraints = new List<Behaviour>();
            public readonly List<Behaviour> Contacts = new List<Behaviour>();
            public bool RigBuilt;
        }

        private sealed class AvatarState
        {
            public int ComponentCount;
            public int UnitCount;
            public readonly HashSet<string> OutputParameters = new HashSet<string>();
        }

        // Generating で立てた計画を Transforming / Optimizing でも使い、Animator と Contact のパラメーター名を必ず一致させる。
        private static readonly ConditionalWeakTable<AxisToParameter, Plan> Plans = new ConditionalWeakTable<AxisToParameter, Plan>();

        // ビルドごとにアバタールートは複製されるので、複製をキーにすればビルド間で番号が持ち越されない。
        private static readonly ConditionalWeakTable<GameObject, AvatarState> AvatarStates = new ConditionalWeakTable<GameObject, AvatarState>();

        static AxisToParameterProcessor()
        {
            AxisToParameter.GenerateAnimatorHandler = GenerateAnimator;
            AxisToParameter.BuildRigHandler = BuildRig;
            AxisToParameter.RemapAnimationHandler = RemapAnimations;
        }

        #region Plan

        /// <summary>
        /// 回転軸ごとの計測に使う Sender の方向と、sin / cos として読む座標成分。
        /// 成分は計測空間の正の軸で読むため、負の向きは sinSign で表す。
        /// </summary>
        private static void GetLayout(AxisToParameter.Axis axis, out Vector3 senderDirection, out int sinAxis, out float sinSign, out int cosAxis)
        {
            AxisToParameter.GetMeasureDirections(axis, out var zero, out var quarter);
            senderDirection = zero;
            cosAxis = DominantAxis(zero);
            sinAxis = DominantAxis(quarter);
            sinSign = Mathf.Sign(quarter[sinAxis]);
        }

        private static int DominantAxis(Vector3 v)
        {
            var ax = Mathf.Abs(v.x);
            var ay = Mathf.Abs(v.y);
            var az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return 0;
            return ay >= az ? 1 : 2;
        }

        private static Plan CreatePlan(AxisToParameter component, Transform avatarRoot, int componentIndex, HashSet<string> usedOutputs, bool report)
        {
            var plan = new Plan { Sensor = AxisSensors.Current };
            var prefix = ParameterRoot + "/" + componentIndex;
            if (component.entries == null)
                return plan;

            for (var i = 0; i < component.entries.Count; i++)
            {
                var entry = component.entries[i];
                if (entry == null)
                    continue;

                var label = $"[AxisToParameter] {i + 1} 番目の項目";
                var outputName = entry.parameterName?.Trim();
                if (string.IsNullOrEmpty(outputName))
                {
                    if (report) SamirinBuildContext.ReportWarning($"{label}: パラメーター名が空のためスキップします。", component);
                    continue;
                }

                if (usedOutputs != null && usedOutputs.Contains(outputName))
                {
                    if (report)
                        SamirinBuildContext.ReportWarning(
                            $"{label}: パラメーター「{outputName}」は別の項目でも出力しているためスキップします（値が加算されて壊れるため）。",
                            component);
                    continue;
                }

                var target = component.ResolveTarget(entry);
                if (avatarRoot != null && !target.IsChildOf(avatarRoot))
                {
                    if (report) SamirinBuildContext.ReportWarning($"{label}: ターゲットがアバターの外にあるためスキップします。", component, target);
                    continue;
                }

                if (entry.reference != null && avatarRoot != null && !entry.reference.IsChildOf(avatarRoot))
                {
                    if (report) SamirinBuildContext.ReportWarning($"{label}: 基準がアバターの外にあるためスキップします。", component, entry.reference);
                    continue;
                }

                if (entry.reference == target)
                {
                    if (report) SamirinBuildContext.ReportWarning($"{label}: 基準がターゲット自身のため角度が常に 0 になります。スキップします。", component, target);
                    continue;
                }

                if (!AxisToParameter.IsValidMapping(entry))
                {
                    if (report) SamirinBuildContext.ReportWarning($"{label}: 任意範囲の角度の最大値が最小値以下のためスキップします。", component);
                    continue;
                }

                GetLayout(entry.axis, out var senderDirection, out var sinAxis, out var sinSign, out var cosAxis);
                var unit = plan.Units.FirstOrDefault(u =>
                    u.Target == target && u.Reference == entry.reference && u.SenderDirection == senderDirection);
                if (unit == null)
                {
                    unit = new Unit
                    {
                        Target = target,
                        Reference = entry.reference,
                        SenderDirection = senderDirection,
                        ParameterPrefix = prefix + "/U" + plan.Units.Count,
                    };
                    plan.Units.Add(unit);
                }

                unit.Channels[sinAxis] = true;
                unit.Channels[cosAxis] = true;

                var entryPrefix = prefix + "/E" + plan.Entries.Count;
                plan.Entries.Add(new EntryPlan
                {
                    Entry = entry,
                    Unit = unit,
                    OutputParameter = outputName,
                    SinAxis = sinAxis,
                    SinSign = sinSign,
                    CosAxis = cosAxis,
                    PParameter = entryPrefix + "/P",
                    QParameter = entryPrefix + "/Q",
                });
                usedOutputs?.Add(outputName);
            }

            return plan;
        }

        /// <summary>インスペクタ表示用に、生成される Contact / Constraint の数を見積もる。</summary>
        public static void Estimate(AxisToParameter component, out int senders, out int receivers, out int constraints)
        {
            var plan = CreatePlan(component, null, 0, new HashSet<string>(), false);
            senders = plan.Units.Count;
            receivers = plan.Units.Sum(u => plan.Sensor.CountReceivers(u.Channels));
            constraints = plan.Units.Count * (plan.Sensor.RequiresAlignedSender ? 3 : 2);
            if (plan.Units.Any(u => u.Reference == null))
                constraints++;
        }

        #endregion

        #region Animator

        private static void GenerateAnimator(AxisToParameter component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null)
                return;

            var state = AvatarStates.GetOrCreateValue(avatarRootObject);
            var plan = CreatePlan(component, avatarRootObject.transform, state.ComponentCount++, state.OutputParameters, true);
            Plans.Remove(component);
            Plans.Add(component, plan);

            if (plan.Entries.Count == 0)
            {
                SamirinBuildContext.ReportWarning("[AxisToParameter] 有効な項目が無いため何も生成しません。", component);
                return;
            }

            var controller = CreateController(plan, null);
            if (controller == null)
            {
                SamirinBuildContext.ReportError("[AxisToParameter] Animator Controller の生成に失敗しました。", component);
                return;
            }

            // Direct BlendTree の加算は Write Defaults オンが前提なので、アバターの設定には合わせない。
            var module = ModularAvatarMergeAnimatorUtility.RegisterMergeAnimatorModule(
                component.gameObject, ModuleObjectName, controller, layerPriority: 0, matchAvatarWriteDefaults: false);
            if (module == null)
                SamirinBuildContext.ReportError("[AxisToParameter] MA Merge Animator の登録に失敗しました。", component);
        }

        /// <summary>
        /// 現在の設定から Animator Controller を生成する。manualAssetPath が null ならメモリ上に作る。
        /// 確認用の書き出しとテストで使い、シーンには何も追加しない。
        /// </summary>
        /// <param name="sensor">null なら現在の SDK で使える計測方式。</param>
        public static AnimatorController CreateController(AxisToParameter component, string manualAssetPath = null, AxisSensor sensor = null)
        {
            if (component == null)
                return null;

            var avatarRoot = RuntimeUtil.FindAvatarInParents(component.transform);
            var plan = CreatePlan(component, avatarRoot, 0, new HashSet<string>(), true);
            if (sensor != null)
                plan.Sensor = sensor;
            if (plan.Entries.Count == 0)
                return null;
            return CreateController(plan, manualAssetPath);
        }

        public static AnimatorController ExportController(AxisToParameter component)
        {
            return CreateController(component, $"{GeneratedFolder}/{ControllerName}.controller");
        }

        private static AnimatorController CreateController(Plan plan, string manualAssetPath)
        {
            var controller = ModularAvatarMergeAnimatorUtility.CreateGeneratedController(ControllerName, manualAssetPath);
            if (controller == null)
                return null;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = OneParameter,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });
            foreach (var unit in plan.Units)
            {
                for (var axis = 0; axis < 3; axis++)
                {
                    if (!unit.Channels[axis])
                        continue;
                    var channel = plan.Sensor.GetChannel(unit.ParameterPrefix, axis);
                    AddFloatParameter(controller, channel.Parameter, channel.RestValue);
                }
            }

            foreach (var entry in plan.Entries)
            {
                AddFloatParameter(controller, entry.PParameter, 0f);
                AddFloatParameter(controller, entry.QParameter, 0f);
                AddFloatParameter(controller, entry.OutputParameter, 0f);
            }

            controller.RemoveLayer(0);

            var root = new BlendTree
            {
                name = LayerName,
                blendType = BlendTreeType.Direct,
                useAutomaticThresholds = false,
            };
            AnimatorControllerAssetUtility.EnsureSubAsset(root, controller);

            var children = new List<ChildMotion>();
            foreach (var (weightParameter, values) in CollectLinearTerms(plan))
            {
                var clip = CreateParameterClip("Linear " + weightParameter, values, controller);
                children.Add(new ChildMotion { motion = clip, directBlendParameter = weightParameter, timeScale = 1f });
            }

            var clipCache = new Dictionary<(string, float), AnimationClip>();
            foreach (var entry in plan.Entries)
            {
                var tree = CreateAngleTree(entry, controller, clipCache);
                children.Add(new ChildMotion { motion = tree, directBlendParameter = OneParameter, timeScale = 1f });
            }

            root.children = children.ToArray();

            var stateMachine = new AnimatorStateMachine { name = LayerName };
            var state = stateMachine.AddState(LayerName, new Vector3(280f, 120f, 0f));
            state.writeDefaultValues = true;
            state.motion = root;
            stateMachine.defaultState = state;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = LayerName,
                defaultWeight = 1f,
                stateMachine = stateMachine,
            });

            AnimatorControllerAssetUtility.RegisterControllerHierarchy(controller);
            return ModularAvatarMergeAnimatorUtility.FinalizeGeneratedController(controller);
        }

        private static void AddFloatParameter(AnimatorController controller, string name, float defaultValue)
        {
            if (controller.parameters.Any(p => p.name == name))
                return;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = defaultValue,
            });
        }

        /// <summary>
        /// 不連続点 σ の向き d = (cosσ, sinσ)（sin / cos 平面）に対し、P = d⊥ 成分、Q = -d 成分を作る。
        /// atan2(P, Q) = θ - σ - 180° なので、不連続点はちょうど Q 軸の負側（P = 0, Q &lt; 0）に来る。
        /// S = sinSign(scale_s·a_s + offset_s), C = scale_c·a_c + offset_c を代入すると、計測パラメーター a と定数 1 の線形結合になる。
        /// </summary>
        private static List<(string weight, Dictionary<string, float> values)> CollectLinearTerms(Plan plan)
        {
            var order = new List<string>();
            var terms = new Dictionary<string, Dictionary<string, float>>();

            void Add(string weight, string parameter, float coefficient)
            {
                if (Mathf.Abs(coefficient) < 1e-6f)
                    return;
                if (!terms.TryGetValue(weight, out var values))
                {
                    values = new Dictionary<string, float>();
                    terms.Add(weight, values);
                    order.Add(weight);
                }

                values.TryGetValue(parameter, out var current);
                values[parameter] = current + coefficient;
            }

            foreach (var entry in plan.Entries)
            {
                var seam = AxisToParameter.GetSeamAngle(entry.Entry) * Mathf.Deg2Rad;
                var sinSeam = Mathf.Sin(seam) * DirectionScale;
                var cosSeam = Mathf.Cos(seam) * DirectionScale;
                var k = entry.SinSign;
                var sin = plan.Sensor.GetChannel(entry.Unit.ParameterPrefix, entry.SinAxis);
                var cos = plan.Sensor.GetChannel(entry.Unit.ParameterPrefix, entry.CosAxis);

                // P = sinσ·C - cosσ·S
                Add(cos.Parameter, entry.PParameter, sinSeam * cos.Scale);
                Add(sin.Parameter, entry.PParameter, -cosSeam * k * sin.Scale);
                Add(OneParameter, entry.PParameter, sinSeam * cos.Offset - cosSeam * k * sin.Offset);

                // Q = -(cosσ·C + sinσ·S)
                Add(cos.Parameter, entry.QParameter, -cosSeam * cos.Scale);
                Add(sin.Parameter, entry.QParameter, -sinSeam * k * sin.Scale);
                Add(OneParameter, entry.QParameter, -cosSeam * cos.Offset - sinSeam * k * sin.Offset);
            }

            return order.Select(w => (w, terms[w])).ToList();
        }

        /// <summary>
        /// P の符号で不連続点の左右（φ = atan2(P, Q) が負 / 正）を切り替え、それぞれを Simple Directional でブレンドする。
        /// 左右のツリーは境界の外側に 1 サンプルずつはみ出させ、切り替え幅の中でも隣接サンプルが常に存在するようにする。
        /// </summary>
        private static BlendTree CreateAngleTree(EntryPlan entry, AnimatorController controller, Dictionary<(string, float), AnimationClip> clipCache)
        {
            var left = CreateDirectionalTree(entry, "Left", -180f - SampleStep, SampleStep, 0f, 180f, controller, clipCache);
            var right = CreateDirectionalTree(entry, "Right", -SampleStep, 180f + SampleStep, 180f, 360f, controller, clipCache);

            var tree = new BlendTree
            {
                name = entry.OutputParameter + " Angle",
                blendType = BlendTreeType.Simple1D,
                blendParameter = entry.PParameter,
                useAutomaticThresholds = false,
                minThreshold = -SeamEpsilon,
                maxThreshold = SeamEpsilon,
            };
            AnimatorControllerAssetUtility.EnsureSubAsset(tree, controller);
            tree.children = new[]
            {
                new ChildMotion { motion = left, threshold = -SeamEpsilon, timeScale = 1f },
                new ChildMotion { motion = right, threshold = SeamEpsilon, timeScale = 1f },
            };
            return tree;
        }

        /// <param name="phiStart">φ（不連続点の反対側を 0° とした角度）の最初のサンプル。</param>
        /// <param name="uMin">このツリーが受け持つ、不連続点から測った角度の範囲（折れ点のサンプルを足す範囲）。</param>
        private static BlendTree CreateDirectionalTree(
            EntryPlan entry,
            string name,
            float phiStart,
            float phiEnd,
            float uMin,
            float uMax,
            AnimatorController controller,
            Dictionary<(string, float), AnimationClip> clipCache)
        {
            var angles = new List<float>();
            for (var phi = phiStart; phi <= phiEnd + 0.001f; phi += SampleStep)
                angles.Add(phi);

            // 任意範囲の端（直線とクランプの折れ点）にサンプルを置き、角を丸めない。
            foreach (var u in GetBreakpoints(entry.Entry))
            {
                if (u >= uMin - 0.001f && u <= uMax + 0.001f)
                    angles.Add(u - 180f);
            }

            angles.Sort();
            var samples = new List<float>();
            foreach (var phi in angles)
            {
                if (samples.Count == 0 || phi - samples[samples.Count - 1] > 0.01f)
                    samples.Add(phi);
            }

            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = entry.PParameter,
                blendParameterY = entry.QParameter,
                useAutomaticThresholds = false,
            };
            AnimatorControllerAssetUtility.EnsureSubAsset(tree, controller);

            tree.children = samples.Select(phi =>
            {
                var rad = phi * Mathf.Deg2Rad;
                var value = AxisToParameter.EvaluateFromSeam(entry.Entry, phi + 180f);
                return new ChildMotion
                {
                    motion = GetValueClip(entry.OutputParameter, value, controller, clipCache),
                    position = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)),
                    timeScale = 1f,
                };
            }).ToArray();
            return tree;
        }

        private static IEnumerable<float> GetBreakpoints(AxisToParameter.AxisEntry entry)
        {
            AxisToParameter.GetMapping(entry, out var angleMin, out var angleMax, out _, out _);
            var span = angleMax - angleMin;
            if (span >= 360f - 0.001f)
                yield break;

            var outside = (360f - span) * 0.5f;
            yield return outside;
            yield return outside + span;
        }

        private static AnimationClip GetValueClip(
            string parameter, float value, AnimatorController controller, Dictionary<(string, float), AnimationClip> clipCache)
        {
            if (clipCache.TryGetValue((parameter, value), out var cached))
                return cached;

            var clip = CreateParameterClip($"{parameter} {value:0.####}", new Dictionary<string, float> { { parameter, value } }, controller);
            clipCache.Add((parameter, value), clip);
            return clip;
        }

        private static AnimationClip CreateParameterClip(string name, Dictionary<string, float> values, AnimatorController controller)
        {
            var clip = new AnimationClip { name = name };
            foreach (var pair in values)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), pair.Key),
                    AnimationCurve.Constant(0f, 0f, pair.Value));
            }

            AnimatorControllerAssetUtility.EnsureSubAsset(clip, controller);
            return clip;
        }

        #endregion

        #region Rig

        private static void BuildRig(AxisToParameter component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null)
                return;
            if (!Plans.TryGetValue(component, out var plan) || plan.Units.Count == 0)
                return;

            var context = SamirinBuildContext.Current;
            if (context == null)
            {
                SamirinBuildContext.ReportError(
                    "[AxisToParameter] NDMF のビルド外では Contact の生成とアニメーションの書き換えを行えません。", component);
                return;
            }

            var state = AvatarStates.GetOrCreateValue(avatarRootObject);
            using (var animators = new SamirinAnimatorServicesScope(context))
            {
                var parent = component.transform;
                var container = CreateChild(parent, UniqueChildName(parent, "AxisToParameter"));
                var worldAnchor = plan.Units.Any(u => u.Reference == null)
                    ? CreateWorldAnchor(component, plan, avatarRootObject.transform)
                    : null;
                foreach (var unit in plan.Units)
                    BuildUnit(component, plan, unit, container, worldAnchor, state.UnitCount++);
                plan.RigBuilt = true;

                ExpandAnimations(component, plan, animators.Services);
            }
        }

        /// <summary>
        /// アバターはワールドの向きを知る手段が無いため、アバタールート直下（直立・プレイヤーの向き）に置いた
        /// オブジェクトを Freeze To World で読み込み時の向きに固定し、ワールド基準として使う。
        /// Constraint 有効をオフ→オンにすると、その時点の向きで固定し直す。
        /// </summary>
        private static Transform CreateWorldAnchor(AxisToParameter component, Plan plan, Transform avatarRoot)
        {
            var anchor = CreateChild(avatarRoot, UniqueChildName(avatarRoot, "AxisToParameter_World"));
            var constraint = AddRotationConstraint(anchor.gameObject, avatarRoot, component.constraintActive, freezeToWorld: true);
            plan.Constraints.Add(constraint);
            return anchor;
        }

        private static void BuildUnit(AxisToParameter component, Plan plan, Unit unit, Transform container, Transform worldAnchor, int globalIndex)
        {
            var target = unit.Target;
            if (target == null)
            {
                SamirinBuildContext.ReportWarning(
                    "[AxisToParameter] ターゲットがビルド中に削除されたため、このターゲットの Contact を生成できません。", component);
                return;
            }

            var reference = unit.Reference != null ? unit.Reference : worldAnchor;
            if (reference == null)
            {
                SamirinBuildContext.ReportWarning(
                    "[AxisToParameter] 基準がビルド中に削除されたため、このターゲットの Contact を生成できません。", component, target);
                return;
            }

            var tag = ParameterRoot + "_" + globalIndex;
            var suffix = unit.SenderDirection == Vector3.up ? "_Up" : string.Empty;
            var root = CreateChild(container, UniqueChildName(container, "GetAxis_(" + target.name + ")" + suffix));
            // 複数のリグの Contact が同じ座標に重ならないようにずらす。
            root.localPosition = new Vector3(globalIndex * UnitSpacing, 0f, 0f);
            plan.Constraints.Add(AddRotationConstraint(root.gameObject, reference, component.constraintActive));

            var rotation = CreateChild(root, "Rotation");
            plan.Constraints.Add(AddRotationConstraint(rotation.gameObject, target, component.constraintActive));

            var sender = CreateChild(rotation, "Sender");
            sender.localPosition = unit.SenderDirection * OrbitRadius;
            if (plan.Sensor.RequiresAlignedSender)
                plan.Constraints.Add(AddRotationConstraint(sender.gameObject, root, component.constraintActive));
            plan.Contacts.Add(AddSender(plan.Sensor, sender.gameObject, tag, component.localOnly, component.contactActive));

            plan.Contacts.AddRange(plan.Sensor.CreateReceivers(
                root, unit.ParameterPrefix, unit.Channels, tag, component.localOnly, component.contactActive));
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// <summary>
        /// 同名の兄弟があるとアニメーションのパスが先頭の同名オブジェクトに解決されるため、名前を重複させない。
        /// </summary>
        private static string UniqueChildName(Transform parent, string baseName)
        {
            var names = new HashSet<string>();
            foreach (Transform child in parent)
                names.Add(child.name);

            if (!names.Contains(baseName))
                return baseName;

            for (var i = 1; ; i++)
            {
                var candidate = baseName + " (" + i + ")";
                if (!names.Contains(candidate))
                    return candidate;
            }
        }

        private static VRCRotationConstraint AddRotationConstraint(GameObject gameObject, Transform source, bool isActive, bool freezeToWorld = false)
        {
            var raw = gameObject.AddComponent<VRCRotationConstraint>();
            raw.AffectsRotationX = true;
            raw.AffectsRotationY = true;
            raw.AffectsRotationZ = true;

            var wrapper = (VRCRotationConstraintWrapper)AllConstraint.FromComponent(raw);
            wrapper.TargetTransform = null;
            wrapper.SolveInLocalSpace = false;
            wrapper.AddSource(source, 1f);
            // ActivateConstraint は現在の姿勢を保つオフセットを焼き込むため呼ばない。オフセット 0 でソースの回転に一致させる。
            wrapper.RotationOffset = Vector3.zero;
            wrapper.RotationAtRest = Vector3.zero;
            wrapper.Weight = 1f;
            wrapper.IsActive = isActive;
            wrapper.FreezeToWorld = freezeToWorld;
            wrapper.Locked = true;
            return raw;
        }

        private static VRCContactSender AddSender(AxisSensor sensor, GameObject gameObject, string tag, bool localOnly, bool enabled)
        {
            var sender = gameObject.AddComponent<VRCContactSender>();
            sensor.ConfigureSender(sender);
            sender.localOnly = localOnly;
            sender.collisionTags = new List<string> { tag };
            sender.enabled = enabled;
            return sender;
        }

        #endregion

        #region Animation

        /// <summary>
        /// Rig 生成後に追加された Animator（VRCFury 等）にある AxisToParameter 向けカーブを、生成した Constraint / Contact へ展開する。
        /// </summary>
        private static void RemapAnimations(AxisToParameter component, GameObject avatarRootObject)
        {
            if (component == null || avatarRootObject == null)
                return;
            if (!Plans.TryGetValue(component, out var plan) || !plan.RigBuilt)
                return;

            var context = SamirinBuildContext.Current;
            if (context == null)
                return;

            using (var animators = new SamirinAnimatorServicesScope(context))
            {
                ExpandAnimations(component, plan, animators.Services);
            }
        }

        private static void ExpandAnimations(AxisToParameter component, Plan plan, AnimatorServicesContext services)
        {
            // 仮想コントローラ内のパスはスコープを開いた時点のもの。新しいカーブも同じ基準のパスで書く。
            var remapper = services.ObjectPathRemapper;
            var componentPath = remapper.GetVirtualPathForObject(component.transform);

            var constraintPaths = plan.Constraints
                .Where(c => c != null)
                .Select(c => remapper.GetVirtualPathForObject(c.transform))
                .ToList();
            var contactBindings = plan.Contacts
                .Where(c => c != null)
                .Select(c => (path: remapper.GetVirtualPathForObject(c.transform), type: c.GetType()))
                .ToList();

            var clips = new List<VirtualClip>(services.AnimationIndex.GetClipsForObjectPath(componentPath));
            foreach (var clip in clips)
            {
                if (clip == null || clip.IsMarkerClip)
                    continue;

                var bindings = new List<EditorCurveBinding>(clip.GetFloatCurveBindings());
                foreach (var binding in bindings)
                {
                    if (binding.type != typeof(AxisToParameter))
                        continue;
                    if ((binding.path ?? string.Empty) != componentPath)
                        continue;

                    var curve = clip.GetFloatCurve(binding);
                    clip.SetFloatCurve(binding, null);
                    if (curve == null)
                        continue;

                    switch (binding.propertyName)
                    {
                        case AxisToParameter.ConstraintActiveProperty:
                            foreach (var path in constraintPaths)
                            {
                                clip.SetFloatCurve(
                                    EditorCurveBinding.FloatCurve(path, typeof(VRCRotationConstraint), IsActiveProperty),
                                    CloneCurve(curve));
                            }
                            break;
                        case AxisToParameter.ContactActiveProperty:
                            foreach (var (path, type) in contactBindings)
                                clip.SetFloatCurve(EditorCurveBinding.FloatCurve(path, type, EnabledProperty), CloneCurve(curve));
                            break;
                    }
                }
            }
        }

        private static AnimationCurve CloneCurve(AnimationCurve curve)
        {
            return new AnimationCurve(curve.keys)
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode,
            };
        }

        #endregion
    }
}
