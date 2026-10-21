using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// 計測空間での Sender の座標 1 成分を表す Animator パラメーター。
    /// Scale * 値 + Offset が座標 / OrbitRadius（±1 程度）になるようにする。
    /// 角度は成分の比だけで決まるので倍率が多少ずれても構わないが、すべての成分で共通でなければならない。
    /// </summary>
    public readonly struct AxisSensorChannel
    {
        public readonly string Parameter;
        public readonly float Scale;
        public readonly float Offset;

        public AxisSensorChannel(string parameter, float scale, float offset)
        {
            Parameter = parameter;
            Scale = scale;
            Offset = offset;
        }

        /// <summary>座標 0 に当たる値。Contact が動く前の初期値に使う。</summary>
        public float RestValue => -Offset / Scale;
    }

    /// <summary>
    /// Contact で Sender の座標を Animator パラメーターに読み出す方法。
    /// 計測空間はリグのルート（Receiver を置く Transform）のローカル空間で、Sender は原点から OrbitRadius の位置を回る。
    /// </summary>
    public abstract class AxisSensor
    {
        public abstract string DisplayName { get; }

        /// <summary>今の VRChat SDK で使えるか。</summary>
        public abstract bool IsSupported { get; }

        /// <summary>Sender の向きを計測空間に揃える Constraint が要るか。</summary>
        public abstract bool RequiresAlignedSender { get; }

        /// <param name="parameterPrefix">リグごとに一意なパラメーター名の接頭辞。</param>
        /// <param name="axis">計測空間の成分（0: X, 1: Y, 2: Z）。</param>
        public abstract AxisSensorChannel GetChannel(string parameterPrefix, int axis);

        /// <param name="channels">使う成分。false の成分のパラメーターは読まない。</param>
        public abstract int CountReceivers(bool[] channels);

        public abstract void ConfigureSender(VRCContactSender sender);

        /// <summary>root の子に Receiver を作り、作った Contact を返す。</summary>
        public abstract IEnumerable<VRCContactReceiver> CreateReceivers(
            Transform root, string parameterPrefix, bool[] channels, string collisionTag, bool localOnly, bool enabled);
    }

    /// <summary>
    /// 成分ごとに Box 型 Receiver を置き、Face Proximity（+Z 面で 1、-Z 面で 0 の線形）で座標を読む。
    /// Sender の Box の最大側の角を原点に置き向きを計測空間に揃えると、+X/+Y/+Z を向いた Receiver から見た最近点が常に Sender の原点になる。
    /// </summary>
    public sealed class BoxProximityAxisSensor : AxisSensor
    {
        public const float SenderSize = 0.01f;

        private static readonly string[] AxisNames = { "X", "Y", "Z" };

        /// <summary>Receiver の +Z 面を計測空間の +X / +Y / +Z へ向ける回転。</summary>
        private static readonly Quaternion[] ReceiverRotations =
        {
            Quaternion.Euler(0f, 90f, 0f),
            Quaternion.Euler(-90f, 0f, 0f),
            Quaternion.identity,
        };

        public override string DisplayName => "Box Contact の Face Proximity（成分ごとに Receiver 1 つ）";
        public override bool IsSupported => true;
        public override bool RequiresAlignedSender => true;

        /// <summary>Receiver の Box の一辺。軸と平行になったときも Sender が Box の外へ出ないよう軌道の直径より少し大きくする。</summary>
        public static float ReceiverSize => AxisToParameterProcessor.OrbitRadius * 2f * 1.05f;

        public static string ParameterName(string parameterPrefix, int axis) => parameterPrefix + "/" + AxisNames[axis];

        public override AxisSensorChannel GetChannel(string parameterPrefix, int axis)
        {
            return new AxisSensorChannel(ParameterName(parameterPrefix, axis), 2f, -1f);
        }

        public override int CountReceivers(bool[] channels) => channels.Count(c => c);

        public override void ConfigureSender(VRCContactSender sender)
        {
            sender.shapeType = ContactBase.ShapeType.Box;
            sender.size = Vector3.one * SenderSize;
            sender.position = -Vector3.one * (SenderSize * 0.5f);
            sender.rotation = Quaternion.identity;
        }

        public override IEnumerable<VRCContactReceiver> CreateReceivers(
            Transform root, string parameterPrefix, bool[] channels, string collisionTag, bool localOnly, bool enabled)
        {
            var receivers = new List<VRCContactReceiver>();
            for (var axis = 0; axis < 3; axis++)
            {
                if (!channels[axis])
                    continue;

                var child = new GameObject("Receiver_" + AxisNames[axis]).transform;
                child.SetParent(root, false);
                var receiver = child.gameObject.AddComponent<VRCContactReceiver>();
                receiver.shapeType = ContactBase.ShapeType.Box;
                receiver.size = Vector3.one * ReceiverSize;
                receiver.position = Vector3.zero;
                receiver.rotation = ReceiverRotations[axis];
                receiver.localOnly = localOnly;
                receiver.collisionTags = new List<string> { collisionTag };
                receiver.allowSelf = true;
                receiver.allowOthers = false;
                receiver.useFaceProximity = true;
                receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
                receiver.parameter = GetChannel(parameterPrefix, axis).Parameter;
                receiver.enabled = enabled;
                receivers.Add(receiver);
            }

            return receivers;
        }
    }

    public static class AxisSensors
    {
        /// <summary>優先順。先頭から IsSupported のものを使う。</summary>
        private static readonly AxisSensor[] Candidates =
        {
            new BoxProximityAxisSensor(),
        };

        public static AxisSensor Current => Candidates.First(s => s.IsSupported);
    }
}
