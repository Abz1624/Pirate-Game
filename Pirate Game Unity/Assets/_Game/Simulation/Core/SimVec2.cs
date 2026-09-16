namespace RoguePirates.Simulation
{
    public readonly struct SimVec2
    {
        public SimFixed X { get; }
        public SimFixed Y { get; }

        public SimVec2(SimFixed x, SimFixed y)
        {
            X = x;
            Y = y;
        }

        public static SimVec2 Zero => new(SimFixed.FromInt(0), SimFixed.FromInt(0));

        public SimVec2 Add(SimVec2 o) => new(X + o.X, Y + o.Y);
        public SimVec2 Sub(SimVec2 o) => new(X - o.X, Y - o.Y);
        public SimVec2 Mul(float s) => new(X * s, Y * s);

        public SimFixed LengthSq()
        {
            var x = X.ToFloat();
            var y = Y.ToFloat();
            return SimFixed.FromFloat(x * x + y * y);
        }

        public SimVec2 Normalized()
        {
            var x = X.ToFloat();
            var y = Y.ToFloat();
            var len = System.MathF.Sqrt(x * x + y * y);
            if (len < 1e-6f) return Zero;
            return new SimVec2(SimFixed.FromFloat(x / len), SimFixed.FromFloat(y / len));
        }

        public static SimVec2 operator +(SimVec2 a, SimVec2 b) => a.Add(b);
        public static SimVec2 operator -(SimVec2 a, SimVec2 b) => a.Sub(b);
    }
}
