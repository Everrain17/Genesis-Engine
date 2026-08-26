using System;

namespace GenesisEngine.Core
{
    public struct Vector2
    {
        public int X, Y;

        public Vector2(int x, int y) { X = x; Y = y; }

        public float Distance(Vector2 other)
        {
            int dx = X - other.X;
            int dy = Y - other.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public override bool Equals(object obj) => obj is Vector2 v && this == v;
        public override int GetHashCode() => X * 100000 + Y;
        public override string ToString() => $"({X}, {Y})";
    }
}