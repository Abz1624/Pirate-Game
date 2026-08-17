using System;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// Client port of pirate-sim tick.rs integrate_ship (authoritative on-chain physics).
    /// Unity ships face -Z at heading 0.
    /// </summary>
    public static class ShipPredictor
    {
        const int MaxSpeed = 50;
        const int ReverseMaxSpeed = 15;
        const int Accel = 15;
        const int ReverseAccel = 4;
        const int WaterDrag = 3;
        const int SteerScale = 127;
        const int TurnRateDegreesPerSecond = 800;

        public struct ShipState
        {
            // Q16.16 raw positions. Retaining sub-units avoids directional
            // rounding where negative movement jumped and positive movement stalled.
            public int posX;
            public int posZ;
            public ushort heading;
            public int velX;
            public int velZ;
        }

        public struct ShipInput
        {
            public sbyte throttle;
            public sbyte steer;
        }

        public static ShipState Integrate(ShipState state, ShipInput input, int tickHz)
        {
            var hz = Math.Max(1, tickHz);
            var dt = SimFix.FromInt(1).Div(SimFix.FromInt(hz));
            var maxSpeed = SimFix.FromInt(MaxSpeed);
            var accel = SimFix.FromInt(Accel);
            var waterDrag = SimFix.FromInt(WaterDrag);
            var turnRate = SimFix.FromInt(TurnRateDegreesPerSecond);

            var heading = state.heading;
            var velX = new SimFix(state.velX);
            var velZ = new SimFix(state.velZ);

            var forward = Forward(heading);
            var forwardSpeed = velX.Mul(forward.x).Add(velZ.Mul(forward.z));
            var reverseMax = SimFix.FromInt(ReverseMaxSpeed);
            var turnReference = forwardSpeed.Raw >= 0 ? maxSpeed : reverseMax;
            var turnEff = SimFix.Zero;
            if (turnReference.Raw > 0)
            {
                turnEff = forwardSpeed.Abs().Div(turnReference);
                if (turnEff.Raw > SimFix.One.Raw)
                    turnEff = SimFix.One;
            }
            if (input.steer != 0 && turnEff.Raw > 0)
            {
                var steer = SimFix.FromInt(input.steer).Div(SimFix.FromInt(SteerScale));
                var headingDelta = steer.Mul(turnRate).Mul(turnEff).Mul(dt)
                    .Mul(SimFix.FromInt(1024)).Div(SimFix.FromInt(360));
                heading = (ushort)((heading + headingDelta.ToInt()) & 1023);
            }

            if (input.throttle != 0)
            {
                var thrustForward = Forward(heading);
                var thrust = input.throttle > 0 ? accel : SimFix.FromInt(ReverseAccel);
                var t = SimFix.FromInt(input.throttle).Div(SimFix.FromInt(SteerScale));
                velX = velX.Add(thrustForward.x.Mul(t).Mul(thrust).Mul(dt));
                velZ = velZ.Add(thrustForward.z.Mul(t).Mul(thrust).Mul(dt));

                var fwdSpeed = velX.Mul(thrustForward.x).Add(velZ.Mul(thrustForward.z));
                var opposing = (input.throttle > 0 && fwdSpeed.Raw < 0)
                    || (input.throttle < 0 && fwdSpeed.Raw > 0);
                if (opposing && fwdSpeed.Raw != 0)
                {
                    var brake = SimFix.One.Sub(SimFix.FromInt(6).Mul(dt));
                    velX = velX.Mul(brake);
                    velZ = velZ.Mul(brake);
                }
            }

            var drag = SimFix.One.Sub(waterDrag.Mul(dt));
            if (drag.Raw < 0) drag = SimFix.Zero;
            velX = velX.Mul(drag);
            velZ = velZ.Mul(drag);

            var speedSq = velX.Mul(velX).Add(velZ.Mul(velZ));
            var maxSq = maxSpeed.Mul(maxSpeed);
            if (speedSq.Raw > maxSq.Raw)
            {
                var speed = Sqrt(speedSq);
                if (speed.Raw > 0)
                {
                    velX = velX.Mul(maxSpeed).Div(speed);
                    velZ = velZ.Mul(maxSpeed).Div(speed);
                }
            }

            var posX = new SimFix(state.posX).Add(velX.Mul(dt));
            var posZ = new SimFix(state.posZ).Add(velZ.Mul(dt));

            return new ShipState
            {
                posX = posX.Raw,
                posZ = posZ.Raw,
                heading = heading,
                velX = velX.Raw,
                velZ = velZ.Raw
            };
        }

        public static Vector3 ToWorldPosition(in ShipState s) =>
            new(s.posX / 65536f, 0f, s.posZ / 65536f);

        public static float HeadingToDegrees(ushort heading) => heading / 1024f * 360f;

        public static ShipState FromSnapshot(short x, short z, ushort heading)
        {
            return new ShipState
            {
                posX = SimFix.FromInt(x).Raw,
                posZ = SimFix.FromInt(z).Raw,
                heading = heading
            };
        }

        static (SimFix x, SimFix z) Forward(ushort angle)
        {
            var (fx, fz) = SinCos(angle);
            return (new SimFix(-fx.Raw), new SimFix(-fz.Raw));
        }

        static (SimFix sin, SimFix cos) SinCos(ushort angle)
        {
            var idx = angle >> 2;
            var frac = angle & 3;
            var sin0 = SinTable[idx & 255];
            var sin1 = SinTable[(idx + 1) & 255];
            var sinV = sin0 + (sin1 - sin0) * frac / 4;
            var cos0 = SinTable[(idx + 64) & 255];
            var cos1 = SinTable[(idx + 65) & 255];
            var cosV = cos0 + (cos1 - cos0) * frac / 4;
            return (new SimFix(sinV), new SimFix(cosV));
        }

        static SimFix Sqrt(SimFix v)
        {
            if (v.Raw <= 0) return SimFix.Zero;
            var n = (ulong)(uint)v.Raw << 16;
            var x = n;
            var y = (x + 1) >> 1;
            while (y < x)
            {
                x = y;
                y = (x + n / x) >> 1;
            }
            return new SimFix((int)x);
        }

        static readonly int[] SinTable = BuildSinTable();

        static int[] BuildSinTable()
        {
            var t = new int[256];
            for (var i = 0; i < 256; i++)
            {
                int s;
                if (i <= 64) s = i * 4;
                else if (i <= 128) s = (128 - i) * 4;
                else if (i <= 192) s = (i - 128) * -4;
                else s = (256 - i) * -4;
                t[i] = s * 256;
            }
            return t;
        }

        readonly struct SimFix
        {
            public readonly int Raw;
            public SimFix(int raw) => Raw = raw;
            public static SimFix Zero => default;
            public static SimFix One => new SimFix(1 << 16);

            public static SimFix FromInt(int n) => new SimFix(n << 16);
            public static SimFix FromFraction(float value, float divisor)
            {
                if (divisor <= 0f) return Zero;
                return new SimFix((int)(value / divisor * (1 << 16)));
            }

            public int ToInt() => Raw >> 16;
            public SimFix Abs() => new SimFix(Math.Abs(Raw));

            public SimFix Add(SimFix o) => new SimFix(Raw + o.Raw);
            public SimFix Sub(SimFix o) => new SimFix(Raw - o.Raw);
            public SimFix Mul(SimFix o) => new SimFix((int)((long)Raw * o.Raw >> 16));
            public SimFix Div(SimFix o) => o.Raw == 0 ? Zero : new SimFix((int)(((long)Raw << 16) / o.Raw));
        }
    }
}
