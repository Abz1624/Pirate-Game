using System;

namespace RoguePirates.Simulation
{
    /// <summary>Fixed-point value scaled by 1000 (see plan/NUMERIC_CONTRACT.md).</summary>
    public readonly struct SimFixed : IEquatable<SimFixed>, IComparable<SimFixed>
    {
        public const int Scale = 1000;
        public long Raw { get; }

        public SimFixed(long raw) => Raw = raw;

        public static SimFixed FromFloat(float value) =>
            new((long)Math.Round(value * Scale, MidpointRounding.AwayFromZero));

        public static SimFixed FromInt(int value) => new(value * Scale);

        public float ToFloat() => Raw / (float)Scale;

        public int ToRoundedInt() => (int)Math.Round(Raw / (double)Scale, MidpointRounding.AwayFromZero);

        public SimFixed Add(SimFixed other) => new(Raw + other.Raw);
        public SimFixed Sub(SimFixed other) => new(Raw - other.Raw);
        public SimFixed Mul(SimFixed other) => new(Raw * other.Raw / Scale);
        public SimFixed Mul(float scalar) => FromFloat(ToFloat() * scalar);

        public static SimFixed Max(SimFixed a, SimFixed b) => a.Raw >= b.Raw ? a : b;
        public static SimFixed Min(SimFixed a, SimFixed b) => a.Raw <= b.Raw ? a : b;

        public static SimFixed operator +(SimFixed a, SimFixed b) => a.Add(b);
        public static SimFixed operator -(SimFixed a, SimFixed b) => a.Sub(b);
        public static SimFixed operator *(SimFixed a, SimFixed b) => a.Mul(b);
        public static SimFixed operator *(SimFixed a, float b) => a.Mul(b);

        public bool Equals(SimFixed other) => Raw == other.Raw;
        public int CompareTo(SimFixed other) => Raw.CompareTo(other.Raw);
        public override bool Equals(object obj) => obj is SimFixed f && Equals(f);
        public override int GetHashCode() => Raw.GetHashCode();
        public override string ToString() => ToFloat().ToString("0.###");
    }
}
